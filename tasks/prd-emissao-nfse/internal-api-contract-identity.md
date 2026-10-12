# API Contract — Identity, tomador para o fiscal

> **Gerado a partir de:** `tasks/prd-emissao-nfse/prd.md`  
> **Data:** 2026-10-11  
> **Status:** Aprovado para implementação  
> **Versão do contrato:** 1.6.0 de Identity interno  
> **Fonte técnica:** [internal-api-contract-identity.yaml](internal-api-contract-identity.yaml)

`getStudentContactInternal` não muda. Notificação não passa a ver CPF, CNPJ ou NIF.

## Endpoint

| Método | Path | Quem | Status |
|---|---|---|---|
| `GET` | `/student-accounts/{studentId}/tax-document` | Asserção de `billing`, escopo `student-tax-document:read` | 200, 401, 403, 404 |

Devolve nome, e-mail, situação da conta e o documento, ou `taxDocument: null`. Conta desativada ainda devolve o que houver: a nota da compra já feita não depende da conta seguir ativa. `Cache-Control: no-store`.

A permissão `fiscal.gerir` entra no catálogo do papel financeiro, junto de `financeiro.ler`. Professor e suporte não recebem.
