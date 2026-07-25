using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Omni.AutoApi.AspNetCore
{
    /// <summary>
    /// Classe base opcional para serviços de aplicação. Implementa <see cref="IRemoteService"/>
    /// (a classe é exposta automaticamente como Auto API Controller) e expõe helpers resolvidos
    /// sob demanda via <see cref="LazyServices"/> — injetado pelo <see cref="AutoApiControllerActivator"/>.
    /// </summary>
    public abstract class ApplicationService : IRemoteService
    {
        /// <summary>Provedor de serviços lazy da requisição (injetado pelo ativador).</summary>
        public LazyServiceProvider? LazyServices { get; set; }

        private ILogger? _logger;

        protected ILogger Logger =>
            _logger ??= LazyServices?.GetService<ILoggerFactory>()?.CreateLogger(GetType().FullName ?? GetType().Name)
                ?? NullLogger.Instance;

        protected ICurrentUser CurrentUser =>
            LazyServices?.GetRequiredService<ICurrentUser>()
                ?? throw new InvalidOperationException("LazyServices não foi injetado; resolva o serviço via DI/MVC.");

        protected T GetRequiredService<T>() where T : notnull =>
            LazyServices is not null
                ? LazyServices.GetRequiredService<T>()
                : throw new InvalidOperationException("LazyServices não foi injetado; resolva o serviço via DI/MVC.");
    }
}
