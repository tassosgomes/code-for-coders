---
tsg_artifact: tasks
product: code-4-coders
capability: CAP-011
version: 1.0
status: approved
updated: 2026-10-05
sources: tasks/prd-compra-avulsa/prd.md@1.0, tasks/prd-compra-avulsa/techspec.md@1.0
---

# Plano de Implementação — Compra avulsa de curso (CAP-011)

> **TechSpec de origem:** [techspec.md](techspec.md) 1.0, aprovada em 2026-10-05 (OD84)
> **Escopo:** Full-stack — `billing` (novo), `commerce` (Vendas e Matrícula), `notification`, `identity`, `bff-student`, `bff-admin`, `student-spa`, `admin-spa`
> **ADRs pertinentes:** [0016](../../docs/adr/0016-jwt-de-aluno-em-commerce.md), [0017](../../docs/adr/0017-autenticacao-de-servico-commerce-em-billing.md), [0018](../../docs/adr/0018-notificacao-resolve-destinatario-pela-conta.md); herdadas [0004](../../docs/adr/0004-autenticacao-de-servico-bff-identity.md), [0005](../../docs/adr/0005-sessao-e-servico-do-backoffice.md), [0010](../../docs/adr/0010-autenticacao-de-servico-commerce-em-identity.md), [0013](../../docs/adr/0013-jwt-de-aluno-validado-por-servicos-de-dominio.md)
> **Contratos:** [contracts.md](contracts.md) 1.0 (aprovado, OD83; errata D-11) — 8 OpenAPI e 3 AsyncAPI deste diretório
> **Design:** novo documento `docs/design/wireframes-compra.md` (a produzir em 1.0 e 2.0)
> **Status do plano:** Confirmado para implementação (aprovado pelo responsável em 2026-10-05)

## Visão Geral

Ao final, o aluno compra um curso sozinho: clica em *Comprar*, entra ou cria conta e volta à opção escolhida, vê o resumo com preço e vigência, paga na página do Stripe com cartão, PIX ou boleto e, quando o pagamento se confirma, o acesso é liberado sem ninguém da escola intervir. Ele recebe o comprovante por e-mail, acompanha os pedidos, retoma um PIX ou boleto pendente ou desiste e paga de outro jeito. O financeiro consulta os pedidos no backoffice. `billing` nasce como serviço próprio.

O design (ASCII → Figma → aprovação) vem antes de qualquer código de tela. A unidade `billing` (3.0) não tem tela e corre em paralelo ao design.

## Fases

### Fase 0 — Design aprovado e `billing` de pé

1.0 wireframes ASCII e 2.0 Figma; em paralelo, 3.0 a unidade `billing` no golden path. Checkpoint: cabeçalho de `docs/design/wireframes-compra.md` com `ASCII e Figma aprovados`; `billing` saudável em `http://localhost:5110/health/ready` com `scripts/apps.sh start --remote`.

### Fase 1 — Comprar e ter acesso

4.0 pedido com a condição congelada; 5.0 pagamento com cartão até o acesso liberado. Checkpoint: em `http://localhost:8082/student/cursos/{courseId}`, *Comprar* → resumo → pagamento no sandbox do Stripe com cartão de teste → "Compra confirmada" → *Ir para o curso* reproduz a aula.

### Fase 2 — PIX, boleto, expiração e desistência

6.0 pendentes e retomada; 7.0 expiração, desistência e pagamento tardio. Checkpoint: PIX de teste gerado, retomado pela página do pedido e pago → acesso; PIX expirado por `stripe trigger` → pedido expirado.

### Fase 3 — Comprovante, pedidos e backoffice

8.0 comprovante pela conta; 9.0 *Meus pedidos*; 10.0 pedidos no backoffice. Checkpoint: e-mail no smtp4dev com o link `http://localhost:8082/student/pedidos/{orderId}`; *Meus pedidos* com os três estados; `http://localhost:8081/admin/financeiro` filtrando por aluno.

## Mapa de Entrega

| Fatia | Task | Comportamento observável | Gate | Bloqueado por |
|---|---|---|---|---|
| EN-02 (ASCII) | 1.0 | Wireframes ASCII das telas de compra, pedido e backoffice aprovados | `rg` do status em `wireframes-compra.md` | Nenhum |
| EN-02 (Figma) | 2.0 | Figma aprovado e registrado | `rg` de `ASCII e Figma aprovados` | 1.0 |
| EN-01 | 3.0 | `billing` compila, sobe saudável e entra na esteira e nos três composes | build + `PlatformHeartbeatEndpointTests` de `billing` + `docker compose config` | Nenhum |
| V-01 | 4.0 | O aluno escolhe uma opção e ganha um pedido com a condição congelada | Commerce `OrderCreationTests` + `PurchaseSummaryTests` + Identity `CommerceAudienceTests` + BFF `OrderProxyTests` + SPA `student-purchase-summary` | 2.0 |
| V-02 | 5.0 | O aluno paga com cartão e o acesso é liberado sozinho | Billing `PaymentSessionTests` + `GatewayWebhookTests` + `StripeGatewayAdapterTests` + Commerce `OrderPaymentTests` + `PurchaseGrantTests` + `BillingClientTests` + BFF `OrderPaymentProxyTests` + SPA `order-return` | 3.0, 4.0 |
| V-03 | 6.0 | PIX ou boleto pendente, retomada e acesso quando compensa | Billing `PendingPaymentTests` + Commerce `PendingPaymentOrderTests` + SPA `order-pending-payment` | 5.0 |
| V-04 | 7.0 | Pedido expira; o aluno desiste e paga de outro jeito; pagamento tardio concede | Commerce `OrderExpirationTests` + `OrderCancellationTests` + Billing `PaymentCancellationTests` + SPA `order-cancel` | 6.0 |
| V-05 | 8.0 | O aluno recebe o comprovante por e-mail, endereçado à conta | Commerce `PurchaseReceiptRequestTests` + Notification `PurchaseReceiptDeliveryTests` + Identity `StudentContactTests` | 7.0 |
| V-06 | 9.0 | O aluno vê os próprios pedidos | Commerce `StudentOrderListTests` + BFF `MyOrdersProxyTests` + SPA `my-orders` | 8.0 |
| V-07 | 10.0 | O financeiro consulta os pedidos no backoffice | Commerce `FinanceOrdersTests` + Identity `StudentAccountResolutionTests` + BFF admin `FinanceOrdersProxyTests` + SPA admin `finance-orders` | 9.0 |

EN-02 vira duas tasks (ASCII e Figma) porque cada aprovação é decisão separada do responsável e o Figma parte do ASCII aprovado.

### Habilitadores

| Enabler | Task | Por que não cabe numa fatia | Desbloqueia |
|---|---|---|---|
| EN-02 (ASCII) | 1.0 | O desenho é decidido pelo responsável antes do código e vale para todas as fatias com tela | 4.0 a 7.0, 9.0, 10.0 (tela) |
| EN-02 (Figma) | 2.0 | A aprovação do Figma vale para todos os estados de uma vez | idem |
| EN-01 | 3.0 | Um serviço novo precisa existir, subir saudável, ter banco, esteira e implantação antes de qualquer comportamento de pagamento; nada disso é observável pelo aluno (D-09) | 5.0 |

Não há outro habilitador técnico: o schema e as filas de Vendas nascem em 4.0 e 5.0 com o comportamento que os usa; a concessão por compra nasce em 5.0; a forma 1.0.0 do pedido de envio pela conta nasce em 8.0 (migrations sempre pela ferramenta do EF).

## Tasks

- [x] 1.0 Wireframes ASCII da compra, do pedido e dos pedidos no backoffice aprovados
- [x] 2.0 Figma da compra, do pedido e dos pedidos no backoffice aprovado
- [x] 3.0 A unidade `billing` compila, sobe saudável e está na esteira e nos ambientes
- [x] 4.0 O aluno escolhe uma opção e ganha um pedido com preço e vigência congelados
- [x] 5.0 O aluno paga com cartão e o acesso ao curso é liberado sozinho
- [x] 6.0 O aluno gera PIX ou boleto, volta depois e o acesso sai quando o pagamento compensa
- [x] 7.0 O pedido não pago expira, o aluno pode desistir, e o pagamento tardio ainda concede
- [ ] 8.0 O aluno recebe o comprovante por e-mail, endereçado à conta
- [ ] 9.0 O aluno vê os próprios pedidos
- [ ] 10.0 O financeiro consulta os pedidos da escola no backoffice

## Caminho crítico e lanes

`1.0 → 2.0 → 4.0 → 5.0 → 6.0 → 7.0 → 8.0 → 9.0 → 10.0`, com `3.0` em paralelo ao design (não toca tela, nem `commerce`, nem os arquivos de 1.0/2.0) e pré-requisito de 5.0.

De 4.0 em diante **não há lane paralela segura**: todas alteram o agregado Pedido, `CommerceDbContext.cs`, a topologia RabbitMQ e o mapeamento de rotas de `commerce` e do `bff-student`. 8.0 depende de 7.0 por alterar a mesma transição "pago" e o publicador; 9.0 e 10.0 dependem das anteriores por reaproveitarem as leituras de pedido e os estados que elas produzem, e para o checkpoint ter pedidos em todos os estados.

**Estado intermediário conhecido:** depois de 4.0, o pedido existe e a página do pedido mostra "Aguardando pagamento" sem botão de pagar; depois de 5.0, só o cartão leva a concessão até o fim (PIX/boleto geram pendência que ainda não aparece); entre 5.0 e 8.0 não há comprovante. O plano é integrado numa única entrega. Ordem de implantação: a de `contracts.md` (Identity → `notification` 1.2.0 → `billing` → `commerce` → BFFs → SPAs). Sem dado legado (até o MVP).

## Integridade dos gates

- .NET: `dotnet test --project <csproj> -- --filter-class <classe> --minimum-expected-tests N` (Microsoft.Testing.Platform: exit 8 se nada rodar, 9 se rodar menos que N). Toda classe nomeada num gate é criada na própria task; nenhuma coincide com classe existente. As preexistentes que mudam de comportamento (`PurchaseIntentTests`, `PurchaseIntentRateLimitTests`, `AcceptAndDeliverAccountConfirmationTests`, `RejectInvalidSendRequestTests`, `StudentAccountLookupTests`, `CourtesyGrantTests`) aparecem só em Verificações do projeto.
- SPA: `npm --prefix src/<spa> run test -- <fragmento de caminho>`. O filtro de caminho sai com 1 quando nenhum arquivo casa; não usar `-t`. Fragmentos: `student-purchase-summary` (4.0), `order-return` (5.0), `order-pending-payment` (6.0), `order-cancel` (7.0), `my-orders` (9.0), `finance-orders` (10.0, `admin-spa`); nenhum casa arquivo existente nem é prefixo de outro. `student-purchase-intent.test.tsx` e `finance-area-route.test.tsx` são atualizados e rodam na suíte inteira.
- Design (1.0, 2.0): verificação estática da linha `> **Status:**` do cabeçalho de `docs/design/wireframes-compra.md`.
- Gateway: nenhum teste de gate chama o Stripe real. A porta do gateway é dublada nos testes de caso de uso; o adaptador real é testado contra um servidor HTTP local que responde como a API do Stripe (regra 11). O sandbox do Stripe é só do checkpoint manual.
- Testcontainers (PostgreSQL, RabbitMQ) rodam **em sequência**; os comandos do `gate` são encadeados e não paralelos. Testes de integração **sem exportador OTLP** (memória do projeto: 5–10 s por teste quando há exportador sem coletor).

## Artefatos compartilhados

| Artefato | Produzido em | Evolui em |
|---|---|---|
| `docs/design/wireframes-compra.md` (ASCII + frames Figma) | 1.0, 2.0 | referência de 4.0 a 10.0 |
| Unidade `billing` (solução, imagem, workflow, composes, banco, chaves) | 3.0 | 5.0 a 7.0 |
| Audiência `commerce` no JWT de aluno e rotas `/api/v1/offers`, `/api/v1/orders` no BFF | 4.0 | 5.0, 7.0, 9.0 |
| Agregado Pedido, schema `sales`, recibo de idempotência, contador de número | 4.0 | 5.0 a 10.0 |
| Exchange `billing.events`, filas `commerce.sales-payments`, `commerce.entitlement-purchases`, `commerce.sales-access-granted` | 5.0 | 6.0, 7.0 |
| Fila `billing.order-cancellations` | 7.0 | — |
| Concessão por compra e índice único de origem em `entitlement` | 5.0 | — |
| Publicador de `commerce` com tabela chave → exchange | 8.0 | — |
| Pedido de envio pela conta em `notification` e `getStudentContactInternal` | 8.0 | — |
| Página do pedido no `student-spa` | 4.0 | 5.0, 6.0, 7.0 |

## Verificação herdada

| Componente | Fonte | Checks e limites | Estado da base | Resolução planejada |
|---|---|---|---|---|
| `src/commerce` | `.github/workflows/commerce.yml` → `tassosgomes/template-pipeline/.github/workflows/ci-dotnet.yml@v1` | restore; `dotnet format --verify-no-changes`; `dotnet test` (MTP) com cobertura ≥ 70%; `dotnet publish -c Release`; imagem | Não medido nesta sessão; `CAP-017` integrada em `main` (PR #153) com a full aprovada | Nenhuma falha herdada comprovada. Cobertura agregada medida na validação full |
| `src/billing` | `.github/workflows/billing.yml` (criado em 3.0, no mesmo formato de `commerce.yml`) | Idem, cobertura ≥ 70% | Inexistente | 3.0 cria; 5.0 a 7.0 sustentam a cobertura |
| `src/notification` | `.github/workflows/notification.yml` → `ci-dotnet.yml@v1` | Idem, cobertura ≥ 70% | Não medido | Nenhuma |
| `src/identity` | `.github/workflows/identity.yml` → `ci-dotnet.yml@v1` | Idem, cobertura ≥ 70% | Não medido | Nenhuma |
| `src/bff-student`, `src/bff-admin` | `.github/workflows/bff-student.yml`, `bff-admin.yml` → `ci-dotnet.yml@v1` | Idem, cobertura ≥ 70% | Não medido | Nenhuma |
| `src/student-spa` | `.github/workflows/student-spa.yml` → `ci-react-ts.yml@v1` (`--base=/student/`, cobertura 70) | lint; typecheck; `vitest run --coverage` ≥ 70%; `vite build --base=/student/`; imagem | Não medido | Nenhuma |
| `src/admin-spa` | `.github/workflows/admin-spa.yml` → `ci-react-ts.yml@v1` (`--base=/admin/`, cobertura 70) | Idem | Não medido | Nenhuma |
| Compose | `docker-compose.yml`, `docker-compose.remote.yml`, `docker-compose.coolify.yml`; AGENTS.md | `docker compose config -q` nos três arranjos; toda dependência nova replicada no override remoto | Não medido | `billing` (3.0), audiências e emissores (4.0, 5.0, 8.0, 10.0) |
| Contratos | `contracts/README.md` | Spectral e `@asyncapi/cli` sobre os recortes do PRD | Validados em 2026-10-05 (contracts.md) | Promoção para `contracts/` só na conclusão do PRD |

## Cobertura

| Requisito | Task(s) |
|---|---|
| RF-01 | 1.0, 2.0, 4.0 |
| RF-02 | 1.0, 2.0, 4.0 |
| RF-03 | 4.0 |
| RF-04 | 5.0, 6.0 |
| RF-05 | 1.0, 2.0, 5.0, 6.0 |
| RF-06 | 1.0, 2.0, 6.0, 7.0 |
| RF-07 | 5.0 |
| RF-08 | 7.0 |
| RF-09 | 8.0 |
| RF-10 | 1.0, 2.0, 9.0 |
| RF-11 | 1.0, 2.0, 10.0 |
| RN-V01 | 4.0 |
| RN-V02 | 4.0 |
| RN-V03 | 4.0, 9.0 |
| RN-V04 | 4.0 |
| RN-V05 | 5.0, 7.0 |
| RN-V06 | 4.0 |
| RN-V07 | 7.0 |
| RN-V08 | 7.0 |
| RN-V09 | 5.0, 8.0 |
| RN-V10 | 5.0 |
| RN-V11 | 4.0 |
| RN-V12 | 9.0, 10.0 |
| RN-CB01 | 3.0, 5.0 |
| RN-CB02 | 5.0, 6.0 |
| RN-CB03 | 6.0, 7.0 |
| RN-CB04 | 5.0 |
| RN-CB05 | 5.0 |
| RN-CB06 | 5.0 |
| US-01 | 4.0, 5.0, 6.0 |
| US-02 | 5.0 |
| US-03 | 6.0, 7.0 |
| US-04 | 8.0 |
| US-05 | 9.0 |
| US-06 | 4.0 |
| US-07 | 4.0 |
| US-08 | 10.0 |

Mapeamento das histórias do PRD, na ordem em que aparecem em "Histórias de Usuário": US-01 comprar por cartão, PIX ou boleto; US-02 ver o acesso liberado logo depois de pagar; US-03 voltar ao PIX/boleto ou desistir; US-04 comprovante por e-mail; US-05 ver meus pedidos; US-06 aviso de acesso já existente; US-07 visitante volta à compra depois de entrar; US-08 financeiro consulta pedidos.

Regras herdadas dos domain docs (RN-O05, RN-O09 a RN-O12, RN-D02 a RN-D04, RN-D07, RN-D10, RN-21, RN-26 a RN-28) entram pelas tasks que as aplicam: congelamento e oferta em 4.0; concessão em 5.0; mascaramento e comprovante em 8.0.

Sem task própria, por decisão: **observabilidade** está nas tasks que a medem (5.0 histograma pagamento → concessão e alerta de pedido pago sem concessão; 5.0 a 7.0 contadores do webhook; 8.0 entrega); **segurança** (política de aluno, asserções novas, dado pessoal fora de `commerce` e `billing`, link de pagamento e corpo do webhook não persistidos) está nos testes de 4.0, 5.0, 8.0 e 10.0; **migração de dados** não existe (sem dado legado até o MVP).

> Verificado por `python3 <skill-dir>/scripts/validate_plan.py tasks/prd-compra-avulsa/` antes do handoff.
