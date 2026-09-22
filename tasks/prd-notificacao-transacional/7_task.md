---
status: done
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

- [x] Gate passa (exit 0): `dotnet test src/notification/tests/CodeForCoders.Notification.IntegrationTests/CodeForCoders.Notification.IntegrationTests.csproj -- --filter-class "CodeForCoders.Notification.IntegrationTests.PurgeDeliveryRecordPersonalDataTests" --minimum-expected-tests 1`
- [x] Registro de Entrega criado com data de corte já vencida: após rodar o job de expurgo,
      `destinatario` e o motivo detalhado ficam nulos, e `finalidade`/situação/timestamps agregados
      permanecem
- [x] O contador agregado por `tenant_id + finalidade + situação + dia` está incrementado antes do
      expurgo e permanece com o mesmo valor depois — o expurgo não o altera nem o duplica
- [x] Os gates das Tasks 1.0, 4.0, 5.0 e 6.0 continuam passando sem alteração de comportamento
      observável

## Reabertura pós-full (2026-09-21, ver `prd_review.md`, run.kgdLCoEc)

A revisão full sobre o PRD inteiro encontrou bloqueantes e mutantes sobreviventes atribuídos a esta
task. Corrigir sem alterar o comportamento já provado pelo gate acima:

- **B1 (lint):** `dotnet format` falha na migration `20260921225912_AddDeliveryOutcomeCounters.cs`
  (BOM UTF-8 contra `charset=utf-8`). Rodar `dotnet format` nesse arquivo.
- **B5 (parcial, RN-N08/DP-06):** a Task 1.0 já zera `DeliveryRecord.Link` em `MarkDelivered`/
  `MarkFailed`/`CreateRefused` (corrigido na reabertura dela). Confirmar aqui que
  `DeliveryRecordPurgeWorker` continua consistente com isso (não precisa reexpurgar um campo que já
  nasce nulo) e que nenhum teste desta task depende do valor antigo de `Link` sobrevivendo até o
  expurgo.
- **B7 (mutante sobrevivente — cobertura insuficiente):** remover o ramo `Delivered` de
  `DeliveryRecordPurgeWorker.cs:66` (a condição que seleciona quais desfechos são elegíveis ao
  expurgo) não quebra nenhum teste hoje — `PurgeDeliveryRecordPersonalDataTests` só expurga um
  registro **recusado**. Na prática, nenhum registro "entregue" seria expurgado em produção sem que
  isso apareça em teste. Estender `PurgeDeliveryRecordPersonalDataTests` (ou adicionar teste irmão)
  cobrindo também os desfechos "entregue" e "falhou", garantindo que cada um perde os campos pessoais
  após a janela de retenção.
- **B8 (mutante sobrevivente — cobertura insuficiente):** remover o incremento do contador agregado em
  `DeliverAcceptedNotification.cs:70-73` (transição para "entregue") não quebra nenhum teste hoje —
  nenhum teste lê o contador após uma entrega bem-sucedida. Por inspeção, o mesmo vale para o
  incremento no caminho de falha (`:114-117`). Adicionar teste (em V-01/V-04/V-06, ou no próprio teste
  de expurgo desta task) que afirme o valor do contador `tenant_id+purpose+status+outcome_day` **após**
  uma entrega e **após** uma falha, não só após uma recusa.

Evidência de todos os pontos acima está em `prd_review.md` (B1, B5, B7, B8, mutantes M10/M11). Corrigir
sem regredir os gates das Tasks 1.0, 4.0, 5.0 e 6.0.

## Reabertura pós-full #2 (2026-09-22, ver `prd_review.md`, run.Ul8nMYpY)

- **B1 (parcial, vazamento de estado entre testes):** os testes de expurgo adicionados nesta task para
  os desfechos "entregue" e "falhou" (fechamento de B7 na reabertura anterior) entregam a notificação
  com sucesso antes de expurgar, mas não ligam fila alguma à routing key
  `notificacao.mensagem-entregue.v1` para consumir/confirmar esse evento. A linha de outbox
  correspondente fica pendente com `NO_ROUTE` (publish `mandatory` sem fila vinculada), e o
  `OutboxPublisherWorker` trava atrás dela — o próximo teste que liga fila de entrega acaba lendo o
  evento órfão deste teste em vez do seu próprio (mesmo mecanismo já corrigido na Task 3.0 nesta
  mesma reabertura). Corrigir seguindo o mesmo padrão: declarar e vincular uma fila exclusiva à
  routing key `mensagem-entregue.v1` no host de cada teste de expurgo que gera uma entrega, e
  consumi-la/confirmá-la antes do expurgo rodar. Sem alterar as asserções de negócio já existentes.

Verificar, junto com a Task 3.0, que a suíte completa do projeto de integração não regride mais por
esse mecanismo (evidência: `dotnet test` sem filtro).
