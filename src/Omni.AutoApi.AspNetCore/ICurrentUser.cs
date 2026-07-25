using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Omni.AutoApi.AspNetCore
{
    /// <summary>Informações do usuário da requisição atual, expostas à base ApplicationService.</summary>
    public interface ICurrentUser
    {
        bool IsAuthenticated { get; }
        string? Id { get; }
        string? UserName { get; }
        IEnumerable<Claim> Claims { get; }
    }

    internal sealed class HttpContextCurrentUser : ICurrentUser
    {
        private readonly IHttpContextAccessor _accessor;

        public HttpContextCurrentUser(IHttpContextAccessor accessor)
        {
            _accessor = accessor;
        }

        private ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

        public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

        public string? Id => Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        public string? UserName => Principal?.Identity?.Name;

        public IEnumerable<Claim> Claims => Principal?.Claims ?? Enumerable.Empty<Claim>();
    }
}
