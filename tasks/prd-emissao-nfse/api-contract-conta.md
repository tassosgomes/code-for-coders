# API Contract — Documento do tomador (aluno)

> **Gerado a partir de:** `tasks/prd-emissao-nfse/prd.md`  
> **Data:** 2026-10-11  
> **Status:** Em Revisão  
> **Versão do contrato:** 1.1.0 de `bff-student` conta  
> **Fonte técnica:** [api-contract-conta.yaml](api-contract-conta.yaml)

## Premissas

| Decisão | Escolha |
|---|---|
| Autenticação | Cookie de sessão. A conta é a da sessão |
| Escrita | `X-CSRF-Token` e `Idempotency-Key` |
| Documento | CPF e CNPJ só dígitos; NIF texto até 40 caracteres |
| Versionamento | `/api/v1` |

## Endpoints

| Método | Path | Descrição | Auth | Status |
|---|---|---|---|---|
| `GET` | `/student-accounts/current/tax-document` | Ler o documento da conta | Sessão | 200, 401, 502, 504 |
| `PUT` | `/student-accounts/current/tax-document` | Gravar ou substituir | Sessão + CSRF | 200, 400, 401, 403, 422, 502, 504 |

Sem documento, a leitura devolve `taxDocument: null`. Dígito inválido não grava (`TAX_DOCUMENT_INVALID`). O mesmo valor não gera fato novo.

### `PUT` — CPF

```json
{
  "kind": "cpf",
  "value": "52998224725"
}
```
