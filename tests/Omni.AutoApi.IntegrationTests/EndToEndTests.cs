using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Omni.AutoApi.Sample.Web;
using Xunit;

namespace Omni.AutoApi.IntegrationTests;

public class EndToEndTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public EndToEndTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task Normal_mvc_controller_works_under_custom_activator()
    {
        // WeatherForecastController tem ctor com ILogger -> valida que o pipeline do Omni.AutoApi
        // não quebra a criação de controllers normais.
        var response = await _factory.CreateClient().GetAsync("/WeatherForecast");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Auto_api_get_returns_data_and_exercises_enriched_base()
    {
        // get-todos usa Logger + CurrentUser (LazyServices injetado pelo ativador).
        var response = await _factory.CreateClient().GetAsync("/api/app-service/todo/get-todos");
        response.EnsureSuccessStatusCode();
        Assert.Contains("Teste 1", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Auto_api_post_binds_body()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync(
            "/api/app-service/todo/create-todo", new { title = "X", isCompleted = true });
        response.EnsureSuccessStatusCode();
        Assert.Contains("\"title\":\"X\"", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Business_exception_maps_to_problem_details_404()
    {
        var response = await _factory.CreateClient().GetAsync("/api/app-service/todo/get-todo?id=-1");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"code\":\"EntityNotFound\"", body);
        Assert.Contains("\"status\":404", body);
    }

    [Fact]
    public async Task Definition_endpoint_lists_actions_without_generated_client_duplicates()
    {
        var json = await _factory.CreateClient().GetStringAsync("/api/auto-api/definition");
        Assert.Contains("/api/app-service/todo/get-todos", json);
        Assert.DoesNotContain("todo-app-service-client", json);
    }
}
