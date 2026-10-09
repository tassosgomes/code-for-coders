---
id: TASK-14
title: Remover referências operacionais do Coolify
status: Done
assignee: []
created_date: '2026-10-09 12:36'
updated_date: '2026-10-09 12:46'
labels: []
dependencies: []
modified_files:
  - docker-compose.coolify.apis.yml
  - docker-compose.coolify.web.yml
  - docker-compose.komodo.apis.yml
  - docker-compose.komodo.web.yml
  - README.md
  - infra/cloudflared/billing-webhook.yml
  - docs/foundation.md
  - docs/adr/0019-runtime-e-deploy-atual.md
  - docs/adr/index.md
  - docs/adr/0002-plataforma-de-runtime-coolify.md
  - context/architecture-baseline.md
  - docs/course-publication-rollout.md
  - docs/design/wireframes-catalogo-vitrine.md
  - docs/design/wireframes-autoria-curso.md
  - docs/adr/0004-autenticacao-de-servico-bff-identity.md
  - docs/adr/0006-preparacao-de-video-e-custodia-de-chave.md
  - docs/adr/0007-snapshots-efemeros-da-consulta-de-auditoria.md
  - docs/adr/0009-autenticacao-de-servico-bff-aluno-em-servicos-de-dominio.md
  - docs/adr/0010-autenticacao-de-servico-commerce-em-identity.md
  - docs/adr/0011-autenticacao-de-servico-media-learning-em-commerce.md
  - >-
    docs/adr/0014-entrega-de-segmentos-por-credencial-opaca-e-porta-de-distribuicao.md
  - docs/adr/0015-escopos-de-servico-media-learning-em-commerce-por-rota.md
  - docs/adr/0016-jwt-de-aluno-em-commerce.md
  - docs/foundation-plan.md
type: chore
ordinal: 12000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Dev e estável agora rodam respectivamente pelo Komodo no desenv-server e por Docker Compose no infra-server. O repositório ainda contém Compose específicos da plataforma aposentada, instruções de variáveis que citam o antigo painel e documentos de runtime que a marcam como decisão vigente; essas referências podem levar a um deploy incorreto.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 Os Compose atualmente usados pelo Komodo e pelo ambiente estável não têm instruções nem interpolação específicas da plataforma aposentada.
- [x] #2 Os arquivos Compose exclusivos da plataforma antiga são removidos e a ADR de runtime vigente descreve Komodo, Compose, Caddy e a separação dos hosts.
- [x] #3 Os guias operacionais ativos apontam para os domínios, arquivos e ambiente atuais.
- [x] #4 Os Compose dev e stable continuam validando com a configuração remota correspondente.
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. Atualizar o Compose do Komodo para instruções de ambiente sem dependência do painel antigo e remover os arquivos Compose que só serviam à plataforma aposentada. 2. Atualizar os guias de deploy e as decisões arquiteturais para o runtime atualmente implantado, marcando a decisão anterior como supersedida. 3. Ajustar apontamentos operacionais, links e domínios obsoletos; preservar registros de implantação arquivados e o estado TSG gerado. 4. Pesquisar referências restantes e validar os Compose ativos.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Validação final: `ssh desenv-server ... docker compose ... config --quiet` e `ssh infra-server ... docker compose ... config --quiet` retornaram exit 0. `test ! -e ...` confirmou ausência dos Compose legados; a busca nos guias e configs ativos não encontrou domínios `.lab` nem referências operacionais ao Coolify. As menções restantes estão no ADR-0002 histórico supersedido e nos links para esse registro. `git diff --check` passou.
<!-- SECTION:NOTES:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
Removidos os Compose legados e as referências operacionais; ADRs e guias agora descrevem Komodo/dev e Compose/estável. Os dois stacks remotos passaram em `docker compose config --quiet`; registros históricos foram preservados.
<!-- SECTION:FINAL_SUMMARY:END -->
