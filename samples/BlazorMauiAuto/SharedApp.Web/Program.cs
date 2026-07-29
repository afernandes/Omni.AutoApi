using Omni.AutoApi.AspNetCore;
using SharedApp.Web;
using SharedApp.Web.Components;
using SharedApp.Shared.Services;
using SharedApp.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

// Add device-specific services used by the SharedApp.Shared project
builder.Services.AddSingleton<IFormFactor, FormFactor>();

// Omni.AutoApi: expõe o TodoApplicationService como Auto API Controller (HTTP, para WASM/MAUI)
// e o disponibiliza in-process para o render server-side do Blazor — uma linha por serviço.
// Diferente de um AddScoped manual, isto também injeta o LazyServices, então Logger/CurrentUser
// funcionam igual no caminho in-process e no HTTP.
builder.Services.AddAutoApiServices();
builder.Services.AddAutoApiServer<ITodoAppService, TodoApplicationService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();

// Endpoints da Auto API (consumidos pelo TodoAppServiceClient no WASM e no MAUI).
app.MapControllers();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(
        typeof(SharedApp.Shared._Imports).Assembly,
        typeof(SharedApp.Web.Client._Imports).Assembly);

app.Run();
