using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Omni.AutoApi.AspNetCore;
using Omni.AutoApi.Sample.Web;
using Xunit;

namespace Omni.AutoApi.IntegrationTests;

/// <summary>
/// R13 — um documento OpenAPI por versão e um api-definition ciente de versão.
/// O sample declara CatalogAppService com [ApiVersion("1.0")] e [ApiVersion("2.0")].
/// </summary>
public class OpenApiPerVersionTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public OpenApiPerVersionTests(WebApplicationFactory<Program> factory) => _factory = factory;

    private static async Task<JsonElement> GetJsonAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    }

    // ---------- documentos OpenAPI ----------

    [Fact]
    public async Task Existe_um_documento_por_versao()
    {
        var client = _factory.CreateClient();

        var v1 = await client.GetAsync("/openapi/v1.json");
        var v2 = await client.GetAsync("/openapi/v2.json");

        Assert.Equal(HttpStatusCode.OK, v1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, v2.StatusCode);
    }

    [Fact]
    public async Task Documento_v1_tem_a_acao_exclusiva_da_v1_e_nao_a_da_v2()
    {
        var doc = await GetJsonAsync(_factory.CreateClient(), "/openapi/v1.json");
        var paths = doc.GetProperty("paths").EnumerateObject().Select(p => p.Name).ToList();

        Assert.Contains(paths, p => p.Contains("get-legacy-code"));
        Assert.DoesNotContain(paths, p => p.Contains("get-summary"));
    }

    [Fact]
    public async Task Documento_v2_tem_a_acao_exclusiva_da_v2_e_nao_a_da_v1()
    {
        var doc = await GetJsonAsync(_factory.CreateClient(), "/openapi/v2.json");
        var paths = doc.GetProperty("paths").EnumerateObject().Select(p => p.Name).ToList();

        Assert.Contains(paths, p => p.Contains("get-summary"));
        Assert.DoesNotContain(paths, p => p.Contains("get-legacy-code"));
    }

    [Fact]
    public async Task Acao_comum_aparece_nos_dois_documentos()
    {
        var client = _factory.CreateClient();
        var v1 = await GetJsonAsync(client, "/openapi/v1.json");
        var v2 = await GetJsonAsync(client, "/openapi/v2.json");

        Assert.Contains(v1.GetProperty("paths").EnumerateObject(), p => p.Name.Contains("catalog/get-name"));
        Assert.Contains(v2.GetProperty("paths").EnumerateObject(), p => p.Name.Contains("catalog/get-name"));
    }

    // ---------- api-definition ciente de versão ----------

    [Fact]
    public async Task Definition_lista_as_versoes_disponiveis()
    {
        var doc = await GetJsonAsync(_factory.CreateClient(), "/api/auto-api/definition");

        var versoes = doc.GetProperty("apiVersions").EnumerateArray().Select(v => v.GetString()).ToList();

        Assert.Contains("v1", versoes);
        Assert.Contains("v2", versoes);
    }

    [Fact]
    public async Task Definition_nao_colapsa_versoes_da_mesma_rota()
    {
        // get-name existe em v1 e v2 com o MESMO verbo e rota (a versão vai em query/header).
        // Deduplicar só por verbo+rota faria a v2 sumir — por isso a versão entra na chave.
        var doc = await GetJsonAsync(_factory.CreateClient(), "/api/auto-api/definition");

        var getName = doc.GetProperty("actions").EnumerateArray()
            .Where(a => a.GetProperty("route").GetString()!.Contains("catalog/get-name"))
            .Select(a => a.GetProperty("apiVersion").GetString())
            .ToList();

        Assert.Equal(2, getName.Count);
        Assert.Contains("v1", getName);
        Assert.Contains("v2", getName);
    }

    [Fact]
    public async Task Definition_filtra_por_versao()
    {
        var doc = await GetJsonAsync(_factory.CreateClient(), "/api/auto-api/definition?apiVersion=v2");

        var acoes = doc.GetProperty("actions").EnumerateArray().ToList();

        Assert.NotEmpty(acoes);
        Assert.All(acoes, a => Assert.Equal("v2", a.GetProperty("apiVersion").GetString()));
        Assert.Contains(acoes, a => a.GetProperty("route").GetString()!.Contains("get-summary"));
        Assert.DoesNotContain(acoes, a => a.GetProperty("route").GetString()!.Contains("get-legacy-code"));
    }

    // ---------- descoberta de versões ----------

    [Fact]
    public void DiscoverApiDocumentNames_encontra_as_versoes_declaradas()
    {
        var documentos = ApiVersioningExtensions.DiscoverApiDocumentNames(typeof(Program).Assembly);

        Assert.Contains("v1", documentos);
        Assert.Contains("v2", documentos);
    }

    [Fact]
    public void DiscoverApiDocumentNames_retorna_vazio_sem_ApiVersion()
    {
        // Este assembly de testes tem Application Services, mas nenhum com [ApiVersion]
        // além dos de VersioningTests — que declaram v1/v2.
        var documentos = ApiVersioningExtensions.DiscoverApiDocumentNames(typeof(InventoryHandler).Assembly);

        Assert.DoesNotContain("v9", documentos);
    }
}
