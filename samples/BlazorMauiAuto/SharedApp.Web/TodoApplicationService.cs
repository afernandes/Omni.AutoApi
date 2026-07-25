using Omni.AutoApi.AspNetCore;
using SharedApp.Shared.Services;

namespace SharedApp.Web;

/// <summary>
/// Implementação real do serviço (no servidor). Por estender <see cref="ApplicationService"/>
/// (=> IRemoteService), é exposta automaticamente como Auto API Controller em
/// <c>/api/app-service/todo/*</c> — exatamente as rotas que o TodoAppServiceClient gerado chama
/// no WebAssembly e no MAUI. No render server-side do Blazor, é injetada e usada in-process.
/// </summary>
public class TodoApplicationService : ApplicationService, ITodoAppService
{
    private static readonly object Gate = new();
    private static readonly List<TodoItem> Store = new()
    {
        new TodoItem { Id = 1, Title = "Comprar café" }
    };

    public Task<List<TodoItem>> GetTodosAsync()
    {
        lock (Gate)
        {
            return Task.FromResult(Store.ToList());
        }
    }

    public Task<TodoItem> CreateTodoAsync(CreateTodoDto input)
    {
        lock (Gate)
        {
            var item = new TodoItem { Id = Store.Count + 1, Title = input.Title };
            Store.Add(item);
            return Task.FromResult(item);
        }
    }
}
