# Changelog

Mudanças notáveis deste projeto. Formato baseado em [Keep a Changelog](https://keepachangelog.com),
versionamento [SemVer](https://semver.org).

## [Unreleased]

### Adicionado
- **Validação automática**: DataAnnotations inválidas → 400 `ProblemDetails` (`ValidationError`,
  erros por campo) via `AutoApiValidationFilter` — estilo `AbpValidationActionFilter`.
- **Upload**: `RemoteStreamContent` (Abstractions) + model binder no servidor + emissão
  `multipart/form-data` nos dois clientes (gerado e dinâmico).
- **Streaming**: suporte a `IAsyncEnumerable<T>` nos dois clientes
  (`JsonSerializer.DeserializeAsyncEnumerable`, `ResponseHeadersRead`).
- **Multi-backend**: overloads com configuração POR SERVIÇO em `AddAllAutoApiClients` (gerado) e
  `AddAutoApiClients` (runtime) — o callback recebe o `Type` da interface.
- **Multi-targeting**: pacotes runtime agora em `net9.0;net10.0`.
- Registro gerado via **`IHttpClientFactory`** (`AddHttpClient` + typed client, retorna
  `IHttpClientBuilder`) quando `Microsoft.Extensions.Http` está presente; `JsonSerializerOptions`
  do DI é injetado; fallback leve mantido (agora com parâmetro `jsonOptions`).
- Warning no startup quando `[Route]` custom cai em POST por fallback de verbo.
- +26 testes (60 no total): proxy (formato/encoding/coleções/BaseAddress), colisão de rota,
  goldens do gerador, e integração (validação, mapa completo de exceções, upload, streaming,
  `[Authorize]`, registro de DI gerado).

### Corrigido
- **`[Authorize]` era ignorado em Auto API Controllers**: a convenção descartava o
  `EndpointMetadata` do controller ao limpar selectors; agora é preservado (401/403 funcionam).
- **`DateOnly`/`TimeOnly` em query string** agora em ISO-8601 invariante (antes `MM/dd/yyyy`,
  sujeito a falha de binding por cultura); **enum** agora explicitamente pelo nome.
- **`Task<string>`**: clientes toleram resposta `text/plain` do `StringOutputFormatter` do MVC
  (antes `JsonException`).
- **`BaseAddress`**: normalização de barra final (evita URL malformada silenciosa) e erro claro
  quando ausente; clientes gerados propagam valores default de parâmetros opcionais.
- Proxy dinâmico falha com mensagem clara para múltiplos parâmetros de corpo (antes descartava).
- Anotação `[DynamicallyAccessedMembers]` também no proxy dinâmico (trimming/AOT).
- Sample: `Microsoft.OpenApi` transitivo fixado em 2.9.0 (advisory GHSA-v5pm-xwqc-g5wc).

## [0.1.0] - 2026-06-23

> Alvo: **.NET 10** (libs e samples; o source generator permanece em `netstandard2.0`). Pacotes
> Microsoft.* em `10.0.0`.

### Adicionado
- **Omni.AutoApi.AspNetCore** — expõe `IRemoteService`/`ApplicationService` como controllers MVC reais
  (convention + feature provider), com verbo/rota/binding convencionais, detecção de colisão de rota
  e enriquecimento do OpenAPI (204 para void).
- **Omni.AutoApi.Client** — proxy de cliente dinâmico (`DynamicHttpProxy`) e registro por interface/assembly.
- **Omni.AutoApi.Client.SourceGenerator** — gera clientes HTTP tipados (`XxxClient`) em tempo de compilação,
  com diagnóstico `AUTOAPI001` para métodos não suportados. Também emite extensões de DI
  (`Add{Nome}Client` e `AddAllAutoApiClients`, em `Omni.AutoApi.Client.Generated`) quando
  `Microsoft.Extensions.DependencyInjection` está disponível.
- **Exemplo `samples/BlazorMauiAuto`** — Blazor Web App (InteractiveAuto: Server → WebAssembly) +
  MAUI compartilhando páginas, com a Omni.AutoApi como camada de dados (mesmo `ITodoAppService` nos três hosts).
- **Exemplo `samples/Distribution`** — distribuição do cliente gerado para outra solution via pacote
  de contratos (cliente "assado" no DLL; consumidor não precisa do gerador).
- Helper de query do cliente gerado anotado com `[DynamicallyAccessedMembers]` (trimming/AOT).
- **Options de rota** (`RouteOptions`): prefixo, sufixos removidos e kebab/camel — no servidor, no proxy
  e no gerador (via propriedade MSBuild `AutoApiRoutePrefix`).
- **Base `ApplicationService` enriquecida**: `Logger`, `CurrentUser` e `GetRequiredService<T>` via
  `LazyServiceProvider` (injetado por um `IControllerActivator` próprio).
- **Pipeline de erro** padronizado em `ProblemDetails` (RFC 9457) + exceções (`EntityNotFoundException`,
  `BusinessException`).
- **Endpoint de definição** `/api/auto-api/definition` (estilo ABP `/api/abp/api-definition`).
- **Versionamento** opt-in via `Asp.Versioning` (`AddAutoApiVersioning`).
- **Testes** (xUnit) para `ApiRouteBuilder`, `TypeHelper` e o source generator.
