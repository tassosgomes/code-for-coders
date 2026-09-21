---
status: done
task_kind: vertical
blocked_by: ["5.0"]
gate: "dotnet test src/notification/tests/CodeForCoders.Notification.IntegrationTests/CodeForCoders.Notification.IntegrationTests.csproj -- --filter-class \"CodeForCoders.Notification.IntegrationTests.FailImmediatelyOnPermanentProviderErrorTests\" --minimum-expected-tests 1"
gate_expect: "1 teste passa"
---

# 6.0 Falhar imediatamente em erro definitivo do provedor

**Fatia:** V-06 · **Cobre:** RF-06 (segundo critério) · **Spec:** `techspec.md#v-06` · **ADR:** —

## Comportamento

O provedor responde, na primeira tentativa, com um erro classificado como **definitivo** (endereço
inexistente, recusa permanente — 4xx que não seja 429), usando a classificação transitório/definitivo
já introduzida na Task 5.0. Nenhuma nova tentativa é agendada: o Registro de Entrega vai direto para
"falhou" com `esgotouTentativas: false`, e no mesmo commit é gravado o outbox de
`notificacao.entrega-falhou.v1` com o motivo classificado.

Esta task não introduz nova classificação de erro — reaproveita a que a Task 5.0 já construiu no
adaptador — e prova que o caminho "falha definitiva" do processo de reentrega termina em uma única
tentativa, sem passar pelo backoff.

## Fora do escopo desta task

Falha transitória e esgotamento de tentativas (V-05, já resolvida e reaproveitada aqui); qualquer
alteração na classificação de erro em si — se a classificação precisar mudar, é escopo da Task 5.0.

## Decisões fechadas

- Falha definitiva nunca agenda retry, mesmo que o limite de tentativas configurado seja maior que
  zero — a distinção é sobre o **tipo** de erro, não sobre tentativas restantes.
- `esgotouTentativas: false` é o valor correto para falha definitiva imediata; `true` é exclusivo do
  caminho de esgotamento por tentativas (V-05) — os dois nunca coexistem no mesmo desfecho.

## Modificar / Referenciar

- **ref:** `src/notification/src/CodeForCoders.Notification.Infra.Data/Adapters/HttpTransactionalEmailSender.cs` (classificação transitório/definitivo introduzida na Task 5.0 — não modificar a lógica de classificação em si nesta task, salvo lacuna encontrada)
- **ref:** o processo de reentrega ao provedor criado na Task 5.0 — esta task garante o ramo "não
  agendar retry" desse mesmo processo
- **ref:** `tasks/prd-notificacao-transacional/asyncapi-contract.yaml` (`EntregaFalhouPayload.esgotouTentativas`)

## Pronto quando

- [ ] Gate passa (exit 0): `dotnet test src/notification/tests/CodeForCoders.Notification.IntegrationTests/CodeForCoders.Notification.IntegrationTests.csproj -- --filter-class "CodeForCoders.Notification.IntegrationTests.FailImmediatelyOnPermanentProviderErrorTests" --minimum-expected-tests 1`
- [ ] Fake do provedor retornando erro definitivo na primeira chamada: nenhuma segunda chamada é
      feita, Registro de Entrega "falhou" com `esgotouTentativas: false`
- [ ] Evento `notificacao.entrega-falhou.v1` publicado com o motivo classificado e
      `esgotouTentativas: false`
