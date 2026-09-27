# Revisão full — prd-ingestao-midia (CAP-006)

Run: run.pRm2FrdL
Modo: full · Tentativa: 2/3 · Data: 2026-09-27

- **base_ref:** `ce2be47124fbf409235eda7c4e8b5a22874fee4a`. É o `main` local e também `origin/main`, conferido com `git ls-remote` nesta execução.
- **target_ref:** `main`.
- **validated_commit:** `4ee534aea9d41a27188fc16943febcf942909c50`
- **validated_tree:** `8c6c0ebbf1c439b9bda494321963be92c705cfaa`
- **Diff:** `ce2be47..HEAD`, 16 commits. São 8 checkpoints do ciclo 1 e 4 pares de reabertura e checkpoint do ciclo 2. Ao todo, 206 arquivos.
- **O que mudou desde a full 1 (`df63c88..HEAD`):** só 4 arquivos de teste, mais os relatórios e o `flow-state`. O código de produção, as dependências, a configuração e os Dockerfiles ficaram iguais aos que a full 1 revisou.
- **Onde rodei:** o CI e as mutações rodaram em worktrees temporários do HEAD, já removidos.
- **Estabilidade:** HEAD e `git status --porcelain` estavam iguais no início e no fim da revisão.

**Resultado: FULL VALIDATION APROVADA.** Os 5 bloqueantes do ciclo 1 estão resolvidos e nenhum bloqueante novo apareceu. Ficam 4 recomendações, que não bloqueiam.

## Bloqueantes do ciclo 1

| # | Correção | Evidência nesta execução | Estado |
|---|---|---|---|
| B1 | `StaffInvitationAcceptanceTests.cs:96` passou a esperar `[autoria.ler, midia.enviar]`, que é o catálogo de RF-01. A asserção não foi enfraquecida. | O CI de identity saiu 0, com 114 de 114 testes. | resolvido |
| B2 | `video-library-filter.test.tsx:63` agora usa `await waitFor(() => expect(searchedTitles).toEqual(['injecao']))`. A asserção continua a mesma, só passou a esperar a condição. | `npm test` completo rodou 10 vezes, todas com exit 0 e 47 de 47 testes. Todas as execuções concorreram por CPU com a matriz .NET, a mesma condição em que o teste falhou no ciclo 1. | resolvido |
| B3 | Novo teste `VideoUploadResume_ExpiredFingerprintStartsANewUploadWithoutTheSweep`. Ele avança o relógio além de `ExpiresAt` sem a varredura, repete o fingerprint e confere que o envio é novo, que o antigo ficou `ExpiredAt` e que o multipart foi abortado (`NoSuchUpload`). | M4 morreu. | resolvido |
| B4 | Novo teste `VideoUploadResume_SweepExpiresOnlyAfterTheDeadline`. Ele passa pela varredura real (`IExpirePendingVideoUploads` e `GetExpiredPendingIdsAsync`) com o relógio 1 min antes do prazo e depois no próprio prazo. | M4b e M4c morreram. | resolvido |
| B5 | Novo teste `OldPreparingVideoIsStuckWithoutDuration`. Um vídeo `preparing` de 13 h conta como preso e um de 1 h não conta. | M8 morreu. | resolvido |

## Matriz de CI

- **Fonte:** `.github/workflows/*.yml`, que chamam `tassosgomes/template-pipeline` `ci-dotnet.yml@v1` e `ci-react-ts.yml@v1`.
- **Versão do template:** atualizei as tags da cópia local por `git fetch`. `v1` resolve para `db5006a`, que é o `v1.2.2` no GitHub. Os passos espelhados vêm desse commit.
- **Correção de uma limitação do ciclo 1:** a limitação R3 foi resolvida. A tag local antiga apontava para `6d74521`, mas os passos que o ciclo 1 espelhou já eram os do `db5006a`.
- **Componentes acionados pelo diff:**
  - `media`, `bff-admin`, `identity` e `admin-spa` foram alterados diretamente.
  - O filtro `Directory.*` aciona também audit, bff-student, commerce, learning e notification, porque `Directory.Packages.props` mudou.
- **Passos .NET:**
  1. `dotnet restore`
  2. `dotnet format --verify-no-changes --no-restore`
  3. `dotnet test --no-restore -c Debug --coverage --coverage-output-format cobertura`. O runner é o Microsoft.Testing.Platform, definido em `global.json`.
  4. Cobertura mesclada por união de linhas, com o script Python extraído do próprio workflow `db5006a` e limite de 70%.
  5. `dotnet publish -c Release --no-restore`
  6. `docker build` com o `docker-context` e o `dockerfile` do workflow.
- **Passos do admin-spa:**
  1. `npm ci`
  2. `npm run lint`
  3. `npx --no-install tsc --noEmit`
  4. `npm test`, com vitest e cobertura em `coverage-summary.json`
  5. `npm run build -- --base=/admin/`
  6. `docker build`

| Componente | restore | format/lint | testes | cobertura (≥70) | publish/build | imagem |
|---|---:|---:|---|---:|---:|---:|
| media | 0 | 0 | 0 (106 = 103 + 3 novos) | 90,02 | 0 | 0 |
| bff-admin | 0 | 0 | 0 (60) | 78,37 | 0 | 0 |
| identity | 0 | 0 | 0 (114) | 79,61 | 0 | 0 |
| audit | 0 | 0 | 0 (59) | 83,45 | 0 | 0 |
| bff-student | 0 | 0 | 0 (131) | 83,56 | 0 | 0 |
| commerce | 0 | 0 | 0 (20) | 78,41 | 0 | 0 |
| learning | 0 | 0 | 0 (13) | 76,09 | 0 | 0 |
| notification | 0 | 0 | 0 (51) | 85,45 | 0 | 0 |
| admin-spa | 0 (`npm ci`) | 0 / tsc 0 | 0 nas 10 execuções (47) | 88,65 | 0 | 0 |

- **Passos que não reproduzi:**
  - SonarCloud e a varredura de segurança (`security-mode: observe`). São observacionais.
  - Push para o GHCR. Exige credencial e não mede o código.
- **Migrations:** o modelo EF e as migrations não mudaram desde a full 1, onde `has-pending-model-changes` saiu 0. Não repeti a checagem.

## Sensor de discriminação

As mutações rodaram no worktree temporário `mut`, no HEAD, cada uma contra a suíte focalizada da fatia. Depois de cada uma, restaurei o arquivo. No fim, removi o worktree e conferi que a linha de base continuava igual.

- **Linha de base:** as duas suítes focalizadas passaram, com 17 testes e exit 0.
- **M1–M3, M5–M7 e M9:** não repeti. Elas morreram no ciclo 1, e desde então o código de produção não mudou e nenhum teste foi removido.

| # | Fatia | Mutação | Suíte | Resultado |
|---|---|---|---|---|
| M4 | V-03 | `VideoUploadRepository.cs:50`: `ExpiresAt <= now` → `<= now.AddHours(-24)` | `VideoUploadResumeTests` | morto: `…ExpiredFingerprintStartsANewUploadWithoutTheSweep` |
| M4b | V-03 | `VideoUploadRepository.cs:84`: `ExpiresAt <= now` → `<= now.AddHours(-24)` | `VideoUploadResumeTests` | morto: `…SweepExpiresOnlyAfterTheDeadline` |
| M4c | V-03 | `VideoUploadRepository.cs:84`: `<=` → `<`. Testa a fronteira exata do prazo. | `VideoUploadResumeTests` | morto: `…SweepExpiresOnlyAfterTheDeadline` |
| M8 | V-07 | `MediaVolumeMetricsWorker.cs:44`: `IN ('received','preparing')` → `IN ('received')` | `MediaVolumeMetricsTests` | morto: `OldPreparingVideoIsStuckWithoutDuration` |
| M8b | V-07 | Limite sem duração de `12 hours` → `24 hours` | `MediaVolumeMetricsTests` | morto: 3 falhas |

Nenhum mutante sobreviveu.

## Smoke (stack `docker-compose.yml` + `docker-compose.remote.yml`, mantida no ar)

- **Reconstrução:** rodei `up -d --build media media-worker bff-admin admin-spa` a partir do HEAD, com exit 0.
  - `media`, `media-worker` e `bff-admin` foram recriados e ficaram `healthy`.
  - `admin-spa` não foi recriado. A imagem saiu idêntica, porque desde o último build só mudou um arquivo `.test.tsx`.
- **Sem sessão:** `GET` anônimo em Media `/internal/v1/videos` e no BFF `/api/v1/videos` devolveu 401.
- **MinIO anônimo:** listagem do bucket, com e sem prefixo, devolveu 403.
- **SPA:** `/admin/` devolveu 200.
- **Banco e logs:** o Postgres do compose tem 1 vídeo `ready`, com `original_deleted_at` preenchido. Os logs de `media` e `media-worker` não tiveram erro nem exceção nos 2 min após a recriação.
- **O que não repeti:** a tela com sessão e o envio ponta a ponta. O código de produção não mudou desde o smoke completo do ciclo 1, e não redefini senhas.

## Rastreabilidade e integração

- **Conclusões mantidas do ciclo 1:** RF-01 a RF-11, V-01 a V-07, C-16, 8.0-R5 e os riscos aceitos 8.0-R2, 10.0-R2 e 10.0-R4.
- **Mudanças do ciclo 2:** tocam só testes. Não surgiu nenhum contrato novo entre tasks nem regressão.
- **Revisões por task:** os relatórios de 3.0, 6.0, 9.0 e 10.0 registram revalidações aprovadas das correções.

## Recomendações (não bloqueiam)

1. **Métricas no coletor.** Os instrumentos de volume ainda não foram observados no coletor remoto. A evidência de emissão continua sendo o `MeterListener` dos testes. Esta é a recomendação R2 do ciclo 1.
2. **CI real.** A branch `feature/ingestao-midia` ainda não existe no GitHub. Confirme o CI real quando o PR for aberto.
3. **Pergunta de produto em aberto.** Aceitar `.mkv` com MIME vazio (4.0-R5) continua sem decisão.
4. **Diretórios `publish/` sem controle de versão.** O passo `dotnet publish` gera esses diretórios. Eles só apareceram no worktree temporário, já removido. Não é preciso agir.

---

## Histórico — ciclo 1 (full reprovada)

Execução do ciclo 1: `run.DqHeR9Nv`
Modo: full · Tentativa: 1/3 · Data: 2026-09-27

- **base_ref:** `ce2be47124fbf409235eda7c4e8b5a22874fee4a` (`main` local)
- **target_ref:** `main`. `origin/main` não foi atualizado porque a sessão não tem rede para o GitHub. Isso é uma limitação, não um defeito.
- **validated_commit:** `df63c880c8eea63cd37a127af02cbad61bb84bde`
- **validated_tree:** `2e42bfbd7d3e55a2c75d0eb64210d8a6d7e5e547`
- **Diff:** `ce2be47..HEAD`, 8 checkpoints (tasks 3.0–10.0), 205 arquivos.
- **Onde rodei:** CI e mutações rodaram em worktrees temporários do mesmo HEAD, já removidos.
- **Estabilidade:** HEAD e `git status --porcelain` estavam iguais no início e no fim da revisão.

**Resultado: FULL VALIDATION REPROVADA.** 5 bloqueantes e 5 recomendações.

### Matriz de CI

- **Fonte:** `.github/workflows/*.yml`. Eles chamam `tassosgomes/template-pipeline` `ci-dotnet.yml@v1` e `ci-react-ts.yml@v1`. Li os dois na cópia local `~/github-tassosgomes/template-pipeline`.
- **Componentes acionados pelo diff:**
  - `media`, `bff-admin`, `identity` e `admin-spa` foram alterados diretamente.
  - `Directory.Packages.props` também mudou. O filtro `Directory.*` aciona por isso **todos** os serviços .NET: audit, bff-student, commerce, learning e notification.
- **Passos .NET espelhados:**
  1. `dotnet restore`
  2. `dotnet format --verify-no-changes --no-restore`
  3. `dotnet test --no-restore -c Debug --coverage --coverage-output-format cobertura`
  4. Cobertura mesclada por união de linhas, com limite de 70%
  5. `dotnet publish -c Release`
  6. `docker build` com o Dockerfile do serviço
- **Passos do SPA:** `npm ci`, `npm run lint`, `tsc --noEmit`, `npm test` (vitest com cobertura), `npm run build -- --base=/admin/` e `docker build`.

| Componente | restore | format/lint | testes | cobertura (≥70) | publish/build | imagem |
|---|---:|---:|---|---:|---:|---:|
| media | 0 | 0 | **0** (103) | 89,58 | 0 | 0 |
| bff-admin | 0 | 0 | **0** (60) | 78,37 | 0 | 0 |
| identity | 0 | 0 | **2** (114; 1 falhou) — B1 | 79,61 | 0 | 0 |
| audit | 0 | 0 | 0 (59) | 83,45 | 0 | 0 |
| bff-student | 0 | 0 | 0 (131) | 83,56 | 0 | 0 |
| commerce | 0 | 0 | 0 (20) | 78,41 | 0 | 0 |
| learning | 0 | 0 | 0 (13) | 76,09 | 0 | 0 |
| notification | 0 | 0 | 0 (51) | 85,37 | 0 | 0 |
| admin-spa | 0 | 0 / tsc 0 | **1** na 1ª execução (47; 1 falhou), depois 0/0/0 — B2 | 88,65 | 0 | 0 |

- **Passos que não reproduzi:**
  - SonarCloud e a varredura de segurança (`security-mode: observe`). São observacionais.
  - Push da imagem para o GHCR. Exige credencial.
- **Migrations:** `dotnet ef migrations has-pending-model-changes` em Media saiu 0, sem mudanças pendentes.

### Smoke (stack `docker-compose.yml` + `docker-compose.remote.yml`, mantida no ar)

- **Acesso anônimo ao MinIO:** todas as tentativas foram negadas com 403.
  - Listagem do bucket, com e sem prefixo.
  - `master.m3u8`, playlist de qualidade, segmento `.ts` e `original` de um vídeo pronto.
  - A política do bucket é `private`.
- **Original descartado e chave fora do bucket:**
  - O vídeo pronto só tem `hls/`, sem `original`.
  - A playlist usa `EXT-X-KEY` com a URI opaca `c4c-key:<videoId>`.
  - Nenhum objeto de chave existe no bucket.
- **Banco remoto:** 4 vídeos `ready` e 3 `failed`, todos com `original_deleted_at` preenchido.
- **Sem sessão:** `GET` anônimo em Media `/internal/v1/videos` e no BFF `/api/v1/videos` devolveu 401.
- **Tenant:** o filtro global por tenant fica fechado quando falta tenant. O JWT sem `tenantId` ou sem `sub` é recusado (`MediaJwtBearerOptionsSetup.cs:42-53`).

### Sensor de discriminação

Cada mutação rodou isolada no worktree `mut`, contra a suíte focalizada da fatia. Depois de cada uma, o arquivo foi restaurado. No fim, o worktree foi removido e a linha de base conferida.

| # | Fatia | Mutação | Suíte | Resultado |
|---|---|---|---|---|
| M1 | V-01 | Filtro de tenant de `Video` ignora o `TenantId` (`MediaDbContext.cs:28`) | `VideoLibraryAuthorizationTests` | morto (2 falhas) |
| M2 | V-01 | BFF ignora a falta de `midia.enviar` (`VideoLibraryEndpoints.cs:128`) | BFF `VideoLibraryTests` | morto |
| M3 | V-02 | Limite de 5 GB dobrado (`VideoUpload.cs:69`) | `VideoUploadTests` | morto |
| **M4** | V-03 | Retomada de envio vencido só expira 24 h depois (`VideoUploadRepository.cs:50`) | `VideoUploadResumeTests` | **sobreviveu** — B3 |
| **M4b** | V-03 | Varredura só expira 24 h depois (`VideoUploadRepository.cs:84`) | IntegrationTests inteira (79) e EndToEndTests (9) | **sobreviveu** — B4 |
| M5 | V-04 | Original não é apagado após *pronto* (`PrepareVideo.cs:158`) | `VideoPreparationTests` | morto |
| M6 | V-05 | Limite de 3 h dobrado (`FfmpegVideoTranscoder.cs:108`) | `VideoPreparationFailureTests` | morto |
| M7 | V-06 | Normalização mantém os acentos (`Video.cs:238`) | `VideoTitleSearchTests` | morto (2) |
| **M8** | V-07 | `preparing` deixa de contar como preso (`MediaVolumeMetricsWorker.cs:44`) | `MediaVolumeMetricsTests` | **sobreviveu** — B5 |
| M9 | V-05 (SPA) | Motivo "ilegível" vira o motivo genérico (`videos-area-screen.tsx:40`) | `video-failure` | morto |

### Bloqueantes

#### B1 — CI de identity reprovado por regressão da task 3.0

- **Falha:** `src/identity/tests/CodeForCoders.Identity.IntegrationTests/StaffInvitationAcceptanceTests.cs:96` ainda espera `[autoria.ler]` para o papel professor.
- **Causa:** `StaffRoleCatalog.cs:10` agora concede `[autoria.ler, midia.enviar]` ao professor.
- **Saída:** `Assert.Equal() Failure: Collections differ … Actual: ["autoria.ler", "midia.enviar"]`, 1 de 114 testes, exit 2.
- **Reprodução:** é determinística. Na base, o teste passava.
- **Por que as revisões anteriores não pegaram:** a revisão da 3.0 rodou só `MediaAudienceTests`.
- **Correção:** atualizar a expectativa do teste para o catálogo de RF-01. Não remover o teste.

#### B2 — Teste do admin-spa não determinístico no job obrigatório

- **Onde:** `src/admin-spa/src/app/routes/video-library-filter.test.tsx:63`, teste *searches by title and offers to clear an empty result*.
- **Frequência:** falhou na 1ª execução de `npm test`, com a suíte completa e exit 1. As 3 execuções seguintes e 3 execuções isoladas passaram.
- **Saída:** `expected [] to deeply equal ['injecao']`.
- **Causa:**
  - O `waitFor` da linha 62 é satisfeito quando "Aula falhada" some.
  - Isso pode acontecer pela troca de estado da consulta antes de o handler MSW registrar o `q`.
  - A asserção síncrona seguinte então corre contra o handler.
- **Por que bloqueia:** um job obrigatório de CI falha de forma intermitente.
- **Correção:** esperar a condição observável dentro do `waitFor`, por exemplo `await waitFor(() => expect(searchedTitles).toEqual(['injecao']))`. Não enfraquecer a asserção.

#### B3 — Mutante sobrevivente: retomada depois de 24 h sem a varredura ter rodado

- **Onde:** `src/media/src/CodeForCoders.Media.Infra.Data/Videos/VideoUploadRepository.cs:50`, em `GetExpiredPendingByFingerprintAsync`, usado por `CreateVideoUpload.cs:69-82`.
- **Critério:** RF-03 CA3, "interrompido há mais de 24 horas → começa do zero, e o envio antigo já não existe".
- **Mutação:** `ExpiresAt <= now` → `ExpiresAt <= now.AddHours(-24)`.
- **Resultado:** `VideoUploadResumeTests` (8 testes) passa.
- **Teste que falta:** ele deveria falhar num cenário que avança o relógio além de `ExpiresAt` **sem** a varredura e cria um envio com o mesmo fingerprint. O esperado é envio novo, com o antigo abortado e marcado expirado.

#### B4 — Mutante sobrevivente: a varredura de expiração não é testada pela consulta real

- **Onde:** `VideoUploadRepository.cs:84`, em `GetExpiredPendingIdsAsync`, a consulta usada pelo `ExpiredVideoUploadWorker`.
- **Critério:** RF-03 e V-03, "envio abandonado some em 24 h".
- **Mutação:** `ExpiresAt <= now` → `ExpiresAt <= now.AddHours(-24)`.
- **Resultado:** IntegrationTests (79) e EndToEndTests (9) passam.
- **Causa:** `VideoUploadResumeTests.cs:152` chama `ExpireAsync(uploadId)` diretamente. Nenhum teste passa pela seleção de vencidos.
- **Teste que falta:** um teste pelo ciclo de varredura (seleção + expiração) com relógio controlado, cobrindo os dois lados da fronteira de 24 h.

#### B5 — Mutante sobrevivente: vídeo preso *em preparação* não é medido por teste

- **Onde:** `src/media/src/CodeForCoders.Media.Infra.Messaging/MediaVolumeMetricsWorker.cs:44`.
- **Critério:** PRD, métrica "Vídeo preso", que conta vídeos em *recebido* **ou em preparação* há mais de 4× a duração. A task 10.0 também espera 1 preso.
- **Mutação:** `status IN ('received', 'preparing')` → `status IN ('received')`.
- **Resultado:** `MediaVolumeMetricsTests` (6) passa.
- **Causa:** os testes de presos (`OldReceivedVideoIsStuckWithoutDuration`, `DurationControlsStuckThreshold`) só usam `received`.
- **Teste que falta:** um vídeo `preparing` antigo precisa contar como preso.

### Rastreabilidade e integração

- **Revisões por task:** todas as tasks 3.0–10.0 fecharam com VALIDAÇÃO APROVADA nos seus relatórios.
- **Cobertura dos requisitos:**
  - RF-01 a RF-11 aparecem em código e testes das fatias V-01 a V-07.
  - RF-11 usa a dimensão só `status`, conforme D-06, decidido pelo usuário.
- **Decisões respeitadas:** C-16, 8.0-R5 (fila `media.events.audit`) e os riscos aceitos 8.0-R2, 10.0-R2 e 10.0-R4. Não as reabri.
- **Escada de qualidade:** abaixo de 480p é gerada a própria altura, por exemplo `120p` no vídeo de smoke. Isso está conforme a TechSpec §preparação.
- **Mudança nos serviços não tocados:** `Directory.Packages.props` só **acrescenta** as versões `AWSSDK.S3` e `Testcontainers`. Os serviços não tocados passaram no CI completo.

### Recomendações (não bloqueiam)

1. **Imagens em execução vs. HEAD.** Os IDs das imagens da stack diferem dos builds deste HEAD (`media` `fc95ffdc…`). A diferença é esperada por metadados de build, mas não prova a identidade. Depois das correções, reconstrua `media`, `media-worker`, `bff-admin` e `admin-spa` antes do smoke final.
2. **Métricas no coletor.** Não observei os instrumentos de volume no coletor remoto. A limitação é a mesma da revisão 10.0; a evidência de emissão continua sendo o gate com `MeterListener`.
3. **Cópia local do template.** O HEAD local de `template-pipeline` (`8a958e1`) está à frente da tag `v1` (`6d74521`). Os passos de build e teste relevantes são os mesmos, mas confirme no CI real quando houver rede.
4. **Varredura em lote do CI.** Na primeira execução, o Vitest compete por CPU com as suítes .NET. B2 apareceu nessa condição, o que reforça a correção do teste em vez de novas tentativas.
5. **Pergunta de produto em aberto.** Aceitar `.mkv` com MIME vazio (4.0-R5) continua sem decisão e não bloqueia.

### Próximos passos

Corrigir B1 e B2 nos testes, e acrescentar os testes de B3, B4 e B5 sem enfraquecer os existentes. Depois, repetir a validação full com o CI completo e o sensor.
