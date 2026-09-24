---
status: pending
task_kind: vertical
blocked_by: []
gate: "dotnet test --project src/audit/tests/CodeForCoders.Audit.IntegrationTests/CodeForCoders.Audit.IntegrationTests.csproj -- --filter-class CodeForCoders.Audit.IntegrationTests.AdministrativeActRecordingTests --minimum-expected-tests 6"
gate_expect: "Pelo menos 6 testes de integração passam (quatro tipos, tenants distintos, campo fora do contrato descartado)"
---

# 1.0 Registrar ato administrativo conforme

**Fatia:** V-01 · **Cobre:** RF-01, US-01, US-04, RN-A02/A04/A05/A08/A11/A12/A13/A14, RN-19 · **Spec:** `techspec.md#v-01` · **ADR:** [0001](../../docs/adr/0001-monorepo-de-codigo.md)

## Comportamento

Uma mensagem publicada no exchange de entrada do `audit` (`audit.events`) com routing key
`auditoria.ato-praticado.v1`, no formato do [contrato](asyncapi-contract.yaml) 1.0.1, completa, vira
um Registro de Auditoria **conforme** e só então é confirmada ao broker (`ack` depois do commit).

- Os quatro tipos aceitos — `convite-interno-emitido`, `convite-interno-aceito`, `papel-concedido`,
  `papel-revogado` — geram, cada um, uma linha com: tenant, origem, `fatoId`, tipo, autor
  (`tipo`, `id`), alvo (`tipo`, `id`), complemento (`papel`), motivo, `praticado_em` vindo da
  mensagem e `recebido_em` do relógio do serviço (via `TimeProvider`), conformidade `conforming`,
  razões vazias e a impressão digital do conteúdo (calculada aqui; usada na 2.0).
- `convite-interno-aceito` sem motivo é conforme; os demais três tipos exigem motivo (a política de
  tipos aceitos e de motivo por tipo é única, no Domain).
- Autor e alvo são guardados só como referência. Campo fora do contrato (o teste manda `email` e
  `nome`) é descartado: não aparece em coluna, log nem span.
- Dois atos de tenants diferentes geram registros cada um no seu tenant.
- Emite `audit.acts.recorded` com `origin`, `type`, `conformity`; log e span carregam só
  `fatoId`, `origem`, `tipo`, `tenantId` — nunca `motivo` nem `complemento`.
- Nenhum ato anterior é reconstruído; o `audit` não publica nada e não expõe consulta.

A tabela `audit_access.audit_records` é **reconstruída** por uma migration EF nova (gerada por
`dotnet ef migrations add`; a migration aplicada não é editada): a migration remove a tabela genérica
da Fase 0 com seu trigger e função e cria a do Registro com unicidade em (`origem`, `fato_id`), índice
(`tenant_id`, `praticado_em`) e trigger `BEFORE UPDATE OR DELETE` recriado. `AuditEventV1`, o
binding `audit.audit.event.v1` e os testes que dependiam dele são substituídos; a fila nova é quorum,
com DLX e `x-delivery-limit` como a atual.

## Fora do escopo desta task

Reentrega e divergência (2.0); faltas e tipo desconhecido (3.0); mensagem ilegível e falha de banco
(4.0) — nesta task, basta que a mensagem legível e completa seja registrada; credencial de execução e
prova de imutabilidade (5.0). A conexão continua com a credencial atual do serviço.

## Decisões fechadas

- Envelope, campos e obrigatoriedade: [asyncapi-contract.yaml](asyncapi-contract.yaml) 1.0.1 e
  [contracts.md](contracts.md). Produtor publica no exchange do consumidor (`audit.events`).
- Reconstruir `audit_records` em vez de tabela nova: techspec.md, Decisões Técnicas 1 (sistema fora de
  produção). Campos fora do contrato são descartados: Decisão 4.
- Convenções de estrutura, EF, migration, mensageria e testes: skill `dotnet`.

## Modificar / Referenciar

- **modificar:** `src/audit/src/CodeForCoders.Audit.Infra.Messaging/AuditEventConsumerWorker.cs` (recebe `AtoPraticado`; ack após commit)
- **modificar:** `src/audit/src/CodeForCoders.Audit.Infra.Messaging/AuditTopologyInitializer.cs`, `src/audit/src/CodeForCoders.Audit.Infra.Messaging/Configuration/RabbitMqOptions.cs` e `src/audit/src/CodeForCoders.Audit.Api/appsettings.json` (fila e routing key novas; remove binding legado)
- **modificar:** `src/audit/src/CodeForCoders.Audit.Infra.Messaging/AuditReceiptStore.cs` (ajustar ao novo desfecho ou remover)
- **modificar:** `src/audit/src/CodeForCoders.Audit.Contracts/AuditEventV1.cs` (removido; DTO de `AtoPraticado` no projeto de contratos)
- **modificar:** `src/audit/src/CodeForCoders.Audit.Domain/Entities/AuditRecord.cs`, `src/audit/src/CodeForCoders.Audit.Infra.Data/Configuration/AuditRecordConfiguration.cs`, `src/audit/src/CodeForCoders.Audit.Infra.Data/AuditDbContext.cs` (Registro de Auditoria; guarda de mutação)
- **modificar:** `src/audit/src/CodeForCoders.Audit.Application/Interfaces/IAuditEventRecorder.cs`, `src/audit/src/CodeForCoders.Audit.Application/Interfaces/IAuditRecordWriter.cs`, `src/audit/src/CodeForCoders.Audit.Application/UseCases/Audit/RecordConsumedAuditEvent/*` (caso de uso de registro de ato)
- **modificar:** `src/audit/src/CodeForCoders.Audit.Application/Common/AuditTelemetry.cs` (`audit.acts.recorded`)
- **modificar:** `src/audit/src/CodeForCoders.Audit.Infra.Data/DependencyInjection.cs` (desligar `EnableSensitiveDataLogging`, risco da TechSpec)
- **modificar:** `src/audit/tests/CodeForCoders.Audit.IntegrationTests/*`, `src/audit/tests/CodeForCoders.Audit.UnitTests/RecordConsumedAuditEventTests.cs`, `src/audit/tests/CodeForCoders.Audit.ArchitectureTests/*` (migrar para o contrato novo)
- **ref:** `src/audit/src/CodeForCoders.Audit.Infra.Data/Migrations/20260920232840_InitialAudit.cs` (trigger a recriar), `src/notification/src/CodeForCoders.Notification.Infra.Messaging/NotificationSendRequestConsumerWorker.cs` (padrão de receptor), `domains/auditoria-e-conformidade/domain.md`, `techspec.md#v-01`, skill `dotnet`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/audit` | `dotnet format src/audit/CodeForCoders.Audit.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` passo Lint |
| `src/audit` | `dotnet build src/audit/CodeForCoders.Audit.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` (restore/build implícito no test) |
| `src/audit` | `dotnet test --project src/audit/tests/CodeForCoders.Audit.ArchitectureTests/CodeForCoders.Audit.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` passo Testes (suíte do componente) |
| `src/audit` | `dotnet test --project src/audit/tests/CodeForCoders.Audit.UnitTests/CodeForCoders.Audit.UnitTests.csproj` | exit 0 | `ci-dotnet.yml` passo Testes |

Cobertura agregada ≥ 70% e `dotnet publish` ficam para a validação full.

## Pronto quando

- [ ] Gate passa (exit 0) com pelo menos 6 testes selecionados.
- [ ] Os quatro exemplos de tipo publicados geram quatro registros conformes com todos os campos, `praticado_em` preservado e `recebido_em` do relógio controlado.
- [ ] O ato com `email`/`nome` fora do contrato não deixa esses valores em nenhuma coluna nem no log capturado; o motivo não aparece em log.
- [ ] Uma base nova aplica todas as migrations e termina com a tabela reconstruída, unicidade e trigger ativos.
