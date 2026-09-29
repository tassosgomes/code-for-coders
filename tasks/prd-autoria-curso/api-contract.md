# API HTTP — autoria de cursos no backoffice

> Derivado de [api-contract.yaml](api-contract.yaml), versão 1.0.0, OpenAPI 3.1.0. Recorte do primeiro PRD de `CAP-005` v1.0 (2026-09-28). Estado: Aprovado para implementação em 2026-09-28.

O SPA do backoffice usa o BFF do backoffice em `/api/v1`, com o cookie `staff_session` de `CAP-002`. Campos, respostas, erros e exemplos têm como fonte o YAML.

| Método | Rota | Operação | Sucesso | Segurança | PRD |
|---|---|---|---|---|---|
| `GET` | `/courses` | `listCourses` — Cursos da escola | 200 | cookie, `autoria.ler` | RF-05 |
| `POST` | `/courses` | `createCourse` — Criar curso | 201 | cookie + CSRF, `autoria.editar` | RF-02 |
| `GET` | `/courses/{courseId}` | `getCourse` — Abrir no editor | 200 | cookie, `autoria.ler` | RF-03, RF-04, RF-07 |
| `PATCH` | `/courses/{courseId}` | `updateCourse` — Título e descrição | 200 | cookie + CSRF, `autoria.editar` | RF-02, RF-07 |
| `DELETE` | `/courses/{courseId}` | `deleteCourse` — Excluir nunca publicado | 204 | cookie + CSRF, `autoria.editar` | RF-09 |
| `POST` | `/courses/{courseId}/modules` | `createModule` — Criar módulo | 201 | cookie + CSRF, `autoria.editar` | RF-03 |
| `PATCH` | `/courses/{courseId}/modules/{moduleId}` | `updateModule` — Renomear ou mover | 200 | cookie + CSRF, `autoria.editar` | RF-03 |
| `DELETE` | `/courses/{courseId}/modules/{moduleId}` | `deleteModule` — Remover com as aulas | 200 | cookie + CSRF, `autoria.editar` | RF-03 |
| `POST` | `/courses/{courseId}/modules/{moduleId}/lessons` | `createLesson` — Criar aula | 201 | cookie + CSRF, `autoria.editar` | RF-03, RF-04 |
| `PATCH` | `/courses/{courseId}/lessons/{lessonId}` | `updateLesson` — Alterar, mover, vincular vídeo | 200 | cookie + CSRF, `autoria.editar` | RF-03, RF-04 |
| `DELETE` | `/courses/{courseId}/lessons/{lessonId}` | `deleteLesson` — Remover aula | 200 | cookie + CSRF, `autoria.editar` | RF-03 |
| `POST` | `/courses/{courseId}/discard-draft` | `discardCourseDraft` — Descartar alterações | 200 | cookie + CSRF, `autoria.editar` | RF-07 |
| `GET` | `/courses/{courseId}/versions` | `listCourseVersions` — Histórico | 200 | cookie, `autoria.ler` | RF-08 |
| `POST` | `/courses/{courseId}/versions` | `publishCourse` — Publicar ou republicar | 201 | cookie + CSRF, `autoria.editar` | RF-06, RF-07 |
| `GET` | `/courses/{courseId}/versions/{versionNumber}` | `getCourseVersion` — Versão como foi publicada | 200 | cookie, `autoria.ler` | RF-07 |

O seletor de vídeo da aula usa `listVideos?status=ready` do contrato de `CAP-006` ([api-contract.yaml](../prd-ingestao-midia/api-contract.yaml) 1.1.1), sem mudança; exige `midia.enviar`, que o professor tem.

## Fluxo do editor

1. `POST /courses` cria o curso em `draft`, com `draftRevision: 1`.
2. Cada alteração de estrutura é uma operação por item e devolve o **curso inteiro** com a nova `draftRevision`. A tela substitui o que mostrava pelo que voltou.
3. `POST /courses/{courseId}/versions` com a `draftRevision` exibida. Se um colega mudou o rascunho nesse meio-tempo, `409 DRAFT_CHANGED`: a tela recarrega e o professor confere antes de publicar. Se faltar algo, `422 COURSE_INCOMPLETE` com `pendencies` (módulo e aula de cada falta); nada é publicado.
4. Publicado, o curso fica `published` com `currentVersion`. Novas alterações marcam `hasUnpublishedChanges: true` até a próxima publicação ou até `discard-draft`.

## Regras de integração

- **Permissão:** leituras exigem `autoria.ler`; escritas, `autoria.editar` (DP-02). O BFF recusa com 403 `PERMISSION_DENIED`, e `learning` recusa de novo com o JWT do ator.
- **Cursos da escola:** todo ator com a permissão vê e edita todos os cursos da escola (RN-C03). Curso, módulo ou aula de outra escola ou de outro curso respondem como inexistentes.
- **Identidade estável:** renomear, reposicionar, mover de módulo e trocar vídeo mantêm `moduleId`/`lessonId`. Só remover e criar gera identificador novo (RN-C07).
- **Posições:** 1-based, contíguas e únicas; o servidor desloca os irmãos. Fora do intervalo, `INVALID_POSITION`.
- **Vídeo:** só vídeo pronto e da escola; do contrário `VIDEO_NOT_AVAILABLE`, sem mudar a aula. Um vídeo que acabou de ficar pronto pode levar alguns instantes para ser aceito (visão local, OD35). `videoId: null` desvincula.
- **Título do vídeo na aula:** vem de Media na hora de `getCourse`; se Media não responder, a aula mostra só o `videoId` e o editor segue funcionando (C-05).
- **Limites (C-07):** título 1–200, descrição até 5 000, nota de versão até 1 000 caracteres; até 100 módulos por curso e 200 aulas por módulo. Acima, `STRUCTURE_LIMIT_REACHED`.
- **Exclusão:** só curso `draft`; publicado responde `409 COURSE_ALREADY_PUBLISHED`.
- **Escritas** exigem `Idempotency-Key` (24 horas) e `X-CSRF-Token`. Duplo clique em publicar devolve a mesma versão.
- **Listagens** paginam com `_page` e `_size` (máximo 50).
- **Erros** seguem RFC 9457 com `code` estável e `traceId`; nenhum erro carrega nome, título ou descrição. `learning` fora do ar → 502 `LEARNING_UNAVAILABLE`; tempo esgotado → 504 `LEARNING_UNAVAILABLE`.

## Exemplo derivado do contrato

`POST /courses/6f1e2d3c-4b5a-4c69-8d7e-1f2a3b4c5d6e/versions` com `{"draftRevision": 14, "versionNote": "Aula 4 regravada com o SDK novo"}` responde 201, `Location: /api/v1/courses/6f1e2d3c-4b5a-4c69-8d7e-1f2a3b4c5d6e/versions/3`, e a versão 3 passa a vigente. Na mesma transação, `learning` publica `conteudo.versao-publicada` e o ato `versao-publicada` em `auditoria.ato-praticado` ([asyncapi-contract.yaml](asyncapi-contract.yaml)).

## Handoff

A chamada BFF → `learning` está em [internal-api-contract-learning.yaml](internal-api-contract-learning.yaml), com o JWT de ator interno de audiência `learning` (ADR-0005). Tipos e mocks podem ser derivados de `api-contract.yaml`. A preservação de identificadores, a conferência de `draftRevision`, a atomicidade versão + fatos e o isolamento por escola exigem testes da implementação.
