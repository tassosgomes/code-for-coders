---
status: done
task_kind: vertical
blocked_by: ["3.0", "4.0"]
gate: 'dotnet test --project src/billing/tests/CodeForCoders.Billing.IntegrationTests/CodeForCoders.Billing.IntegrationTests.csproj -- --filter-class CodeForCoders.Billing.IntegrationTests.PaymentSessionTests --minimum-expected-tests 6 && dotnet test --project src/billing/tests/CodeForCoders.Billing.IntegrationTests/CodeForCoders.Billing.IntegrationTests.csproj -- --filter-class CodeForCoders.Billing.IntegrationTests.GatewayWebhookTests --minimum-expected-tests 7 && dotnet test --project src/billing/tests/CodeForCoders.Billing.IntegrationTests/CodeForCoders.Billing.IntegrationTests.csproj -- --filter-class CodeForCoders.Billing.IntegrationTests.StripeGatewayAdapterTests --minimum-expected-tests 3 && dotnet test --project src/commerce/tests/CodeForCoders.Commerce.IntegrationTests/CodeForCoders.Commerce.IntegrationTests.csproj -- --filter-class CodeForCoders.Commerce.IntegrationTests.OrderPaymentTests --minimum-expected-tests 7 && dotnet test --project src/commerce/tests/CodeForCoders.Commerce.IntegrationTests/CodeForCoders.Commerce.IntegrationTests.csproj -- --filter-class CodeForCoders.Commerce.IntegrationTests.PurchaseGrantTests --minimum-expected-tests 6 && dotnet test --project src/commerce/tests/CodeForCoders.Commerce.IntegrationTests/CodeForCoders.Commerce.IntegrationTests.csproj -- --filter-class CodeForCoders.Commerce.IntegrationTests.BillingClientTests --minimum-expected-tests 3 && dotnet test --project src/bff-student/tests/CodeForCoders.BffStudent.IntegrationTests/CodeForCoders.BffStudent.IntegrationTests.csproj -- --filter-class CodeForCoders.BffStudent.IntegrationTests.OrderPaymentProxyTests --minimum-expected-tests 3 && npm --prefix src/student-spa run test -- order-return'
gate_expect: "Pelo menos 35 testes .NET passam (6, 7 e 3 de billing; 7, 6 e 3 de commerce; 3 do BFF) e os testes do student-spa com o fragmento order-return passam"
---

# 5.0 O aluno paga com cartão e o acesso ao curso é liberado sozinho

**Fatia:** V-02 · **Cobre:** RF-04 (cartão), RF-05, RF-07; RN-V05, RN-V09, RN-V10; RN-CB01, RN-CB02, RN-CB04, RN-CB05, RN-CB06; RN-D02, RN-D03, RN-D04, RN-D10; US-01, US-02; C-02, C-04, C-05, C-07 · **Spec:** `techspec.md` § V-02, § Bloco Backend, § Contratos e Fronteiras · **ADR:** [ADR-0017](../../docs/adr/0017-autenticacao-de-servico-commerce-em-billing.md)

## Comportamento

**Ir ao pagamento.** `startOrderPayment` (BFF) → `startOrderPaymentInternal` (`commerce`): pedido que não está `awaiting-payment` → 422 `ORDER_NOT_PAYABLE` sem chamar `billing`; senão `commerce` chama `ensurePaymentSessionInternal` com asserção de serviço própria (`aud billing`, `payment:request`, ADR-0017), valor, moeda e descrição congelados ("título — opção") e as URLs de volta `{StudentApp:PublicBaseUrl}/student/pedidos/{orderId}?resultado=concluido|saiu`; grava `paymentPageExpiresAt`; devolve `paymentUrl`, que o BFF repassa sem guardar nem registrar. `billing` ou Stripe fora → 503 `PAYMENT_PROVIDER_UNAVAILABLE` e o pedido inalterado.

**`billing` — página.** Agregado Pagamento, um por (escola, pedido). `ensurePaymentSessionInternal` cria o Checkout hospedado (modo pagamento, um item com descrição e valor, cartão, PIX e boleto, `client_reference_id` = pedido, metadados de escola e pedido, validade de 24 h, chave de idempotência do Stripe derivada do pedido) ou devolve a página aberta e válida; termos divergentes → 422 `PAYMENT_TERMS_CONFLICT`; URLs fora da lista do ambiente → 400 (http só em `localhost` de desenvolvimento, D-11); gateway fora → 503 `GATEWAY_UNAVAILABLE`. O `paymentUrl` **não é persistido** em lugar nenhum (D-05). Nada do Stripe sai da porta do gateway (RN-CB05).

**`billing` — webhook.** `POST /webhooks/v1/stripe/events`: assinatura `Stripe-Signature` verificada com o segredo e tolerância de horário; inválida/ausente → 400 `SIGNATURE_INVALID`, nada gravado. Válida → inbox por `id` do evento com **só** id, tipo, momento, ids do objeto (sessão, PaymentIntent, `client_reference_id`, metadados) e campos traduzíveis — **nunca** o corpo nem `customer_details`; 200 depois de gravar; repetido → 200 sem efeito; tipo não tratado → 200 ignorado; falha antes de gravar → 503. Um processador traduz `checkout.session.completed` pago em `cobranca.pagamento-confirmado.v1` (meio `card`, valor, referência, `confirmedAt`) no outbox, uma vez por pagamento. Reembolso e contestação: registrados e ignorados (DP-07).

**`commerce` — Vendas.** Fila `commerce.sales-payments` ligada a `billing.events`. `pagamento-confirmado` → pedido `paid` (com `paidAt`, meio, valor confirmado, referência) e `vendas.compra-concluida.v1` no outbox na mesma transação; pedido já `paid` → sem efeito; pedido desconhecido → DLQ e alerta; valor divergente → conclui e registra a divergência (D-06). Fila `commerce.sales-access-granted` ligada a `commerce.events`: `matricula.acesso-concedido.v1` de origem `purchase` → `grantId` e `accessGrantedAt` no pedido; outras origens ignoradas.

**`commerce` — Matrícula.** Fila `commerce.entitlement-purchases`: `vendas.compra-concluida.v1` → concessão `purchase`, `originRef` = pedido, vigência **da mensagem** contada do momento da concessão pela função de término da cortesia (RN-D03, RN-D04), sem motivo nem autor; `matricula.acesso-concedido.v1` na mesma transação. Índice único (escola, origem, `origin_ref`): reentrega → sem segunda concessão nem fato (RN-D10). Curso desconhecido → DLQ e alerta. `decideAccessInternal`, `listStudentCourseAccessInternal` e a lista de concessões do backoffice passam a ver a concessão de compra sem mudança de código (motivo só para cortesia).

**`student-spa`.** Na página do pedido: *Ir para o pagamento* → `startOrderPayment` → navega para `paymentUrl`; 503 → *Tentar de novo*. Volta com `?resultado=concluido`: consulta `getMyOrder` a cada 3 s mostrando "Confirmando pagamento"; `paid` sem `accessGrantedAt` → "Liberando seu acesso"; `paid` com `accessGrantedAt` → "Compra confirmada" (anunciado a leitor de tela) e *Ir para o curso* → `listMyCourses` → `/aulas/{continueLessonId}`; após 2 min, aviso de demora e consulta a cada 15 s. Volta com `?resultado=saiu` → *Continuar pagamento*.

**Sinais.** Histograma confirmação → concessão; contador de pedidos pagos há mais de 10 min sem concessão; contadores de eventos do webhook por tipo traduzido e de assinaturas recusadas; divergência de valor. Sem id de aluno, e-mail nem endereço de pagamento em atributos e logs.

Casos que os testes provam: página única no Stripe para duas chamadas ao mesmo pedido; termos divergentes → 422; URL não permitida → 400; adaptador real contra servidor HTTP local que responde como a API do Stripe (cria sessão com os parâmetros esperados, propaga a chave de idempotência, trata erro 5xx como indisponível); assinatura inválida → 400 e nada na inbox; o mesmo evento três vezes → um `pagamento-confirmado`; a linha da inbox não contém nome, e-mail nem CPF do payload de teste; pedido pago e `compra-concluida` na mesma transação; `compra-concluida` reentregue → uma concessão; concessão de 12 meses em 05/10/2026 termina em 05/10/2027; vitalícia sem término; vigência da mensagem prevalece sobre a oferta alterada; Matrícula parada → concessão ao voltar; `acesso-concedido` registrado no pedido; `billing` fora → 503 e pedido inalterado; cliente real de `billing` com asserção contra servidor HTTP local; fatos `cobranca.pagamento-confirmado`, `vendas.compra-concluida` e `matricula.acesso-concedido` (`purchase`) conformes aos AsyncAPI do PRD. No SPA: confirmando → confirmada → aula; aviso de demora; volta sem concluir → continuar; 503 → tentar de novo.

## Fora do escopo desta task

- PIX e boleto pendentes, retomada → 6.0; expiração e desistência → 7.0.
- Comprovante → 8.0.
- Cadastro do webhook no painel e o E2E em homologação (dependem do domínio público, pendência da TechSpec).

## Decisões fechadas

- Pedido do pagamento síncrono, resultado por fato (C-02); um salto (BA08).
- Link de pagamento e corpo do webhook nunca persistidos (D-05).
- Matrícula consome a compra concluída de Vendas, nunca o pagamento de `billing` (RN-D03).
- O pedido nunca decide acesso (RN-V10): *Ir para o curso* usa `listMyCourses`, e a aula continua exigindo a decisão de Matrícula.

## Modificar / Referenciar

- **modificar:** `src/commerce/src/CodeForCoders.Commerce.Domain/Entities/AccessGrant.cs` (fábrica de compra); `src/commerce/src/CodeForCoders.Commerce.Infra.Data/Configurations/AccessGrantConfiguration.cs` (índice único de origem, migration pelo EF); `src/commerce/src/CodeForCoders.Commerce.Infra.Messaging/RabbitMqTopologyInitializer.cs`, `Configuration/RabbitMqOptions.cs`, `DependencyInjection.cs` (três filas, DLQs, consumidores); `src/commerce/src/CodeForCoders.Commerce.Api/appsettings.json` (`StudentApp:PublicBaseUrl`, cliente de `billing`); `src/commerce/src/CodeForCoders.Commerce.Infra.Data/CommerceDbContext.cs`
- **modificar:** `src/billing/` (agregado, porta e adaptador do gateway, webhook, inbox, processador, fatos)
- **modificar:** `src/bff-student/src/CodeForCoders.BffStudent.Api/Extensions/EndpointExtensions.cs`
- **modificar:** `src/student-spa/src/config/paths.ts`, `src/student-spa/src/app/router.tsx` (página do pedido completa)
- **modificar:** `docker-compose.yml`, `docker-compose.remote.yml`, `docker-compose.coolify.yml` (cliente `commerce`→`billing`, emissor `commerce` em `billing`, `StudentApp__PublicBaseUrl`, `Stripe__*`, filas)
- **ref:** `src/commerce/src/CodeForCoders.Commerce.Api/Clients/StudentAccountConfirmationClient.cs`, `Security/StudentAccountAssertionTokenFactory.cs` (cliente com asserção de `commerce`); `src/commerce/src/CodeForCoders.Commerce.Infra.Messaging/EntitlementCourseConsumerWorker.cs` (consumidor); `src/commerce/src/CodeForCoders.Commerce.Domain/Entities/AccessTerm.cs`; `src/commerce/src/CodeForCoders.Commerce.Infra.Data/Queries/StudentAccessGrantQueries.cs:18` (motivo só de cortesia); `src/bff-student/src/CodeForCoders.BffStudent.Api/Clients/MyCoursesLearningClient.cs`; `internal-api-contract-billing.yaml`, `webhook-contract-billing.yaml`, `asyncapi-contract-billing.yaml`, `asyncapi-contract.yaml`; documentação do Stripe pelo Context7 (`/websites/stripe`)

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/billing` | `dotnet format src/billing/CodeForCoders.Billing.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/billing` | `dotnet test --project src/billing/tests/CodeForCoders.Billing.ArchitectureTests/CodeForCoders.Billing.ArchitectureTests.csproj` | exit 0 (nenhum tipo do Stripe fora do adaptador) | `ci-dotnet.yml` Testes |
| `src/commerce` | `dotnet format src/commerce/CodeForCoders.Commerce.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/commerce` | `dotnet test --project src/commerce/tests/CodeForCoders.Commerce.ArchitectureTests/CodeForCoders.Commerce.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/commerce` | `dotnet test --project src/commerce/tests/CodeForCoders.Commerce.IntegrationTests/CodeForCoders.Commerce.IntegrationTests.csproj -- --filter-class CodeForCoders.Commerce.IntegrationTests.CourtesyGrantTests` | exit 0 (a cortesia não muda) | `ci-dotnet.yml` Testes |
| `src/bff-student` | `dotnet format src/bff-student/CodeForCoders.BffStudent.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/student-spa` | `npm --prefix src/student-spa run lint`; `npm --prefix src/student-spa run typecheck` | exit 0 cada | `ci-react-ts.yml` |
| Compose | `docker compose ... config -q` nos três arranjos | exit 0 cada | AGENTS.md |

## Pronto quando

- [ ] Gate focalizado passa (exit 0).
- [ ] `billing` e `commerce` iniciam com os registros reais (filas, consumidores, cliente, porta do gateway) no ambiente de teste.
- [ ] No sandbox do Stripe, com `stripe listen --forward-to http://localhost:5110/webhooks/v1/stripe/events` e as chaves de teste no `.env`: pedido de 12 meses → *Ir para o pagamento* → cartão de teste aprovado → volta a `http://localhost:8082/student/pedidos/{orderId}?resultado=concluido` → "Compra confirmada" → *Ir para o curso* reproduz a aula; `decideAccessInternal` responde `allowed` até o fim do dia 12 meses depois.
- [ ] A linha da inbox do webhook e as tabelas de `commerce` não contêm o e-mail, o nome nem o endereço de pagamento usados no teste.
- [ ] O mesmo evento reenviado pela Stripe CLI não cria segunda concessão.
