---
status: in_progress
task_kind: vertical
blocked_by: ["1.0"]
gate: "dotnet test src/notification/tests/CodeForCoders.Notification.IntegrationTests/CodeForCoders.Notification.IntegrationTests.csproj -- --filter-class \"CodeForCoders.Notification.IntegrationTests.RetryProviderTransientFailureTests\" --minimum-expected-tests 2"
gate_expect: "2 testes passam"
---

# 5.0 Reentregar ao provedor em falha transitória até esgotar tentativas

**Fatia:** V-05 · **Cobre:** RF-06 (primeiro, terceiro e quarto critério), RF-07 (falha); RN-N09,
RN-N11, RN-N12 · **Spec:** `techspec.md#v-05` · **ADR:** —

## Comportamento

O provedor de e-mail responde com erro classificado como **transitório** (5xx, 429, timeout) em
chamadas sucessivas para um pedido já aceito (Registro de Entrega "aceito", da Task 1.0). Cada falha
transitória incrementa a contagem de tentativas do Registro de Entrega e agenda a próxima com
espaçamento crescente — sem reenfileirar a mensagem no RabbitMQ: o pedido já foi confirmado (`ack`) e
fica retido no Postgres, não na fila (RN-N11 — "aceito e retido", indisponibilidade de terceiro não
vira erro no cadastro). Esse processo de reentrega roda como um serviço próprio, no mesmo padrão de
*poll* de `OutboxPublisherWorker` (seleção do próximo pendente, backoff, limite parametrizado), mas
operando sobre o Registro de Entrega — não sobre a fila AMQP.

Enquanto o provedor está indisponível, **novos** pedidos continuam sendo aceitos normalmente (a
aceitação, V-01/V-02, não depende da disponibilidade de entrega).

Ao esgotar o limite de tentativas: a situação do Registro de Entrega vira "falhou" com
`esgotouTentativas: true`; no mesmo commit é gravado o outbox de `notificacao.entrega-falhou.v1` com o
motivo; e um sinal de observabilidade próprio de "pedido em tratamento manual" é emitido exatamente
uma vez por Registro de Entrega — não uma vez por tentativa (RN-N12; esta fatia não usa o DLQ nativo do
broker para este caso, então o sinal não é automático, TechSpec §Decisões Técnicas item 1).

A classificação de erro do provedor (transitório vs. definitivo) mora no adaptador
`HttpTransactionalEmailSender` e cruza para a Application já traduzida — nunca o código de status HTTP
do provedor (camada anticorrupção).

## Fora do escopo desta task

Falha definitiva reconhecida de imediato, sem qualquer retry (V-06, que reaproveita a classificação
introduzida aqui); expurgo do dado pessoal do Registro de Entrega que ficou "falhou" (V-07).

## Decisões fechadas

- Reentrega ao provedor não usa `nack`/requeue nem escada de filas com TTL crescente do RabbitMQ —
  roda sobre Postgres, no padrão de `OutboxPublisherWorker` (Decisão Técnica 1 da TechSpec; a imagem
  `rabbitmq:4.3-management-alpine` do `docker-compose.yml` não inclui plugin de delayed-message).
- A contagem de "tentativas ao provedor" é própria do Registro de Entrega, nunca derivada do número
  de redeliveries brutas do broker (deixaria de ser 1:1 com mais de uma instância do consumidor).
- Timeout do provedor é classificado como transitório, não como definitivo (TechSpec §Verificação —
  cenário crítico não óbvio).
- O sinal de "tratamento manual" é responsabilidade da aplicação apenas na **emissão**; o roteamento
  para uma pessoa é responsabilidade de plataforma (QP-04 do PRD, não bloqueia esta task).

## Modificar / Referenciar

- **modificar:** `src/notification/src/CodeForCoders.Notification.Infra.Data/Adapters/HttpTransactionalEmailSender.cs` (classificar a resposta do provedor em transitória/definitiva antes de propagar; hoje `EnsureSuccessStatusCode()` trata todo erro igual)
- **modificar:** `src/notification/src/CodeForCoders.Notification.Infra.Messaging/Configuration/RabbitMqOptions.cs` (parâmetro de limite de tentativas de reentrega ao provedor, distinto do `DeliveryLimit` do broker já existente)
- **modificar:** `src/notification/src/CodeForCoders.Notification.Infra.Data/DependencyInjection.cs` (registrar as novas opções de retry/backoff)
- **modificar:** `src/notification/src/CodeForCoders.Notification.Infra.Messaging/DependencyInjection.cs` (registrar o novo processo de reentrega ao provedor)
- **ref:** `src/notification/src/CodeForCoders.Notification.Infra.Messaging/OutboxPublisherWorker.cs` (padrão de *poll* com `PeriodicTimer` e seleção `FOR UPDATE SKIP LOCKED` a reaproveitar)
- **ref:** `context/architecture-baseline.md:405-413` (sinais de observabilidade obrigatórios desde a Fase 1; divisão emissão/roteamento de alerta)
- **ref:** `techspec.md` (Riscos e Preocupações — `OutboxPublisherWorker` não emite sinal de
  esgotamento hoje; este novo processo não deve repetir a lacuna)

## Pronto quando

- [ ] Gate passa (exit 0): `dotnet test src/notification/tests/CodeForCoders.Notification.IntegrationTests/CodeForCoders.Notification.IntegrationTests.csproj -- --filter-class "CodeForCoders.Notification.IntegrationTests.RetryProviderTransientFailureTests" --minimum-expected-tests 2`
- [ ] Fake do provedor configurado para falhar transitoriamente N vezes seguidas: espaçamento
      crescente entre tentativas observado, estado final "falhou" com `esgotouTentativas: true`,
      evento `notificacao.entrega-falhou.v1` publicado, e sinal de "tratamento manual" emitido
      exatamente uma vez
- [ ] Um segundo pedido publicado durante a janela de indisponibilidade do provedor é aceito
      normalmente (Registro de Entrega "aceito"), não recusado
