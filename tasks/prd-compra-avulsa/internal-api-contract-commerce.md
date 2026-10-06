# API interna de `commerce` — Vendas e Checkout

> **Gerado a partir de:** [internal-api-contract-commerce.yaml](internal-api-contract-commerce.yaml), OpenAPI 3.1.0, versão do contrato **1.4.0** (1.3.0 → 1.4.0)  
> **PRD:** [prd.md](prd.md) v1.0, aprovado em 2026-10-05 (`CAP-011`)  
> **Estado:** Aprovado para implementação em 2026-10-05. Documento derivado do YAML, que é a fonte única; não o edite à mão.

Registra o acordo para a implementação deste PRD. PRDs posteriores podem evoluí-lo. Catalogação e mecanismos de atualização do acervo são da plataforma.

## Premissas e Decisões

| Decisão | Escolha | Motivo |
|---|---|---|
| Interface | `bff-student` e `bff-admin` → `commerce`, sob `/internal/v1` | Rota privada. |
| Aluno | JWT de aluno `aud commerce`, `scope orders:use`, sem `permissions` | C-03; ADR-0013 estendida. |
| Financeiro | JWT do ator com `financeiro.ler` | ADR-0005, DP-09. |
| Pedido ≠ acesso | `accessGrantedAt` é exibição | RN-V10, C-07. |
| Depreciação | `registerPurchaseIntentInternal` não conta mais | DP-06, C-08. |

## Resumo de Operações

| Método | Path | `operationId` | Descrição | Segurança | Status |
|---|---|---|---|---|---|
| `GET` | `/internal/v1/offers/{offerId}/purchase-summary` | `getPurchaseSummaryInternal` | Resumir a compra de uma oferta | StudentUserToken | 200, 401, 403, 404 |
| `POST` | `/internal/v1/orders` | `createOrderInternal` | Criar o pedido de uma oferta | StudentUserToken | 201, 200, 400, 401, 403, 404, 422 |
| `GET` | `/internal/v1/orders` | `listStudentOrdersInternal` | Listar os pedidos do aluno | StudentUserToken | 200, 400, 401, 403 |
| `GET` | `/internal/v1/orders/{orderId}` | `getStudentOrderInternal` | Obter um pedido do aluno | StudentUserToken | 200, 401, 403, 404 |
| `POST` | `/internal/v1/orders/{orderId}/payment-session` | `startOrderPaymentInternal` | Ir para o pagamento do pedido | StudentUserToken | 200, 401, 403, 404, 422, 503 |
| `POST` | `/internal/v1/orders/{orderId}/cancellation` | `cancelOrderInternal` | Desistir de um pedido aguardando pagamento | StudentUserToken | 200, 401, 403, 404, 422 |
| `GET` | `/internal/v1/finance/orders` | `listFinanceOrdersInternal` | Listar os pedidos da escola | StaffUserToken | 200, 400, 401, 403 |
| `GET` | `/internal/v1/finance/orders/{orderId}` | `getFinanceOrderInternal` | Obter o detalhe de um pedido | StaffUserToken | 200, 401, 403, 404 |
| `POST` | `/internal/v1/showcase/offers/{offerId}/purchase-intents` | `registerPurchaseIntentInternal` | Registrar um clique em Comprar (depreciada) *(depreciada)* | StudentBffServiceAssertion | 202, 404, 401, 403 |

## Operações Detalhadas

### `GET /internal/v1/offers/{offerId}/purchase-summary` — Resumir a compra de uma oferta

O que o aluno vê antes de ir ao pagamento (RF-01, RF-02): curso, opção, preço e vigência prometida **vigentes agora** — os que o pedido congelaria se fosse criado neste instante (RN-V03). Sem efeito.

- `pendingOrderId`: pedido do aluno aguardando pagamento **nesta** oferta, se houver (RN-V06); o cliente leva a ele em vez de criar outro.
- `existingAccess`: concessão ativa e dentro da vigência do aluno sobre o curso, de qualquer origem, a que termina por último (vitalícia prevalece). **Informativo** (RN-V11, DP-08): não bloqueia nem libera nada. Se Matrícula não responder, `existingAccessChecked: false` e `existingAccess: null`, e o resumo sai mesmo assim (RF-02, quarto critério).

Oferta não publicada, inexistente ou de outra escola → 404 indistinto (RN-V02).

| Parâmetro | Em | Obrigatório | Descrição |
|---|---|---|---|
| `offerId` | path | sim | Identificador da oferta. |

**200** — Resumo da compra.

Exemplo `comCortesia`:

```json
{
  "course": {
    "courseId": "6f1e2d3c-4b5a-4c69-8d7e-1f2a3b4c5d6e",
    "title": ".NET do zero à API"
  },
  "offer": {
    "offerId": "8d9e0f1a-2b3c-4d4e-9f5a-6b7c8d9e0f1a",
    "name": "Acesso vitalício",
    "priceCents": 89700,
    "currency": "BRL",
    "accessPeriod": {
      "type": "lifetime"
    }
  },
  "pendingOrderId": null,
  "existingAccessChecked": true,
  "existingAccess": {
    "origin": "courtesy",
    "validity": {
      "type": "until",
      "endsOn": "2026-11-30"
    }
  }
}
```

**401** — JWT ausente, expirado, de audiência ou emissor errados, ou com assinatura inválida; code `TOKEN_INVALID`.

**403** — Token válido sem a política de aluno — sem `orders:use` ou com `permissions` (token de ator interno, RN-V01); code `SCOPE_DENIED`.

**404** — Oferta não publicada, inexistente ou de outra escola — indistintas; code `OFFER_NOT_AVAILABLE`. Nada é criado.

### `POST /internal/v1/orders` — Criar o pedido de uma oferta

Cria o pedido em `awaiting-payment` com curso, opção, preço e vigência da oferta **congelados neste instante** (RF-03, RN-V03, RN-V04). Só conta de aluno (RN-V01): token de ator interno não tem o escopo de aluno e é recusado com 403.

- Já existe pedido do aluno aguardando pagamento **para a mesma oferta** → 200 com **esse** pedido, inalterado, sem criar outro (RN-V06). Pedido pendente de outra oferta do mesmo curso não impede (RF-03, quinto critério).
- Oferta não publicada, inexistente ou de outra escola → 404, nada criado (RN-V02).
- `Idempotency-Key` obrigatória (G09): repetição com o mesmo corpo devolve o mesmo resultado e o mesmo status; corpo diferente → 422 `IDEMPOTENCY_KEY_REUSED`.

Publica `vendas.pedido-criado.v1` pelo outbox, na mesma transação (G06). Não abre pagamento: isso é `startOrderPaymentInternal`.

| Parâmetro | Em | Obrigatório | Descrição |
|---|---|---|---|
| `Idempotency-Key` | header | sim | Chave opaca única por intenção de escrita (G09). Repetição com o mesmo corpo devolve o mesmo resultado na janela de 24 horas; corpo diferente com a mesma chave → 422 `IDEMPOTENCY_KEY_REUSED`. |

**Corpo** — `dozeMeses`

```json
{
  "offerId": "7c8d9e0f-1a2b-4c3d-8e4f-5a6b7c8d9e0f"
}
```

**201** — Pedido criado.

Exemplo `criado`:

```json
{
  "orderId": "1b2c3d4e-5f60-4718-9a2b-3c4d5e6f7a81",
  "number": "000123",
  "status": "awaiting-payment",
  "course": {
    "courseId": "6f1e2d3c-4b5a-4c69-8d7e-1f2a3b4c5d6e",
    "title": ".NET do zero à API"
  },
  "offer": {
    "offerId": "7c8d9e0f-1a2b-4c3d-8e4f-5a6b7c8d9e0f",
    "name": "Acesso por 12 meses"
  },
  "priceCents": 49700,
  "currency": "BRL",
  "accessPeriod": {
    "type": "months",
    "months": 12
  },
  "paymentMethod": null,
  "pendingPayment": null,
  "paymentPageExpiresAt": null,
  "accessGrantedAt": null,
  "createdAt": "2026-10-05T14:19:30Z",
  "paidAt": null,
  "expiredAt": null,
  "cancelledAt": null
}
```

**200** — Já havia pedido do aluno aguardando pagamento nesta oferta; é ele que volta (RN-V06).

Exemplo `pendente`:

```json
{
  "orderId": "2c3d4e5f-6071-4829-8b3c-4d5e6f7a8b92",
  "number": "000124",
  "status": "awaiting-payment",
  "course": {
    "courseId": "9a7e5c31-2b4d-4f68-8e01-6d3c5a7b9f24",
    "title": "React na prática"
  },
  "offer": {
    "offerId": "5a6b7c8d-9e0f-4a1b-8c2d-3e4f5a6b7c8d",
    "name": "Acesso por 6 meses"
  },
  "priceCents": 29700,
  "currency": "BRL",
  "accessPeriod": {
    "type": "months",
    "months": 6
  },
  "paymentMethod": "boleto",
  "pendingPayment": {
    "method": "boleto",
    "expiresAt": "2026-10-09T02:59:59Z"
  },
  "paymentPageExpiresAt": null,
  "accessGrantedAt": null,
  "createdAt": "2026-10-06T10:00:00Z",
  "paidAt": null,
  "expiredAt": null,
  "cancelledAt": null
}
```

**400** — Requisição malformada, parâmetro fora dos limites ou período invertido; code `INVALID_REQUEST`.

**401** — JWT ausente, expirado, de audiência ou emissor errados, ou com assinatura inválida; code `TOKEN_INVALID`.

**403** — Token válido sem a política de aluno — sem `orders:use` ou com `permissions` (token de ator interno, RN-V01); code `SCOPE_DENIED`.

**404** — Oferta não publicada, inexistente ou de outra escola — indistintas; code `OFFER_NOT_AVAILABLE`. Nada é criado.

**422** — Mesma `Idempotency-Key` com corpo diferente; code `IDEMPOTENCY_KEY_REUSED`. Nada é criado.

### `GET /internal/v1/orders` — Listar os pedidos do aluno

Pedidos do aluno do token, na escola do token, do mais recente para o mais antigo (RF-10). Mostram o que o pedido congelou, mesmo que a oferta tenha mudado ou saído da vitrine (RN-V03, RN-V12). Sem pedidos → lista vazia.

| Parâmetro | Em | Obrigatório | Descrição |
|---|---|---|---|
| `_page` | query | não | Página, a partir de 1. |
| `_size` | query | não | Itens por página. |

**200** — Página de pedidos do aluno.

Exemplo `tres`:

```json
{
  "data": [
    {
      "orderId": "2c3d4e5f-6071-4829-8b3c-4d5e6f7a8b92",
      "number": "000124",
      "status": "awaiting-payment",
      "course": {
        "courseId": "9a7e5c31-2b4d-4f68-8e01-6d3c5a7b9f24",
        "title": "React na prática"
      },
      "offer": {
        "offerId": "5a6b7c8d-9e0f-4a1b-8c2d-3e4f5a6b7c8d",
        "name": "Acesso por 6 meses"
      },
      "priceCents": 29700,
      "currency": "BRL",
      "accessPeriod": {
        "type": "months",
        "months": 6
      },
      "paymentMethod": "boleto",
      "pendingPayment": {
        "method": "boleto",
        "expiresAt": "2026-10-09T02:59:59Z"
      },
      "paymentPageExpiresAt": null,
      "accessGrantedAt": null,
      "createdAt": "2026-10-06T10:00:00Z",
      "paidAt": null,
      "expiredAt": null,
      "cancelledAt": null
    },
    {
      "orderId": "1b2c3d4e-5f60-4718-9a2b-3c4d5e6f7a81",
      "number": "000123",
      "status": "paid",
      "course": {
        "courseId": "6f1e2d3c-4b5a-4c69-8d7e-1f2a3b4c5d6e",
        "title": ".NET do zero à API"
      },
      "offer": {
        "offerId": "7c8d9e0f-1a2b-4c3d-8e4f-5a6b7c8d9e0f",
        "name": "Acesso por 12 meses"
      },
      "priceCents": 49700,
      "currency": "BRL",
      "accessPeriod": {
        "type": "months",
        "months": 12
      },
      "paymentMethod": "card",
      "pendingPayment": null,
      "paymentPageExpiresAt": null,
      "accessGrantedAt": "2026-10-05T14:21:04Z",
      "createdAt": "2026-10-05T14:19:30Z",
      "paidAt": "2026-10-05T14:21:02Z",
      "expiredAt": null,
      "cancelledAt": null
    }
  ],
  "pagination": {
    "page": 1,
    "size": 10,
    "total": 2,
    "totalPages": 1
  }
}
```

**400** — Requisição malformada, parâmetro fora dos limites ou período invertido; code `INVALID_REQUEST`.

**401** — JWT ausente, expirado, de audiência ou emissor errados, ou com assinatura inválida; code `TOKEN_INVALID`.

**403** — Token válido sem a política de aluno — sem `orders:use` ou com `permissions` (token de ator interno, RN-V01); code `SCOPE_DENIED`.

### `GET /internal/v1/orders/{orderId}` — Obter um pedido do aluno

A situação **real** do pedido, para a página de retorno e para a retomada (RF-05, RF-06, RN-CB04). O cliente consulta de novo até a situação mudar; a resposta não é guardada (`no-store`).

- `paid` → pago; `accessGrantedAt` preenchido quando a concessão existe (RF-05: *Ir para o curso*).
- `awaiting-payment` com `pendingPayment` → PIX ou boleto gerado, ainda não pago.
- `awaiting-payment` sem `pendingPayment` → página de pagamento aberta (`paymentPageExpiresAt`) ou ainda não aberta (nulo). O cliente sabe se o aluno concluiu ou saiu pela própria volta da página; "Confirmando" é `awaiting-payment` sem `pendingPayment` depois de o aluno ter concluído.
- `expired`, `cancelled` → terminais sem concessão, salvo confirmação tardia (RN-V08), que muda para `paid`.

Pedido de outro aluno, de outra escola ou inexistente → 404 indistinto.

| Parâmetro | Em | Obrigatório | Descrição |
|---|---|---|---|
| `orderId` | path | sim | Identificador do pedido. |

**200** — O pedido.

Exemplo `pago`:

```json
{
  "orderId": "1b2c3d4e-5f60-4718-9a2b-3c4d5e6f7a81",
  "number": "000123",
  "status": "paid",
  "course": {
    "courseId": "6f1e2d3c-4b5a-4c69-8d7e-1f2a3b4c5d6e",
    "title": ".NET do zero à API"
  },
  "offer": {
    "offerId": "7c8d9e0f-1a2b-4c3d-8e4f-5a6b7c8d9e0f",
    "name": "Acesso por 12 meses"
  },
  "priceCents": 49700,
  "currency": "BRL",
  "accessPeriod": {
    "type": "months",
    "months": 12
  },
  "paymentMethod": "card",
  "pendingPayment": null,
  "paymentPageExpiresAt": null,
  "accessGrantedAt": "2026-10-05T14:21:04Z",
  "createdAt": "2026-10-05T14:19:30Z",
  "paidAt": "2026-10-05T14:21:02Z",
  "expiredAt": null,
  "cancelledAt": null
}
```

**401** — JWT ausente, expirado, de audiência ou emissor errados, ou com assinatura inválida; code `TOKEN_INVALID`.

**403** — Token válido sem a política de aluno — sem `orders:use` ou com `permissions` (token de ator interno, RN-V01); code `SCOPE_DENIED`.

**404** — Pedido inexistente, de outro aluno ou de outra escola — indistintos; code `ORDER_NOT_FOUND`.

### `POST /internal/v1/orders/{orderId}/payment-session` — Ir para o pagamento do pedido

Devolve para onde levar o aluno (RF-04, RF-06): a página de pagamento ou, com PIX ou boleto já gerados, as instruções desse pagamento. `commerce` pede a `billing` (`ensurePaymentSessionInternal`) com o valor, a moeda e a descrição **congelados no pedido** e os endereços de volta da escola; repetir é seguro, porque `billing` é idempotente por pedido. Na primeira página aberta, o pedido passa a ter `paymentPageExpiresAt`.

- Pedido `paid`, `expired` ou `cancelled` → 422 `ORDER_NOT_PAYABLE`, sem chamar `billing`.
- `billing` responde que o prazo venceu → 422 `ORDER_NOT_PAYABLE`; o pedido expira pelo fato `cobranca.pagamento-nao-confirmado.v1`.
- `billing` ou o gateway indisponível → 503 `PAYMENT_PROVIDER_UNAVAILABLE`; o pedido continua `awaiting-payment` e uma nova tentativa usa o mesmo pedido (RF-04, último critério).

Sem corpo. `paymentUrl` é credencial de acesso ao pagamento: o BFF a entrega ao navegador do dono do pedido e não a guarda nem a registra.

| Parâmetro | Em | Obrigatório | Descrição |
|---|---|---|---|
| `orderId` | path | sim | Identificador do pedido. |

**200** — Para onde levar o aluno.

Exemplo `pagina`:

```json
{
  "orderId": "1b2c3d4e-5f60-4718-9a2b-3c4d5e6f7a81",
  "kind": "checkout",
  "paymentUrl": "https://checkout.stripe.com/c/pay/cs_test_a1B2c3D4e5F6g7H8",
  "expiresAt": "2026-10-06T14:20:00Z"
}
```

**401** — JWT ausente, expirado, de audiência ou emissor errados, ou com assinatura inválida; code `TOKEN_INVALID`.

**403** — Token válido sem a política de aluno — sem `orders:use` ou com `permissions` (token de ator interno, RN-V01); code `SCOPE_DENIED`.

**404** — Pedido inexistente, de outro aluno ou de outra escola — indistintos; code `ORDER_NOT_FOUND`.

**422** — Pedido `paid`, `expired` ou `cancelled`, ou prazo de pagamento vencido; code `ORDER_NOT_PAYABLE`, com a situação atual em `detail`.

**503** — `billing` ou o gateway não responderam; nada mudou no pedido. code `PAYMENT_PROVIDER_UNAVAILABLE`.

### `POST /internal/v1/orders/{orderId}/cancellation` — Desistir de um pedido aguardando pagamento

Torna o pedido `cancelled` (RF-06, RN-V07). Publica `vendas.pedido-cancelado.v1` pelo outbox, na mesma transação; `billing` o recebe e cancela no gateway a página ou o pagamento pendente. Se, mesmo assim, o gateway confirmar o pagamento, o pedido vira `paid` e concede (RN-V08).

- Pedido já `cancelled` → 200 com ele, sem novo fato (repetição segura).
- Pedido `paid` ou `expired` → 422 `ORDER_NOT_CANCELLABLE`, nada muda.

Depois de desistir, `createOrderInternal` na mesma oferta cria um pedido **novo**, pelas condições vigentes da oferta.

| Parâmetro | Em | Obrigatório | Descrição |
|---|---|---|---|
| `orderId` | path | sim | Identificador do pedido. |

**200** — Pedido cancelado.

Exemplo `cancelado`:

```json
{
  "orderId": "2c3d4e5f-6071-4829-8b3c-4d5e6f7a8b92",
  "number": "000124",
  "status": "cancelled",
  "course": {
    "courseId": "9a7e5c31-2b4d-4f68-8e01-6d3c5a7b9f24",
    "title": "React na prática"
  },
  "offer": {
    "offerId": "5a6b7c8d-9e0f-4a1b-8c2d-3e4f5a6b7c8d",
    "name": "Acesso por 6 meses"
  },
  "priceCents": 29700,
  "currency": "BRL",
  "accessPeriod": {
    "type": "months",
    "months": 6
  },
  "paymentMethod": "boleto",
  "pendingPayment": null,
  "paymentPageExpiresAt": null,
  "accessGrantedAt": null,
  "createdAt": "2026-10-06T10:00:00Z",
  "paidAt": null,
  "expiredAt": null,
  "cancelledAt": "2026-10-06T18:42:10Z"
}
```

**401** — JWT ausente, expirado, de audiência ou emissor errados, ou com assinatura inválida; code `TOKEN_INVALID`.

**403** — Token válido sem a política de aluno — sem `orders:use` ou com `permissions` (token de ator interno, RN-V01); code `SCOPE_DENIED`.

**404** — Pedido inexistente, de outro aluno ou de outra escola — indistintos; code `ORDER_NOT_FOUND`.

**422** — Pedido `paid` ou `expired`; code `ORDER_NOT_CANCELLABLE`. Nada muda.

### `GET /internal/v1/finance/orders` — Listar os pedidos da escola

Pedidos da escola do token, mais recentes primeiro (RF-11). Exige `financeiro.ler` (DP-09); só leitura. Filtros combináveis: `status`, `courseId`, `studentId` e período de **criação** do pedido (`createdFrom` e `createdTo`, datas inclusivas no fuso da escola). O aluno vai por referência (`studentId`): nome e e-mail são resolvidos pelo `bff-admin` em Identity, nunca guardados aqui.

| Parâmetro | Em | Obrigatório | Descrição |
|---|---|---|---|
| `status` | query | não | Situação do pedido. Ausente = todas. |
| `courseId` | query | não | Curso do pedido. |
| `studentId` | query | não | Conta de aluno em Identidade e Acesso. |
| `createdFrom` | query | não | Primeiro dia do período, inclusivo, no fuso da escola. |
| `createdTo` | query | não | Último dia do período, inclusivo, no fuso da escola. Anterior a `createdFrom` → 400. |
| `_page` | query | não | Página, a partir de 1. |
| `_size` | query | não | Itens por página. |

**200** — Página de pedidos.

Exemplo `pendentes`:

```json
{
  "data": [
    {
      "orderId": "2c3d4e5f-6071-4829-8b3c-4d5e6f7a8b92",
      "number": "000124",
      "studentId": "2b3c4d5e-6f70-4a81-9b92-a3b4c5d6e7f8",
      "status": "awaiting-payment",
      "courseId": "9a7e5c31-2b4d-4f68-8e01-6d3c5a7b9f24",
      "courseTitle": "React na prática",
      "offerName": "Acesso por 6 meses",
      "priceCents": 29700,
      "currency": "BRL",
      "paymentMethod": "boleto",
      "createdAt": "2026-10-06T10:00:00Z",
      "paidAt": null
    }
  ],
  "pagination": {
    "page": 1,
    "size": 10,
    "total": 1,
    "totalPages": 1
  }
}
```

**400** — Requisição malformada, parâmetro fora dos limites ou período invertido; code `INVALID_REQUEST`.

**401** — JWT ausente, expirado, de audiência ou emissor errados, ou com assinatura inválida; code `TOKEN_INVALID`.

**403** — Token de ator sem `financeiro.ler`, ou token de aluno; code `PERMISSION_DENIED`.

### `GET /internal/v1/finance/orders/{orderId}` — Obter o detalhe de um pedido

Tudo o que a lista mostra, mais a vigência congelada, os momentos de cada mudança, a referência do pagamento no gateway (para localizar no painel dele) e a concessão correspondente, quando existe (RF-11). Exige `financeiro.ler`. Pedido de outra escola ou inexistente → 404 indistinto.

| Parâmetro | Em | Obrigatório | Descrição |
|---|---|---|---|
| `orderId` | path | sim | Identificador do pedido. |

**200** — O pedido.

Exemplo `pago`:

```json
{
  "orderId": "1b2c3d4e-5f60-4718-9a2b-3c4d5e6f7a81",
  "number": "000123",
  "studentId": "9d8c7b6a-5f4e-4d3c-8b2a-1f0e9d8c7b6a",
  "status": "paid",
  "courseId": "6f1e2d3c-4b5a-4c69-8d7e-1f2a3b4c5d6e",
  "courseTitle": ".NET do zero à API",
  "offerId": "7c8d9e0f-1a2b-4c3d-8e4f-5a6b7c8d9e0f",
  "offerName": "Acesso por 12 meses",
  "priceCents": 49700,
  "currency": "BRL",
  "accessPeriod": {
    "type": "months",
    "months": 12
  },
  "paymentMethod": "card",
  "paymentReference": "pi_3Q2w3E4r5T6y7U8i0",
  "paidAmountCents": 49700,
  "grantId": "4c5d6e7f-8091-4a2b-9c3d-4e5f6a7b8c9d",
  "accessGrantedAt": "2026-10-05T14:21:04Z",
  "createdAt": "2026-10-05T14:19:30Z",
  "paymentPageExpiresAt": "2026-10-06T14:19:41Z",
  "paidAt": "2026-10-05T14:21:02Z",
  "expiredAt": null,
  "cancelledAt": null
}
```

**401** — JWT ausente, expirado, de audiência ou emissor errados, ou com assinatura inválida; code `TOKEN_INVALID`.

**403** — Token de ator sem `financeiro.ler`, ou token de aluno; code `PERMISSION_DENIED`.

**404** — Pedido inexistente, de outro aluno ou de outra escola — indistintos; code `ORDER_NOT_FOUND`.

### `POST /internal/v1/showcase/offers/{offerId}/purchase-intents` — Registrar um clique em Comprar (depreciada)

**Depreciada em 1.4.0 (DP-06, C-08).** O botão *Comprar* passa a levar à compra (`getPurchaseSummaryInternal`, `createOrderInternal`), e esta operação **deixa de contar**: responde 202 com `purchaseAvailability: available` sem somar nada. A contagem histórica continua visível na oferta como estava (`purchaseIntentCount`, inalterado). Oferta não publicada → 404. Remoção em versão futura, quando nenhum cliente a chamar.

| Parâmetro | Em | Obrigatório | Descrição |
|---|---|---|---|
| `offerId` | path | sim | Identificador da oferta. |
| `Idempotency-Key` | header | sim | Mantida por compatibilidade; sem efeito. |

**202** — Nada contado; a compra está disponível.

Exemplo `disponivel`:

```json
{
  "purchaseAvailability": "available"
}
```

**404** — Oferta não publicada, inexistente ou de outra escola — indistintas; code `OFFER_NOT_AVAILABLE`. Nada é criado.

**401** — Asserção de serviço ausente, inválida, repetida (`jti`) ou de outro emissor.

**403** — Asserção sem o escopo da operação; code `SCOPE_DENIED`.
