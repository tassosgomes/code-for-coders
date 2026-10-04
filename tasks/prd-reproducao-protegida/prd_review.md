# Revisão full — PRD reproducao-protegida

Run: run.j4as8nYb
Tentativa: 2/3
Modo: full · delivery=pr

## Resultado: FULL VALIDATION APROVADA (0 bloqueantes, 5 recomendações)

| Item | Valor |
|---|---|
| base_ref | e3d2ed468054f904c3cf6cc41e9bec56d191fa2e |
| validated_commit | 4f9b7760e1d412eced8350f88fad162aa7275764 |
| validated_tree | 1815d59b2add01683e51b42af1e1db3138bbd2be |
| Estabilidade | HEAD, árvore e `git status --porcelain` idênticos antes e depois (inclusive após o sensor); worktree temporário removido |
| Escopo | 248 arquivos, +10890/−113; specs: `techspec.md`, `contracts.md` (e `internal-api-contract-media.yaml`) |
| Working tree | `routing.default.json`, `flow-state.json` (modificados), `scripts/kibana/__pycache__/` e `prompt.txt` (não rastreados): fora do commit, estado operacional/decisão do responsável; não são defeito das tasks |

## Bloqueantes da tentativa 1 — conferência

- **B1 (job `media`, 2 testes quebrados pela validação de startup): resolvido.** `MediaInfrastructureTests.cs:75-77` e `VideoLibraryAuthorizationTests.cs:233-235` fornecem `Preparation:MasterKey`, `Preparation:MasterKeyId` e `Playback:Delivery:SharedSecret` aos hosts de teste; a validação de produção não foi tocada. Suíte completa de `media`: 229/229, exit 0.
- **B2 (códigos de erro fora do contrato): resolvido.** `GetPlaybackResource.cs:15,17,21,25` emite `PLAYBACK_SESSION_NOT_FOUND` (404) e `PLAYBACK_SESSION_EXPIRED` (410); o par legado saiu de `PlaybackMediaClient.cs`; `PlaybackDeliveryTests` agora afirma o `code` e `PlaybackProxyTests` usa os nomes do contrato. Busca em `src/`: nenhuma ocorrência de `SESSION_NOT_FOUND`/`SESSION_EXPIRED` sem o prefixo `PLAYBACK_`.
- Diff desde a tentativa 1 (`2ec1552..HEAD` em `src/`): 6 arquivos, +16/−7, só esses dois ajustes; sem regressão.

## Matriz de CI (espelho local dos workflows reutilizáveis `ci-dotnet.yml@v1` e `ci-react-ts.yml@v1`)

Fonte: chamadores em `.github/workflows/` (`media`, `bff-student`, `student-spa` e demais) e os parâmetros efetivos (`test-configuration: Debug`, `coverage-threshold: 70`, `build-args: --base=/student/`). Testes .NET: `dotnet test --no-restore -c Debug --coverage --coverage-output-format cobertura` na raiz de cada componente, em sequência (sem paralelismo entre componentes). Commit/árvore: os do quadro acima.

| Componente | restore | format (`--verify-no-changes`) | test Debug | cobertura de linhas (união, limite 70) | publish Release |
|---|---|---|---|---|---|
| identity | 0 | 0 | **0** (169/169) | 81,1% | 0 |
| commerce | 0 | 0 | **0** (378/378) | 96,0% | 0 |
| learning | 0 | 0 | **0** (171/171) | 95,7% | 0 |
| media | 0 | 0 | **0** (229/229) | 91,4% | 0 |
| bff-student | 0 | 0 | 2 com `OTEL_SDK_DISABLED=true` (204/205); o único falho (`HttpSpansNeverRecordVisitorHeadersOrTheIdempotencyKey`) **passa isolado com o SDK ligado, exit 0** — ver Limites | 85,8% | 0 |
| student-spa | `npm ci` 0 | `eslint .` 0 · `tsc --noEmit` 0 | `vitest run --coverage` **0** (21 arquivos, 128 testes) | linhas 88,6% · statements 87,0% · branches 71,6% | `vite build --base=/student/` 0 |

Passos adicionais:

| Passo | Comando | Resultado |
|---|---|---|
| Imagem `student-spa` | `docker build -f src/student-spa/Dockerfile src/student-spa` | 0 |
| Imagem `media-edge` | `docker build -f src/media-edge/Dockerfile .` | 0; contêiner executado contra origem stub: sem credencial 403, credencial inválida 403, vencida 410, válida passa à origem (404 do stub, sem o objeto), prefixo de outro vídeo 403; o log de acesso não registra a consulta |
| Compose | `docker compose config -q` (base, `+remote`, `coolify` com variáveis fictícias) | 0 / 0 / 0 |
| Kibana | `python3 scripts/kibana/import_observabilidade.py --verify-only` | 0 |

Limites e passos não reproduzíveis (classificados):

- **Ambiente:** conexões TCP a portas fechadas de `127.0.0.1` ficam penduradas neste sandbox (`/dev/tcp/127.0.0.1/4317` não responde em 3 s), então o exportador OTLP deixa cada teste de integração lento. `media`, `bff-student`, `learning` e `commerce` rodaram com `OTEL_SDK_DISABLED=true` (só ambiente, sem alterar código ou comando); `identity` rodou sem. O teste de span do `bff-student` foi reexecutado isolado (`dotnet test --project tests/CodeForCoders.BffStudent.IntegrationTests --filter-method ...`) com o SDK ligado: 1/1, exit 0. A suíte completa de `media` tinha rodado com SDK ativo no fix da 4.0 (exit 0, registrado na task); aqui o ambiente não permitiu repetir.
- `scripts/remote-infra.sh check` e os cenários de navegador contra o ambiente de desenvolvimento (V-01 a V-04) **não foram executados** nesta rodada: não são jobs de CI, e a rede do sandbox não alcança a infra remota (sem tentativa nova, mesma restrição da tentativa 1).
- Jobs `container` das imagens .NET (identity, commerce, learning, media, bff-student): os `Dockerfile` não mudaram no range; coberto por `dotnet publish -c Release` (exit 0), sem `docker build` dessas imagens. Jobs de segurança (`security-mode: observe`, Semgrep/gitleaks/Trivy, SBOM, push ao GHCR): observacionais ou exclusivos do runner, não executados.
- Cobertura: união de linhas dos relatórios Cobertura locais (mesmo critério do passo `coverage` do template); os valores no GitHub podem diferir pouco.

## Sensor de discriminação (executado)

Isolamento: `git worktree` temporário em `HEAD` (nunca `git stash`, árvore real intocada); linha de base registrada (HEAD + `git status --porcelain`) e conferida idêntica no fim. Controle sem mutação: `PlaybackProgressTests` 15/15, exit 0. Cada mutação rodou com `--filter-class` por classe; só conta falha de teste (build quebrado não conta).

Duas mutações (M2, M5) quebraram o build na primeira passada (`CS8604`/`CS9113`, aviso tratado como erro); foram refeitas de forma que compila e só essas valem.

| ID | Critério | Arquivo — mutação | Testes que falharam |
|---|---|---|---|
| M1 | Decisão negada em `GetStudentLesson` | `GetStudentLesson.cs` — guarda `!= "allowed"` neutralizada | `DeniedStudentSeesReasonAndNoCurriculum` (2 casos), `AccessToOneCourseDoesNotAuthorizeAnotherCourse` |
| M2 | Ausência de e-mail em `OpenPlaybackSession` | `OpenPlaybackSession.cs` — guarda de e-mail vazio removida | `OpeningFailsClosedInDeclaredOrderAndNeverPersistsFailure` (`email`, `blank`) |
| M3 | Renovação de sessão vencida (caso de uso) | `RenewPlaybackSession.cs` — checagem de vencimento removida | `UnknownForeignAndExpiredSessionsNeverConsultCommerce` (`expired`) |
| M3b | Renovação de sessão vencida (domínio) | `PlaybackSession.cs` `TryRenew` — guarda removida | `ExpiredSessionsCannotBeRevived` (300 s, 301 s) |
| M4a | Intervalo mínimo do avanço | `PlaybackSession.cs` — `< minGapSeconds` → `< 0` | `ProgressLessThan10SecondsSinceLastAcceptedReturnsRecordedFalse` |
| M4b | Janela do avanço | `RecordPlaybackProgress.cs` — graça ×24 | `ProgressBeyond60MinutesAfterExpiryReturns410` |
| M5 | Fato retido no outbox | `RecordPlaybackProgress.cs` — `AppendAsync` removido | `PlaybackProgressTests` (≥6 falhas, incl. `ValidProgressRecordsFactAndUpdatesSessionFields`) |
| M6 | Claim `email` fora da audiência `media` | `StudentSessionTokenIssuer.cs` — e-mail sempre emitido | `EmailIsOnlyIssuedForConfiguredMediaAudience` (`other`, `learning`) |
| M7 | `internalReturnPath` (`//`) | `internal-return-path.ts` — `startsWith('//')` removido | `student-login-return.test.tsx`: `//evil.example` e `/%2f%2fevil.example` |
| M8a | Redação da `segmentAccess.query` (span) | `telemetry-url-redaction.ts` — atributos do span não redigidos | 2 testes de `telemetry-url-redaction.test.ts` |
| M8b | Redação da `segmentAccess.query` (função) | `telemetry-url-redaction.ts` — `redactSensitiveUrl` devolve a entrada | 8 testes de `telemetry-url-redaction.test.ts` |

**Sobreviventes: 0 de 11.** Observação: em M5 a classe `RetainedOutboxFactTests` (6 testes) passou com o `AppendAsync` removido, porque cobre o replay de fatos já gravados e não a gravação do avanço; a gravação está coberta por `PlaybackProgressTests`, então o critério é discriminado. Em M6 `StudentTokenAudienceTests` também passou, e `StudentTokenEmailClaimTests` detectou. Não bloqueia.

## Rastreabilidade e revisão semântica (resumo)

Sem mudança relevante além do diff acima desde a tentativa 1, cuja revisão contra `techspec.md`/`contracts.md`/PRD permanece válida: identidade por audiência e `email` condicional; decisão de acesso com cache de 30 s, falha fechada e indisponível distinto de negado; ordem de RN-M10 na abertura; sessão sem coluna de e-mail; entrega sem credencial na playlist e borda `secure_link` (reverificada em contêiner); concorrência do avanço com token de concorrência e `eventId` determinístico; fato retido no outbox; audiência por rota no BFF com `Cache-Control: no-store`; retorno pós-login só para caminho interno; hls.js com `xhrSetup`; redação de credencial e e-mail nos spans do SPA. Os contratos entre `media`, BFF e SPA agora usam um único vocabulário de erro de sessão (`PLAYBACK_SESSION_*`).

## Recomendações (não bloqueiam)

1. `media`: a política (`SessionMinutes`, `RenewLeadSeconds`, `WatermarkRepositionSeconds`, `ProgressIntervalSeconds`, `ProgressMinGapSeconds`, `ProgressGraceMinutes`) segue fixa no código; a TechSpec (D-11) manda ler da configuração. Benefício: cadência ajustável sem republicar; custo: um objeto de opções injetado nos três casos de uso; gatilho: primeiro ajuste de cadência com dado real do painel (RF-08).
2. `RecordPlaybackProgress` e `PlaybackSessionEndpoints.ProgressAsync` (media e BFF) leem o corpo sem limite de tamanho; limitar (por exemplo, 1 KB) no endpoint.
3. `GetPlaybackResource` e `RenewPlaybackSession` não conferem `TenantId` da sessão contra o do JWT (o avanço confere); uniformizar com `FindSessionAsync(tenant, id)`.
4. `telemetry-url-redaction.ts`: `registerTelemetrySecret(query, expiresAt)` ignora `expiresAt` e o conjunto `secrets` nunca é podado; remover as entradas vencidas.
5. Entrega da PR (não imputável às tasks): o commit `c4179ef` "Atualização nas Skills" já está no range e irá na PR; `routing.default.json`, `scripts/kibana/__pycache__/` (não ignorado pelo Git) e `prompt.txt` estão fora do commit e não devem entrar. Decisão do responsável; adicionar `__pycache__/` ao `.gitignore` evita vazamento acidental.

FULL VALIDATION APROVADA — 0 bloqueantes, 5 recomendações.
