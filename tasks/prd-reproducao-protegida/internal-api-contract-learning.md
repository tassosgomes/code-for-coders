# API interna de `learning` — aula do aluno

> **Gerado a partir de:** [internal-api-contract-learning.yaml](internal-api-contract-learning.yaml), OpenAPI 3.1.0, versão do contrato **1.1.0** (1.0.0 (autoria, CAP-005) → 1.1.0)  
> **PRD:** [prd.md](prd.md) v1.0, aprovado em 2026-10-03 (`CAP-007`)  
> **Estado:** Aprovado para implementação em 2026-10-03. Este documento é derivado do YAML, que é a fonte única; não o edite à mão.

Registra o acordo para a implementação deste PRD. PRDs posteriores podem evoluí-lo. Catalogação e mecanismos de atualização do acervo são da plataforma.

## Premissas e Decisões

| Decisão | Escolha | Motivo |
|---|---|---|
| Interface | `bff-student` → `learning`, sob `/internal/v1` | Rota privada. |
| Autenticação | JWT de aluno, audiência `learning`, validado localmente | ADR-0003. |
| Decisão de acesso | `learning` → `commerce` com asserção de serviço de `learning` | ADR-0011. **Antecipa** de `CAP-017` para esta entrega a credencial de `learning` (C-02). |
| Conteúdo | Versão vigente do curso; sem `videoId`, sem estado de progresso | RN-R06, RN-R08. |

## Resumo de Operações

| Método | Path | `operationId` | Descrição | Segurança | Status |
|---|---|---|---|---|---|
| `GET` | `/lessons/{lessonId}` | `getStudentLessonInternal` | Obter a aula e a lista de aulas do curso | StudentUserToken | 200, 403, 404, 503, 401 |

## Operações Detalhadas

### `GET /lessons/{lessonId}` — Obter a aula e a lista de aulas do curso

Devolve a aula pedida e o curso **na versão vigente**, para a tela da aula (RF-01, RF-06). Ordem das verificações: (1) a aula existe na versão vigente de um curso publicado da escola do aluno; (2) Matrícula responde que o aluno pode acessar o curso agora (`decideAccessInternal`, cache de até 30 s, falha fechada: BA07, G11). Nada do conteúdo é devolvido antes da decisão positiva (RN-R07). Todas as aulas estão abertas a quem tem direito; não há liberação progressiva (RN-R06, G20).

`operationId`: `getStudentLessonInternal`

| Parâmetro | Em | Obrigatório | Descrição |
|---|---|---|---|
| `lessonId` | path | sim | Identidade estável da aula (RN-C07). Opaca para quem a recebe. |

#### Response 200

A aula e o curso na versão vigente.

Exemplo `aula`:
```json
{
  "lesson": {
    "lessonId": "3f6a1c52-7d84-4b0e-9a13-5c2e8f4d7b60",
    "moduleId": "c1d2e3f4-a5b6-4c7d-8e9f-0a1b2c3d4e5f",
    "title": "Injeção de dependência na prática",
    "position": 2
  },
  "course": {
    "courseId": "9a7e5c31-2b4d-4f68-8e01-6d3c5a7b9f24",
    "title": "APIs com .NET do zero ao deploy",
    "versionNumber": 3,
    "modules": [
      {
        "moduleId": "c1d2e3f4-a5b6-4c7d-8e9f-0a1b2c3d4e5f",
        "title": "Fundamentos",
        "position": 1,
        "lessons": [
          {
            "lessonId": "5c2e8f4d-7b60-4a13-9a13-3f6a1c527d84",
            "title": "Visão geral do curso",
            "position": 1
          },
          {
            "lessonId": "3f6a1c52-7d84-4b0e-9a13-5c2e8f4d7b60",
            "title": "Injeção de dependência na prática",
            "position": 2
          }
        ]
      },
      {
        "moduleId": "d2e3f4a5-b6c7-4d8e-9fa0-1b2c3d4e5f60",
        "title": "Persistência",
        "position": 2,
        "lessons": [
          {
            "lessonId": "7a8b9c0d-1e2f-4a3b-8c4d-5e6f7a8b9c0d",
            "title": "Migrations com Entity Framework",
            "position": 1
          }
        ]
      }
    ]
  }
}
```

#### Response 403

Matrícula respondeu que o aluno não pode acessar o curso agora. `reason` distingue nunca ter tido de ter vencido; nada do conteúdo do curso é revelado.

Exemplo `semAcesso`:
```json
{
  "type": "about:blank",
  "title": "Você não tem acesso a este curso.",
  "status": 403,
  "code": "ACCESS_DENIED",
  "traceId": "00-9f2b5d6c4e7a8b90123456789abcdef0-0123456789abcdef-01",
  "reason": "no-grant"
}
```

Exemplo `vencido`:
```json
{
  "type": "about:blank",
  "title": "Seu acesso a este curso terminou.",
  "status": 403,
  "code": "ACCESS_DENIED",
  "traceId": "00-9f2b5d6c4e7a8b90123456789abcdef0-0123456789abcdef-01",
  "reason": "grant-ended",
  "accessEndedAt": "2026-10-04T03:00:00Z"
}
```

#### Response 404

Aula inexistente, de outra escola, de curso não publicado ou removida da versão vigente. A resposta **não distingue** o motivo (RN-R07).

Exemplo `indisponivel`:
```json
{
  "type": "about:blank",
  "title": "Esta aula não está disponível.",
  "status": 404,
  "code": "LESSON_NOT_AVAILABLE",
  "traceId": "00-9f2b5d6c4e7a8b90123456789abcdef0-0123456789abcdef-01"
}
```

#### Response 503

Matrícula não respondeu a tempo ou respondeu com erro. **Não é negação** (RN-R02, DP-08): nenhuma sessão abre nem se estende. O cliente oferece *Tentar de novo*.

Exemplo `indisponivel`:
```json
{
  "type": "about:blank",
  "title": "Não foi possível confirmar seu acesso agora.",
  "status": 503,
  "code": "ACCESS_DECISION_UNAVAILABLE",
  "traceId": "00-9f2b5d6c4e7a8b90123456789abcdef0-0123456789abcdef-01"
}
```

#### Response 401

JWT de aluno ausente, inválido, de outra audiência ou expirado. Autenticação por JWT com a audiência do serviço dono (ADR-0003); nenhuma consulta a Identity.

Exemplo `tokenInvalido`:
```json
{
  "type": "about:blank",
  "title": "Token do aluno ausente, inválido ou expirado.",
  "status": 401,
  "code": "TOKEN_INVALID",
  "traceId": "00-9f2b5d6c4e7a8b90123456789abcdef0-0123456789abcdef-01"
}
```

---
