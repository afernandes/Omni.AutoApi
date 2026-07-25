using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;

namespace Omni.AutoApi.AspNetCore
{
    /// <summary>
    /// Ativador de controller que, após criar um <see cref="ApplicationService"/>, injeta nele
    /// um <see cref="LazyServiceProvider"/> ligado aos serviços da requisição — habilitando os
    /// helpers da base (Logger, CurrentUser, GetRequiredService) sem construtor.
    /// Usa <see cref="ActivatorUtilities"/> (o mesmo mecanismo do ativador padrão do MVC).
    /// </summary>
    internal sealed class AutoApiControllerActivator : IControllerActivator
    {
        public object Create(ControllerContext context)
        {
            var controllerType = context.ActionDescriptor.ControllerTypeInfo.AsType();
            var controller = ActivatorUtilities.CreateInstance(context.HttpContext.RequestServices, controllerType);

            if (controller is ApplicationService applicationService)
            {
                applicationService.LazyServices = new LazyServiceProvider(context.HttpContext.RequestServices);
            }

            return controller;
        }

        public void Release(ControllerContext context, object controller)
        {
            if (controller is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        public ValueTask ReleaseAsync(ControllerContext context, object controller)
        {
            if (controller is IAsyncDisposable asyncDisposable)
            {
                return asyncDisposable.DisposeAsync();
            }

            Release(context, controller);
            return default;
        }
    }
}
