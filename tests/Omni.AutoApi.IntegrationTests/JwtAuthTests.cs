using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Omni.AutoApi.Client;
using Omni.AutoApi.Sample.Web;
using Omni.AutoApi.Sample.Web.Contracts;
using Omni.AutoApi.Sample.Web.Services;
using Xunit;

namespace Omni.AutoApi.IntegrationTests;

/// <summary>
/// R11 — cenário de autenticação real (JWT Bearer + policy), incluindo o lado cliente
/// anexando o token via <see cref="AuthTokenHandler"/>.
/// </summary>
public class JwtAuthTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public JwtAuthTests(WebApplicationFactory<Program> factory) => _factory = factory;

    private static async Task<string> GetTokenAsync(HttpClient client, string user, params string[] roles)
    {
        var response = await client.PostAsJsonAsync("/dev/token", new { user, roles });
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<TokenResponse>();
        return payload!.AccessToken;
    }

    private sealed record TokenResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("access_token")] string AccessToken);

    // ---------- servidor ----------

    [Fact]
    public async Task Sem_token_retorna_401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/app-service/todo/create-secure-todo", new { title = "x" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Token_valido_autoriza()
    {
        var client = _factory.CreateClient();
        var token = await GetTokenAsync(client, "ana");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync(
            "/api/app-service/todo/create-secure-todo", new { title = "comprar pão" });

        response.EnsureSuccessStatusCode();
        Assert.Contains("comprar pão", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Token_invalido_retorna_401()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "token.falso.aqui");

        var response = await client.PostAsJsonAsync(
            "/api/app-service/todo/create-secure-todo", new { title = "x" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Sem_a_role_exigida_retorna_403()
    {
        var client = _factory.CreateClient();
        var token = await GetTokenAsync(client, "ana");   // autenticada, mas sem role admin
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.DeleteAsync("/api/app-service/todo/delete-all-todos");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Com_a_role_exigida_autoriza()
    {
        var client = _factory.CreateClient();
        var token = await GetTokenAsync(client, "root", "admin");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.DeleteAsync("/api/app-service/todo/delete-all-todos");

        Assert.True(response.IsSuccessStatusCode, $"esperava sucesso, veio {response.StatusCode}");
    }

    [Fact]
    public async Task Endpoint_publico_continua_acessivel_sem_token()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/app-service/todo/get-todos");

        response.EnsureSuccessStatusCode();
    }

    // ---------- cliente ----------

    [Fact]
    public async Task AuthTokenHandler_anexa_o_bearer_no_cliente_gerado()
    {
        var raw = _factory.CreateClient();
        var token = await GetTokenAsync(raw, "ana");

        // Encadeia o handler exatamente como um consumidor faria.
        var handler = new AuthTokenHandler(() => token) { InnerHandler = _factory.Server.CreateHandler() };
        var http = new HttpClient(handler) { BaseAddress = _factory.Server.BaseAddress };
        var client = new TodoAppServiceClient(http);

        var criado = await client.CreateSecureTodoAsync(new CreateTodoDto { Title = "via handler" });

        Assert.Equal("via handler", criado.Title);
    }

    [Fact]
    public async Task AuthTokenHandler_tambem_funciona_no_proxy_dinamico()
    {
        var raw = _factory.CreateClient();
        var token = await GetTokenAsync(raw, "ana");

        var handler = new AuthTokenHandler(() => token) { InnerHandler = _factory.Server.CreateHandler() };
        var http = new HttpClient(handler) { BaseAddress = _factory.Server.BaseAddress };
        var proxy = DynamicHttpProxy<ITodoAppService>.Create(http);

        var criado = await proxy.CreateSecureTodoAsync(new CreateTodoDto { Title = "via proxy" });

        Assert.Equal("via proxy", criado.Title);
    }

    [Fact]
    public async Task Sem_o_handler_a_chamada_falha_com_401()
    {
        var http = _factory.CreateClient();
        var client = new TodoAppServiceClient(http);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.CreateSecureTodoAsync(new CreateTodoDto { Title = "x" }));

        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
    }
}
