---
status: done
task_kind: vertical
blocked_by: ["7.0"]
gate: 'dotnet test --project src/commerce/tests/CodeForCoders.Commerce.IntegrationTests/CodeForCoders.Commerce.IntegrationTests.csproj -- --filter-class CodeForCoders.Commerce.IntegrationTests.PurchaseReceiptRequestTests --minimum-expected-tests 4 && dotnet test --project src/identity/tests/CodeForCoders.Identity.IntegrationTests/CodeForCoders.Identity.IntegrationTests.csproj -- --filter-class CodeForCoders.Identity.IntegrationTests.StudentContactTests --minimum-expected-tests 5 && dotnet test --project src/notification/tests/CodeForCoders.Notification.IntegrationTests/CodeForCoders.Notification.IntegrationTests.csproj -- --filter-class CodeForCoders.Notification.IntegrationTests.PurchaseReceiptDeliveryTests --minimum-expected-tests 7'
gate_expect: "Pelo menos 16 testes passam (4 de commerce, 5 de identity, 7 de notification)"
---

# 8.0 O aluno recebe o comprovante por e-mail, endereçado à conta

**Fatia:** V-05 · **Cobre:** RF-09; RN-V09; RN-21, RN-26, RN-27, RN-28; US-04; C-06 · **Spec:** `techspec.md` § V-05, § Contratos e Fronteiras (percurso de dados sensíveis) · **ADR:** [ADR-0018](../../docs/adr/0018-notificacao-resolve-destinatario-pela-conta.md)

## Comportamento

**`commerce`.** Na mesma transação que marca o pedido `paid` e grava a compra concluída (5.0 e o caminho tardio de 7.0), Vendas grava o pedido de envio: `destinatarioConta {tipo: conta-aluno, id: studentId}` — **nunca** `destinatario` —, finalidade e modelo `comprovante-de-compra`, `pedidoId` derivado de forma estável do pedido (a republicação tem o mesmo), `dados` com número, curso e opção congelados, valor confirmado, meio, `pagoEm`, vigência congelada e o link `{StudentApp:PublicBaseUrl}/student/pedidos/{orderId}`. O publicador escolhe o exchange por **tabela chave → exchange** em configuração: `notificacao.envio-solicitado.v1` vai para o exchange de Notificação (`notification.events.default`), auditoria para `audit.events`, o resto para `commerce.events` (risco de `RabbitMqPublisher.cs:34`). O pedido e a concessão não esperam a entrega.

**`identity`.** `getStudentContactInternal` (`GET /internal/v1/student-accounts/{studentId}/contact`) para o emissor novo `notification` (ADR-0018, escopo `student-contact:read`, chave gerada em 3.0): e-mail, nome e situação da conta de aluno do tenant da asserção; inexistente, interna ou de outro tenant → 404 `STUDENT_ACCOUNT_NOT_FOUND`; desativada → 200 `disabled`; `no-store`; nada no log.

**`notification`** (1.2.0, `asyncapi-contract-notification.yaml`):
- Aceita exatamente um destinatário (`destinatario` **ou** `destinatarioConta`); os dois ou nenhum → recusado como forma inválida; `comprovante-de-compra` exige `destinatarioConta` e os dados do modelo; os pedidos 1.1.0 de Identity continuam aceitos sem mudança.
- O Registro de Entrega guarda a conta de destino e os dados do modelo; e-mail e nome são resolvidos **na entrega** por `getStudentContactInternal` e gravados no registro como já acontece com o e-mail (retenção em duas camadas inalterada).
- Conta indisponível (404 ou `disabled`) → não entrega; `notificacao.entrega-falhou.v1` com `motivo: destinatario-indisponivel` e `esgotouTentativas: false`. Identity fora → tentativa transitória, no mesmo limite e na mesma política da falha do provedor.
- Modelo `comprovante-de-compra`: curso, opção, valor em reais, meio por extenso ("cartão de crédito", "PIX", "boleto"), data do pagamento no fuso da escola, número do pedido, vigência ("acesso por N meses a partir da liberação" / "acesso vitalício"), botão para o pedido e a frase de que não é documento fiscal.
- Idempotência por `pedidoId` (RN-N09): reentrega não gera segundo e-mail.

Casos que os testes provam: pedido pago → um pedido de envio na fila de Notificação, conforme o AsyncAPI 1.2.0 (`AssertSends`), sem `destinatario`; republicação → mesmo `pedidoId`; o e-mail do aluno não aparece em tabela, outbox ou mensagem de `commerce`; publicador manda o pedido de envio ao exchange de Notificação e a auditoria ao de auditoria; Identity: contato de conta ativa, desativada, interna (404), de outro tenant (404), asserção sem escopo (403); Notificação: entrega pela conta com o texto do modelo; conta desativada → `entrega-falhou` `destinatario-indisponivel`; Identity fora → retido e entregue quando volta; os dois destinatários → recusado; comprovante sem conta → recusado; reentrega → um e-mail; pedido 1.1.0 com e-mail → entregue como antes.

## Fora do escopo desta task

- Comprovante como documento fiscal (`CAP-016`).
- Aviso de pedido expirado ou lembrete de boleto (não-objetivos do PRD).

## Decisões fechadas

- Destinatário pela conta, resolvido por Notificação em Identity (C-06, ADR-0018); nenhuma nova exceção a RN-21.
- O link do comprovante leva ao pedido, não à aula (TechSpec, URLs públicas).

## Modificar / Referenciar

- **modificar:** `src/commerce/src/CodeForCoders.Commerce.Infra.Messaging/RabbitMqPublisher.cs`, `Configuration/RabbitMqOptions.cs`
- **modificar:** `src/identity/src/CodeForCoders.Identity.Api/Endpoints/StudentAccountLookupEndpoints.cs` (ou endpoint irmão no mesmo padrão)
- **modificar:** `src/notification/src/CodeForCoders.Notification.Contracts/TransactionalNotificationV1.cs`; `src/notification/src/CodeForCoders.Notification.Application/Common/NotificationSendRequestRules.cs`, `NotificationPurposes.cs`; `src/notification/src/CodeForCoders.Notification.Application/UseCases/Notifications/AcceptNotificationSendRequest/AcceptNotificationSendRequest.cs`; `src/notification/src/CodeForCoders.Notification.Application/UseCases/Notifications/DeliverAcceptedNotification/DeliverAcceptedNotification.cs`; `src/notification/src/CodeForCoders.Notification.Application/Services/MessageTemplateRenderer.cs`; `src/notification/src/CodeForCoders.Notification.Domain/DeliveryRecords/DeliveryRecord.cs` (migration pelo EF)
- **modificar:** `docker-compose.yml`, `docker-compose.remote.yml`, `docker-compose.coolify.yml` (emissor `notification` em Identity; cliente de Identity em Notificação; exchange de Notificação em `commerce`)
- **ref:** `src/identity/src/CodeForCoders.Identity.Application/Services/StudentConfirmationMessageWriter.cs` (remetente no exchange de Notificação); `src/identity/tests/CodeForCoders.Identity.IntegrationTests/StudentAccountLookupTests.cs`; `src/notification/tests/CodeForCoders.Notification.IntegrationTests/RetryProviderTransientFailureTests.cs`, `PurgeDeliveryRecordPersonalDataTests.cs`; `src/commerce/src/CodeForCoders.Commerce.Api/Clients/StudentAccountConfirmationClient.cs` (padrão de cliente com asserção); `asyncapi-contract-notification.yaml`, `internal-api-contract-identity.yaml`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/notification` | `dotnet format src/notification/CodeForCoders.Notification.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/notification` | `dotnet test --project src/notification/tests/CodeForCoders.Notification.IntegrationTests/CodeForCoders.Notification.IntegrationTests.csproj -- --filter-class CodeForCoders.Notification.IntegrationTests.AcceptAndDeliverAccountConfirmationTests` | exit 0 (fluxo 1.1.0 inalterado) | `ci-dotnet.yml` Testes |
| `src/notification` | `dotnet test --project src/notification/tests/CodeForCoders.Notification.IntegrationTests/CodeForCoders.Notification.IntegrationTests.csproj -- --filter-class CodeForCoders.Notification.IntegrationTests.RejectInvalidSendRequestTests` | exit 0 | `ci-dotnet.yml` Testes |
| `src/notification` | `dotnet test --project src/notification/tests/CodeForCoders.Notification.IntegrationTests/CodeForCoders.Notification.IntegrationTests.csproj -- --filter-class CodeForCoders.Notification.IntegrationTests.PurgeDeliveryRecordPersonalDataTests` | exit 0 (purga cobre o registro pela conta) | `ci-dotnet.yml` Testes |
| `src/identity` | `dotnet format src/identity/CodeForCoders.Identity.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/identity` | `dotnet test --project src/identity/tests/CodeForCoders.Identity.IntegrationTests/CodeForCoders.Identity.IntegrationTests.csproj -- --filter-class CodeForCoders.Identity.IntegrationTests.StudentAccountLookupTests` | exit 0 | `ci-dotnet.yml` Testes |
| `src/commerce` | `dotnet format src/commerce/CodeForCoders.Commerce.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/commerce` | `dotnet test --project src/commerce/tests/CodeForCoders.Commerce.IntegrationTests/CodeForCoders.Commerce.IntegrationTests.csproj -- --filter-class CodeForCoders.Commerce.IntegrationTests.CourtesyGrantTests` | exit 0 (ato de auditoria continua no exchange de auditoria) | `ci-dotnet.yml` Testes |
| Compose | `docker compose ... config -q` nos três arranjos | exit 0 cada | AGENTS.md |

## Pronto quando

- [ ] Gate focalizado passa (exit 0).
- [ ] `notification` e `identity` iniciam com os registros reais (cliente de Identity, emissor novo) no ambiente de teste.
- [ ] Depois de uma compra no sandbox, o smtp4dev (`http://127.0.0.1:5000`) mostra um único comprovante com o texto do modelo, e o link abre `http://localhost:8082/student/pedidos/{orderId}` (com login, volta ao pedido).
- [ ] Busca do e-mail de teste nas tabelas e no outbox de `commerce` e `billing` e nos logs: zero ocorrências.
