# API Contract — Documento fiscal do pedido (aluno)

> **Gerado a partir de:** `tasks/prd-emissao-nfse/prd.md`  
> **Data:** 2026-10-11  
> **Status:** Aprovado para implementação  
> **Versão do contrato:** 1.1.0 de `bff-student` compra  
> **Fonte técnica:** [api-contract-student.yaml](api-contract-student.yaml)

O YAML é a fonte. Este texto só resume o recorte.

## Premissas

| Decisão | Escolha |
|---|---|
| Autenticação | Cookie de sessão do aluno. O BFF pede JWT audiência `billing`, escopo `fiscal-document:own` |
| Datas | ISO 8601 UTC |
| Dinheiro | Não aparece nesta visão |
| Versionamento | `servers.url: /api/v1` |
| Arquivo | PDF e XML, exceção C-08. `Cache-Control: no-store` |

## Endpoints

| Método | Path | Descrição | Auth | Status |
|---|---|---|---|---|
| `GET` | `/orders/{orderId}/fiscal-document` | Situação da nota para o aluno | Sessão | 200, 401, 404, 502, 504 |
| `GET` | `/orders/{orderId}/fiscal-document/pdf` | PDF | Sessão | 200, 401, 404, 502, 504 |
| `GET` | `/orders/{orderId}/fiscal-document/xml` | XML | Sessão | 200, 401, 404, 502, 504 |

Pedido de outro aluno e pedido sem nota respondem 404 `FISCAL_DOCUMENT_NOT_FOUND`.

`awaiting-provider` e `queued` chegam como `issuing`. Falha não traz motivo nem ação de tentar de novo.

### `GET /orders/{orderId}/fiscal-document` — nota emitida na bancada

```json
{
  "orderId": "1b2c3d4e-5f60-4718-9a2b-3c4d5e6f7a81",
  "status": "issued",
  "studyDocument": true,
  "downloadsReady": true
}
```
