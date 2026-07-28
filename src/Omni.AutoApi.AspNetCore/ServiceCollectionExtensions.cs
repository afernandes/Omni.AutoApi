using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Omni.AutoApi.Routing;

namespace Omni.AutoApi.AspNetCore
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registra o mecanismo Auto API: descobre IRemoteService, cria controllers reais,
        /// enriquece o ApiExplorer e instala a base de serviços (ProblemDetails, current user).
        /// </summary>
        public static IServiceCollection AddAutoApiServices(
            this IServiceCollection services,
            Action<RouteOptions>? configureRoutes = null)
        {
            var routeOptions = new RouteOptions();
            configureRoutes?.Invoke(routeOptions);
            services.AddSingleton(routeOptions);

            // Configure<ILoggerFactory> injeta o logger na convenção (warnings de verbo fallback).
            services.AddOptions<MvcOptions>().Configure<ILoggerFactory>((options, loggerFactory) =>
            {
                options.Conventions.Add(new AutoApiControllerConvention(
                    routeOptions, loggerFactory.CreateLogger<AutoApiControllerConvention>()));

                // Validação (400 ProblemDetails) roda antes da action; exceções depois.
                options.Filters.Add<AutoApiValidationFilter>();
                options.Filters.Add<AutoApiExceptionFilter>();

                // Materializa RemoteStreamContent (upload) a partir de multipart/corpo bruto.
                options.ModelBinderProviders.Insert(0, new RemoteStreamContentModelBinderProvider());
            });

            services.AddControllers()
                .ConfigureApplicationPartManager(manager =>
                {
                    manager.FeatureProviders.Add(new AutoApiControllerFeatureProvider());
                });

            services.AddEndpointsApiExplorer();
            services.AddProblemDetails();        // idempotente (TryAdd interno)
            services.AddHttpContextAccessor();   // idempotente (TryAdd interno)
            services.TryAddScoped<ICurrentUser, HttpContextCurrentUser>();

            // Enriquece (não recria) as descrições reais geradas pelo pipeline MVC.
            services.AddTransient<IApiDescriptionProvider, AutoApiResponseEnrichmentProvider>();

            // Ativador que injeta o LazyServiceProvider nos ApplicationService.
            // Registrado com Add (após AddControllers) para sobrescrever o ativador padrão.
            services.AddTransient<Microsoft.AspNetCore.Mvc.Controllers.IControllerActivator, AutoApiControllerActivator>();

            return services;
        }
    }
}
