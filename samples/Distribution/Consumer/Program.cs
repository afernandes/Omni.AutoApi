using Omni.AutoApi.Client.Generated;
using Microsoft.Extensions.DependencyInjection;
using MyApi.Contracts;

// Consumidor em OUTRA solution: referencia apenas o pacote MyApi.Contracts (do feed local).
// O TodoAppServiceClient E as extensões de DI (AddAllAutoApiClients) vieram assados no pacote.
var baseUrl = args.Length > 0 ? args[0] : "http://localhost:5097";

var services = new ServiceCollection();
// Extensão GERADA, vinda do pacote — registra todos os clientes numa linha:
services.AddAllAutoApiClients((_, http) => http.BaseAddress = new Uri(baseUrl));

using var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();
var client = scope.ServiceProvider.GetRequiredService<ITodoAppService>();

var before = await client.GetTodosAsync();
Console.WriteLine($"[Consumer] GET -> {before.Count} todo(s): {string.Join(", ", before.Select(t => t.Title))}");

var created = await client.CreateTodoAsync(new CreateTodoDto { Title = "Do outro projeto" });
Console.WriteLine($"[Consumer] POST -> criado id={created.Id}, title='{created.Title}'");
