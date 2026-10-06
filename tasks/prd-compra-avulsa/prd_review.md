# Validação full — PRD compra-avulsa (CAP-011)

Run: run.HYUdHUSh

- Modo: full · tentativa 1/3 · validator fresco, sem edição de código, tasks, estado ou commits.
- **Resultado: FULL VALIDATION REPROVADA** — 2 bloqueantes (B1, B2), 8 recomendações.
- `base_ref` / `target_ref`: `43e223d5bd7527fad0f80c3bebadb1795eabb07e`
- `validated_commit`: `822047901f9536b6306ae2b9b36f71c5ab5a9d6b`
- `validated_tree`: `3ac933d1b38753a4419b90cbdd3c59b1ceaea865`
- Integridade: HEAD, árvore e `git status --porcelain` (só `tasks/prd-compra-avulsa/flow-state.json` modificado, já assim na entrada) iguais no início e no fim. Nada foi alterado ou restaurado na working tree. Nenhum processo ficou em segundo plano.
- Escopo: `43e223d..HEAD` (557 arquivos): `billing` (novo), `commerce`, `notification`, `identity`, `bff-student`, `bff-admin`, `student-spa`, `admin-spa`, `contracts/`, os três composes, `scripts/`, ADRs 0016–0018 e `docs/design/wireframes-compra.md`.

## Bloqueantes

### B1 — A suíte completa de `commerce` reprova (49 de 486 testes) por colisão de filas no RabbitMQ — task 5.0

O job `commerce` do CI roda `dotnet test` da solução inteira e reprova nessa etapa. Reproduzido duas vezes (matriz e reexecução isolada de `IntegrationTests`): exit 2, 49 falhas, 48 delas com a mesma causa:

```
RabbitMQ.Client.Exceptions.OperationInterruptedException: code=406,
PRECONDITION_FAILED - inequivalent arg 'x-dead-letter-exchange' for queue
'commerce.sales-payments' in vhost '/': received 'commerce.events.dlx'
but current is 'commerce.integration.events.dlx'
  at RabbitMqTopologyInitializer.DeclarePurchasesAsync(...) RabbitMqTopologyInitializer.cs:148
  at RabbitMqTopologyInitializer.StartAsync(...) RabbitMqTopologyInitializer.cs:77
```

Causa: `DeclarePurchasesAsync` (introduzido em 5.0) declara `SalesPaymentsQueue`, `EntitlementPurchasesQueue` e `SalesAccessGrantedQueue` (`RabbitMqOptions.cs:69-72`) com a `DeadLetterExchange` configurada. `CommerceInfrastructureTests.cs:28-32` sobrescreve `Exchange`, `DeadLetterExchange` e as filas antigas para `commerce.integration.*`, mas não as três filas novas nem `BillingExchange`. As duas configurações dividem o mesmo RabbitMQ do `CommerceIntegrationFixture` (`CommerceIntegrationFixture.cs:20`, coleção `commerce-integration`). A primeira a declarar fixa o DLX; as demais recebem 406 ao subir o host.

Evidência de isolamento: `OfferChangeTests` sozinha passa (12/12, exit 0); `CommerceInfrastructureTests` + `OfferChangeTests` juntas reprovam 2 testes com o 406 acima. Os gates focados das tasks 5.0 a 10.0 filtram por classe e nunca exerceram a suíte completa. Na base não havia essas filas, então a regressão vem desta entrega.

Também reprovou `CommerceInfrastructureTests.HeartbeatFlowsThroughOutboxRabbitMqAndConsumer` (timeout de 15 s) nas duas execuções completas; passa quando roda só com `OfferChangeTests`. Provável mesma origem (estado compartilhado do broker). O implementer deve confirmar depois da correção.

Correção esperada (task 5.0, no teste ou na configuração, sem enfraquecer nem remover teste): isolar a topologia de cada configuração do teste de infraestrutura, incluindo `BillingExchange` e as filas de compra, ou dar a ela seu próprio broker. Gate de saída: `dotnet test` de `src/commerce` inteiro com exit 0.

### B2 — `src/billing/Dockerfile` não copia o projeto `Infra.Gateway`; a imagem não compila — task 5.0

`docker build -f src/billing/Dockerfile .` sai com exit 1:

```
error NETSDK1004: Assets file '/src/src/billing/src/CodeForCoders.Billing.Infra.Gateway/obj/project.assets.json' not found. Run a NuGet package restore
```

O `COPY` de csproj (`Dockerfile:11-16`) lista Api, Application, Contracts, Domain, Infra.Data e Infra.Messaging, mas não `CodeForCoders.Billing.Infra.Gateway.csproj`, criado em 5.0. O `dotnet restore` da imagem não o restaura e o `publish --no-restore` falha.

Impacto: o passo `build-container: true` do job `billing` (`.github/workflows/billing.yml`, `ci-dotnet.yml`) reprova; `scripts/apps.sh start` e o checkpoint "billing saudável em `localhost:5110`" não sobem, e a implantação no Coolify também não. Os demais Dockerfiles (`commerce`, `notification`, `identity`, `bff-student`, `bff-admin`) listam todos os seus csproj. Esta é a única lacuna.

Correção esperada (task 5.0): acrescentar o `COPY` do csproj de `Infra.Gateway`. Gate de saída: `docker build -f src/billing/Dockerfile .` com exit 0.

## Matriz de CI por componente (comandos de `ci-dotnet.yml@v1` e `ci-react-ts.yml@v1`, lidos de `tassosgomes/template-pipeline`)

Parâmetros efetivos: .NET `10.0.x`, `--configuration Debug`, cobertura mínima 70 % (união de linhas dos relatórios Cobertura, mesmo script do workflow, restrito a relatórios gerados nesta execução), `build-container: true`, `security-mode: observe`. Os componentes foram selecionados pelos `paths` dos workflows: `contracts/**` e `.github/` tocados acionam também `learning` e `media`. Os workflows de `audit` e `bff-*` não escutam `contracts/`; `audit` não foi tocado e ficou fora. Todos os comandos rodaram em primeiro plano, no máximo duas suítes em paralelo, cada uma com seus contêineres.

| Componente | restore | `dotnet format --verify-no-changes` | testes (total / falhas) | cobertura | publish | Veredito |
|---|:-:|:-:|---|--:|:-:|---|
| billing | 0 | 0 | 86 / 0, exit 0 | 89,12 % | 0 | passou (falta docker build, B2) |
| **commerce** | 0 | 0 | 486 / **49**, exit **2** | 93,45 % (informativo) | 0 | **reprovado (B1)** |
| notification | 0 | 0 | 65 / 0, exit 0 | 86,99 % | 0 | passou |
| identity | 0 | 0 | 181 / 0, exit 0 | 81,21 % | 0 | passou |
| bff-student | 0 | 0 | 269 / 0, exit 0 | 86,87 % | 0 | passou |
| bff-admin | 0 | 0 | 238 / 0, exit 0 | 84,13 % | 0 | passou |
| learning (acionado por `contracts/`) | 0 | 0 | 245 / 0, exit 0 | 96,08 % | 0 | passou |
| media (acionado por `contracts/`) | 0 | 0 | 233 / 0, exit 0 | 91,69 % | 0 | passou |

| Componente | lint (`npm run lint`) | `tsc --noEmit` | `npm test` (vitest, cobertura) | cobertura (linhas) | `npm run build -- --base=…` | Veredito |
|---|:-:|:-:|---|--:|:-:|---|
| student-spa (`--base=/student/`) | 0 | 0 | 185 testes em 29 arquivos, exit 0 | 90,24 % | 0 | passou |
| admin-spa (`--base=/admin/`) | 0 | 0 | 245 testes em 45 arquivos, exit 0 | 90,69 % | 0 | passou |

Demais verificações do projeto:

| Verificação | Resultado |
|---|---|
| `docker compose -f docker-compose.yml config -q` | exit 0 |
| `docker compose -f docker-compose.yml -f docker-compose.remote.yml config -q` | exit 0 |
| `docker compose -f docker-compose.coolify.yml config -q` | exit 1 sem variáveis (`STRIPE_SECRET_KEY`, `STRIPE_WEBHOOK_SECRET`, `AWS_MEDIA_BUCKET` obrigatórias por `${VAR:?}`); exit 0 com as obrigatórias preenchidas. A base também exige variáveis (`AWS_MEDIA_BUCKET`), então a falha sem variáveis não é nova |
| Spectral (`contracts/**/openapi*.yaml`, ruleset `.spectral-exception.yaml`, `--fail-severity=error`) | exit 0: 0 erros, 7 avisos |
| `@asyncapi/cli validate` de `billing`, `commerce`, `notification` | exit 0 nos três |
| `dotnet ef migrations has-pending-model-changes` (billing, commerce, notification) | exit 0 nos três: sem mudanças pendentes |
| `docker build -f src/billing/Dockerfile .` | **exit 1 (B2)** |
| `bash -n` de `scripts/apps.sh`, `remote-infra.sh`, `generate-local-env.sh` | exit 0 |
| Segredos no diff (`sk_*`, `whsec_*`, `pk_*`) | nenhum; chaves do Stripe só por variável de ambiente |

## Rastreabilidade e revisão semântica

Revisão dirigida, proporcional ao diff, sobre as regras do PRD de maior risco:

- **RN-CB04/CB05:** o webhook (`StripeEventTranslator.cs`) verifica HMAC-SHA256 com `FixedTimeEquals`, tolerância de 5 min e rejeita corpo malformado; o inbox é idempotente por id do evento (violação de unicidade tratada). O vocabulário do Stripe aparece fora de `billing` só em testes que o usam para afirmar a ausência.
- **RN-V03/V05/V08/V09/V10:** `Order.cs` congela preço e vigência na criação; `ConfirmPayment` aceita pedido `expired` ou `cancelled` (pagamento tardio concede); `SalesPaymentSink` publica uma única `CompraConcluida` e um único comprovante por confirmação. `learning` e `media` não consultam situação de pedido.
- **RN-V06:** `CreateOrder` reaproveita o pedido pendente por aluno e oferta, com idempotência por chave.
- **RN-CB01/CB03:** só a página do gateway recebe dado de cartão; `expires_after_seconds=86400` (PIX), `expires_after_days=3` (boleto, vencimento às 23:59:59 -03:00).
- **Contratos:** os onze arquivos promovidos a `contracts/` validam; os testes de contrato leem só `contracts/` e passaram em todos os componentes verdes. `info.version`, README e origem seguem a decisão de consolidar no fim do PRD.
- **Migrations:** geradas pela ferramenta; sem divergência de modelo.
- Cada task tem revisão focused aprovada; as recomendações abertas delas foram avaliadas (abaixo) e nenhuma quebra jornada do PRD nem check do CI.

## Recomendações (não bloqueantes)

1. **4.0 R2** — pedido pendente + oferta despublicada: `CreateOrder` chama `catalog.FindAsync` antes de `FindPendingAsync` e devolve 404 em vez do pendente (RN-V06). É borda; o pedido pendente segue acessível pela página do pedido e por *Meus pedidos*, e criar pedido novo de oferta despublicada é o que RN-V02 exige. Não quebra jornada. Inverter a ordem quando a task de correção tocar o arquivo.
2. **5.0** — inbox: `PaymentStore.ProcessPendingAsync` faz `continue` quando não existe `Payment` e o lote é `ORDER BY occurred_at LIMIT 50`. Cada entrada libera a fila em 10 min (descarte por tempo), o que limita o risco; falta teste da janela de graça e do descarte (também 6.0 R1).
3. **5.0** — `paymentUrl` aceita `http` além de `https` no BFF (`PaymentResponseValidation.cs:12`), no adaptador e no SPA (`start-order-payment.ts:13`). A origem é o próprio Stripe via `billing`; restringir a `https` fora de desenvolvimento.
4. **6.0 R2** — o fallback de `GetPaymentMethodAsync` por `next_action` não tem teste.
5. **7.0** — corrigir o comentário de `OrderCancellationConsumerWorker.cs:74` (promete não gravar cancelamento com PIX vivo; RN-V08 admite a coexistência).
6. **8.0** — `contracts/notification/asyncapi.yaml` (exemplo do link e descrição de `NotificationServiceAssertion`) e o reflow de `StudentAccountNotFound` em `contracts/identity/openapi-internal.yaml`: consolidar na conclusão do PRD.
7. **9.0** — o item manual de *Meus pedidos* tem só o smoke do implementer com HTTP controlado; repetir contra o commerce real no checkpoint de integração.
8. **10.0** — `FinanceOrders` fora de `tags` globais em `contracts/commerce/openapi-internal.yaml` (aviso `operation-tag-defined`); `finance-orders-screen.tsx` com linhas muito longas; o filtro de cursos percorre o catálogo de 50 em 50.

Observação de padrão (sem bloqueio): `PaymentStore.ProcessPendingAsync` concentra tradução, decisão e publicação num `if` encadeado por resultado (`awaiting`, `confirmed`, `not-confirmed`). Uma refatoração simples em um método por resultado reduziria o risco de mudança. Um padrão State sobre `Payment` só se justificaria se surgissem mais situações; gatilho: novo resultado do gateway.

## Limitações e passos não executados

- **Sensor de discriminação: não executado.** O SKILL o roda depois de os checks obrigatórios passarem; B1 e B2 já determinam a reprovação. Fica exigido na próxima full (tentativa 2/3), com as mutações por fatia (V-01 a V-07), reaproveitando a evidência dos componentes que não mudarem.
- **Checkpoint manual no sandbox do Stripe:** não executado (sem `STRIPE_SECRET_KEY`/`STRIPE_WEBHOOK_SECRET` de teste); registrado como limitação, não como falha. Nenhum teste chamou o Stripe real.
- **Não reproduzidos localmente (observacionais ou dependem do GitHub):** SAST (Semgrep), gitleaks, Trivy de dependências e de imagem, SBOM, upload de artifact, push ao GHCR; todos em `security-mode: observe` (gitleaks é bloqueante e não foi executado).
- `npm ci` não foi executado, para não reescrever o `node_modules` da working tree; os SPAs usaram as dependências já instaladas.
- `docker build` rodou só para `billing` (único Dockerfile alterado); os demais não foram construídos, apenas conferidos quanto à lista de csproj.
- Matriz é execução local com `dotnet test` da solução por componente; não é espelho completo do CI (jobs de segurança e publicação ficam fora).
- A stack completa (`scripts/apps.sh start`) não foi subida: o bridge Docker local não alcança o RabbitMQ remoto e o B2 impede a imagem de `billing`.

## Para o implementer

Reabrir a task **5.0** para B1 e B2. Depois da correção, a próxima full deve repetir: `commerce` completo, `docker build` de `billing`, e o sensor de discriminação (nada aqui foi mutado).
