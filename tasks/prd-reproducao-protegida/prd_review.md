# Revisão full — PRD reproducao-protegida

Run: run.Ejq25cS3
Tentativa: 1/3
Modo: full · delivery=pr

## Resultado: FULL VALIDATION REPROVADA (2 bloqueantes, 5 recomendações)

| Item | Valor |
|---|---|
| base_ref | e3d2ed468054f904c3cf6cc41e9bec56d191fa2e |
| validated_commit | 2ec1552e12288b5e2796e3978e7553107aa19baf |
| validated_tree | 2b2dd5c7bdbc115d66227673469364e0a6ff25be |
| Estabilidade | HEAD, árvore e `git status --porcelain` idênticos antes e depois da revisão |
| Escopo | 244 arquivos, +10625/−113; specs: `techspec.md`, `contracts.md`, `internal-api-contract-media.yaml` |

## Bloqueantes

### B1 — Task 4.0 — job de CI `media` reprova: 2 testes preexistentes quebrados pela validação de startup nova

- `src/media/src/CodeForCoders.Media.Infra.Data/DependencyInjection.cs:61` (credenciais de entrega, `Playback delivery credentials are invalid`) e
  `src/media/src/CodeForCoders.Media.Infra.Messaging/DependencyInjection.cs:31` (`Video key custody settings are invalid`) passaram a validar na partida do papel `api`. Os dois foram introduzidos no commit `853c367` (checkpoint 4.0).
- Dois testes **não alterados no diff** sobem o host do papel `api` sem essas configurações e passam a falhar na partida:
  - `CodeForCoders.Media.IntegrationTests.MediaInfrastructureTests.HeartbeatFlowsThroughOutboxRabbitMqAndConsumer` (`MediaInfrastructureTests.cs:101`);
  - `CodeForCoders.Media.IntegrationTests.VideoLibraryAuthorizationTests.VideoLibrary_FailsClosedWhenJwksIsUnavailableAndKeepsLivenessHealthy` (`VideoLibraryAuthorizationTests.cs:173`, via `MediaJwksUnavailableApiFactory`).
- Evidência: `dotnet test` de `src/media` sai com **2** (229 testes, 2 falhas); as duas falhas se repetem isoladas no HEAD (exit 2, `OptionsValidationException`). **No `base_ref` (worktree temporário) os mesmos dois testes passam** (exit 0), logo a regressão é desta entrega.
- Por que não apareceu antes: os gates focados da 4.0 rodam só as classes nomeadas (`PlaybackSessionOpenTests`, `PlaybackDeliveryTests` etc.); a suíte agregada nunca tinha rodado.
- Correção esperada: fornecer a configuração de entrega e de custódia de chave nos hosts de teste que sobem o papel `api` (corrigir o teste/a fábrica, sem enfraquecer a validação de produção) e rodar a suíte completa de `media`.

### B2 — Task 4.0 — `media` devolve códigos de erro fora do contrato interno nas operações de entrega

- `internal-api-contract-media.yaml` 1.1.0 (`components.responses.SessionNotFound`, linha ~639, e `SessionExpired`) e `techspec.md` ("Exceção → resposta HTTP") definem `PLAYBACK_SESSION_NOT_FOUND` (404) e `PLAYBACK_SESSION_EXPIRED` (410) para `getPlaybackPlaylist`, `getPlaybackVariantPlaylist` e `getPlaybackKey`.
- `src/media/src/CodeForCoders.Media.Application/UseCases/PlaybackSessions/GetPlaybackResource/GetPlaybackResource.cs:15,17,21,25` emite `SESSION_NOT_FOUND` e `SESSION_EXPIRED`. Renovação e avanço já usam os códigos do contrato, então o mesmo recurso responde com dois vocabulários.
- O BFF contorna a divergência aceitando os dois pares (`PlaybackMediaClient.cs:12-13`) e repassa o código ao SPA, que só reconhece `PLAYBACK_SESSION_EXPIRED` (`use-protected-playback.ts:30`); o contrato do BFF (`api-contract.yaml:453,842`) também só declara os nomes `PLAYBACK_SESSION_*`.
- Correção esperada: `GetPlaybackResource` passa a emitir os códigos do contrato; atualizar os testes de `media` e do BFF que fixam os códigos antigos (`PlaybackProxyTests.cs:92-93`) e remover o par legado de `KnownErrors`.

## Matriz de CI (espelho local dos workflows reutilizáveis `ci-dotnet.yml@v1` e `ci-react-ts.yml@v1`)

Fonte dos passos: leitura dos workflows `tassosgomes/template-pipeline@v1` (via `gh api`) e dos chamadores em `.github/workflows/`. Testes .NET: `dotnet test --no-restore -c Debug --coverage --coverage-output-format cobertura` na raiz de cada componente; testes em sequência, sem paralelismo entre componentes.

| Componente | restore | format (`--verify-no-changes`) | test Debug | cobertura de linhas (união, limite 70) | publish Release |
|---|---|---|---|---|---|
| identity | 0 | 0 | **0** (169/169) | 81,1% | 0 |
| commerce | 0 | 0 | **0** (378/378) | 96,1% | 0 |
| learning | 0 | 0 | **0** (171/171) | 95,7% | 0 |
| media | 0 | 0 | **2** (227/229; B1) | 91,0% | 0 |
| bff-student | 0 | 0 | 2 na execução com `OTEL_SDK_DISABLED=true`; o único teste falho (`HttpSpansNeverRecordVisitorHeadersOrTheIdempotencyKey`) **passa isolado com o SDK ligado** (exit 0); ver Limites | 85,8% | 0 |
| student-spa | `npm ci` 0 | lint 0 · `tsc --noEmit` 0 | `vitest run --coverage` **0** (21 arquivos, 128 testes) | linhas 88,6% · statements 87,0% · branches 71,6% | `vite build --base=/student/` 0 |

Passos adicionais:

| Passo | Comando | Resultado |
|---|---|---|
| Imagem `student-spa` (hls.js) | `docker build -f src/student-spa/Dockerfile src/student-spa` | 0 |
| Imagem `media-edge` (nova) | `docker build -f src/media-edge/Dockerfile .` | 0; contêiner executado: sem credencial 403, credencial inválida 403, vencida 410, válida passa à origem, prefixo de outro vídeo 403; o log de acesso não registra a consulta |
| Compose | `docker compose config -q` (base, `+remote`, `coolify` com variáveis fictícias) | 0 / 0 / 0 |
| Kibana | `python3 scripts/kibana/import_observabilidade.py --verify-only` | 0 (painel e alertas A1–A5 e instrumentos de reprodução presentes) |
| hls.js | licença e versão | Apache-2.0, 1.7.3 |

Limites e passos não reproduzíveis (classificados):

- **Ambiente:** neste sandbox, conexões TCP a portas fechadas de `127.0.0.1` ficam em `SYN-SENT`; o exportador OTLP (`localhost:4317`) fazia cada teste de integração levar 10–15 s e a suíte do commerce estourou 25 min. Para as execuções de commerce, learning, media e bff-student usei `OTEL_SDK_DISABLED=true` (só ambiente, sem alterar código ou comando). Identity rodou antes sem o ajuste. O único teste que depende de spans do SDK foi reexecutado com o SDK ligado e passou. A comparação base × HEAD de B1 foi feita com o mesmo ajuste nos dois lados.
- `scripts/remote-infra.sh check`: **falhou nos 6 serviços** (mesma restrição de rede do sandbox). Os cenários de navegador contra o ambiente de desenvolvimento (V-01 a V-04 com cortesia real, painel com reprodução de teste) **não foram executados** nesta rodada; não são jobs de CI.
- Jobs `container` das imagens .NET (identity, commerce, learning, media, bff-student): `Dockerfile` não mudou no range; validado por `dotnet publish -c Release` (exit 0), sem `docker build` dessas imagens. Jobs de segurança (`security-mode: observe`, Semgrep/gitleaks/Trivy, SBOM, upload de artefato, push ao GHCR): observacionais ou exclusivos do runner, não executados.
- A cobertura acima é a união de linhas dos relatórios Cobertura locais (mesmo critério do passo `coverage` do template); os valores no GitHub podem diferir pouco.

## Sensor de discriminação

**Não executado.** A regra exige rodar o sensor depois que os checks obrigatórios passam; B1 (job `media`) já determina o veredito. Não declaro mutação nenhuma como executada. Na próxima tentativa, depois de B1 e B2 corrigidos, o sensor deve rodar sobre: decisão negada em `GetStudentLesson`, ausência de e-mail em `OpenPlaybackSession`, renovação de sessão vencida, intervalo mínimo e janela do avanço, fato retido no outbox, claim `email` fora da audiência `media`, `internalReturnPath` (`//`) e a redação da `segmentAccess.query`.

## Rastreabilidade e revisão semântica (resumo)

Revisados contra `techspec.md`/`contracts.md`/PRD: identidade por audiência e `email` condicional (Identity, 3.0/4.0); decisão de acesso com cache de 30 s, falha fechada e indisponível distinto de negado (`learning`, `media`); ordem de RN-M10 na abertura; sessão em `playback_sessions` sem coluna de e-mail; entrega sem credencial na playlist e borda `secure_link` verificada em contêiner; concorrência do avanço por token de concorrência (`last_sequence`, `last_progress_at`) e `eventId` determinístico; fato retido no outbox; audiência por rota no BFF com `Cache-Control: no-store`; retorno pós-login só para caminho interno (`internal-return-path.ts`); hls.js com `xhrSetup`; redação da credencial e de e-mail nos spans do SPA. Nenhum bloqueante adicional encontrado nessas áreas.

Mudança fora da lista da TechSpec, aceita: `StudentAccountConfirmationClient.cs` (commerce, 3.0) passou a exigir `{studentId, eligible}`, alinhando-se ao contrato vigente de Identity (`contracts/identity/openapi-internal.yaml`, `StudentAccountConfirmation`) e ao endpoint real (`StudentAccountConfirmationEndpoints.cs:32`); teste novo cobre.

## Recomendações (não bloqueiam)

1. `media`: a política (`SessionMinutes`, `RenewLeadSeconds`, `WatermarkRepositionSeconds`, `ProgressIntervalSeconds`, `ProgressMinGapSeconds`, `ProgressGraceMinutes`) está fixa no código (`OpenPlaybackSession.cs`, `RenewPlaybackSession.cs`, `RecordPlaybackProgress.cs`, `PlaybackSession.cs`) e a seção `Playback` de `appsettings.json` só tem `Delivery`. A TechSpec (Interfaces entre fatias, D-11) manda ler da configuração. Benefício de mudar: cadência ajustável sem republicar; custo: um objeto de opções injetado nos três casos de uso; gatilho: primeiro ajuste de cadência com dado real do painel (RF-08).
2. `RecordPlaybackProgress` e `PlaybackSessionEndpoints.ProgressAsync` (media e BFF) leem o corpo inteiro sem limite de tamanho; limitar o corpo (por exemplo, 1 KB) no endpoint.
3. `GetPlaybackResource` e `RenewPlaybackSession` não conferem `TenantId` da sessão contra o do JWT (o avanço confere). O `sub` do aluno já restringe, mas vale uniformizar com `FindSessionAsync(tenant, id)`.
4. `telemetry-url-redaction.ts`: `registerTelemetrySecret(query, expiresAt)` ignora `expiresAt` e o conjunto `secrets` nunca é podado; sessões longas acumulam valores. Remover as entradas vencidas.
5. Entrega da PR (não imputável às tasks): o commit `c4179ef` "Atualização nas Skills" (`.agents/skills/*`, `skills-lock.json`) já está no range e irá na PR; `routing.default.json` (modificado) e `scripts/kibana/__pycache__/` (não ignorado pelo Git) e `tasks/prd-reproducao-protegida/prompt.txt` estão fora do commit e não devem entrar. Decisão do responsável antes de abrir a PR; adicionar `__pycache__/` ao `.gitignore` evita vazamento acidental.

## Atribuição

| Bloqueante | Task | Retorno ao implementer |
|---|---|---|
| B1 | 4.0 | Ajustar os hosts de teste do papel `api` e rodar a suíte completa de `media` (exit 0) |
| B2 | 4.0 | Alinhar os códigos de erro de playlist, variante e chave ao contrato e atualizar os testes de `media` e `bff-student` |

Após corrigir: reexecutar as suítes completas de `media` e `bff-student` (sem `OTEL_SDK_DISABLED` onde o ambiente permitir) e então o sensor de discriminação.

FULL VALIDATION REPROVADA — 2 bloqueantes, 5 recomendações.
