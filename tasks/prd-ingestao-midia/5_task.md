---
status: pending
task_kind: enabling
blocked_by: ["3.0"]
gate: 'docker build -f src/media/Dockerfile -t code-4-coders-media:en03 . && docker run --rm --entrypoint ffmpeg code-4-coders-media:en03 -version && docker run --rm --entrypoint ffprobe code-4-coders-media:en03 -version'
gate_expect: 'exit 0: a imagem final de Media constrói e ffmpeg e ffprobe executam dentro dela'
---

# 5.0 Media parte como worker em container próprio, com ffmpeg na imagem e no CI

**Fatia:** EN-03 · **Cobre:** — (habilitador de RF-03 e RF-04) · **Spec:** `techspec.md` § Habilitadores (EN-03), § Media worker — preparação · **ADR:** [ADR-0006](../../docs/adr/0006-preparacao-de-video-e-custodia-de-chave.md), [ADR-0002](../../docs/adr/0002-plataforma-de-runtime-coolify.md)

**Por que é habilitador:** nenhuma fatia pode preparar vídeo ou varrer envios vencidos sem um processo
`worker` implantável e sem ffmpeg na imagem e no job de CI. O risco do CI é próprio: o workflow
reutilizável `ci-dotnet.yml@v1` não tem entrada para passo de instalação, e isso precisa estar resolvido
antes de a maior fatia (7.0) depender dele. Desbloqueia 6.0 (varredura de expiração) e 7.0.

## Comportamento

- A mesma imagem de Media parte em um de dois papéis, escolhido por configuração (`api` ou `worker`).
  No papel `api`, expõe os endpoints de vídeo e o health. No papel `worker`, não expõe endpoints de
  vídeo, expõe só o health, e registra os serviços em segundo plano do worker (nesta task, só o
  publicador do outbox; preparação e varreduras chegam em 6.0/7.0). Papel ausente ou inválido → a
  aplicação não parte, com erro claro.
- O publicador do outbox continua seguro com os dois papéis ativos (reivindicação por `SKIP LOCKED`).
- `docker-compose.yml` e `docker-compose.coolify.yml` ganham o serviço `media-worker` (mesma imagem,
  papel `worker`, volume de trabalho próprio); o serviço `media` fica explicitamente com papel `api`.
- ffmpeg e ffprobe estão na imagem **final** de Media.
- **ffmpeg no CI:** os testes de integração que chamam ffmpeg rodam no job de CI de Media com o binário
  real, sem pular teste quando ele falta. O mecanismo fica no repositório (workflow chamador ou fixture
  de teste); o template `tassosgomes/template-pipeline` não é editado. Um teste de integração confirma
  que ffmpeg e ffprobe estão disponíveis e falha se não estiverem.

## Fora do escopo desta task

Reivindicação, preparação, chave e publicação de HLS (7.0). Varreduras (6.0 e 7.0). Chave-mestra em
produção (plataforma; questão em aberto da TechSpec).

## Decisões fechadas

- Papéis `api` e `worker` da mesma imagem, worker em container separado: ADR-0006; decidido pelo usuário em 2026-09-25 (D-01).
- Worker roda no Coolify: ADR-0002.
- Nenhum teste de ffmpeg é pulado por ausência do binário.

## Modificar / Referenciar

- **modificar:** `src/media/Dockerfile` (ffmpeg na imagem final)
- **modificar:** `src/media/src/CodeForCoders.Media.Api/Program.cs`, `src/media/src/CodeForCoders.Media.Api/Extensions/ServiceConfigurationExtensions.cs`, `src/media/src/CodeForCoders.Media.Api/appsettings.json` (papel)
- **modificar:** `src/media/src/CodeForCoders.Media.Infra.Messaging/DependencyInjection.cs` (hosted services por papel)
- **modificar:** `docker-compose.yml`, `docker-compose.coolify.yml` (`media-worker`, volume de trabalho)
- **modificar:** `.github/workflows/media.yml` (se o mecanismo escolhido para ffmpeg no CI estiver no chamador)
- **ref:** `src/media/src/CodeForCoders.Media.Infra.Messaging/OutboxPublisherWorker.cs` (`SKIP LOCKED`); `tassosgomes/template-pipeline/.github/workflows/ci-dotnet.yml@v1` (entradas disponíveis: `runs-on`, `test-args` etc.); skill `dotnet`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/media` | `dotnet format src/media/CodeForCoders.Media.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/media` | `dotnet build src/media/CodeForCoders.Media.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/media` | `dotnet test --project src/media/tests/CodeForCoders.Media.ArchitectureTests/CodeForCoders.Media.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/media` | `dotnet test --project src/media/tests/CodeForCoders.Media.EndToEndTests/CodeForCoders.Media.EndToEndTests.csproj` | exit 0, inclusive partida nos dois papéis com os registros reais | `ci-dotnet.yml` Testes |
| `src/media` | `dotnet test --project src/media/tests/CodeForCoders.Media.IntegrationTests/CodeForCoders.Media.IntegrationTests.csproj` | exit 0, inclusive o teste de disponibilidade de ffmpeg/ffprobe | `ci-dotnet.yml` Testes |
| Compose | `docker compose config --quiet` | exit 0 | Compose local |

## Pronto quando

- [ ] Gate passa (exit 0).
- [ ] `docker compose up media media-worker`: os dois ficam saudáveis; o worker não responde às rotas de vídeo e o `media` responde.
- [ ] Papel inválido impede a partida com mensagem clara.
- [ ] O teste de disponibilidade de ffmpeg passa localmente e o mecanismo de CI está registrado no relatório da task.
