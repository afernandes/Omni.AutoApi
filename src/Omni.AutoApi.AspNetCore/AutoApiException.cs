namespace Omni.AutoApi.AspNetCore
{
    /// <summary>Exceção de negócio mapeada para um status HTTP + ProblemDetails padronizado.</summary>
    public class AutoApiException : Exception
    {
        public int StatusCode { get; }
        public string? ErrorCode { get; }

        public AutoApiException(string message, int statusCode = 400, string? errorCode = null)
            : base(message)
        {
            StatusCode = statusCode;
            ErrorCode = errorCode;
        }
    }

    /// <summary>404 — recurso não encontrado.</summary>
    public sealed class EntityNotFoundException : AutoApiException
    {
        public EntityNotFoundException(string message = "Recurso não encontrado.")
            : base(message, 404, "EntityNotFound")
        {
        }
    }

    /// <summary>409 — violação de regra de negócio.</summary>
    public sealed class BusinessException : AutoApiException
    {
        public BusinessException(string message, string? errorCode = null)
            : base(message, 409, errorCode ?? "BusinessError")
        {
        }
    }
}
