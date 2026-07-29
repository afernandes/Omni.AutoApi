using Asp.Versioning;
using Omni.AutoApi.AspNetCore;

namespace Omni.AutoApi.Sample.Web.Services;

/// <summary>
/// Demonstra o versionamento (R13). A versão viaja em <c>?api-version=2.0</c> ou no header
/// <c>X-Api-Version</c> — a rota é a mesma nas duas versões. Cada versão vira um documento
/// OpenAPI separado (<c>/openapi/v1.json</c> e <c>/openapi/v2.json</c>).
/// </summary>
[ApiVersion("1.0")]
[ApiVersion("2.0")]
public class CatalogAppService : ApplicationService
{
    /// <summary>Existe nas duas versões.</summary>
    public Task<string> GetNameAsync() => Task.FromResult("Catálogo");

    /// <summary>Só na v1 — some do documento v2.</summary>
    [MapToApiVersion("1.0")]
    public Task<string> GetLegacyCodeAsync() => Task.FromResult("LEGADO-001");

    /// <summary>Só na v2.</summary>
    [MapToApiVersion("2.0")]
    public Task<CatalogSummary> GetSummaryAsync()
        => Task.FromResult(new CatalogSummary { Total = 42, UpdatedAt = new DateOnly(2026, 7, 25) });
}

public class CatalogSummary
{
    public int Total { get; set; }
    public DateOnly UpdatedAt { get; set; }
}
