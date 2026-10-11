# API Contract — Identity, tomador pela sessão

> **Gerado a partir de:** `tasks/prd-emissao-nfse/prd.md`  
> **Data:** 2026-10-11  
> **Status:** Em Revisão  
> **Versão do contrato:** 1.3.0 de Identity aluno  
> **Fonte técnica:** [internal-api-contract-identity-student.yaml](internal-api-contract-identity-student.yaml)

## Premissas

| Decisão | Escolha |
|---|---|
| Chamador | Asserção do `bff-student` |
| Conta | A da `sessionId` |
| Audiência nova | `validateStudentSessionInternal` passa a emitir JWT `billing` / `fiscal-document:own`, sem e-mail. A operação não é redefinida neste arquivo |

## Endpoints

| Método | Path | Descrição | Status |
|---|---|---|---|
| `POST` | `/student-tax-document-reads` | Ler pela sessão. Ausência é `null` | 200, 400, 401 |
| `PUT` | `/student-tax-documents` | Gravar. Dígito inválido não grava | 200, 400, 401, 422 |

Valor novo publica `identidade.identificacao-fiscal-informada.v1` sem o documento no payload. O mesmo valor não publica de novo.
