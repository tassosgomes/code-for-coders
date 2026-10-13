# Revisão full — CAP-032 Transcrição e legenda de aula

Run: run.qN9J8Zzy
Tentativa: 2/3
Modo: full
Veredito: **FULL VALIDATION REPROVADA** (1 bloqueante do sensor, 9 recomendações)

- base_ref: `0ac6bf407b87c24b9d219cc3f8549b05005d9211`
- validated_commit: `b29519fcfc3d5c2d16de0ef5f07c9e53adf8ad95`
- validated_tree: `f36b02b8d5aed1a5513d9c754e5b51f6c73d04b6`
- Branch: `feature/cap-032-transcricao-legenda`. Base inalterada e ancestral de HEAD.
- Estabilidade: HEAD, árvore e `git status --porcelain` (só `flow-state.json`, operacional) iguais no início e no fim; worktree do sensor removido e linha de base conferida.
- Diff desde a full anterior (run.dGgYmYs6, `21d3ec8`): só `TranscriptionEventReceiverTests.cs` (media) e documentos de task/estado. Nada compartilhado (`Directory.*`, `global.json`, `contracts/`, compose, `.github/`) mudou.
- Pendência operacional (não reprova): 13.0 sem o smoke real (EN-02 ausente), como combinado no contexto da chamada.

## B-1 anterior (isolamento do teste de 7.0): resolvido

`TranscriptionEventReceiverTests.cs:202` e `:232` agora restringem o assert ao `TranscriptionId` da tentativa semeada e mantêm o assert de `waiting`. A suíte completa do media saiu com **exit 0** (abaixo).

## Bloqueante (sensor de discriminação)

### B-2. Fallback `Cache-Control: private, no-store` do bff-student não é exercitado por nenhum teste

- `src/bff-student/src/CodeForCoders.BffStudent.Api/Endpoints/PlaybackSessionEndpoints.cs` (linha do `context.Response.Headers.CacheControl = result.CacheControl ?? "private, no-store"`, nos caminhos `/transcription` e `/captions`).
- Mutação: `?? "private, no-store"` → `?? "public, max-age=60"`. **Sobreviveu**: `LessonTranscriptionTests` 9/9 e `PlaybackCaptionsTests` 8/8, exit 0.
- Critério: texto da aula não pode ser cacheado (PRD: nenhum texto de aula em cache; contrato exige `private, no-store`). O BFF é quem garante isso quando o upstream não manda o cabeçalho.
- Por que escapa: o simulador de mídia dos dois testes (`LessonTranscriptionTests.cs:131`, `PlaybackCaptionsTests.cs:117`) sempre devolve `Cache-Control: private, no-store`, então o ramo do fallback nunca roda; os asserts das linhas 40–41 e 27 passam com o valor repassado.
- Correção esperada (no teste, sem enfraquecer): um caso por rota (`transcription` e `captions`) em que o simulador **não** envia `Cache-Control` e o teste exige `private` + `no-store` na resposta do BFF. Rodar os dois com `--filter-class`; revalidar o sensor só nesta mutação.

## Matriz de CI

Fonte: `.github/workflows/*.yml` + reutilizáveis `ci-dotnet.yml@v1`/`ci-react-ts.yml@v1` (como na full anterior). Não reproduzíveis aqui: build/push de imagem Docker, SAST/secret/dependency scan (`security-mode: observe`), upload de artifact.

| Componente | Origem | Format/lint | Typecheck | Testes | Cobertura (≥70%) | Build/publish | Resultado |
|---|---|---|---|---|---|---|---|
| media | **executado agora** (alterado, `b29519f`) | `dotnet format --verify-no-changes` exit 0 | — | `dotnet test --configuration Debug --coverage`: **exit 0**, 537 total, 536 ok, 0 falho, 1 skip (smoke explícito) | 93,94% (união de linhas dos 4 projetos) | `dotnet publish -c Release` da Api exit 0 | ok |
| bff-admin | **reaproveitada** de run.dGgYmYs6 @ `21d3ec8` | exit 0 | — | exit 0: 299/299 | 84,84% | publish exit 0 | ok |
| bff-student | **reaproveitada** de run.dGgYmYs6 @ `21d3ec8` | exit 0 | — | exit 0: 286/286 | 86,88% | publish exit 0 | ok |
| admin-spa | **reaproveitada** de run.dGgYmYs6 @ `21d3ec8` | lint 0 | 0 | exit 0: 394/394 | 92,51% | build 0 | ok |
| student-spa | **reaproveitada** de run.dGgYmYs6 @ `21d3ec8` | lint 0 | 0 | exit 0: 222/222 | 91,57% | build 0 | ok |
| identity, learning, billing, commerce, notification | **reaproveitada** de run.dGgYmYs6 @ `21d3ec8` | exit 0 | — | exit 0: 182, 245, 86, 491, 65 | não medida | — | ok |
| Spectral (`contracts/**/openapi*.yaml`) | **reaproveitada** (contracts/ inalterado) | — | — | exit 0, 0 erros, 7 avisos C-05 | — | — | ok |
| Kibana `--verify-only` | **reaproveitada** | — | — | exit 0 | — | — | ok |
| Migrations media | **reaproveitada** (sem diff em Infra/Domain) | — | — | `has-pending-model-changes` exit 0 | — | — | ok |

Limites: o media foi rodado inteiro (Testcontainers) uma vez, em primeiro plano, sem outra suíte em paralelo; o `TestResults` antigo em `src/media/TestResults` não foi usado (resultados em diretório próprio).

## Sensor de discriminação (executado)

Worktree temporário `--detach` em HEAD; cada mutação aplicada, suíte focalizada rodada, `git checkout -- .` entre elas; worktree removido e baseline (HEAD + `git status`) conferida. Todas as mutações compilaram (uma tentativa com erro de compilação `CS9113` foi descartada e refeita).

| Id | Fatia | Mutação (arquivo) | Suíte | Resultado |
|---|---|---|---|---|
| M1 | pedido/estado | guarda `in-progress`/`CanStart` invertida (`RequestVideoTranscription.cs`) | VideoTranscriptionRequest 4 falhas; Restart 3 | morta |
| M2 | webhook HMAC | tolerância de timestamp ×1000 (`TranscriptionWebhookSignature.cs`) | unit 1 falha; EventReceiver 1 falha | morta |
| M3 | webhook/sinal | `Signal` aceita `jobId` de outro job (`Transcription.cs`) | unit 0; EventReceiver 1 falha | morta |
| M4 | legenda WebVTT | sem escape de `&` (`WebVttBuilder.cs`) | unit 1; PlaybackCaptions 1 | morta |
| M5 | aluno/acesso | decisão `denied` não gera 403 (`GetLessonTranscription.cs`) | LessonTranscription 2 falhas | morta |
| M6 | professor | `video.Status == "ready"` relaxado (`GetVideoTranscription.cs`) | 11/11 passam | **equivalente** (ver abaixo) |
| M6b | professor/desligada | `Enabled` ignorado (`GetVideoTranscription.cs`) | VideoTranscriptionRead 1 falha | morta |
| M7 | pipeline | limite 7200 s → 72000 s (`ProcessTranscription.cs`) | FailurePolicy 1; Pipeline 1 | morta |
| M8 | disparo automático | `PrepareVideo` nunca enfileira (`PrepareVideo.cs`) | Automatic 3 falhas | morta |
| M9 | Refazer/vigente | exclusão do Refazer atinge a vigente (`VideoTranscriptionRepository.cs`) | Restart 11 falhas | morta |
| M10 | métricas | atributo `outcome` → `result` (`MediaTelemetry.cs`) | Metrics 2 falhas | morta |
| M11 | falhas | `size-limit` ↔ `duration-limit` (`ProcessTranscription.cs`) | FailurePolicy 1 falha (Pipeline 0) | morta |
| B1 | bff-student | fallback `Cache-Control` público (`PlaybackSessionEndpoints.cs`) | Lesson 0 falha; Captions 0 falha | **SOBREVIVEU → B-2** |
| B2 | bff-admin | `Idempotency-Key` ausente aceita (`VideoLibraryEndpoints.cs`) | VideoTranscription 1 falha | morta |
| F1 | admin-spa | mensagem de `duration-limit` trocada (`video-transcription.ts`) | vitest 2 falhas | morta |
| F2 | student-spa | busca diferencia maiúsculas (`transcript-search.ts`) | vitest 5 falhas | morta |

M6 (equivalente, não bloqueia): `Video.Status` só sai de `ready` por nenhuma transição (`Video.cs`: `ready` é terminal; `SchedulePreparationRetry` parte de `preparing`) e a tentativa só nasce de vídeo `ready`; a guarda é defensiva e inalcançável. A parte testável da condição (`Enabled`) foi mutada em M6b e morreu.

## Revisão independente (dimensões)

Herdada da full anterior, válida porque o diff novo é só teste: segurança (texto/URL/chave AES/segredo fora de log, span, métrica e cache persistente; 403 `ACCESS_DENIED` com decisão fresca; HMAC com `FixedTimeEquals`, janela ±300 s, idempotência por `event_id`), contratos×código (9 operações conferidas), rastreabilidade (RF/RN/US com código e teste) e arquitetura (sem pressão concreta para padrão de projeto). O B-2 acima é lacuna de teste, não de implementação: o código está correto.

## Recomendações (não bloqueiam; as mesmas da tentativa 1)

1. `docker-compose.remote.yml:87`: `${WHISPER_API_KEY:?...}` incondicional impede `apps.sh start --remote` sem a chave mesmo com `TRANSCRIPTION_ENABLED=false`; `:-` como nos demais (decisão do responsável).
2. `HttpClient` do serviço de transcrição segue redirecionamentos (`X-API-Key` iria ao destino): `AllowAutoRedirect = false`.
3. `TranscriptionServiceOptions.IsValid` aceita `http://`; exigir `https` fora de Development.
4. A mensagem de partida inválida não diz qual chave falta (sem mostrar valor).
5. `Enabled=true` com configuração inválida derruba o host do worker inteiro, inclusive a preparação de vídeo (tensão com RN-M19).
6. Falha transitória de storage/banco fora dos estágios HTTP (ex.: `DeleteObjectAsync`, `ProcessTranscription.cs:203`) vira `processing-failed` definitivo.
7. `language`: contrato fixa `pt-BR`, mas o media guarda o que o serviço enviar e o schema do student-spa rejeitaria outro valor; `revision` no admin-spa usa regex apesar de "opaco".
8. `docker-compose.yml`: variáveis `Transcription__Service__*`/`AwsMedia__EndpointTranscription` estão no serviço `media` e não no `media-worker`.
9. Testes sem cobertura: clique no trecho com sessão vencida (RN-TL02), idioma pt-BR (RN-M25), verificação manual da 11.0 em tela cheia; Spectral avisa paginação em `GET .../captions` (registrar exceção).

## Resultado

Reprovada por B-2 (mutante sobrevivente no bff-student). B-1 está corrigido e o media inteiro passa. Voltar ao implementer para acrescentar os dois casos de teste sem `Cache-Control` no upstream (bff-student), rodar a suíte do bff-student (exit 0) e pedir a full 3/3: só bff-student muda, então reaproveitar os demais componentes e repetir o sensor apenas em B1.
