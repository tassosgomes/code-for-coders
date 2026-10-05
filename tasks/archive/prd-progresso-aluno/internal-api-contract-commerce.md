# API interna de `commerce` — cursos do aluno em Matrícula

> **Gerado a partir de:** [internal-api-contract-commerce.yaml](internal-api-contract-commerce.yaml), OpenAPI 3.1.0, versão do contrato **1.3.0** (1.2.0 → 1.3.0)  
> **PRD:** [prd.md](prd.md) v1.0, aprovado em 2026-10-04 (`CAP-017`)  
> **Estado:** Aprovado para implementação em 2026-10-04. Este documento é derivado do YAML, que é a fonte única; não o edite à mão.

Registra o acordo para a implementação deste PRD. PRDs posteriores podem evoluí-lo. Catalogação e mecanismos de atualização do acervo são da plataforma.

## Premissas e Decisões

| Decisão | Escolha | Motivo |
|---|---|---|
| Interface | `learning` → `commerce`, sob `/internal/v1` | Rota privada. |
| Autenticação | Asserção de serviço de `learning`, escopo novo `course-access:read` | C-03; revisão da ADR-0012 na TechSpec. |
| Agregação | Um item por curso; `active` se ao menos uma concessão vale agora | RN-D07, RN-D06. |
| Sem réplica | O consumidor não guarda a resposta; abrir aula segue exigindo a decisão | G11, RN-D01. |
| Paginação | Sem paginação; `maxItems` 500 | C-05; aviso do Spectral registrado como exceção. |

## Resumo de Operações

| Método | Path | `operationId` | Descrição | Segurança | Status |
|---|---|---|---|---|---|
| `GET` | `/internal/v1/course-access` | `listStudentCourseAccessInternal` | Listar os cursos do aluno com acesso vigente ou encerrado | DomainServiceAssertion | 200, 400, 401, 403 |

## Operações Detalhadas

### `GET /internal/v1/course-access` — Listar os cursos do aluno com acesso vigente ou encerrado

Um item por curso sobre o qual o aluno tem ou teve concessão, na escola da asserção (RN-D17). `status` é **calculado na leitura**, como em `decideAccessInternal` (RN-D06, RN-D07): `active` se ao menos uma concessão do curso está ativa e dentro da vigência agora; `ended` se houve concessão e nenhuma vale agora. Para `active`, `since` é o momento da concessão ativa mais recente (ordenação de "meus cursos"); para `ended`, `endedOn` e `endedAt` são os da concessão que terminou por último. Aluno sem concessão, desconhecido ou de outra escola devolve lista vazia, indistinta (G07). A resposta **não é réplica do direito**: `learning` a usa para montar a tela e não a guarda além da requisição; abrir aula continua exigindo `decideAccessInternal` (G11). Quem não obtiver resposta não mostra lista vazia (RF-04 do PRD).

| Parâmetro | Em | Obrigatório | Descrição |
|---|---|---|---|
| `studentId` | query | sim | Conta de aluno em Identidade e Acesso (`sub` do JWT de aluno validado pelo chamador). |

**200** — `vigenteEEncerrado`

```json
{
  "data": [
    {
      "courseId": "9a7e5c31-2b4d-4f68-8e01-6d3c5a7b9f24",
      "status": "active",
      "since": "2026-10-01T12:00:00Z",
      "endedOn": null,
      "endedAt": null,
      "endedReason": null
    },
    {
      "courseId": "6f1e2d3c-4b5a-4c69-8d7e-1f2a3b4c5d6e",
      "status": "ended",
      "since": null,
      "endedOn": "2028-03-15",
      "endedAt": "2028-03-16T03:00:00Z",
      "endedReason": "grant-ended"
    }
  ]
}
```

**200** — `semConcessao`

```json
{
  "data": []
}
```

**400** — `invalido` (`studentId` ausente ou não é UUID.)

```json
{
  "type": "about:blank",
  "title": "Requisição inválida.",
  "status": 400,
  "code": "INVALID_REQUEST",
  "traceId": "00-9f2b5d6c4e7a8b90123456789abcdef0-0123456789abcdef-01"
}
```

**401** — `invalida` (Asserção ausente, inválida, expirada, de emissor desconhecido ou repetida.)

```json
{
  "type": "about:blank",
  "title": "Serviço não autenticado.",
  "status": 401,
  "code": "SERVICE_UNAUTHORIZED",
  "traceId": "00-9f2b5d6c4e7a8b90123456789abcdef0-0123456789abcdef-01"
}
```

**403** — `semEscopo` (Asserção válida sem o escopo `course-access:read` (por exemplo, a de `media`).)

```json
{
  "type": "about:blank",
  "title": "Escopo insuficiente.",
  "status": 403,
  "code": "SCOPE_DENIED",
  "traceId": "00-9f2b5d6c4e7a8b90123456789abcdef0-0123456789abcdef-01"
}
```
