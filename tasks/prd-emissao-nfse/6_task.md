---
status: pending
task_kind: vertical
blocked_by: ["2.0", "3.0", "4.0", "5.0"]
gate: 'dotnet test --project src/billing/tests/CodeForCoders.Billing.IntegrationTests/CodeForCoders.Billing.IntegrationTests.csproj -- --filter-class CodeForCoders.Billing.IntegrationTests.NfseWebhookTests --minimum-expected-tests 8 && dotnet test --project src/billing/tests/CodeForCoders.Billing.IntegrationTests/CodeForCoders.Billing.IntegrationTests.csproj -- --filter-class CodeForCoders.Billing.IntegrationTests.FiscalDocumentDownloadTests --minimum-expected-tests 4 && dotnet test --project src/billing/tests/CodeForCoders.Billing.IntegrationTests/CodeForCoders.Billing.IntegrationTests.csproj -- --filter-class CodeForCoders.Billing.IntegrationTests.NfseDocumentAdapterTests --minimum-expected-tests 3 && dotnet test --project src/billing/tests/CodeForCoders.Billing.IntegrationTests/CodeForCoders.Billing.IntegrationTests.csproj -- --filter-class CodeForCoders.Billing.IntegrationTests.FiscalNoticeContractTests --minimum-expected-tests 3 && dotnet test --project src/notification/tests/CodeForCoders.Notification.IntegrationTests/CodeForCoders.Notification.IntegrationTests.csproj -- --filter-class CodeForCoders.Notification.IntegrationTests.FiscalNoticeDeliveryTests --minimum-expected-tests 4 && dotnet test --project src/bff-student/tests/CodeForCoders.BffStudent.IntegrationTests/CodeForCoders.BffStudent.IntegrationTests.csproj -- --filter-class CodeForCoders.BffStudent.IntegrationTests.FiscalDocumentProxyTests --minimum-expected-tests 4 && dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.IntegrationTests/CodeForCoders.BffAdmin.IntegrationTests.csproj -- --filter-class CodeForCoders.BffAdmin.IntegrationTests.FinanceFiscalProxyTests --minimum-expected-tests 3 && npm --prefix src/student-spa run test -- student-fiscal-issued && npm --prefix src/student-spa run test -- my-orders-fiscal && npm --prefix src/admin-spa run test -- finance-fiscal-issued'
gate_expect: "Pelo menos 29 testes .NET passam (8 de webhook, 4 de download, 3 do adaptador de arquivo, 3 do aviso em billing, 4 em notification, 4 no bff-student e 3 no bff-admin) e os testes das SPAs com os fragmentos student-fiscal-issued, my-orders-fiscal e finance-fiscal-issued passam"
---

# 6.0 O emissor autoriza, o aluno e o financeiro baixam, e sai um aviso

**Fatia:** V-04 · **Cobre:** RF-04 (autorização), RF-05, RF-06 (leitura), RF-07; RN-F04, RN-F05, RN-F06, RN-F09, RN-F11, RN-F12, RN-F13, RN-F14, RN-16; US-01, US-03 · **Spec:** `techspec.md#v-04` · **ADR:** 0018, 0020

## Comportamento

`nfse.issued` e `nfse.documents_ready` da submissão corrente, os dois, em qualquer ordem, disparam o download de PDF e XML. Um só não libera download. Com os dois arquivos gravados, a mesma transação congela tomador (nome, e-mail, tipo e valor), descrição, valor e competência, passa o documento a `issued`, marca `studyDocument` se o emissor sinalizou estudo, e acrescenta um aviso. Falha ao baixar não leva a `failed`, não incrementa `n` e não abre outra nota: o documento continua `queued` e o download é tentado de novo. O aluno segue vendo "Nota em emissão".

O webhook é `POST /webhooks/v1/notaas/events`, fora do BFF, ao lado do webhook do Stripe, sem inbox compartilhada. HMAC-SHA256 do corpo cru, cabeçalho `X-Notaas-Signature: sha256=` mais o hex. O segredo é configuração de ambiente, no mesmo papel do segredo do Stripe, e não é a chave da escola. Assinatura ruim não muda nada. `X-Notaas-Event` diferente de `evento` no corpo → 400 `SIGNATURE_INVALID`. A mesma `X-Notaas-Delivery` responde 200 sem segundo efeito. 200 só depois de gravar; falha antes disso → 503. `nfse.cancelled`, `teste: true`, evento desconhecido e autorização de submissão que já não é a corrente respondem 200 e não mudam a situação. A marca de estudo grava `studyDocument` sem tirar `issued` e sem segunda emissão. Corpo e cabeçalho de assinatura não vão para log.

O download usa o adaptador HTTP real contra um servidor local no recorte do emissor. Baixar não é submissão nova.

O aviso é `notificacao.envio-solicitado.v1`, finalidade e modelo `nota-fiscal-disponivel`, no exchange que Notificação já consome. Não vai para o exchange próprio de `billing`. `cobranca.*` continuam onde já estão. Dados: número do pedido, curso e link da página do pedido (`StudentApp:PublicBaseUrl` + `/pedidos/{orderId}`). Sem anexo e sem URL de emissor. `pedidoId` é o UUIDv5 do `fiscalDocumentId` descrito em `techspec.md` § Bloco Backend. Para `8f3e2d1c-0b9a-4c8d-9e7f-6a5b4c3d2e1f`, o `pedidoId` é `8a981d19-6d32-54f4-9f29-1011ffeaf273`. Republicar a mesma transição gera o mesmo `pedidoId`, e Notificação não manda um segundo e-mail. Notificação aceita o modelo antes de `billing` publicar. Conta indisponível falha com `destinatario-indisponivel`, como os outros envios pela conta. Falha do aviso não tira a nota de `issued` nem esconde o botão.

Visão do aluno (`getMyFiscalDocument`), JWT de audiência `billing` e escopo `fiscal-document:own`, sem e-mail:

- `awaiting-provider` e `queued` saem como `issuing`.
- `failed` sai sem motivo.
- PDF e XML só com `downloadsReady`. `Cache-Control: no-store`. O nome do arquivo usa o número do pedido.
- Outro aluno, ou pedido sem documento, recebe 404 `FISCAL_DOCUMENT_NOT_FOUND`.
- Ator recebe 403 `ACTOR_NOT_ALLOWED`.
- O prefixo `/api/v1/orders` do pedido continua audiência `commerce`. Os caminhos da nota são a exceção. 502/504 na nota não viram erro de *Meus pedidos* nem do detalhe do pedido. Com o aviso falho, o botão de baixar continua.

Visão do financeiro, audiência `billing` à parte da audiência `commerce` do pedido, permissão `financeiro.ler`:

- Detalhe: situação canônica, motivo e downloads. 404: não há nota. Outra falha: o pedido continua.
- Lista: lookup de no máximo 50 ids. Pedido sem documento é omitido. Falha da consulta não esconde a lista.
- Professor e suporte não baixam.

Nas duas SPAs, o texto e a marca "sem valor fiscal" são os do adendo aprovado em 2.0. A página não guarda PDF, XML nem documento do tomador em `localStorage` ou cache de consulta. Troca de documento na conta depois de `issued` não altera o PDF.

Contador de `issued`. O túnel `infra/cloudflared/billing-webhook.yml`, se for o caminho público, passa a deixar `/webhooks/v1/notaas/events`. O webhook local continua `http://localhost:5110/webhooks/v1/notaas/events`. Esta fatia fala só com a bancada (QA-03).

## Fora do escopo desta task

- Recusa, três ausências e o botão de nova tentativa → 7.0. Aqui `failed` só é lido, se a consulta devolver esse estado.
- Apagar o arquivo um ano depois → 8.0. Aqui `issued` sem `downloadsReady` já tem frase, para o estado existir na tela.
- Cadastro do endpoint de webhook no emissor: configuração de deploy, não caso de uso.

## Decisões fechadas

- *Emitida* exige os dois eventos na submissão corrente e os dois arquivos gravados (C-06, RN-F11).
- O aviso sai uma vez, pela conta, sem anexo (RN-F13, DP-04, ADR-0018).
- O aluno chega a `billing` com audiência `billing` (ADR-0020). O pedido continua em `commerce` (ADR-0016).
- A marca de estudo não abre segunda nota (RN-F14, QA-03).
- PDF e XML ficam em bytes no documento, no PostgreSQL de `billing` (`techspec.md` § Decisões Técnicas).

## Modificar / Referenciar

- **modificar:** `src/billing/src/CodeForCoders.Billing.Infra.Messaging/RabbitMqPublisher.cs` (aviso no exchange de Notificação; `cobranca.*` onde já estão); `src/billing/src/CodeForCoders.Billing.Api/Endpoints/PaymentEndpoints.cs` (o webhook do emissor não se mistura com o do Stripe e o corpo não é registrado); `src/bff-student/src/CodeForCoders.BffStudent.Application/Common/BffSecurityOptions.cs` (o prefixo `/api/v1/orders` do pedido continua `commerce`); `src/bff-student/src/CodeForCoders.BffStudent.Api/Security/BffSecurityMiddleware.cs` (exceção de audiência `billing` nos caminhos da nota); `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Endpoints/FinanceOrderEndpoints.cs` (pedido segue audiência `commerce`; a nota pede `billing` à parte); `src/notification/src/CodeForCoders.Notification.Application/Common/NotificationPurposes.cs` (modelo `nota-fiscal-disponivel`); `src/student-spa/src/features/student-purchase/components/student-order-screen.tsx`; `src/student-spa/src/features/student-purchase/components/my-orders-screen.tsx`; `src/admin-spa/src/features/finance-area/components/finance-order-detail-screen.tsx`; `src/admin-spa/src/features/finance-area/components/finance-orders-screen.tsx`; `docker-compose.yml`, `docker-compose.remote.yml`, `docker-compose.komodo.apis.yml`, `docker-compose.stable.yml` e `scripts/generate-local-env.sh` (segredo HMAC do webhook); `infra/cloudflared/billing-webhook.yml` (caminho da Notaas no ingress)
- **ref:** `src/billing/src/CodeForCoders.Billing.Infra.Data/Payments/GatewayInboxWorker.cs` (gravar, responder, processar depois; inbox separada); `src/billing/src/CodeForCoders.Billing.Infra.Data/Outbox/OutboxMessageWriter.cs` (o aviso não leva documento nem e-mail); `src/commerce/src/CodeForCoders.Commerce.Application/Common/StudentAppOptions.cs`; `tasks/prd-emissao-nfse/webhook-contract-notaas.yaml`; `tasks/prd-emissao-nfse/asyncapi-contract-notification.yaml`; `tasks/prd-emissao-nfse/api-contract-student.yaml`; `tasks/prd-emissao-nfse/api-contract-admin.yaml`; `docs/adr/0020-jwt-de-aluno-em-billing.md`; `docs/adr/0018-notificacao-resolve-destinatario-pela-conta.md`; `docs/design/wireframes-compra.md` (adendo aprovado em 2.0); `.claude/skills/dotnet/SKILL.md`; `.claude/skills/react/SKILL.md`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/billing` | `dotnet format src/billing/CodeForCoders.Billing.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/billing` | `dotnet test --project src/billing/tests/CodeForCoders.Billing.ArchitectureTests/CodeForCoders.Billing.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/billing` | `dotnet test --project src/billing/tests/CodeForCoders.Billing.IntegrationTests/CodeForCoders.Billing.IntegrationTests.csproj -- --filter-class CodeForCoders.Billing.IntegrationTests.GatewayWebhookTests --minimum-expected-tests 1` | exit 0 (o webhook do Stripe segue) | `ci-dotnet.yml` Testes |
| `src/notification` | `dotnet format src/notification/CodeForCoders.Notification.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/notification` | `dotnet test --project src/notification/tests/CodeForCoders.Notification.IntegrationTests/CodeForCoders.Notification.IntegrationTests.csproj -- --filter-class CodeForCoders.Notification.IntegrationTests.PurchaseReceiptDeliveryTests --minimum-expected-tests 1` | exit 0 (o comprovante de compra segue) | `ci-dotnet.yml` Testes |
| `src/bff-student` | `dotnet format src/bff-student/CodeForCoders.BffStudent.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/bff-student` | `dotnet test --project src/bff-student/tests/CodeForCoders.BffStudent.IntegrationTests/CodeForCoders.BffStudent.IntegrationTests.csproj -- --filter-class CodeForCoders.BffStudent.IntegrationTests.OrderProxyTests --minimum-expected-tests 1` | exit 0 (o pedido continua em `commerce`) | `ci-dotnet.yml` Testes |
| `src/bff-student` | `dotnet test --project src/bff-student/tests/CodeForCoders.BffStudent.IntegrationTests/CodeForCoders.BffStudent.IntegrationTests.csproj -- --filter-class CodeForCoders.BffStudent.IntegrationTests.MyOrdersProxyTests --minimum-expected-tests 1` | exit 0 | `ci-dotnet.yml` Testes |
| `src/bff-admin` | `dotnet format src/bff-admin/CodeForCoders.BffAdmin.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/bff-admin` | `dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.IntegrationTests/CodeForCoders.BffAdmin.IntegrationTests.csproj -- --filter-class CodeForCoders.BffAdmin.IntegrationTests.FinanceOrdersProxyTests --minimum-expected-tests 1` | exit 0 (a lista de pedidos continua em `commerce`) | `ci-dotnet.yml` Testes |
| `src/student-spa` | `npm --prefix src/student-spa run lint` | exit 0 | `ci-react-ts.yml` Lint |
| `src/student-spa` | `npm --prefix src/student-spa run typecheck` | exit 0 | `ci-react-ts.yml` Typecheck |
| `src/student-spa` | `npm --prefix src/student-spa run test -- my-orders` | exit 0 | `ci-react-ts.yml` Testes |
| `src/admin-spa` | `npm --prefix src/admin-spa run lint` | exit 0 | `ci-react-ts.yml` Lint |
| `src/admin-spa` | `npm --prefix src/admin-spa run typecheck` | exit 0 | `ci-react-ts.yml` Typecheck |
| `src/admin-spa` | `npm --prefix src/admin-spa run test -- finance-orders` | exit 0 | `ci-react-ts.yml` Testes |
| Túnel | `rg -q "webhooks/v1/notaas/events" infra/cloudflared/billing-webhook.yml` | exit 0, e o caminho do Stripe permanece | `techspec.md` § Riscos |

## Pronto quando

- [ ] Gate focalizado passa (exit 0).
- [ ] Assinatura ruim não muda nada; a mesma entrega não gera segundo aviso; um evento só não baixa; a ordem inversa também espera os dois.
- [ ] Com os dois arquivos, a nota fica `issued`, o tomador congelado não muda se a conta trocar o documento, e o aviso leva à página do pedido sem anexo e sem URL de emissor.
- [ ] O aluno dono e o financeiro baixam PDF e XML; outro aluno recebe 404; professor não baixa.
- [ ] *Meus pedidos* e a lista do financeiro abrem quando a consulta da nota devolve 502, e o botão continua se o aviso falha.
- [ ] Com `studyDocument`, as duas telas mostram "sem valor fiscal" em texto, e não nasce segunda nota.
- [ ] O webhook do Stripe continua passando, e a aplicação de teste sobe com o endpoint da Notaas e o adaptador de download reais.
