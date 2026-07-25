namespace Omni.AutoApi
{
    /// <summary>
    /// Configura como uma classe é exposta como Auto API Controller (rota custom e/ou
    /// grupo OpenAPI). Opcional — serviços que implementam <see cref="IRemoteService"/>
    /// já são expostos com a convenção padrão.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class AutoApiControllerAttribute : Attribute
    {
        public string? Route { get; }
        public string GroupName { get; }

        public AutoApiControllerAttribute(string? route = null, string groupName = "v1")
        {
            Route = route;
            GroupName = groupName;
        }
    }
}
