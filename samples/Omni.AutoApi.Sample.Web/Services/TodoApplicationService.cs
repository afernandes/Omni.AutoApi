using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Omni.AutoApi.AspNetCore;
using Omni.AutoApi.Sample.Web.Contracts;
using Omni.AutoApi.Sample.Web.Models;

namespace Omni.AutoApi.Sample.Web.Services
{
    public class TodoApplicationService : ApplicationService, ITodoAppService
    {
        /// <summary>
        /// Exige token válido (R11). Leitura fica pública; escrita/remoção exigem autenticação —
        /// os atributos padrão do ASP.NET Core funcionam normalmente nos Application Services.
        /// </summary>
        [Authorize]
        public Task<TodoItem> CreateSecureTodoAsync(CreateTodoDto input)
            => Task.FromResult(new TodoItem { Title = input.Title, IsCompleted = input.IsCompleted });

        /// <summary>Exige a policy "todo:admin" (role admin).</summary>
        [Authorize(Policy = "todo:admin")]
        public Task DeleteAllTodosAsync() => Task.CompletedTask;

        public Task<List<TodoItem>> GetTodosAsync()
        {
            // Exercita a base enriquecida: Logger e CurrentUser vêm do LazyServiceProvider
            // injetado pelo LazyServicesActionFilter (sem construtor).
            Logger.LogInformation("Listando todos. Autenticado: {Auth}", CurrentUser.IsAuthenticated);

            var todos = new List<TodoItem>
            {
                new TodoItem { Id = 1, Title = "Teste 1", IsCompleted = false }
            };

            return Task.FromResult(todos);
        }

        public async Task<TodoItem> GetTodoAsync(int id)
        {
            // Exercita o pipeline de erro: vira 404 ProblemDetails.
            if (id < 0)
            {
                throw new EntityNotFoundException($"Todo {id} não encontrado.");
            }

            return new TodoItem { Id = id };
        }

        public async Task<TodoItem> CreateTodoAsync(CreateTodoDto input)
        {
            return new TodoItem { Title = input.Title, IsCompleted = input.IsCompleted };
        }

        /// <summary>
        /// Atualiza um item de todo.
        /// </summary>
        public async Task<TodoItem> UpdateTodoAsync(int id, UpdateTodoDto input)
        {
            return new TodoItem { Id = id, Title = input.Title, IsCompleted = input.IsCompleted };
        }

        public async Task DeleteTodoAsync(int id)
        {
            // Implementação real aqui
        }

        public async Task<string> CreateAttachmentAsync(RemoteStreamContent content)
        {
            // Ecoa nome + tamanho para o teste E2E de upload.
            using var buffer = new MemoryStream();
            await content.Stream.CopyToAsync(buffer);
            return $"{content.FileName}:{buffer.Length}";
        }

        public async IAsyncEnumerable<TodoItem> GetTodoStreamAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            for (var i = 1; i <= 3; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return new TodoItem { Id = i, Title = $"Stream {i}" };
                await Task.Yield();
            }
        }
    }
}
