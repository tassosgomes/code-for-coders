# API interna de Auditoria — consulta da trilha

> Derivado de [internal-api-contract-audit.yaml](internal-api-contract-audit.yaml) 1.1.0, OpenAPI 3.1.0, em 2026-09-27. Estado: **Aprovado para implementação**.

`bff-admin` chama `audit` em `/internal/v1` com JWT curto emitido por Identity para `audience: audit`. `audit` verifica o JWT localmente por JWKS, exige papel `administrador` e usa o `tenantId` do token em cada consulta. Nenhum header de identidade ou tenant fornecido pelo BFF é fonte de autorização.

| Operação | Método e caminho | Resultado | Erros |
|---|---|---|---|
| `listAuditRecordsInternal` | `POST /internal/v1/audit-record-searches` | `200` com originais e visão paginada estável | `400`, `401 TOKEN_INVALID`, `403 PERMISSION_DENIED`, `422 AUDIT_FILTER_INVALID` |
| `getAuditRecordInternal` | `GET /internal/v1/audit-records/{recordId}` | `200` com original, faltas e complementos | `401`, `403`, `404 AUDIT_RECORD_NOT_FOUND` |

Os filtros e a paginação seguem no corpo da interface pública, inclusive a ordenação por `receivedAt` quando `practicedAt` está ausente. `audit` devolve somente referências `{type, id}`, com campos nulos quando faltaram na origem, nunca nome ou e-mail de Identity. O BFF resolve os rótulos no momento da leitura. O `404` não diferencia ausência de outro tenant. Nenhuma operação interna cria, edita ou exclui registros.

Schemas, parâmetros e exemplos normativos estão em [internal-api-contract-audit.yaml](internal-api-contract-audit.yaml).

A revisão 1.1.0 substitui a busca GET 1.0.0 antes de sua implementação; não há consumidor implantado dessa operação a migrar.
