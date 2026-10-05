# Webhook de `billing`

> **Gerado a partir de:** [webhook-contract-billing.yaml](webhook-contract-billing.yaml), OpenAPI 3.1.0, versão do contrato **1.0.0** (novo)  
> **PRD:** [prd.md](prd.md) v1.0, aprovado em 2026-10-05 (`CAP-011`)  
> **Estado:** Aprovado para implementação em 2026-10-05. Documento derivado do YAML, que é a fonte única; não o edite à mão.

Registra o acordo para a implementação deste PRD. PRDs posteriores podem evoluí-lo. Catalogação e mecanismos de atualização do acervo são da plataforma.

## Premissas e Decisões

| Decisão | Escolha | Motivo |
|---|---|---|
| Interface | Gateway → `billing`, sob `/webhooks/v1` | Único ponto público de `billing`; C-05. |
| Autenticação | Assinatura `Stripe-Signature` | RN-CB04. |
| Idempotência | Por `id` do evento | G09. |

## Resumo de Operações

| Método | Path | `operationId` | Descrição | Segurança | Status |
|---|---|---|---|---|---|
| `POST` | `/webhooks/v1/stripe/events` | `receiveGatewayEvent` | Receber um evento do gateway | GatewaySignature | 200, 400, 503 |

## Operações Detalhadas

### `POST /webhooks/v1/stripe/events` — Receber um evento do gateway

Garantias observáveis (mecanismo na TechSpec):

- **Autenticidade**: a assinatura do cabeçalho `Stripe-Signature` é verificada com o segredo do endpoint e com tolerância de horário; assinatura ausente, inválida ou fora da tolerância → 400, nada muda (RF-07, critério de notificação forjada).
- **Idempotência**: o mesmo evento (mesmo `id`) recebido de novo responde 200 sem segundo efeito; nenhum fato `cobranca.*` é publicado duas vezes para o mesmo pagamento e o mesmo desfecho (RN-V09, G09).
- **Registro antes de responder**: 200 só depois de o evento estar gravado; o efeito (tradução e fato) pode ocorrer depois, sem depender de nova entrega do gateway.
- **Tipos não tratados** respondem 200 e são ignorados, para o gateway não insistir.
- **Falha transitória** de `billing` responde 503, e o gateway reenvia pela política dele.

Eventos traduzidos nesta entrega (C-05): página concluída com pagamento confirmado → `cobranca.pagamento-confirmado.v1`; página concluída com PIX ou boleto pendente → `cobranca.pagamento-aguardando.v1`; pagamento assíncrono confirmado → `cobranca.pagamento-confirmado.v1`; pagamento assíncrono vencido e página vencida sem meio escolhido → `cobranca.pagamento-nao-confirmado.v1`. Reembolso e contestação **não** são traduzidos nesta entrega (DP-07): são registrados e ignorados.

**Corpo** — `pagamentoConcluido`

```json
{
  "id": "evt_1Q2w3E4r5T6y7U8i",
  "type": "checkout.session.completed",
  "created": 1791295200,
  "livemode": false,
  "data": {
    "object": {
      "id": "cs_test_a1B2c3D4e5F6g7H8",
      "payment_status": "paid",
      "client_reference_id": "1b2c3d4e-5f60-4718-9a2b-3c4d5e6f7a81"
    }
  }
}
```

**200** — Evento aceito (novo, repetido ou de tipo não tratado).

Exemplo `aceito`:

```json
{
  "received": true
}
```

**400** — Assinatura ausente, inválida ou fora da tolerância de horário, ou corpo sem `id`/`type`; nada muda. code `SIGNATURE_INVALID`. O corpo de erro não ecoa o evento.

**503** — Falha transitória de `billing` antes de gravar o evento; o gateway reenvia. code `TEMPORARILY_UNAVAILABLE`.
