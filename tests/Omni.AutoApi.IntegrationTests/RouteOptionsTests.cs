using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Omni.AutoApi.AspNetCore;
using Omni.AutoApi.Routing;
using Xunit;

namespace Omni.AutoApi.IntegrationTests;

/// <summary>
/// R6 — `RouteOptions` só era testado unitariamente (ApiRouteBuilder). Aqui a rota customizada
/// é exercitada por HTTP real: se servidor e cliente derivarem rotas diferentes, o sintoma em
/// produção seria 404.
/// </summary>
public class InventoryHandler : ApplicationService
{
    public Task<string> GetStatusAsync() => Task.FromResult("ok");
    public Task<int> GetCountAsync(int id) => Task.FromResult(id * 2);
}

public class RouteOptionsTests
{
    private static async Task<(WebApplication app, HttpClient client)> StartAsync(Action<RouteOptions> configure)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services.AddAutoApiServices(configure);
        builder.Services.AddControllers().AddApplicationPart(typeof(InventoryHandler).Assembly);

        var app = builder.Build();
        app.MapControllers();
        await app.StartAsync();
        return (app, app.GetTestClient());
    }

    [Fact]
    public async Task Prefixo_customizado_e_respeitado()
    {
        var (app, client) = await StartAsync(o => o.Prefix = "api/v2/services");
        await using var _ = app;

        var response = await client.GetAsync("/api/v2/services/inventory-handler/get-status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("ok", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Rota_padrao_nao_responde_quando_ha_prefixo_customizado()
    {
        var (app, client) = await StartAsync(o => o.Prefix = "api/v2/services");
        await using var _ = app;

        var response = await client.GetAsync("/api/app-service/inventory-handler/get-status");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Sufixo_customizado_e_removido_do_nome_do_controller()
    {
        // "InventoryHandler" + postfix "Handler" => "inventory"
        var (app, client) = await StartAsync(o =>
        {
            o.Prefix = "api/app-service";
            o.ControllerPostfixes = new[] { "Handler" };
        });
        await using var _ = app;

        var response = await client.GetAsync("/api/app-service/inventory/get-status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CamelCase_quando_UseKebabCase_e_falso()
    {
        var (app, client) = await StartAsync(o =>
        {
            o.Prefix = "api/app-service";
            o.UseKebabCase = false;
        });
        await using var _ = app;

        var camel = await client.GetAsync("/api/app-service/inventoryHandler/getStatus");
        var kebab = await client.GetAsync("/api/app-service/inventory-handler/get-status");

        Assert.Equal(HttpStatusCode.OK, camel.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, kebab.StatusCode);
    }

    [Fact]
    public async Task Binding_de_parametro_continua_valendo_com_rota_customizada()
    {
        var (app, client) = await StartAsync(o => o.Prefix = "svc");
        await using var _ = app;

        var response = await client.GetAsync("/svc/inventory-handler/get-count?id=21");

        response.EnsureSuccessStatusCode();
        Assert.Equal("42", await response.Content.ReadAsStringAsync());
    }
}
