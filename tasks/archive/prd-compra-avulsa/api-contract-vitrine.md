# BFF do aluno — vitrine (recorte)

> **Gerado a partir de:** [api-contract-vitrine.yaml](api-contract-vitrine.yaml), OpenAPI 3.1.0, versão do contrato **1.1.0** (1.0.0 → 1.1.0)  
> **PRD:** [prd.md](prd.md) v1.0, aprovado em 2026-10-05 (`CAP-011`)  
> **Estado:** Aprovado para implementação em 2026-10-05. Documento derivado do YAML, que é a fonte única; não o edite à mão.

Registra o acordo para a implementação deste PRD. PRDs posteriores podem evoluí-lo. Catalogação e mecanismos de atualização do acervo são da plataforma.

## Premissas e Decisões

| Decisão | Escolha | Motivo |
|---|---|---|
| Mudança | `registerPurchaseIntent` depreciada e sem contagem | DP-06, C-08. |

## Resumo de Operações

| Método | Path | `operationId` | Descrição | Segurança | Status |
|---|---|---|---|---|---|
| `POST` | `/api/v1/showcase/offers/{offerId}/purchase-intents` | `registerPurchaseIntent` | Registrar um clique em Comprar (depreciada) *(depreciada)* | pública | 202, 404, 429 |

## Operações Detalhadas

### `POST /api/v1/showcase/offers/{offerId}/purchase-intents` — Registrar um clique em Comprar (depreciada)

**Depreciada em 1.1.0 (DP-06).** O `student-spa` deixa de chamá-la: *Comprar* leva a `getPurchaseSummary`. Quem ainda chamar recebe 202 com `purchaseAvailability: available` e **nada é contado**; a contagem histórica fica como estava na oferta. Oferta não publicada → 404. Remoção em versão futura.

| Parâmetro | Em | Obrigatório | Descrição |
|---|---|---|---|
| `offerId` | path | sim | Oferta publicada. |
| `Idempotency-Key` | header | sim | Mantida por compatibilidade; sem efeito. |

**202** — Nada contado; a compra está disponível.

Exemplo `disponivel`:

```json
{
  "purchaseAvailability": "available"
}
```

**404** — Oferta não publicada, inexistente ou de outra escola — indistintas.

**429** — Limite de requisições da borda excedido.
