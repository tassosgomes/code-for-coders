# Revisão full do PRD — Vitrine e oferta de curso (CAP-003, 1º PRD)

Run: run.HWb74vFt
Modo: full · Tentativa: 4/5 · Validator independente (worker fresco)

- base_ref: `5566d027a6c74924f85a84bfa318711e56b15104`
- validated_commit: `80839c5888bd6f1c7240d0b5dbcc22fafc84cfd0`
- validated_tree: `cdb1da05a162ff47a67f386cecb95e20aa65d4bc`
- HEAD, árvore e `git status --porcelain` idênticos antes e depois da revisão (só `tasks/prd-vitrine-oferta/flow-state.json` modificado, já na entrada).

**Resultado: FULL VALIDATION APROVADA** — 0 bloqueantes, 4 recomendações (herdadas).

## Escopo do diff desde a rodada 3 (run.u0ZkoRq9, aprovou `5a34aa9`)

`git diff 5a34aa9..80839c5 -- src`: 2 arquivos, ambos do bff-student.
- `ObservabilityExtensions.cs`: `EnrichWithHttpRequest/Response` remove `user_agent.original`, `http.user_agent`, `client.address`, `client.port`, `http.client_ip` dos spans ASP.NET Core (V-09, RN-O05).
- `PurchaseIntentRateLimitTests.cs`: `HttpSpansNeverRecordVisitorHeadersOrTheIdempotencyKey` passou a filtrar por `traceparent` próprio, forçar `TracerProvider` (a factory remove os hosted services) e aguardar o span servidor parar antes de varrer as tags. Antes dependia de timing.
- Demais arquivos: relatórios, estado e `tasks.md`. Nenhum outro código mudou, então os outros seis componentes mantêm a árvore validada nas rodadas 2 e 3.

## Matriz de verificações (CI: `bff-student.yml` → `ci-dotnet.yml@v1`, `test-configuration: Debug`, `coverage-threshold: 70`)

| Componente | Origem | lint/format | testes | cobertura | build/publish |
|---|---|---|---|---|---|
| **bff-student** | **reexecutado por inteiro** | `dotnet format --verify-no-changes` 0 | `dotnet test --coverage` exit 0 · **167/167** (Unit, Architecture, Integration, E2E todas passam) | **84,66%** (união de linhas dos 4 relatórios Cobertura; mín. 70%) | restore 0 · build 0 (0 erros) · `publish -c Release` 0 |
| bff-student (flake) | IntegrationTests reexecutado 2× adicionais | — | 38/38 e 38/38, exit 0 | — | — |
| bff-student (determinismo) | `HttpSpansNeverRecordVisitorHeadersOrTheIdempotencyKey` isolado 5× | — | 5/5 exit 0 | — | — |
| identity, commerce, audit, bff-admin, student-spa, admin-spa | reaproveitado das rodadas 2 e 3 (árvore de código idêntica; `git diff 5a34aa9..80839c5 -- src` não os toca) | 0 | 132, 232, 127, 193, 63, 205 passam | 79,65 / 95,19 / 81,28 / 83,49 / 85,90 / 90,33% | 0 |

- Não reproduzíveis localmente (observacionais, `security-mode: observe`): push GHCR, login, cache GHA, SBOM, Trivy, SAST, secret scan, scan de dependências, docker build. DAST desligado. Não é espelho completo do CI.
- **Limitação de ambiente externa (conhecida):** o job `Segurança` do workflow commerce falha no step `./.platform/actions/upload-findings` (infra do template; SAST e secret-scan passam, dependency-scan skipped) desde b5aa2a1. Não atribuído ao PR.

## Sensor de discriminação (fatia V-09/11.0)

Worktree temporário `--detach` (sem `git stash`). Controle sem mutação: `PurchaseIntentRateLimitTests` 5/5, exit 0.

| # | Mutação | Critério (11.0) | Resultado |
|---|---|---|---|
| M1 | `EnrichWithHttpRequest/Response` viram no-op (`ObservabilityExtensions.cs:35-36`) | spans sem dados do visitante | **morto** (exit 2: `HttpSpansNeverRecordVisitorHeadersOrTheIdempotencyKey`) |
| M2 | `user_agent.original` removido de `VisitorTags` (`:60`) | spans sem User-Agent | **morto** (exit 2, mesmo teste) |
| M3 | cabeçalho `Retry-After` removido (`PurchaseIntentRateLimitExtensions.cs:36`) | 429 com `Retry-After` | **morto** (exit 2: `LimitIsSharedForTheSameOfferAndIndependentForOtherOffers`) |
| M4 | chave de idempotência gravada crua em vez de SHA-256 (`RegisterPurchaseIntent.cs:20`) | só hash da chave | **morto** (exit 2: `ReceiptStoresOnlyTheHashAndExpiresAfter24Hours`) |

- Mutações das demais fatias (M01–M17 da rodada 1, M12/M12b da 2, M1–M5 da 3) reaproveitadas: código de produção idêntico.
- Descarte: worktree removido e podado; HEAD, árvore e `git status --porcelain` voltaram exatamente à linha de base.
- Teste determinístico: filtra por trace próprio e espera o `Stopped` do span servidor; 5 execuções isoladas e 3 da suíte de integração sem falha.
- Limitação herdada: não mutados o fluxo de Conteúdo (`CatalogCourseView.Apply`/consumidor) e a asserção de serviço BFF→commerce, cobertos pelas revisões focused 3.0 e 9.0. `client.address`/`http.client_ip` em `VisitorTags` não têm mutação própria (o `X-Forwarded-For` do teste é coberto por varredura de valor, não por nome de tag).

## Rastreabilidade e integração

Revisão semântica das rodadas anteriores permanece válida (rotas OpenAPI x implementadas, migrations geradas pelo EF, fato/ato no outbox na mesma transação, área pública anônima com `no-store`/404 uniforme/rate limit, sem segredo versionado). O diff novo fecha o requisito de privacidade do visitante em telemetria (RF-09, RN-O05) sem alterar contrato nem comportamento HTTP.

## Recomendações (não bloqueiam)

1. **Par vazio em `oferta-alterada`** (`src/audit/.../AdministrativeActPolicy.cs:101-113`): gatilho: um segundo produtor.
2. **Accordion vs frame P2.a** (`src/student-spa/src/components/ui/accordion.tsx:25`, `student-course-page.tsx:115`): divergência de apresentação sem quebra de jornada.
3. **M6 sobrevive** (`src/admin-spa/.../format-offer-audit-attribute.ts:4`): acrescentar `precoNovo: '1e3'` ao teste.
4. **3 MAJOR `autoFocus` do SonarCloud**: sem correção autorizada; aceite de risco.

## Veredito

**FULL VALIDATION APROVADA.** bff-student reexecutado por inteiro passa (format, build, 167 testes, cobertura 84,66%, publish; sem flake); sensor sem mutante sobrevivente na fatia de telemetria de visitante; demais componentes com evidência comprovadamente inalterada.
