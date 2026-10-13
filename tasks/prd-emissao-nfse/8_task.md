---
status: pending
task_kind: vertical
blocked_by: ["4.0", "6.0", "7.0"]
gate: 'dotnet test --project src/billing/tests/CodeForCoders.Billing.IntegrationTests/CodeForCoders.Billing.IntegrationTests.csproj -- --filter-class CodeForCoders.Billing.IntegrationTests.FiscalRetentionTests --minimum-expected-tests 6 && dotnet test --project src/identity/tests/CodeForCoders.Identity.IntegrationTests/CodeForCoders.Identity.IntegrationTests.csproj -- --filter-class CodeForCoders.Identity.IntegrationTests.TaxDocumentRetentionTests --minimum-expected-tests 3 && npm --prefix src/student-spa run test -- student-fiscal-retention && npm --prefix src/admin-spa run test -- finance-fiscal-retention'
gate_expect: "Pelo menos 9 testes .NET passam (6 de billing e 3 de identity) e os testes das SPAs com os fragmentos student-fiscal-retention e finance-fiscal-retention passam"
---

# 8.0 Um ano depois, o arquivo e o documento do tomador saem

**Fatia:** V-06 · **Cobre:** QA-01; RN-F02, RN-F09 · **Spec:** `techspec.md#v-06` · **ADR:** 0021

## Comportamento

O ciclo é periódico e idempotente, no formato do ciclo de expiração de acesso de Matrícula. O prazo é o aniversário do instante, em UTC. Rodar de novo não apaga o que já saiu nem abre outra nota.

**Na nota.** No aniversário de um ano de `issued`, saem os bytes do PDF, os bytes do XML e a cópia congelada do tomador. A linha permanece, com `status` `issued` e `downloadsReady` falso. O ciclo não chama o emissor e não abre outro documento. O GET do arquivo passa a responder 404 `FISCAL_DOCUMENT_NOT_FOUND`. A leitura da situação continua 200. Reentregar a compra não abre segunda nota. Excluir a conta não antecipa esse prazo.

**Na conta.** No aniversário de um ano da última gravação válida, Identity apaga o documento e publica `identidade.identificacao-fiscal-informada.v1` sem o valor. Uma gravação válida nova reinicia o prazo. Fiscal relê. Nota já `issued` não é reescrita: o PDF que já saiu não volta, e o que ainda está no prazo não muda por esse fato. Documento ainda não emitido que dependia do tomador volta a `awaiting-holder`. Submissão já aceita pelo emissor não é cancelada.

Nas duas telas de pedido, `issued` com `downloadsReady` falso mostra a frase de um ano aprovada em 2.0, sem download e sem tratar isso como falha. No detalhe do financeiro também não aparece nova tentativa. O aluno e o financeiro não baixam mais o arquivo.

## Fora do escopo desta task

- Emitir de novo o mesmo pedido porque o arquivo saiu (RN-F02 proíbe).
- Guardar pelo prazo legal de cerca de cinco anos. O prazo fechado é um ano (QA-01).
- Apagar a linha do documento.

## Decisões fechadas

- Um ano, no aniversário UTC: da emissão para o arquivo e a cópia do tomador; da última gravação válida para o documento na conta (QA-01, `techspec.md` § Decisões Técnicas).
- A linha `issued` fica, senão o mesmo pedido poderia emitir de novo (RN-F02, RN-F09).
- Exclusão da conta não apaga a nota antes do aniversário (`prd.md`, Restrições Técnicas).

## Modificar / Referenciar

- **modificar:** `src/student-spa/src/features/student-purchase/components/student-order-screen.tsx` (frase de um ano quando `downloadsReady` é falso); `src/admin-spa/src/features/finance-area/components/finance-order-detail-screen.tsx` (a mesma frase, sem download e sem nova tentativa)
- **ref:** `src/commerce/src/CodeForCoders.Commerce.Infra.Data/Entitlement/AccessExpirationCycle.cs` (formato do ciclo periódico e idempotente); `src/identity/src/CodeForCoders.Identity.Domain/Entities/Account.cs`; `tasks/prd-emissao-nfse/asyncapi-contract-identity.yaml`; `techspec.md#v-06`; `docs/design/wireframes-compra.md` (adendo aprovado em 2.0); `.claude/skills/dotnet/SKILL.md`; `.claude/skills/react/SKILL.md`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/billing` | `dotnet format src/billing/CodeForCoders.Billing.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/billing` | `dotnet test --project src/billing/tests/CodeForCoders.Billing.ArchitectureTests/CodeForCoders.Billing.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/billing` | `dotnet ef migrations has-pending-model-changes --project src/billing/src/CodeForCoders.Billing.Infra.Data --startup-project src/billing/src/CodeForCoders.Billing.Api` | exit 0, sem mudança pendente | AGENTS.md |
| `src/identity` | `dotnet format src/identity/CodeForCoders.Identity.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/identity` | `dotnet test --project src/identity/tests/CodeForCoders.Identity.ArchitectureTests/CodeForCoders.Identity.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/identity` | `dotnet ef migrations has-pending-model-changes --project src/identity/src/CodeForCoders.Identity.Infra.Data --startup-project src/identity/src/CodeForCoders.Identity.Api` | exit 0, sem mudança pendente | AGENTS.md |
| `src/student-spa` | `npm --prefix src/student-spa run lint` | exit 0 | `ci-react-ts.yml` Lint |
| `src/student-spa` | `npm --prefix src/student-spa run typecheck` | exit 0 | `ci-react-ts.yml` Typecheck |
| `src/admin-spa` | `npm --prefix src/admin-spa run lint` | exit 0 | `ci-react-ts.yml` Lint |
| `src/admin-spa` | `npm --prefix src/admin-spa run typecheck` | exit 0 | `ci-react-ts.yml` Typecheck |

## Pronto quando

- [ ] Gate focalizado passa (exit 0).
- [ ] No aniversário de `issued`, o arquivo e a cópia do tomador somem, a leitura da situação continua 200 `issued`, e o download responde 404.
- [ ] Reentregar a compra não abre outra nota, e excluir a conta não antecipa o prazo.
- [ ] O documento na conta some um ano após a última gravação válida, o fato não leva o valor, e uma gravação nova reinicia o prazo.
- [ ] Nota já emitida não muda por esse fato; quem ainda esperava o tomador volta a `awaiting-holder`; submissão já aceita não é cancelada.
- [ ] As duas telas de pedido mostram a frase de um ano, sem download e sem nova tentativa.
