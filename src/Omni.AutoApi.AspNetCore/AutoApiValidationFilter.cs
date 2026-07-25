using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.WebUtilities;

namespace Omni.AutoApi.AspNetCore
{
    /// <summary>
    /// Converte ModelState inválido (DataAnnotations: [Required], [Range], etc.) em 400
    /// <see cref="ValidationProblemDetails"/> no MESMO formato RFC 9457 do
    /// <see cref="AutoApiExceptionFilter"/> — no espírito do <c>AbpValidationActionFilter</c>.
    /// Só age em Auto API Controllers; controllers normais mantêm o comportamento padrão.
    /// </summary>
    internal sealed class AutoApiValidationFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            if (context.ModelState.IsValid)
            {
                return;
            }

            if (context.ActionDescriptor is not ControllerActionDescriptor descriptor
                || !AutoApiHelper.IsAutoApiController(descriptor.ControllerTypeInfo))
            {
                return;
            }

            var problem = new ValidationProblemDetails(context.ModelState)
            {
                Type = "about:blank",
                Status = StatusCodes.Status400BadRequest,
                Title = ReasonPhrases.GetReasonPhrase(StatusCodes.Status400BadRequest),
                Detail = "Um ou mais erros de validação ocorreram."
            };
            problem.Extensions["code"] = "ValidationError";

            context.Result = new ObjectResult(problem) { StatusCode = StatusCodes.Status400BadRequest };
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
        }
    }
}
