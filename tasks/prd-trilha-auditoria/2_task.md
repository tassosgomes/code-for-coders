---
status: done
task_kind: vertical
blocked_by: ["1.0"]
gate: "dotnet test --project src/audit/tests/CodeForCoders.Audit.IntegrationTests/CodeForCoders.Audit.IntegrationTests.csproj -- --filter-class CodeForCoders.Audit.IntegrationTests.AdministrativeActRedeliveryTests --minimum-expected-tests 4"
gate_expect: "Pelo menos 4 testes de integração passam (reentrega idêntica, fatoId novo, divergente, corrida)"
---

# 2.0 Não duplicar ato reentregue e alertar divergência

**Fatia:** V-02 · **Cobre:** RF-02, RN-A01, RN-A03, US-03 · **Spec:** `techspec.md#v-02` · **ADR:** —

## Comportamento

A chave do ato é (`origem`, `fatoId`), garantida pela unicidade no banco. A violação da unicidade no
commit é o sinal de reentrega — inclusive na corrida de duas entregas simultâneas; não há "consulta
antes de gravar" como fonte de verdade.

- **Reentrega idêntica** (mesma impressão digital): `ack`, nenhum registro novo, contador
  `audit.acts.redelivered{outcome=identical}`. Nenhum log de aviso.
- **Mesmo conteúdo, `fatoId` diferente:** dois registros — igualdade de conteúdo não é duplicidade.
- **Reentrega divergente** (mesma chave, impressão diferente): `ack`, nenhum registro novo, registro
  original byte a byte igual, `audit.acts.redelivered{outcome=divergent}` e log de aviso com
  `origem`, `fatoId` e as duas impressões — sem motivo nem complemento.
- **Corrida:** duas entregas simultâneas da mesma mensagem → um registro, nenhuma na DLQ.

A impressão digital é a da TechSpec (V-02): SHA-256 da forma canônica dos campos do contrato que entram
no registro; campos descartados não entram. Uma mudança nessa canonicalização é mudança de
comportamento.

## Fora do escopo desta task

Reentrega de ato não conforme sem repetir o alerta de não conformidade é provada em 3.0. Reprocessamento
a partir da DLQ é provado em 4.0.

## Decisões fechadas

- Idempotência por unicidade + impressão digital, sem inbox: techspec.md, Decisões Técnicas 2.
- Chave e comportamento de reentrega: [asyncapi-contract.yaml](asyncapi-contract.yaml), compromisso (1) de `receberAtoPraticado`.

## Modificar / Referenciar

- **modificar:** `src/audit/src/CodeForCoders.Audit.Application/Interfaces/IAuditRecordWriter.cs` (ler impressão por chave)
- **modificar:** `src/audit/src/CodeForCoders.Audit.Infra.Data/AuditRecordWriter.cs` e `src/audit/src/CodeForCoders.Audit.Infra.Data/AuditUnitOfWork.cs` (tradução da violação de unicidade)
- **modificar:** `src/audit/src/CodeForCoders.Audit.Application/Common/AuditTelemetry.cs` (`audit.acts.redelivered`)
- **modificar:** `src/audit/tests/CodeForCoders.Audit.IntegrationTests/*` (cenários de reentrega e corrida)
- **ref:** `techspec.md#v-02`, `contracts.md` cenários 5 e 6, skill `dotnet`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/audit` | `dotnet format src/audit/CodeForCoders.Audit.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` passo Lint |
| `src/audit` | `dotnet build src/audit/CodeForCoders.Audit.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` (restore/build implícito no test) |
| `src/audit` | `dotnet test --project src/audit/tests/CodeForCoders.Audit.ArchitectureTests/CodeForCoders.Audit.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` passo Testes (suíte do componente) |
| `src/audit` | `dotnet test --project src/audit/tests/CodeForCoders.Audit.UnitTests/CodeForCoders.Audit.UnitTests.csproj` | exit 0 | `ci-dotnet.yml` passo Testes |

Cobertura agregada ≥ 70% e `dotnet publish` ficam para a validação full.

## Pronto quando

- [ ] Gate passa (exit 0) com pelo menos 4 testes selecionados.
- [ ] Mesma mensagem publicada duas vezes → 1 registro e 1 incremento `identical`.
- [ ] Conteúdo alterado com a mesma chave → registro original inalterado, nenhum novo, 1 incremento `divergent` e 1 log de aviso sem motivo.
- [ ] Duas entregas simultâneas → 1 registro e DLQ vazia.
