# Contribuindo com o Omni.AutoApi

Obrigado pelo interesse! Este guia cobre o essencial para uma contribuição entrar rápido.

## Pré-requisitos

- **.NET SDK 10** — a versão exata está fixada em [`global.json`](global.json). Não altere esse
  arquivo num PR de feature; o CI usa exatamente o que está ali.
- O sample [`samples/BlazorMauiAuto`](samples/BlazorMauiAuto) exige **SDK 11 + workloads MAUI** e
  tem `global.json` próprio. Ele **não** faz parte de `Omni.AutoApi.sln` — abra
  `samples/BlazorMauiAuto/SharedApp.sln` separadamente.

## Ciclo de desenvolvimento

```bash
dotnet build Omni.AutoApi.sln
dotnet test  Omni.AutoApi.sln
dotnet format Omni.AutoApi.sln          # antes de commitar
```

O CI roda `dotnet format --verify-no-changes` e **falha** se o código divergir do
[`.editorconfig`](.editorconfig). Rodar `dotnet format` antes do commit evita esse ping-pong.

> **Dica no Windows:** se o build falhar com `MSB3027` dizendo que
> `Omni.AutoApi.Client.SourceGenerator.dll` está bloqueado, é o language server da sua IDE
> segurando o analisador. Feche a IDE (ou encerre `csharp-ls`/`CSharpLanguageServer`) e rode de novo.

## Mudanças na API pública

A superfície pública é travada pelo `Microsoft.CodeAnalysis.PublicApiAnalyzers`. Se você
adicionar, remover ou alterar um membro público, o build falha com **RS0016/RS0017** até você
atualizar os arquivos correspondentes:

- `src/<Projeto>/PublicAPI.Unshipped.txt` — **é aqui que entram as novidades** (vão para a próxima
  release).
- `src/<Projeto>/PublicAPI.Shipped.txt` — API já publicada. Só é alterado no momento de uma
  release, movendo o conteúdo de `Unshipped` para cá.

A forma mais fácil de atualizar é o code fix da IDE ("Add to public API") ou:

```bash
dotnet format analyzers src/<Projeto> --diagnostics RS0016 --severity warn
```

Isso é proposital: garante que nenhum *breaking change* entre sem alguém decidir conscientemente.

## Testes

Toda correção de bug deve vir com um teste que **falha antes** e passa depois. Onde colocar:

| Tipo | Projeto | Quando usar |
|---|---|---|
| Unitário | `tests/Omni.AutoApi.Tests` | regras de rota, `TypeHelper`, saída do source generator, proxy dinâmico |
| Integração | `tests/Omni.AutoApi.IntegrationTests` | qualquer coisa que dependa do pipeline MVC real (roteamento, binding, filtros, auth, versionamento) |

Testes de integração usam `WebApplicationFactory` sobre o sample, ou um host próprio com
`WebApplication.CreateBuilder()` + `UseTestServer()` quando precisam de configuração específica
(veja `RouteOptionsTests` e `VersioningTests`).

## Padrão de commits

Prefixo no estilo *conventional commits* — `feat:`, `fix:`, `test:`, `docs:`, `ci:`, `build:`,
`refactor:`. O corpo deve explicar **por quê**, não o quê (o diff já mostra o quê).

## Antes de abrir o PR

- [ ] `dotnet build` sem avisos novos
- [ ] `dotnet test` verde
- [ ] `dotnet format` aplicado
- [ ] `PublicAPI.Unshipped.txt` atualizado, se mexeu na API pública
- [ ] [`CHANGELOG.md`](CHANGELOG.md) atualizado em `[Unreleased]`, se for mudança visível ao usuário

## Escopo e roadmap

Antes de investir tempo numa feature grande, dê uma olhada no [ROADMAP.md](ROADMAP.md) — o item
pode já estar planejado (e com uma abordagem definida) ou ter sido deliberadamente adiado. Para
mudanças estruturais, abra uma issue antes de codar.

## Divergências intencionais em relação ao ABP

O projeto se inspira nos *Auto API Controllers* do ABP, mas diverge de propósito em alguns pontos
(o nome da ação preserva o prefixo do verbo, `id` simples vai para a query em vez de `/{id}`).
Não trate isso como bug sem antes discutir numa issue.
