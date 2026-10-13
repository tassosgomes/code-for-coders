---
status: pending
task_kind: vertical
blocked_by: ["6.0"]
gate: 'dotnet test --project src/billing/tests/CodeForCoders.Billing.IntegrationTests/CodeForCoders.Billing.IntegrationTests.csproj -- --filter-class CodeForCoders.Billing.IntegrationTests.FiscalFailureRetryTests --minimum-expected-tests 8 && dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.IntegrationTests/CodeForCoders.BffAdmin.IntegrationTests.csproj -- --filter-class CodeForCoders.BffAdmin.IntegrationTests.FinanceFiscalRetryProxyTests --minimum-expected-tests 2 && npm --prefix src/admin-spa run test -- finance-fiscal-retry'
gate_expect: "Pelo menos 10 testes .NET passam (8 de billing e 2 do bff-admin) e os testes do admin-spa com o fragmento finance-fiscal-retry passam"
---

# 7.0 A falha fica no mesmo documento e o financeiro pede outra tentativa

**Fatia:** V-05 · **Cobre:** RF-04 (recusa e ausência), RF-06 (motivo e tentativa); RN-F02, RN-F03, RN-F11; US-03, US-05 · **Spec:** `techspec.md#v-05` · **ADR:** —

## Comportamento

A recusa definitiva do emissor leva o mesmo documento a `failed` na hora, sem tentativa automática. A ausência soma no contador da submissão corrente; a terceira seguida leva a `failed`. O pedido continua *Pago*. A concessão não é consultada. O aluno vê só a frase de falha aprovada em 2.0, sem motivo e sem botão.

- E001 (código de serviço recusado) → `failed` na hora, motivo `O código de serviço foi recusado pelo emissor.` Não conta como ausência e não reenvia sozinho.
- Chave recusada → `failed` na hora, motivo `A chave do emissor foi recusada.`
- E004 (cota) → `failed` na hora, motivo `O emissor recusou por cota.`
- Timeout, indisponibilidade, E002 e E003 somam ausência. A terceira leva a `failed` com motivo `O emissor não respondeu após as tentativas.` Antes disso o aluno continua vendo "Nota em emissão".

`retryFinanceFiscalDocument` exige `fiscal.gerir` e só parte de `failed`. Outra situação → 422 `FISCAL_DOCUMENT_NOT_RETRYABLE`. A tentativa incrementa `n`, zera o contador de ausência e não nasce outro `fiscalDocumentId`.

- Tomador e prestador ainda servem → `queued`, com outra chave no emissor.
- Tomador removido → `awaiting-holder`.
- Prestador deixou de ser utilizável → `awaiting-provider`.

Autorização tardia da submissão abandonada responde 200, não substitui a submissão nova e não cria segunda nota. Esse caso incrementa o contador de autorização ignorada. O contador de `failed` sobe na transição para falha. Nenhum motivo leva código municipal.

No detalhe do financeiro, o motivo aparece e o botão de nova tentativa só existe em Falha, no texto do adendo. Não há marcar emitida à mão. Sem `fiscal.gerir`, a ação responde 403.

## Fora do escopo desta task

- Autorização que conclui a submissão corrente → 6.0.
- Cancelar a nota no emissor (DP-05).
- Apagar o arquivo um ano depois → 8.0.

## Decisões fechadas

- Uma submissão em aberto por documento; nova tentativa usa outra chave (C-10, RN-F02).
- Código de serviço recusado não entra em tentativa automática (RN-F11).
- Falha fiscal não desfaz venda nem acesso (RN-F03).
- A nova tentativa só aparece em Falha (`prd.md`, Experiência do Usuário).

## Modificar / Referenciar

- **modificar:** `src/admin-spa/src/features/finance-area/components/finance-order-detail-screen.tsx` (motivo e botão só em Falha, no texto de 2.0)
- **ref:** `tasks/prd-emissao-nfse/api-contract-admin.yaml` (`retryFinanceFiscalDocument`, `FISCAL_DOCUMENT_NOT_RETRYABLE`); `tasks/prd-emissao-nfse/provider-contract-notaas.yaml` (E001–E004 ficam na porta); `techspec.md#v-05`; `docs/design/wireframes-compra.md` (adendo aprovado em 2.0); `.claude/skills/dotnet/SKILL.md`; `.claude/skills/react/SKILL.md`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/billing` | `dotnet format src/billing/CodeForCoders.Billing.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/billing` | `dotnet test --project src/billing/tests/CodeForCoders.Billing.ArchitectureTests/CodeForCoders.Billing.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/bff-admin` | `dotnet format src/bff-admin/CodeForCoders.BffAdmin.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/bff-admin` | `dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.ArchitectureTests/CodeForCoders.BffAdmin.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/admin-spa` | `npm --prefix src/admin-spa run lint` | exit 0 | `ci-react-ts.yml` Lint |
| `src/admin-spa` | `npm --prefix src/admin-spa run typecheck` | exit 0 | `ci-react-ts.yml` Typecheck |
| `src/admin-spa` | `npm --prefix src/admin-spa run test -- finance-orders` | exit 0 (a lista continua sem o botão de tentativa) | `ci-react-ts.yml` Testes |

## Pronto quando

- [ ] Gate focalizado passa (exit 0).
- [ ] E001 não reenvia sozinho; E002 três vezes leva a `failed`; chave recusada e E004 falham na hora.
- [ ] A nova tentativa conserva o `fiscalDocumentId`, incrementa `n` e volta a `queued` quando tomador e prestador ainda servem.
- [ ] Outra situação responde 422 `FISCAL_DOCUMENT_NOT_RETRYABLE`.
- [ ] Autorização da submissão antiga responde 200 e não cria outra nota.
- [ ] O pedido segue *Pago*, o curso abre, e o botão do financeiro só aparece em Falha.
