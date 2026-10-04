# BFF do aluno — meus cursos e progresso

> **Gerado a partir de:** [api-contract.yaml](api-contract.yaml), OpenAPI 3.1.0, versão do contrato **1.1.0** (1.0.0 (`openapi-aula.yaml`, CAP-007) → 1.1.0)  
> **PRD:** [prd.md](prd.md) v1.0, aprovado em 2026-10-04 (`CAP-017`)  
> **Estado:** Aprovado para implementação em 2026-10-04. Este documento é derivado do YAML, que é a fonte única; não o edite à mão.

Registra o acordo para a implementação deste PRD. PRDs posteriores podem evoluí-lo. Catalogação e mecanismos de atualização do acervo são da plataforma.

## Premissas e Decisões

| Decisão | Escolha | Motivo |
|---|---|---|
| Interface | `student-spa` → `bff-student`, sob `/api/v1` | G15: o SPA fala só com o BFF. |
| Autenticação | Cookie `student_session`; o BFF valida a sessão e obtém JWT de aluno com audiência `learning` | ADR-0003, ADR-0013. |
| CSRF | Não exigido: as duas operações são leituras | G18 vale para escritas. |
| Dono do dado | `learning` (Aprendizagem e Progresso); lista de cursos de Matrícula, via `learning` | C-01, C-02. |
| Paginação | Sem paginação; tetos fixos (`maxItems` 500 por lista; 20000 aulas) | C-05; o aviso do Spectral em `/courses/{courseId}/progress` é falso positivo: recurso único, não coleção. |
| Datas | ISO 8601 UTC; `endedOn` é data no fuso da escola | DP-03 de CAP-008. |

## Resumo de Operações

| Método | Path | `operationId` | Descrição | Segurança | Status |
|---|---|---|---|---|---|
| `GET` | `/api/v1/my-courses` | `listMyCourses` | Listar meus cursos | StudentSessionCookie | 200, 401, 503, 502, 504 |
| `GET` | `/api/v1/courses/{courseId}/progress` | `getCourseProgress` | Obter meu progresso no curso | StudentSessionCookie | 200, 401, 403, 404, 503, 502, 504 |

## Operações Detalhadas

### `GET /api/v1/my-courses` — Listar meus cursos

Os cursos do aluno corrente em duas listas (RF-04): `active` (Matrícula responde que há acesso agora) e `ended` (houve concessão e nenhuma vale agora). A lista vem **sempre de Matrícula**; progresso nunca põe curso na lista (RN-P08). Um curso aparece uma vez (RN-D07). `active` vem ordenada pelo avanço mais recente; os nunca começados por último, do acesso mais recente para o mais antigo. `ended` vem do término mais recente para o mais antigo. Se Matrícula não responde, 503 `COURSE_ACCESS_UNAVAILABLE` e **nunca** lista vazia (RF-04). Se só o progresso não está disponível, 200 com `progressAvailable: false`, `progress: null` e `continueLessonId` na primeira aula do curso.

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

**401** — `semSessao` (Sem sessão de aluno válida. O SPA leva ao login e volta à página.)

```json
{
  "type": "about:blank",
  "title": "Sessão ausente, expirada ou revogada.",
  "status": 401,
  "code": "SESSION_REQUIRED",
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

**502** — `indisponivel` (Serviço de destino indisponível.)

```json
{
  "type": "about:blank",
  "title": "Serviço temporariamente indisponível.",
  "status": 502,
  "code": "UPSTREAM_UNAVAILABLE",
  "traceId": "00-9f2b5d6c4e7a8b90123456789abcdef0-0123456789abcdef-01"
}
```

**504** — `tempo` (Serviço de destino não respondeu a tempo.)

```json
{
  "type": "about:blank",
  "title": "Serviço temporariamente indisponível.",
  "status": 504,
  "code": "UPSTREAM_TIMEOUT",
  "traceId": "00-9f2b5d6c4e7a8b90123456789abcdef0-0123456789abcdef-01"
}
```


### `GET /api/v1/courses/{courseId}/progress` — Obter meu progresso no curso

O progresso do aluno corrente no curso, sobre a **versão vigente** (RF-03, RF-05, RF-06): contagem e percentual, e, por aula com algum registro, se está concluída e de onde retomar. Aula sem registro não aparece em `lessons`: não está concluída e começa do início. A tela da aula combina esta resposta com a estrutura de `getStudentLesson`. Exige o mesmo direito que a tela da aula (C-04): `learning` consulta a decisão de acesso antes de responder (cache de até 30 s, falha fechada), e nada do curso é devolvido a quem não pode acessá-lo. Uma falha aqui **não** impede a reprodução: a aula começa do início e a lista fica sem marcas.

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

**401** — `semSessao` (Sem sessão de aluno válida. O SPA leva ao login e volta à página.)

```json
{
  "type": "about:blank",
  "title": "Sessão ausente, expirada ou revogada.",
  "status": 401,
  "code": "SESSION_REQUIRED",
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

**502** — `indisponivel` (Serviço de destino indisponível.)

```json
{
  "type": "about:blank",
  "title": "Serviço temporariamente indisponível.",
  "status": 502,
  "code": "UPSTREAM_UNAVAILABLE",
  "traceId": "00-9f2b5d6c4e7a8b90123456789abcdef0-0123456789abcdef-01"
}
```

**504** — `tempo` (Serviço de destino não respondeu a tempo.)

```json
{
  "type": "about:blank",
  "title": "Serviço temporariamente indisponível.",
  "status": 504,
  "code": "UPSTREAM_TIMEOUT",
  "traceId": "00-9f2b5d6c4e7a8b90123456789abcdef0-0123456789abcdef-01"
}
```

> **Notas para o frontend:** Chamar ao abrir a aula, em paralelo com `getStudentLesson`, e de novo ao pausar, ao chegar ao fim e a cada 60 s de reprodução, para a marca de concluída aparecer sem recarregar (RF-06). `resumeAtSeconds` da aula aberta é a posição inicial do player; acima de 0, mostrar "Retomando de m:ss" com Começar do início.
