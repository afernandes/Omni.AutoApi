using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Omni.AutoApi.Routing;

namespace Omni.AutoApi.Client
{
    /// <summary>
    /// Registro de proxies de cliente tipados, análogo ao <c>AddHttpClientProxies</c> do ABP.
    /// Cada interface remota é resolvida por um <see cref="DynamicHttpProxy{T}"/> apoiado em
    /// um <see cref="HttpClient"/> nomeado criado pelo <see cref="IHttpClientFactory"/>.
    /// </summary>
    public static class AutoApiClientExtensions
    {
        private static readonly MethodInfo AddAutoApiClientMethod = typeof(AutoApiClientExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .First(m => m.Name == nameof(AddAutoApiClient) && m.IsGenericMethodDefinition);

        /// <summary>
        /// Retorna o <see cref="IHttpClientBuilder"/> do client nomeado, permitindo encadear
        /// resiliência (ex.: <c>.AddStandardResilienceHandler()</c>) e <c>DelegatingHandler</c>s
        /// (<c>.AddHttpMessageHandler&lt;...&gt;()</c>).
        /// </summary>
        public static IHttpClientBuilder AddAutoApiClient<TService>(
            this IServiceCollection services,
            Action<IServiceProvider, HttpClient> configureClient,
            RouteOptions? routeOptions = null,
            JsonSerializerOptions? jsonOptions = null)
            where TService : class
        {
            var clientName = typeof(TService).FullName!;

            var builder = services.AddHttpClient(clientName, configureClient);

            services.AddTransient(serviceProvider =>
            {
                var httpClient = serviceProvider
                    .GetRequiredService<IHttpClientFactory>()
                    .CreateClient(clientName);

                return DynamicHttpProxy<TService>.Create(httpClient, jsonOptions, routeOptions);
            });

            return builder;
        }

        /// <summary>
        /// Descobre e registra automaticamente um proxy para cada interface <see cref="IRemoteService"/>
        /// do assembly informado, análogo ao <c>AddHttpClientProxies(assembly)</c> do ABP.
        /// </summary>
        public static IServiceCollection AddAutoApiClients(
            this IServiceCollection services,
            Assembly assembly,
            Action<IServiceProvider, HttpClient> configureClient,
            RouteOptions? routeOptions = null)
        {
            return services.AddAutoApiClients(assembly, (sp, http, _) => configureClient(sp, http), routeOptions);
        }

        /// <summary>
        /// Variante com configuração POR SERVIÇO: o callback recebe o tipo da interface remota,
        /// permitindo BaseAddress/headers distintos por backend (cenários multi-serviço).
        /// </summary>
        public static IServiceCollection AddAutoApiClients(
            this IServiceCollection services,
            Assembly assembly,
            Action<IServiceProvider, HttpClient, Type> configureClient,
            RouteOptions? routeOptions = null)
        {
            var serviceTypes = assembly.GetTypes()
                .Where(t => t.IsInterface
                    && typeof(IRemoteService).IsAssignableFrom(t)
                    && t != typeof(IRemoteService));

            foreach (var serviceType in serviceTypes)
            {
                var capturedType = serviceType;
                Action<IServiceProvider, HttpClient> configure = (sp, http) => configureClient(sp, http, capturedType);

                AddAutoApiClientMethod
                    .MakeGenericMethod(serviceType)
                    .Invoke(null, new object?[] { services, configure, routeOptions, null });
            }

            return services;
        }
    }
}
