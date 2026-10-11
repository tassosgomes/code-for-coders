# API Contract — Documento fiscal e prestador (backoffice)

> **Gerado a partir de:** `tasks/prd-emissao-nfse/prd.md`  
> **Data:** 2026-10-11  
> **Status:** Em Revisão  
> **Versão do contrato:** 1.1.0 de `bff-admin` financeiro  
> **Fonte técnica:** [api-contract-admin.yaml](api-contract-admin.yaml)

A lista e o detalhe de pedidos de `CAP-011` não mudam. A nota é outra chamada, para uma falha fiscal não derrubar a lista.

## Premissas

| Decisão | Escolha |
|---|---|
| Leitura | `financeiro.ler` |
| Escrita | `fiscal.gerir` (prestador e nova tentativa), com CSRF e `Idempotency-Key` |
| Dinheiro | Centavos de real |
| Alíquota | Texto percentual de 0 a 100, até duas casas (`"2"`, `"2.50"`) |
| Arquivo | PDF e XML, exceção C-08 |
| Lote | Até 50 pedidos, sem paginação (teto da página de pedidos) |

## Endpoints

| Método | Path | Descrição | Auth | Status |
|---|---|---|---|---|
| `POST` | `/finance/fiscal-document-lookups` | Situação de uma página de pedidos | `financeiro.ler` | 200, 400, 401, 403, 502, 504 |
| `GET` | `/finance/orders/{orderId}/fiscal-document` | Nota do pedido | `financeiro.ler` | 200, 401, 403, 404, 502, 504 |
| `GET` | `/finance/orders/{orderId}/fiscal-document/pdf` | PDF | `financeiro.ler` | 200, 401, 403, 404, 502, 504 |
| `GET` | `/finance/orders/{orderId}/fiscal-document/xml` | XML | `financeiro.ler` | 200, 401, 403, 404, 502, 504 |
| `POST` | `/finance/orders/{orderId}/fiscal-document-retries` | Nova tentativa do mesmo documento | `fiscal.gerir` | 200, 400, 401, 403, 404, 422, 502, 504 |
| `GET` | `/finance/service-provider` | Prestador, sem a chave | `financeiro.ler` | 200, 401, 403, 502, 504 |
| `PUT` | `/finance/service-provider` | Chave, código e alíquota | `fiscal.gerir` | 200, 400, 401, 403, 422, 502, 504 |

Pedido sem nota é omitido no lote e 404 no detalhe. A chave do emissor nunca volta.

### Prestador utilizável

```json
{
  "apiKey": "ntaas_escola_estudo_7f3a",
  "serviceCode": "010700",
  "issRate": "2"
}
```

A resposta não inclui `apiKey`. `releasedCount` diz quantas notas que esperavam só o prestador entraram na fila.
