---
status: done
task_kind: vertical
blocked_by: ["1.0"]
gate: "dotnet test src/notification/tests/CodeForCoders.Notification.IntegrationTests/CodeForCoders.Notification.IntegrationTests.csproj -- --filter-class \"CodeForCoders.Notification.IntegrationTests.SendRequestIdempotencyTests\" --minimum-expected-tests 2"
gate_expect: "2 testes passam"
---

# 3.0 Garantir idempotência entre reentrega e reenvio

**Fatia:** V-03 · **Cobre:** RF-01 (sexto critério), RF-02 (terceiro critério); RN-N09
· **Spec:** `techspec.md#v-03` · **ADR:** —

## Comportamento

Dois cenários distintos, que precisam terminar em resultados opostos:

1. **Redelivery do broker:** o mesmo `pedidoId` chega uma segunda vez pela fila (reentrega física do
   RabbitMQ). A segunda gravação colide com a chave única `tenant_id + pedido_id` do Registro de
   Entrega; o caso de uso trata a colisão como "já processado", confirma (`ack`) a mensagem e **não**
   chama `ITransactionalEmailSender` de novo. Resultado observável: um único Registro de Entrega, um
   único e-mail entregue, mesmo com duas entregas da mensagem AMQP.
2. **Reenvio deliberado:** um `pedidoId` **novo** chega com a mesma `finalidade` e o mesmo
   destinatário do pedido anterior (ex.: aluno pede reenvio de confirmação). Por ter `pedidoId`
   diferente, é tratado como um pedido novo por inteiro, com seu próprio Registro de Entrega e seu
   próprio ciclo de entrega. Resultado observável: dois Registros de Entrega distintos, dois e-mails
   entregues.

A distinção entre os dois cenários é inteiramente pelo valor de `pedidoId` — nenhuma lógica adicional
de "mesma finalidade + mesmo destinatário" deve suprimir o reenvio.

## Fora do escopo desta task

Recusa de pedido inválido (V-02, já resolvida antes desta verificação); qualquer variação de
finalidade além da usada no cenário base (V-04 prova que o mecanismo se repete para o segundo
modelo, não esta task).

## Decisões fechadas

- A chave de deduplicação é exclusivamente `tenant_id + pedido_id` — nunca `finalidade +
  destinatario`, que descreve o segundo cenário (reenvio válido), não uma duplicata.
- Concorrência entre duas instâncias do consumidor processando o mesmo `pedidoId` em paralelo deve
  resultar em no-op para a segunda, não em exceção não tratada (TechSpec, §Verificação — "cenário
  crítico não óbvio").

## Modificar / Referenciar

- **ref:** `techspec.md#v-03` (mapeamento completo dos dois cenários)
- **ref:** a restrição de unicidade `tenant_id + pedido_id` e o tratamento de violação como "já
  processado" introduzidos na Task 1.0 — esta task adiciona o teste que prova o comportamento sob
  redelivery real, sem novo componente de infraestrutura
- **ref:** `techspec.md` (Riscos e Preocupações — corrida entre duas reentregas simultâneas do
  mesmo `pedidoId`)

## Pronto quando

- [x] Gate passa (exit 0): `dotnet test src/notification/tests/CodeForCoders.Notification.IntegrationTests/CodeForCoders.Notification.IntegrationTests.csproj -- --filter-class "CodeForCoders.Notification.IntegrationTests.SendRequestIdempotencyTests" --minimum-expected-tests 2`
- [x] Publicar o mesmo `pedidoId` duas vezes em sequência resulta em uma única chamada ao fake do
      provedor e um único Registro de Entrega
- [x] Publicar dois `pedidoId`s distintos com a mesma finalidade e destinatário resulta em dois
      Registros de Entrega e duas chamadas ao fake do provedor

## Reabertura pós-full (2026-09-22, ver `prd_review.md`, run.Ul8nMYpY)

- **B1 (parcial, vazamento de estado entre testes):** `SendRequestIdempotencyTests` entrega uma
  notificação com sucesso (o cenário de reenvio, `pedidoId` novo, gera um segundo e-mail/registro)
  mas não liga fila alguma à routing key `notificacao.mensagem-entregue.v1` para consumir/confirmar
  esse evento. A linha de outbox correspondente fica pendente com `NO_ROUTE` (publish `mandatory`
  sem fila vinculada), e o `OutboxPublisherWorker` (que sempre processa o menor id pendente e para o
  lote na primeira falha) trava atrás dela — o teste seguinte que liga fila de entrega acaba lendo o
  evento órfão deste teste em vez do seu próprio. Corrigir seguindo o mesmo padrão já usado pelos
  testes de entrega (`AcceptAndDeliverAccountConfirmationTests`,
  `DeliverPasswordRecoveryEmailTests`): declarar e vincular uma fila exclusiva (nome único por teste)
  à routing key `mensagem-entregue.v1` no host de teste e consumi-la (ou pelo menos confirmá-la),
  para que o outbox não fique pendente. Sem alterar as asserções de negócio já existentes.

Evidência completa em `prd_review.md` (B1). Verificar após a correção, isolado e em conjunto com
7.0, que a suíte completa do projeto de integração não regride (mecanismo compartilhado).
