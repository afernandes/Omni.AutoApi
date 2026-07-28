using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Omni.AutoApi.Sample.Web.Auth;

/// <summary>
/// Autenticação JWT do sample (R11). Em produção a chave viria de um secret store e os tokens
/// de um identity provider — aqui geramos localmente só para o exemplo ser executável.
/// </summary>
public static class JwtSetup
{
    public const string Issuer = "omni.autoapi.sample";
    public const string Audience = "omni.autoapi.sample.clients";

    // APENAS PARA O SAMPLE. Nunca versione uma chave real.
    private const string SigningKey = "sample-only-signing-key-please-do-not-use-in-production-0123456789";

    public static SymmetricSecurityKey Key { get; } =
        new(Encoding.UTF8.GetBytes(SigningKey));

    public static IServiceCollection AddSampleJwtAuth(this IServiceCollection services)
    {
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = Issuer,
                    ValidAudience = Audience,
                    IssuerSigningKey = Key,
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });

        services.AddAuthorization(options =>
        {
            // Policy usada pelo TodoApplicationService.DeleteTodoAsync.
            options.AddPolicy("todo:admin", policy => policy.RequireRole("admin"));
        });

        return services;
    }

    /// <summary>Gera um token de teste. Num sistema real, quem emite é o identity provider.</summary>
    public static string CreateToken(string user, params string[] roles)
    {
        var claims = new List<Claim> { new(ClaimTypes.Name, user), new(JwtRegisteredClaimNames.Sub, user) };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(Key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
