---
id: TASK-17
title: >-
  Corrigir falha no segundo upload de vídeo quando o primeiro termina de
  preparar
status: Done
assignee:
  - '@codex'
created_date: '2026-10-10 19:22'
updated_date: '2026-10-10 19:37'
labels:
  - frontend
  - bug
dependencies: []
references:
  - codeforcoders-media-infra-data-dependencyinjection
modified_files:
  - scripts/tests/remote-media-config.test.py
  - docker-compose.remote.yml
  - scripts/remote-infra.sh
  - README.md
priority: high
type: bug
ordinal: 15000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
No Admin SPA, enviar um vídeo, aguardar Preparando e iniciar outro upload provoca falha no segundo quando o primeiro deixa Preparando. Reprodução solicitada em Playwright com as credenciais de teste.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 Reprodução anterior à correção registrada com Playwright.
- [x] #2 O segundo upload continua e conclui quando o primeiro sai de Preparando, sem erro nem perda de progresso.
- [x] #3 Regressão automatizada e lint, build e testes pertinentes passam.
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. Registrar com Playwright, no deploy dev, o segundo vídeo enviado durante a preparação do primeiro e cruzar estados com logs e banco. 2. Corrigir a divergência de bucket S3 entre a stack local --remote e o deploy, que compartilham a fila de preparação; usar AWS_MEDIA_BUCKET/AWS_MEDIA_OBJECT_PREFIX de forma consistente no provisionamento, API, worker e edge. 3. Adicionar regressão da configuração Compose, repetir uploads pelo deploy com ambos os workers alinhados e executar lint, build e testes pertinentes.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Preparação do ambiente: admin de backoffice não tem midia.enviar; usar QA_PROFESSOR_USER/PASSWORD. A tentativa inicial de upload retornou 502 MEDIA_UNAVAILABLE: containers Media usavam credencial S3 diferente da .env. Provisionamento idempotente e recriação somente dos containers de mídia para alinhar o ambiente; tentativa bloqueada preservada em qa-evidence/admin-video-consecutive/before/setup-blocked.json.

Usuário direcionou a reprodução para https://dev-code4coders.tasso.dev.br/admin/videos. Playwright enviou A e depois B enquanto A estava Em preparação. B teve uma falha de download no worker local e foi reprocessado pelo deploy após o worker local ser parado; terminou pronto com preparation_attempts=2. Os dois workers compartilham Database=code_for_coders_media, mas local usa bucket code-for-coders-media e deploy usa code4coders-midia. Logs e consulta direcionada aos vídeos de QA em qa-evidence/admin-video-consecutive/before/. Investigação continua para distinguir a interferência do ambiente do defeito reportado.

Com o worker local parado, o mesmo cenário no deploy passou: ambos vídeos Pronto, sem erros de página. Credenciais S3, master key e prefixo dos workers coincidem; a única divergência é AwsMedia__BucketName. Ajuste de configuração dentro do escopo do bug; nenhuma mudança necessária no SPA.

Correção aplicada: override remoto e provisionamento agora compartilham AWS_MEDIA_BUCKET/AWS_MEDIA_OBJECT_PREFIX do deploy; check faz HEAD S3 autenticado. .env e containers locais de mídia alinhados ao bucket code4coders-midia. Playwright no deploy, com ambos workers ativos: dois vídeos Pronto em ~34 s, sem erros, ambos preparation_attempts=1. ShellCheck, bash -n, regressão Compose (2 testes), lint/build Admin SPA, suíte completa (47 arquivos/256 testes) e diff --check passaram. Controle negativo do check com bucket inexistente falhou corretamente. Limite explícito: a tentativa de preparação antes da correção foi confirmada por log e banco; o worker conflitante foi parado antes do estado terminal Falhou, permitindo a recuperação do vídeo. Relatório em qa-evidence/admin-video-consecutive/qa_report.md.
<!-- SECTION:NOTES:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
Corrigida divergência de bucket entre workers local --remote e deploy que consomem a mesma fila, evitando tentativas de preparação no armazenamento errado. Configuração, provisionamento e check S3 alinhados; regressão Compose adicionada e documentação atualizada. Validado com Playwright no deploy: ambos vídeos Pronto, uma tentativa cada, com os dois workers ativos. Lint/build e 256 testes Admin SPA, 2 regressões Compose e ShellCheck passaram. A reprodução confirmou falha no download do original; não aguardou esgotar tentativas até Falhou. Evidências: qa-evidence/admin-video-consecutive/qa_report.md.
<!-- SECTION:FINAL_SUMMARY:END -->
