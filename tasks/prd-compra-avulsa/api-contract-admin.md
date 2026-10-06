# BFF do backoffice — pedidos

> **Gerado a partir de:** [api-contract-admin.yaml](api-contract-admin.yaml), OpenAPI 3.1.0, versão do contrato **1.0.0** (novo)  
> **PRD:** [prd.md](prd.md) v1.0, aprovado em 2026-10-05 (`CAP-011`)  
> **Estado:** Aprovado para implementação em 2026-10-05. Documento derivado do YAML, que é a fonte única; não o edite à mão.

Registra o acordo para a implementação deste PRD. PRDs posteriores podem evoluí-lo. Catalogação e mecanismos de atualização do acervo são da plataforma.

## Premissas e Decisões

| Decisão | Escolha | Motivo |
|---|---|---|
| Interface | `admin-spa` → `bff-admin`, sob `/api/v1` | BA05. |
| Permissão | `financeiro.ler`, só leitura | DP-09. |
| Aluno | Nome e e-mail resolvidos em Identity por página | C-09; e-mail nunca em URL (G23). |
| Filtro por e-mail | `lookupStudentAccount` → `studentId` | Reuso, sem e-mail em query. |
| Paginação | `_page`/`_size`, máximo 50 | RF-11. |

## Resumo de Operações

| Método | Path | `operationId` | Descrição | Segurança | Status |
|---|---|---|---|---|---|
| `GET` | `/api/v1/finance/orders` | `listFinanceOrders` | Listar os pedidos da escola | StaffSessionCookie | 200, 400, 401, 403, 502, 504 |
| `GET` | `/api/v1/finance/orders/{orderId}` | `getFinanceOrder` | Obter o detalhe de um pedido | StaffSessionCookie | 200, 401, 403, 404, 502, 504 |

## Operações Detalhadas

### `GET /api/v1/finance/orders` — Listar os pedidos da escola

Pedidos mais recentes primeiro, com filtros combináveis por situação, curso, aluno e período de criação (datas inclusivas no fuso da escola). Cada item traz nome e e-mail do aluno, resolvidos em Identity para a página inteira de uma vez; se Identity não responder, a lista não sai (502), em vez de sair sem aluno.

| Parâmetro | Em | Obrigatório | Descrição |
|---|---|---|---|
| `status` | query | não | Situação do pedido. Ausente = todas. |
| `courseId` | query | não | Curso do pedido. |
| `studentId` | query | não | Conta de aluno, obtida por `lookupStudentAccount`. |
| `createdFrom` | query | não | Primeiro dia do período, inclusivo. |
| `createdTo` | query | não | Último dia do período, inclusivo. Anterior a `createdFrom` → 400. |
| `_page` | query | não | Página, a partir de 1. |
| `_size` | query | não | Itens por página. |

**200** — Página de pedidos.

Exemplo `pendentesDoCurso`:

```json
{
  "data": [
    {
      "orderId": "2c3d4e5f-6071-4829-8b3c-4d5e6f7a8b92",
      "number": "000124",
      "student": {
        "studentId": "2b3c4d5e-6f70-4a81-9b92-a3b4c5d6e7f8",
        "name": "Joana Ribeiro",
        "email": "joana.ribeiro@example.com"
      },
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

**400** — Parâmetro inválido ou período invertido; code `VALIDATION_ERROR`.

**401** — Sessão do backoffice ausente, expirada ou revogada; code `SESSION_REQUIRED`.

**403** — Ator sem `financeiro.ler` (professor, suporte, administrador sem o papel financeiro); code `PERMISSION_DENIED`. `commerce` recusa da mesma forma se chamado diretamente.

**502** — `commerce` ou `identity` não respondeu; code `COMMERCE_UNAVAILABLE` ou `IDENTITY_UNAVAILABLE`.

**504** — O serviço não respondeu no tempo limite; code `UPSTREAM_TIMEOUT`.

### `GET /api/v1/finance/orders/{orderId}` — Obter o detalhe de um pedido

O pedido com a vigência congelada, os momentos de criação, pagamento, expiração ou cancelamento, a referência do pagamento no gateway (para localizar no painel dele) e a concessão correspondente, quando existe (RF-11). Sem ações. Pedido de outra escola ou inexistente → 404.

| Parâmetro | Em | Obrigatório | Descrição |
|---|---|---|---|
| `orderId` | path | sim | Identificador do pedido. |

**200** — O pedido.

Exemplo `pago`:

```json
{
  "orderId": "1b2c3d4e-5f60-4718-9a2b-3c4d5e6f7a81",
  "number": "000123",
  "student": {
    "studentId": "9d8c7b6a-5f4e-4d3c-8b2a-1f0e9d8c7b6a",
    "name": "Ana Souza",
    "email": "ana.souza@example.com"
  },
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

**401** — Sessão do backoffice ausente, expirada ou revogada; code `SESSION_REQUIRED`.

**403** — Ator sem `financeiro.ler` (professor, suporte, administrador sem o papel financeiro); code `PERMISSION_DENIED`. `commerce` recusa da mesma forma se chamado diretamente.

**404** — Pedido inexistente ou de outra escola; code `ORDER_NOT_FOUND`.

**502** — `commerce` ou `identity` não respondeu; code `COMMERCE_UNAVAILABLE` ou `IDENTITY_UNAVAILABLE`.

**504** — O serviço não respondeu no tempo limite; code `UPSTREAM_TIMEOUT`.
