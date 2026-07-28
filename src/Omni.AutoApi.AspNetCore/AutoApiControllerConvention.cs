using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Logging;
using Omni.AutoApi.Routing;

namespace Omni.AutoApi.AspNetCore
{
    public class AutoApiControllerConvention : IControllerModelConvention
    {
        // Tipos que nunca devem ir para o corpo (vinculados pelo MVC de outra forma),
        // espelhando AbpConventionalControllerOptions.FormBodyBindingIgnoredTypes.
        private static readonly Type[] FormBodyBindingIgnoredTypes =
        {
            typeof(IFormFile),
            typeof(CancellationToken),
            typeof(RemoteStreamContent)   // materializado pelo RemoteStreamContentModelBinder
        };

        private static readonly string[] NoBodyHttpMethods = { "GET", "DELETE", "TRACE", "HEAD" };

        private readonly RouteOptions _routeOptions;
        private readonly ILogger? _logger;

        public AutoApiControllerConvention(RouteOptions? routeOptions = null, ILogger? logger = null)
        {
            _routeOptions = routeOptions ?? RouteOptions.Default;
            _logger = logger;
        }

        public void Apply(ControllerModel controller)
        {
            if (!AutoApiHelper.IsAutoApiController(controller.ControllerType))
            {
                return;
            }

            var autoApiAttr = controller.ControllerType
                .GetCustomAttributes(typeof(AutoApiControllerAttribute), true)
                .FirstOrDefault() as AutoApiControllerAttribute;

            ConfigureApiExplorer(controller, autoApiAttr);
            ConfigureSelector(controller, autoApiAttr);
            ConfigureParameters(controller);
        }

        private void ConfigureApiExplorer(ControllerModel controller, AutoApiControllerAttribute? attribute)
        {
            // O GroupName mapeia para o documento OpenAPI (ex.: "v1"). Só o definimos quando há
            // um grupo explícito no atributo; caso contrário deixamos nulo, para que a descrição
            // apareça no documento padrão. Definir o nome do controller aqui o esconderia do
            // documento "v1" gerado pelo AddOpenApi().
            if (string.IsNullOrEmpty(controller.ApiExplorer.GroupName) && attribute?.GroupName != null)
            {
                controller.ApiExplorer.GroupName = attribute.GroupName;
            }

            // Não sobrescreve uma visibilidade já definida explicitamente.
            controller.ApiExplorer.IsVisible ??= true;
        }

        private void ConfigureSelector(ControllerModel controller, AutoApiControllerAttribute? attribute)
        {
            // Preserva o EndpointMetadata dos selectors do controller (ex.: [Authorize] na CLASSE)
            // antes de limpá-los: com endpoint routing, autorização/filtros declarativos viajam
            // como metadata do endpoint — descartá-los desativaria [Authorize] silenciosamente.
            var controllerMetadata = controller.Selectors
                .Where(s => s.EndpointMetadata != null)
                .SelectMany(s => s.EndpointMetadata)
                .ToList();

            // Remove seletores do controller para evitar combinação de rotas controller+action.
            controller.Selectors.Clear();

            var routeTemplate = attribute?.Route
                ?? ApiRouteBuilder.GetApiServiceRoute(controller.ControllerType.Name, options: _routeOptions);

            foreach (var action in controller.Actions)
            {
                RemoveEmptySelectors(action.Selectors);

                if (action.Selectors.Count == 0)
                {
                    // Nenhum atributo de rota/verbo no método: aplica a convenção.
                    AddConventionalSelector(routeTemplate, action);
                }
                else
                {
                    // Já existe [HttpGet]/[HttpPost]/[Route]/... : respeita e só completa o que falta.
                    NormalizeSelectorRoutes(routeTemplate, action);
                }

                // Reinsere o metadata do controller (antes do da action, que deve prevalecer).
                foreach (var selector in action.Selectors)
                {
                    for (var i = controllerMetadata.Count - 1; i >= 0; i--)
                    {
                        selector.EndpointMetadata.Insert(0, controllerMetadata[i]);
                    }
                }
            }

            ValidateNoRouteCollisions(controller);
        }

        /// <summary>
        /// Métodos sobrecarregados (ex.: GetTodo(int) e GetTodo(string)) derivam a mesma
        /// rota + verbo e causariam AmbiguousMatchException em runtime. Detecta isso no
        /// startup e falha com uma mensagem clara pedindo [Http*]/[Route] explícitos.
        /// </summary>
        private static void ValidateNoRouteCollisions(ControllerModel controller)
        {
            var seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var action in controller.Actions)
            {
                foreach (var selector in action.Selectors)
                {
                    var template = selector.AttributeRouteModel?.Template;
                    if (template == null)
                    {
                        continue;
                    }

                    var verbs = selector.ActionConstraints
                        .OfType<HttpMethodActionConstraint>()
                        .SelectMany(c => c.HttpMethods)
                        .DefaultIfEmpty("*");

                    foreach (var verb in verbs)
                    {
                        var key = $"{verb} {template}";
                        if (seen.TryGetValue(key, out var firstAction))
                        {
                            throw new InvalidOperationException(
                                $"Auto API: rota duplicada '{key}' gerada por '{firstAction}' e " +
                                $"'{action.ActionMethod.Name}' no controller '{controller.ControllerName}'. " +
                                "Métodos sobrecarregados não são suportados pela convenção; " +
                                "use [HttpGet(\"rota-unica\")]/[Route] explícitos para desambiguar.");
                        }

                        seen[key] = action.ActionMethod.Name;
                    }
                }
            }
        }

        private void AddConventionalSelector(string routeTemplate, ActionModel action)
        {
            var httpMethod = ApiRouteBuilder.GetHttpMethod(action.ActionName);

            action.Selectors.Add(new SelectorModel
            {
                AttributeRouteModel = new AttributeRouteModel
                {
                    Template = BuildActionRoute(routeTemplate, action)
                },
                ActionConstraints = { new HttpMethodActionConstraint(new[] { httpMethod }) }
            });
        }

        /// <summary>
        /// Completa seletores definidos pelo desenvolvedor: usa o verbo HTTP explícito
        /// quando houver (em vez do convencional) e só injeta rota/verbo se estiverem ausentes.
        /// </summary>
        private void NormalizeSelectorRoutes(string routeTemplate, ActionModel action)
        {
            foreach (var selector in action.Selectors)
            {
                var explicitHttpMethod = selector.ActionConstraints
                    .OfType<HttpMethodActionConstraint>()
                    .SelectMany(c => c.HttpMethods)
                    .FirstOrDefault();

                var httpMethod = explicitHttpMethod;
                if (httpMethod == null)
                {
                    httpMethod = ApiRouteBuilder.GetHttpMethod(action.ActionName, out var usedFallback);
                    if (usedFallback)
                    {
                        // [Route] custom sem [Http*] e sem prefixo de verbo reconhecível no nome:
                        // caindo em POST implícito — alerta para evitar combinações inesperadas.
                        _logger?.LogWarning(
                            "Auto API: a ação '{Action}' de '{Controller}' tem rota customizada sem verbo HTTP explícito " +
                            "e o nome não tem prefixo convencional (Get/Create/Update/Delete/Patch); usando POST por fallback. " +
                            "Adicione [HttpGet]/[HttpPost]/... para tornar o verbo explícito.",
                            action.ActionMethod.Name, action.Controller.ControllerName);
                    }
                }

                selector.AttributeRouteModel ??= new AttributeRouteModel
                {
                    Template = BuildActionRoute(routeTemplate, action)
                };

                if (!selector.ActionConstraints.OfType<HttpMethodActionConstraint>().Any())
                {
                    selector.ActionConstraints.Add(new HttpMethodActionConstraint(new[] { httpMethod }));
                }
            }
        }

        private string BuildActionRoute(string routeTemplate, ActionModel action)
        {
            var actionName = ApiRouteBuilder.GetActionName(action.ActionName, _routeOptions);

            if (routeTemplate.Contains("[action]"))
            {
                return routeTemplate.Replace("[action]", actionName);
            }

            // Rota custom sem o token [action]: anexa o segmento da ação para evitar
            // que todas as ações colidam na mesma rota.
            return string.IsNullOrEmpty(actionName)
                ? routeTemplate
                : $"{routeTemplate.TrimEnd('/')}/{actionName}";
        }

        private static void RemoveEmptySelectors(IList<SelectorModel> selectors)
        {
            for (var i = selectors.Count - 1; i >= 0; i--)
            {
                var selector = selectors[i];
                if (selector.AttributeRouteModel == null
                    && (selector.ActionConstraints == null || selector.ActionConstraints.Count == 0)
                    && (selector.EndpointMetadata == null || selector.EndpointMetadata.Count == 0))
                {
                    selectors.RemoveAt(i);
                }
            }
        }

        private void ConfigureParameters(ControllerModel controller)
        {
            foreach (var action in controller.Actions)
            {
                foreach (var parameter in action.Parameters)
                {
                    // Respeita binding explícito ([FromBody]/[FromRoute]/[FromQuery]/[FromHeader]/...).
                    if (parameter.BindingInfo != null)
                    {
                        continue;
                    }

                    if (TypeHelper.IsPrimitiveExtended(parameter.ParameterType, includeEnums: true))
                    {
                        // Tipos simples (incl. Nullable<T>) viajam na query string.
                        parameter.BindingInfo = new BindingInfo { BindingSource = BindingSource.Query };
                    }
                    else if (CanUseFormBodyBinding(action, parameter))
                    {
                        // Tipos complexos em verbos com corpo viajam no body.
                        parameter.BindingInfo = BindingInfo.GetBindingInfo(new[] { new FromBodyAttribute() });
                    }
                    // Demais casos (ex.: CancellationToken, IFormFile, DTO complexo em GET/DELETE)
                    // ficam com binding nulo, deixando o MVC resolver via seus binders padrão.
                }
            }
        }

        /// <summary>
        /// Porta de <c>AbpServiceConvention.CanUseFormBodyBinding</c>: não usa o corpo para
        /// 'id', tipos ignorados (IFormFile/CancellationToken) ou quando todos os verbos da
        /// ação são sem corpo (GET/DELETE/TRACE/HEAD).
        /// </summary>
        private bool CanUseFormBodyBinding(ActionModel action, ParameterModel parameter)
        {
            if (parameter.ParameterName == "id")
            {
                return false;
            }

            foreach (var ignoredType in FormBodyBindingIgnoredTypes)
            {
                if (ignoredType.IsAssignableFrom(parameter.ParameterType))
                {
                    return false;
                }
            }

            foreach (var selector in action.Selectors)
            {
                if (selector.ActionConstraints == null)
                {
                    continue;
                }

                foreach (var constraint in selector.ActionConstraints)
                {
                    if (constraint is HttpMethodActionConstraint httpMethodConstraint
                        && httpMethodConstraint.HttpMethods.All(IsNoBodyHttpMethod))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool IsNoBodyHttpMethod(string httpMethod)
        {
            return NoBodyHttpMethods.Contains(httpMethod, StringComparer.OrdinalIgnoreCase);
        }
    }
}
