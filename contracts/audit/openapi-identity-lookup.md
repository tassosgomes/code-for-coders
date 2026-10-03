# API interna de Identity — referências da trilha

> Derivado de [internal-api-contract-identity.yaml](internal-api-contract-identity.yaml) 1.0.0, OpenAPI 3.1.0, em 2026-09-27. Estado: **Aprovado para implementação**.

| Operação | Método e caminho | Consumidor | Resultado |
|---|---|---|---|
| `resolveAuditIdentityReferencesInternal` | `POST /internal/v1/audit-identity-reference-lookups` | `bff-admin` | `200` com um resultado por referência distinta, na ordem enviada |

O BFF envia até 50 referências `{type, id}` de `conta-interna` ou `convite-interno` lidas de Auditoria. A chamada exige asserção curta de `bff-admin` com escopo `audit-references:read` e `X-Staff-Session`. Identity valida ambos, o tenant e o papel `administrador`. Conta desativada pode conservar identificação legível. Para ID inexistente ou de outro tenant, a resposta mantém só `{type, id}` sem `label`, sem indicar qual caso ocorreu. O BFF não persiste o rótulo na Auditoria.

Erros: `400 VALIDATION_ERROR`, `401 SERVICE_UNAUTHORIZED` ou `SESSION_REQUIRED`, `403 PERMISSION_DENIED`. Os schemas e exemplos normativos estão em [internal-api-contract-identity.yaml](internal-api-contract-identity.yaml).
