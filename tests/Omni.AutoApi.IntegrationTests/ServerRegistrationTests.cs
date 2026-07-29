using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Omni.AutoApi.AspNetCore;
using Xunit;

namespace Omni.AutoApi.IntegrationTests;

/// <summary>
/// R12 — `AddAutoApiServer` registra o Application Service para uso in-process (Blazor Server)
/// além da exposição HTTP, preenchendo o `LazyServices` que o ativador do MVC injetaria.
/// </summary>
public interface IReportAppService : IRemoteService
{
    Task<string> GetTitleAsync();
    Task<string> GetUserAsync();
}

public class ReportAppService : ApplicationService, IReportAppService
{
    public Task<string> GetTitleAsync()
    {
        Logger.LogInformation("gerando relatório");   // exige LazyServices
        return Task.FromResult("Relatório");
    }

    public Task<string> GetUserAsync()
        => Task.FromResult(CurrentUser.IsAuthenticated ? "autenticado" : "anônimo");
}

public class ServerRegistrationTests
{
    private static async Task<WebApplication> StartAsync(Action<IServiceCollection> configure)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddAutoApiServices();
        configure(builder.Services);

        var app = builder.Build();
        app.MapControllers();
        await app.StartAsync();
        return app;
    }

    [Fact]
    public async Task Resolve_o_servico_in_process_pelo_contrato()
    {
        await using var app = await StartAsync(s => s.AddAutoApiServer<IReportAppService, ReportAppService>());

        using var scope = app.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IReportAppService>();

        Assert.IsType<ReportAppService>(service);
        Assert.Equal("Relatório", await service.GetTitleAsync());
    }

    [Fact]
    public async Task LazyServices_e_injetado_no_uso_in_process()
    {
        // Sem o wiring, CurrentUser lançaria InvalidOperationException.
        await using var app = await StartAsync(s => s.AddAutoApiServer<IReportAppService, ReportAppService>());

        using var scope = app.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IReportAppService>();

        Assert.Equal("anônimo", await service.GetUserAsync());
    }

    [Fact]
    public async Task Contrato_e_concreto_resolvem_a_mesma_instancia_no_escopo()
    {
        await using var app = await StartAsync(s => s.AddAutoApiServer<IReportAppService, ReportAppService>());

        using var scope = app.Services.CreateScope();
        var porContrato = scope.ServiceProvider.GetRequiredService<IReportAppService>();
        var porConcreto = scope.ServiceProvider.GetRequiredService<ReportAppService>();

        Assert.Same(porContrato, porConcreto);
    }

    [Fact]
    public async Task Continua_exposto_como_endpoint_http()
    {
        // O registro in-process não pode substituir a exposição HTTP.
        await using var app = await StartAsync(s => s.AddAutoApiServer<IReportAppService, ReportAppService>());

        var response = await app.GetTestClient().GetAsync("/api/app-service/report/get-title");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Relatório", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Overload_por_assembly_registra_os_servicos_do_assembly()
    {
        await using var app = await StartAsync(s => s.AddAutoApiServers(typeof(ReportAppService).Assembly));

        using var scope = app.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IReportAppService>();

        Assert.Equal("Relatório", await service.GetTitleAsync());
    }

    [Fact]
    public async Task Overload_por_assembly_ignora_clientes_gerados()
    {
        // TodoAppServiceClient implementa ITodoAppService mas é [AutoApiGeneratedClient]:
        // registrá-lo como implementação de servidor faria o serviço chamar a si mesmo por HTTP.
        await using var app = await StartAsync(s =>
            s.AddAutoApiServers(typeof(Sample.Web.Services.TodoApplicationService).Assembly));

        using var scope = app.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<Sample.Web.Services.ITodoAppService>();

        Assert.IsType<Sample.Web.Services.TodoApplicationService>(service);
    }

    [Fact]
    public async Task AddAutoApiServices_e_idempotente()
    {
        // Segunda chamada era catastrófica: convenção duplicada => rota duplicada no startup.
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddAutoApiServices();
        builder.Services.AddAutoApiServices();
        builder.Services.AddAutoApiServer<IReportAppService, ReportAppService>();

        await using var app = builder.Build();
        app.MapControllers();
        await app.StartAsync();   // não deve lançar

        var response = await app.GetTestClient().GetAsync("/api/app-service/report/get-title");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
