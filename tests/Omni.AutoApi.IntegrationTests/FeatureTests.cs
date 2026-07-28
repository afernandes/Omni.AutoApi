using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Omni.AutoApi.Client;
using Omni.AutoApi.Sample.Web;
using Omni.AutoApi.Sample.Web.Services;
using Xunit;

namespace Omni.AutoApi.IntegrationTests;

public class FeatureTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public FeatureTests(WebApplicationFactory<Program> factory) => _factory = factory;

    // ---------- Upload (RemoteStreamContent -> multipart/form-data) ----------

    [Fact]
    public async Task Upload_via_raw_multipart_binds_RemoteStreamContent()
    {
        using var content = new MultipartFormDataContent
        {
            { new StringContent("hello world"), "content", "notes.txt" }
        };

        var response = await _factory.CreateClient().PostAsync("/api/app-service/todo/create-attachment", content);
        response.EnsureSuccessStatusCode();

        Assert.Contains("notes.txt:11", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Upload_via_generated_client_sends_multipart()
    {
        var client = new TodoAppServiceClient(_factory.CreateClient());
        var bytes = System.Text.Encoding.UTF8.GetBytes("hello world");

        var echo = await client.CreateAttachmentAsync(
            new RemoteStreamContent(new MemoryStream(bytes), "notes.txt", "text/plain"));

        Assert.Equal("notes.txt:11", echo);
    }

    [Fact]
    public async Task Upload_via_dynamic_proxy_sends_multipart()
    {
        var proxy = DynamicHttpProxy<ITodoAppService>.Create(_factory.CreateClient());
        var bytes = System.Text.Encoding.UTF8.GetBytes("abc");

        var echo = await proxy.CreateAttachmentAsync(
            new RemoteStreamContent(new MemoryStream(bytes), "a.bin", "application/octet-stream"));

        Assert.Equal("a.bin:3", echo);
    }

    // ---------- Streaming (IAsyncEnumerable<T>) ----------

    [Fact]
    public async Task Streaming_via_generated_client_yields_all_items()
    {
        var client = new TodoAppServiceClient(_factory.CreateClient());

        var titles = new List<string>();
        await foreach (var item in client.GetTodoStreamAsync())
        {
            titles.Add(item.Title);
        }

        Assert.Equal(new[] { "Stream 1", "Stream 2", "Stream 3" }, titles);
    }

    [Fact]
    public async Task Streaming_via_dynamic_proxy_yields_all_items()
    {
        var proxy = DynamicHttpProxy<ITodoAppService>.Create(_factory.CreateClient());

        var count = 0;
        await foreach (var _ in proxy.GetTodoStreamAsync())
        {
            count++;
        }

        Assert.Equal(3, count);
    }

    // ---------- [Authorize] declarativo em Auto API Controllers ----------

    [Fact]
    public async Task Authorize_attribute_returns_401_without_credentials()
    {
        var client = _factory.WithTestServices().CreateClient();

        var response = await client.GetAsync("/api/app-service/secured/get-secret");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Authorize_attribute_allows_authenticated_request()
    {
        var client = _factory.WithTestServices().CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "tester");

        var response = await client.GetAsync("/api/app-service/secured/get-secret");
        response.EnsureSuccessStatusCode();

        Assert.Contains("42", await response.Content.ReadAsStringAsync());
    }

    // ---------- Registro de DI gerado (AddHttpClient/typed client) ----------

    [Fact]
    public void Generated_registration_resolves_typed_client_via_factory()
    {
        var services = new ServiceCollection();
        Omni.AutoApi.Client.Generated.GeneratedClientRegistrations.AddTodoAppServiceClient(
            services, (_, http) => http.BaseAddress = new Uri("http://localhost/"));

        using var provider = services.BuildServiceProvider();
        var resolved = provider.GetRequiredService<ITodoAppService>();

        Assert.IsType<TodoAppServiceClient>(resolved);
    }
}
