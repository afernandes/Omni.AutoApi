using System.Reflection;
using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using Asp.Versioning.ApplicationModels;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Omni.AutoApi.AspNetCore
{
    /// <summary>
    /// Integração opcional com a biblioteca <c>Asp.Versioning</c>. É opt-in (não faz parte de
    /// <c>AddAutoApiServices</c>) para não alterar o documento OpenAPI padrão. Configura leitura
    /// de versão por query string (<c>?api-version=2.0</c>) e header (<c>X-Api-Version</c>), sem
    /// mudar as rotas; basta anotar o Application Service com <c>[ApiVersion("2.0")]</c>.
    /// </summary>
    public static class ApiVersioningExtensions
    {
        /// <summary>Formato do nome de documento por versão: <c>v1</c>, <c>v2</c>, <c>v1.1</c>.</summary>
        public const string DocumentNameFormat = "'v'VVV";

        /// <summary>
        /// Configura o versionamento com as convenções do Omni.AutoApi.
        /// <para>
        /// Para ajustar o ApiExplorer (formato do nome de documento, etc.), use o options pattern
        /// depois desta chamada — não há sobrecarga própria para isso, o que mantém a assinatura
        /// binária estável desde a v0.1.0:
        /// <code>services.Configure&lt;ApiExplorerOptions&gt;(o =&gt; o.GroupNameFormat = "'v'VV");</code>
        /// </para>
        /// </summary>
        public static IServiceCollection AddAutoApiVersioning(
            this IServiceCollection services,
            Action<ApiVersioningOptions>? configure = null)
        {
            services
                .AddApiVersioning(options =>
                {
                    options.DefaultApiVersion = new ApiVersion(1, 0);
                    options.AssumeDefaultVersionWhenUnspecified = true;
                    options.ReportApiVersions = true;
                    options.ApiVersionReader = ApiVersionReader.Combine(
                        new QueryStringApiVersionReader("api-version"),
                        new HeaderApiVersionReader("X-Api-Version"));
                    configure?.Invoke(options);
                })
                .AddMvc()
                // Agrupa as ApiDescriptions por versão: o GroupName passa a ser "v1"/"v2", que é
                // exatamente o que AddOpenApi("v1") usa para filtrar o documento.
                .AddApiExplorer(options =>
                {
                    options.GroupNameFormat = DocumentNameFormat;
                    // A versão viaja em query/header, não na URL — nada a substituir na rota.
                    options.SubstituteApiVersionInUrl = false;
                });

            // Sem isto, o Asp.Versioning ignora os Auto API Controllers: ele só aplica
            // versionamento a controllers que satisfaçam alguma IApiControllerSpecification
            // (por padrão, os marcados com [ApiController]). Como os nossos são promovidos por
            // convenção, precisamos declará-los — mesmo papel do
            // AbpConventionalApiControllerSpecification no ABP.
            services.TryAddEnumerable(
                ServiceDescriptor.Transient<IApiControllerSpecification, AutoApiControllerSpecification>());

            return services;
        }

        /// <summary>
        /// Descobre os nomes de documento (<c>v1</c>, <c>v2</c>, …) a partir dos atributos
        /// <c>[ApiVersion]</c> dos Application Services dos assemblies informados — sem assembly,
        /// usa o de entrada.
        /// <para>
        /// Serve para registrar <b>um documento OpenAPI por versão</b> sem que esta biblioteca
        /// precise depender de um pacote de OpenAPI específico (funciona igual com o
        /// <c>AddOpenApi</c> nativo, Swashbuckle ou NSwag):
        /// </para>
        /// <example>
        /// <code>
        /// foreach (var doc in ApiVersioningExtensions.DiscoverApiDocumentNames())
        /// {
        ///     builder.Services.AddOpenApi(doc);     // /openapi/v1.json, /openapi/v2.json, ...
        /// }
        /// </code>
        /// </example>
        /// </summary>
        public static IReadOnlyList<string> DiscoverApiDocumentNames(params Assembly[] assemblies)
        {
            if (assemblies is null || assemblies.Length == 0)
            {
                var entry = Assembly.GetEntryAssembly();
                assemblies = entry is null ? Array.Empty<Assembly>() : new[] { entry };
            }

            var versions = new SortedSet<ApiVersion>();

            foreach (var assembly in assemblies)
            {
                foreach (var type in GetLoadableTypes(assembly))
                {
                    if (!AutoApiHelper.IsAutoApiController(type))
                    {
                        continue;
                    }

                    foreach (var attribute in type.GetCustomAttributes<ApiVersionAttribute>(inherit: true))
                    {
                        foreach (var version in attribute.Versions)
                        {
                            versions.Add(version);
                        }
                    }
                }
            }

            return versions.Count == 0
                ? Array.Empty<string>()
                : versions.Select(v => v.ToString(DocumentNameFormat)).ToList();
        }

        /// <summary>Tolera assemblies com dependências ausentes (evita quebrar a descoberta).</summary>
        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(t => t is not null)!;
            }
        }
    }

    /// <summary>Faz o Asp.Versioning reconhecer os Auto API Controllers como controllers de API.</summary>
    internal sealed class AutoApiControllerSpecification : IApiControllerSpecification
    {
        public bool IsSatisfiedBy(ControllerModel controller)
            => AutoApiHelper.IsAutoApiController(controller.ControllerType);
    }
}
