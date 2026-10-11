# API Contract — Billing, documento fiscal (interno)

> **Gerado a partir de:** `tasks/prd-emissao-nfse/prd.md`  
> **Data:** 2026-10-11  
> **Status:** Em Revisão  
> **Versão do contrato:** 1.1.0 de `billing` HTTP interno  
> **Fonte técnica:** [internal-api-contract-billing.yaml](internal-api-contract-billing.yaml)

`ensurePaymentSessionInternal` permanece na 1.0.0 e não está neste recorte. Os BFFs repassam estas operações.

## Premissas

| Decisão | Escolha |
|---|---|
| Aluno | JWT audiência `billing`, escopo `fiscal-document:own`, sem e-mail e sem `permissions` |
| Financeiro | JWT de ator audiência `billing`. Leitura `financeiro.ler`, escrita `fiscal.gerir` |
| Versionamento | `/internal/v1` |
| Um documento | Por `orderId`. Nova tentativa não cria outro |

## Endpoints

| Método | Path | Quem | Status |
|---|---|---|---|
| `GET` | `/my-fiscal-documents/by-order/{orderId}` | Aluno | 200, 401, 403, 404 |
| `GET` | `/fiscal-documents/by-order/{orderId}` | Financeiro | 200, 401, 403, 404 |
| `POST` | `/fiscal-document-lookups` | Financeiro | 200, 400, 401, 403 |
| `GET` | `/fiscal-documents/by-order/{orderId}/pdf` | Aluno ou financeiro | 200, 401, 403, 404 |
| `GET` | `/fiscal-documents/by-order/{orderId}/xml` | Aluno ou financeiro | 200, 401, 403, 404 |
| `POST` | `/fiscal-documents/by-order/{orderId}/retries` | `fiscal.gerir` | 200, 401, 403, 404, 422 |
| `GET` | `/service-provider` | `financeiro.ler` | 200, 401, 403 |
| `PUT` | `/service-provider` | `fiscal.gerir` | 200, 400, 401, 403, 422 |

Prioridade: sem tomador válido → `awaiting-holder`, mesmo sem prestador. Com tomador e sem prestador → `awaiting-provider`. A visão do aluno transforma esses dois últimos estados de espera do emissor, e `queued`, em `issuing`.

Ausência de resposta do emissor: três vezes, depois `failed`. Código de serviço, chave recusada e cota: `failed` na hora. Submissão abandonada que autorize depois não é adotada.
