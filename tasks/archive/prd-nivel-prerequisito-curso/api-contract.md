# API — `admin-spa` → `bff-admin`: nível e pré-requisito do curso

> Derivado de [api-contract.yaml](api-contract.yaml), versão 1.1.0, OpenAPI 3.1.0. PRD: [prd.md](prd.md) v1.0. Estado: Aprovado para implementação em 2026-09-30.

Recorte aditivo sobre a autoria 1.0.0 ([../prd-autoria-curso/api-contract.yaml](../prd-autoria-curso/api-contract.yaml)). Só quatro operações mudam; as outras onze da autoria continuam como estão. Sessão por cookie `staff_session`, CSRF e `Idempotency-Key` nas escritas, erros em `application/problem+json`, paginação `_page`/`_size`: tudo herdado.

## Premissas e decisões

| Decisão | Escolha | Motivo |
|---|---|---|
| Nível | `level`: `beginner`, `intermediate`, `advanced` ou `null` | RN-C18; `null` é "sem nível" e é válido (DP-01 do PRD) |
| Pré-requisito | `prerequisiteText` (≤ 1 000) e `recommendedCourseIds` (≤ 5, ordem do professor) | DP-02 do PRD |
| Limpar valor | `null` em `level`/`prerequisiteText`; `[]` em `recommendedCourseIds` | Campo ausente continua significando "não muda" |
| Ausência na leitura | Sempre explícita: `level: null`, `prerequisite.text: null`, `recommendedCourses: []` | RF-06 |

## Operações que mudam

| Método | Path | Mudança | Status |
|---|---|---|---|
| `GET` | `/courses` | Filtro `title` (seletor de recomendados, RF-02); `currentLevel` em cada item (RF-04) | 200, 400, 401, 403, 502, 504 |
| `GET` | `/courses/{courseId}` | `level`, `prerequisite` do rascunho; `currentLevel` da versão vigente | 200, 401, 403, 404, 502, 504 |
| `PATCH` | `/courses/{courseId}` | Aceita `level`, `prerequisiteText`, `recommendedCourseIds`; novo 422 `RECOMMENDED_COURSE_INVALID` e `FIELD_INVALID` | 200, 400, 401, 403, 404, 422, 502, 504 |
| `GET` | `/courses/{courseId}/versions/{versionNumber}` | `level` e `prerequisite` como publicados; recomendados com o título da época (RF-05) | 200, 401, 403, 404, 502, 504 |

`draftRevision` e `hasUnpublishedChanges` passam a considerar nível e pré-requisito, de modo que publicar e descartar (C-03 de CAP-005) continuam protegendo o que o professor viu.

Esquemas, respostas e exemplos têm como fonte o YAML.
