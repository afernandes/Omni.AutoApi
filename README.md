# Omni.AutoApi

Mecanismo estilo **ABP Auto API Controllers** para **ASP.NET Core (.NET 10)**: exponha *Application
Services* como controllers HTTP automaticamente e consuma-os por **clientes tipados** — um proxy
dinâmico em runtime **ou** um cliente real gerado em tempo de compilação. A **mesma interface** roda
no servidor, no Blazor (Server/WebAssembly) e no MAUI.

## Destaques

- **Controllers automáticos** a partir de `IRemoteService` — sem `[ApiController]`/`[Route]`/`[HttpGet]`.
- **Clientes tipados** em duas formas: proxy dinâmico (runtime, `DispatchProxy`) e cliente real
  **gerado** (compile-time, AOT-friendly) — ambos derivam a rota da **mesma** fonte, então não saem de sincronia.
- **Extensões de DI geradas**: `Add{Nome}Client(...)` (via `IHttpClientFactory` quando disponível) e
  `AddAllAutoApiClients(...)` — incl. overload com configuração **por serviço** (multi-backend).
- **Validação automática**: DataAnnotations inválidas → 400 `ProblemDetails` com erros por campo.
- **`[Authorize]` declarativo** nos Application Services (metadata preservado no endpoint).
- **Upload** via `RemoteStreamContent` (multipart) e **streaming** via `IAsyncEnumerable<T>` — nos dois clientes.
- **Base `ApplicationService` enriquecida**: `Logger`, `CurrentUser`, `GetRequiredService<T>` sem construtor.
- **Erros padronizados** em `ProblemDetails` (RFC 9457) + exceções de negócio.
- **Endpoint de definição** `/api/auto-api/definition` (estilo ABP `/api/abp/api-definition`).
- **Rotas configuráveis** (`RouteOptions`) e **versionamento** opt-in (`Asp.Versioning`).
- Coberto por **60 testes** (unit + integração). Licença **MIT**.

## Pacotes

| Pacote (NuGet) | Descrição | TFM |
|---|---|---|
| `AndersonN.Omni.AutoApi.Abstractions` | `IRemoteService`, atributos, `RemoteStreamContent` e regras de rota/verbo | net9.0; net10.0 |
| `AndersonN.Omni.AutoApi.AspNetCore` | Lado servidor: transforma `IRemoteService` em controllers MVC reais | net9.0; net10.0 |
| `AndersonN.Omni.AutoApi.Client` | Lado cliente: proxy HTTP dinâmico (runtime, via `DispatchProxy`) + DI | net9.0; net10.0 |
| `AndersonN.Omni.AutoApi.Client.SourceGenerator` | Gera o cliente HTTP real (`XxxClient`) + extensões de DI em compilação | netstandard2.0 (analyzer) |

```bash
dotnet add package AndersonN.Omni.AutoApi.AspNetCore   # servidor
dotnet add package AndersonN.Omni.AutoApi.Client       # cliente (proxy dinâmico)
```

> **Nome do pacote × namespace:** os IDs no NuGet usam o prefixo `AndersonN.` (o prefixo `Omni.*` é
> reservado por outra conta no nuget.org), mas os **namespaces C# continuam `Omni.AutoApi.*`** —
> ou seja, você instala `AndersonN.Omni.AutoApi.Client` e escreve `using Omni.AutoApi.Client;`.
>
> O pacote `...Client.SourceGenerator` é um analisador e requer `...Abstractions` no projeto
> consumidor (vem transitivamente ao usar `...AspNetCore` ou `...Client`).

## Início rápido

### Servidor — expondo um Application Service

```csharp
public class TodoApplicationService : ApplicationService, ITodoAppService   // ApplicationService : IRemoteService
{
    public Task<List<TodoItem>> GetTodosAsync() => ...;          // GET    api/app-service/todo/get-todos
    public Task<TodoItem>       GetTodoAsync(int id) => ...;      // GET    api/app-service/todo/get-todo?id=
    public Task<TodoItem>       CreateTodoAsync(CreateTodoDto i); // POST   api/app-service/todo/create-todo  (body)
    public Task<TodoItem>       UpdateTodoAsync(int id, UpdateTodoDto i); // PUT  ...update-todo?id=  (body)
    public Task                 DeleteTodoAsync(int id);          // DELETE ...delete-todo?id=  -> 204
}
```

```csharp
builder.Services.AddAutoApiServices();   // descobre IRemoteService, cria controllers, enriquece o OpenAPI
...
app.MapControllers();
app.MapAutoApiDefinition();               // (opcional) /api/auto-api/definition
```

Convenções: verbo pelo prefixo do método (`Get/Create/Update/Delete/Patch`), rota kebab-case, tipos
simples → query, DTO complexo → body (exceto GET/DELETE). `[Http*]`/`[Route]`/`[From*]` explícitos são
respeitados, e **colisões de rota** (sobrecargas) falham no startup com mensagem clara.

### Cliente — mesma interface, registro por host

```csharp
[AutoApiClient]                                   // habilita o cliente gerado
public interface ITodoAppService : IRemoteService { /* mesmos métodos */ }
```

**(1) Proxy dinâmico (runtime, sem geração):**

```csharp
builder.Services.AddAutoApiClient<ITodoAppService>((_, c) => c.BaseAddress = new Uri(baseUrl));
// ou descobrindo todas as interfaces IRemoteService de um assembly:
builder.Services.AddAutoApiClients(typeof(ITodoAppService).Assembly, (_, c) => c.BaseAddress = new Uri(baseUrl));
```

**(2) Cliente gerado (compile-time, AOT-friendly):** o `[AutoApiClient]` faz o source generator emitir
`TodoAppServiceClient : ITodoAppService` **e** extensões de DI (`using Omni.AutoApi.Client.Generated;`):

```csharp
builder.Services.AddTodoAppServiceClient((_, http) => http.BaseAddress = new Uri(baseUrl)); // um cliente
builder.Services.AddAllAutoApiClients((_, http) => http.BaseAddress = new Uri(baseUrl));     // todos do assembly

// multi-backend: configuração POR SERVIÇO (o callback recebe o Type da interface):
builder.Services.AddAllAutoApiClients((_, http, svc) =>
    http.BaseAddress = new Uri(svc == typeof(IOrderAppService) ? ordersUrl : defaultUrl));
```

Quando `Microsoft.Extensions.Http` está presente, `Add{Nome}Client` usa **`AddHttpClient` + typed
client** e retorna `IHttpClientBuilder` — encadeie resiliência e handlers (auth, correlação):

```csharp
builder.Services.AddTodoAppServiceClient((_, http) => http.BaseAddress = new Uri(baseUrl))
    .AddHttpMessageHandler<AuthTokenHandler>()      // DelegatingHandler p/ Authorization: Bearer ...
    .AddStandardResilienceHandler();                 // Microsoft.Extensions.Http.Resilience
```

## Recursos do servidor

**Base `ApplicationService` enriquecida** — helpers resolvidos sob demanda (injetados por um
`IControllerActivator` próprio), sem construtor:

```csharp
public Task<List<TodoItem>> GetTodosAsync()
{
    Logger.LogInformation("user={User}", CurrentUser.Id);   // ILogger + ICurrentUser via LazyServices
    var repo = GetRequiredService<ITodoRepository>();
    ...
}
```

**Pipeline de erro** — exceções viram `ProblemDetails` (RFC 9457) com status mapeado; mensagens de
negócio são preservadas, as de framework (4xx) são mascaradas:

```csharp
throw new EntityNotFoundException("Todo não encontrado");  // -> 404 { "code": "EntityNotFound", ... }
throw new BusinessException("Saldo insuficiente");         // -> 409
```

**Validação automática** — DataAnnotations no DTO (`[Required]`, `[Range]`, ...) inválidas retornam
400 `ProblemDetails` com `"code": "ValidationError"` e erros por campo, sem código na action.

**Autorização declarativa** — `[Authorize]`/policies na classe ou método do Application Service
funcionam normalmente (o metadata é preservado no endpoint):

```csharp
[Authorize(Roles = "admin")]
public class AdminAppService : ApplicationService { ... }
```

**Upload e streaming** — a mesma interface declara upload (sem depender de `IFormFile`) e
streaming incremental, funcionando no servidor e nos dois clientes:

```csharp
Task<string> CreateAttachmentAsync(RemoteStreamContent content);         // multipart/form-data
IAsyncEnumerable<TodoItem> GetTodoStreamAsync(CancellationToken ct = default); // JSON incremental
```

**Rotas configuráveis** (`RouteOptions`) — no servidor e no gerador (mantenha os dois em sincronia):

```csharp
builder.Services.AddAutoApiServices(o => { o.Prefix = "api/services"; o.UseKebabCase = true; });
```
```xml
<!-- no projeto que gera o cliente -->
<PropertyGroup>
  <AutoApiRoutePrefix>api/services</AutoApiRoutePrefix>
  <AutoApiControllerPostfixes>AppService;Handler</AutoApiControllerPostfixes>
</PropertyGroup>
```

**Versionamento** opt-in (não altera o OpenAPI padrão; lê versão por query `?api-version=` / header):

```csharp
builder.Services.AddAutoApiVersioning();   // depois use [ApiVersion("2.0")] no Application Service
```

## Exemplos (`samples/`)

| Exemplo | O que mostra |
|---|---|
| [`Omni.AutoApi.Sample.Web`](samples/Omni.AutoApi.Sample.Web) | Host + consumidor básico; OpenAPI/Scalar; pipeline de erro. |
| [`BlazorMauiAuto`](samples/BlazorMauiAuto) | **Blazor Web App (InteractiveAuto: Server → WebAssembly) + MAUI** compartilhando páginas, com a Omni.AutoApi como camada de dados (o mesmo `ITodoAppService` nos três hosts). |
| [`Distribution`](samples/Distribution) | **Distribuir o cliente gerado para outra solution** via pacote de contratos (o cliente vem "assado" no DLL; o consumidor não precisa do gerador). |

## Estrutura do repositório

```
src/
  Omni.AutoApi.Abstractions/           # tipos compartilhados (fonte de verdade das rotas)
  Omni.AutoApi.AspNetCore/             # mecanismo server (convention, providers, DI, erro, versioning)
  Omni.AutoApi.Client/                 # proxy dinâmico de runtime + DI
  Omni.AutoApi.Client.SourceGenerator/ # gerador Roslyn (cliente + extensões de DI)
samples/
  Omni.AutoApi.Sample.Web/             # exemplo básico
  BlazorMauiAuto/                 # Blazor Auto + MAUI compartilhando UI/dados
  Distribution/                   # distribuição cross-solution via NuGet
tests/
  Omni.AutoApi.Tests/                  # unit (ApiRouteBuilder, TypeHelper, gerador)
  Omni.AutoApi.IntegrationTests/       # WebApplicationFactory (e2e)
```

## Build / testes / empacotamento

```bash
dotnet build Omni.AutoApi.sln
dotnet test  Omni.AutoApi.sln
dotnet pack  Omni.AutoApi.sln -c Release -o artifacts   # gera os 4 .nupkg
```

### Publicação

A publicação no nuget.org é automática via **Trusted Publishing** (OIDC — sem API key de longa
duração). Basta criar a tag; o workflow [`release.yml`](.github/workflows/release.yml) usa a **tag como
versão** dos pacotes:

```bash
git tag v0.2.0 && git push origin v0.2.0   # publica Omni.AutoApi.* 0.2.0
```

## Limitações & notas

- **Sobrecargas** de método não são suportadas (colisão de rota → falha no startup); use `[HttpGet("rota")]`/`[Route]`.
- **Cliente gerado** suporta `Task`/`ValueTask`(`<T>`) e `IAsyncEnumerable<T>`. Métodos **genéricos**,
  com **mais de um parâmetro complexo** (corpo) ou com **`Stream`/`IFormFile` crus** (use
  `RemoteStreamContent`) viram stub que lança `NotSupportedException` + diagnóstico **AUTOAPI001**.
- **Query string**: contrato de serialização é ISO-8601 invariante para datas/horas (`DateOnly` →
  `2026-06-24`) e **nome** para enums. DTOs complexos em GET/DELETE são achatados em **1 nível**
  (propriedades complexas aninhadas são omitidas).
- **Tratamento de erro no cliente**: chamam `EnsureSuccessStatusCode()` e propagam exceções
  (`HttpRequestException`/`JsonException`); aplique resiliência via `IHttpClientBuilder` retornado
  pelo registro gerado (`.AddStandardResilienceHandler()`).
- **Sincronia de contrato**: rota/verbo derivam do mesmo `ApiRouteBuilder`, mas a **assinatura** não é
  validada contra o servidor — compartilhe a mesma interface (idealmente num assembly de contratos).
- **Dinâmico vs. gerado**: o proxy dinâmico usa `DispatchProxy` + reflexão (zero setup); o gerado é
  código real (mais rápido, debugável, AOT-friendly). Mesma semântica de rota/binding.
- **Trimming/AOT**: o caminho reflexivo do cliente (DTO complexo em GET/DELETE) já é anotado com
  `[DynamicallyAccessedMembers]`; ainda assim prefira parâmetros simples/body em apps trimmed.
- **BaseAddress**: normalizada automaticamente (barra final); ausência gera erro claro na 1ª chamada.

## Licença, changelog & roadmap

[MIT](LICENSE). Histórico em [CHANGELOG.md](CHANGELOG.md). Próximos passos e pendências
priorizadas em [ROADMAP.md](ROADMAP.md).
