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

        /// <summary>
        /// Documento/versão a que a ação pertence (<c>v1</c>, <c>v2</c>, …), ou <c>null</c> quando
        /// o versionamento não está ativo. Como a versão viaja em query/header, duas versões da
        /// mesma ação compartilham verbo e rota — este campo é o que as distingue.
        /// </summary>
        public string? ApiVersion { get; set; }

        public string? ReturnType { get; set; }
        public List<AutoApiParameterModel> Parameters { get; set; } = new();
    }

    public sealed class AutoApiDefinitionModel
    {
        /// <summary>Versões disponíveis (vazio quando o versionamento não está ativo).</summary>
        public List<string> ApiVersions { get; set; } = new();

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
        /// <summary>
        /// Mapeia o endpoint. Aceita <c>?api-version=v2</c> (nome do documento) para filtrar a
        /// definição a uma única versão — útil para gerar um cliente só da v2.
        /// </summary>
        public static IEndpointConventionBuilder MapAutoApiDefinition(
            this IEndpointRouteBuilder endpoints,
            string pattern = "/api/auto-api/definition")
        {
            return endpoints.MapGet(pattern,
                (IApiDescriptionGroupCollectionProvider provider, string? apiVersion) =>
                    Results.Ok(Build(provider, apiVersion)));
        }

        public static AutoApiDefinitionModel Build(IApiDescriptionGroupCollectionProvider provider)
            => Build(provider, apiVersion: null);

        /// <summary>Monta a definição, opcionalmente restrita a uma versão.</summary>
        /// <param name="provider">Fonte das descrições reais geradas pelo pipeline MVC.</param>
        /// <param name="apiVersion">
        /// Nome do documento (<c>v1</c>, <c>v2</c>) para filtrar; <c>null</c> devolve todas.
        /// </param>
        public static AutoApiDefinitionModel Build(
            IApiDescriptionGroupCollectionProvider provider,
            string? apiVersion)
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

                    // Com o Asp.Versioning + ApiExplorer, o GroupName vira "v1"/"v2"; sem
                    // versionamento, fica nulo.
                    var versao = description.GroupName;

                    if (apiVersion is not null
                        && !string.Equals(versao, apiVersion, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    model.Actions.Add(new AutoApiActionModel
                    {
                        Controller = descriptor.ControllerName,
                        Action = descriptor.ActionName,
                        HttpMethod = description.HttpMethod ?? "POST",
                        Route = "/" + (description.RelativePath ?? string.Empty).TrimStart('/'),
                        ApiVersion = versao,
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

            // Deduplica por (versão + verbo + rota). A VERSÃO É PARTE DA CHAVE de propósito:
            // como ela viaja em query/header e não na URL, v1 e v2 da mesma ação compartilham
            // verbo e rota — deduplicar só por (verbo + rota) faria a v2 sumir da definição.
            model.Actions = model.Actions
                .GroupBy(a => $"{a.ApiVersion}|{a.HttpMethod} {a.Route}")
                .Select(g => g.First())
                .ToList();

            model.ApiVersions = model.Actions
                .Select(a => a.ApiVersion)
                .Where(v => !string.IsNullOrEmpty(v))
                .Distinct()
                .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
                .ToList()!;

            return model;
        }

        // FullName (com namespace) evita ambiguidade entre tipos homônimos em namespaces distintos.
        private static string? TypeName(Type? type) => type?.FullName ?? type?.Name;
    }
}
