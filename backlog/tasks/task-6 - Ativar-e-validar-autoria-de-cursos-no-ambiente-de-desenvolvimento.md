---
id: TASK-6
title: Ativar e validar autoria de cursos no ambiente de desenvolvimento
status: Done
assignee:
  - '@codex'
created_date: '2026-10-02 20:15'
updated_date: '2026-10-02 20:21'
labels: []
dependencies: []
documentation:
  - tasks/prd-autoria-curso/prd.md
  - docs/course-video-projection-rollout.md
modified_files:
  - .env
  - docs/course-video-projection-rollout.md
type: bug
ordinal: 6000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
A área de autoria está disponível na SPA, porém o BFF Admin em execução usa CourseAuthoring__Enabled=false e não registra /api/v1/courses. O PRD está concluído sem a ativação operacional do ambiente remoto de desenvolvimento.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 Pré-requisitos de publicação e reconciliação de vídeos conferidos no ambiente atendido.
- [x] #2 A flag de autoria persiste no ambiente de desenvolvimento e uma chamada autenticada de listagem retorna HTTP 200 pela SPA.
- [x] #3 Procedimento de ativação documentado com comandos concretos e validação das rotas.
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. Confirmar flag, rotas e pré-requisitos no servidor de infraestrutura autorizado.
2. Reproduzir fatos e reconciliar vídeos com os comandos operacionais dos serviços.
3. Persistir COURSE_AUTHORING_ENABLED=true em .env e recriar somente o BFF Admin.
4. Validar listagem pela SPA com sessão autenticada e registrar o procedimento e evidências.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Causa confirmada: CourseAuthoring__Enabled=false no container. Infra remota check OK; migrations de autoria/publicação/projeção aplicadas. Replay Media: 9 fatos históricos. Manifesto HTTP Media: 6 vídeos ready; operação Learning reconciliou exatamente os IDs (exit 0). Worker Media reiniciado e healthy. RabbitMQ alterado de 16 para 64 MiB em runtime e em ~/infra/rabbitmq/rabbitmq.conf com backup, sem reiniciar infraestrutura.

Reprodução em navegador autenticado: GET /api/v1/courses?_page=1&_size=20 retornou 404 vazio antes e 200 depois da ativação, com data=[] e pagination page=1,size=20,total=0. Tela /admin/autoria mostra estado vazio e Novo curso, sem erros de console. COURSE_AUTHORING_ENABLED=true persistido em .env e confirmado no container recriado. Verificação: dotnet format BffAdmin --verify-no-changes passou; build Release BffAdmin API passou com 0 warnings/erros; CourseAuthoringAccessTests 5/5; Compose config e git diff --check do documento passaram; Learning/BFF ready healthy e filas/DLQs verificadas vazias com consumidores ativos.
<!-- SECTION:NOTES:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
Autoria ativada no ambiente de desenvolvimento após replay e reconciliação dos 6 vídeos ready, partida do worker Media e ajuste persistente do broker para 64 MiB. Flag persistida no .env; BFF recriado. Com a mesma sessão de teste, o endpoint pela SPA passou de 404 para 200 e a tela abriu normalmente. Procedimento documentado em docs/course-video-projection-rollout.md. Build, formatação, configuração Compose e 5 testes de acesso passaram.
<!-- SECTION:FINAL_SUMMARY:END -->
