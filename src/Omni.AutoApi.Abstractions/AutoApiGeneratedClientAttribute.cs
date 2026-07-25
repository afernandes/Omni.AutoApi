namespace Omni.AutoApi
{
    /// <summary>
    /// Aplicado automaticamente pelo source generator às classes de cliente geradas
    /// (ex.: <c>TodoAppServiceClient</c>). Embora implementem uma interface
    /// <see cref="IRemoteService"/>, são CLIENTES e não devem ser expostas como controllers
    /// no servidor — o mecanismo usa este marcador para excluí-las da descoberta.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class AutoApiGeneratedClientAttribute : Attribute
    {
    }
}
