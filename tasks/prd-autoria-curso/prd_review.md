# Revisão full — PRD Autoria de curso (CAP-005)

Run: run.z20UhGJS

Resultado: FULL VALIDATION APROVADA
Gate: passed
Modo: full · attempt 3/3 · Branch: feature/prd-autoria-curso
base_ref: 79ad0331124457b75e192afa0f261d8673022ee5
validated_commit: 5aef6be23ee7b6d9f0d572be6828551b45b46539
validated_tree: 7b809404352bc564570fe7533aff3c2e2aec9cbe
HEAD e código inalterados durante a revisão (nenhuma edição do validator; `publish/` gerados pelo
gate foram removidos; working tree só com artefatos operacionais do fluxo, iguais à linha de base
inicial: `runs.jsonl` modificado + `flow-state.json`/`prd_review.md` untracked).

## Escopo e rastreabilidade

- Diff desde a base: 336 arquivos, +12765/−135 (inalterado desde a attempt 2). Componentes:
  `src/learning`, `src/admin-spa`, `src/bff-admin`, `src/media`, `src/identity`, `src/audit`,
  mais `docker-compose*.yml`, `docs/` (rollout), `scripts/`. Produção idêntica à attempt 2
  (só 2 arquivos de teste mudaram desde a attempt 1: fixes B-01/B-02).
- Contratos (`api-contract.yaml`, `internal-api-contract-learning.yaml`, 3 AsyncAPI,
  `contracts.md`, `prd.md`, `techspec.md`) inalterados — só comportamento contra C-01 a C-11.
- RF→task→evidência: RF-01/02/05→2.0, RF-03→3.0, RF-04→4.0, RF-06/10/11/12→5.0, RF-07/08→6.0,
  RF-09→7.0. Semântica por fatia nos 6 `N_task_review.md` + 2 revalidações (todos APROVADA);
  esta full confere regressões agregadas, paridade de checks e cobertura.
- Segurança/arquitetura: produção idêntica à attempt 1 (amostrada lá sem achado bloqueante).
  Sem achado novo. Jobs de segurança do CI em `observe` (não bloqueantes por construção).

## Matriz de evidência (fonte CI → comando local → resultado)

CI chamadores lidos: `identity/learning/media/audit/bff-admin.yml` → `ci-dotnet.yml@v1`
(restore, format, `dotnet test --configuration Debug --coverage --coverage-output-format cobertura`,
cobertura agregada por união de linhas ≥70%, publish Release, imagem; security observe) e
`admin-spa.yml` → `ci-react-ts.yml@v1` (lint, tsc, Vitest cobertura linhas ≥70%, build
`--base=/admin/`, imagem). Reutilizáveis externos lidos na íntegra (raw GitHub); parâmetros
extraídos dos chamadores. Cobertura .NET apurada com o algoritmo de união do próprio template.

| Componente | Comandos executados | Resultado |
|---|---|---|
| identity | format 0; `dotnet test` full Debug + cobertura 127/127 (Unit+Arch+Integ 96+E2E); cobertura união **79,63%** (8962/11255); publish Release 0 | passou (≥70%) |
| learning | format 0; full 87/87 (Integ 66); cobertura união **94,02%** (5202/5533); publish Release 0 | passou (≥70%) |
| media | format 0; full 119/119 (Integ 95); cobertura união **90,49%** (6184/6834); publish Release 0 | passou (≥70%) |
| audit | format 0; full 96/96 (Integ 71); cobertura união **81,18%** (2812/3464); publish Release 0 | passou (≥70%) |
| bff-admin | format 0; full 135/135 (Integ 50); cobertura união **82,85%** (3966/4787); publish Release 0 | passou (≥70%) |
| admin-spa | eslint 0 (1 warning pré-existente `single-choice-form`); tsc 0; Vitest cheia 126/126 em 27 arquivos; cobertura linhas **88,62%** (1527/1723, json-summary); `vite build --base=/admin/` 0 (warning chunk >500 kB pré-existente) | passou (≥70%) |
| imagens | `docker build` local: identity, learning, media, audit, bff-admin (contexto raiz, como o CI) + admin-spa (contexto `src/admin-spa`, como o CI) | 6/6 exit 0 |
| geral | `git diff --check` base..HEAD: 1 whitespace (`app-routes.tsx:71` blank line at EOF) | não bloqueante (recomendação) |

Total .NET: 564 testes (Unit+Architecture+Integration+E2E), 0 falhas em execução limpa final.
Nenhum filtro vazio; nenhuma divergência saída × gate_expect.

Notas de execução (transientes, resolvidos com exit 0 em repetição limpa, sem mudança de código):
- Identity full (4 assemblies em paralelo): 1 erro em IntegrationTests na 1ª execução; isolado
  passa e 2 fulls seguintes passam 127/127. Flakiness de carga, mesmo padrão da nota SPA da attempt 2.
- Learning publish: `MSB4018 GenerateMvcTestManifestTask` quando rodado em paralelo ao publish do
  identity; repetição sequencial exit 0. Contenção local, não defeito do build.
- `npm ci` não reexecutado (node_modules pré-existente, como nas attempts anteriores); lint/tsc/
  testes/build verdes sobre as deps instaladas.

## Sensor de discriminação

Não repetido nesta attempt por decisão registrada no `context.txt`: 4/4 mutantes mortos na
attempt 2 (M1 `Course.cs:178`, M2 `CourseReferenceStore.cs:17`, M3 `DeleteCourse.cs:30`,
M4 `CourseEditSession.cs:48`) sobre a mesma árvore de produção (HEAD `5aef6be`, tree `7b809404`
idênticos; desde a attempt 1 só testes B-01/B-02 mudaram). Nenhum mutante sobrevivente;
nenhuma task de correção de teste necessária.

## Bloqueantes

Nenhum. Zero falhas essenciais. Cobertura agregada ≥70% comprovada nos 6 componentes
(lacuna que recusou a attempt 2, fechada com números acima).

## Recomendações (não bloqueantes, 2 — herdadas)

1. `git diff --check` aponta blank line at EOF em `src/admin-spa/src/app/app-routes.tsx:71` —
   remover em higiene futura.
2. Warning de chunk >500 kB no build do SPA e warning de lint em `single-choice-form.tsx`
   (pré-existentes) — higiene futura.

## Veredito

**FULL VALIDATION APROVADA.** Suítes agregadas verdes (564 .NET + 126 SPA), cobertura agregada
≥70% nos 6 componentes com o método do CI, 6/6 imagens construídas localmente, publish Release
5/5, regressões B-01/B-02 eliminadas, sensor 4/4 herdado sobre árvore idêntica, sem mudança de
produção e sem alteração de contratos.
