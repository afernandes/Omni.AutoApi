using Omni.AutoApi.Client.Generated;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SharedApp.Shared.Services;
using SharedApp.Web.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Add device-specific services used by the SharedApp.Shared project
builder.Services.AddSingleton<IFormFactor, FormFactor>();

// Omni.AutoApi: registra TODOS os clientes gerados numa linha (extensão gerada AddAllAutoApiClients).
builder.Services.AddAllAutoApiClients((_, http) =>
    http.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress));

await builder.Build().RunAsync();
