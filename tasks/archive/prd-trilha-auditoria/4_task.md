---
status: done
task_kind: vertical
blocked_by: ["1.0"]
gate: "dotnet test --project src/audit/tests/CodeForCoders.Audit.IntegrationTests/CodeForCoders.Audit.IntegrationTests.csproj -- --filter-class CodeForCoders.Audit.IntegrationTests.AuditDeadLetterTests --minimum-expected-tests 5"
gate_expect: "Pelo menos 5 testes de integração passam (corpo inválido, sem fatoId, sem origem, sem tenantId, falha de banco com reprocessamento)"
---

# 4.0 Reter mensagem ilegível e falha transitória na DLQ sem perda

**Fatia:** V-04 · **Cobre:** RF-04, RN-A02, RN-A03, RN-A13, DP-05 · **Spec:** `techspec.md#v-04` · **ADR:** —

## Comportamento

- **Ilegível** — corpo que não é objeto JSON, ou sem `fatoId` uuid, sem `origem` no padrão do contrato,
  ou sem `tenantId` uuid: nenhum registro; `nack(requeue: false)`; a mensagem chega à DLQ com o corpo
  **byte a byte igual** ao publicado; contador `audit.messages.illegible{reason=body|fatoId|origem|tenantId}`
  e log de erro **sem o corpo**.
- **Falha transitória** — banco indisponível, timeout ou qualquer exceção inesperada durante uma
  mensagem legível: `nack(requeue: true)`; a fila quorum conta as entregas e, no `x-delivery-limit`
  (5), a mensagem vai à DLQ. **Nenhuma exceção deixa a mensagem sem `ack` ou `nack`** (hoje
  `AuditEventConsumerWorker.cs:72-98` só trata `JsonException` e `AlreadyClosedException`).
- **Reprocessamento** — mover a mensagem da DLQ de volta para a fila (operação manual; nada consome a
  DLQ automaticamente) gera o registro uma vez; mover de novo não duplica (idempotência de 1.0).

Cenário de falha transitória no teste: parar o PostgreSQL do fixture com uma mensagem legível em voo,
aguardar a mensagem na DLQ, religar, reprocessar duas vezes, verificar um registro.

## Fora do escopo desta task

Ato legível incompleto (3.0). Regra de alerta sobre "DLQ não vazia" e sobre os contadores é da
plataforma (QT-02 da TechSpec).

## Decisões fechadas

- Ilegível não vira registro e fica retido intacto: DP-05; ausência de tenant é ilegível: OD24,
  contrato 1.0.1.
- Limite de entregas mantido em 5 (`RabbitMqOptions.DeliveryLimit`); convenção de DLQ por fila: baseline G06.

## Modificar / Referenciar

- **modificar:** `src/audit/src/CodeForCoders.Audit.Infra.Messaging/AuditEventConsumerWorker.cs` (classificação, `nack` por desfecho, captura de toda exceção)
- **modificar:** `src/audit/src/CodeForCoders.Audit.Application/Common/AuditTelemetry.cs` (`audit.messages.illegible`)
- **modificar:** `src/audit/tests/CodeForCoders.Audit.IntegrationTests/*` (leitura da DLQ, controle do contêiner PostgreSQL)
- **ref:** `techspec.md#v-04`, `contracts.md` cenários 7 e 8, `src/audit/src/CodeForCoders.Audit.Infra.Messaging/AuditTopologyInitializer.cs` (DLX/DLQ), skill `dotnet`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/audit` | `dotnet format src/audit/CodeForCoders.Audit.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` passo Lint |
| `src/audit` | `dotnet build src/audit/CodeForCoders.Audit.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` (restore/build implícito no test) |
| `src/audit` | `dotnet test --project src/audit/tests/CodeForCoders.Audit.ArchitectureTests/CodeForCoders.Audit.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` passo Testes (suíte do componente) |
| `src/audit` | `dotnet test --project src/audit/tests/CodeForCoders.Audit.UnitTests/CodeForCoders.Audit.UnitTests.csproj` | exit 0 | `ci-dotnet.yml` passo Testes |

Cobertura agregada ≥ 70% e `dotnet publish` ficam para a validação full.

## Pronto quando

- [ ] Gate passa (exit 0) com pelo menos 5 testes selecionados.
- [ ] Cada tipo de ilegibilidade termina na DLQ com corpo idêntico ao publicado e nenhum registro.
- [ ] Com o banco fora, a mensagem legível termina na DLQ após o limite; reprocessada duas vezes, gera um único registro.
- [ ] Nenhum log contém o corpo da mensagem.
