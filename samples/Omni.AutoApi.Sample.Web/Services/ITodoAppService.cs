using Omni.AutoApi.Sample.Web.Contracts;
using Omni.AutoApi.Sample.Web.Models;

namespace Omni.AutoApi.Sample.Web.Services
{
    /// <summary>
    /// Contrato remoto do serviço de Todo. É implementado pelo servidor
    /// (<see cref="TodoApplicationService"/>) e, graças ao atributo [AutoApiClient],
    /// também ganha uma implementação de cliente HTTP gerada em tempo de compilação
    /// (TodoAppServiceClient) pelo Omni.AutoApi.Client.SourceGenerator.
    /// </summary>
    [AutoApiClient]
    public interface ITodoAppService : IRemoteService
    {
        Task<List<TodoItem>> GetTodosAsync();

        Task<TodoItem> GetTodoAsync(int id);

        Task<TodoItem> CreateTodoAsync(CreateTodoDto input);

        Task<TodoItem> UpdateTodoAsync(int id, UpdateTodoDto input);

        Task DeleteTodoAsync(int id);

        /// <summary>Upload (multipart/form-data) via RemoteStreamContent — funciona nos dois clientes.</summary>
        Task<string> CreateAttachmentAsync(RemoteStreamContent content);

        /// <summary>Streaming JSON incremental (IAsyncEnumerable) — consumido sem buffering.</summary>
        IAsyncEnumerable<TodoItem> GetTodoStreamAsync(CancellationToken cancellationToken = default);
    }
}
