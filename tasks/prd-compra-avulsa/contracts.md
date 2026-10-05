---
tsg_artifact: contract
product: code-4-coders
capability: CAP-011
version: 1.0
status: approved
updated: 2026-10-05
sources: tasks/prd-compra-avulsa/prd.md@1.0, domains/catalogo-e-oferta/domain.md@1.1, domains/matricula-e-direito-de-acesso/domain.md@1.0, domains/identidade-e-acesso/domain.md@1.1, context/architecture-baseline.md@1.2, context/domain-map.md@1.2
---

# Contratos de integração — compra avulsa

> - PRD: [prd.md](prd.md), v1.0, aprovado em 2026-10-05 (OD79)
> - Data: 2026-10-05
> - Estado do conjunto: **Aprovado para implementação (1.0)** em 2026-10-05 — C-01 a C-12 aprovadas pelo responsável; todos os documentos validados sem erros.

> - **Errata 1.0.0 (2026-10-05, aprovada com a TechSpec, D-11):** `ensurePaymentSessionInternal` aceita `http` nas URLs de volta apenas em host local de desenvolvimento autorizado, para o smoke local em `http://localhost:8082`. Só a descrição muda; schema e versão do contrato permanecem.

Este conjunto registra o acordo para o primeiro PRD de `CAP-011`. A aprovação significa acordo para implementar; não afirma implantação. O contrato anterior de cada fronteira é o vigente em `contracts/` (ver `contracts/README.md`), e os recortes daqui registram só o que muda; ao concluir o PRD, são promovidos para lá.

## Seleção e escopo

| Integração identificada | Modalidade | Justificativa |
|---|---|---|
| `student-spa` → `bff-student` | OpenAPI | Resumo da compra, pedido, ida ao pagamento, desistência e *Meus pedidos* (RF-01 a RF-06, RF-10); depreciação do clique anônimo (DP-06). |
| `admin-spa` → `bff-admin` | OpenAPI | Lista e detalhe de pedidos (RF-11). |
| `bff-student`/`bff-admin` → `commerce` | OpenAPI | As mesmas regras, decididas pelo dono do pedido. |
| `commerce` → `billing` | OpenAPI | Vendas pede o recebimento (Domain Map §6) e recebe o endereço de pagamento (RF-04, RF-06). |
| Gateway → `billing` | OpenAPI (webhook) | Confirmação autenticada do gateway (RN-CB04; baseline, camada anticorrupção). |
| `bff-admin`/`notification` → `identity`; `bff-student` → `identity` | OpenAPI | Nome e e-mail para o backoffice (C-09); contato para o comprovante pela conta (C-06); JWT de aluno para `commerce` (C-03). |
| `billing` ↔ `commerce` | AsyncAPI | Fatos de pagamento e desistência do pedido. |
| Vendas → Matrícula, Matrícula → Vendas (`commerce`) | AsyncAPI | Compra concluída como ordem de concessão (RN-D03) e concessão registrada no pedido (C-07). |
| `commerce` → `notification` | AsyncAPI | Comprovante de compra (RF-09). |
| Pedido e pagamento como produto de dados | Não aplicável | Bancos internos de `commerce` e `billing`; nenhum consumidor recebe dataset com compromisso próprio. Nenhum ODCS. |

| Documento | Padrão | Versão do contrato | Fronteira | Estado |
|---|---|---|---|---|
| [api-contract.yaml](api-contract.yaml) e [.md](api-contract.md) | OpenAPI 3.1.0 | `bff-student` compra **1.0.0** (novo) | 6 operações; vitrine, conta, aula e progresso sem mudança | **Aprovado para implementação** em 2026-10-05; lint sem erros nem avisos |
| [api-contract-vitrine.yaml](api-contract-vitrine.yaml) e [.md](api-contract-vitrine.md) | OpenAPI 3.1.0 | `bff-student` vitrine 1.0.0 → **1.1.0** | Recorte: só `registerPurchaseIntent`, depreciada | **Aprovado para implementação** em 2026-10-05; lint sem erros nem avisos |
| [api-contract-admin.yaml](api-contract-admin.yaml) e [.md](api-contract-admin.md) | OpenAPI 3.1.0 | `bff-admin` pedidos **1.0.0** (novo) | 2 operações; área financeira e cortesias sem mudança | **Aprovado para implementação** em 2026-10-05; lint sem erros nem avisos |
| [internal-api-contract-commerce.yaml](internal-api-contract-commerce.yaml) e [.md](internal-api-contract-commerce.md) | OpenAPI 3.1.0 | `commerce` 1.3.0 → **1.4.0** | Recorte: 8 operações novas de Vendas e `registerPurchaseIntentInternal` depreciada | **Aprovado para implementação** em 2026-10-05; lint sem erros nem avisos |
| [internal-api-contract-billing.yaml](internal-api-contract-billing.yaml) e [.md](internal-api-contract-billing.md) | OpenAPI 3.1.0 | `billing` **1.0.0** (novo) | `ensurePaymentSessionInternal` | **Aprovado para implementação** em 2026-10-05; lint sem erros nem avisos |
| [webhook-contract-billing.yaml](webhook-contract-billing.yaml) e [.md](webhook-contract-billing.md) | OpenAPI 3.1.0 | `billing` webhook **1.0.0** (novo) | `receiveGatewayEvent` | **Aprovado para implementação** em 2026-10-05; lint sem erros nem avisos |
| [internal-api-contract-identity.yaml](internal-api-contract-identity.yaml) e [.md](internal-api-contract-identity.md) | OpenAPI 3.1.0 | Identity 1.4.0 → **1.5.0** | Recorte: `resolveStudentAccountsInternal`, `getStudentContactInternal` | **Aprovado para implementação** em 2026-10-05; lint sem erros nem avisos |
| [internal-api-contract-identity-student.yaml](internal-api-contract-identity-student.yaml) e [.md](internal-api-contract-identity-student.md) | OpenAPI 3.1.0 | Identity aluno 1.1.0 → **1.2.0** | Recorte: audiência `commerce` em `validateStudentSessionInternal` | **Aprovado para implementação** em 2026-10-05; lint sem erros nem avisos |
| [asyncapi-contract.yaml](asyncapi-contract.yaml) | AsyncAPI 3.0.0 | `commerce` 1.1.0 → **1.2.0** | Fatos de Vendas; Vendas e Matrícula recebendo; pedido de envio | **Aprovado para implementação** em 2026-10-05; parser sem erros |
| [asyncapi-contract-billing.yaml](asyncapi-contract-billing.yaml) | AsyncAPI 3.0.0 | `billing` **1.0.0** (novo) | Três fatos de pagamento; recebe a desistência | **Aprovado para implementação** em 2026-10-05; parser sem erros |
| [asyncapi-contract-notification.yaml](asyncapi-contract-notification.yaml) | AsyncAPI 3.0.0 | `notification` 1.1.0 → **1.2.0** | Recorte: pedido de envio pela conta e modelo `comprovante-de-compra` | **Aprovado para implementação** em 2026-10-05; parser sem erros |

## Participantes e interfaces

| Identificador | Provedor/produtor | Consumidores | Comportamento | PRD |
|---|---|---|---|---|
| `getPurchaseSummary` → `getPurchaseSummaryInternal` | `bff-student`, decisão de `commerce` | `student-spa` | Condição vigente, pedido pendente na opção, aviso de acesso existente (informativo; falha de Matrícula não bloqueia) | RF-01, RF-02 |
| `createOrder` → `createOrderInternal` | idem | `student-spa` | Pedido com condição congelada; 200 com o pendente da mesma opção; `Idempotency-Key` | RF-03 |
| `startOrderPayment` → `startOrderPaymentInternal` → `ensurePaymentSessionInternal` | `bff-student` → `commerce` → `billing` | `student-spa` | Endereço da página ou das instruções de PIX/boleto; idempotente por pedido; 503 sem mudar o pedido | RF-04, RF-06 |
| `cancelMyOrder` → `cancelOrderInternal` | `bff-student`, decisão de `commerce` | `student-spa` | Pedido `cancelled` + `vendas.pedido-cancelado.v1` | RF-06 |
| `listMyOrders`, `getMyOrder` → `*Internal` | idem | `student-spa` | Pedidos do aluno; situação real para a página de retorno | RF-05, RF-10 |
| `listFinanceOrders`, `getFinanceOrder` → `*Internal` + `resolveStudentAccountsInternal` | `bff-admin`, `commerce`, `identity` | `admin-spa` | Só leitura, `financeiro.ler`; aluno com nome e e-mail | RF-11 |
| `receiveGatewayEvent` | `billing` | gateway (Stripe) | Assinatura verificada, idempotente por evento, tradução para fatos | RF-07, RN-CB04 |
| `cobranca.pagamento-aguardando/confirmado/nao-confirmado.v1` | `billing` | `commerce` (Vendas) | Pendente, pago, vencido ou cancelado, no vocabulário da plataforma | RF-05, RF-07, RF-08 |
| `vendas.compra-concluida.v1` | `commerce` (Vendas) | `commerce` (Matrícula) | Ordem de concessão com a vigência congelada; um fato por pedido | RF-07, RN-V09 |
| `matricula.acesso-concedido.v1` (vigente) | `commerce` (Matrícula) | `commerce` (Vendas) | Agora também com `origin purchase`, `originRef` = pedido | RF-07, C-07 |
| `vendas.pedido-criado/cancelado/expirado.v1` | `commerce` (Vendas) | `billing` (cancelado); ninguém mais nesta entrega | Ciclo do pedido | RF-03, RF-06, RF-08 |
| `notificacao.envio-solicitado.v1` 1.2.0 + `getStudentContactInternal` | `commerce` → `notification` → `identity` | — | Comprovante pela conta; e-mail e nome resolvidos por Notificação | RF-09 |
| `validateStudentSessionInternal` (`audience: commerce`) | `identity` | `bff-student` | JWT de aluno `scope orders:use`, sem `email` | C-03 |

Relação entre os documentos, no caminho principal:

| Ação | HTTP | Mensagens |
|---|---|---|
| Comprar | `createOrder` → `createOrderInternal` | `vendas.pedido-criado.v1` |
| Ir ao pagamento | `startOrderPayment` → `startOrderPaymentInternal` → `ensurePaymentSessionInternal` | nenhuma |
| PIX/boleto gerado | `receiveGatewayEvent` | `cobranca.pagamento-aguardando.v1` |
| Pagamento confirmado | `receiveGatewayEvent` | `cobranca.pagamento-confirmado.v1` → `vendas.compra-concluida.v1` + `notificacao.envio-solicitado.v1` (mesma transação) → `matricula.acesso-concedido.v1` |
| Prazo vencido | `receiveGatewayEvent` (ou rotina de Vendas, C-10) | `cobranca.pagamento-nao-confirmado.v1` → `vendas.pedido-expirado.v1` |
| Desistir | `cancelMyOrder` → `cancelOrderInternal` | `vendas.pedido-cancelado.v1` → `billing` cancela no gateway |

`orderId` é o mesmo nas APIs, nos fatos de Vendas e de `billing`, no `originRef` da concessão e na referência do pagamento em `billing`. `studentId` é a conta de aluno em Identity em todo lugar; e-mail e nome só aparecem nas respostas de Identity para `bff-admin` e `notification`.

## Origem e decisões

| Entrada consultada | Versão e data | Decisão herdada |
|---|---|---|
| [PRD de CAP-011](prd.md) | v1.0, 2026-10-05 | RF-01 a RF-11; RN-V01 a RN-V12; RN-CB01 a RN-CB06; DP-01 a DP-09 |
| [Matrícula e Direito de Acesso](../../domains/matricula-e-direito-de-acesso/domain.md) | v1.0, 2026-09-30 | RN-D01 a RN-D04, RN-D07, RN-D08, RN-D10, RN-D17; compra concluída como ordem de concessão (§7) |
| [Catálogo e Oferta](../../domains/catalogo-e-oferta/domain.md) | v1.1 | RN-O09 a RN-O12, RN-O18 |
| [Identidade e Acesso](../../domains/identidade-e-acesso/domain.md) | v1.1 | RN-13, RN-16, RN-21, RN-23, RN-26, RN-27, RN-28 |
| [Baseline](../../context/architecture-baseline.md) | v1.2 | `billing` no agrupamento; camada anticorrupção e webhook dedicado; BA05, BA07, BA08 (1 salto), BA09, G06, G07, G09, G10, G18, G23; PCI mínimo |
| `contracts/commerce/openapi-internal.yaml` | 1.3.0 | Esquemas `StaffUserToken`, `StudentBffServiceAssertion`; `AccessPeriod`; padrão de erros |
| `contracts/commerce/asyncapi.yaml` | 1.1.0 | `AcessoConcedido` com `origin purchase` e `originRef` já reservados (C-10 de CAP-008) |
| `contracts/notification/asyncapi.yaml` | 1.1.0 | Pedido de envio, idempotência por `pedidoId`, retenção na indisponibilidade |
| `contracts/identity/openapi-internal.yaml`, `openapi-internal-student.yaml` | 1.4.0, 1.1.0 | `lookupStudentAccountInternal`; JWT de aluno por audiência; código `AUDIENCE_NOT_ALLOWED` |
| `contracts/bff-student/openapi-vitrine.yaml`, `openapi-aula.yaml`; `contracts/bff-admin/openapi-acesso-interno.yaml` | 1.0.0, 1.1.0, 1.4.0 | Cookie, CSRF, 502/504 do BFF; área financeira com `financeiro.ler` |
| [ADR-0004](../../docs/adr/0004-autenticacao-de-servico-bff-identity.md), [ADR-0005](../../docs/adr/0005-sessao-e-servico-do-backoffice.md), [ADR-0010](../../docs/adr/0010-autenticacao-de-servico-commerce-em-identity.md), [ADR-0013](../../docs/adr/0013-jwt-de-aluno-validado-por-servicos-de-dominio.md) | aceitas | Asserção de serviço com vários emissores; JWT de ator e de aluno por audiência |

Decisões deste contrato:

| ID | Decisão | Alternativa descartada | Estado |
|---|---|---|---|
| C-01 | **`billing` nasce nesta entrega**, como o baseline agrupa (Cobrança e Assinatura; Fiscal entra como módulo em `CAP-016`), em fatia mínima: pagamento único à vista, camada anticorrupção do Stripe, webhook e fatos. Serviço, banco, esteira e implantação são Fase 0 conduzida fora do fluxo | Módulo `Billing` dentro de `commerce` — contraria a tabela do baseline e moveria webhook e dados já em produção na extração de `CAP-013` | **Decidida em 2026-10-05** |
| C-02 | **Pedido do pagamento síncrono, resultado por fato.** `commerce` pede a `billing` por `PUT /payment-sessions/{orderId}` (um salto, BA08), idempotente por pedido; o desfecho volta só pelos fatos `cobranca.*`. O endereço do gateway nunca é guardado fora de `billing` nem passa pelo broker: a retomada pede de novo (`kind` checkout ou instruções) | `commerce` comandar `billing` por mensagem e o BFF buscar o endereço em `billing` (BFF falaria com dois donos para uma ação); guardar o endereço no pedido (credencial de pagamento em `commerce` e no broker) | **Aprovada em 2026-10-05** |
| C-03 | **O aluno chega a `commerce` pelo JWT de aluno** de audiência `commerce`, `scope orders:use`, sem `permissions` e sem `email` (ADR-0013 estendida); o `sub` é o comprador, e nenhuma rota aceita `studentId` do cliente. Ator interno é recusado pela política de aluno (RN-V01). **ADR própria na TechSpec** | Asserção do `bff-student` com `studentId` no corpo (o BFF passaria a ser confiado quanto a quem compra) | **Aprovada em 2026-10-05** |
| C-04 | **`commerce` é emissor de asserção em `billing`**, escopo `payment:request`, no mecanismo da ADR-0004 (precedente ADR-0010). `billing` toma a escola do `tenantId` da asserção. **ADR própria na TechSpec**, junto com C-03 e C-06 | Chave compartilhada entre serviços | **Aprovada em 2026-10-05** |
| C-05 | **Webhook só em `billing`**, `POST /webhooks/v1/stripe/events`, assinatura verificada, 200 só depois de gravar, idempotente por `id`, tipos não tratados 200 e ignorados. Traduzidos: página concluída paga → confirmado; página concluída com PIX/boleto → aguardando; pagamento assíncrono confirmado → confirmado; assíncrono vencido ou página vencida → não confirmado. **Reembolso e contestação registrados e ignorados** (DP-07) | Webhook passando pelo BFF (não há sessão; amplia superfície); reagir a reembolso agora (é `CAP-015`) | **Aprovada em 2026-10-05** |
| C-06 | **Comprovante pela conta.** O pedido de envio aceita `destinatarioConta` em vez de `destinatario` (exatamente um); Notificação resolve e-mail e nome em Identity (`getStudentContactInternal`, asserção nova de `notification`, escopo `student-contact:read`) na hora de entregar; conta desativada ou inexistente → `entrega-falhou` com `destinatario-indisponivel`; Identity fora → retém. O e-mail nunca entra em `commerce`, no outbox nem no broker. Fecha QA-01 do PRD sem nova exceção a RN-21. **ADR própria na TechSpec** | `commerce` buscar e-mail e nome em Identity e publicar como hoje (terceira exceção a RN-21, revisão do domain doc de Identidade) | **Decidida em 2026-10-05** |
| C-07 | **Vendas registra a concessão no pedido** consumindo `matricula.acesso-concedido.v1` (`origin purchase`), em fila própria, para *Ir para o curso* e para o detalhe do backoffice. É exibição: nada decide acesso pelo pedido (RN-V10) | Página de retorno consultar a decisão de acesso (rota de serviço, não do aluno); Vendas ler a tabela de Matrícula (fere a fronteira de módulo) | **Aprovada em 2026-10-05** |
| C-08 | **Clique anônimo depreciado, não removido**: `registerPurchaseIntent(Internal)` responde 202 `purchaseAvailability: available` sem contar (valor de enum que o contrato já mandava tolerar); a contagem histórica fica na oferta. Remoção quando nenhum cliente chamar | Remover já (quebra SPA antiga em cache durante a implantação); continuar contando (DP-06) | **Aprovada em 2026-10-05** |
| C-09 | **Backoffice com aluno resolvido em lote**: `bff-admin` chama `resolveStudentAccountsInternal` (até 50 ids, escopo `student-account:resolve`, sessão com `financeiro.ler`) por página e falha com 502 se Identity não responder. **Filtro por e-mail reaproveita `lookupStudentAccount`**, que exige `cortesia.conceder` — permissão que o financeiro já tem; se um dia existir papel com `financeiro.ler` sem `cortesia.conceder`, a lookup precisa aceitar as duas | Guardar nome e e-mail no pedido (réplica de dado pessoal); e-mail em query string (G23); lista sem aluno quando Identity falha | **Aprovada em 2026-10-05** |
| C-10 | **Expiração do pedido**: por `cobranca.pagamento-nao-confirmado.v1` com `reason expired`; e por rotina de Vendas para pedido sem página aberta 24 h depois de criado e para pedido cujo prazo conhecido venceu há mais de 24 h sem fato de `billing`. Confirmação tardia continua valendo (RN-V08) | Só a rotina de Vendas (duplicaria os prazos do gateway); só `billing` (pedido sem página nunca expiraria) | **Aprovada em 2026-10-05** |
| C-11 | **Valores e textos do pedido congelados**: preço, vigência, título do curso e nome da opção vão no pedido e nos fatos; a página de pagamento mostra "curso — opção" e o valor congelado; `billing` recusa divergência de termos para o mesmo pedido (`PAYMENT_TERMS_CONFLICT`) | Página de pagamento lendo a oferta atual (RN-O10) | **Aprovada em 2026-10-05** |
| C-12 | **Número do pedido** em texto de 6 ou mais dígitos, sequencial por escola ("000123"), nas telas, no backoffice e no comprovante; `orderId` continua sendo a identidade técnica | Expor o `orderId` ao aluno; prefixo com nome da escola (mono-tenant) | **Aprovada em 2026-10-05** |

## Evolução e compatibilidade

- **`bff-student` compra 1.0.0, `bff-admin` pedidos 1.0.0, `billing` 1.0.0 (HTTP, webhook, mensagens):** novos, sem versão anterior.
- **`commerce` HTTP 1.3.0 → 1.4.0:** só acrescenta operações e esquemas (`StudentUserToken`, novo emissor de JWT para `commerce`). `registerPurchaseIntentInternal` depreciada; `PurchaseIntentAccepted.purchaseAvailability` ganha `available`, valor que o contrato 1.1.0 já mandava tolerar. Catálogo, cortesias, decisão de acesso e `course-access` inalterados.
- **`bff-student` vitrine 1.0.0 → 1.1.0:** a mesma depreciação; a `student-spa` deixa de chamar a operação no mesmo deploy que liga o botão à compra.
- **`commerce` mensagens 1.1.0 → 1.2.0:** canais novos; `matricula.acesso-concedido.v1` sem mudança de forma, agora com `origin purchase` (valor já declarado). Consumidores atuais de `acesso-concedido`: nenhum fora de `commerce`.
- **`notification` 1.1.0 → 1.2.0:** mudança no **receptor**, aditiva: `destinatario` deixa de ser obrigatório quando há `destinatarioConta`; finalidade e modelo novos. Os exemplos da 1.1.0 (`confirmacao-de-conta`, `convite-interno`) foram revalidados contra o schema 1.2.0 e continuam válidos: `identity` não muda. Os fatos `mensagem-entregue` e `entrega-falhou` passam a poder carregar a finalidade nova; não há consumidor deles fora de `notification`.
- **Identity 1.4.0 → 1.5.0 e 1.1.0 → 1.2.0:** aditivas; exigem **emissores e escopos novos** (`notification` com `student-contact:read`; `bff-admin` com `student-account:resolve`) e a audiência `commerce` com `orders:use` na configuração do JWT de aluno.
- **Ordem de implantação obrigatória:** Identity (audiência `commerce`, as duas operações, emissor `notification`) → `notification` 1.2.0 (senão o comprovante pela conta é recusado como inválido) → `billing` (com o webhook cadastrado no gateway e o consumidor de `vendas.pedido-cancelado.v1`) → `commerce` (Vendas, filas de Matrícula e de Vendas, emissor em `billing`) → `bff-student` e `bff-admin` → `student-spa` e `admin-spa`, quando o botão *Comprar* passa a levar à compra.

## Validação e verificação

| Documento | Comando e ferramenta | Padrão/ruleset | Resultado |
|---|---|---|---|
| Os 8 OpenAPI | `npx --yes @stoplight/spectral-cli lint <arquivos> --ruleset contracts/.spectral-exception.yaml -F hint` (Spectral 6.17.0) | OpenAPI 3.1.0; ruleset vigente do repositório (padrão da skill + a exceção de entrega HLS de `CAP-007`, que não se aplica aqui) | Sem erros nem avisos; exemplos de requisição e resposta validados contra os schemas (`oas3-valid-media-example`) |
| [asyncapi-contract.yaml](asyncapi-contract.yaml), [-billing](asyncapi-contract-billing.yaml), [-notification](asyncapi-contract-notification.yaml) | `npx --yes @asyncapi/cli@6.1.0 validate <arquivo>` | AsyncAPI 3.0.0 | Válidos, referências entre arquivos resolvidas (inclusive para `contracts/commerce/asyncapi.yaml`); uma informação recomenda 3.1.0, mantida a 3.0.0 dos contratos vigentes |
| Exemplos de mensagem | `jsonschema` 4.26.0 (Draft 7, com formato), script local que resolve os `$ref` | Schemas dos próprios documentos | 10 exemplos conformes. Recusados, como esperado: os dois destinatários, nenhum destinatário, comprovante por e-mail, comprovante sem vigência, meio desconhecido, conta interna, confirmação sem nome, compra concluída com e-mail, 0 meses e meio no vocabulário do gateway. Exemplos 1.1.0 de Identity válidos em 1.2.0 |

Revisão de compatibilidade e lint não comprovam comportamento. A implementação deve verificar:

- **Congelamento:** pedido criado com R$ 497 e 12 meses; oferta alterada para R$ 597 e 6 meses → pedido, página de pagamento, fato e concessão com R$ 497 e 12 meses; oferta despublicada → pedido pendente ainda pagável.
- **Um pendente por oferta:** segundo `createOrder` na mesma oferta → 200 com o mesmo pedido e nenhum `pedido-criado`; em outra oferta do mesmo curso → 201; mesma `Idempotency-Key` → mesmo resultado; corpo diferente → `IDEMPOTENCY_KEY_REUSED`.
- **Pagamento:** `startOrderPayment` repetido → mesmo endereço, um só pagamento no gateway; depois de gerar boleto → `boleto-instructions`; pedido pago, expirado ou cancelado → `ORDER_NOT_PAYABLE` sem chamar `billing`; `billing` fora → 503 e pedido inalterado.
- **Webhook:** assinatura inválida ou ausente → 400 e nada muda; o mesmo evento três vezes → um `pagamento-confirmado`, um pedido `paid`, uma `compra-concluida`, uma concessão, um comprovante; reembolso e contestação → 200 e nenhum fato.
- **Concessão:** cartão confirmado em 05/10/2026 → concessão `purchase`, `originRef` = pedido, término 05/10/2027 (fim do dia, regra de `CAP-008`); boleto gerado 05/10 e pago 08/10 → concessão conta de 08/10; vitalícia → sem término; `compra-concluida` repetida → nenhuma segunda concessão; Matrícula parada → concessão nasce quando ela volta.
- **Pagamento tardio:** pedido `cancelled` ou `expired` cujo gateway confirma → `paid` e concede com a vigência congelada (RN-V08).
- **Expiração:** página vencida sem meio → `pagamento-nao-confirmado` `expired` → pedido `expired` e `pedido-expirado`; pedido sem página aberta 24 h depois → expirado pela rotina; PIX não pago em 24 h e boleto em 3 dias → expirados.
- **Desistência:** `cancelMyOrder` → `cancelled`, `pedido-cancelado`, `billing` encerra a página/cancela o pendente; repetir → 200 sem novo fato; pago ou expirado → `ORDER_NOT_CANCELLABLE`.
- **Comprovante:** pedido de envio com `destinatarioConta` e sem `destinatario`; Notificação resolve e-mail e nome em Identity e entrega uma vez por pedido; conta desativada → `entrega-falhou` `destinatario-indisponivel`; Identity fora → retido e entregue depois; **e-mail do aluno ausente** de toda tabela, outbox e mensagem de `commerce` e `billing`.
- **Segurança:** token de ator em rota de aluno de `commerce` → 403; pedido de outro aluno → 404 nas rotas de aluno; financeiro sem `financeiro.ler` → 403 no BFF e em `commerce`; outra escola nunca aparece; `paymentUrl` ausente de log, span e métrica de BFF, `commerce` e `billing`; nenhum dado de cartão em lugar nenhum.
- **Página de retorno:** pedido pago com `accessGrantedAt` em até 10 s na maior parte dos casos; p95 de pagamento confirmado → concessão ≤ 60 s (métrica do PRD).
- **Backoffice:** filtros por situação, curso, aluno e período combinados; nome e e-mail resolvidos por página; Identity fora → 502.
- **Depreciação:** `registerPurchaseIntent` → 202 `available` e contagem inalterada.

## Pendências e handoff

1. **C-01 a C-12 aprovadas em 2026-10-05.**
2. **ADRs na TechSpec:** JWT de aluno para `commerce` (C-03, estende ADR-0013); `commerce` como emissor em `billing` (C-04); `notification` como emissor em Identity (C-06). Os três estendem ADR-0004 e seguem o precedente da ADR-0010.
3. **OD80 (dono: tasso):** PIX confirmado no Stripe em 2026-10-05; falta confirmar boleto habilitado na conta, antes da implementação. Os contratos não mudam se um meio cair: `PaymentMethod` só deixa de ter aquele valor em uso.
4. **Fase 0 de `billing`, fora do fluxo:** serviço, banco, esteira, `docker-compose.yml` com o override em `docker-compose.remote.yml`, implantação e endereço público do webhook, com segredo por escola. É pré-requisito de implementação, não deste contrato.
5. **Para a TechSpec:** modelo do pedido e da numeração por escola no módulo Vendas; transação única de pedido pago, compra concluída e pedido de envio; filas próprias de Matrícula e de Vendas; rotina de expiração de Vendas (C-10); tradução de eventos do Stripe e mínimos por meio no `billing`; derivação estável de `pedidoId` do comprovante; consulta da página de retorno.

Para `tsg-flow-techspec-creator`: usar este índice e os documentos acima, sem duplicar schemas.
