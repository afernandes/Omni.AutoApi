using Omni.AutoApi;

namespace SharedApp.Shared.Services;

/// <summary>
/// Contrato remoto compartilhado por TODOS os hosts (Blazor Server, WebAssembly e MAUI).
/// Graças a [AutoApiClient], o Omni.AutoApi.Client.SourceGenerator gera neste assembly um
/// TodoAppServiceClient (HttpClient tipado) usado por WASM e MAUI; o servidor o expõe
/// como Auto API Controller e o consome in-process. A página Todos.razor injeta apenas
/// esta interface e funciona igual nos três.
/// </summary>
[AutoApiClient]
public interface ITodoAppService : IRemoteService
{
    Task<List<TodoItem>> GetTodosAsync();

    Task<TodoItem> CreateTodoAsync(CreateTodoDto input);
}

public class TodoItem
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
}

public class CreateTodoDto
{
    public string Title { get; set; } = string.Empty;
}
