using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Omni.AutoApi.Sample.Web;
using Xunit;

namespace Omni.AutoApi.IntegrationTests;

public class ValidationAndErrorTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ValidationAndErrorTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task Invalid_dto_returns_400_validation_problem_details()
    {
        // Title é [Required]: ausente -> 400 no formato ProblemDetails do Omni.AutoApi.
        var response = await _factory.CreateClient().PostAsJsonAsync(
            "/api/app-service/todo/create-todo", new { isCompleted = true });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"code\":\"ValidationError\"", body);
        Assert.Contains("Title", body);                 // erro por campo
        Assert.Contains("\"status\":400", body);
    }

    [Fact]
    public async Task ArgumentException_maps_to_400_with_masked_detail()
    {
        var client = _factory.WithTestServices().CreateClient();

        var response = await client.GetAsync("/api/app-service/faulty/get-argument-error");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("\"code\":\"InvalidArgument\"", body);
        Assert.DoesNotContain("detalhe interno sensível", body);   // 4xx de framework mascarado
    }

    [Fact]
    public async Task BusinessException_maps_to_409_preserving_message()
    {
        var client = _factory.WithTestServices().CreateClient();

        var response = await client.GetAsync("/api/app-service/faulty/get-business-error");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("Saldo insuficiente", body);   // mensagem de negócio é exposta
    }

    [Fact]
    public async Task NotImplementedException_maps_to_501()
    {
        var client = _factory.WithTestServices().CreateClient();

        var response = await client.GetAsync("/api/app-service/faulty/get-not-implemented");

        Assert.Equal(HttpStatusCode.NotImplemented, response.StatusCode);
    }

    [Fact]
    public async Task Unhandled_exception_maps_to_500_with_masked_detail()
    {
        var client = _factory.WithTestServices().CreateClient();

        var response = await client.GetAsync("/api/app-service/faulty/get-boom");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("\"code\":\"ServerError\"", body);
        Assert.DoesNotContain("stack trace interno", body);
    }
}
