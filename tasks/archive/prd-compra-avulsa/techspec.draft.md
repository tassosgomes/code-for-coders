---
tsg_artifact: techspec
product: code-4-coders
capability: CAP-011
version: 1.0
status: in_review
updated: 2026-10-05
sources: tasks/prd-compra-avulsa/prd.md@1.0, tasks/prd-compra-avulsa/contracts.md@1.0, context/architecture-baseline.md@1.2, domains/matricula-e-direito-de-acesso/domain.md@1.0, domains/catalogo-e-oferta/domain.md@1.1, domains/identidade-e-acesso/domain.md@1.1
---

# Especificação Técnica — Compra avulsa de curso

> **Escopo:** Full-stack
> **Modo:** Pipeline + API-First
> **PRD de origem:** `tasks/prd-compra-avulsa/prd.md` (v1.0, OD79)
> **Contratos de integração:** [contracts.md](contracts.md) (aprovado, OD83) e os 11 documentos que ele indexa
> **Data:** 2026-10-05
> **Status:** Em Revisão
> **Handoff:** draft — não gerar Tasks

---

## Resumo Executivo

A compra atravessa sete unidades: `student-spa` e `bff-student` (resumo, pedido, retorno, *Meus pedidos*), `commerce` (Vendas nasce como módulo; Matrícula passa a conceder por compra), **`billing`, serviço novo** (Stripe atrás de camada anticorrupção, webhook, fatos de pagamento), `notification` (comprovante pela conta), `identity` (audiência `commerce`, contato para Notificação, resolução de alunos para o backoffice) e `bff-admin`/`admin-spa` (pedidos na área financeira).

Decisões principais:

- **Pedido em Vendas, pagamento em `billing`, acesso em Matrícula**, como o Domain Map separa. Vendas fala com Matrícula só por fato (`vendas.compra-concluida.v1`), e com `billing` por uma chamada síncrona (pedir o caminho de pagamento) e por fatos (resultado do pagamento).
- **O endereço de pagamento do gateway nunca é guardado**: `billing` o obtém do Stripe a cada pedido do aluno e ele atravessa `commerce` e o BFF só em memória até o navegador.
- **O corpo do webhook nunca é guardado**: `billing` grava só identificadores e os campos que traduz, porque o evento do Stripe traz nome, e-mail e, no boleto, CPF do pagador.
- **Um fato por desfecho, garantido no banco**: a concessão por compra tem índice único por origem; o pedido é uma máquina de estados que ignora transição repetida; o webhook tem inbox por `id` do evento.

**Trade-off primário:** nascer `billing` como serviço próprio custa uma unidade inteira de Fase 0 (projeto, banco, esteira, três composes, Coolify, endereço público) dentro do MVP, em troca de não migrar webhook e dados de pagamento já em produção quando `CAP-013` chegar (OD81). Também aceitamos duas chamadas síncronas na ida ao pagamento (`bff-student` → `commerce` → `billing` → Stripe) para não espalhar a credencial de pagamento por broker e banco.

---

## Arquitetura da Solução

```text
student-spa ──► bff-student ──► commerce ─────────────► billing ──► Stripe (Checkout hospedado)
   │  (cookie)     (JWT aluno       │ Vendas  ▲  sync PUT     │  ▲
   │                aud commerce)   │         │  (ADR-0017)   │  │ webhook assinado
   │                                │         └─ cobranca.* ──┘  └── Stripe → /webhooks/v1/stripe/events
   │                                ├─ vendas.compra-concluida ─► Matrícula ─► matricula.acesso-concedido ─► Vendas
   │                                └─ notificacao.envio-solicitado (pela conta) ─► notification ─► identity (contato)
admin-spa ──► bff-admin ──► commerce (financeiro.ler) + identity (nome e e-mail por página)
```

### URLs públicas

| Link | Local | Homologação | Quem cria |
|---|---|---|---|
| Página de retorno do gateway (concluiu) | `http://localhost:8082/student/pedidos/{orderId}?resultado=concluido` | `https://c4c-student.lab.tasso.dev.br/student/pedidos/{orderId}?resultado=concluido` | `commerce` (Vendas), de `StudentApp:PublicBaseUrl` + `/student/pedidos/{orderId}`, enviado a `billing` como `successUrl` |
| Página de retorno do gateway (saiu) | idem, `?resultado=saiu` | idem | idem, `cancelUrl` |
| Link do comprovante | `http://localhost:8082/student/pedidos/{orderId}` | `https://c4c-student.lab.tasso.dev.br/student/pedidos/{orderId}` | `commerce`, no `dados.link` do pedido de envio |
| *Ir para o curso* | `/student/aulas/{continueLessonId}` | idem | `student-spa`, com `continueLessonId` de `listMyCourses` (contrato vigente, sem mudança) |
| Webhook do Stripe | `http://localhost:5110/webhooks/v1/stripe/events`, alcançado pela Stripe CLI (`stripe listen --forward-to`) | `https://<domínio público de billing>/webhooks/v1/stripe/events` (Coolify; domínio a definir na Fase 0 de `billing`) | cadastrado no painel do Stripe por ambiente |

O link do comprovante leva ao pedido, e não direto ao curso: a página do pedido exige sessão (o login devolve para ela) e já resolve *Ir para o curso*, sem que `commerce` precise conhecer aulas.

### Bloco Backend

**`commerce` — módulo Vendas (schema `sales`, que já existe e hoje só tem o outbox).**

- **Pedido** é o agregado: aluno, curso e título, oferta e nome, preço, moeda, vigência, número, situação, meio, pagamento pendente (meio e prazo), prazo da página, referência do gateway, valor confirmado, concessão (id e momento) e os momentos de cada transição. A máquina de estados vive no agregado: `awaiting-payment` → `paid` | `expired` | `cancelled`; `expired`/`cancelled` → `paid` (RN-V08); `paid` é terminal; transição para o estado atual é ignorada sem efeito. Fatos e pedido de envio são gravados no outbox de `sales` na mesma transação da transição (G06).
- **Número do pedido** por escola: contador por tenant em tabela própria de `sales`, incrementado com bloqueio na transação de criação; formatado com 6 dígitos à esquerda.
- **Um pendente por aluno e oferta** (RN-V06): índice único parcial em (tenant, aluno, oferta) onde a situação é `awaiting-payment`; a criação concorrente perde no índice e devolve o pedido existente.
- **Idempotência da criação**: recibo por (tenant, aluno, hash da `Idempotency-Key`) com o hash do corpo e a resposta, janela de 24 h — o mesmo padrão do `GrantReceipt` da cortesia.
- **Leituras de outros módulos** (D-02): a oferta vigente vem da porta de leitura do Catálogo (caso de uso, não tabela); o acesso existente vem da porta de leitura de Matrícula. Nenhuma tabela de `catalog` ou `entitlement` é lida por Vendas.
- **Rotina de expiração de Vendas** (C-10): mesmo formato do `AccessExpirationWorker` — ciclo periódico, lote, idempotente.
- **Cliente de `billing`**: asserção de serviço de `commerce` com `aud billing` (ADR-0017), tempo-limite curto, sem repetição automática (a repetição é do aluno, e é segura porque `billing` é idempotente por pedido).

**`commerce` — módulo Matrícula (schema `entitlement`).**

- **Concessão por compra**: nova fábrica no agregado `AccessGrant` para origem `purchase`, com `originRef` = pedido, sem motivo e sem autor interno; vigência da mensagem, contada do momento da concessão, pela mesma função de término da cortesia (`AccessTerm`).
- **RN-D10 no banco**: índice único parcial em (tenant, origem, `origin_ref`) onde `origin_ref` não é nulo. A mensagem repetida perde no índice e é confirmada sem efeito nem novo fato.
- **Consumidor** de `vendas.compra-concluida.v1` em fila própria, no padrão do `EntitlementCourseConsumerWorker`.

**`billing` — serviço novo, no golden path dos demais serviços .NET** (`docs/foundation.md`; skill `dotnet`).

- **Pagamento** é o agregado, um por pedido (índice único por tenant e pedido): valor, moeda, descrição, aluno, sessão do Checkout, PaymentIntent, meio, situação (`open` | `awaiting` | `confirmed` | `not-confirmed`), motivo, prazos e momentos.
- **Camada anticorrupção do Stripe**: uma porta de gateway com três operações (abrir página, obter caminho atual de um pagamento, cancelar) e um tradutor de eventos. Nada do Stripe aparece fora dela: nem tipos, nem estados, nem nomes de evento.
- **Abrir página** (D-05): Checkout hospedado, modo pagamento, um item com a descrição e o valor congelados, meios cartão, PIX e boleto, `client_reference_id` = pedido, metadados com tenant e pedido, `expires_at` de 24 h, prazo do PIX de 24 h e do boleto de 3 dias, chave de idempotência do Stripe derivada do pedido (não abre duas páginas para o mesmo pedido em repetição de rede).
- **Webhook**: valida a assinatura com o segredo do endpoint; grava na **inbox** só o `id`, o tipo, o momento, os identificadores do objeto (sessão, PaymentIntent, `client_reference_id`, metadados) e os campos traduzíveis (situação do pagamento, meio, valor, prazo); responde 200 depois de gravar. Um processador lê a inbox, aplica ao Pagamento e grava o fato no outbox na mesma transação.
- **Tradução** (C-05): `checkout.session.completed` pago → confirmado; `checkout.session.completed` não pago (PIX/boleto) → aguardando; `checkout.session.async_payment_succeeded` → confirmado; `checkout.session.async_payment_failed` → não confirmado (`expired`); `checkout.session.expired` sem meio escolhido → não confirmado (`expired`). Reembolso e contestação: registrados na inbox e ignorados.
- **Caminho atual** (`ensurePaymentSessionInternal`): página aberta e válida → endereço da sessão; PIX/boleto pendente → endereço das instruções obtido do PaymentIntent no momento da chamada; nada disso é persistido.
- **Desistência**: consumidor de `vendas.pedido-cancelado.v1` encerra a sessão aberta ou cancela o pagamento pendente no gateway e grava `not-confirmed`/`cancelled` com o fato.

**`notification`.**

- Aceita `destinatarioConta` (C-06): o Registro de Entrega ganha a conta de destino e os dados estruturados do modelo; o e-mail e o nome passam a poder ser preenchidos **na entrega**, pela resolução em Identity (ADR-0018).
- Novo modelo `comprovante-de-compra` no renderizador, com o texto do PRD (RF-09) e a declaração de que não é documento fiscal.
- Entrega pela conta: resolve contato → conta indisponível falha com `destinatario-indisponivel`; Identity fora conta como tentativa transitória, no mesmo limite da falha do provedor.

**`identity`.**

- Audiência `commerce` com `orders:use` no JWT de aluno (ADR-0016): configuração, sem código novo além do teste.
- `resolveStudentAccountsInternal` (emissor `bff-admin`, escopo novo, sessão com `financeiro.ler`) e `getStudentContactInternal` (emissor novo `notification`, ADR-0018), no padrão de `StudentAccountLookupEndpoints`.

**Transação e consistência.** Todas as mudanças de estado de pedido e pagamento gravam o fato no outbox na mesma transação. Consumidores são idempotentes pela chave de negócio (pedido, origem da concessão, `id` do evento do gateway), não por tabela de inbox genérica, exceto o webhook, que precisa de inbox porque o efeito acontece depois da resposta.

### Bloco Frontend

**`student-spa`** (rotas sob `/student`):

| Tela | Rota | Estado |
|---|---|---|
| Resumo da compra | `/comprar/:offerId` | servidor: `getPurchaseSummary`; cliente: nada |
| Pedido (retorno, retomada, *Ir para o curso*) | `/pedidos/:orderId` | servidor: `getMyOrder`, consultado de novo a cada 3 s enquanto "Confirmando" ou "Liberando seu acesso", até 2 min; depois, aviso de demora e consulta a cada 15 s |
| Meus pedidos | `/pedidos` | servidor: `listMyOrders` |

- O botão *Comprar* (`purchase-intent-button`) deixa de abrir o aviso e de chamar `registerPurchaseIntent`: leva a `/comprar/:offerId`.
- **Compra pendente de login** (D-10): visitante que clica em *Comprar* recebe 401 no resumo; o cliente guarda `{offerId, courseId, guardadoEm}` no `localStorage` (vale 24 h) e leva ao login com `returnTo=/comprar/:offerId`. O cadastro não carrega `returnTo` até o e-mail de confirmação — por isso o `localStorage`: depois de confirmar e entrar (mesmo em outra aba), o login, sem `returnTo`, consulta a compra pendente e leva ao resumo; a compra pendente é apagada ao abrir o resumo ou ao vencer.
- Ir ao pagamento: `startOrderPayment` → `window.location.assign(paymentUrl)`; o endereço não é guardado em estado, cache de consulta nem armazenamento.
- *Ir para o curso*: `listMyCourses` → `continueLessonId` do curso → `/aulas/:lessonId`. Se o curso ainda não aparecer (concessão não lida), a página mantém "Liberando seu acesso".

**`admin-spa`**: a área `/financeiro` deixa de ser a tela reservada e passa a ter a lista de pedidos com filtros e o detalhe `/financeiro/pedidos/:orderId`. O filtro por e-mail usa a busca de aluno que a área de cortesias já usa (C-09).

Todas as telas novas passam pelo fluxo de design do projeto antes do código (EN-02).

---

## Mapa de Fatias Verticais

### V-01: o aluno escolhe uma opção e ganha um pedido com a condição congelada

- **Cobre:** RF-01, RF-02, RF-03; RN-V01, RN-V02, RN-V03, RN-V04, RN-V06, RN-V11; DP-06, DP-08; C-03, C-08, C-11, C-12; ADR-0016.
- **Entrada / gatilho:** clique em *Comprar* numa oferta; visitante entra ou cria conta e volta; aluno confirma o resumo.
- **Processamento:**
  - Identity emite JWT de aluno `aud commerce`, `scope orders:use`; o `bff-student` mapeia `/api/v1/offers` e `/api/v1/orders` para `commerce`.
  - `getPurchaseSummaryInternal`: lê a oferta publicada pela porta do Catálogo; o acesso existente pela porta de Matrícula (falha → `existingAccessChecked: false`); o pedido pendente do aluno na oferta.
  - `createOrderInternal`: valida a política de aluno; recibo de idempotência; lê a oferta vigente; congela curso e título, oferta e nome, preço e vigência; número do pedido; pendente existente → 200 com ele; grava pedido + `vendas.pedido-criado.v1`.
  - `registerPurchaseIntent(Internal)` passa a responder `available` sem contar.
  - `student-spa`: botão *Comprar* → resumo; compra pendente de login; confirmar → `createOrder` → `/pedidos/:orderId` mostrando o pedido aguardando pagamento (o botão de pagar entra em V-02).
- **Saída observável:** resumo com preço e vigência; aviso de cortesia; pedido `awaiting-payment` com número; fato `pedido-criado` conforme o AsyncAPI.
- **Evidência / checkpoint:** testes de integração de `commerce` (congelamento com oferta alterada depois; pendente por oferta; concorrência de duas criações; token de ator → 403; oferta despublicada → 404; replay de `Idempotency-Key`); teste de contrato do fato `pedido-criado`; testes de rota do `student-spa` (visitante → login → resumo; cadastro + confirmação em outra aba → resumo); E2E local: oferta de 12 meses → pedido com R$ 497 e 12 meses.
- **Bloqueado por:** EN-02.

### V-02: o aluno paga com cartão e o acesso é liberado sozinho

- **Cobre:** RF-04 (cartão), RF-05, RF-07; RN-CB01, RN-CB02, RN-CB04, RN-CB05, RN-CB06, RN-V05, RN-V09, RN-V10; RN-D02, RN-D03, RN-D04, RN-D10; C-02, C-04, C-05, C-07; ADR-0017.
- **Entrada / gatilho:** *Ir para o pagamento* no pedido; pagamento aprovado no Checkout; evento do Stripe.
- **Processamento:**
  - `startOrderPaymentInternal` → `ensurePaymentSessionInternal` com valor, moeda e descrição congelados e as URLs de volta; `billing` abre a página (D-05) ou devolve a aberta; `commerce` grava `paymentPageExpiresAt`.
  - Webhook → inbox → processador traduz `checkout.session.completed` pago → `cobranca.pagamento-confirmado.v1`.
  - Vendas: pedido `paid`, `paidAt`, meio, valor confirmado, referência; `vendas.compra-concluida.v1` na mesma transação.
  - Matrícula: concessão `purchase`, `originRef` = pedido, vigência da mensagem, `matricula.acesso-concedido.v1`.
  - Vendas: consumidor de `acesso-concedido` (só `purchase`) grava `grantId` e `accessGrantedAt`.
  - `student-spa`: página de retorno com "Confirmando" → "Compra confirmada" → *Ir para o curso*.
- **Saída observável:** a aula do curso comprado reproduz; `decideAccessInternal` responde `allowed` com o término de 12 meses; o pedido mostra *Ir para o curso*.
- **Evidência / checkpoint:** integração de `billing` com o gateway substituído por dublê na porta (página única por pedido em repetição; termos divergentes → `PAYMENT_TERMS_CONFLICT`); webhook com assinatura inválida → 400 e nada muda; o mesmo evento três vezes → um fato, um pedido pago, uma concessão; Matrícula parada → concessão nasce ao voltar; testes de contrato dos três fatos; E2E no sandbox Stripe com cartão de teste: pedido → pago → aula reproduz, com p95 de confirmação → concessão medido.
- **Bloqueado por:** V-01, EN-01.

### V-03: o aluno gera PIX ou boleto, volta depois e o acesso sai quando compensa

- **Cobre:** RF-04 (PIX e boleto), RF-05 (aguardando boleto), RF-06 (retomar); RN-CB02, RN-CB03; RN-D04 (concessão conta da compensação); C-02.
- **Entrada / gatilho:** aluno escolhe PIX ou boleto no Checkout; volta mais tarde ao pedido; o PIX ou boleto é pago.
- **Processamento:** `checkout.session.completed` não pago → `cobranca.pagamento-aguardando.v1` → pedido com `paymentMethod` e `pendingPayment`; `ensurePaymentSessionInternal` devolve `pix-instructions`/`boleto-instructions` com o endereço do PaymentIntent obtido na hora; `checkout.session.async_payment_succeeded` → confirmado → mesmo caminho de V-02. Aguardando que chega depois do confirmado é ignorado.
- **Saída observável:** a página do pedido mostra "Aguardando pagamento do boleto", o vencimento e o acesso ao boleto; *Continuar pagamento* abre as instruções do mesmo pagamento; depois de pago, a concessão começa na data da compensação.
- **Evidência / checkpoint:** integração de `billing` (aguardando → instruções, sem nova página); integração de Vendas (aguardando depois de pago → sem efeito); E2E no sandbox com PIX de teste; boleto de teste no sandbox quando OD80 confirmar o meio.
- **Bloqueado por:** V-02.

### V-04: pedido não pago expira, e o aluno pode desistir e pagar de outro jeito

- **Cobre:** RF-06 (desistir), RF-08; RN-V05, RN-V07, RN-V08; RN-CB03; DP-03, DP-04, DP-05; C-10.
- **Entrada / gatilho:** página, PIX ou boleto vencidos; pedido sem página aberta há 24 h; *Desistir*; confirmação tardia de pedido expirado ou cancelado.
- **Processamento:**
  - `billing`: `checkout.session.async_payment_failed` e `checkout.session.expired` sem meio → `cobranca.pagamento-nao-confirmado.v1` (`expired`); consumidor de `vendas.pedido-cancelado.v1` encerra a sessão ou cancela o PaymentIntent e grava `not-confirmed`/`cancelled`; `ensurePaymentSessionInternal` responde `PAYMENT_EXPIRED`/`PAYMENT_CANCELLED`.
  - Vendas: `nao-confirmado` `expired` → pedido `expired` + `vendas.pedido-expirado.v1`; rotina de expiração (sem página há 24 h; prazo conhecido vencido há mais de 24 h sem fato); `cancelOrderInternal` → `cancelled` + `vendas.pedido-cancelado.v1`; confirmação tardia → `paid` e concede (RN-V08).
  - `student-spa`: *Desistir e pagar de outra forma* no pedido; estados "Expirado" e "Cancelado" com *Comprar de novo*.
- **Saída observável:** pedido expirado ou cancelado sem acesso; nova compra pelas condições vigentes; boleto pago depois de cancelado ainda libera o acesso.
- **Evidência / checkpoint:** integração de Vendas com relógio controlado (rotina nos dois casos); integração de `billing` (cancelar com gateway fora → mensagem volta para a fila); cenário RN-V08 com evento simulado; E2E no sandbox: PIX não pago + `stripe trigger` de expiração → pedido expirado.
- **Bloqueado por:** V-03.

### V-05: o aluno recebe o comprovante por e-mail

- **Cobre:** RF-09; RN-V09; RN-21, RN-26, RN-27, RN-28; C-06; ADR-0018.
- **Entrada / gatilho:** pedido vira `paid`.
- **Processamento:** Vendas grava o pedido de envio (`destinatarioConta`, modelo `comprovante-de-compra`, `pedidoId` derivado do pedido, `dados` congelados, link do pedido) no outbox de `sales`, na mesma transação da compra concluída, e o publica no exchange de Notificação. Notificação aceita pela conta, resolve o contato em Identity na entrega, renderiza o modelo novo e entrega; conta desativada → `entrega-falhou` `destinatario-indisponivel`; Identity fora → tentativa transitória.
- **Saída observável:** e-mail no smtp4dev com curso, opção, valor, meio, data, número, vigência e o link do pedido; um único e-mail em reprocessamentos.
- **Evidência / checkpoint:** teste de contrato do pedido de envio de `commerce` contra o AsyncAPI de Notificação 1.2.0 (`AssertSends`); testes de Notificação (pela conta, conta desativada, Identity fora, exemplos 1.1.0 continuam aceitos); varredura do e-mail de teste nas tabelas e no outbox de `commerce` e `billing` e na telemetria — **zero ocorrências**.
- **Bloqueado por:** V-02.

### V-06: o aluno vê os próprios pedidos

- **Cobre:** RF-10; RN-V03, RN-V12.
- **Entrada / gatilho:** aluno abre *Meus pedidos*.
- **Processamento:** `listMyOrders` → `listStudentOrdersInternal` paginado, mais recente primeiro; pendente leva à retomada; pago leva ao pedido.
- **Saída observável:** lista com situação, valor e o que foi congelado, inclusive de oferta despublicada; lista vazia com caminho para a vitrine.
- **Evidência / checkpoint:** integração de `commerce` (pedido de outro aluno → 404; ordem e paginação); teste de rota do `student-spa`.
- **Bloqueado por:** V-03.

### V-07: o financeiro consulta os pedidos no backoffice

- **Cobre:** RF-11; RN-16, RN-V12; DP-09; C-09.
- **Entrada / gatilho:** financeiro abre a área financeira e filtra.
- **Processamento:** `listFinanceOrdersInternal`/`getFinanceOrderInternal` com JWT do ator e `financeiro.ler`; `bff-admin` resolve nome e e-mail da página em `resolveStudentAccountsInternal` (sessão com `financeiro.ler`; Identity fora → 502); filtro por e-mail via `lookupStudentAccount` → `studentId`.
- **Saída observável:** lista filtrável e detalhe com vigência, momentos, referência do gateway e concessão.
- **Evidência / checkpoint:** integração de `commerce` (filtros combinados, período invertido → 400, professor/suporte/administrador → 403, outra escola nunca aparece); integração de Identity (`resolveStudentAccountsInternal`: omite desconhecidos, ≤ 50, sessão sem `financeiro.ler` → 403); testes de rota do `admin-spa`.
- **Bloqueado por:** V-02, EN-02.

### Habilitadores inevitáveis

| Habilitador | Por que não cabe numa fatia | Menor escopo | Primeira fatia desbloqueada |
|---|---|---|---|
| EN-01 — Unidade `billing` no golden path | Um serviço novo precisa existir, subir saudável e ter banco, esteira e implantação antes de qualquer comportamento de pagamento; nada disso é observável pelo aluno. É a "Fase 0" de `billing` (OD81), trazida para este plano (D-09) | Solução .NET nas camadas padrão com health e heartbeat; `src/billing/Dockerfile`; `.github/workflows/billing.yml`; serviço em `docker-compose.yml`, override em `docker-compose.remote.yml` e `docker-compose.coolify.yml`; banco e papel em `scripts/init-local-databases.sql` e `scripts/remote-infra.sh` (`provision`/`migrate`); `scripts/apps.sh` (porta 5110); chaves `commerce`→`billing` e `notification`→`identity` em `scripts/generate-local-env.sh`; segredos do Stripe como variáveis sem valor padrão | V-02 |
| EN-02 — Design das telas | Decisão de processo do projeto: telas só são construídas sobre wireframe ASCII e Figma aprovados | `docs/design/wireframes-compra.md` (resumo, pedido em todos os estados, *Meus pedidos*, área financeira com lista e detalhe), aprovado em ASCII e depois em Figma | V-01 |

---

## Contratos e Fronteiras

### Mapeamento de mensagens e dados

| Contrato e identificador | Aplicação; perspectiva | Comportamento a implementar | Evidência |
|---|---|---|---|
| `vendas.pedido-criado.v1` | `commerce` send | Outbox de `sales`, mesma transação da criação; nunca no replay nem no pendente existente | contrato + integração (V-01) |
| `vendas.compra-concluida.v1` | `commerce` send (Vendas) / receive (Matrícula, fila própria) | Um por pedido; Matrícula concede uma vez (índice único) | contrato + reentrega (V-02) |
| `matricula.acesso-concedido.v1` | `commerce` send (Matrícula) / receive (Vendas, fila própria) | `origin purchase`, `originRef` = pedido; Vendas ignora outras origens | contrato + integração (V-02) |
| `cobranca.pagamento-confirmado/aguardando/nao-confirmado.v1` | `billing` send / `commerce` receive | Tradução do webhook; ordem não garantida; transições repetidas sem efeito | contrato + integração (V-02 a V-04) |
| `vendas.pedido-cancelado.v1` | `commerce` send / `billing` receive | Cancelar no gateway; gateway fora → reentrega; esgotado → DLQ reprocessável | integração (V-04) |
| `vendas.pedido-expirado.v1` | `commerce` send | Um por pedido, pela rotina ou pelo fato de `billing` | contrato (V-04) |
| `notificacao.envio-solicitado.v1` (1.2.0) | `commerce` send / `notification` receive | Pela conta; `pedidoId` estável por pedido; Notificação resolve contato na entrega | contrato + varredura do e-mail (V-05) |

**Topologia RabbitMQ** (D-04): `billing` publica no exchange próprio `billing.events`; `commerce` escuta `billing.events` com a fila `commerce.sales-payments` (três chaves `cobranca.*`); `commerce.entitlement-purchases` e `commerce.sales-access-granted` escutam `commerce.events`; `billing.order-cancellations` escuta `commerce.events` (`vendas.pedido-cancelado.v1`). O pedido de envio vai para o exchange de Notificação (`notification.events.default`), como Identity já faz. Todas as filas são quorum, com DLX/DLQ e `x-delivery-limit` (G06). Como `commerce` hoje escolhe o exchange por uma condição fixa (ver Riscos), o publicador passa a escolher por tabela de chave → exchange.

**Percurso de credenciais e dados sensíveis:**

| Dado | Criado | Atravessa | Persistido | Nunca | Descarte |
|---|---|---|---|---|---|
| Dado de cartão, CPF, nome do pagador | Página do Stripe | — | só no Stripe | plataforma inteira | — |
| `paymentUrl` (endereço da página ou das instruções) | `billing`, a partir do Stripe, a cada chamada | `billing` → `commerce` → `bff-student` → navegador, em memória e TLS | **em lugar nenhum** | log, span, métrica, outbox, broker, banco, cache de consulta do SPA | fim da requisição |
| Corpo do evento do webhook | Stripe | rede → `billing` | **não**; a inbox guarda só ids, tipo, momento e campos traduzidos (sem `customer_details`) | log de requisição do webhook | — |
| Chave secreta do Stripe e segredo do webhook | Painel do Stripe | configuração de `billing` | segredo do ambiente (Coolify; `.env` local fora do git) | log, imagem, repositório | rotação no painel |
| E-mail e nome do aluno | Identity | Identity → Notificação (contato); Identity → `bff-admin` (resolução por página) | Registro de Entrega de Notificação (retenção já decidida, OD11); nenhum outro | `commerce`, `billing`, broker, log, span, URL, cache do BFF | purga do Registro de Entrega |
| Asserções de serviço e JWT de aluno | emissores | chamada única | não (só `jti` consumido) | log | expiração ≤ 60 s / 5 min |

### Mapeamento do contrato de API

| operationId | Caminho de implementação |
|---|---|
| `getPurchaseSummary` | `bff-student` (rota com audiência `commerce`) → `getPurchaseSummaryInternal` → caso de uso de Vendas → portas de Catálogo e Matrícula + leitura de pedido pendente |
| `createOrder` | `bff-student` → `createOrderInternal` → caso de uso de criação → agregado Pedido, recibo, contador, outbox |
| `listMyOrders`, `getMyOrder` | `bff-student` → `listStudentOrdersInternal`/`getStudentOrderInternal` → consultas de Vendas filtradas por `sub` |
| `startOrderPayment` | `bff-student` → `startOrderPaymentInternal` → caso de uso de ida ao pagamento → cliente de `billing` |
| `cancelMyOrder` | `bff-student` → `cancelOrderInternal` → agregado Pedido + outbox |
| `ensurePaymentSessionInternal` | `billing` → caso de uso → agregado Pagamento + porta do gateway |
| `receiveGatewayEvent` | `billing` → verificação de assinatura → inbox; processador → tradutor → agregado → outbox |
| `listFinanceOrders`, `getFinanceOrder` | `bff-admin` → `listFinanceOrdersInternal`/`getFinanceOrderInternal` + `resolveStudentAccountsInternal` |
| `resolveStudentAccountsInternal`, `getStudentContactInternal` | Identity, no padrão de `StudentAccountLookupEndpoints` |
| `registerPurchaseIntent(Internal)` | resposta fixa `available`, sem gravar contagem |

**Validações além do contrato:**

| operationId | Regra | Camada |
|---|---|---|
| `createOrderInternal` | Política de aluno (`orders:use`, sem `permissions`); oferta publicada da escola; congela da oferta vigente, nunca do cliente | aplicação + domínio |
| `startOrderPaymentInternal` | Só `awaiting-payment` chama `billing`; demais → `ORDER_NOT_PAYABLE` sem chamada | domínio |
| `ensurePaymentSessionInternal` | URLs de volta só em hosts da lista do ambiente; `https`, exceto `localhost` em desenvolvimento (D-11) | aplicação de `billing` |
| `receiveGatewayEvent` | Assinatura e tolerância de horário do segredo da escola; corpo sem `id`/`type` → 400 | API de `billing` |

**Exceção → resposta HTTP** (códigos do contrato):

| Situação | HTTP | `code` |
|---|---|---|
| Oferta não publicada/inexistente/de outra escola | 404 | `OFFER_NOT_AVAILABLE` |
| Pedido de outro aluno/escola ou inexistente | 404 | `ORDER_NOT_FOUND` |
| Mesma chave, corpo diferente | 422 | `IDEMPOTENCY_KEY_REUSED` |
| Pedido não pagável / prazo vencido em `billing` | 422 | `ORDER_NOT_PAYABLE` |
| Pedido pago ou expirado na desistência | 422 | `ORDER_NOT_CANCELLABLE` |
| `billing` ou Stripe fora | 503 | `PAYMENT_PROVIDER_UNAVAILABLE` (`commerce`), `GATEWAY_UNAVAILABLE` (`billing`) |
| Token de ator em rota de aluno | 403 | `SCOPE_DENIED` |

### Mapeamento de jornada

| História | Tela | operationId / ação | Evidência |
|---|---|---|---|
| Comprar sem depender da escola | Página do curso → resumo → pedido | `getPurchaseSummary`, `createOrder`, `startOrderPayment` | V-01, V-02 |
| Ver o acesso liberado logo depois de pagar | Pedido (retorno) | `getMyOrder` (consulta repetida), `listMyCourses` | V-02 |
| Voltar ao PIX/boleto ou desistir | Pedido | `startOrderPayment`, `cancelMyOrder` | V-03, V-04 |
| Receber comprovante | E-mail | — | V-05 |
| Ver meus pedidos | *Meus pedidos* | `listMyOrders` | V-06 |
| Ser avisado de acesso existente | Resumo | `getPurchaseSummary` | V-01 |
| Visitante volta à compra depois de entrar | Login/cadastro → resumo | compra pendente no `localStorage` | V-01 |
| Financeiro consulta pedidos | `/financeiro`, `/financeiro/pedidos/:orderId` | `listFinanceOrders`, `getFinanceOrder`, `lookupStudentAccount` | V-07 |

### Entidades do domínio

| Entidade | Representação técnica | Local |
|---|---|---|
| Pedido, Item do Pedido (Vendas) | Agregado Pedido com o item embutido (um por pedido, RN-V04); contador de número; recibo de idempotência | `commerce`, schema `sales` |
| Pagamento (Cobrança) | Agregado Pagamento; inbox de eventos do gateway | `billing`, banco próprio |
| Concessão de Acesso (Matrícula) | `AccessGrant` com origem `purchase` | `commerce`, schema `entitlement` |
| Registro de Entrega (Notificação) | `DeliveryRecord` com conta de destino e dados do modelo | `notification` |

---

## Arquivos a Modificar e a Referenciar

### A modificar

| Caminho | Fatia | Alteração |
|---|---|---|
| `src/commerce/src/CodeForCoders.Commerce.Domain/Entities/AccessGrant.cs` | V-02 | Fábrica de concessão por compra (sem motivo nem autor) |
| `src/commerce/src/CodeForCoders.Commerce.Infra.Data/Configurations/AccessGrantConfiguration.cs` | V-02 | Índice único parcial (tenant, origem, `origin_ref`) |
| `src/commerce/src/CodeForCoders.Commerce.Infra.Data/CommerceDbContext.cs` | V-01 | Entidades de Vendas com filtro de tenant |
| `src/commerce/src/CodeForCoders.Commerce.Infra.Messaging/RabbitMqPublisher.cs` | V-05 | Exchange por chave de roteamento (Notificação, Auditoria, próprio) |
| `src/commerce/src/CodeForCoders.Commerce.Infra.Messaging/RabbitMqTopologyInitializer.cs` | V-02 | Filas `commerce.sales-payments`, `commerce.entitlement-purchases`, `commerce.sales-access-granted` e DLQs |
| `src/commerce/src/CodeForCoders.Commerce.Infra.Messaging/Configuration/RabbitMqOptions.cs` | V-02 | Nomes de exchange de `billing` e de Notificação, filas novas |
| `src/commerce/src/CodeForCoders.Commerce.Api/Security/FinanceAreaJwtBearerOptionsSetup.cs` | V-01 | Desafio 401/403 com o `code` do contrato também nas rotas de aluno |
| `src/commerce/src/CodeForCoders.Commerce.Application/UseCases/Showcase/RegisterPurchaseIntent/RegisterPurchaseIntent.cs` | V-01 | Responder `available` sem contar |
| `src/commerce/src/CodeForCoders.Commerce.Api/appsettings.json` | V-01, V-02 | `StudentApp:PublicBaseUrl`, cliente de `billing` |
| `src/commerce/tests/CodeForCoders.Commerce.ArchitectureTests/ModuleSchemaConventionTest.cs` | V-01 | Vendas não depende de tabela de Catálogo nem de Matrícula |
| `src/bff-student/src/CodeForCoders.BffStudent.Application/Common/BffSecurityOptions.cs` | V-01 | `RouteAudiences`: `/api/v1/orders`, `/api/v1/offers` → `commerce` |
| `src/bff-student/src/CodeForCoders.BffStudent.Api/Extensions/EndpointExtensions.cs` | V-01 | Mapear as rotas de compra |
| `src/bff-student/src/CodeForCoders.BffStudent.Api/appsettings.json` | V-01 | Escopo `purchase-intent:write` mantido; nada novo na asserção |
| `src/student-spa/src/config/paths.ts` | V-01, V-06 | `/comprar/:offerId`, `/pedidos`, `/pedidos/:orderId` |
| `src/student-spa/src/app/router.tsx` | V-01 | Rotas novas |
| `src/student-spa/src/features/student-showcase/components/purchase-intent-button.tsx` | V-01 | Leva ao resumo; sem diálogo nem chamada de intenção |
| `src/student-spa/src/features/student-showcase/api/register-purchase-intent.ts` | V-01 | Removido do uso (a operação está depreciada) |
| `src/student-spa/src/features/student-session/components/student-login-screen.tsx` | V-01 | Sem `returnTo`, consultar a compra pendente |
| `src/notification/src/CodeForCoders.Notification.Contracts/TransactionalNotificationV1.cs` | V-05 | `DestinatarioConta` e dados do comprovante |
| `src/notification/src/CodeForCoders.Notification.Application/Common/NotificationSendRequestRules.cs` | V-05 | Finalidade nova; exatamente um destinatário; dados por modelo |
| `src/notification/src/CodeForCoders.Notification.Application/Common/NotificationPurposes.cs` | V-05 | `comprovante-de-compra`; motivo `destinatario-indisponivel` |
| `src/notification/src/CodeForCoders.Notification.Application/UseCases/Notifications/AcceptNotificationSendRequest/AcceptNotificationSendRequest.cs` | V-05 | Aceitar sem e-mail quando há conta |
| `src/notification/src/CodeForCoders.Notification.Application/UseCases/Notifications/DeliverAcceptedNotification/DeliverAcceptedNotification.cs` | V-05 | Resolver contato antes de entregar |
| `src/notification/src/CodeForCoders.Notification.Application/Services/MessageTemplateRenderer.cs` | V-05 | Modelo `comprovante-de-compra` |
| `src/notification/src/CodeForCoders.Notification.Domain/DeliveryRecords/DeliveryRecord.cs` | V-05 | Conta de destino, dados do modelo, preenchimento do contato na entrega |
| `src/identity/src/CodeForCoders.Identity.Api/Endpoints/StudentAccountLookupEndpoints.cs` | V-05, V-07 | Contato e resolução em lote (ou endpoints irmãos no mesmo padrão) |
| `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Endpoints/FinanceAreaEndpoints.cs` | V-07 | Lista e detalhe de pedidos |
| `src/admin-spa/src/features/finance-area/components/finance-area-screen.tsx` | V-07 | Da tela reservada para a lista de pedidos |
| `src/admin-spa/src/config/paths.ts`, `src/admin-spa/src/app/app-routes.tsx` | V-07 | Detalhe do pedido |
| `docker-compose.yml`, `docker-compose.remote.yml`, `docker-compose.coolify.yml` | EN-01, V-01, V-05 | `billing`; audiência `commerce` (`orders:use`) em Identity; emissores `notification` em Identity e `commerce` em `billing`; escopo `student-account:resolve` no emissor `bff-admin`; filas e exchanges |
| `scripts/generate-local-env.sh`, `scripts/init-local-databases.sql`, `scripts/remote-infra.sh`, `scripts/apps.sh` | EN-01 | Chaves, banco, provisionamento e saúde de `billing` |
| `contracts/README.md` e os arquivos de `contracts/` | — | **Só na conclusão do PRD** (promoção dos recortes) |

### A referenciar (não alterar)

| Caminho | Por que consultar |
|---|---|
| `src/commerce/src/CodeForCoders.Commerce.Application/UseCases/CourtesyGrants/GrantCourtesy/GrantCourtesy.cs` | Padrão de recibo de idempotência, bloqueio e fato na mesma transação |
| `src/commerce/src/CodeForCoders.Commerce.Infra.Data/Entitlement/AccessExpirationWorker.cs` | Formato da rotina periódica para a expiração de pedidos |
| `src/commerce/src/CodeForCoders.Commerce.Infra.Messaging/EntitlementCourseConsumerWorker.cs` | Padrão de consumidor (DLQ, reject com contagem, telemetria) |
| `src/commerce/src/CodeForCoders.Commerce.Api/Clients/StudentAccountConfirmationClient.cs`, `Security/StudentAccountAssertionTokenFactory.cs` | Padrão de cliente com asserção de serviço de `commerce` (para `billing`) |
| `src/commerce/src/CodeForCoders.Commerce.Domain/Entities/AccessTerm.cs` | Cálculo de término; a compra usa o mesmo |
| `src/learning/src/CodeForCoders.Learning.Api/Extensions/AuthenticationConfigurationExtensions.cs` | Política de aluno (`scope` e ausência de `permissions`) |
| `src/bff-student/src/CodeForCoders.BffStudent.Api/Endpoints/MyCoursesEndpoints.cs`, `Clients/MyCoursesLearningClient.cs` | Padrão de rota do aluno com JWT por audiência |
| `src/bff-student/src/CodeForCoders.BffStudent.Api/Security/BffSecurityMiddleware.cs` | Rotas anônimas × autenticadas, CSRF, erro de Identity |
| `src/identity/src/CodeForCoders.Identity.Application/Services/StudentConfirmationMessageWriter.cs` | Como um remetente publica no exchange de Notificação |
| `src/student-spa/src/app/student-login-return.test.tsx` | Padrão de teste de retorno pós-login sob `/student` |
| `docs/foundation.md` | Golden path de uma unidade nova |
| `docs/design/wireframes-cortesias.md` | Formato do registro de aprovação de design |

---

## Análise de Impacto

| Componente | Tipo | Impacto e risco | Ação requerida |
|---|---|---|---|
| `billing` | novo | Unidade inteira; segredos do Stripe; endpoint público | EN-01; webhook cadastrado por ambiente |
| `commerce` | modificado | Novo módulo com escrita, três consumidores, rotina, cliente síncrono; Matrícula ganha origem `purchase` | Migrations pela ferramenta do EF; testes de arquitetura de módulo |
| `notification` | modificado | Forma do pedido de envio (aditiva), dependência síncrona nova de Identity na entrega | Ordem de implantação (Notificação 1.2.0 antes de `commerce` publicar) |
| `identity` | modificado | Duas operações, emissor novo, audiência nova | Configuração nos três composes |
| `bff-student`, `bff-admin` | modificado | Rotas novas; composição com Identity no backoffice | — |
| `student-spa` | modificado | Botão *Comprar* muda de comportamento; três telas | EN-02 antes do código |
| `admin-spa` | modificado | Área financeira deixa de ser reservada | EN-02 antes do código |
| Contagem de intenção de compra | depreciado | Para de crescer; tabela e limpeza continuam | Remoção em versão futura |
| `learning`, `media` | inalterado | Passam a ver concessões `purchase` na decisão e em *meus cursos*, sem mudança de código | Coberto pelo E2E de V-02 |
| RabbitMQ | recurso compartilhado | Exchange `billing.events` e cinco filas novas | Topologia declarada pelos serviços donos |
| Conta Stripe | externo | Chave de teste e de produção; meios habilitados | OD80 (boleto) |

---

## Riscos e Preocupações

| Preocupação | Local (`arquivo:linha`) | Impacto | Mitigação |
|---|---|---|---|
| O publicador de `commerce` escolhe o exchange por uma condição fixa (auditoria × próprio) | `src/commerce/src/CodeForCoders.Commerce.Infra.Messaging/RabbitMqPublisher.cs:34` | O pedido de envio iria para `commerce.events` e Notificação nunca o receberia, sem erro (o exchange próprio aceita a chave) | Tabela chave → exchange em configuração; teste de integração que captura o pedido de envio na fila de Notificação (V-05) |
| `AccessGrant` só tem a fábrica de cortesia, que exige motivo e autor | `src/commerce/src/CodeForCoders.Commerce.Domain/Entities/AccessGrant.cs:32` e `:49-50` | Concessão por compra sem motivo seria recusada ou usaria valores falsos | Fábrica própria de compra; leituras já tratam motivo só para cortesia (`StudentAccessGrantQueries.cs:18`) |
| Não há unicidade de origem nas concessões | `src/commerce/src/CodeForCoders.Commerce.Infra.Data/Configurations/AccessGrantConfiguration.cs:32-34` | Mensagem reentregue concederia duas vezes (RN-D10) | Índice único parcial (tenant, origem, `origin_ref`) + teste de reentrega |
| Notificação assume e-mail presente no aceite (`Destinatario!`) | `src/notification/src/CodeForCoders.Notification.Application/UseCases/Notifications/AcceptNotificationSendRequest/AcceptNotificationSendRequest.cs:41` e `:69` | Pedido pela conta quebraria o aceite ou gravaria registro inválido | Aceite com conta de destino; regras de validade novas |
| A entrega descarta em silêncio registro sem e-mail | `src/notification/src/CodeForCoders.Notification.Application/UseCases/Notifications/DeliverAcceptedNotification/DeliverAcceptedNotification.cs:28` | Comprovante pela conta ficaria parado para sempre como "aceito" | Resolver o contato antes dessa verificação; teste de entrega pela conta |
| Finalidades fixas no código | `src/notification/src/CodeForCoders.Notification.Application/Common/NotificationSendRequestRules.cs:14` | `comprovante-de-compra` seria recusado como finalidade desconhecida | Incluir a finalidade e a validação dos dados do modelo |
| O renderizador só conhece nome, link e papel | `src/notification/src/CodeForCoders.Notification.Application/Services/MessageTemplateRenderer.cs:12` | Sem lugar para curso, valor, meio, vigência | Dados do modelo estruturados no registro e no renderizador |
| Audiências do JWT de aluno só existem por variável de ambiente | `src/identity/src/CodeForCoders.Identity.Api/appsettings.json:53` | Esquecer `commerce` num ambiente faz toda compra falhar com 502 | Configurar nos três composes; smoke de homologação cria um pedido |
| Tabela de rotas → audiência do BFF está no código como valor padrão | `src/bff-student/src/CodeForCoders.BffStudent.Application/Common/BffSecurityOptions.cs:24` | Rota de compra sem audiência chegaria a `commerce` sem token | Acrescentar as duas rotas ao padrão; teste do BFF |
| O evento do Stripe traz nome, e-mail e CPF do pagador | — (código novo de `billing`) | Gravar o corpo bruto criaria cópia de dado pessoal fora de Identity | Inbox só com ids, tipo e campos traduzidos; teste que inspeciona a linha gravada |
| A busca de aluno do backoffice exige `cortesia.conceder` | `contracts/identity/openapi-internal.yaml` (`lookupStudentAccountInternal`) | Papel futuro com `financeiro.ler` e sem cortesia não filtraria por e-mail | Aceito hoje (o financeiro tem as duas); registrado em C-09 |
| Expiração do PIX e do boleto no Checkout | — (gateway) | Não encontrei na documentação consultada os valores padrão nem os limites dos parâmetros de prazo do PIX e do boleto no Checkout | **Incerto**: confirmar na implementação de V-03 com a documentação do Stripe; se o PIX não aceitar 24 h, DP-03 volta ao negócio |

---

## Decisões Técnicas

- **D-01 — Pedido como agregado de Vendas no schema `sales`, com máquina de estados no domínio.**
  - Racional: o schema e o módulo já estão declarados; transições repetidas viram no-op no próprio agregado, o que torna todos os consumidores idempotentes sem tabela de inbox.
  - Trade-offs: a regra de "pago prevalece" fica no agregado e precisa de teste para cada par de estados.
  - Alternativas rejeitadas: inbox genérica por `eventId` em todos os consumidores (duplica o que a máquina de estados já garante).

- **D-02 — Vendas lê Catálogo e Matrícula por portas de leitura dos módulos, nunca por tabela.**
  - Racional: baseline proíbe tabela compartilhada entre módulos; Vendas fala com Matrícula "como se fosse outro serviço".
  - Trade-offs: mais uma interface por módulo.
  - Alternativas rejeitadas: consulta SQL direta (perde a extração futura de `Entitlement`).

- **D-03 — Unicidade de negócio no banco**: pendente por (aluno, oferta) e concessão por (origem, referência).
  - Racional: concorrência e reentrega são resolvidas pelo índice, não por bloqueio de aplicação.

- **D-04 — Exchanges e filas** como na topologia acima; publicador de `commerce` com tabela chave → exchange.

- **D-05 — Checkout hospedado com idempotência do Stripe por pedido; caminho de pagamento obtido a cada chamada, nunca persistido; webhook com inbox mínima.**
  - Racional: o endereço é credencial do pagamento; o corpo do evento tem dado pessoal.
  - Trade-offs: cada retomada é uma chamada ao Stripe.
  - Alternativas rejeitadas: guardar a URL (credencial em banco); guardar o evento bruto (dado pessoal).

- **D-06 — Valor confirmado diferente do congelado não bloqueia a conclusão** (C-11, RN-V08); a divergência aparece no backoffice e gera log de alerta sem dado pessoal.

- **D-07 — Rotina de expiração em Vendas** no formato da rotina de expiração de Matrícula, com relógio injetado.

- **D-08 — `commerce` valida o JWT de aluno no esquema que já usa para o ator, separado por política** (ADR-0016).

- **D-09 — A Fase 0 de `billing` entra neste plano como EN-01.**
  - Racional: o golden path existe (`docs/foundation.md`) e repetir o que os outros nove serviços já fizeram é trabalho mecânico e verificável; deixá-lo fora do plano tornaria V-02 dependente de algo sem dono nem gate.
  - Trade-offs: o plano fica maior; a decisão de Fase 0 "fora do fluxo" (registrada em `flow-state.json` para OD81) é revista.
  - Alternativas rejeitadas: Fase 0 externa (sem gate, sem dono no plano).

- **D-10 — Compra pendente de login no `localStorage`, com 24 h de validade.**
  - Racional: a confirmação de e-mail pode abrir em outra aba; `sessionStorage` e `returnTo` se perdem nesse caminho.
  - Trade-offs: só leva ao resumo, nunca compra sozinho; nenhum dado pessoal guardado (só `offerId` e `courseId`).

- **D-11 — URLs de volta em `http` só para `localhost` em desenvolvimento.**
  - Racional: o smoke local roda em `http://localhost:8082`; o contrato de `billing` diz "`https`, host autorizado".
  - Trade-offs: exige **errata** da descrição de `ensurePaymentSessionInternal` (sem mudança de schema): "`https` num host autorizado; `http` apenas em host local de desenvolvimento autorizado". Aplicada no contrato junto com a aprovação desta TechSpec.

---

## Verificação

- **Cenários críticos não óbvios:**
  - duas criações simultâneas de pedido na mesma oferta → um pedido;
  - `cobranca.pagamento-aguardando` depois de `confirmado` → sem efeito;
  - `compra-concluida` reentregue depois de a concessão existir → sem segunda concessão nem fato;
  - pedido `cancelled` + confirmação → `paid` e concessão;
  - rotina de expiração e fato de `billing` expirando o mesmo pedido → um `pedido-expirado`;
  - `startOrderPayment` repetido durante a abertura da página → uma página no Stripe;
  - webhook com evento de outro endpoint/escola (assinatura de outro segredo) → 400.
- **Dados ou ambiente especiais:**
  - Testes de integração com PostgreSQL e RabbitMQ em Testcontainers, como os demais serviços; **sem exportador OTLP** nos testes (memória do projeto sobre testes lentos).
  - Gateway em testes de integração: dublê na porta do gateway de `billing`, nunca o Stripe real.
  - E2E: sandbox do Stripe com chave de teste; **Stripe CLI** (`stripe listen --forward-to http://localhost:5110/webhooks/v1/stripe/events`) para o webhook local; `stripe trigger` para expiração; cartão e PIX de teste do Stripe.
  - Pré-requisitos reproduzíveis do smoke local: `scripts/generate-local-env.sh` (chaves novas), `scripts/remote-infra.sh provision && scripts/remote-infra.sh migrate` (banco de `billing`), `scripts/apps.sh start --remote`, `STRIPE_SECRET_KEY` e `STRIPE_WEBHOOK_SECRET` no `.env` local, uma oferta publicada de curso com aula com vídeo pronto.
- **Observabilidade além do padrão:**
  - histograma de pagamento confirmado (`confirmedAt`) → concessão (`grantedAt`), para o p95 ≤ 60 s do PRD;
  - contador de pedidos pagos há mais de 10 min sem `accessGrantedAt` e alerta (métrica "pedido pago sem concessão" = 0);
  - contadores de eventos do webhook por tipo traduzido e por recusa de assinatura;
  - contador de divergência de valor confirmado;
  - nenhuma dessas medidas carrega id de aluno, e-mail ou endereço de pagamento como dimensão.
- **Verificação dos contratos:**
  - mensagens: `AssertSends` em `commerce` (`vendas.*`, `matricula.acesso-concedido` com `purchase`, pedido de envio contra Notificação 1.2.0) e em `billing` (`cobranca.*`), lendo o recorte do PRD enquanto não promovido; depois, `contracts/`;
  - HTTP: respostas de `getStudentOrderInternal`, `ensurePaymentSessionInternal` e `resolveStudentAccountsInternal` validadas contra o schema dos recortes, no padrão de `AccessDecisionContract`;
  - validação de YAML (já feita no contrato) não substitui esses testes.

---

## Questões em Aberto

- [ ] **OD80 — boleto habilitado na conta Stripe** — tasso — sem boleto, V-03 entrega só PIX e o boleto sai do recorte por decisão explícita, sem mudar contrato.
- [ ] **Domínio público de `billing` em homologação** e regra do Coolify para expor só `/webhooks/v1` — tasso, em EN-01 — sem ele, o E2E de homologação não recebe o webhook (o local funciona pela Stripe CLI).
- [ ] **Parâmetros de prazo do PIX e do boleto no Checkout** — implementador de V-03, com a documentação do Stripe — ver Riscos.

---

## Architecture Decision Records

- [ADR-0016: JWT de aluno validado por `commerce`](../../docs/adr/0016-jwt-de-aluno-em-commerce.md) — **nova, Proposed**: audiência `commerce`, `orders:use`, separação aluno × ator por política; o comprador é o `sub`.
- [ADR-0017: Autenticação de serviço de `commerce` em `billing`](../../docs/adr/0017-autenticacao-de-servico-commerce-em-billing.md) — **nova, Proposed**: emissor `commerce`, escopo `payment:request`, chave exclusiva.
- [ADR-0018: Notificação resolve o destinatário pela conta](../../docs/adr/0018-notificacao-resolve-destinatario-pela-conta.md) — **nova, Proposed**: pedido de envio pela conta, emissor `notification` em Identity, escopo `student-contact:read`.
- [ADR-0004](../../docs/adr/0004-autenticacao-de-servico-bff-identity.md), [ADR-0005](../../docs/adr/0005-sessao-e-servico-do-backoffice.md), [ADR-0010](../../docs/adr/0010-autenticacao-de-servico-commerce-em-identity.md) — herdadas: mecanismo de asserção, JWT do ator para o backoffice de pedidos.
- [ADR-0009](../../docs/adr/0009-autenticacao-de-servico-bff-aluno-em-servicos-de-dominio.md) — herdada: a vitrine e o clique depreciado continuam anônimos.
- [ADR-0013](../../docs/adr/0013-jwt-de-aluno-validado-por-servicos-de-dominio.md) — herdada e estendida pela ADR-0016; o e-mail continua só na audiência `media`.
- [ADR-0002](../../docs/adr/0002-plataforma-de-runtime-coolify.md), [ADR-0001](../../docs/adr/0001-monorepo-de-codigo.md) — herdadas: `billing` segue o monorepo e o runtime Coolify.
