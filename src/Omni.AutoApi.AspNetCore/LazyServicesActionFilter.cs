using Microsoft.AspNetCore.Mvc.Filters;

namespace Omni.AutoApi.AspNetCore
{
    /// <summary>
    /// Injeta o <see cref="LazyServiceProvider"/> da requisição nos <see cref="ApplicationService"/>,
    /// habilitando os helpers da base (<c>Logger</c>, <c>CurrentUser</c>, <c>GetRequiredService</c>)
    /// sem construtor.
    /// <para>
    /// Por que um filtro e não um <c>IControllerActivator</c> próprio: o ativador é um serviço
    /// ÚNICO do MVC e vence quem registrar por último. Recursos padrão como
    /// <c>AddControllersAsServices()</c> também o substituem — se o usuário os chamasse depois de
    /// <c>AddAutoApiServices()</c>, a injeção silenciosamente parava de acontecer (o <c>Logger</c>
    /// virava <c>NullLogger</c> e o <c>CurrentUser</c> lançava). Um filtro roda independentemente
    /// de quem criou o controller, e ainda deixa o MVC usar seu ativador padrão — que tem cache de
    /// construtor e já cuida de <c>Dispose</c>/<c>DisposeAsync</c>.
    /// </para>
    /// </summary>
    internal sealed class LazyServicesActionFilter : IActionFilter, IOrderedFilter
    {
        /// <summary>Roda antes de qualquer outro filtro, caso algum passe a depender disto.</summary>
        public int Order => int.MinValue;

        public void OnActionExecuting(ActionExecutingContext context)
        {
            // Respeita um LazyServices já definido (ex.: instância vinda do DI via
            // AddAutoApiServer + AddControllersAsServices).
            if (context.Controller is ApplicationService applicationService
                && applicationService.LazyServices is null)
            {
                applicationService.LazyServices =
                    new LazyServiceProvider(context.HttpContext.RequestServices);
            }
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
        }
    }
}
