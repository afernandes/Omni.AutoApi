namespace Omni.AutoApi
{
    /// <summary>
    /// Marque uma interface remota com este atributo para que o gerador
    /// <c>Omni.AutoApi.Client.SourceGenerator</c> emita, em tempo de compilação, uma
    /// implementação concreta de cliente HTTP (ex.: <c>ITodoAppService</c> →
    /// <c>TodoAppServiceClient</c>) — uma alternativa estática ao proxy dinâmico de runtime.
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface)]
    public sealed class AutoApiClientAttribute : Attribute
    {
    }
}
