namespace Omni.AutoApi.AspNetCore
{
    /// <summary>
    /// Critério único de "isto é um Auto API Controller?", compartilhado pelo
    /// FeatureProvider, pela Convention e pelo EnrichmentProvider.
    /// </summary>
    internal static class AutoApiHelper
    {
        public static bool IsAutoApiController(Type type)
        {
            // Clientes gerados implementam a interface remota mas NÃO são controllers de servidor.
            if (type.IsDefined(typeof(AutoApiGeneratedClientAttribute), inherit: false))
            {
                return false;
            }

            return typeof(IRemoteService).IsAssignableFrom(type)
                || type.IsDefined(typeof(AutoApiControllerAttribute), inherit: true);
        }
    }
}
