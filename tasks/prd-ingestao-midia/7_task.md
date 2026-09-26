---
status: pending
task_kind: vertical
blocked_by: ["4.0", "5.0"]
gate: 'dotnet test --project src/media/tests/CodeForCoders.Media.IntegrationTests/CodeForCoders.Media.IntegrationTests.csproj -- --filter-class CodeForCoders.Media.IntegrationTests.VideoPreparationTests --minimum-expected-tests 9 && dotnet test --project src/media/tests/CodeForCoders.Media.EndToEndTests/CodeForCoders.Media.EndToEndTests.csproj -- --filter-class CodeForCoders.Media.EndToEndTests.MediaHostRoleTests --minimum-expected-tests 2 && npm --prefix src/admin-spa run test -- video-status'
gate_expect: 'Pelo menos 14 testes passam: 9 Media com Postgres, MinIO, RabbitMQ e ffmpeg reais; 2 papéis do host; 3 SPA'
---

# 7.0 Vídeo recebido fica pronto em três qualidades cifradas, e a lista mostra isso sozinha

**Fatia:** V-04 · **Cobre:** RF-04, RF-05 (*pronto*), RF-06 (atualização automática), RF-09 (`midia.ativo-pronto`), RF-10, RN-M01, RN-M05, RN-M07, RN-M08, DP-01, DP-04, DP-06, US-03, US-05, US-06 · **Spec:** `techspec.md` § V-04, § Media worker — preparação (passos 1–5, 7b, 7c), § Media — publicação, § Interfaces entre fatias · **ADR:** [ADR-0006](../../docs/adr/0006-preparacao-de-video-e-custodia-de-chave.md)

## Comportamento

- **Reivindicar:** o worker pega um vídeo `received` elegível por vez (`Preparation:MaxConcurrency`,
  padrão 1), com `FOR UPDATE SKIP LOCKED`, só se o disco de trabalho tem livre ≥ 3× o original; grava
  `preparing`, lease de 5 min e `attempts + 1`, e renova a lease a cada minuto. Dois workers sobre o
  mesmo vídeo → um só processa. Worker morto → a lease vence e outro retoma (varredura 7c).
- **Preparar:** baixa o original, sonda com ffprobe, gera a chave AES-128 do vídeo, e um único ffmpeg
  produz a escada **sem ampliar** (altura ≥ 1080 → 1080p, 720p, 480p; ≥ 720 → 720p, 480p; menor → 480p,
  ou a própria altura abaixo de 480), H.264 + AAC, segmentos de 6 s, playlists de variante e mestre com
  `#EXT-X-KEY:METHOD=AES-128,URI="c4c-key:{videoId}"`. Envia a árvore `hls/` ao armazenamento
  (sobrescrever é seguro).
- **Concluir:** num commit, `ready` + duração + bytes guardados + chave cifrada (AES-GCM, chave-mestra,
  com o id da chave-mestra) + outbox `midia.ativo-pronto.v1` com `eventId` fixo da transição,
  `durationSeconds` e sem título. **Depois** do commit, apaga o original e limpa o diretório de trabalho;
  se o apagamento falhar, a varredura 7b o refaz. Falha do commit depois de enviar `hls/` → a nova
  tentativa sobrescreve os mesmos objetos e a chave nova entra no mesmo commit.
- **Nada público:** GET anônimo em segmento, playlist ou objeto do vídeo é negado; a chave não aparece
  em objeto, log, span, mensagem ou resposta; chave-mestra de tamanho inválido impede a partida do worker.
- **Spans** por etapa (baixar, sondar, preparar, publicar) com `video.status` e qualidades geradas, nunca
  título nem id de autor.
- **Papéis:** o papel `api` não registra preparação nem varreduras; o `worker` registra.
- **SPA:** a lista consulta de novo a cada 10 s **somente enquanto** há vídeo em `received`/`preparing`
  e a aba está visível; a linha passa a *em preparação* e depois a *pronto · 0:20* sem recarregar; a
  mudança de estado é anunciada a leitor de tela.

## Fora do escopo desta task

Falhas determinísticas, retentativas e `midia.preparacao-falhou` (8.0). Métricas (10.0). Entrega ao
aluno e reescrita do marcador de chave (CAP-007).

## Decisões fechadas

- Fila no estado do vídeo com lease; chave por vídeo cifrada no banco; marcador de URI: ADR-0006.
- Escada e parâmetros: D-04. Descarte do original: DP-06.
- `correlationId` = `traceparent` guardado na conclusão do envio (techspec, Media — publicação).
- Vídeos de teste gerados por ffmpeg (`testsrc`, 1080p e 720p de 20 s) dentro do teste; nenhum binário de vídeo no repositório.
- Migrations pelo tooling do EF.

## Modificar / Referenciar

- **modificar:** `src/media/src/CodeForCoders.Media.Application/Interfaces/IMediaStoragePort.cs`, `src/media/src/CodeForCoders.Media.Infra.Data/Adapters/S3MediaStorageAdapter.cs` (baixar, enviar árvore, apagar)
- **modificar:** `src/media/src/CodeForCoders.Media.Infra.Messaging/DependencyInjection.cs`, `src/media/src/CodeForCoders.Media.Api/appsettings.json` (seções de preparação e proteção de chave)
- **modificar:** `docker-compose.yml`, `docker-compose.coolify.yml` (chave-mestra local e variável sem segredo no Coolify)
- **modificar:** `src/admin-spa/src/testing/handlers.ts`
- **ref:** `asyncapi-contract.yaml` 1.0.0 (`publicarAtivoPronto`); `src/media/src/CodeForCoders.Media.Infra.Messaging/OutboxPublisherWorker.cs`, `src/media/src/CodeForCoders.Media.Infra.Data/Outbox/OutboxMessage.cs`; `domains/entrega-de-midia-e-protecao/domain.md` (RN-M01–RN-M08); Figma aprovado; skills `dotnet` e `react`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/media` | `dotnet format src/media/CodeForCoders.Media.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/media` | `dotnet build src/media/CodeForCoders.Media.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/media` | `dotnet test --project src/media/tests/CodeForCoders.Media.UnitTests/CodeForCoders.Media.UnitTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/media` | `dotnet test --project src/media/tests/CodeForCoders.Media.ArchitectureTests/CodeForCoders.Media.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/admin-spa` | `npm --prefix src/admin-spa run lint` | exit 0 | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run typecheck` | exit 0 | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run build` | exit 0 | `ci-react-ts.yml` |
| Imagem | `docker build -f src/media/Dockerfile -t code-4-coders-media:v04 .` | exit 0 | `ci-dotnet.yml` build-container |

## Pronto quando

- [ ] Gate passa (exit 0) com pelo menos 14 testes.
- [ ] 1080p de 20 s → `master.m3u8` e três variantes; 720p → só 720p e 480p; o original não está mais no bucket.
- [ ] Segmento não toca sem a chave e toca com ela; a chave não aparece em log, mensagem nem objeto.
- [ ] Fila de teste recebe um `midia.ativo-pronto.v1` conforme o AsyncAPI, com `durationSeconds` e sem título; republicação forçada repete o `eventId`.
- [ ] Dois workers → um processa; worker encerrado → outro retoma após a lease.
- [ ] Smoke no Compose pelo navegador: vídeo enviado em 4.0 passa a *pronto* sem recarregar.
