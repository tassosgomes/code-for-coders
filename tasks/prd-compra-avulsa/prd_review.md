# Validação full — PRD compra-avulsa (CAP-011)

Run: run.MtgqxZVz

- Modo: full · tentativa 2/3 · validator fresco, sem edição de código, tasks, estado ou commits.
- **Resultado: FULL VALIDATION APROVADA** — 0 bloqueantes, 8 recomendações (abertas desde a tentativa 1, sem mudança).
- `base_ref` / `target_ref`: `43e223d5bd7527fad0f80c3bebadb1795eabb07e` (inalterada desde a tentativa 1)
- `validated_commit`: `cf015ab1a382c2147b468abf766ab5a3a14fb4a0`
- `validated_tree`: `640d2e2f5e8808b29f0adcdc471d9eb12c758913`
- Integridade: HEAD, árvore e `git status --porcelain` (só `tasks/prd-compra-avulsa/flow-state.json` modificado, já assim na entrada) iguais no início e no fim. O sensor rodou em `git worktree` temporário, removido e conferido contra a linha de base. Nenhum processo ficou em segundo plano.
- Evidência anterior: full 1/3, run.HYUdHUSh, `validated_commit` `822047901f9536b6306ae2b9b36f71c5ab5a9d6b`, reprovada por B1 e B2 (ambos da task 5.0).

## Bloqueantes anteriores

| Id | Situação | Evidência desta tentativa |
|---|---|---|
| B1 — suíte completa de `commerce` (49 falhas, 406 de DLX no RabbitMQ) | **resolvido** | `dotnet test` de `src/commerce` inteiro: 486 testes, 0 falhas, exit 0 (Unit 8,9 s, Architecture 6,1 s, EndToEnd 14,8 s, Integration 2 min 48 s). O `HeartbeatFlowsThroughOutboxRabbitMqAndConsumer` também passou. Reexecutei os quatro projetos separadamente para a cobertura, todos exit 0. |
| B2 — `src/billing/Dockerfile` sem `Infra.Gateway` | **resolvido** | `docker build -f src/billing/Dockerfile .` exit 0 (`COPY` do csproj de `Infra.Gateway` acrescentado, `Dockerfile:18`). A imagem de teste foi removida. |

Revisão do diff de correção (`8220479..cf015ab`, 4 testes e 1 Dockerfile em `src/`): o isolamento de `CommerceInfrastructureTests` usa banco próprio e nomes `commerce.integration.*` para as três filas novas, `BillingExchange` e as demais. As classes `CatalogCourseListingTests`, `CourtesyCourseListingTests` e `EntitlementOutboxPublishingTests` ganharam as três filas de compra. O aumento de timeout (15→30 s; 100→300 tentativas) fica nos testes de espera e não relaxa nenhuma asserção nem remove teste. Nada de produção mudou além do `COPY`.

## Reaproveitamento (SKILL: "Full a partir da segunda tentativa")

Condições: a base não avançou; `git diff --name-only 822047901f..HEAD` toca só `src/billing/Dockerfile`, quatro arquivos de teste de `src/commerce` e artefatos de `tasks/`; não toca `Directory.*`, `global.json`, `nuget.config`, `contracts/`, `src/contract-testing/`, compose nem `.github/`. Reexecutados: `billing` e `commerce`. Os demais ficam **reaproveitados** da full run.HYUdHUSh, commit `822047901f`.

## Matriz de CI por componente

Parâmetros efetivos (`ci-dotnet.yml@v1` / `ci-react-ts.yml@v1`): .NET `10.0.x`, `--configuration Debug`, cobertura mínima 70 %, `build-container: true`, `security-mode: observe`.

| Componente | restore | `dotnet format --verify-no-changes` | testes (total / falhas) | cobertura | publish / docker | Origem |
|---|:-:|:-:|---|--:|:-:|---|
| **billing** | 0 | 0 | 86 / 0, exit 0 | 89,12 % (código inalterado; da full 1) | `docker build` exit 0 (publish dentro da imagem) | **reexecutado** nesta run (cobertura da full 1: só o Dockerfile mudou) |
| **commerce** | 0 | 0 | 486 / 0, exit 0 | 95,64 % (união de linhas de 4 relatórios Cobertura; mín. 70 %) | publish 0 (full 1; código de produção inalterado) | **reexecutado** nesta run |
| notification | 0 | 0 | 65 / 0, exit 0 | 86,99 % | 0 | reaproveitado, run.HYUdHUSh @ `822047901f` |
| identity | 0 | 0 | 181 / 0, exit 0 | 81,21 % | 0 | reaproveitado, run.HYUdHUSh @ `822047901f` |
| bff-student | 0 | 0 | 269 / 0, exit 0 | 86,87 % | 0 | reaproveitado, run.HYUdHUSh @ `822047901f` |
| bff-admin | 0 | 0 | 238 / 0, exit 0 | 84,13 % | 0 | reaproveitado, run.HYUdHUSh @ `822047901f` |
| learning (acionado por `contracts/`) | 0 | 0 | 245 / 0, exit 0 | 96,08 % | 0 | reaproveitado, run.HYUdHUSh @ `822047901f` |
| media (acionado por `contracts/`) | 0 | 0 | 233 / 0, exit 0 | 91,69 % | 0 | reaproveitado, run.HYUdHUSh @ `822047901f` |

| Componente | lint | `tsc --noEmit` | `npm test` | cobertura (linhas) | `npm run build -- --base=…` | Origem |
|---|:-:|:-:|---|--:|:-:|---|
| student-spa (`/student/`) | 0 | 0 | 185 / 0, exit 0 | 90,24 % | 0 | reaproveitado, run.HYUdHUSh @ `822047901f` |
| admin-spa (`/admin/`) | 0 | 0 | 245 / 0, exit 0 | 90,69 % | 0 | reaproveitado, run.HYUdHUSh @ `822047901f` |

Demais verificações do projeto (todas reaproveitadas da run.HYUdHUSh @ `822047901f`, pois compose, contratos, migrations e scripts não mudaram): compose `config -q` local e remoto exit 0; `coolify` exit 0 com as variáveis obrigatórias; Spectral exit 0 (0 erros, 7 avisos); `@asyncapi/cli validate` de `billing`, `commerce`, `notification` exit 0; `has-pending-model-changes` nos três exit 0; `bash -n` dos scripts exit 0; nenhum segredo (`sk_*`, `whsec_*`, `pk_*`) no diff.

## Sensor de discriminação

Execução em `git worktree` temporário de `cf015ab`, uma mutação de comportamento por fatia (a segunda de V-02 cobre o webhook), cada uma com a suíte focalizada da fatia. Na tentativa 1 o sensor não rodou, então não havia sobreviventes a repetir. Linha de base e árvore conferidas ao fim (`BASELINE_OK`).

| Fatia | Arquivo e mutação | Critério | Suíte | Resultado | Teste que matou |
|---|---|---|---|---|---|
| V-01 | `CreateOrder.cs:35`: `FindPendingAsync` substituído por `null` (pedido pendente nunca reaproveitado) | RN-V06 | `OrderCreationTests` | morta (2 de 16 falham) | `SequentialNumbersAreIndependentPerSchoolAndPendingReplayStays200`, `ConcurrentCreationsReturnOnePendingOrderAndOneFact` |
| V-02a | `Order.cs:65`: removido `if (Status == "paid") return false;` (confirmação sem idempotência) | RN-V09/V10 | `OrderPaymentTests`, `PurchaseReceiptRequestTests`, `PurchaseGrantTests` | morta (2 de 21) | `PaymentConfirmedFact_ReplayIsIdempotent`, `RepublishedPaymentKeepsOneReceiptAndStableRequestId` |
| V-02b | `StripeEventTranslator.cs:67`: `FixedTimeEquals` negado (assinatura aceita é a errada) | RN-CB04 | `GatewayWebhookTests` (billing) | morta (8 de 9) | `ValidSignatureRecordsEventInInboxAndReturns200`, `SameEventIdThreeTimesIsIdempotentAndStoresOnce` e outros |
| V-03 | `Order.cs:39`: removido `if (Status != "awaiting-payment") return false;` em `RecordPendingPayment` | V-03: "aguardando depois do confirmado é ignorado" | `PendingPaymentOrderTests`, `OrderPaymentTests` | morta (1 de 13) | `ReceivingPaymentAwaitingFactOnAlreadyPaidOrderHasNoEffect` |
| V-04 | `Order.cs:58`: removido `if (Status == "paid" \|\| Status == "cancelled") return false;` em `Expire` | RN-V05/V07 | `OrderExpirationTests`, `OrderPaymentTests`, `OrderCancellationTests` | morta (1 de 31) | `ReceivingPaymentNotConfirmedFactOnAlreadyPaidOrderHasNoEffect` |
| V-05 | `SalesPaymentSink.cs:53-54`: removido o `AppendAsync` do pedido de envio do comprovante | RF-09, RN-V09 | `PurchaseReceiptRequestTests` | morta (4 de 4) | `PaidOrderWritesFrozenReceiptAtomicallyWithoutPersonalContact` e outros três |
| V-06 | `StudentOrderQueries.cs:11`: removido o filtro por aluno | RF-10, RN-V12 | `StudentOrderListTests` | morta (1 de 10) | `OtherStudentsAndSchoolsAreExcludedFromRowsAndTotal` |
| V-07 | `FinanceOrderQueries.cs:11`: removido o filtro de situação | RF-11 | `FinanceOrdersTests` | morta (1 de 11) | `CombinedFiltersUseInclusiveSchoolDates` |

0 mutantes sobreviventes. Conferi nos logs que todas as falhas são asserções (nenhum `error CS`).

## Rastreabilidade e revisão semântica

A revisão semântica da full 1 (RN-CB01/CB03/CB04/CB05, RN-V03/V05/V06/V08/V09/V10, contratos promovidos, migrations) continua válida: o diff novo não toca código de produção além do `COPY` do Dockerfile. Cada task tem revisão focused aprovada, e a 5.0 foi revalidada (run.KgbPs1N2).

## Recomendações (não bloqueantes, 8)

Mantidas as da full 1; nenhuma quebra jornada do PRD nem check do CI.

1. **4.0 R2** — pedido pendente + oferta despublicada: `CreateOrder` consulta o catálogo antes de `FindPendingAsync` e devolve 404 (RN-V06). Inverter a ordem quando a task de correção tocar o arquivo.
2. **5.0** — inbox: `PaymentStore.ProcessPendingAsync` faz `continue` sem `Payment` e o lote é `LIMIT 50`; falta teste da janela de graça e do descarte por tempo (também 6.0 R1).
3. **5.0** — `paymentUrl` aceita `http` além de `https` (`PaymentResponseValidation.cs:12`, adaptador, `start-order-payment.ts:13`); restringir a `https` fora de desenvolvimento.
4. **6.0 R2** — o fallback de `GetPaymentMethodAsync` por `next_action` não tem teste.
5. **7.0** — comentário de `OrderCancellationConsumerWorker.cs:74` promete não gravar cancelamento com PIX vivo; RN-V08 admite a coexistência.
6. **8.0** — `contracts/notification/asyncapi.yaml` (exemplo do link, `NotificationServiceAssertion`) e reflow de `StudentAccountNotFound` em `contracts/identity/openapi-internal.yaml`: consolidar na conclusão do PRD.
7. **9.0** — *Meus pedidos* tem só o smoke do implementer com HTTP controlado; repetir contra o commerce real no checkpoint de integração.
8. **10.0** — `FinanceOrders` fora de `tags` globais em `contracts/commerce/openapi-internal.yaml` (aviso `operation-tag-defined`); linhas muito longas em `finance-orders-screen.tsx`; o filtro de cursos percorre o catálogo de 50 em 50.

Observação de padrão (sem bloqueio): `PaymentStore.ProcessPendingAsync` concentra tradução, decisão e publicação num `if` encadeado por resultado. Manter agora; gatilho para um método por resultado ou State sobre `Payment`: novo resultado do gateway.

## Limitações e passos não executados

- **Checkpoint manual no sandbox do Stripe:** não executado (sem `STRIPE_SECRET_KEY`/`STRIPE_WEBHOOK_SECRET` de teste); limitação, não falha. Nenhum teste chamou o Stripe real.
- **Não reproduzidos localmente (observacionais ou dependem do GitHub):** SAST (Semgrep), gitleaks, Trivy, SBOM, upload de artifact, push ao GHCR; todos em `security-mode: observe` (gitleaks é bloqueante e não foi executado).
- `npm ci` não foi executado (preserva o `node_modules` da working tree); SPAs e demais componentes .NET são reaproveitamento da full 1, conforme as condições acima.
- `docker build` rodou só para `billing` (único Dockerfile alterado nesta correção); a stack completa (`scripts/apps.sh start`) não foi subida: o bridge Docker local não alcança o RabbitMQ remoto.
- A cobertura de `commerce` (95,64 %) é a união de linhas de quatro relatórios gerados por `dotnet test --coverage`, não o script exato do workflow; a margem sobre o mínimo de 70 % é ampla.
- Matriz é execução local; não é espelho completo do CI (jobs de segurança e publicação ficam fora).
