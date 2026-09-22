---
status: in_progress
task_kind: vertical
blocked_by: ["1.0"]
gate: "dotnet test src/notification/tests/CodeForCoders.Notification.IntegrationTests/CodeForCoders.Notification.IntegrationTests.csproj -- --filter-class \"CodeForCoders.Notification.IntegrationTests.PurgeDeliveryRecordPersonalDataTests\" --minimum-expected-tests 1"
gate_expect: "1 teste passa"
---

# 7.0 Expurgar dado pessoal do Registro de Entrega após a retenção

**Fatia:** V-07 · **Cobre:** DP-08, RN-N16 · **Spec:** `techspec.md#v-07` · **ADR:** —

## Comportamento

Um Registro de Entrega (de qualquer desfecho — entregue, recusado ou falhou, criados pelas Tasks
1.0–6.0) ultrapassa a janela de retenção paramétrica com dado pessoal. Um processo agendado limpa
`destinatario` e o motivo detalhado (de recusa ou falha) desse Registro de Entrega, preservando
`finalidade`, situação final e os timestamps agregados — a linha continua existindo, só os campos
pessoais são nulos.

No mesmo commit em que cada Registro de Entrega original transiciona para seu desfecho final (não no
momento do expurgo), um contador por `tenant_id + finalidade + situação + dia` é incrementado — sem
dado pessoal, e esse contador **nunca** é expurgado. É o contador, não a linha expurgada, que
permanece como "fato agregado" consultável pela operação depois da janela.

Esta task, portanto, tem duas partes: (a) o processo de expurgo em si, agendado sobre Registros de
Entrega com data de corte vencida; (b) retrofitar os pontos de transição de estado já criados nas
Tasks 1.0, 4.0, 5.0 e 6.0 para incrementar o contador agregado no momento da transição — sem alterar
o comportamento observável que essas tasks já provam.

## Fora do escopo desta task

O número de dias da janela de retenção — nasce como valor provisório até a decisão do encarregado de
dados (PRD, QP-01); parametrizado, não hardcoded, mas o valor em si não é decisão desta task. Consulta
operacional por destinatário individual deixa de funcionar após a janela — é o efeito pretendido, não
uma lacuna a compensar.

## Decisões fechadas

- Retenção em duas camadas: expurgo dos campos pessoais da própria linha + contador agregado
  incrementado na transição original, nunca no expurgo (Decisão Técnica 3 da TechSpec) — não
  substituir por anonimização sem contador separado, nem por uma segunda tabela de inbox.
- O expurgo não deve alterar o contador agregado já incrementado, nem incrementá-lo de novo
  (TechSpec §Verificação — cenário crítico não óbvio).
- Prazo de retenção é parâmetro (RN-N16); código não assume um número fixo.

## Modificar / Referenciar

- **modificar:** `src/notification/src/CodeForCoders.Notification.Infra.Data/NotificationDbContext.cs` (novo `DbSet` do contador agregado)
- **modificar:** `src/notification/src/CodeForCoders.Notification.Infra.Data/Migrations/` (migration gerada para o contador agregado e, se necessário, nulabilidade dos campos pessoais do Registro de Entrega)
- **modificar:** `src/notification/src/CodeForCoders.Notification.Infra.Data/DependencyInjection.cs` (registrar o novo processo agendado de expurgo e seu parâmetro de janela)
- **ref:** os pontos de transição de estado do Registro de Entrega criados nas Tasks 1.0, 4.0, 5.0 e
  6.0 — cada um passa a incrementar o contador agregado no mesmo `SaveChangesAsync` da transição,
  sem mudar o desfecho observável já coberto pelos gates dessas tasks
- **ref:** `src/notification/src/CodeForCoders.Notification.Infra.Messaging/OutboxPublisherWorker.cs` (padrão de processo agendado a reaproveitar para o expurgo)

## Pronto quando

- [ ] Gate passa (exit 0): `dotnet test src/notification/tests/CodeForCoders.Notification.IntegrationTests/CodeForCoders.Notification.IntegrationTests.csproj -- --filter-class "CodeForCoders.Notification.IntegrationTests.PurgeDeliveryRecordPersonalDataTests" --minimum-expected-tests 1`
- [ ] Registro de Entrega criado com data de corte já vencida: após rodar o job de expurgo,
      `destinatario` e o motivo detalhado ficam nulos, e `finalidade`/situação/timestamps agregados
      permanecem
- [ ] O contador agregado por `tenant_id + finalidade + situação + dia` está incrementado antes do
      expurgo e permanece com o mesmo valor depois — o expurgo não o altera nem o duplica
- [ ] Os gates das Tasks 1.0, 4.0, 5.0 e 6.0 continuam passando sem alteração de comportamento
      observável
