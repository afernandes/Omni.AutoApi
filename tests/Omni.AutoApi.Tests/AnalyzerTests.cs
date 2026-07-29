extern alias analyzers;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;
using AutoApiUsageAnalyzer = analyzers::Omni.AutoApi.Analyzers.AutoApiUsageAnalyzer;

namespace Omni.AutoApi.Tests;

/// <summary>
/// R14 — o analisador antecipa para a digitação erros que hoje só quebram no startup
/// (colisão de rota, múltiplos parâmetros de corpo).
/// </summary>
public class AnalyzerTests
{
    private const string Preambulo = @"
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Omni.AutoApi;
";

    private static async Task<ImmutableArray<Diagnostic>> AnalisarAsync(string corpo)
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .ToList();
        references.Add(MetadataReference.CreateFromFile(typeof(IRemoteService).Assembly.Location));

        var compilation = CSharpCompilation.Create(
            "AnalyzerTestAssembly",
            new[] { CSharpSyntaxTree.ParseText(Preambulo + corpo) },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var comAnalisador = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(new AutoApiUsageAnalyzer()));

        return await comAnalisador.GetAnalyzerDiagnosticsAsync();
    }

    // ---------- AUTOAPI002: colisão de rota ----------

    [Fact]
    public async Task Sobrecarga_gera_AUTOAPI002()
    {
        var diags = await AnalisarAsync(@"
public class TodoAppService : IRemoteService
{
    public Task<int> GetTodoAsync(int id) => Task.FromResult(id);
    public Task<int> GetTodoAsync(string slug) => Task.FromResult(0);
}");

        var colisoes = diags.Where(d => d.Id == "AUTOAPI002").ToList();
        Assert.Equal(2, colisoes.Count);                       // uma marcação por método
        Assert.Contains("GetTodoAsync", colisoes[0].GetMessage());
    }

    [Fact]
    public async Task Par_Foo_e_FooAsync_tambem_colide()
    {
        // Ambos viram a rota "get-todo": o sufixo Async é removido na derivação.
        var diags = await AnalisarAsync(@"
public class TodoAppService : IRemoteService
{
    public Task<int> GetTodoAsync(int id) => Task.FromResult(id);
    public Task<int> GetTodo(int id) => Task.FromResult(id);
}");

        Assert.NotEmpty(diags.Where(d => d.Id == "AUTOAPI002"));
    }

    [Fact]
    public async Task Rota_explicita_desambigua_e_nao_alerta()
    {
        var diags = await AnalisarAsync(@"
public class HttpGetAttribute : System.Attribute { public HttpGetAttribute(string template) { } }

public class TodoAppService : IRemoteService
{
    public Task<int> GetTodoAsync(int id) => Task.FromResult(id);
    [HttpGet(""por-slug"")] public Task<int> GetTodoAsync(string slug) => Task.FromResult(0);
}");

        Assert.Empty(diags.Where(d => d.Id == "AUTOAPI002"));
    }

    [Fact]
    public async Task Metodos_distintos_nao_alertam()
    {
        var diags = await AnalisarAsync(@"
public class TodoAppService : IRemoteService
{
    public Task<int> GetTodoAsync(int id) => Task.FromResult(id);
    public Task<int> CreateTodoAsync(int id) => Task.FromResult(id);
}");

        Assert.Empty(diags.Where(d => d.Id == "AUTOAPI002"));
    }

    // ---------- AUTOAPI003: múltiplos parâmetros de corpo ----------

    [Fact]
    public async Task Dois_parametros_complexos_em_POST_geram_AUTOAPI003()
    {
        var diags = await AnalisarAsync(@"
public class Dto1 { public string? Nome { get; set; } }
public class Dto2 { public string? Nome { get; set; } }

public class PedidoAppService : IRemoteService
{
    public Task CreatePedidoAsync(Dto1 a, Dto2 b) => Task.CompletedTask;
}");

        var d = Assert.Single(diags.Where(x => x.Id == "AUTOAPI003"));
        Assert.Contains("a, b", d.GetMessage());
    }

    [Fact]
    public async Task Um_complexo_mais_simples_e_CancellationToken_nao_alerta()
    {
        var diags = await AnalisarAsync(@"
public class Dto1 { public string? Nome { get; set; } }

public class PedidoAppService : IRemoteService
{
    public Task CreatePedidoAsync(int id, Dto1 corpo, CancellationToken ct) => Task.CompletedTask;
}");

        Assert.Empty(diags.Where(d => d.Id == "AUTOAPI003"));
    }

    [Fact]
    public async Task Complexos_em_GET_nao_alertam_pois_viram_query()
    {
        var diags = await AnalisarAsync(@"
public class Filtro1 { public string? Nome { get; set; } }
public class Filtro2 { public string? Nome { get; set; } }

public class BuscaAppService : IRemoteService
{
    public Task GetBuscaAsync(Filtro1 a, Filtro2 b) => Task.CompletedTask;
}");

        Assert.Empty(diags.Where(d => d.Id == "AUTOAPI003"));
    }

    // ---------- AUTOAPI004: retorno não assíncrono ----------

    [Fact]
    public async Task Metodo_sincrono_gera_AUTOAPI004_informativo()
    {
        var diags = await AnalisarAsync(@"
public class TodoAppService : IRemoteService
{
    public string GetNome() => ""x"";
}");

        var d = Assert.Single(diags.Where(x => x.Id == "AUTOAPI004"));
        Assert.Equal(DiagnosticSeverity.Info, d.Severity);
    }

    [Fact]
    public async Task IAsyncEnumerable_e_ValueTask_nao_alertam()
    {
        var diags = await AnalisarAsync(@"
public class FeedAppService : IRemoteService
{
    public IAsyncEnumerable<int> GetStreamAsync() => null!;
    public ValueTask<int> GetCountAsync() => new ValueTask<int>(1);
    public Task GetPingAsync() => Task.CompletedTask;
}");

        Assert.Empty(diags.Where(d => d.Id == "AUTOAPI004"));
    }

    // ---------- escopo ----------

    [Fact]
    public async Task Classe_que_nao_e_IRemoteService_e_ignorada()
    {
        var diags = await AnalisarAsync(@"
public class Qualquer
{
    public Task<int> GetTodoAsync(int id) => Task.FromResult(id);
    public Task<int> GetTodoAsync(string slug) => Task.FromResult(0);
    public string GetNome() => ""x"";
}");

        Assert.Empty(diags);
    }

    [Fact]
    public async Task Cliente_gerado_e_ignorado()
    {
        // Implementa a interface remota, mas é o lado CLIENTE.
        var diags = await AnalisarAsync(@"
[AutoApiGeneratedClient]
public class TodoAppServiceClient : IRemoteService
{
    public string GetNome() => ""x"";
}");

        Assert.Empty(diags);
    }
}
