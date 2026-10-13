# Revisão full — CAP-032 Transcrição e legenda de aula

Run: run.dGgYmYs6
Tentativa: 1/3
Modo: full
Veredito: **FULL VALIDATION REPROVADA** (1 bloqueante, 9 recomendações)

- base_ref: `0ac6bf407b87c24b9d219cc3f8549b05005d9211`
- validated_commit: `21d3ec8d932b4f62333e10f67e4bac6acaffd7d2`
- validated_tree: `13d88635929164223766a2695c97653add454872`
- Branch: `feature/cap-032-transcricao-legenda` (16 commits sobre a base; base é ancestral de HEAD).
- Estabilidade: HEAD, árvore e `git status --porcelain` (só `flow-state.json`, operacional) iguais no início e no fim.
- Escopo: 243 arquivos. Componentes: `media`, `bff-admin`, `bff-student`, `admin-spa`, `student-spa`, contratos, Kibana, compose.
- Pendência operacional (não reprova): 13.0 sem o smoke real (EN-02 ausente), como combinado no contexto da chamada.

## Bloqueante

### B-1. Teste do media depende de ordem e quebra a suíte completa (CI `media` reprova)

- `src/media/tests/CodeForCoders.Media.IntegrationTests/TranscriptionEventReceiverTests.cs:202` (`CompletedNoticeNeverTurnsTheAttemptIntoReadyOrWritesText`).
- O teste faz `Assert.Empty(Db(scope).TranscriptionContents.IgnoreQueryFilters().ToArrayAsync(...))`, isto é, exige a **tabela inteira** vazia. A coleção `VideoLibraryApiCollection` compartilha um único PostgreSQL entre as classes, e `InitializeAsync` da classe só trunca `video_transcription_events`.
- Classes que gravam conteúdo e não limpam (ex.: `PlaybackCaptionsTests.cs:112`, texto "Introduction & <script>") deixam linhas que o assert enxerga.
- Evidência: `dotnet test` da suíte completa do media saiu com exit 2 em duas execuções (537 testes: 535 ok, 1 falho, 1 explícito não rodado; e 409 na repetição só do projeto de integração: 1 falho). O mesmo teste, rodando a classe sozinha (`--filter-class *TranscriptionEventReceiverTests`), passou 28/28, exit 0. O erro: `Assert.Empty() Failure: Collection was not empty` com `TranscriptionContent` de outros testes.
- O gate por task não pegou porque cada task roda só os seus testes (7.0 foi aprovada antes de 9.0/10.0/11.0 escreverem conteúdo na mesma base).
- Correção esperada (no teste, sem enfraquecê-lo): restringir o assert ao `TranscriptionId` da tentativa semeada (`.Where(c => c.TranscriptionId == id)`) ou truncar `video_transcription_contents` no `InitializeAsync`. Manter o assert que a tentativa continua `waiting`.
- Cada execução custa ~7 min; revalidar rodando o projeto de integração completo.

## Matriz de CI (fonte: workflows `.github/workflows/*.yml` + reutilizáveis `ci-dotnet.yml@v1`/`ci-react-ts.yml@v1`, lidos via `gh api`)

Passos reproduzidos: restore, `dotnet format --verify-no-changes`, `dotnet test --configuration Debug --coverage` (Microsoft.Testing.Platform), cobertura mesclada por união de linhas (script do CI reproduzido; limite 70%), `dotnet publish -c Release`; SPAs: `npm ci`, lint, `tsc --noEmit`, `npm test` (vitest + cobertura), cobertura `lines.pct`, `vite build --base=...`. Não reproduzíveis aqui: build/push de imagem Docker, SAST/secret/dependency scan (`security-mode: observe`, não reprova), upload de artifact.

| Componente | Disparo | Format/lint | Typecheck | Testes | Cobertura (≥70%) | Build/publish | Resultado |
|---|---|---|---|---|---|---|---|
| media | alterado | exit 0 | — | **exit 2**: 537 total, 535 ok, 1 falho, 1 skip (smoke explícito) | 94,01% (medida em execução com falha) | publish exit 0 | **REPROVA** (B-1) |
| bff-admin | alterado | exit 0 | — | exit 0: 299/299 | 84,84% | publish exit 0 | ok |
| bff-student | alterado | exit 0 | — | exit 0: 286/286 | 86,88% | publish exit 0 | ok |
| admin-spa | alterado | lint 0 | 0 | exit 0: 394/394 (60 arquivos) | 92,51% | build 0 | ok |
| student-spa | alterado | lint 0 | 0 | exit 0: 222/222 (33 arquivos) | 91,57% | build 0 | ok |
| identity, learning, billing, commerce | `contracts/**` (sem diff próprio) | exit 0 | — | exit 0: 182, 245, 86, 491 | não medida (sem diff do componente) | — | ok |
| notification | `contracts/**` (sem diff próprio) | exit 0 | — | 1ª execução, em paralelo com outras suítes: exit 2, 36/65 falhos por `Could not find resource 'DockerContainer'` na fixture (Testcontainers, ambiente). Isolada: exit 0, 65/65 | — | — | ok (falha de ambiente sob concorrência, confirmada isolada) |

Demais passos:

- Spectral 6.17.0 com `contracts/.spectral-exception.yaml`, `--fail-severity=error` em `contracts/**/openapi*.yaml`: exit 0, 0 erros, 7 avisos de paginação (C-05).
- `python3 scripts/kibana/import_observabilidade.py --verify-only`: exit 0 (12 saved objects; instrumentos e regras T1–T3 da transcrição conferidos).
- `dotnet ef migrations has-pending-model-changes` (media): exit 0, sem pendência. As duas migrations (`AddVideoTranscriptions`, `AddTranscriptionAudioCleanups`) batem com as configurações e o snapshot.
- Os 5 contratos da capacidade em `contracts/` batem com os recortes aprovados e com as rotas do código.
- Limites: TestResults antigos podem existir nos diretórios `bff-*`; usei só relatórios gerados nesta execução (conferidos por horário).

## Sensor de discriminação

**Não executado.** Pré-condição não atendida: um check obrigatório (media) já reprova. Declarado como limitação, não como executado. Na revalidação (tentativa 2), rodar uma mutação por fatia (pedido/estado, pipeline, disparo automático, webhook, falhas/Refazer, leitura do professor, transcrição do aluno, legenda WebVTT, métricas) e reaproveitar a matriz dos componentes não alterados.

## Revisão independente (dimensões)

Segurança, contratos×código e rastreabilidade/arquitetura revisadas por subagentes; achados conferidos por mim nos pontos que viram bloqueante ou recomendação alta.

- **Segurança:** sem bloqueante. Texto, URL assinada, chave AES e segredos fora de log/span/métrica e de cache persistente (`gcTime: 0`; só um booleano em `localStorage`); 404/403 `ACCESS_DENIED` do aluno com decisão fresca e `Cache-Control: private, no-store`; HMAC com `FixedTimeEquals`, janela ±300 s, idempotência por `event_id`; chave AES em arquivo 0600 apagado em `finally`; ffmpeg por `ArgumentList` com `protocol_whitelist`.
- **Contratos×código:** 9 operações novas conferem (rota, status, códigos de erro, enums), migrations coerentes, compose replicado.
- **Rastreabilidade:** todo RF/RN/US tem código e teste; nenhum `Skip` nos arquivos da capacidade. Decisões do orquestrador (precedência temporal do WebVTT; `WHISPER_API_KEY` opcional em stable/komodo com validação na partida) sem problema concreto; seguem para confirmação do responsável na PR.
- Padrões de projeto: sem pressão concreta que justifique refatoração.

## Recomendações (não bloqueiam)

1. `docker-compose.remote.yml:87`: `${WHISPER_API_KEY:?...}` incondicional faz `scripts/apps.sh start --remote` abortar sem a chave, mesmo com `TRANSCRIPTION_ENABLED=false`; nenhum script gera a variável. A TechSpec (linha 172) pede `:?`, por isso não reprovo. Sugestão: `:-` como nos demais compose (a validação na partida cobre `Enabled=true` sem chave). Decisão do responsável.
2. `HttpClient` do serviço de transcrição segue redirecionamentos (`X-API-Key` custom iria ao destino): `AllowAutoRedirect = false`.
3. `TranscriptionServiceOptions.IsValid` aceita `http://` para o serviço; exigir `https` fora de Development.
4. Mensagem de partida inválida não diz qual chave falta (sem mostrar valor).
5. `Enabled=true` com configuração inválida derruba o host do worker inteiro, inclusive a preparação de vídeo (tensão com RN-M19); degradar só a transcrição ou documentar.
6. Falha transitória de storage/banco fora dos estágios HTTP (ex.: `DeleteObjectAsync`, `ProcessTranscription.cs:203`) vira `processing-failed` definitivo; tratar como transitória.
7. `language`: contrato fixa `pt-BR`, mas o media guarda o que o serviço enviar e o schema do student-spa (`z.literal('pt-BR')`) rejeitaria outro valor; normalizar/validar no media ou afrouxar o schema. `revision` no admin-spa usa regex `^tr_[a-f0-9]{8}$` apesar de o contrato dizer "opaco".
8. `docker-compose.yml`: variáveis `Transcription__Service__*`/`AwsMedia__EndpointTranscription` estão no serviço `media` (API) e não no `media-worker`; com `--remote` a API herda `http://minio:9000`.
9. Testes sem cobertura: clique no trecho com sessão vencida/parada (RN-TL02, ramo de reabertura em `use-protected-playback.ts`); idioma pt-BR (RN-M25); verificação manual em tela cheia da 11.0 pendente. Spectral avisa paginação em `GET .../captions` (falso positivo; registrar exceção).

## Resultado

Reprovada por B-1 (1 bloqueante). Os demais componentes passam. Voltar ao implementer para corrigir o isolamento do teste, rodar a suíte completa do media (exit 0) e pedir nova full (tentativa 2: reaproveitar componentes não alterados, rodar media inteiro e o sensor).
