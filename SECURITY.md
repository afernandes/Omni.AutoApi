# Política de Segurança

## Como reportar uma vulnerabilidade

**Não abra uma issue pública** para vulnerabilidades — isso as expõe antes que exista correção.

Use o **[Private Vulnerability Reporting do GitHub](https://github.com/afernandes/Omni.AutoApi/security/advisories/new)**
(aba *Security* → *Report a vulnerability*). O canal é privado e permite discutir a correção e
publicar um advisory coordenado.

Ao reportar, ajuda muito incluir:

- versão dos pacotes `AndersonN.Omni.AutoApi.*` afetada;
- um cenário mínimo que reproduza o problema;
- o impacto que você enxerga (vazamento de dados, bypass de autorização, DoS…).

### O que esperar

| Etapa | Prazo alvo |
|---|---|
| Confirmação de recebimento | 5 dias úteis |
| Avaliação inicial e severidade | 10 dias úteis |
| Correção ou plano de mitigação | conforme severidade |

Este é um projeto mantido em tempo pessoal — os prazos são metas de melhor esforço, não SLA.

## Versões suportadas

Enquanto o projeto estiver em `0.x`, **apenas a versão estável mais recente** recebe correções de
segurança. Após a `1.0`, esta seção passará a listar as faixas suportadas.

| Versão | Suportada |
|---|---|
| 0.1.x | ✅ |

## Escopo

São considerados vulnerabilidade, entre outros:

- **bypass de autorização** — uma ação com `[Authorize]`/policy ficar acessível sem credencial
  válida (a convenção manipula metadados de endpoint, então esta é uma área sensível por natureza);
- **exposição indevida de dados** — um Application Service, método ou propriedade ser publicado
  como endpoint sem que o autor tivesse essa intenção;
- **vazamento de informação em erros** — detalhes internos escaparem no `ProblemDetails`
  (mensagens de exceções de framework são mascaradas de propósito);
- injeção via binding de parâmetros ou desserialização.

**Fora de escopo:** problemas nos projetos em `samples/`, que são demonstrações didáticas. Em
particular, `samples/Omni.AutoApi.Sample.Web` contém uma chave de assinatura JWT versionada e um
endpoint `/dev/token` que emite tokens sem autenticação — ambos existem só para o exemplo ser
executável e estão marcados como tal no código. Não use nenhum dos dois como base para produção.

## Boas práticas ao usar a biblioteca

- Exponha como `IRemoteService` **apenas** o que deve ser público: todo método público vira endpoint.
- Prefira DTOs explícitos a expor entidades de domínio diretamente.
- Aplique `[Authorize]` no Application Service (classe ou método) — funciona normalmente e é coberto
  por testes de integração.
- Em produção, mantenha a `BaseAddress` dos clientes em configuração, nunca no código.
