# API do backoffice — consulta da trilha

> Derivado de [api-contract.yaml](api-contract.yaml) 1.1.0, OpenAPI 3.1.0, em 2026-09-27.  
> PRD: [prd.md](prd.md) v1.0. Estado: **Aprovado para implementação em 2026-09-27**.

O `admin-spa` chama somente o `bff-admin` em `/api/v1`. O cookie opaco de sessão não expõe JWT ao navegador. Em cada ação, o BFF revalida a sessão em Identity e exige papel `administrador`. `audit` repete a decisão para o recurso e tenant na interface interna. Buscas POST e confirmação exigem `X-CSRF-Token`; a confirmação exige também `Idempotency-Key`.

| Operação | Método e caminho | Resultado | Erros de domínio |
|---|---|---|---|
| `listAuditRecords` | `POST /api/v1/audit-record-searches` | `200` com originais e paginação | `400 VALIDATION_ERROR`, `401 SESSION_REQUIRED`, `403 PERMISSION_DENIED` ou `CSRF_INVALID`, `422 AUDIT_FILTER_INVALID` |
| `getAuditRecord` | `GET /api/v1/audit-records/{recordId}` | `200` com original e complementos | `401`, `403`, `404 AUDIT_RECORD_NOT_FOUND` |
| `confirmAuditRecordComplement` | `POST /api/v1/audit-records/{recordId}/complement-confirmations` | `202` com `confirmationId` | `400`, `401`, `403`, `404`, `422 EXPLANATION_REQUIRED` ou `IDEMPOTENCY_CONFLICT` |

## Consulta

`listAuditRecords` recebe no corpo JSON `from` e `to` (instantes inclusivos), `type`, `authorId`, `targetId`, `compliant`, `_page` e `_size` (padrão 10, máximo 50). Nenhum filtro de pessoa aparece na URL. A ordenação é `practicedAt` e ID, ambos decrescentes; se o momento do ato está ausente, `receivedAt` serve apenas para posicionar o registro e `practicedAt` continua `null`. O resultado inclui apenas originais. A primeira página fixa uma visão dos registros elegíveis e devolve `pagination.snapshot`; as demais páginas mandam esse token no corpo com os mesmos filtros e tamanho. O token é opaco. Estado vazio devolve `data: []`.

`getAuditRecord` distingue `practicedAt` de `receivedAt`, mostra conteúdo recebido, faltas, conformidade e complementos posteriores. `author` e `target` podem ser `null` se faltaram na origem. Quando Identity não resolve uma referência, o par `type`/`id` permanece visível e `label` fica ausente. `reason` pode ser `null` para o aceite de convite, que não exige motivo. A lista de complementos é ordenada por `createdAt` e ID crescentes.

## Confirmação

O corpo contém `explanation` de 1 a 1000 caracteres não vazios. O BFF verifica o original no tenant antes de aceitar e usa uma confirmação durável para publicação. `202` indica apenas aceite para envio; o cliente consulta novamente o detalhe até encontrar o complemento por `confirmationId`. A mesma chave de idempotência, no mesmo tenant, ator e operação, com o mesmo `recordId` e corpo retorna o mesmo resultado por 24 horas. Outro `recordId` ou corpo com a mesma chave retorna `422 IDEMPOTENCY_CONFLICT`.

O original, sua conformidade, tipo, autor, alvo e motivo nunca são alterados pelo complemento. Um registro de outro tenant retorna o mesmo `404` que um registro inexistente. Motivo e explicação não vão para URL, log, métrica ou span.

Schemas, campos obrigatórios, exemplos e respostas normativas estão em [api-contract.yaml](api-contract.yaml). Esta página é apenas a leitura do acordo.

A revisão 1.1.0 substitui a busca GET 1.0.0 antes de sua implementação, por decisão do responsável em 2026-09-27: referências de pessoas deixam de trafegar em query string. Não há consumidor implantado dessa operação a migrar.
