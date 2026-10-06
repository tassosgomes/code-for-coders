# API interna de Identity (recorte)

> **Gerado a partir de:** [internal-api-contract-identity.yaml](internal-api-contract-identity.yaml), OpenAPI 3.1.0, versão do contrato **1.5.0** (1.4.0 → 1.5.0)  
> **PRD:** [prd.md](prd.md) v1.0, aprovado em 2026-10-05 (`CAP-011`)  
> **Estado:** Aprovado para implementação em 2026-10-05. Documento derivado do YAML, que é a fonte única; não o edite à mão.

Registra o acordo para a implementação deste PRD. PRDs posteriores podem evoluí-lo. Catalogação e mecanismos de atualização do acervo são da plataforma.

## Premissas e Decisões

| Decisão | Escolha | Motivo |
|---|---|---|
| Resolução de alunos | `bff-admin`, `student-account:resolve`, `financeiro.ler` | C-09. |
| Contato de entrega | `notification`, `student-contact:read` | C-06; ADR na TechSpec. |

## Resumo de Operações

| Método | Path | `operationId` | Descrição | Segurança | Status |
|---|---|---|---|---|---|
| `POST` | `/internal/v1/student-account-resolutions` | `resolveStudentAccountsInternal` | Resolver nome e e-mail de várias contas de aluno | BffAdminServiceAssertion | 200, 400, 401, 403 |
| `GET` | `/internal/v1/student-accounts/{studentId}/contact` | `getStudentContactInternal` | Obter o contato de entrega de uma conta de aluno | NotificationServiceAssertion | 200, 401, 403, 404 |

## Operações Detalhadas

### `POST /internal/v1/student-account-resolutions` — Resolver nome e e-mail de várias contas de aluno

Para a lista e o detalhe de pedidos no backoffice (RF-11). Recebe até 50 `studentId` (o tamanho máximo de página) e devolve um item por conta de aluno encontrada no tenant; identificador inexistente, de outro tenant ou de conta interna é **omitido**, sem erro. Conta desativada é devolvida com `status: disabled` (RN-23). Leitura sem efeito; usa POST para não pôr identificadores em URL e permitir o lote.

Exige asserção do `bff-admin` com escopo `student-account:resolve` e `X-Staff-Session` de ator com `financeiro.ler` vigente: Identity valida a sessão (RN-17) e a permissão. A resposta não é guardada pelo BFF além da requisição.

| Parâmetro | Em | Obrigatório | Descrição |
|---|---|---|---|
| `X-Staff-Session` | header | sim | ID opaco da sessão interna; Identity valida vigência, tenant e a permissão `financeiro.ler`. |

**Corpo** — `pagina`

```json
{
  "studentIds": [
    "9d8c7b6a-5f4e-4d3c-8b2a-1f0e9d8c7b6a",
    "2b3c4d5e-6f70-4a81-9b92-a3b4c5d6e7f8"
  ]
}
```

**200** — As contas de aluno encontradas.

Exemplo `duas`:

```json
{
  "data": [
    {
      "studentId": "9d8c7b6a-5f4e-4d3c-8b2a-1f0e9d8c7b6a",
      "name": "Ana Souza",
      "email": "ana.souza@example.com",
      "status": "active"
    },
    {
      "studentId": "2b3c4d5e-6f70-4a81-9b92-a3b4c5d6e7f8",
      "name": "Joana Ribeiro",
      "email": "joana.ribeiro@example.com",
      "status": "disabled"
    }
  ]
}
```

**400** — Pedido inválido (lista vazia, acima de 50 ou com repetidos); code VALIDATION_ERROR.

**401** — Asserção inválida, ou sessão interna ausente ou revogada; code SERVICE_UNAUTHORIZED ou SESSION_REQUIRED.

**403** — Asserção sem `student-account:resolve` ou ator sem `financeiro.ler`; code PERMISSION_DENIED.

### `GET /internal/v1/student-accounts/{studentId}/contact` — Obter o contato de entrega de uma conta de aluno

Para Notificação entregar um pedido de envio endereçado **à conta** (C-06): e-mail e nome da conta, no tenant da asserção. Conta inexistente, de outro tenant ou interna → 404 indistinto. Conta desativada → 200 com `status: disabled`, e Notificação **não** entrega (registra a falha como destinatário indisponível).

Exige asserção de `notification` com escopo `student-contact:read`. Notificação não guarda a resposta além do Registro de Entrega que ela já mantém (RN-26); o e-mail nunca vai para log, URL, métrica ou chave de cache.

| Parâmetro | Em | Obrigatório | Descrição |
|---|---|---|---|
| `studentId` | path | sim | Conta de aluno, por referência. |

**200** — Contato da conta.

Exemplo `ativa`:

```json
{
  "studentId": "9d8c7b6a-5f4e-4d3c-8b2a-1f0e9d8c7b6a",
  "email": "ana.souza@example.com",
  "name": "Ana Souza",
  "status": "active"
}
```

**401** — Asserção ausente, inválida, expirada ou repetida; code SERVICE_UNAUTHORIZED.

**403** — Asserção sem `student-contact:read` ou de emissor sem permissão para o tenant; code PERMISSION_DENIED.

**404** — Conta de aluno inexistente, de outro tenant ou interna — indistintas (G07); code STUDENT_ACCOUNT_NOT_FOUND.
