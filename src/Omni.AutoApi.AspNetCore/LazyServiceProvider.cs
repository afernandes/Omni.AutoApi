using System.Collections.Concurrent;

namespace Omni.AutoApi.AspNetCore
{
    /// <summary>
    /// Resolve e cacheia serviços sob demanda a partir do <see cref="IServiceProvider"/> da
    /// requisição — análogo (em escopo reduzido) ao <c>ILazyServiceProvider</c> do ABP.
    /// Usa <see cref="ConcurrentDictionary{TKey,TValue}"/> para tolerar acesso concorrente
    /// dentro de uma mesma requisição (ex.: Task.WhenAll no Application Service).
    /// </summary>
    public sealed class LazyServiceProvider
    {
        private readonly IServiceProvider _provider;
        private readonly ConcurrentDictionary<Type, object?> _cache = new();

        public LazyServiceProvider(IServiceProvider provider)
        {
            _provider = provider;
        }

        public T GetRequiredService<T>() where T : notnull
            => (T)GetRequiredService(typeof(T));

        public object GetRequiredService(Type type)
        {
            var service = _cache.GetOrAdd(type, t => _provider.GetService(t));
            return service ?? throw new InvalidOperationException($"Serviço não registrado: {type}.");
        }

        public T? GetService<T>()
            => (T?)_cache.GetOrAdd(typeof(T), t => _provider.GetService(t));
    }
}
