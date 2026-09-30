# Revisão full — Nível e pré-requisito do curso (CAP-005, 2º PRD)

Run: run.03Ldwp6t

Resultado: **FULL VALIDATION APROVADA** · Bloqueantes: **0** · Recomendações: **5**

| Campo | Valor |
|---|---|
| base_ref | `b9fd745a39017aa9da1d684bc43149b5f6ec3d9b` |
| validated_commit | `c207ba499422ddbbb1b997571f12bb6b66313414` |
| validated_tree | `3a172a420e808f81b9f381511b66a11e344486d2` |
| Branch | `feature/nivel-prerequisito-curso` (6 checkpoints, tasks 1.0–6.0 concluídas) |
| Escopo | `git diff b9fd745..HEAD`: 119 arquivos; `learning`, `bff-admin`, `admin-spa`, `Directory.Packages.props`, docs de tasks. `src/media` e os YAMLs de contrato **sem diff** |
| Estabilidade | HEAD, árvore e `git status --porcelain` idênticos no início e no fim (só `runs.jsonl` e `flow-state.json`, estado operacional). Worktree do sensor removido; imagens locais e `publish/` temporários removidos |

## Correção ao contexto da chamada

O contexto afirma que `src/media` não dispara checks. **Dispara:** `Directory.Packages.props` (+`JsonSchema.Net 9.4.0`, `YamlDotNet 18.1.0`, na seção Tests) casa `Directory.*` nos `paths` de todos os workflows .NET. Por isso a matriz abaixo cobre também `media`, `identity`, `commerce`, `audit`, `notification` e `bff-student`, executados localmente com os mesmos comandos.

## Matriz de evidência (CI = `learning.yml`, `bff-admin.yml`, `admin-spa.yml`, `media.yml` e os demais → `ci-dotnet.yml@v1` / `ci-react-ts.yml@v1`, lidos via API do GitHub na ref `v1`)

Todos os passos rodaram em primeiro plano sobre o commit/árvore acima; exit code registrado.

| Componente | restore / `npm ci` | format / lint | tsc | test (Debug, `--coverage`) | cobertura (mín. 70) | publish / build | imagem (Dockerfile do CI) |
|---|---|---|---|---|---|---|---|
| `src/learning` | 0 | 0 | — | 0 · 147 testes, 0 falhas (Testcontainers PG+RabbitMQ) | 95,53% | 0 | 0 |
| `src/bff-admin` | 0 | 0 | — | 0 · 145 testes, 0 falhas | 82,89% | 0 | 0 |
| `src/admin-spa` | 0 | 0 (`eslint .`) | 0 | 0 · 147 testes / 30 arquivos (Vitest) | linhas 89,03% (métrica do CI) | 0 (`npm run build -- --base=/admin/`) | 0 |
| `src/media` | 0 | 0 | — | 0 · 138 | 90,67% | 0 | 0 |
| `src/identity` | 0 | 0 | — | 0 · 127 | 79,63% | 0 | 0 |
| `src/commerce` | 0 | 0 | — | 0 · 20 | 78,41% | 0 | 0 |
| `src/audit` | 0 | 0 | — | 0 · 96 | 81,18% | 0 | 0 |
| `src/notification` | 0 | 0 | — | 0 · 51 | 85,45% | 0 | 0 |
| `src/bff-student` | 0 | 0 | — | 0 · 131 | 83,56% | 0 | 0 |

Cobertura .NET calculada com o mesmo algoritmo do `ci-dotnet.yml` (união de linhas dos relatórios Cobertura). `dotnet publish` dos serviços de fora do escopo usou `--output` fora do repositório (única diferença do CI). `student-spa` não tem workflow acionado por este diff.

**Contratos:** `api-contract.yaml`, `internal-api-contract-learning.yaml`, `asyncapi-contract.yaml` e `contracts.md` não mudaram desde a base; o lint registrado permanece válido, sem revalidação. O oráculo `VersaoPublicadaPayload` 1.1.0 é exercitado pelos testes de `learning` (`OutboxFactMatchesAsyncApiIncludingExplicitNullsAndOrderedReferences`, `PublishedCourseContract`).

**Não reproduzíveis / observacionais (não chamados de espelho do CI):** SAST, gitleaks, Trivy, SBOM e push de imagem ao GHCR (`security-mode: observe`, exigem runner/registro); upload de artifact. **Compose:** não reexecutado nesta full; usei a evidência focused de `run.odCXWgRd` (5.0: Media consome fato 1.1.0, DLQ 0) e `run.KMRW9ZB6` (6.0: 17 cursos → 17 mensagens, 17 confirmações, DLQ 0, reinício sem reenvio), cujo código não mudou depois, mais leitura de `PublishedCourseFact.Parse` da Media (lê só `eventId`, `tenantId`, `courseId`, `versionNumber`, `publishedAt`, `modules[].lessons[].lessonId/videoId`, exige `MessageId == eventId`; ignora campos novos).

## Rastreabilidade (TechSpec/PRD × código × testes)

| Requisito | Evidência |
|---|---|
| RF-01 nível, RN-C10, RN-C18 | `Course.ValidateChanges`/`Update`, `CourseChangesRequest`; `CourseLevelRuleTests`, `CourseLevelTests`, `authoring-course-level` |
| RF-02 pré-requisito e recomendados, C-04 | `UpdateCourse` (recomendado publicado da escola, não o próprio, ≤5, sem repetição), filtro `title_search`; `CoursePrerequisiteRuleTests`/`Tests`, `CoursePrerequisiteProxyTests`, `authoring-prerequisite` |
| RF-03/RF-05 versão e histórico, C-02 | `CourseVersion.Create` copia nível/pré-requisito; título da época via `GetCurrentReferencesAsync`; `CourseLevelPublicationTests`, `CourseVersionLevelProxyTests`, `authoring-version-level` |
| RF-04 aviso sem nível sem bloquear (DP-01) | `CourseLevelNotice` (editor, janela de publicação, lista); botão de publicar mantido |
| RF-06 fato 1.1.0 e carga inicial (C-01, C-03) | `PublishedCourseFact.FromVersion` (descrição/nível/pré-requisito sempre presentes); `CatalogInitialLoad` (flag desligada por padrão, lock consultivo, transação por curso, marcador), `message_id` no outbox, `RabbitMqPublisher`; `CatalogInitialLoadTests` |
| C-05 impressão/`draftRevision` | `CourseContentFingerprint` com nível e pré-requisito; migration zera `published_fingerprint` |
| Segurança/PII | `autoria.editar` e filtro de tenant (`PublishedIdsAsync` sem `IgnoreQueryFilters`); nenhum log/span/métrica novo com título, texto ou descrição (único log novo: contagem e marcador) |
| Migrations | 4 geradas por `dotnet ef` (Designer + snapshot consistentes), nível dos já publicados não preenchido (Não-Objetivos) |
| Integração entre tasks | campos de 3.0 → 4.0 → 5.0 → 6.0 consistentes; descarte restaura os três campos; `current_level` mantido em publicar e descartar |

## Sensor de discriminação (modo full)

Worktree temporário (`git worktree add --detach`, sem stash), uma mutação de comportamento por critério, suíte focalizada de cada fatia. **13 de 13 mutantes mortos**; worktree removido e linha de base (HEAD, `git status`) confirmada idêntica.

| # | Fatia · mutação (arquivo) | Critério | Teste que falhou |
|---|---|---|---|
| M1 | V-01 · aceita nível `expert` (`Course.cs` ValidateChanges) | nível fora da lista → `FIELD_INVALID` | `InvalidLevelRejectsEntireEdit` |
| M2 | V-01 · impressão ignora nível (`CourseContentFingerprint.cs`) | C-05 | `LevelOnlyEditAndRevertUpdateRevisionWithoutChangingPublishedSnapshot`, `LegacyMigrationResetsFingerprint…` |
| M3 | V-02 · limite 5→6 (`Course.cs`) | ≤5 recomendados | `SelfDuplicateAndMoreThanFiveRecommendationsAreRejected` |
| M4 | V-02 · aceita recomendado não publicado (`UpdateCourse.cs`) | `RECOMMENDED_COURSE_INVALID` | `InvalidRecommendedCourseRejectsWholeRequest`, `OtherTenantAndMissingRecommendation…` (4 falhas) |
| M5 | V-02 · termo de busca sem normalizar (`CourseQueries.cs`) | busca sem acento | `SearchCombinesStatusNormalizesAccents…`, `RenameAndDiscardMaintainTitleSearch` |
| M6 | V-03 · descrição nula no fato (`PublishedCourseFact.cs`) | fato 1.1.0 sempre com `description` | `OutboxFactMatchesAsyncApi…`, `LevelOnlyRepublication…` (3 falhas) |
| M7 | V-03 · versão não copia o nível (`CourseVersion.cs`) | RF-03 | `OutboxFactMatchesAsyncApi…`, `DiscardRestores…` (3 falhas) |
| M8 | V-03 · descarte não restaura o nível (`Course.cs`) | descarte restaura o retrato | `DiscardRestoresPublishedLevel…`, `LegacyVersion…DiscardRestoresEmptyAudience` |
| M9 | V-04 · `MessageId` = `Id` da linha (`RabbitMqPublisher.cs`) | `MessageId` = `eventId` original | `WorkerPublishesOneCurrentVersionPerCourseAcrossAllTenants` |
| M10 | V-04 · marcador de conclusão ignorado (`CatalogInitialLoad.cs`) | execução única | `CompletedMarkerPreventsAnotherRun…`, `ConcurrentHostsReplayEachCourseOnlyOnce` |
| M11 | V-04 · flag `Enabled` ignorada (`CatalogInitialLoad.cs`) | desligada por padrão | `DisabledOrAbsentFlagDoesNotReplayOrCompleteMarker` |
| M12 | BFF · filtro `title` não repassado (`CourseAuthoringEndpoints.cs`) | C-04 | `RealClientForwardsEncodedTitleTogetherWithPublishedStatus` |
| M13 | SPA · aviso de sem nível invertido (`course-level-notice.tsx`) | RF-04 | 3 testes de `authoring-course-level` |

Uma primeira versão de M12 só quebrou a compilação (CS8604) e foi descartada como inválida; refeita como mutação de comportamento (acima). Os logs completos ficam no scratchpad da sessão.

## Bloqueantes

Nenhum.

## Recomendações (não bloqueantes)

1. **`OutboxPublisherWorker.ExecuteAsync`:** uma falha de `CatalogInitialLoad.RunAsync` (ex.: banco indisponível com a flag ligada) propaga e encerra o host antes do polling do outbox. Gatilho: antes de habilitar `CatalogInitialLoad:Enabled` em produção — capturar e logar, ou documentar no runbook que a falha derruba o processo (reinício reexecuta, pois o marcador só é gravado ao final).
2. **`CourseVersionStore.GetCurrentReferencesAsync`:** `titles[id]` lança `KeyNotFoundException` se um recomendado sumir. Hoje inalcançável (OD45: curso publicado não é excluído); reavaliar quando existir exclusão/despublicação.
3. **Larguras de `level` distintas** (`course_versions` varchar 12, `courses` varchar 20): inofensivo com o enum atual (máx. `intermediate`); alinhar se o enum crescer.
4. **Testes do SPA:** `asyncUtilTimeout` 5 s e `testTimeout` 15 s foram ampliados por carga de CPU do runner (`setup.ts`, `vitest.config.ts`); se o CI real ficar lento, investigar em vez de subir de novo.
5. **Decisão de CI a registrar:** `Directory.Packages.props` dispara todos os workflows .NET; a mudança é só de pacotes de teste de `learning`. Nenhum impacto observado (todos verdes), mas vale um PR que diga isso aos revisores.

## Padrões de projeto

Sem pressão concreta: `PublishedCourseFact.FromVersion` já concentra a montagem do fato (publicação e reenvio), o que evita divergência entre os dois caminhos. Manter; gatilho para extrair abstração seria um segundo tipo de fato reenviável.
