# Exemplo: Blazor Web App (Auto) + MAUI com páginas compartilhadas + Omni.AutoApi

Gerado do template oficial `dotnet new maui-blazor-web -int Auto` e **integrado à Omni.AutoApi** para
demonstrar como compartilhar **UI e camada de dados** entre Blazor Server, WebAssembly e MAUI com
o mínimo de boilerplate.

## Projetos

| Projeto | Papel | TFM |
|---|---|---|
| `SharedApp.Shared` | **RCL compartilhado**: páginas (`Todos.razor`), layout, `ITodoAppService` (`[AutoApiClient]`) e o `TodoAppServiceClient` **gerado** | net11.0 |
| `SharedApp.Web` | **Blazor Web App** (render `InteractiveAuto`: Server na 1ª carga → WebAssembly). Hospeda a Auto API e injeta o serviço in-process | net11.0 |
| `SharedApp.Web.Client` | Cliente **WebAssembly** | net11.0 |
| `SharedApp` | App **MAUI** (BlazorWebView) que reaproveita o RCL | net11.0-android/ios/maccatalyst/windows |

## A ideia: um contrato, três hosts, zero cliente escrito à mão

`SharedApp.Shared/Services/ITodoAppService.cs` define **uma** interface:

```csharp
[AutoApiClient]
public interface ITodoAppService : IRemoteService
{
    Task<List<TodoItem>> GetTodosAsync();
    Task<TodoItem> CreateTodoAsync(CreateTodoDto input);
}
```

A página `Todos.razor` injeta **só** `ITodoAppService` e roda **igual** nos três hosts. O que muda é
apenas o **registro por host** (mesmo padrão que o template já usa para `IFormFactor`):

| Host | Registro de `ITodoAppService` | Como funciona |
|---|---|---|
| **Web (Server)** | `AddScoped<ITodoAppService, TodoApplicationService>()` | **in-process**, sem HTTP (rápido) |
| **Web (WebAssembly)** | `AddAllAutoApiClients((_, http) => http.BaseAddress = origin)` | cliente **gerado**, HTTP para a mesma app |
| **MAUI** | `AddAllAutoApiClients((_, http) => http.BaseAddress = serverUrl)` | cliente **gerado**, HTTP para o servidor remoto |

> `AddAllAutoApiClients` é uma **extensão gerada** pelo source generator (junto de um
> `Add{Nome}Client` por interface) — registra todos os clientes do assembly numa linha. Basta
> `using Omni.AutoApi.Client.Generated;`.

O servidor expõe `TodoApplicationService` (que estende `ApplicationService`) **automaticamente** como
Auto API Controller em `/api/app-service/todo/*` — exatamente as rotas que o `TodoAppServiceClient`
gerado chama. Cliente e servidor derivam a rota do mesmo `ApiRouteBuilder`, então **não saem de sincronia**.

Com `InteractiveAuto`, a página `Todos` renderiza primeiro no **Server** (serviço in-process) e, quando
o WebAssembly baixa, passa a usar o **cliente HTTP gerado** — tudo transparente para o componente.

## Verificado

- `SharedApp.Web` builda e roda; `GET /api/app-service/todo/get-todos` → 200; `POST .../create-todo` → 200.
- A página `/todos` renderizada no servidor mostra os dados via serviço in-process.
- `SharedApp` (MAUI) compila para `net11.0-windows10.0.19041.0`.

## Como rodar

```bash
# Web (Server -> WASM Auto):
dotnet run --project SharedApp.Web        # abra /todos

# MAUI (Windows): ajuste a BaseAddress em MauiProgram.cs para a URL do servidor e:
dotnet build SharedApp/SharedApp.csproj -f net11.0-windows10.0.19041.0 -t:Run
```

> Para o MAUI no **Android emulator**, use `http://10.0.2.2:PORTA` (o `localhost` do host).

## Como tornar isso ainda mais fácil

- ✅ **Extensão de DI gerada** — `AddAllAutoApiClients` e `Add{Nome}Client` por interface, já em uso
  no WASM e no MAUI (registro de uma linha por host).

Próximos passos possíveis:

1. **Multi-target das libs Omni.AutoApi** (net9 + net10/net11): hoje a referência net9→net11 funciona, mas
   multi-targetar elimina avisos e mantém o `Microsoft.Extensions.Http` alinhado.
2. **BaseAddress por configuração** (não hardcoded no MAUI): ler de `appsettings`/DI.
3. **Helper de servidor**: um `AddAutoApiServer<TService>()` que registre o serviço como controller **e**
   como `ITodoAppService` in-process numa só chamada.
4. **Resolução por serviço no agregado**: hoje `AddAllAutoApiClients` usa uma config única (backend
   único). Para múltiplos backends, um overload com URL por serviço.
