---
status: done
task_kind: vertical
blocked_by: ["1.0"]
gate: "dotnet test src/notification/tests/CodeForCoders.Notification.IntegrationTests/CodeForCoders.Notification.IntegrationTests.csproj -- --filter-class \"CodeForCoders.Notification.IntegrationTests.RejectInvalidSendRequestTests\" --minimum-expected-tests 3"
gate_expect: "3 testes passam"
---

# 2.0 Recusar pedido de envio inválido

**Fatia:** V-02 · **Cobre:** RF-01 (segundo a quinto critério) · **Spec:** `techspec.md#v-02`
· **ADR:** —

## Comportamento

Um pedido bem formado (deserializa) mas inválido por regra de negócio chega em um dos três estados:
sem `finalidade`, com `modelo` desconhecido, ou com `dados` faltando `nome` ou `link`. Em qualquer um
dos três casos, o caso de uso recusa **antes** de qualquer renderização ou chamada ao provedor — nenhuma
mensagem parcial é composta. O Registro de Entrega nasce diretamente na situação "recusado", com o
motivo específico do caso (finalidade ausente | modelo desconhecido | dado faltante), e a mensagem AMQP
é confirmada (`ack`) — recusa é desfecho de negócio, não falha de transporte, então não vai para a DLQ
nativa nem é reentregue pelo broker.

Nenhuma chamada é feita a `ITransactionalEmailSender` em nenhum dos três casos, e nenhuma linha de
outbox para `notificacao.mensagem-entregue.v1` é gravada.

## Fora do escopo desta task

Payload que não deserializa (poison message, tratado em V-01 com `nack(requeue:false)`); idempotência
de pedido válido reentregue (V-03); qualquer cenário com finalidade `recuperacao-de-senha` (V-04) —
os três casos negativos aqui usam `confirmacao-de-conta` por serem suficientes para provar a regra,
que independe da finalidade.

## Decisões fechadas

- Recusa é desfecho de negócio, sempre `ack`, nunca `nack` — não reabrir essa distinção com a
  mensagem malformada de V-01.
- Os três motivos de recusa (finalidade ausente, modelo desconhecido, dado faltante) são casos
  distintos e devem ficar diferenciáveis no Registro de Entrega, não um motivo genérico único —
  RF-01 recusa "por motivos diferentes" (`contracts.md`, item 1 de "Origem e decisões").

## Modificar / Referenciar

- **ref:** `techspec.md#v-02` (mapeamento completo dos três casos e evidência esperada)
- **ref:** o caso de uso e a validação de pedido criados na Task 1.0 — esta task estende a mesma
  regra de aceite/recusa com os casos negativos, sem novo arquivo de infraestrutura
- **ref:** `tasks/prd-notificacao-transacional/asyncapi-contract.yaml` (`PedidoDeEnvioSolicitadoPayload`,
  campos obrigatórios `finalidade`, `modelo`, `dados.nome`, `dados.link`)

## Pronto quando

- [x] Gate passa (exit 0): `dotnet test src/notification/tests/CodeForCoders.Notification.IntegrationTests/CodeForCoders.Notification.IntegrationTests.csproj -- --filter-class "CodeForCoders.Notification.IntegrationTests.RejectInvalidSendRequestTests" --minimum-expected-tests 3`
- [x] Pedido sem `finalidade` → Registro de Entrega "recusado" com motivo "finalidade ausente",
      nenhuma chamada ao fake do provedor
- [x] Pedido com `modelo` desconhecido → Registro de Entrega "recusado" com motivo "modelo
      desconhecido", nenhuma chamada ao fake do provedor
- [x] Pedido com `dados` faltando `nome` ou `link` → Registro de Entrega "recusado" com motivo "dado
      faltante", nenhuma chamada ao fake do provedor e nenhum outbox de `mensagem-entregue` gravado
