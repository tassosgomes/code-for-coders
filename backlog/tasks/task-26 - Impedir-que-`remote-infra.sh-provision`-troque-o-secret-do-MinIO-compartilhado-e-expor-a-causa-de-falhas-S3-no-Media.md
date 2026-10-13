---
id: TASK-26
title: >-
  Impedir que `remote-infra.sh provision` troque o secret do MinIO compartilhado
  e expor a causa de falhas S3 no Media
status: Done
assignee:
  - '@claude'
created_date: '2026-10-12 01:04'
updated_date: '2026-10-12 01:09'
labels:
  - infra
  - media
dependencies: []
references:
  - codeforcoders-media-application-dependencyinjection
  - codeforcoders-media-infra-data-dependencyinjection
modified_files:
  - >-
    src/media/src/CodeForCoders.Media.Application/Exceptions/StorageUnavailableException.cs
  - >-
    src/media/src/CodeForCoders.Media.Infra.Data/Adapters/S3MediaStorageAdapter.cs
  - src/media/tests/CodeForCoders.Media.UnitTests/MediaStorageBoundaryTests.cs
  - scripts/remote-infra.sh
  - scripts/tests/remote-infra-provision.test.py
type: bug
ordinal: 33000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Em 2026-10-11, uma sessão Codex rodou `scripts/remote-infra.sh provision` num worktree com `.env` recém-gerado. O script criou um `REMOTE_S3_SECRET_KEY` aleatório e o `mc admin user add` sobrescreveu o secret do usuário compartilhado `code_for_coders_media` no MinIO do servidor. Media e media-worker (dev) passaram a receber 403 SignatureDoesNotMatch e a playlist devolvia 502. O diagnóstico foi lento porque `S3MediaStorageAdapter.ExecuteAsync` converte qualquer erro em `StorageUnavailableException` sem registrar a causa original. Os passos de PostgreSQL e RabbitMQ do mesmo script também redefinem as senhas dos usuários compartilhados.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 Com `.env` sem os secrets remotos e usuários já existentes no servidor, `provision` aborta antes de alterar qualquer credencial e orienta a copiar os valores do `.env` canônico
- [x] #2 Com os secrets presentes no `.env`, `provision` continua idempotente (reaplica os mesmos valores)
- [x] #3 Falhas S3 no Media registram em log o código de erro e o status HTTP de origem, sem expor credenciais, e a API continua respondendo com o mesmo erro
- [x] #4 Testes do adapter e lint/build do Media passam
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. Media: StorageUnavailableException passa a carregar causa e exceção original; S3MediaStorageAdapter informa status HTTP e ErrorCode do S3 (sem credenciais). Resposta da API inalterada (title fixo no GlobalExceptionHandler).
2. remote-infra.sh provision: antes de gerar qualquer secret ausente no .env, verificar no servidor se o role PostgreSQL, os usuários RabbitMQ e o usuário MinIO já existem; se algum existir, abortar sem escrever .env nem alterar credenciais, indicando copiar o valor do .env canônico.
3. Testes: unit test do adapter (S3 indisponível/403 → causa na mensagem), teste do script com ssh/docker falsos (aborta sem mutação; com secrets presentes segue e reaplica o mesmo valor).
4. Build, lint e testes do Media; teste do script.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Incidente 2026-10-11: secret do usuário MinIO code_for_coders_media restaurado no servidor para o valor do .env principal (mesmo dos containers Komodo); leitura de master.m3u8 com as credenciais do media voltou a funcionar.
Validação: dotnet build Media.slnx (0 warnings); dotnet format --verify-no-changes nos arquivos alterados; UnitTests 49/49; ArchitectureTests 8/8; IntegrationTests PlaybackDeliveryTests+VideoUploadTests 26/26; scripts/tests/remote-infra-provision.test.py 3/3 (aborta sem mutação; reaplica secrets presentes; gera secrets em primeiro provision); remote-media-config.test.py 2/2; shellcheck limpo. Execução real contra desenv-server com .env sem secrets: abortou listando as 4 credenciais, sem alterar nada.
<!-- SECTION:NOTES:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
provision agora aborta, antes de escrever .env ou alterar o servidor, quando faltam no .env os secrets de usuários PostgreSQL/RabbitMQ/MinIO que já existem; o Media passa a carregar status HTTP e ErrorCode do S3 (ex.: "S3 403 SignatureDoesNotMatch") e a exceção original na StorageUnavailableException, sem mudar a resposta da API. Verificado com testes unitários, de integração, de script e execução real contra desenv-server.
<!-- SECTION:FINAL_SUMMARY:END -->
