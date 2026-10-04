# API interna de `identity` — JWT de aluno para `media` e `learning`

> **Gerado a partir de:** [internal-api-contract-identity.yaml](internal-api-contract-identity.yaml), OpenAPI 3.1.0, versão do contrato **1.1.0** (1.0.0 (CAP-001) → 1.1.0)  
> **PRD:** [prd.md](prd.md) v1.0, aprovado em 2026-10-03 (`CAP-007`)  
> **Estado:** Aprovado para implementação em 2026-10-03. Este documento é derivado do YAML, que é a fonte única; não o edite à mão.

Registra o acordo para a implementação deste PRD. PRDs posteriores podem evoluí-lo. Catalogação e mecanismos de atualização do acervo são da plataforma.

## Premissas e Decisões

| Decisão | Escolha | Motivo |
|---|---|---|
| Interface | BFF do aluno → `identity`, sob `/internal/v1` | Recorte: só a operação alterada. |
| Mudança | Audiências `media` e `learning`; claim `email` só em `media` | Aditiva (C-04). Clientes que não pedem essas audiências não mudam. |
| Exposição do e-mail | Única exceção declarada, junto com a de Notificação | Identidade RN-21 e RN-26. |

## Resumo de Operações

| Método | Path | `operationId` | Descrição | Segurança | Status |
|---|---|---|---|---|---|
| `POST` | `/student-session-validations` | `validateStudentSessionInternal` | Validar e renovar sessão | BffServiceAssertion | 200, 400, 401, 403 |

## Operações Detalhadas

### `POST /student-session-validations` — Validar e renovar sessão

Sem mudança de comportamento em relação a 1.0.0: valida sessão, tenant, situação da Conta e expiração; renova a inatividade atomicamente; com `audience`, emite JWT de aluno curto só para aquela audiência autorizada. Mudança aditiva deste recorte (C-04): `media` e `learning` passam a ser audiências autorizadas, e o JWT de `media` carrega `email`. Falha fecha acesso.

`operationId`: `validateStudentSessionInternal`

**Request body**

Exemplo `paraMedia`:
```json
{
  "sessionId": "8a9b0c1d-2e3f-4a5b-8c6d-7e8f9a0b1c2d",
  "audience": "media"
}
```

#### Response 200

Sessão ativa com expiração renovada e JWT opcional.

Exemplo `comToken`:
```json
{
  "accountId": "6f1e2d3c-4b5a-4968-8777-a1b2c3d4e5f6",
  "name": "Marina Alves",
  "expiresAt": "2026-10-05T18:00:00Z",
  "accessToken": "eyJhbGciOiJSUzI1NiIsImtpZCI6ImlkMS0yMDI2LTEwIn0.eyJpc3MiOiJpZGVudGl0eSIsImF1ZCI6Im1lZGlhIn0.c2lnbmF0dXJl"
}
```

#### Response 400

JSON ou campo obrigatório inválido.

Exemplo `invalido`:
```json
{
  "type": "about:blank",
  "title": "Requisição inválida.",
  "status": 400,
  "code": "VALIDATION_ERROR",
  "traceId": "00-9f2b5d6c4e7a8b90123456789abcdef0-0123456789abcdef-01"
}
```

#### Response 401

Asserção de serviço inválida, ou sessão ausente, expirada ou revogada; distinguir por `code`.

Exemplo `sessaoRevogada`:
```json
{
  "type": "about:blank",
  "title": "Sessão revogada.",
  "status": 401,
  "code": "SESSION_REVOKED",
  "traceId": "00-9f2b5d6c4e7a8b90123456789abcdef0-0123456789abcdef-01"
}
```

#### Response 403

Audiência de JWT não autorizada para o BFF do aluno.

Exemplo `audiencia`:
```json
{
  "type": "about:blank",
  "title": "Audiência não autorizada.",
  "status": 403,
  "code": "AUDIENCE_NOT_ALLOWED",
  "traceId": "00-9f2b5d6c4e7a8b90123456789abcdef0-0123456789abcdef-01"
}
```

---
