# API interna de `billing`

> **Gerado a partir de:** [internal-api-contract-billing.yaml](internal-api-contract-billing.yaml), OpenAPI 3.1.0, versão do contrato **1.0.0** (novo)  
> **PRD:** [prd.md](prd.md) v1.0, aprovado em 2026-10-05 (`CAP-011`)  
> **Estado:** Aprovado para implementação em 2026-10-05. Documento derivado do YAML, que é a fonte única; não o edite à mão.

Registra o acordo para a implementação deste PRD. PRDs posteriores podem evoluí-lo. Catalogação e mecanismos de atualização do acervo são da plataforma.

## Premissas e Decisões

| Decisão | Escolha | Motivo |
|---|---|---|
| Interface | `commerce` → `billing`, sob `/internal/v1` | C-01; Domain Map: Vendas aciona Cobrança. |
| Autenticação | Asserção de `commerce`, escopo `payment:request` | C-04; ADR na TechSpec. |
| Idempotência | `PUT` por `orderId` | G09; um pagamento aberto por pedido. |
| Camada anticorrupção | Meio e situação no vocabulário da plataforma | RN-CB05. |

## Resumo de Operações

| Método | Path | `operationId` | Descrição | Segurança | Status |
|---|---|---|---|---|---|
| `PUT` | `/internal/v1/payment-sessions/{orderId}` | `ensurePaymentSessionInternal` | Garantir a página de pagamento de um pedido | CommerceServiceAssertion | 200, 400, 401, 403, 422, 503 |

## Operações Detalhadas

### `PUT /internal/v1/payment-sessions/{orderId}` — Garantir a página de pagamento de um pedido

Garante que o pedido tenha um caminho de pagamento e devolve o endereço para onde o aluno deve ir (RF-04, RF-06). **Idempotente por `orderId`** (G09): a mesma chamada repetida não cria um segundo pagamento.

- Sem pagamento ainda, ou com a página anterior ainda válida e nenhum meio escolhido → devolve a página de pagamento (`kind: checkout`), criada uma vez e reaproveitada até vencer (24 h, RN-CB03).
- Com PIX ou boleto gerado e ainda não pago → devolve as instruções desse pagamento (`kind: pix-instructions` ou `boleto-instructions`), para o aluno ver de novo o código ou o boleto. Não abre outro pagamento.
- Página vencida sem meio escolhido → **não** cria outra: responde `PAYMENT_EXPIRED`. O pedido expira por `cobranca.pagamento-nao-confirmado.v1` (RF-08).

`amountCents`, `currency` e `description` precisam ser iguais aos de um pagamento já aberto para o mesmo pedido; divergência é recusada (`PAYMENT_TERMS_CONFLICT`), porque o pedido congela a condição (RN-V03). Os meios oferecidos são cartão, PIX e boleto (RN-CB02); um meio que o gateway não aceite para o valor (mínimo por meio) simplesmente não aparece na página. `successUrl` e `cancelUrl` precisam ser `https` num host autorizado para a escola, ou `http` apenas em host local de desenvolvimento autorizado (errata 1.0.0, D-11 da TechSpec); fora disso, 400.

| Parâmetro | Em | Obrigatório | Descrição |
|---|---|---|---|
| `orderId` | path | sim | Pedido em Vendas e Checkout. É a referência do pagamento em `billing`; um pedido tem no máximo um pagamento aberto. |

**Corpo** — `dozeMeses`

```json
{
  "studentId": "9d8c7b6a-5f4e-4d3c-8b2a-1f0e9d8c7b6a",
  "amountCents": 49700,
  "currency": "BRL",
  "description": ".NET do zero à API — Acesso por 12 meses",
  "successUrl": "https://app.code4coders.com.br/pedidos/1b2c3d4e-5f60-4718-9a2b-3c4d5e6f7a81?resultado=concluido",
  "cancelUrl": "https://app.code4coders.com.br/pedidos/1b2c3d4e-5f60-4718-9a2b-3c4d5e6f7a81?resultado=saiu"
}
```

**200** — Caminho de pagamento do pedido.

Exemplo `pagina`:

```json
{
  "orderId": "1b2c3d4e-5f60-4718-9a2b-3c4d5e6f7a81",
  "kind": "checkout",
  "paymentUrl": "https://checkout.stripe.com/c/pay/cs_test_a1B2c3D4e5F6g7H8",
  "method": null,
  "expiresAt": "2026-10-06T14:20:00Z"
}
```

**400** — Corpo malformado, valor fora dos limites ou `successUrl`/`cancelUrl` fora dos hosts autorizados; code `INVALID_REQUEST`.

**401** — Asserção ausente, inválida, expirada, repetida (`jti`) ou de outro emissor; code `SERVICE_ASSERTION_INVALID`.

**403** — Asserção sem `payment:request` ou de emissor sem permissão para o `tenantId`; code `SCOPE_DENIED`.

**422** — Não há caminho de pagamento para devolver: `PAYMENT_ALREADY_CONFIRMED` (o pedido já foi pago), `PAYMENT_CANCELLED` (o pedido foi cancelado — `vendas.pedido-cancelado.v1` recebido), `PAYMENT_EXPIRED` (página, PIX ou boleto vencidos sem confirmação) ou `PAYMENT_TERMS_CONFLICT` (valor, moeda ou descrição diferentes dos do pagamento já aberto).

**503** — O gateway não respondeu ou recusou a operação por falha dele; nada foi criado. Repetir é seguro (idempotente por `orderId`); code `GATEWAY_UNAVAILABLE`.
