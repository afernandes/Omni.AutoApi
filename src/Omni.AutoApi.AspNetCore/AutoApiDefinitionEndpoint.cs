using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;

namespace Omni.AutoApi.AspNetCore
{
    public sealed class AutoApiParameterModel
    {
        public string Name { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool Required { get; set; }
    }

    public sealed class AutoApiActionModel
    {
        public string Controller { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string HttpMethod { get; set; } = string.Empty;
        public string Route { get; set; } = string.Empty;
        public string? ReturnType { get; set; }
        public List<AutoApiParameterModel> Parameters { get; set; } = new();
    }

    public sealed class AutoApiDefinitionModel
    {
        public List<AutoApiActionModel> Actions { get; set; } = new();
    }

    /// <summary>
    /// Expõe a definição da API (controllers/ações/rotas/parâmetros) como JSON — análogo ao
    /// endpoint <c>/api/abp/api-definition</c> do ABP. Serve de fonte para validação de contrato
    /// e geração de proxies externos (JS/TS/Angular).
    /// <para>
    /// SEGURANÇA: é público por padrão (para clientes externos descobrirem a API). Se a API for
    /// privada, restrinja: <c>app.MapAutoApiDefinition().RequireAuthorization("Policy");</c>
    /// </para>
    /// </summary>
    public static class AutoApiDefinitionEndpoint
    {
        public static IEndpointConventionBuilder MapAutoApiDefinition(
            this IEndpointRouteBuilder endpoints,
            string pattern = "/api/auto-api/definition")
        {
            return endpoints.MapGet(pattern,
                (IApiDescriptionGroupCollectionProvider provider) => Results.Ok(Build(provider)));
        }

        public static AutoApiDefinitionModel Build(IApiDescriptionGroupCollectionProvider provider)
        {
            var model = new AutoApiDefinitionModel();

            foreach (var group in provider.ApiDescriptionGroups.Items)
            {
                foreach (var description in group.Items)
                {
                    if (description.ActionDescriptor is not ControllerActionDescriptor descriptor
                        || !AutoApiHelper.IsAutoApiController(descriptor.ControllerTypeInfo))
                    {
                        continue;
                    }

                    model.Actions.Add(new AutoApiActionModel
                    {
                        Controller = descriptor.ControllerName,
                        Action = descriptor.ActionName,
                        HttpMethod = description.HttpMethod ?? "POST",
                        Route = "/" + (description.RelativePath ?? string.Empty).TrimStart('/'),
                        ReturnType = TypeName(description.SupportedResponseTypes
                            .FirstOrDefault(r => r.Type != null && r.Type != typeof(void))?.Type),
                        Parameters = description.ParameterDescriptions.Select(p => new AutoApiParameterModel
                        {
                            Name = p.Name,
                            Source = p.Source?.Id ?? string.Empty,
                            Type = TypeName(p.Type) ?? "object",
                            Required = p.IsRequired
                        }).ToList()
                    });
                }
            }

            // Com versionamento ligado, a mesma ação aparece em vários grupos do ApiExplorer;
            // deduplica por (verbo + rota) para não retornar entradas repetidas.
            model.Actions = model.Actions
                .GroupBy(a => $"{a.HttpMethod} {a.Route}")
                .Select(g => g.First())
                .ToList();

            return model;
        }

        // FullName (com namespace) evita ambiguidade entre tipos homônimos em namespaces distintos.
        private static string? TypeName(Type? type) => type?.FullName ?? type?.Name;
    }
}
