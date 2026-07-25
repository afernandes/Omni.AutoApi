using System;

namespace Omni.AutoApi.Routing
{
    /// <summary>
    /// Regras de derivação de rota/verbo a partir de nomes de serviço e método.
    /// É a ÚNICA fonte de verdade compartilhada entre o servidor (convenção MVC), o
    /// proxy dinâmico e o source generator (via compilação de código linkado).
    /// Todas as funções aceitam <see cref="RouteOptions"/> opcional; quando nulo,
    /// usam <see cref="RouteOptions.Default"/> (comportamento histórico).
    /// </summary>
    public static class ApiRouteBuilder
    {
        public static string GetApiServiceRoute(string serviceName, string action = "[action]", RouteOptions? options = null)
        {
            options ??= RouteOptions.Default;
            var controllerName = ConvertServiceNameToControllerName(serviceName, options);
            return $"{options.Prefix}/{controllerName}/{action}";
        }

        public static string GetControllerName(string serviceName, RouteOptions? options = null)
        {
            return ConvertServiceNameToControllerName(serviceName, options ?? RouteOptions.Default);
        }

        /// <summary>
        /// Deriva a mesma rota do servidor a partir do nome da interface remota
        /// (ex.: "ITodoAppService" → "TodoAppService" → "todo"), removendo o prefixo "I".
        /// </summary>
        public static string GetApiServiceRouteFromInterface(string interfaceName, string action = "[action]", RouteOptions? options = null)
        {
            var serviceName = interfaceName;

            if (serviceName.Length > 1 && serviceName[0] == 'I' && char.IsUpper(serviceName[1]))
            {
                serviceName = serviceName.Substring(1);
            }

            return GetApiServiceRoute(serviceName, action, options);
        }

        public static string GetActionName(string methodName, RouteOptions? options = null)
        {
            options ??= RouteOptions.Default;
            var name = methodName.RemovePostfix("Async");
            return options.UseKebabCase ? name.ToKebabCase() : name.ToCamelCase();
        }

        public static string GetHttpMethod(string methodName)
        {
            return GetHttpMethod(methodName, out _);
        }

        /// <summary>
        /// Variante que informa se o verbo veio do fallback (nenhum prefixo conhecido → POST),
        /// permitindo ao chamador alertar sobre verbos implícitos inesperados.
        /// </summary>
        public static string GetHttpMethod(string methodName, out bool usedFallback)
        {
            methodName = methodName.RemovePostfix("Async");
            usedFallback = false;

            if (methodName.StartsWith("Get", StringComparison.OrdinalIgnoreCase))
                return "GET";
            if (methodName.StartsWith("Create", StringComparison.OrdinalIgnoreCase))
                return "POST";
            if (methodName.StartsWith("Update", StringComparison.OrdinalIgnoreCase))
                return "PUT";
            if (methodName.StartsWith("Delete", StringComparison.OrdinalIgnoreCase))
                return "DELETE";
            if (methodName.StartsWith("Patch", StringComparison.OrdinalIgnoreCase))
                return "PATCH";

            usedFallback = true;
            return "POST";
        }

        private static string ConvertServiceNameToControllerName(string serviceName, RouteOptions options)
        {
            var controllerName = serviceName.RemovePostfix(options.ControllerPostfixes);
            return options.UseKebabCase ? controllerName.ToKebabCase() : controllerName.ToCamelCase();
        }
    }
}
