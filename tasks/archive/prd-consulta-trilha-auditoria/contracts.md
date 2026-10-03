---
tsg_artifact: contract
product: code-4-coders
capability: CAP-030
version: 1.2
status: approved
updated: 2026-09-28
sources: tasks/prd-consulta-trilha-auditoria/prd.md@1.0, context/architecture-baseline.md@1.2, domains/auditoria-e-conformidade/domain.md@1.2, domains/identidade-e-acesso/domain.md@1.1
---

# Contratos de integração — consulta e complemento da trilha

> PRD: [prd.md](prd.md) v1.0, aprovado em 2026-09-27.  
> Revisão: 2026-09-28. **Estado do conjunto: Aprovado para implementação.**

Este conjunto revisa o acordo aprovado em 2026-09-27 para o segundo PRD de `CAP-030`. O responsável autorizou substituir as buscas GET por POST antes da implementação, para manter referências de pessoas fora de URLs. Em 2026-09-28, o resumo passou a expor `role` nullable para que a lista mostre o papel junto ao tipo do ato, conforme o Figma aprovado. O campo deriva somente do atributo `papel` de atos de concessão ou revogação; não expõe o complemento completo. Os contratos do primeiro PRD e de `CAP-002` permanecem intactos.

## Seleção e escopo

| Integração identificada | Modalidade | Justificativa |
|---|---|---|
| `admin-spa` → `bff-admin` e `bff-admin` → `audit`/`identity` | OpenAPI | Consulta, detalhe, confirmação e identificação legível exigem interfaces HTTP. O SPA conhece só o BFF. |
| `bff-admin` → `audit` | AsyncAPI | O complemento nasce somente do fato de confirmação recebido pela Auditoria (OD38, RN-A02). |
| Trilha como produto de dados | Não aplicável | O banco é interno de Auditoria; nenhum consumidor recebe dataset com compromisso próprio. A trilha não autoriza outros domínios (RN-A10). Nenhum ODCS gerado. |

| Documento | Modalidade e versão do padrão | Versão do contrato | Fronteira coberta | Estado |
|---|---|---|---|---|
| [api-contract.yaml](api-contract.yaml) e [api-contract.md](api-contract.md) | OpenAPI 3.1.0 | 1.2.0 | Recorte `admin-spa` → `bff-admin` deste PRD | Validado, Aprovado para implementação |
| [internal-api-contract-audit.yaml](internal-api-contract-audit.yaml) e [internal-api-contract-audit.md](internal-api-contract-audit.md) | OpenAPI 3.1.0 | 1.2.0 | Recorte `bff-admin` → `audit` para leitura | Validado, Aprovado para implementação |
| [internal-api-contract-identity.yaml](internal-api-contract-identity.yaml) e [internal-api-contract-identity.md](internal-api-contract-identity.md) | OpenAPI 3.1.0 | 1.0.0 | Recorte `bff-admin` → `identity` para resolver referências | Validado, Aprovado para implementação |
| [asyncapi-contract.yaml](asyncapi-contract.yaml) | AsyncAPI 3.0.0 | 1.1.0 | Recorte aditivo da aplicação `audit`: novo canal de complemento | Validado, Aprovado para implementação |

## Participantes e interfaces

| Identificador técnico | Provedor/produtor | Consumidor conhecido | Comportamento | PRD |
|---|---|---|---|---|
| `listAuditRecords` | `bff-admin` | `admin-spa` | Lista originais por tenant, filtros combinados, período inclusivo e páginas estáveis | RF-01, RF-02, RF-05 |
| `getAuditRecord` | `bff-admin` | `admin-spa` | Mostra fato recebido, faltas, referências resolvidas quando disponíveis e complementos | RF-01, RF-03, RF-05 |
| `confirmAuditRecordComplement` | `bff-admin` | `admin-spa` | Aceita confirmação idempotente com explicação; retorna `202` e `confirmationId` | RF-01, RF-04 |
| `listAuditRecordsInternal`, `getAuditRecordInternal` | `audit` | `bff-admin` | Revalida JWT, papel e tenant; retorna referências, não dados pessoais de Identity | RF-01 a RF-03, RF-05 |
| `resolveAuditIdentityReferencesInternal` | `identity` | `bff-admin` | Resolve conta/convite sob sessão administrativa; ausência e outro tenant não se distinguem | RF-01, RF-03 |
| `receberComplementoConfirmado` · `auditoria.registro.complemento-confirmado.v1` · `ComplementoConfirmado` | `bff-admin`, por outbox | `audit` | Cria um único registro complementar ao consumir o fato; original fica intacto | RF-04, RF-05 |

Relação: `confirmAuditRecordComplement` devolve `confirmationId`; o produtor usa o mesmo ID em `ComplementoConfirmado.confirmationId`; `receberComplementoConfirmado` grava um `Complement` que expõe esse ID no detalhe. `getAuditRecordInternal` fornece referências a `getAuditRecord`; o BFF as apresenta com `label` de `resolveAuditIdentityReferencesInternal`, sem persisti-lo em Auditoria. Os schemas HTTP e o evento têm finalidades distintas e não são compartilhados automaticamente.

## Origem e decisões

| Entrada consultada | Versão e revisão/data | Decisão herdada ou aplicada |
|---|---|---|
| [PRD atual](prd.md) | v1.0, aprovado 2026-09-27 | RF-01 a RF-05, OD38 e OD39: administrador apenas, original imutável e complemento posterior por fato recebido |
| [Baseline arquitetural](../../../context/architecture-baseline.md) | v1.2, 2026-09-21 | BFF por audiência, JWT interno, outbox, RabbitMQ, banco por serviço, tenant obrigatório, sem dado pessoal em telemetria |
| [Domínio Auditoria](../../../domains/auditoria-e-conformidade/domain.md) | v1.2, 2026-09-27 | RN-A01 a RN-A14, em especial recebimento do fato, referências e isolamento |
| [Domínio Identidade](../../../domains/identidade-e-acesso/domain.md) | v1.1, revisão consultada 2026-09-27 | RN-17, RN-18, RN-23 a RN-25: revogação, claims e identidade preservada |
| [ADR-0005](../../../docs/adr/0005-sessao-e-servico-do-backoffice.md) | Accepted, 2026-09-22 | `bff-admin` revalida sessão em cada ação; Identity emite JWT de audiência `audit`; serviço dono valida localmente |
| [Contrato anterior de Auditoria](../prd-trilha-auditoria/asyncapi-contract.yaml) e [índice](../prd-trilha-auditoria/contracts.md) | AsyncAPI 1.0.1, índice v1.2, 2026-09-27 | Preserva `auditoria.ato-praticado.v1`, AMQP, correlação, confirmação manual e regra de ingestão |
| [Contratos de acesso interno](../prd-acesso-interno/contracts.md) | Índice v1.4, 2026-09-27; OpenAPI interno 1.0.1 | Reusa cookie opaco, `X-CSRF-Token`, `X-Staff-Session`, asserção do BFF, JWKS e erros Problem; amplia Identity com leitura de referências |

Decisões novas aprovadas pelo responsável pelo PRD em 2026-09-27:

1. **C-01 — Página estável:** `_page`/`_size` e `snapshot` ficam no corpo JSON da busca POST, com `snapshot` opaco que fixa os IDs elegíveis na primeira página. `practicedAt` e ID definem a ordem; quando falta o momento do ato, usa-se `receivedAt` apenas para posicionar o registro. O filtro de período incide somente sobre `practicedAt`: registro sem esse valor não corresponde a um intervalo informado. O contrato promete ausência de duplicação/perda durante a navegação; a TechSpec escolhe o mecanismo e o ciclo de vida do snapshot.
2. **C-02 — Resolução no BFF:** Identity fornece `label` somente no momento da leitura. Falha de resolução conserva `type`/`id` sem nome inventado. A nova operação interna exige escopo `audit-references:read` e sessão de administrador.
3. **C-03 — Confirmação assíncrona:** o BFF produz `auditoria.registro.complemento-confirmado.v1` após aceitar a ação. `202` não declara o complemento gravado. O produtor precisa de persistência durável e outbox; a TechSpec define onde e como, respeitando a fronteira de dados. O evento não é um pedido de correção.
4. **C-04 — Deduplicação:** `Idempotency-Key` da chamada vale 24 horas como nas APIs existentes; o `confirmationId` estável deduplica o consumo, e o detalhe o devolve para conciliação da interface.
5. **C-05 — Erro sem vazamento:** registro inexistente, de outro tenant ou complemento usado como original retornam o mesmo `404`; motivos e explicações não entram em URL, telemetria nem Problem.

## Evolução e compatibilidade

- O contrato `auditoria.ato-praticado.v1` 1.0.1 continua sem alteração. O novo canal é aditivo para `audit`, mas a compatibilidade operacional exige **implantar o consumidor antes de `bff-admin` publicar**. O contrato deste PRD descreve só o recorte novo; não declara a interface completa de `audit`.
- As operações HTTP públicas e internas são novas. As buscas GET do acordo 1.0.0 foram substituídas por POST antes da implementação, por autorização do responsável em 2026-09-27. `authorId` e `targetId` ficam no corpo, e a busca pública exige CSRF por ser POST. Não existe consumidor implantado dessas operações para migrar; compatibilidade com consumidores externos e com produção **não foi verificada**.
- O contrato interno de Identity em `CAP-002` não é editado. A nova operação requer escopo próprio e leitura de contas/convites no tenant; Identity deve aceitar o novo escopo apenas para `bff-admin`.
- A revisão 1.2.0 acrescenta somente o campo opcional e nullable `role` ao resumo HTTP público e interno. O serviço `audit` lê o valor `papel` já armazenado no original e devolve apenas esse valor para concessões/revogações. A mudança é aditiva para consumidores existentes; não há migração de persistência nem mudança na mensagem de auditoria.
- A mensagem usa `confirmationId`, `tenantId`, `originalRecordId`, `confirmedAt`, `author` por referência e `explanation`. Não reutiliza `AtoPraticado`: a semântica e a deduplicação diferem. Falta de identidade estrutural ou original válido leva à fila de erro, sem criar complemento.
- **Identity 1.0.0 → 1.1.0 (C-07 de `tasks/prd-concessao-acesso/contracts.md`, 2026-10-01):** aditiva. `resolveAuditIdentityReferencesInternal` passa a aceitar a referência `conta-aluno`, alvo do ato `cortesia-concedida`, e devolve o **nome** da conta como rótulo, nunca o e-mail. Referência não resolvida segue devolvendo só `{type, id}`. Consumidores que só enviam `conta-interna` e `convite-interno` continuam corretos.

## Validação e verificação

| Documento | Comando e ferramenta | Padrão/ruleset | Resultado |
|---|---|---|---|
| [api-contract.yaml](api-contract.yaml) | `npx --yes @stoplight/spectral-cli@6.15.0 lint tasks/prd-consulta-trilha-auditoria/api-contract.yaml --ruleset .agents/skills/tsg-flow-contract-creator/rulesets/openapi.yaml --fail-severity=error` | OpenAPI 3.1.0; ruleset local | Revisão 1.2.0 válida, 0 erros |
| [internal-api-contract-audit.yaml](internal-api-contract-audit.yaml) | `npx --yes @stoplight/spectral-cli@6.15.0 lint tasks/prd-consulta-trilha-auditoria/internal-api-contract-audit.yaml --ruleset .agents/skills/tsg-flow-contract-creator/rulesets/openapi.yaml --fail-severity=error` | OpenAPI 3.1.0; ruleset local | Revisão 1.2.0 válida, 0 erros |
| [internal-api-contract-identity.yaml](internal-api-contract-identity.yaml) | `npx --yes @stoplight/spectral-cli@6.15.0 lint tasks/prd-consulta-trilha-auditoria/internal-api-contract-identity.yaml --ruleset .agents/skills/tsg-flow-contract-creator/rulesets/openapi.yaml --fail-severity=error` | OpenAPI 3.1.0; ruleset local | Válido, 0 erros e 0 avisos |
| [asyncapi-contract.yaml](asyncapi-contract.yaml) | `npx --yes @asyncapi/cli@6.1.0 validate tasks/prd-consulta-trilha-auditoria/asyncapi-contract.yaml` | AsyncAPI 3.0.0; parser da CLI | Válido, 0 erros e 0 avisos; 1 informação sugere 3.1.0. Mantida 3.0.0 da skill. Avisos `node-config` são do ambiente. |
| Exemplo `ComplementoConfirmado` | Ajv 8 + ajv-formats, schema extraído do YAML com PyYAML 6.0.3 | JSON Schema de `ComplementoConfirmadoPayload` | 1 exemplo válido; 0 erros |

Gates executados nesta revisão: `admin-spa` passou em `npm run lint`, `npm run build` e `npm test` (20 arquivos, 79 testes); `audit` passou em `dotnet build` e nos testes de integração MTP (67 testes); `bff-admin` passou em `dotnet build` e nos testes end-to-end MTP (75 testes). O build do SPA mantém os avisos existentes sobre o script `runtime-env.js` e o tamanho do bundle.

Validade estrutural não comprova autorização, estabilidade da paginação, entrega, idempotência ou imutabilidade. A implementação deve verificar: dois tenants e papel revogado na próxima ação; extremos do período; páginas com inserções concorrentes; ato sem autor/motivo e convite aceito sem motivo; identidade não resolvida; confirmação repetida com mesma chave e com corpo divergente; mensagem repetida e divergente; original inexistente/de outro tenant; falha do outbox, do consumidor e recuperação pela fila de erro; original byte a byte igual antes/depois; ausência de motivo/explicação em telemetria.

## Pendências e handoff

Não há decisão de contrato pendente. A busca POST foi autorizada pelo responsável em 2026-09-27 e validada estruturalmente. A **TechSpec** define persistência do outbox de `bff-admin`, ciclo de vida do snapshot, consulta/atualização do detalhe após `202`, tratamento da indisponibilidade de Identity e cenários de verificação acima. São escolhas de implementação dentro do comportamento acordado; não mudam os schemas sem nova revisão.

Handoff para `tsg-flow-techspec-creator`: usar [prd.md](prd.md), este índice, os três YAML OpenAPI e [asyncapi-contract.yaml](asyncapi-contract.yaml), mapeando operações e evento à implementação sem duplicar schemas.
