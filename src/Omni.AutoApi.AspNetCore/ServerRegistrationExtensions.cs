using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Omni.AutoApi.AspNetCore
{
    /// <summary>
    /// Registro de Application Services para uso <b>in-process</b> (Blazor Server, jobs, testes),
    /// além da exposição HTTP que o <c>AddAutoApiServices</c> já faz.
    /// <para>
    /// Sem isto, resolver o serviço direto do container devolve uma instância criada pelo DI — e
    /// e não pelo pipeline MVC —, deixando <c>LazyServices</c> nulo: o
    /// <c>Logger</c> vira <c>NullLogger</c> silenciosamente e <c>CurrentUser</c>/
    /// <c>GetRequiredService</c> lançam. Estas extensões preenchem o <c>LazyServices</c>, então o
    /// serviço se comporta igual chamado por HTTP ou localmente.
    /// </para>
    /// </summary>
    public static class ServerRegistrationExtensions
    {
        /// <summary>
        /// Registra <typeparamref name="TImplementation"/> como <typeparamref name="TService"/>
        /// para uso in-process e garante que o assembly dele seja varrido em busca de controllers.
        /// </summary>
        /// <example>
        /// <code>
        /// builder.Services.AddAutoApiServices();
        /// builder.Services.AddAutoApiServer&lt;ITodoAppService, TodoApplicationService&gt;();
        /// </code>
        /// </example>
        public static IServiceCollection AddAutoApiServer<TService, TImplementation>(this IServiceCollection services)
            where TService : class
            where TImplementation : class, TService, IRemoteService
        {
            ArgumentNullException.ThrowIfNull(services);

            services.AddAutoApiApplicationPart(typeof(TImplementation).Assembly);

            // O concreto guarda o estado (LazyServices); a interface aponta para ele, de modo que
            // ambos resolvam a MESMA instância dentro do escopo da requisição.
            services.TryAdd(ServiceDescriptor.Scoped(
                typeof(TImplementation),
                sp => CreateInProcess(sp, typeof(TImplementation))));

            services.TryAdd(ServiceDescriptor.Scoped(
                typeof(TService),
                sp => sp.GetRequiredService(typeof(TImplementation))));

            return services;
        }

        /// <summary>
        /// Descobre todos os <see cref="IRemoteService"/> concretos do assembly e os registra para
        /// uso in-process — cada um sob o próprio tipo e sob as interfaces remotas que implementa.
        /// Clientes gerados (<c>[AutoApiGeneratedClient]</c>) são ignorados: eles implementam a
        /// mesma interface, mas são o lado <em>cliente</em>.
        /// </summary>
        public static IServiceCollection AddAutoApiServers(this IServiceCollection services, Assembly assembly)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(assembly);

            services.AddAutoApiApplicationPart(assembly);

            foreach (var implementation in assembly.GetTypes().Where(IsServerImplementation))
            {
                var concrete = implementation;

                services.TryAdd(ServiceDescriptor.Scoped(concrete, sp => CreateInProcess(sp, concrete)));

                foreach (var remoteInterface in GetRemoteInterfaces(concrete))
                {
                    services.TryAdd(ServiceDescriptor.Scoped(
                        remoteInterface,
                        sp => sp.GetRequiredService(concrete)));
                }
            }

            return services;
        }

        private static bool IsServerImplementation(Type type)
        {
            return type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false }
                && type.IsPublic
                && typeof(IRemoteService).IsAssignableFrom(type)
                // Exclui os clientes HTTP gerados, que também implementam a interface remota.
                && !type.IsDefined(typeof(AutoApiGeneratedClientAttribute), inherit: false);
        }

        private static IEnumerable<Type> GetRemoteInterfaces(Type implementation)
        {
            return implementation.GetInterfaces()
                .Where(i => typeof(IRemoteService).IsAssignableFrom(i) && i != typeof(IRemoteService));
        }

        /// <summary>Cria a instância e injeta o <c>LazyServices</c> do escopo atual.</summary>
        private static object CreateInProcess(IServiceProvider serviceProvider, Type implementation)
        {
            var instance = ActivatorUtilities.CreateInstance(serviceProvider, implementation);

            if (instance is ApplicationService applicationService)
            {
                applicationService.LazyServices = new LazyServiceProvider(serviceProvider);
            }

            return instance;
        }

        /// <summary>
        /// Garante que o assembly seja um ApplicationPart do MVC (necessário quando os Application
        /// Services moram numa class library). Não duplica partes já registradas — duplicar geraria
        /// controllers repetidos e falha de rota no startup.
        /// </summary>
        private static void AddAutoApiApplicationPart(this IServiceCollection services, Assembly assembly)
        {
            services.AddControllers().ConfigureApplicationPartManager(manager =>
            {
                var jaRegistrado = manager.ApplicationParts
                    .OfType<AssemblyPart>()
                    .Any(part => part.Assembly == assembly);

                if (!jaRegistrado)
                {
                    manager.ApplicationParts.Add(new AssemblyPart(assembly));
                }
            });
        }
    }
}
