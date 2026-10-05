# API interna de `learning` — meus cursos e progresso

> **Gerado a partir de:** [internal-api-contract-learning.yaml](internal-api-contract-learning.yaml), OpenAPI 3.1.0, versão do contrato **1.3.0** (1.2.0 → 1.3.0)  
> **PRD:** [prd.md](prd.md) v1.0, aprovado em 2026-10-04 (`CAP-017`)  
> **Estado:** Aprovado para implementação em 2026-10-04. Este documento é derivado do YAML, que é a fonte única; não o edite à mão.

Registra o acordo para a implementação deste PRD. PRDs posteriores podem evoluí-lo. Catalogação e mecanismos de atualização do acervo são da plataforma.

## Premissas e Decisões

| Decisão | Escolha | Motivo |
|---|---|---|
| Interface | `bff-student` → `learning`, sob `/internal/v1` | Rota privada. |
| Autenticação | JWT de aluno, audiência `learning`, escopo `lessons:read`, sem `permissions` | ADR-0013; o mesmo escopo de `getStudentLessonInternal` (C-06). |
| Aluno e escola | `sub` e `tenantId` do token; nunca por parâmetro | ADR-0013 item 5, G07. |
| Chamadas a `commerce` | `listStudentCourseAccessInternal` (lista) e `decideAccessInternal` (progresso), com asserção de serviço de `learning` | C-02, C-03, C-04; ADR-0012. |
| Paginação | Sem paginação; tetos fixos | C-05. |

## Resumo de Operações

| Método | Path | `operationId` | Descrição | Segurança | Status |
|---|---|---|---|---|---|
| `GET` | `/internal/v1/student-courses` | `listStudentCoursesInternal` | Listar meus cursos | StudentUserToken | 200, 401, 503 |
| `GET` | `/internal/v1/student-courses/{courseId}/progress` | `getStudentCourseProgressInternal` | Obter meu progresso no curso | StudentUserToken | 200, 401, 403, 404, 503 |

## Operações Detalhadas

### `GET /internal/v1/student-courses` — Listar meus cursos

Os cursos do aluno (`sub`) em duas listas (RF-04): `active` (Matrícula responde que há acesso agora) e `ended` (houve concessão e nenhuma vale agora). A lista vem **sempre de Matrícula**; progresso nunca põe curso na lista (RN-P08). Um curso aparece uma vez (RN-D07). `active` vem ordenada pelo avanço mais recente; os nunca começados por último, do acesso mais recente para o mais antigo. `ended` vem do término mais recente para o mais antigo. Se Matrícula não responde, 503 `COURSE_ACCESS_UNAVAILABLE` e **nunca** lista vazia (RF-04). Se só o progresso não está disponível, 200 com `progressAvailable: false`, `progress: null` e `continueLessonId` na primeira aula do curso. Composição: `learning` pede a Matrícula os cursos do aluno (`listStudentCourseAccessInternal`, C-02), sem guardar a resposta, e junta título e estrutura da versão vigente (Conteúdo) e o progresso (Aprendizagem). Curso da resposta sem versão vigente na escola é omitido.

**200** — `comProgresso`

```json
{
  "progressAvailable": true,
  "active": [
    {
      "courseId": "9a7e5c31-2b4d-4f68-8e01-6d3c5a7b9f24",
      "title": "APIs com .NET do zero ao deploy",
      "started": true,
      "lastActivityAt": "2026-10-05T14:02:12Z",
      "continueLessonId": "3f6a1c52-7d84-4b0e-9a13-5c2e8f4d7b60",
      "progress": {
        "completedLessons": 3,
        "totalLessons": 8,
        "percent": 37
      }
    },
    {
      "courseId": "3b4c5d6e-7f80-4a91-8b2c-4d5e6f7a8b9c",
      "title": "Fundamentos de C#",
      "started": false,
      "lastActivityAt": null,
      "continueLessonId": "5c2e8f4d-7b60-4a13-9a13-3f6a1c527d84",
      "progress": {
        "completedLessons": 0,
        "totalLessons": 12,
        "percent": 0
      }
    }
  ],
  "ended": [
    {
      "courseId": "6f1e2d3c-4b5a-4c69-8d7e-1f2a3b4c5d6e",
      "title": "Testes automatizados em .NET",
      "endedOn": "2028-03-15",
      "endedReason": "grant-ended",
      "progress": {
        "completedLessons": 5,
        "totalLessons": 10,
        "percent": 50
      }
    }
  ]
}
```

**200** — `semCursos`

```json
{
  "progressAvailable": true,
  "active": [],
  "ended": []
}
```

**200** — `progressoIndisponivel`

```json
{
  "progressAvailable": false,
  "active": [
    {
      "courseId": "9a7e5c31-2b4d-4f68-8e01-6d3c5a7b9f24",
      "title": "APIs com .NET do zero ao deploy",
      "started": null,
      "lastActivityAt": null,
      "continueLessonId": "5c2e8f4d-7b60-4a13-9a13-3f6a1c527d84",
      "progress": null
    }
  ],
  "ended": []
}
```

**401** — `invalido` (JWT ausente, inválido, expirado, com audiência, emissor ou escopo errados, ou com `permissions` (token de ator).)

```json
{
  "type": "about:blank",
  "title": "Token inválido.",
  "status": 401,
  "code": "TOKEN_INVALID",
  "traceId": "00-9f2b5d6c4e7a8b90123456789abcdef0-0123456789abcdef-01"
}
```

**503** — `indisponivel` (Matrícula não respondeu com os cursos do aluno. A tela mostra indisponibilidade com Tentar de novo, **nunca** "Você ainda não tem cursos" (RF-04).)

```json
{
  "type": "about:blank",
  "title": "Não foi possível carregar seus cursos agora. Tente de novo em instantes.",
  "status": 503,
  "code": "COURSE_ACCESS_UNAVAILABLE",
  "traceId": "00-9f2b5d6c4e7a8b90123456789abcdef0-0123456789abcdef-01"
}
```


### `GET /internal/v1/student-courses/{courseId}/progress` — Obter meu progresso no curso

O progresso do aluno (`sub`) no curso, sobre a **versão vigente** (RF-03, RF-05, RF-06): contagem e percentual, e, por aula com algum registro, se está concluída e de onde retomar. Aula sem registro não aparece em `lessons`: não está concluída e começa do início. Ordem das verificações, como em `getStudentLessonInternal`: (1) o curso tem versão vigente na escola do token (404 `COURSE_NOT_AVAILABLE`); (2) Matrícula responde que o aluno pode acessar o curso agora (`decideAccessInternal`, cache de até 30 s, falha fechada: 403/503, C-04). Nada do curso é devolvido a quem não pode acessá-lo. `resumeAtSeconds` aplica RN-P09 com a duração do vídeo que a aula usa na versão vigente, recebida de Media (`midia.ativo-pronto`, C-07); duração ainda desconhecida → `resumeAtSeconds` = `lastPositionSeconds`, salvo avanço de fim.

| Parâmetro | Em | Obrigatório | Descrição |
|---|---|---|---|
| `courseId` | path | sim | Identidade do curso em Conteúdo e Currículo. |

**200** — `emAndamento`

```json
{
  "courseId": "9a7e5c31-2b4d-4f68-8e01-6d3c5a7b9f24",
  "versionNumber": 3,
  "completedLessons": 3,
  "totalLessons": 8,
  "percent": 37,
  "lessons": [
    {
      "lessonId": "5c2e8f4d-7b60-4a13-9a13-3f6a1c527d84",
      "completed": true,
      "lastPositionSeconds": 598,
      "resumeAtSeconds": 0
    },
    {
      "lessonId": "3f6a1c52-7d84-4b0e-9a13-5c2e8f4d7b60",
      "completed": false,
      "lastPositionSeconds": 252,
      "resumeAtSeconds": 252
    }
  ]
}
```

**200** — `naoComecado`

```json
{
  "courseId": "3b4c5d6e-7f80-4a91-8b2c-4d5e6f7a8b9c",
  "versionNumber": 1,
  "completedLessons": 0,
  "totalLessons": 12,
  "percent": 0,
  "lessons": []
}
```

**401** — `invalido` (JWT ausente, inválido, expirado, com audiência, emissor ou escopo errados, ou com `permissions` (token de ator).)

```json
{
  "type": "about:blank",
  "title": "Token inválido.",
  "status": 401,
  "code": "TOKEN_INVALID",
  "traceId": "00-9f2b5d6c4e7a8b90123456789abcdef0-0123456789abcdef-01"
}
```

**403** — `semAcesso` (Matrícula respondeu que o aluno não pode acessar o curso agora. Nada do curso é revelado.)

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

**404** — `indisponivel` (Curso inexistente, de outra escola ou sem versão vigente; indistintos.)

```json
{
  "type": "about:blank",
  "title": "Este curso não está disponível.",
  "status": 404,
  "code": "COURSE_NOT_AVAILABLE",
  "traceId": "00-9f2b5d6c4e7a8b90123456789abcdef0-0123456789abcdef-01"
}
```

**503** — `indisponivel` (Matrícula não respondeu à decisão a tempo. Não é "sem acesso" (RN-R02).)

```json
{
  "type": "about:blank",
  "title": "Não foi possível confirmar seu acesso agora. Tente de novo em instantes.",
  "status": 503,
  "code": "ACCESS_DECISION_UNAVAILABLE",
  "traceId": "00-9f2b5d6c4e7a8b90123456789abcdef0-0123456789abcdef-01"
}
```
