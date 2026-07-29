# Roadmap — Omni.AutoApi

Estado atual: **v0.1.0 publicada** ([`AndersonN.Omni.AutoApi.*`](https://www.nuget.org/packages/AndersonN.Omni.AutoApi.AspNetCore)),
4 pacotes (`net9.0;net10.0` + gerador `netstandard2.0`), **115 testes** (53 unitários + 62 de
integração), CI verde e publicação via Trusted Publishing/OIDC.

Este documento lista o que falta, em ordem de prioridade. Itens marcados **✅** já foram entregues
(ver [CHANGELOG.md](CHANGELOG.md)). A numeração (`R1`, `R2`, …) é estável:
use-a em issues e commits.

---

## Lista priorizada

### P0 — Fazer agora (fundação / risco)

| # | Item | Tipo | Esforço |
|---|------|------|---------|
| R1 | Reservar o prefixo `AndersonN.*` no nuget.org | Higiene | 10 min |
| ~~R2~~ ✅ | `global.json` fixando o SDK | Débito técnico | 15 min |
| ~~R3~~ ✅ | Badges + seção de instalação no README | Adoção | 20 min |
| R4 | Publicar o GitHub Release da v0.1.0 | Higiene | 15 min |

### P1 — Antes da v0.2 (qualidade e confiança)

| # | Item | Tipo | Esforço |
|---|------|------|---------|
| ~~R5~~ ✅ | Teste E2E de versionamento (`AddAutoApiVersioning` + `[ApiVersion]`) | Teste | 2–3 h |
| ~~R6~~ ✅ | Teste E2E de `RouteOptions` customizado no servidor | Teste | 1–2 h |
| ~~R7~~ ✅ | `PublicApiAnalyzers` — travar mudanças de API pública | Débito técnico | 2 h |
| ~~R8~~ ✅ | `.editorconfig` + `dotnet format` no CI | Débito técnico | 1–2 h |
| ~~R9~~ ✅ | `CONTRIBUTING.md` e `SECURITY.md` | Higiene | 1 h |
| R10 | Dependabot (NuGet + Actions) | Higiene | 30 min |
| ~~R11~~ ✅ | Cenário de autenticação real documentado (JWT + policies) | Feature essencial | 3–4 h |

### P2 — v0.3 (produtividade e alcance)

| # | Item | Tipo | Esforço |
|---|------|------|---------|
| ~~R12~~ ✅ | `AddAutoApiServer<T>()` — controller + in-process numa chamada | Feature | 2–3 h |
| ~~R13~~ ✅ | Documentos OpenAPI por versão de API | Feature | 4–6 h |
| ~~R14~~ ✅ | Analisador de uso (erros em compile-time, não no startup) | Feature | 1–2 dias |
| R15 | Central Package Management (`Directory.Packages.props`) | Débito técnico | 1–2 h |
| R16 | Cobertura de código no CI (coverlet + relatório) | Teste | 2 h |
| ~~R17~~ ✅ | Teste do ciclo de vida do ativador de controllers | Teste | 1–2 h |
| R18 | Template `dotnet new autoapi` | DX | 1 dia |

### P3 — Futuro / caminho para a v1.0

| # | Item | Tipo | Esforço |
|---|------|------|---------|
| R19 | Geração de clientes TypeScript/Angular | Feature | 3–5 dias |
| R20 | Validação de contrato cliente↔servidor em runtime | Feature | 2–3 dias |
| R21 | Unit of Work / interceptors (paridade ABP) | Feature | 3–5 dias |
| R22 | Auditoria automática | Feature | 2–3 dias |
| R23 | Multi-tenancy | Feature | 5+ dias |
| R24 | Integração opcional com FluentValidation | Feature | 1–2 dias |
| R25 | Benchmarks (BenchmarkDotNet) | Teste | 2 dias |
| R26 | Site de documentação (GitHub Pages) | DX | 2–3 dias |
| R27 | Compromisso de estabilidade v1.0 | Processo | — |

---

## Detalhamento

### R1 — Reservar o prefixo `AndersonN.*` no nuget.org

**Por quê.** A tentativa de publicar como `Omni.*` falhou com `409 The package ID is reserved`
justamente porque outra conta reservou aquele prefixo. Sem reservar `AndersonN.*`, qualquer pessoa
pode publicar um `AndersonN.Omni.AutoApi.Alguma.Coisa` e se passar por parte da sua biblioteca. A
reserva também dá o selo de *verified owner* na página do pacote.

**Como.** nuget.org → sua conta → *Reserved namespaces* → solicitar `AndersonN.*`. Como você já tem
pacotes publicados sob esse prefixo, a aprovação costuma ser direta.

**Pronto quando.** Os 4 pacotes exibem o ✔ azul de prefixo reservado no nuget.org.

---

### R2 — ✅ CONCLUÍDO — `global.json` fixando o SDK

**Por quê.** Este é o débito mais arriscado hoje. A máquina de desenvolvimento usa **SDK 11 preview**
e o CI usa **10.0.x** — o build depende de qual SDK estiver instalado. O source generator é
especialmente sensível: ele compila contra Roslyn, e uma divergência de versão pode gerar código
diferente (ou falhar) entre a sua máquina e o CI, com sintoma confuso.

**Como.**
```json
{ "sdk": { "version": "10.0.100", "rollForward": "latestFeature" } }
```
Alinhar o `dotnet-version` dos workflows ao mesmo valor. Atenção: o sample `BlazorMauiAuto` usa
`net11.0` e workloads MAUI — se o `global.json` na raiz travar o SDK 10, esse sample deixa de
compilar. Duas saídas: (a) `global.json` só sobre `src/`+`tests/`, ou (b) mover o sample MAUI para
fora da solução principal e documentar que ele exige SDK 11.

**Pronto quando.** `dotnet --version` na raiz retorna a versão fixada e o CI usa a mesma.

---

### R3 — ✅ CONCLUÍDO — Badges + seção de instalação no README

**Por quê.** O README documenta bem os conceitos, mas quem chega pela primeira vez não vê
imediatamente: build passa? qual a versão publicada? Badges são o sinal de confiança padrão em OSS.

**Como.** No topo do README: badge de status do CI, badge de versão/downloads do NuGet
(`img.shields.io/nuget/v/AndersonN.Omni.AutoApi.AspNetCore`) e badge de licença. A seção de
instalação já existe — vale destacá-la logo após os badges.

**Pronto quando.** Badges renderizam corretamente na página do repositório.

---

### R4 — Publicar o GitHub Release da v0.1.0

**Por quê.** A tag `v0.1.0` existe, mas não há *Release* no GitHub. Releases são o que aparece em
feeds, no "Releases" da sidebar e o que ferramentas de changelog consomem.

**Como.** `gh release create v0.1.0 --title "v0.1.0" --notes-file <trecho do CHANGELOG>`, anexando
os `.nupkg`. Opcionalmente automatizar no `release.yml` (exige `contents: write` — hoje o workflow
usa `contents: read` por princípio de menor privilégio; adicione a permissão só se automatizar).

**Pronto quando.** A release aparece em `github.com/afernandes/Omni.AutoApi/releases`.

---

### R5 — ✅ CONCLUÍDO — Teste E2E de versionamento

**Por quê.** `AddAutoApiVersioning` existe e compila, mas **nenhum teste exercita o caminho
completo**. Não sabemos empiricamente se `[ApiVersion("2.0")]` num Application Service funciona com
a convenção de rotas — e há um risco concreto: a convenção manipula `Selectors` e `EndpointMetadata`,
exatamente onde o `Asp.Versioning` também atua. Foi um bug desse tipo que quebrou o `[Authorize]`
silenciosamente (descoberto só quando escrevi o teste).

**Como.** Serviço de teste com duas versões, requisições com `?api-version=1.0` e `2.0` e via header
`X-Api-Version`, validando roteamento correto e o comportamento quando a versão é omitida.

**Pronto quando.** Testes cobrindo v1/v2 por query e header, verdes.

---

### R6 — ✅ CONCLUÍDO — Teste E2E de `RouteOptions` customizado

**Por quê.** `RouteOptions` (prefixo, sufixos, kebab/camel) só é testado **unitariamente** no
`ApiRouteBuilder`. Não há nenhuma requisição HTTP real contra um servidor configurado com prefixo
customizado. Como cliente e servidor derivam a rota da mesma fonte, uma divergência aqui só
apareceria em produção, como 404.

**Como.** `WebApplicationFactory` com `AddAutoApiServices(o => { o.Prefix = "api/v2/services";
o.ControllerPostfixes = ["Handler"]; })`, um `TodoHandler`, e um `GET /api/v2/services/todo/...`
esperando 200. Idealmente também o cliente gerado com as mesmas opções via MSBuild.

**Pronto quando.** Teste de integração verde com prefixo e sufixos não-padrão.

---

### R7 — ✅ CONCLUÍDO — `PublicApiAnalyzers`

**Por quê.** A biblioteca tem ~25 tipos públicos e acabou de ser publicada. A partir de agora,
qualquer mudança acidental de assinatura é um *breaking change* para quem instalou. O
`Microsoft.CodeAnalysis.PublicApiAnalyzers` transforma a superfície pública em arquivos versionados
(`PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt`) — alterar a API sem atualizar o arquivo vira
erro de compilação. É a proteção mais barata contra quebra acidental.

**Como.** Adicionar o pacote aos 3 projetos de runtime, gerar o baseline com o code fix da IDE e
commitar. O que estiver em `Unshipped` no momento da release move para `Shipped`.

**Pronto quando.** Renomear um método público quebra o build com diagnóstico RS0016/RS0017.

---

### R8 — ✅ CONCLUÍDO — `.editorconfig` + `dotnet format` no CI

**Por quê.** Não existe `.editorconfig`. Hoje o estilo é consistente porque uma única pessoa
escreveu, mas isso não sobrevive ao primeiro contribuidor externo. Sem isso, PRs viram discussão de
formatação em vez de conteúdo.

**Como.** `.editorconfig` com as convenções do .NET (indentação, `var`, ordenação de `using`, chaves,
`file_header_template` se quiser) e um passo `dotnet format --verify-no-changes` no CI.

**Pronto quando.** `dotnet format --verify-no-changes` passa e o CI falha se alguém desformatar.

---

### R9 — ✅ CONCLUÍDO — `CONTRIBUTING.md` e `SECURITY.md`

**Por quê.** São os dois arquivos que o GitHub exibe ativamente (botão "Contribute", aba "Security").
Sem `SECURITY.md`, um pesquisador que encontre uma vulnerabilidade abre uma **issue pública**.

**Como.** `CONTRIBUTING.md`: como rodar `dotnet test`, o padrão de commits, e o aviso de que mudanças
de API pública exigem atualizar os arquivos do R7. `SECURITY.md`: canal privado de reporte
(GitHub Private Vulnerability Reporting, que é gratuito) e política de versões suportadas.

**Pronto quando.** Ambos na raiz e o GitHub exibindo os atalhos.

---

### R10 — Dependabot

**Por quê.** O projeto depende de `Asp.Versioning`, `Microsoft.CodeAnalysis`, `xunit` e Actions. Já
tivemos um alerta real de vulnerabilidade nesta sessão (`Microsoft.OpenApi`, GHSA-v5pm-xwqc-g5wc),
descoberto por acaso num warning de build — não por processo.

**Como.** `.github/dependabot.yml` com os ecossistemas `nuget` e `github-actions`, agrupando updates
de patch para reduzir ruído.

**Pronto quando.** O Dependabot abre PRs automaticamente.

---

### R11 — ✅ CONCLUÍDO — Cenário de autenticação real documentado

**Por quê.** Os testes provam que `[Authorize]` funciona (401/403) com um esquema *fake*. Falta o
caminho que todo usuário real vai percorrer: **JWT Bearer + policies/roles**, e — do lado cliente —
como anexar o token. Hoje o README menciona `DelegatingHandler` em uma linha, sem exemplo completo.
Sem isso, o primeiro usuário sério trava aqui.

**Como.** Ampliar o sample com JWT Bearer, um serviço protegido por policy e um `AuthTokenHandler`
encadeado via `AddTodoAppServiceClient(...).AddHttpMessageHandler<...>()`. Um teste de integração com
token válido/inválido/sem permissão. Seção dedicada no README.

**Pronto quando.** Sample roda autenticado ponta a ponta e os testes cobrem 200/401/403.

---

### R12 — ✅ CONCLUÍDO — `AddAutoApiServer<T>()`

**Por quê.** Hoje, para usar o serviço **in-process** no servidor (padrão do Blazor Server), é preciso
registrar duas coisas: `AddAutoApiServices()` e `AddScoped<ITodoAppService, TodoApplicationService>()`.
É repetitivo e fácil de esquecer — e o sintoma do esquecimento é um erro de DI em runtime.

**Como.** `services.AddAutoApiServer<ITodoAppService, TodoApplicationService>()` registrando a
implementação e garantindo a exposição como controller. Analisar se vale um overload por assembly.

**Pronto quando.** O sample `BlazorMauiAuto` usa uma linha por serviço.

---

### R13 — ✅ CONCLUÍDO — OpenAPI por versão

**Por quê.** Complementa o R5. Com versionamento ativo, hoje sai **um único documento** OpenAPI
misturando versões. O consumidor não consegue gerar um cliente só da v2.

**Como.** Integrar `Asp.Versioning.ApiExplorer`, criar um documento por `GroupName` e ajustar o
`AutoApiResponseEnrichmentProvider`/UI (Scalar) para listar as versões. Atenção ao
`AutoApiDefinitionEndpoint`: ele hoje deduplica por `verbo + rota`, o que **colapsaria** ações que
existem em duas versões — precisa passar a considerar a versão na chave.

**Pronto quando.** `/openapi/v1.json` e `/openapi/v2.json` existem e o api-definition distingue versões.

---

### R14 — ✅ CONCLUÍDO — Analisador de uso (compile-time)

**Por quê.** Vários erros de uso só aparecem **no startup** (colisão de rota por sobrecarga) ou em
**runtime** (esquecer de registrar a implementação). Um analisador Roslyn os anteciparia para a
digitação — a mesma filosofia do `AUTOAPI001`, que já funciona bem no cliente.

**Como.** Novo analisador (ou estender o existente) com diagnósticos: `AUTOAPI002` sobrecarga em
`IRemoteService`, `AUTOAPI003` DTO complexo em GET sem propriedades públicas, `AUTOAPI004` método
público sem `Async`/retorno não-Task. Publicar dentro do pacote `AspNetCore`.

**Pronto quando.** Escrever uma sobrecarga acende squiggle na IDE, sem precisar rodar a aplicação.

---

### R15 — Central Package Management

**Por quê.** As versões estão espalhadas por ~10 `.csproj`. Já há uma divergência **intencional**
(`Microsoft.Extensions.Http` 9.0.0 vs 10.0.0, condicional por TFM), mas o resto é manutenção manual —
e foi exatamente onde o `Microsoft.OpenApi` vulnerável passou despercebido.

**Como.** `Directory.Packages.props` com `ManagePackageVersionsCentrally`. Cuidado: o multi-targeting
condicional exige `VersionOverride` ou condições por TFM.

**Pronto quando.** Nenhum `Version=` nos `.csproj` (exceto overrides justificados).

---

### R16 — Cobertura de código no CI

**Por quê.** São 60 testes, mas ninguém sabe **quanto** do código eles tocam. Sem o número, é palpite
decidir onde investir — e regressões de cobertura passam sem alarme.

**Como.** `coverlet.collector` nos projetos de teste, `--collect:"XPlat Code Coverage"` no CI e
publicação do relatório (Codecov ou artefato do ReportGenerator). Definir um piso e falhar abaixo dele.

**Pronto quando.** O CI publica a porcentagem e há badge no README.

---

### R17 — ✅ CONCLUÍDO — Teste do ciclo de vida do activator

**Por quê.** O `AutoApiControllerActivator` **substitui o ativador padrão do MVC** — é o ponto mais
invasivo da biblioteca. Testamos que controllers são *criados* (inclusive os normais, como o
`WeatherForecastController`), mas não que são **liberados**: `Release`/`ReleaseAsync` descartam
`IDisposable`/`IAsyncDisposable` sem nenhuma asserção. Um vazamento aqui só apareceria sob carga.

**Como.** `ApplicationService` de teste implementando `IDisposable` e `IAsyncDisposable`, com
asserção de que o dispose ocorre por requisição.

**Pronto quando.** Testes cobrindo os dois caminhos, verdes.

---

### R18 — Template `dotnet new autoapi`

**Por quê.** Hoje o onboarding é ler o README e montar o projeto à mão. Um template reduz o
"primeiro endpoint funcionando" a um comando — é o que mais move a agulha de adoção.

**Como.** Pacote de template com o servidor configurado, um Application Service de exemplo, o
contrato `[AutoApiClient]` e opções (`--with-client`, `--with-blazor`). Publicar como
`AndersonN.Omni.AutoApi.Templates`.

**Pronto quando.** `dotnet new install ...` + `dotnet new autoapi` gera projeto que roda.

---

### R19 — Clientes TypeScript/Angular

**Por quê.** É o **maior diferencial ainda ausente** frente ao ABP, que gera proxies para
Angular/JS. O endpoint `/api/auto-api/definition` já existe justamente como fonte de verdade — falta
o gerador que o consome. Destrava o público front-end, hoje fora do alcance.

**Como.** Ferramenta CLI (`dotnet tool`) que lê o JSON do api-definition e emite serviços TS tipados
(fetch ou `HttpClient` do Angular) + interfaces dos DTOs. Exige enriquecer o api-definition com o
**shape** dos DTOs, que hoje expõe só o nome do tipo.

**Pronto quando.** `autoapi generate-proxy -t ts` produz cliente que compila e chama a API.

---

### R20 — Validação de contrato em runtime

**Por quê.** Rota e verbo derivam da mesma fonte nos dois lados, mas **a assinatura não é validada**.
Se o servidor evoluir sem o cliente, o sintoma é 404/400 em produção, difícil de diagnosticar.

**Como.** Handshake opcional: o cliente compara um hash do contrato com `/api/auto-api/definition` no
startup e emite warning (ou falha, configurável). Precisa ser opt-in e barato.

**Pronto quando.** Cliente desatualizado produz mensagem clara em vez de 404 silencioso.

---

### R21 — Unit of Work / interceptors

**Por quê.** É o maior gap conceitual frente ao ABP. Lá, `IUnitOfWorkEnabled` dá commit/rollback
automático por requisição. Aqui, cada serviço gerencia a transação manualmente.

**Como.** Filtro/interceptor que abre a UoW no início da action e faz commit no sucesso / rollback na
exceção, integrado ao `AutoApiExceptionFilter`. Deve ser **agnóstico de ORM** (abstração `IUnitOfWork`
com implementação opcional para EF Core em pacote separado) para não impor dependência.

**Pronto quando.** Exceção numa action reverte a transação sem código no serviço.

---

### R22 — Auditoria automática

**Por quê.** Complementa o R21; no ABP é `IAuditingEnabled`. Registrar usuário, duração, parâmetros e
exceções por chamada é requisito comum em aplicações corporativas.

**Como.** Filtro que coleta os dados e delega a um `IAuditLogStore` plugável (padrão: `ILogger`).
Cuidado com dados sensíveis nos parâmetros — precisa de máscara/opt-out por propriedade.

**Pronto quando.** Chamadas geram entradas de auditoria com opt-out por serviço/método.

---

### R23 — Multi-tenancy

**Por quê.** Presente no ABP e frequentemente decisivo em SaaS. É o item mais caro e invasivo —
atravessa resolução de tenant, filtro de dados e cache.

**Como.** `ICurrentTenant` (espelhando o `ICurrentUser` já existente), resolvedores plugáveis (header,
subdomínio, claim) e propagação do tenant no cliente HTTP. Avaliar seriamente se cabe no escopo da
biblioteca ou se vira um pacote satélite.

**Pronto quando.** Requisições resolvem o tenant e o cliente o propaga.

---

### R24 — FluentValidation opcional

**Por quê.** A validação por DataAnnotations já funciona (R já entregue). FluentValidation é o padrão
em projetos maiores e o ABP suporta os dois.

**Como.** Pacote `...AspNetCore.FluentValidation` que estende o `AutoApiValidationFilter` para
executar validadores registrados, mantendo o mesmo formato `ProblemDetails`. Opcional, sem tocar o
pacote base.

**Pronto quando.** Um `AbstractValidator<T>` registrado produz o mesmo 400 padronizado.

---

### R25 — Benchmarks

**Por quê.** Afirmamos no README que o cliente gerado é "mais rápido que o proxy dinâmico" — isso é
**plausível, mas não medido**. Sem número, é marketing. Também não sabemos o custo do
`LazyServiceProvider` e do ativador customizado por requisição.

**Como.** BenchmarkDotNet comparando: cliente gerado × proxy dinâmico × `HttpClient` manual; e
overhead do activator × ativador padrão do MVC. Publicar os resultados no README.

**Pronto quando.** Resultados reprodutíveis documentados.

---

### R26 — Site de documentação

**Por quê.** O README já está longo (~215 linhas) e cobre servidor, cliente, samples e limitações.
Conforme R11/R13/R19 forem entrando, ele não escala.

**Como.** DocFX ou Docusaurus publicado no GitHub Pages, com guias (getting started, servidor,
cliente, distribuição, migração do ABP) e a API reference gerada dos XML docs — que já são
empacotados desde a v0.1.0.

**Pronto quando.** Site no ar e README reduzido a visão geral + links.

---

### R27 — Compromisso de estabilidade v1.0

**Por quê.** Em `0.x`, o SemVer permite quebrar a qualquer momento — e usuários sérios hesitam em
adotar. A v1.0 é o compromisso de que a superfície pública é estável.

**Como.** Pré-requisitos: R7 (API travada), R16 (cobertura conhecida), R11 (auth documentado) e
decisão explícita sobre R21/R23 (se entram antes ou depois do 1.0, pois mudariam a superfície).
Documentar a política de suporte no `SECURITY.md`.

**Pronto quando.** `1.0.0` publicada com política de compatibilidade declarada.

---

## Notas de manutenção

- **Ordem sugerida de execução:** R1–R4 numa sentada; depois R5–R7 (os que protegem contra
  regressão) antes de qualquer feature nova.
- **Itens conhecidos e aceitos** (não são bugs, estão documentados no README): achatamento de 1 nível
  em query strings de DTO complexo; sobrecargas de método não suportadas; assinatura de contrato não
  validada (endereçado no R20).
- **Divergências intencionais frente ao ABP:** nome de ação preserva o prefixo do verbo
  (`get-todos`, não `todos`) e `id` simples vai para query em vez de `/{id}` no path.
