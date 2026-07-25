using System.Net;
using Omni.AutoApi;
using Omni.AutoApi.Client;
using Xunit;

namespace Omni.AutoApi.Tests;

public class DynamicHttpProxyTests
{
    public enum Color { Red, Green }

    public class SearchFilter
    {
        public string? Status { get; set; }
        public List<string>? Tags { get; set; }
    }

    public interface IProbeAppService : IRemoteService
    {
        Task<int> GetByDateAsync(DateOnly date, Color color, string q);
        Task<int> GetSearchAsync(SearchFilter filter);
        Task<int> CreateThingAsync(SearchFilter body);
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest;
        public string? LastBody;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("1", System.Text.Encoding.UTF8, "application/json")
            };
        }
    }

    private static (IProbeAppService proxy, CapturingHandler handler) CreateProxy(string baseAddress = "http://host/")
    {
        var handler = new CapturingHandler();
        var client = new HttpClient(handler) { BaseAddress = new Uri(baseAddress) };
        return (DynamicHttpProxy<IProbeAppService>.Create(client), handler);
    }

    [Fact]
    public async Task Formats_DateOnly_as_iso8601_and_enum_by_name()
    {
        var (proxy, handler) = CreateProxy();

        await proxy.GetByDateAsync(new DateOnly(2026, 6, 24), Color.Green, "x");

        var url = handler.LastRequest!.RequestUri!.ToString();
        Assert.Contains("date=2026-06-24", url);   // ISO-8601, não MM/dd/yyyy
        Assert.Contains("color=Green", url);        // enum pelo nome
    }

    [Fact]
    public async Task Escapes_special_characters_in_query()
    {
        var (proxy, handler) = CreateProxy();

        await proxy.GetByDateAsync(new DateOnly(2026, 1, 1), Color.Red, "a b&c=d?e");

        var query = handler.LastRequest!.RequestUri!.Query;
        Assert.Contains("q=a%20b%26c%3Dd%3Fe", query);
    }

    [Fact]
    public async Task Expands_collections_in_complex_query_object()
    {
        var (proxy, handler) = CreateProxy();

        await proxy.GetSearchAsync(new SearchFilter { Status = "open", Tags = new List<string> { "a", "b" } });

        var query = handler.LastRequest!.RequestUri!.Query;
        Assert.Contains("Status=open", query);
        Assert.Contains("Tags=a", query);
        Assert.Contains("Tags=b", query);
    }

    [Fact]
    public async Task Throws_clear_error_when_BaseAddress_is_missing()
    {
        var proxy = DynamicHttpProxy<IProbeAppService>.Create(new HttpClient(new CapturingHandler()));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => proxy.GetByDateAsync(new DateOnly(2026, 1, 1), Color.Red, "x"));

        Assert.Contains("BaseAddress", ex.Message);
    }

    [Fact]
    public async Task Normalizes_BaseAddress_without_trailing_slash()
    {
        var (proxy, handler) = CreateProxy("http://host/app");   // sem barra final

        await proxy.GetByDateAsync(new DateOnly(2026, 1, 1), Color.Red, "x");

        // Sem a normalização, "app" seria descartado ("http://host/api/...").
        Assert.StartsWith("http://host/app/api/app-service/probe/", handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task Sends_complex_parameter_as_json_body_on_post()
    {
        var (proxy, handler) = CreateProxy();

        await proxy.CreateThingAsync(new SearchFilter { Status = "novo" });

        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Contains("\"status\":\"novo\"", handler.LastBody);
    }
}
