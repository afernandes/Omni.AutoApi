using Omni.AutoApi;

namespace MyApi.Contracts;

/// <summary>
/// Contrato remoto. Como o gerador roda NESTE projeto, o TodoAppServiceClient (e as extensões
/// de DI) são compilados dentro do MyApi.Contracts.dll. Quem consome o pacote recebe o cliente
/// pronto — sem precisar do gerador nem do código-fonte da interface.
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
