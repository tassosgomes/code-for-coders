# Plano de Implementação — Trilha de auditoria (CAP-030, fatia mínima)

> **TechSpec de origem:** [techspec.md](techspec.md), aprovada em 2026-09-24
> **Escopo:** Backend (`src/audit`)
> **ADRs pertinentes:** [0001](../../docs/adr/0001-monorepo-de-codigo.md), [0002](../../docs/adr/0002-plataforma-de-runtime-coolify.md)
> **Status do plano:** Aprovado em 2026-09-24

## Visão Geral

Cinco fatias transformam o consumidor técnico da Fase 0 no receptor de `auditoria.ato-praticado.v1`
([contrato](asyncapi-contract.yaml) 1.0.1). Ao final, todo ato publicado vira um Registro de Auditoria
conforme ou não conforme, reentrega não duplica, o que é ilegível fica retido intacto na DLQ, e a
imutabilidade é demonstrada com a credencial que o serviço usa em execução. O produtor real
(Identidade, `CAP-002`) não faz parte deste plano: os testes publicam os atos no formato do contrato.

## Fases

### Fase 1 — Registrar o ato conforme

1.0 troca o contrato genérico pelo envelope do ato, reconstrói `audit_records` e prova os quatro tipos
de Identidade de ponta a ponta. Checkpoint: quatro registros conformes a partir dos exemplos do contrato.

### Fase 2 — Robustez da entrada e prova de imutabilidade

2.0 (reentrega), 3.0 (não conforme), 4.0 (ilegível e falha transitória) e 5.0 (credencial de execução)
partem de 1.0 e não dependem entre si. Checkpoints: nenhuma duplicidade, nenhum ato perdido, nenhuma
mutação possível.

## Mapa de Entrega

| Fatia | Task | Comportamento observável | Gate | Bloqueado por |
|---|---|---|---|---|
| V-01 | [1.0](1_task.md) | Ato conforme dos quatro tipos vira Registro de Auditoria | Integração `AdministrativeActRecordingTests` | Nenhum |
| V-02 | [2.0](2_task.md) | Reentrega idêntica não duplica; divergente é alertada | Integração `AdministrativeActRedeliveryTests` | 1.0 |
| V-03 | [3.0](3_task.md) | Ato incompleto ou de tipo desconhecido vira registro não conforme, alertado uma vez | Unitário `AdministrativeActConformityTests` + integração `NonConformingActRecordingTests` | 1.0 |
| V-04 | [4.0](4_task.md) | Ilegível e falha transitória terminam na DLQ intactas; reprocessar não duplica | Integração `AuditDeadLetterTests` | 1.0 |
| V-05 | [5.0](5_task.md) | Serviço roda com credencial só `SELECT, INSERT`; mutação falha por privilégio e por trigger | Integração `AuditImmutabilityTests` + unitário `AuditAppendOnlyGuardrailTests` | 1.0 |

### Habilitadores

Nenhum. A reconstrução da tabela, a troca de fila e a remoção de `AuditEventV1` entram em 1.0, que
é a primeira fatia que as consome; a credencial de execução entra em 5.0, que é a que a prova.

## Tasks

- [x] 1.0 Registrar ato administrativo conforme
- [x] 2.0 Não duplicar ato reentregue e alertar divergência
- [ ] 3.0 Registrar e alertar ato não conforme
- [ ] 4.0 Reter mensagem ilegível e falha transitória na DLQ sem perda
- [ ] 5.0 Demonstrar imutabilidade com a credencial de execução

## Verificação herdada

| Componente | Fonte | Checks e limites | Estado da base | Resolução planejada |
|---|---|---|---|---|
| `src/audit` | `.github/workflows/audit.yml` → `tassosgomes/template-pipeline/.github/workflows/ci-dotnet.yml@v1` | `dotnet restore`; `dotnet format --verify-no-changes --no-restore`; `dotnet test --no-restore --configuration Debug --coverage --coverage-output-format cobertura` (MTP, `global.json`); cobertura agregada ≥ 70%; `dotnet publish -c Release`; build do container `src/audit/Dockerfile` | Medido em 2026-09-24: format exit 0; UnitTests 4/4 e ArchitectureTests 8/8 passam. IntegrationTests, EndToEndTests e cobertura **não medidos** (dependem de Docker, disponível no ambiente) | Nenhuma falha herdada conhecida; a full mede cobertura e publish |

Os testes existentes de `AuditEventV1` (`RecordConsumedAuditEventTests`, `AuditEventConsumerIntegrationTests`)
deixam de fazer sentido em 1.0 e são substituídos nela; a cobertura ≥ 70% continua valendo para o serviço.

## Cobertura

| Requisito | Task(s) |
|---|---|
| RF-01 | 1.0 |
| RF-02 | 2.0 |
| RF-03 | 3.0 |
| RF-04 | 4.0 |
| RF-05 | 5.0 |
| US-01 | 1.0 |
| US-02 | 5.0 |
| US-03 | 3.0, 2.0 |
| US-04 | 1.0 |
| RN-A01 | 2.0, 5.0 |
| RN-A02 | 1.0, 4.0, 5.0 |
| RN-A03 | 2.0, 4.0 |
| RN-A04 | 1.0 |
| RN-A05 | 1.0, 3.0 |
| RN-A06 | 3.0 |
| RN-A08 | 1.0 |
| RN-A10 | 5.0 |
| RN-A11 | 1.0 |
| RN-A12 | 1.0 |
| RN-A13 | 1.0, 4.0 |
| RN-A14 | 1.0 |
| RN-19 (Identidade) | 1.0, 3.0 |
| RN-20 (Identidade) | — respeitada na origem: autor = alvo não é checado pela Auditoria (RN-A10, RN-A11) |
| RN-25 (Identidade) | 1.0 (sem reconstrução de atos anteriores, RN-A12) |
| DP-01 a DP-05 | 1.0, 3.0, 4.0 |

Observabilidade (contadores e logs sem dado pessoal) entra em cada fatia que emite o sinal; não há
task separada. Migração de dados: não há dado real (sistema fora de produção), decisão 1 da TechSpec.

> Verificado por `python3 .claude/skills/tsg-flow-task-creator/scripts/validate_plan.py tasks/prd-trilha-auditoria/` antes do handoff.
