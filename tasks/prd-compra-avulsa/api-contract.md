# BFF do aluno — compra e pedidos

> **Gerado a partir de:** [api-contract.yaml](api-contract.yaml), OpenAPI 3.1.0, versão do contrato **1.0.0** (novo)  
> **PRD:** [prd.md](prd.md) v1.0, aprovado em 2026-10-05 (`CAP-011`)  
> **Estado:** Aprovado para implementação em 2026-10-05. Documento derivado do YAML, que é a fonte única; não o edite à mão.

Registra o acordo para a implementação deste PRD. PRDs posteriores podem evoluí-lo. Catalogação e mecanismos de atualização do acervo são da plataforma.

## Premissas e Decisões

| Decisão | Escolha | Motivo |
|---|---|---|
| Interface | `student-spa` → `bff-student`, sob `/api/v1` | BA05: SPA nunca fala com serviço. |
| Autenticação | Cookie `student_session`; CSRF nas escritas | ADR-0003, G18. |
| Repasse | JWT de aluno com audiência `commerce` | C-03; o BFF não decide nada. |
| Pagamento | Endereço do gateway devolvido ao cliente | DP-01: checkout hospedado; nenhum dado de cartão aqui. |
| Idempotência | `Idempotency-Key` na criação do pedido | G09; RF-03. |
| Valores | Centavos de real, inteiro | Convenção de CAP-003. |
| Paginação | `_page`/`_size`, máximo 50 | Meus pedidos. |

## Resumo de Operações

| Método | Path | `operationId` | Descrição | Segurança | Status |
|---|---|---|---|---|---|
| `GET` | `/api/v1/offers/{offerId}/purchase-summary` | `getPurchaseSummary` | Resumir a compra de uma opção | StudentSessionCookie | 200, 401, 404, 502, 504 |
| `POST` | `/api/v1/orders` | `createOrder` | Criar o pedido de uma opção | StudentSessionCookie | 201, 200, 400, 401, 403, 404, 422, 502, 504 |
| `GET` | `/api/v1/orders` | `listMyOrders` | Listar meus pedidos | StudentSessionCookie | 200, 400, 401, 502, 504 |
| `GET` | `/api/v1/orders/{orderId}` | `getMyOrder` | Obter um pedido meu | StudentSessionCookie | 200, 401, 404, 502, 504 |
| `POST` | `/api/v1/orders/{orderId}/payment-session` | `startOrderPayment` | Ir para o pagamento | StudentSessionCookie | 200, 401, 403, 404, 422, 502, 503, 504 |
| `POST` | `/api/v1/orders/{orderId}/cancellation` | `cancelMyOrder` | Desistir do pedido | StudentSessionCookie | 200, 401, 403, 404, 422, 502, 504 |

## Operações Detalhadas

### `GET /api/v1/offers/{offerId}/purchase-summary` — Resumir a compra de uma opção

Curso, opção, preço e vigência que o pedido congelaria agora (RF-01), o aviso de acesso já existente (RF-02) e o pedido pendente nesta opção, se houver (RN-V06). Sem efeito. Repasse de `getPurchaseSummaryInternal`.

| Parâmetro | Em | Obrigatório | Descrição |
|---|---|---|---|
| `offerId` | path | sim | Opção de compra (oferta) escolhida na página do curso. |

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

**401** — Sessão do aluno ausente, expirada ou revogada — inclusive visitante; code `SESSION_REQUIRED`.

**404** — Opção não mais à venda, inexistente ou de outra escola; code `OFFER_NOT_AVAILABLE`. Nada criado (RF-01, quarto critério).

**502** — `commerce` ou `identity` não respondeu ou falhou: `COMMERCE_UNAVAILABLE` ou `IDENTITY_UNAVAILABLE`. Nada mudou de forma visível ao cliente.

**504** — O serviço não respondeu no tempo limite; code `UPSTREAM_TIMEOUT`.

> **x-frontend-notes:** Com `pendingOrderId`, o botão leva a `/pedidos/{pendingOrderId}` em vez de criar pedido. Com `existingAccess`, mostra "Você já tem acesso a este curso até DD/MM/AAAA (cortesia)" ou "acesso vitalício", sem bloquear. 401 → entrar ou criar conta, voltando para esta opção.

### `POST /api/v1/orders` — Criar o pedido de uma opção

Cria o pedido com preço e vigência congelados agora (RF-03). Se já existe pedido do aluno aguardando pagamento nesta opção, devolve **esse** com 200, sem criar outro (RN-V06). `Idempotency-Key` gerada pelo cliente por confirmação: o mesmo clique repetido pela rede devolve o mesmo pedido (RF-03, último critério). Repasse de `createOrderInternal`.

| Parâmetro | Em | Obrigatório | Descrição |
|---|---|---|---|
| `Idempotency-Key` | header | sim | Chave opaca por confirmação de compra (G09). Mesma chave e mesmo corpo → mesmo resultado em 24 horas; corpo diferente → 422 `IDEMPOTENCY_KEY_REUSED`. |
| `X-CSRF-Token` | header | sim | Prova CSRF vinculada à sessão do aluno (`csrfToken` de `getCurrentStudentSession`). |

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

**200** — Pedido pendente já existente nesta opção (RN-V06).

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

**400** — JSON malformado, parâmetro ou campo inválido; code `VALIDATION_ERROR`.

**401** — Sessão do aluno ausente, expirada ou revogada — inclusive visitante; code `SESSION_REQUIRED`.

**403** — Prova CSRF ausente ou inválida; code `CSRF_INVALID`.

**404** — Opção não mais à venda, inexistente ou de outra escola; code `OFFER_NOT_AVAILABLE`. Nada criado (RF-01, quarto critério).

**422** — Mesma `Idempotency-Key` com corpo diferente; code `IDEMPOTENCY_KEY_REUSED`.

**502** — `commerce` ou `identity` não respondeu ou falhou: `COMMERCE_UNAVAILABLE` ou `IDENTITY_UNAVAILABLE`. Nada mudou de forma visível ao cliente.

**504** — O serviço não respondeu no tempo limite; code `UPSTREAM_TIMEOUT`.

### `GET /api/v1/orders` — Listar meus pedidos

Pedidos do aluno da sessão, do mais recente para o mais antigo, com o que cada um congelou (RF-10). Repasse de `listStudentOrdersInternal`.

| Parâmetro | Em | Obrigatório | Descrição |
|---|---|---|---|
| `_page` | query | não | Página, a partir de 1. |
| `_size` | query | não | Itens por página. |

**200** — Página de pedidos.

Exemplo `um`:

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

**400** — JSON malformado, parâmetro ou campo inválido; code `VALIDATION_ERROR`.

**401** — Sessão do aluno ausente, expirada ou revogada — inclusive visitante; code `SESSION_REQUIRED`.

**502** — `commerce` ou `identity` não respondeu ou falhou: `COMMERCE_UNAVAILABLE` ou `IDENTITY_UNAVAILABLE`. Nada mudou de forma visível ao cliente.

**504** — O serviço não respondeu no tempo limite; code `UPSTREAM_TIMEOUT`.

### `GET /api/v1/orders/{orderId}` — Obter um pedido meu

A situação real do pedido, para a página de retorno e para a retomada (RF-05, RF-06). A página de retorno consulta de novo a cada poucos segundos enquanto o pedido estiver `awaiting-payment` sem `pendingPayment` depois de o aluno ter concluído na página do gateway ("Confirmando"), e para quando a situação muda; passados 2 minutos, mostra o aviso de demora (RF-05). Pedido de outro aluno → 404, como inexistente. Repasse de `getStudentOrderInternal`.

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

**401** — Sessão do aluno ausente, expirada ou revogada — inclusive visitante; code `SESSION_REQUIRED`.

**404** — Pedido inexistente, de outro aluno ou de outra escola; code `ORDER_NOT_FOUND`.

**502** — `commerce` ou `identity` não respondeu ou falhou: `COMMERCE_UNAVAILABLE` ou `IDENTITY_UNAVAILABLE`. Nada mudou de forma visível ao cliente.

**504** — O serviço não respondeu no tempo limite; code `UPSTREAM_TIMEOUT`.

> **x-frontend-notes:** A volta do gateway chega como `/pedidos/{orderId}?resultado=concluido` ou `?resultado=saiu`. `paid` com `accessGrantedAt` → *Ir para o curso*; `paid` sem ele → "Liberando seu acesso", consultando de novo. A mudança para "Compra confirmada" é anunciada a leitor de tela.

### `POST /api/v1/orders/{orderId}/payment-session` — Ir para o pagamento

Devolve o endereço do gateway para onde o cliente navega: a página de pagamento ou, com PIX ou boleto já gerados, as instruções deles (RF-04, RF-06). Repetir é seguro. Pedido pago, expirado ou cancelado → 422 `ORDER_NOT_PAYABLE`. Gateway indisponível → 503 `PAYMENT_PROVIDER_UNAVAILABLE`, e o cliente oferece *Tentar de novo*, que usa o mesmo pedido. O BFF não guarda nem registra `paymentUrl`. Repasse de `startOrderPaymentInternal`.

| Parâmetro | Em | Obrigatório | Descrição |
|---|---|---|---|
| `orderId` | path | sim | Identificador do pedido. |
| `X-CSRF-Token` | header | sim | Prova CSRF vinculada à sessão do aluno (`csrfToken` de `getCurrentStudentSession`). |

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

**401** — Sessão do aluno ausente, expirada ou revogada — inclusive visitante; code `SESSION_REQUIRED`.

**403** — Prova CSRF ausente ou inválida; code `CSRF_INVALID`.

**404** — Pedido inexistente, de outro aluno ou de outra escola; code `ORDER_NOT_FOUND`.

**422** — Pedido pago, expirado ou cancelado, ou prazo vencido; code `ORDER_NOT_PAYABLE`.

**502** — `commerce` ou `identity` não respondeu ou falhou: `COMMERCE_UNAVAILABLE` ou `IDENTITY_UNAVAILABLE`. Nada mudou de forma visível ao cliente.

**503** — O pagamento não pôde ser aberto agora; o pedido continua aguardando. code `PAYMENT_PROVIDER_UNAVAILABLE`.

**504** — O serviço não respondeu no tempo limite; code `UPSTREAM_TIMEOUT`.

### `POST /api/v1/orders/{orderId}/cancellation` — Desistir do pedido

Desiste de um pedido aguardando pagamento (RF-06, RN-V07); repetir em pedido já cancelado devolve ele. Pedido pago ou expirado → 422 `ORDER_NOT_CANCELLABLE`. Depois, `createOrder` na mesma opção cria pedido novo pelas condições vigentes. Repasse de `cancelOrderInternal`.

| Parâmetro | Em | Obrigatório | Descrição |
|---|---|---|---|
| `orderId` | path | sim | Identificador do pedido. |
| `X-CSRF-Token` | header | sim | Prova CSRF vinculada à sessão do aluno (`csrfToken` de `getCurrentStudentSession`). |

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

**401** — Sessão do aluno ausente, expirada ou revogada — inclusive visitante; code `SESSION_REQUIRED`.

**403** — Prova CSRF ausente ou inválida; code `CSRF_INVALID`.

**404** — Pedido inexistente, de outro aluno ou de outra escola; code `ORDER_NOT_FOUND`.

**422** — Pedido pago ou expirado; code `ORDER_NOT_CANCELLABLE`.

**502** — `commerce` ou `identity` não respondeu ou falhou: `COMMERCE_UNAVAILABLE` ou `IDENTITY_UNAVAILABLE`. Nada mudou de forma visível ao cliente.

**504** — O serviço não respondeu no tempo limite; code `UPSTREAM_TIMEOUT`.
