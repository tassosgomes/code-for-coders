---
status: pending
task_kind: vertical
blocked_by: []
gate: "dotnet test src/notification/tests/CodeForCoders.Notification.IntegrationTests/CodeForCoders.Notification.IntegrationTests.csproj -- --filter-class \"CodeForCoders.Notification.IntegrationTests.AcceptAndDeliverAccountConfirmationTests\" --minimum-expected-tests 1"
gate_expect: "1 teste passa"
---

# 1.0 Aceitar e entregar confirmação de conta (caminho feliz)

**Fatia:** V-01 · **Cobre:** RF-01, RF-02, RF-04, RF-05, RF-07; RN-N01–N08, RN-N10, RN-N13, RN-N14;
RN-26–28 (Identidade e Acesso) · **Spec:** `techspec.md#v-01` · **ADR:** —

## Comportamento

Uma mensagem chega no canal `notificacao.envio-solicitado.v1` (contrato:
`asyncapi-contract.yaml#PedidoDeEnvioSolicitadoPayload`) com `finalidade: confirmacao-de-conta`,
`modelo: confirmacao-de-conta` e `dados` completos (`nome`, `link`). O consumidor deserializa a
mensagem e a confirma (`ack`) assim que o pedido é validado — não a mantém presa na fila enquanto
espera o provedor de e-mail. O caso de uso grava um Registro de Entrega na situação "aceito" (chave
única `tenant_id + pedido_id`), verifica consentimento pela porta de consentimento (nesta fatia,
sempre permite para finalidade transacional — RN-N05) e renderiza o Modelo de Mensagem de confirmação
de conta com `nome`, `link` e a validade vigente do parâmetro por finalidade (RN-N13/14) — sem link de
descadastro (RN-N06). Ao entregar com sucesso via `ITransactionalEmailSender`, o Registro de Entrega
transiciona para "entregue" com o timestamp da transição, e no mesmo commit é gravada a linha de
outbox de `notificacao.mensagem-entregue.v1` (payload: `pedidoId`, `tenantId`, `finalidade`,
`destinatario` em claro — exceção declarada de RF-07 — `canal: email`, `entregueEm`), publicada depois
pelo `OutboxPublisherWorker` já existente, sem o corpo da mensagem.

Uma mensagem que não deserializa (payload malformado) é `nack(requeue: false)` e cai na DLQ nativa da
fila — é poison message, não desfecho de negócio.

Nenhum e-mail em claro aparece em log, span ou métrica emitidos por este fluxo: todo ponto de
observabilidade usa o endereço mascarado (RN-N07/G10/G23); o endereço em claro só existe no Registro
de Entrega e no payload do pedido/evento, ambos exceções declaradas.

## Fora do escopo desta task

Recusa por pedido inválido (V-02); comportamento de reentrega/reenvio do mesmo `pedidoId` (V-03);
segundo modelo de mensagem (V-04); retentativa e classificação de erro do provedor (V-05, V-06);
expurgo de dado pessoal (V-07). A porta de consentimento nasce aqui só com a resposta fixa
"permite" para finalidade transacional — descadastro e estado persistido são `CAP-027`.

## Decisões fechadas

- Ack do broker acontece só depois do commit da gravação do Registro de Entrega; a chamada ao
  provedor é desacoplada dessa garantia (Decisão Técnica 1 da TechSpec) — não reabrir para usar
  `nack`/requeue como mecanismo de retry ao provedor.
- Registro de Entrega é ao mesmo tempo inbox de idempotência e registro consultável — não criar
  tabela de inbox separada (Decisão Técnica 2 da TechSpec).
- `dados` do pedido tem forma única (`nome`, `link`); `validade` não trafega no pedido — é parâmetro
  de Notificação por finalidade (RN-N13/14/15; `contracts.md`, item 3 de "Origem e decisões").
- `destinatario` em claro no evento `mensagem-entregue` é exigência textual de RF-07, não vazamento
  (`contracts.md`, item 4).
- Consumidor segue o padrão manual-ack de `HeartbeatConsumerWorker` (`BasicQos`,
  `AsyncDefaultBasicConsumer`, propagação de `traceparent` na `Activity`).

## Modificar / Referenciar

- **modificar:** `src/notification/src/CodeForCoders.Notification.Infra.Messaging/Configuration/RabbitMqOptions.cs` (fila/routing key do pedido de envio e sua DLQ)
- **modificar:** `src/notification/src/CodeForCoders.Notification.Infra.Messaging/RabbitMqTopologyInitializer.cs` (declarar e vincular a nova fila e DLQ)
- **modificar:** `src/notification/src/CodeForCoders.Notification.Infra.Data/Configuration/EmailOptions.cs` (parâmetro de validade para `confirmacao-de-conta`; domínio de envio se distinto do remetente — RN-N13/14)
- **modificar:** `src/notification/src/CodeForCoders.Notification.Infra.Data/NotificationDbContext.cs` (novo `DbSet` do Registro de Entrega)
- **modificar:** `src/notification/src/CodeForCoders.Notification.Infra.Data/Migrations/` (migration gerada para a tabela do Registro de Entrega)
- **modificar:** `src/notification/src/CodeForCoders.Notification.Infra.Data/DependencyInjection.cs` (registrar novos serviços/opções desta fatia)
- **modificar:** `src/notification/src/CodeForCoders.Notification.Infra.Messaging/DependencyInjection.cs` (registrar o novo consumidor)
- **modificar:** `src/notification/src/CodeForCoders.Notification.Application/DependencyInjection.cs` (registrar o caso de uso e a porta de consentimento)
- **ref:** `src/notification/src/CodeForCoders.Notification.Infra.Messaging/HeartbeatConsumerWorker.cs` (padrão de consumidor manual-ack, `nack(requeue:false)` para payload malformado, propagação de `traceparent`)
- **ref:** `src/notification/src/CodeForCoders.Notification.Application/Interfaces/ITransactionalEmailSender.cs` (porta de envio já existente; não mudar assinatura pública)
- **ref:** `src/notification/src/CodeForCoders.Notification.Application/Interfaces/IOutboxMessageWriter.cs` e `IUnitOfWork.cs` (gravação do outbox no mesmo commit)
- **ref:** `context/architecture-baseline.md:253-262` (convenção de mensageria: outbox obrigatório, inbox no consumidor, DLQ por fila)
- **ref:** `domains/identidade-e-acesso/domain.md:119-146` (contrato do lado que publica — RN-26–28)
- **ref:** `tasks/prd-notificacao-transacional/asyncapi-contract.yaml` (schemas `PedidoDeEnvioSolicitadoPayload`, `MensagemEntreguePayload`, `Finalidade`, `DadosDoModelo`)

## Pronto quando

- [ ] Gate passa (exit 0): `dotnet test src/notification/tests/CodeForCoders.Notification.IntegrationTests/CodeForCoders.Notification.IntegrationTests.csproj -- --filter-class "CodeForCoders.Notification.IntegrationTests.AcceptAndDeliverAccountConfirmationTests" --minimum-expected-tests 1`
- [ ] Pedido de confirmação de conta publicado no broker termina com Registro de Entrega "entregue",
      timestamps de cada transição, e requisição chegou ao fake do provedor com o texto do modelo de
      confirmação (sem link de descadastro)
- [ ] Evento `notificacao.mensagem-entregue.v1` publicado sem o corpo da mensagem, com `destinatario`
      em claro e `canal: email`
- [ ] Nenhum e-mail em claro aparece em log, span ou métrica emitidos durante o fluxo
