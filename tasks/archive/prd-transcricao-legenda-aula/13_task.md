---
status: done
task_kind: vertical
blocked_by: ["4.0", "7.0"]
gate: 'dotnet test --project src/media/tests/CodeForCoders.Media.IntegrationTests/CodeForCoders.Media.IntegrationTests.csproj -- --filter-class CodeForCoders.Media.IntegrationTests.TranscriptionRealServiceSmokeTests --minimum-expected-tests 1'
gate_expect: "1 teste de integração passa contra o serviço real (teste explícito, fora do CI agregado; exige as variáveis do smoke e, sem elas, falha)"
---

# 13.0 Uma aula real é transcrita ponta a ponta contra o serviço de transcrição

**Fatia:** V-01 e V-03 (smoke real) · **Cobre:** RF-01, RF-02, RF-07 (validação em ambiente real), RN-M23 · **Spec:** `techspec.md#verificação` · **ADR:** 0019, 0020

## Comportamento

Um vídeo curto real, preparado pela pilha, é transcrito pelo serviço real
(`https://whisper.lab.tasso.dev.br`) até `ready`, com o texto guardado e nenhum objeto de áudio restante
no prefixo `transcription/`. O teste usa o adaptador real do `media`, é **explícito** (fica fora da execução
agregada do CI e só roda quando selecionado pela classe, como no gate) e **falha**, não pula, se as
variáveis do smoke não estiverem definidas.

**Pré-requisitos operacionais (EN-02, fora deste repositório, por ambiente):**

- `TRUSTED_PRIVATE_SOURCES` no serviço com a origem do áudio: MinIO remoto de desenvolvimento
  `http://192.168.0.5:9000`; MinIO do estável `http://192.168.0.11:9000`; AWS S3 HTTPS público em
  produção, sem entrada.
- `TRUSTED_PRIVATE_WEBHOOKS` com o receptor alcançável da API do `media` (Q-C); onde ele não é
  alcançável, o ambiente fecha só por consulta periódica e o aviso não é exigido.
- Uma conta por ambiente no serviço, com `Transcription__Service__ApiKey` e o segredo do webhook
  entregues ao `media` como segredos de cada ambiente (Q-D). Hoje **as duas listas ainda não estão
  configuradas no deploy**.

**Checkpoint em dev:** `scripts/remote-infra.sh provision` e `migrate`, `scripts/apps.sh start --remote`
com `Transcription__Enabled=true` e `AwsMedia__EndpointTranscription=http://192.168.0.5:9000`; em
`http://localhost:8081/admin/videos` transcrever um vídeo curto, ver `transcrevendo` → `pronta`, e ver o
mesmo vídeo com legenda e painel na aula do aluno. Com o webhook configurado, o tempo até pronta é menor
que a cadência de consulta; sem ele, fecha por consulta.

## Fora do escopo desta task

- Configurar o serviço de transcrição, emitir contas e segredos: é do operador (outro repositório).
- Carga em massa dos vídeos existentes: fora do PRD.

## Decisões fechadas

- Smoke real só depois de EN-02; o código e os testes com simulador não dependem dele.
- Vídeo curto para limitar a carga na fila do serviço (SLO p95 de 24 h).
- Segredos nunca entram no repositório.

## Modificar / Referenciar

- **modificar:** nenhum código de produto; o teste de smoke e a documentação operacional de variáveis do smoke em `src/media/tests/CodeForCoders.Media.IntegrationTests` (classe `TranscriptionRealServiceSmokeTests`)
- **ref:** `scripts/remote-infra.sh`; `scripts/apps.sh`; `docker-compose.remote.yml`; `~/github-tassosgomes/whisper/docs/adr/adr-003.md`; `techspec.md` (§ Configuração e operação)

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/media` | `dotnet format src/media/CodeForCoders.Media.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| Ambiente remoto | `scripts/remote-infra.sh check` | exit 0 (conectividade com a infraestrutura remota) | AGENTS.md (ambiente de testes) |

## Pronto quando

- [ ] Gate focalizado passa (exit 0) com as variáveis do smoke definidas.
- [ ] Vídeo curto real fica `ready` em dev, com texto legível na leitura do professor.
- [ ] O prefixo `transcription/` do bucket está vazio ao fim.
- [ ] `TRUSTED_PRIVATE_SOURCES` (e, onde houver receptor, `TRUSTED_PRIVATE_WEBHOOKS`) foram confirmados pelo operador para o ambiente testado.

## Encerramento (2026-10-13)

Concluída pelo responsável com verificação ponta a ponta no ambiente dev, depois do merge da PR #225:

- Deploy em dev (`desenv-server`) com `TRANSCRIPTION_ENABLED=true` e as migrations `AddVideoTranscriptions` e
  `AddTranscriptionAudioCleanups` aplicadas por `scripts/remote-infra.sh migrate`.
- Serviço de transcrição (`192.168.0.11`, `/home/tsgomes/whisper-api/compose.yml`) com
  `TRUSTED_PRIVATE_SOURCES=http://192.168.0.5:9000,http://192.168.0.11:9000` na api e no worker (EN-02, fontes).
- Transcrição pedida pelo professor na tela Vídeos: envio aceito (202), job `completed`, tentativa `ready` com 20
  trechos e 133.590 ms; áudio temporário apagado do bucket (RN-M23). O responsável conferiu a leitura do professor,
  o painel do aluno (busca e clique) e a legenda no player.
- Antes da allowlist, três envios receberam 422 do serviço e contaram como falhas de envio; a tentativa seguiu e
  ficou pronta sem intervenção.

Não executados: o teste explícito `TranscriptionRealServiceSmokeTests` (o gate desta task, que exige banco exclusivo
e as variáveis descritas em `TRANSCRIPTION-SMOKE.md`) e o aviso (webhook), que segue sem `TRUSTED_PRIVATE_WEBHOOKS`,
destino e segredo configurados; o resultado chegou pela consulta periódica, como a task autoriza. A reavaliação do
`AttemptTimeout` de `GetResultAsync` (review 4.0, rec. 2) segue sem evidência de resultado grande.
