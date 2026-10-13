# Revisão full — CAP-032 Transcrição e legenda de aula

Run: run.fmQPTY6o
Tentativa: 3/3
Modo: full
Veredito: **FULL VALIDATION APROVADA** (0 bloqueantes, 9 recomendações)

- base_ref: `0ac6bf407b87c24b9d219cc3f8549b05005d9211`
- validated_commit: `85d3a590fc6a5d6a982b7fbae3e0594bdf7c7971`
- validated_tree: `6f5d9450ce44c95c0131df55b3484fa52fdada5d`
- Branch: `feature/cap-032-transcricao-legenda`. Base inalterada e ancestral de HEAD.
- Estabilidade: HEAD, árvore e `git status --porcelain` (só `flow-state.json`, operacional) iguais no início e no fim; worktree do sensor removido e linha de base conferida.
- Diff desde a full anterior (run.qN9J8Zzy, `b29519f`): apenas `LessonTranscriptionTests.cs` e `PlaybackCaptionsTests.cs` (bff-student, testes) e documentos de task/estado. Nada compartilhado (`Directory.*`, `global.json`, `contracts/`, compose, `.github/`) mudou; código de produção inalterado.
- Pendência operacional (não reprova): 13.0 sem o smoke real (EN-02 ausente), como combinado no contexto da chamada.

## B-2 anterior (fallback `Cache-Control` do bff-student): resolvido

Os dois testes novos `AppliesPrivateNoStoreWhenUpstreamOmitsCacheControl` (um por rota) usam um simulador que não envia `Cache-Control` e exigem `private` + `no-store` e `MaxAge` nulo. Reexecutei a mutação: **morreu nas duas rotas** (abaixo).

## Matriz de CI

Fonte: `.github/workflows/*.yml` + reutilizáveis `ci-dotnet.yml@v1`/`ci-react-ts.yml@v1` (como nas fulls anteriores). Não reproduzíveis aqui: build/push de imagem Docker, SAST/secret/dependency scan (`security-mode: observe`), upload de artifact.

| Componente | Origem | Format/lint | Typecheck | Testes | Cobertura (≥70%) | Build/publish | Resultado |
|---|---|---|---|---|---|---|---|
| bff-student | **executado agora** (alterado, `85d3a59`) | `dotnet format --verify-no-changes` exit 0 | — | `dotnet test --configuration Debug --coverage`: **exit 0**, 288 total, 288 ok, 0 falho, 0 skip | 86,97% (2437/2802 linhas, `dotnet-coverage merge`) | `dotnet publish -c Release` da Api exit 0 | ok |
| media | **reaproveitada** de run.qN9J8Zzy @ `b29519f` | exit 0 | — | exit 0: 537 total, 536 ok, 1 skip (smoke explícito) | 93,94% | publish exit 0 | ok |
| bff-admin | **reaproveitada** de run.dGgYmYs6 @ `21d3ec8` (via run.qN9J8Zzy) | exit 0 | — | exit 0: 299/299 | 84,84% | publish exit 0 | ok |
| admin-spa | **reaproveitada** de run.dGgYmYs6 @ `21d3ec8` | lint 0 | 0 | exit 0: 394/394 | 92,51% | build 0 | ok |
| student-spa | **reaproveitada** de run.dGgYmYs6 @ `21d3ec8` | lint 0 | 0 | exit 0: 222/222 | 91,57% | build 0 | ok |
| identity, learning, billing, commerce, notification | **reaproveitada** de run.dGgYmYs6 @ `21d3ec8` | exit 0 | — | exit 0: 182, 245, 86, 491, 65 | não medida | — | ok |
| Spectral (`contracts/**/openapi*.yaml`) | **reaproveitada** (contracts/ inalterado) | — | — | exit 0, 0 erros, 7 avisos C-05 | — | — | ok |
| Kibana `--verify-only` | **reaproveitada** | — | — | exit 0 | — | — | ok |
| Migrations media | **reaproveitada** (sem diff em Infra/Domain) | — | — | `has-pending-model-changes` exit 0 | — | — | ok |

Limites: o bff-student foi rodado inteiro (Testcontainers) uma vez, em primeiro plano, sem outra suíte em paralelo, com resultados em diretório próprio. Reaproveitamento conforme as 3 condições do SKILL.md (base igual, diff não toca os componentes, nada compartilhado alterado).

## Sensor de discriminação

Executado nesta tentativa só no que mudou: worktree temporário `--detach` em HEAD, mutação aplicada, suíte focalizada, worktree removido e baseline conferida. As demais mutações (M1–M11, B2, F1, F2) ficam **reaproveitadas** de run.qN9J8Zzy @ `b29519f` (mortas; M6 equivalente), pois nenhum arquivo de produção mudou.

| Id | Fatia | Mutação (arquivo) | Suíte | Resultado |
|---|---|---|---|---|
| B1 | bff-student | `?? "private, no-store"` → `?? "public, max-age=60"` (`PlaybackSessionEndpoints.cs:67`, ramo único das rotas `/transcription` e `/captions`) | `LessonTranscriptionTests` exit 2: 1 falha de 10 (`AppliesPrivateNoStoreWhenUpstreamOmitsCacheControl`); `PlaybackCaptionsTests` exit 2: 1 falha de 9 (mesmo teste) | **morta** (nas duas rotas) |

Os sobreviventes da tentativa anterior (apenas B1/B-2) foram repetidos: nenhum sobrevive.

## Revisão independente (dimensões)

Herdada das fulls anteriores, válida porque o diff novo é só teste: segurança (texto/URL/chave AES/segredo fora de log, span, métrica e cache persistente; 403 `ACCESS_DENIED` com decisão fresca; HMAC com `FixedTimeEquals`, janela ±300 s, idempotência por `event_id`), contratos×código (9 operações conferidas), rastreabilidade (RF/RN/US com código e teste) e arquitetura (sem pressão concreta para padrão de projeto). As duas decisões do orquestrador a confirmar (precedência temporal do WebVTT; `WHISPER_API_KEY` opcional em stable/komodo) não revelaram problema concreto.

## Recomendações (não bloqueiam; as mesmas das tentativas anteriores)

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

Aprovada. B-1 e B-2 corrigidos; matriz de CI toda verde; sensor sem sobreviventes. Pendência operacional fora do veredito: smoke real da 13.0 (EN-02).
