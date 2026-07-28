using System.Net;
using Asp.Versioning;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Omni.AutoApi.AspNetCore;
using Xunit;

namespace Omni.AutoApi.IntegrationTests;

/// <summary>
/// R5 — cobre o caminho completo do versionamento: a convenção manipula Selectors e
/// EndpointMetadata, exatamente onde o Asp.Versioning também atua (foi um conflito desse
/// tipo que quebrou o [Authorize] silenciosamente).
/// </summary>
[ApiVersion("1.0")]
[ApiVersion("2.0")]
public class CatalogAppService : ApplicationService
{
    public Task<string> GetLabelAsync() => Task.FromResult("v-neutral");

    [MapToApiVersion("1.0")]
    public Task<string> GetOnlyV1Async() => Task.FromResult("só-v1");

    [MapToApiVersion("2.0")]
    public Task<string> GetOnlyV2Async() => Task.FromResult("só-v2");
}

public class VersioningTests
{
    /// <summary>Host próprio: o sample não liga versionamento (é opt-in).</summary>
    private static async Task<(WebApplication app, HttpClient client)> StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services.AddAutoApiServices();
        builder.Services.AddAutoApiVersioning();
        builder.Services.AddControllers().AddApplicationPart(typeof(CatalogAppService).Assembly);

        var app = builder.Build();
        app.MapControllers();
        await app.StartAsync();
        return (app, app.GetTestClient());
    }

    [Fact]
    public async Task Versao_por_query_string_funciona()
    {
        var (app, client) = await StartAsync();
        await using var _ = app;

        var v1 = await client.GetAsync("/api/app-service/catalog/get-label?api-version=1.0");
        var v2 = await client.GetAsync("/api/app-service/catalog/get-label?api-version=2.0");

        Assert.Equal(HttpStatusCode.OK, v1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, v2.StatusCode);
    }

    [Fact]
    public async Task Versao_por_header_funciona()
    {
        var (app, client) = await StartAsync();
        await using var _ = app;

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/app-service/catalog/get-label");
        request.Headers.Add("X-Api-Version", "2.0");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Sem_versao_usa_a_default()
    {
        // AddAutoApiVersioning configura AssumeDefaultVersionWhenUnspecified = true.
        var (app, client) = await StartAsync();
        await using var _ = app;

        var response = await client.GetAsync("/api/app-service/catalog/get-label");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task MapToApiVersion_isola_acoes_por_versao()
    {
        var (app, client) = await StartAsync();
        await using var _ = app;

        var okV1 = await client.GetAsync("/api/app-service/catalog/get-only-v1?api-version=1.0");
        var erroV1 = await client.GetAsync("/api/app-service/catalog/get-only-v1?api-version=2.0");

        Assert.Equal(HttpStatusCode.OK, okV1.StatusCode);
        Assert.NotEqual(HttpStatusCode.OK, erroV1.StatusCode);   // ação não existe na v2
    }

    [Fact]
    public async Task Versao_inexistente_e_rejeitada()
    {
        var (app, client) = await StartAsync();
        await using var _ = app;

        var response = await client.GetAsync("/api/app-service/catalog/get-label?api-version=9.0");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Versoes_suportadas_sao_reportadas_no_header()
    {
        // AddAutoApiVersioning liga ReportApiVersions.
        var (app, client) = await StartAsync();
        await using var _ = app;

        var response = await client.GetAsync("/api/app-service/catalog/get-label?api-version=1.0");

        Assert.True(response.Headers.Contains("api-supported-versions"));
        Assert.Contains("2.0", string.Join(",", response.Headers.GetValues("api-supported-versions")));
    }
}
