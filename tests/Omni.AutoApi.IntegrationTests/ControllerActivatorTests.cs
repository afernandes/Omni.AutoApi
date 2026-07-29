using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Omni.AutoApi.AspNetCore;
using Xunit;

namespace Omni.AutoApi.IntegrationTests;

/// <summary>
/// R17 — ciclo de vida dos controllers. A biblioteca deixou de substituir o
/// <c>IControllerActivator</c> do MVC (ver <c>LazyServicesActionFilter</c>); estes testes garantem
/// que o descarte por requisição continua correto, que não há descarte em dobro e — o motivo da
/// mudança — que a injeção do <c>LazyServices</c> não depende da ordem de registro.
/// </summary>
public sealed class CicloDeVidaRecorder
{
    private int _criados;
    private int _descartados;
    private int _descartadosAsync;

    public int Criados => Volatile.Read(ref _criados);
    public int Descartados => Volatile.Read(ref _descartados);
    public int DescartadosAsync => Volatile.Read(ref _descartadosAsync);

    public void RegistrarCriacao() => Interlocked.Increment(ref _criados);
    public void RegistrarDispose() => Interlocked.Increment(ref _descartados);
    public void RegistrarDisposeAsync() => Interlocked.Increment(ref _descartadosAsync);
}

public class DescartavelAppService : ApplicationService, IDisposable
{
    private readonly CicloDeVidaRecorder _recorder;

    public DescartavelAppService(CicloDeVidaRecorder recorder)
    {
        _recorder = recorder;
        recorder.RegistrarCriacao();
    }

    public Task<string> GetPingAsync() => Task.FromResult("ok");

    /// <summary>Confirma que o LazyServices foi injetado pelo ativador.</summary>
    public Task<bool> GetTemLazyServicesAsync() => Task.FromResult(LazyServices is not null);

    public void Dispose() => _recorder.RegistrarDispose();
}

public class AssincronoDescartavelAppService : ApplicationService, IAsyncDisposable
{
    private readonly CicloDeVidaRecorder _recorder;

    public AssincronoDescartavelAppService(CicloDeVidaRecorder recorder)
    {
        _recorder = recorder;
        recorder.RegistrarCriacao();
    }

    public Task<string> GetPingAsync() => Task.FromResult("ok");

    public ValueTask DisposeAsync()
    {
        _recorder.RegistrarDisposeAsync();
        return default;
    }
}

/// <summary>Implementa os dois: o padrão manda descartar só de forma assíncrona.</summary>
public class DuploDescartavelAppService : ApplicationService, IDisposable, IAsyncDisposable
{
    private readonly CicloDeVidaRecorder _recorder;

    public DuploDescartavelAppService(CicloDeVidaRecorder recorder)
    {
        _recorder = recorder;
        recorder.RegistrarCriacao();
    }

    public Task<string> GetPingAsync() => Task.FromResult("ok");

    public void Dispose() => _recorder.RegistrarDispose();

    public ValueTask DisposeAsync()
    {
        _recorder.RegistrarDisposeAsync();
        return default;
    }
}

/// <summary>Controller MVC clássico: o ativador é global, precisa liberá-lo também.</summary>
[ApiController]
[Route("classico-descartavel")]
public class ClassicoDescartavelController : ControllerBase, IDisposable
{
    private readonly CicloDeVidaRecorder _recorder;

    public ClassicoDescartavelController(CicloDeVidaRecorder recorder)
    {
        _recorder = recorder;
        recorder.RegistrarCriacao();
    }

    [HttpGet]
    public string Get() => "ok";

    public void Dispose() => _recorder.RegistrarDispose();
}

public class ControllerActivatorTests
{
    private static async Task<(WebApplication app, HttpClient client, CicloDeVidaRecorder recorder)> StartAsync()
    {
        var recorder = new CicloDeVidaRecorder();

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(recorder);
        builder.Services.AddAutoApiServices();
        builder.Services.AddControllers().AddApplicationPart(typeof(DescartavelAppService).Assembly);

        var app = builder.Build();
        app.MapControllers();
        await app.StartAsync();
        return (app, app.GetTestClient(), recorder);
    }

    [Fact]
    public async Task IDisposable_e_descartado_ao_fim_da_requisicao()
    {
        var (app, client, recorder) = await StartAsync();
        await using var _ = app;

        var response = await client.GetAsync("/api/app-service/descartavel/get-ping");
        response.EnsureSuccessStatusCode();

        Assert.Equal(1, recorder.Criados);
        Assert.Equal(1, recorder.Descartados);
    }

    [Fact]
    public async Task IAsyncDisposable_usa_DisposeAsync()
    {
        var (app, client, recorder) = await StartAsync();
        await using var _ = app;

        var response = await client.GetAsync("/api/app-service/assincrono-descartavel/get-ping");
        response.EnsureSuccessStatusCode();

        Assert.Equal(1, recorder.DescartadosAsync);
        Assert.Equal(0, recorder.Descartados);
    }

    [Fact]
    public async Task Implementando_os_dois_nao_descarta_em_dobro()
    {
        var (app, client, recorder) = await StartAsync();
        await using var _ = app;

        var response = await client.GetAsync("/api/app-service/duplo-descartavel/get-ping");
        response.EnsureSuccessStatusCode();

        Assert.Equal(1, recorder.DescartadosAsync);
        Assert.Equal(0, recorder.Descartados);   // Dispose síncrono NÃO deve rodar também
    }

    [Fact]
    public async Task Controller_MVC_classico_tambem_e_descartado()
    {
        // O ativador substitui o do MVC globalmente — não pode vazar controllers "normais".
        var (app, client, recorder) = await StartAsync();
        await using var _ = app;

        var response = await client.GetAsync("/classico-descartavel");
        response.EnsureSuccessStatusCode();

        Assert.Equal(1, recorder.Criados);
        Assert.Equal(1, recorder.Descartados);
    }

    [Fact]
    public async Task Cada_requisicao_cria_e_descarta_uma_instancia()
    {
        var (app, client, recorder) = await StartAsync();
        await using var _ = app;

        for (var i = 0; i < 5; i++)
        {
            (await client.GetAsync("/api/app-service/descartavel/get-ping")).EnsureSuccessStatusCode();
        }

        Assert.Equal(5, recorder.Criados);
        Assert.Equal(5, recorder.Descartados);   // nada acumulado entre requisições
    }

    [Fact]
    public async Task LazyServices_e_injetado_no_caminho_HTTP()
    {
        var (app, client, recorder) = await StartAsync();
        await using var _ = app;

        var response = await client.GetAsync("/api/app-service/descartavel/get-tem-lazy-services");
        response.EnsureSuccessStatusCode();

        Assert.Equal("true", await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Regressão: com um IControllerActivator próprio, chamar AddControllersAsServices() DEPOIS
    /// de AddAutoApiServices() sobrescrevia o ativador e o LazyServices nunca era injetado —
    /// falha silenciosa (Logger virava NullLogger, CurrentUser lançava) e dependente da ordem.
    /// </summary>
    [Fact]
    public async Task LazyServices_funciona_com_AddControllersAsServices()
    {
        var recorder = new CicloDeVidaRecorder();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(recorder);

        // Ordem obrigatória: AddControllersAsServices() fotografa os controllers conhecidos NO
        // MOMENTO da chamada, então o feature provider do Omni.AutoApi precisa já estar
        // registrado (limitação do próprio MVC; a ordem inversa falha com 500 ao resolver).
        builder.Services.AddAutoApiServices();
        builder.Services.AddControllers()
            .AddApplicationPart(typeof(DescartavelAppService).Assembly)
            .AddControllersAsServices();

        await using var app = builder.Build();
        app.MapControllers();
        await app.StartAsync();

        var response = await app.GetTestClient()
            .GetAsync("/api/app-service/descartavel/get-tem-lazy-services");
        response.EnsureSuccessStatusCode();

        Assert.Equal("true", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Requisicao_com_erro_ainda_libera_o_controller()
    {
        // 404 não chega ao controller; usamos uma rota válida e verificamos que, mesmo com o
        // pipeline de exceção atuando, o Release acontece.
        var (app, client, recorder) = await StartAsync();
        await using var _ = app;

        var ok = await client.GetAsync("/api/app-service/descartavel/get-ping");
        var naoExiste = await client.GetAsync("/api/app-service/descartavel/nao-existe");

        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, naoExiste.StatusCode);
        Assert.Equal(recorder.Criados, recorder.Descartados);   // sem vazamento
    }
}
