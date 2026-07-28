using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Omni.AutoApi.AspNetCore;

namespace Omni.AutoApi.IntegrationTests;

/// <summary>
/// Serviços de teste expostos como Auto API Controllers via ApplicationPart
/// (o feature provider descobre IRemoteService em qualquer part registrada).
/// </summary>
public class FaultyService : ApplicationService
{
    public Task<int> GetArgumentErrorAsync() => throw new ArgumentException("detalhe interno sensível");
    public Task<int> GetBusinessErrorAsync() => throw new BusinessException("Saldo insuficiente");
    public Task<int> GetNotImplementedAsync() => throw new NotImplementedException();
    public Task<int> GetBoomAsync() => throw new InvalidOperationException("stack trace interno");
}

/// <summary>[Authorize] declarativo num Auto API Controller (schemes explícitos p/ o PolicyEvaluator).</summary>
[Authorize(AuthenticationSchemes = "Test")]
public class SecuredService : ApplicationService
{
    public Task<string> GetSecretAsync() => Task.FromResult("42");
}

internal sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey("X-Test-User"))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "tester") }, "Test");
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), "Test");
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

public static class TestFactory
{
    /// <summary>Factory do sample + os serviços deste assembly como ApplicationPart + auth de teste.</summary>
    public static WebApplicationFactory<Omni.AutoApi.Sample.Web.Program> WithTestServices(
        this WebApplicationFactory<Omni.AutoApi.Sample.Web.Program> factory)
    {
        return factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.AddAuthentication("Test")
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
            services.AddAuthorization();

            services.AddControllers()
                .AddApplicationPart(typeof(FaultyService).Assembly);
        }));
    }
}
