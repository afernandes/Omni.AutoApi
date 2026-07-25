using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.WebUtilities;

namespace Omni.AutoApi.AspNetCore
{
    /// <summary>
    /// Converte exceções dos Auto API Controllers em respostas <see cref="ProblemDetails"/>
    /// (RFC 9457) com status apropriado — no espírito do <c>AbpExceptionFilter</c>.
    /// Não interfere em controllers que não são Auto API.
    /// </summary>
    internal sealed class AutoApiExceptionFilter : IExceptionFilter
    {
        public void OnException(ExceptionContext context)
        {
            if (context.ActionDescriptor is not ControllerActionDescriptor descriptor
                || !AutoApiHelper.IsAutoApiController(descriptor.ControllerTypeInfo))
            {
                return;
            }

            var (status, code) = Map(context.Exception);

            var problem = new ProblemDetails
            {
                Type = "about:blank",
                Status = status,
                Title = ReasonPhrases.GetReasonPhrase(status), // legível (RFC 9457), ex.: "Not Found"
                Detail = BuildDetail(context.Exception, status)
            };
            problem.Extensions["code"] = code; // identificador de máquina

            context.Result = new ObjectResult(problem) { StatusCode = status };
            context.ExceptionHandled = true;
        }

        // Mensagens de AutoApiException são autoradas (negócio) e seguras de expor; mensagens de
        // exceções de framework são mascaradas para não vazar detalhes internos.
        private static string BuildDetail(Exception exception, int status) => exception switch
        {
            AutoApiException => exception.Message,
            _ when status >= StatusCodes.Status500InternalServerError
                => "Ocorreu um erro interno ao processar a requisição.",
            _ => "A requisição não pôde ser processada."
        };

        private static (int status, string code) Map(Exception exception) => exception switch
        {
            AutoApiException ex => (ex.StatusCode, ex.ErrorCode ?? "Error"),
            ArgumentException => (StatusCodes.Status400BadRequest, "InvalidArgument"),
            KeyNotFoundException => (StatusCodes.Status404NotFound, "NotFound"),
            NotImplementedException => (StatusCodes.Status501NotImplemented, "NotImplemented"),
            UnauthorizedAccessException => (StatusCodes.Status403Forbidden, "Forbidden"),
            _ => (StatusCodes.Status500InternalServerError, "ServerError")
        };
    }
}
