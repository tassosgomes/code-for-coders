# Identity — BFF do aluno (recorte)

> **Gerado a partir de:** [internal-api-contract-identity-student.yaml](internal-api-contract-identity-student.yaml), OpenAPI 3.1.0, versão do contrato **1.2.0** (1.1.0 → 1.2.0)  
> **PRD:** [prd.md](prd.md) v1.0, aprovado em 2026-10-05 (`CAP-011`)  
> **Estado:** Aprovado para implementação em 2026-10-05. Documento derivado do YAML, que é a fonte única; não o edite à mão.

Registra o acordo para a implementação deste PRD. PRDs posteriores podem evoluí-lo. Catalogação e mecanismos de atualização do acervo são da plataforma.

## Premissas e Decisões

| Decisão | Escolha | Motivo |
|---|---|---|
| Mudança | Audiência `commerce` autorizada, `orders:use`, sem `email` | C-03. |

## Resumo de Operações

| Método | Path | `operationId` | Descrição | Segurança | Status |
|---|---|---|---|---|---|
| `POST` | `/internal/v1/student-session-validations` | `validateStudentSessionInternal` | Validar e renovar sessão | BffServiceAssertion | 200, 400, 401, 403 |

## Operações Detalhadas

### `POST /internal/v1/student-session-validations` — Validar e renovar sessão

Comportamento inalterado: valida sessão, tenant, situação da Conta e expiração; renova a inatividade atomicamente; com `audience`, emite JWT de aluno curto só para aquela audiência autorizada. Mudança aditiva deste recorte (C-03): `commerce` passa a ser audiência autorizada, com `scope: orders:use` e sem `email`. Falha fecha acesso.

**Corpo** — `paraCommerce`

```json
{
  "sessionId": "8a9b0c1d-2e3f-4a5b-8c6d-7e8f9a0b1c2d",
  "audience": "commerce"
}
```

**200** — Sessão ativa com expiração renovada e JWT opcional.

Exemplo `comToken`:

```json
{
  "accountId": "9d8c7b6a-5f4e-4d3c-8b2a-1f0e9d8c7b6a",
  "name": "Ana Souza",
  "expiresAt": "2026-10-05T16:19:30Z",
  "accessToken": "eyJhbGciOiJSUzI1NiIsImtpZCI6InN0dWRlbnQtMjAyNi0xMCJ9.eyJhdWQiOiJjb21tZXJjZSJ9.c2lnbmF0dXJl"
}
```

**400** — JSON ou campo obrigatório inválido.

**401** — Asserção inválida ou sessão ausente, expirada ou revogada; distinguir por code.

**403** — Audiência de JWT não autorizada para o BFF.
