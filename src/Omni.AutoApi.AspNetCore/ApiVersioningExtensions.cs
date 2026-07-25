using Asp.Versioning;
using Microsoft.Extensions.DependencyInjection;

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
                .AddMvc();

            return services;
        }
    }
}
