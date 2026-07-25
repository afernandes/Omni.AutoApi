extern alias gen;
using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Omni.AutoApi.Tests;

public class GeneratorTests
{
    [Fact]
    public void Generates_client_with_expected_routes_and_verbs()
    {
        const string source = @"
using System.Threading.Tasks;
using Omni.AutoApi;
namespace Demo
{
    [AutoApiClient]
    public interface ITodoAppService : IRemoteService
    {
        Task<int> GetCountAsync();
        Task DeleteAsync(int id);
    }
}";
        var output = Run(source, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains("class TodoAppServiceClient", output);
        Assert.Contains("api/app-service/todo/get-count", output);
        Assert.Contains("global::System.Net.Http.HttpMethod.Delete", output);
    }

    [Fact]
    public void Reports_diagnostic_for_generic_method()
    {
        const string source = @"
using System.Threading.Tasks;
using Omni.AutoApi;
namespace Demo
{
    [AutoApiClient]
    public interface IThing : IRemoteService
    {
        Task<T> GetAsync<T>(int id);
    }
}";
        Run(source, out var diagnostics);
        Assert.Contains(diagnostics, d => d.Id == "AUTOAPI001");
    }

    [Fact]
    public void Honors_custom_prefix_and_postfixes_from_msbuild()
    {
        const string source = @"
using System.Threading.Tasks;
using Omni.AutoApi;
namespace Demo
{
    [AutoApiClient]
    public interface ITodoManagerHandler : IRemoteService
    {
        Task<int> GetCountAsync();
    }
}";
        var output = Run(source, out _, new Dictionary<string, string>
        {
            ["build_property.AutoApiRoutePrefix"] = "api/services",
            ["build_property.AutoApiControllerPostfixes"] = "Handler"
        });

        // I + Handler removidos => "TodoManager" => "todo-manager", prefixo custom aplicado.
        Assert.Contains("api/services/todo-manager/get-count", output);
    }

    [Fact]
    public void Generates_multipart_upload_for_RemoteStreamContent()
    {
        const string source = @"
using System.Threading.Tasks;
using Omni.AutoApi;
namespace Demo
{
    [AutoApiClient]
    public interface IFileAppService : IRemoteService
    {
        Task<string> CreateFileAsync(RemoteStreamContent content);
    }
}";
        var output = Run(source, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.DoesNotContain(diagnostics, d => d.Id == "AUTOAPI001");
        Assert.Contains("MultipartFormDataContent", output);
        Assert.Contains("StreamContent", output);
    }

    [Fact]
    public void Generates_async_stream_method_for_IAsyncEnumerable()
    {
        const string source = @"
using System.Collections.Generic;
using System.Threading;
using Omni.AutoApi;
namespace Demo
{
    [AutoApiClient]
    public interface IFeedAppService : IRemoteService
    {
        IAsyncEnumerable<int> GetNumbersStreamAsync(CancellationToken cancellationToken);
    }
}";
        var output = Run(source, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.DoesNotContain(diagnostics, d => d.Id == "AUTOAPI001");
        Assert.Contains("DeserializeAsyncEnumerable", output);
        Assert.Contains("EnumeratorCancellation", output);
        Assert.Contains("ResponseHeadersRead", output);
    }

    [Fact]
    public void Reports_diagnostic_for_raw_stream_parameter()
    {
        const string source = @"
using System.IO;
using System.Threading.Tasks;
using Omni.AutoApi;
namespace Demo
{
    [AutoApiClient]
    public interface IBadUpload : IRemoteService
    {
        Task CreateRawAsync(Stream data);
    }
}";
        Run(source, out var diagnostics);

        var diag = Assert.Single(diagnostics, d => d.Id == "AUTOAPI001");
        Assert.Contains("RemoteStreamContent", diag.GetMessage());
    }

    [Fact]
    public void Registration_uses_IHttpClientFactory_when_available()
    {
        const string source = @"
using System.Threading.Tasks;
using Omni.AutoApi;
namespace Demo
{
    [AutoApiClient]
    public interface ITodoAppService : IRemoteService
    {
        Task<int> GetCountAsync();
    }
}";
        var output = Run(source, out _);   // M.E.Http está no TPA (via Omni.AutoApi.Client)

        Assert.Contains("AddTypedClient", output);
        Assert.Contains("IHttpClientBuilder", output);
    }

    [Fact]
    public void Aggregate_has_per_service_overload()
    {
        const string source = @"
using System.Threading.Tasks;
using Omni.AutoApi;
namespace Demo
{
    [AutoApiClient]
    public interface ITodoAppService : IRemoteService { Task<int> GetCountAsync(); }
    [AutoApiClient]
    public interface IOrderAppService : IRemoteService { Task<int> GetTotalAsync(); }
}";
        var output = Run(source, out _);

        Assert.Contains("global::System.Type> configureClient", output);
        Assert.Contains("typeof(global::Demo.ITodoAppService)", output);
        Assert.Contains("typeof(global::Demo.IOrderAppService)", output);
    }

    [Fact]
    public void Emits_per_client_and_aggregate_di_extensions()
    {
        const string source = @"
using System.Threading.Tasks;
using Omni.AutoApi;
namespace Demo
{
    [AutoApiClient]
    public interface ITodoAppService : IRemoteService
    {
        Task<int> GetCountAsync();
    }
}";
        var output = Run(source, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains("AddTodoAppServiceClient", output);
        Assert.Contains("AddAllAutoApiClients", output);
        Assert.Contains("GeneratedClientRegistrations", output);
    }

    private static string Run(string source, out ImmutableArray<Diagnostic> diagnostics,
        Dictionary<string, string>? buildProperties = null)
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .ToList();
        references.Add(MetadataReference.CreateFromFile(typeof(Omni.AutoApi.IRemoteService).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(
            typeof(Microsoft.Extensions.DependencyInjection.IServiceCollection).Assembly.Location));

        var compilation = CSharpCompilation.Create(
            "GeneratorTestAssembly",
            new[] { CSharpSyntaxTree.ParseText(source) },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: new[] { new gen::Omni.AutoApi.Client.SourceGenerator.AutoApiClientGenerator().AsSourceGenerator() },
            additionalTexts: null,
            parseOptions: null,
            optionsProvider: buildProperties is null ? null : new TestConfigOptionsProvider(buildProperties));

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var updated, out diagnostics);

        return string.Join("\n", updated.SyntaxTrees.Select(t => t.ToString()));
    }

    private sealed class TestConfigOptionsProvider : Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptionsProvider
    {
        private readonly TestConfigOptions _global;
        public TestConfigOptionsProvider(Dictionary<string, string> values) => _global = new TestConfigOptions(values);
        public override Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptions GlobalOptions => _global;
        public override Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _global;
        public override Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptions GetOptions(AdditionalText textFile) => _global;
    }

    private sealed class TestConfigOptions : Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptions
    {
        private readonly Dictionary<string, string> _values;
        public TestConfigOptions(Dictionary<string, string> values) => _values = values;
        public override bool TryGetValue(string key, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? value)
        {
            if (_values.TryGetValue(key, out var v)) { value = v; return true; }
            value = null;
            return false;
        }
    }
}
