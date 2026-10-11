# API Contract — Emissor de NFS-e (recorte externo)

> **Consultado em:** `github.com/tassosgomes/fake-nfe` `ac5306e68a8c5b10651a8cceaab691a975109c4a` (2026-10-07)  
> **Data:** 2026-10-11  
> **Status:** Em Revisão  
> **Versão do recorte:** 1.0.0  
> **Fonte técnica:** [provider-contract-notaas.yaml](provider-contract-notaas.yaml)

Este documento não é API da plataforma. O erro do emissor não é `problem+json`. A validação usa `spectral-provider.yaml` (OpenAPI estrutural), não a norma HTTP interna.

A plataforma chama emitir, consultar, PDF e XML. Não chama cancelamento nem cadastro de webhook no caminho da nota. O cadastro do endpoint de webhook é implantação.

`valores.total` vai em reais (49700 centavos → `497.00`). `Idempotency-Key` é `{orderId}-{n}`, só `[A-Za-z0-9_-]`. `referencia` é o `orderId`.

| Método | Path | Sucesso |
|---|---|---|
| `POST` | `/emitir` | 202, nota `queued` |
| `GET` | `/invoices/{invoiceId}/status` | 200 |
| `GET` | `/invoices/{invoiceId}/pdf` | 200 `application/pdf` |
| `GET` | `/invoices/{invoiceId}/xml` | 200 `application/xml` |

Produção e bancada usam os mesmos paths. O host é configuração. QA-03 continua sem conta de produção.
