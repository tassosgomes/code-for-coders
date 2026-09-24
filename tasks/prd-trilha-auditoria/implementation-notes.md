# Notas de implementação — decisões e divergências conhecidas

Registro do orquestrador a partir das revisões focused. Serve de contexto para a revisão full:
cada item traz a evidência para o validator **conferir**. Não é dispensa de verificação.

## Desvios aceitos da TechSpec

### D-01 — Requeue de falha transitória por `basic.reject`, não `basic.nack` (task 4.0)

- **TechSpec (§ V-04, falha transitória):** `nack(requeue: true)`.
- **Implementado:** `BasicRejectAsync(requeue: true)` em `AuditEventConsumerWorker.TryNackAsync`.
- **Motivo:** no RabbitMQ 4.3, o requeue por `basic.nack` não incrementa a contagem de entregas da
  fila quorum. Com ele, a mensagem nunca atinge o `x-delivery-limit` e não vai à DLQ. `basic.reject`
  conta e produz o comportamento que a spec exige: ir à DLQ após 5 entregas.
- **Evidência:** comentário no próprio método, e o teste de falha de banco em `AuditDeadLetterTests`, que
  leva a mensagem à DLQ e depois a reprocessa sem duplicar. Revisão: `4.0_task_review.md` (run.4SFR2uPp), rec. 1.
- **Status:** aceito. A TechSpec ainda diz `nack` e deve ser atualizada pelo dono da spec. Até lá, esta
  nota é a referência.

## Divergências conhecidas, ainda abertas (podem ser achados legítimos na full)

### A-01 — Classificação de `fatoId`/`tenantId` presentes mas não uuid (task 4.0)

- **Achado da focused** (`4.0_task_review.md`, rec. 2): um valor não uuid lançaria `JsonException` e
  seria contado como `reason=body`.
- **Leitura do orquestrador:** o achado provavelmente está incorreto. `AtoPraticadoJsonConverter.ReadGuid`
  usa `Guid.TryParse` e devolve `Guid.Empty` para valor inválido ou de tipo errado, sem exceção.
  `TryDeserializeAct` então classifica o caso como `fatoId`/`tenantId`, o que atende a TechSpec §
  classificação ("falta/é inválido `fatoId` (uuid)").
- **Lacuna real:** nenhum teste cobre uuid malformado.
- **Resolução decidida pelo usuário em 2026-09-24:** reabrir a 4.0 depois do checkpoint da 5.0 e
  adicionar testes de `fatoId`/`tenantId` inválidos (string e tipo errado), com DLQ intacta e o `reason`
  específico, antes da revisão full.
- **Resolvido (rodada 2):** o fix run.RONTTrOG adicionou 4 testes em `AuditDeadLetterTests`, sem alterar
  código de produção. Os testes confirmam `reason=fatoId`/`tenantId`. A revalidação run.Ve5MoN1j aprovou e
  **retirou** a rec. 2 da focused por estar incorreta.

### A-02 — Ato não conforme não incrementa `audit.acts.recorded` (task 3.0)

- **Implementado:** um ato não conforme incrementa só `audit.acts.nonconforming`. O atributo
  `conformity` de `audit.acts.recorded` fica sempre `conforming`. O total de atos gravados passa a
  ser a soma dos dois contadores.
- **Spec:** não exige nenhuma das duas formas, segundo a revisão. Cabe à full confirmar contra a
  TechSpec e a QT-02.
- **Revisão:** `3.0_task_review.md` (run.Hexj6mzl), rec. 1.

## Ajustes pré-full decididos pelo usuário (2026-09-24)

### P-01 — Teste de `CONNECT` que não discrimina a migration (task 5.0, R2)

- **Achado:** `AuditIntegrationFixture` roda `REVOKE CONNECT … FROM PUBLIC` antes de migrar. Com isso,
  `ProducerServiceRolesCannotConnectToTheAuditDatabase` continuaria verde mesmo se a migration perdesse o
  revoke.
- **Resolução:** reabrir a 5.0. O fixture passa a só criar os papéis, e o teste prova o revoke pela
  migration.

### P-02 — Pré-condição de deploy da migration `SecureAuditRuntimePermissions` (task 5.0, R1)

- **Achado:** a migration exige que o login `code_for_coders_audit_runtime` e o papel
  `code_for_coders_audit_writer` já existam, e fixa o nome do banco. Essa pré-condição não está documentada.
- **Resolução:** na mesma reabertura, documentar a pré-condição no guardrail (relacionar com o QT-01) e
  usar `current_database()` no lugar do nome fixo.
