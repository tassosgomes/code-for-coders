---
id: TASK-13
title: Documentar topologia e automatizar atualização dos ambientes
status: Done
assignee: []
created_date: '2026-10-09 12:11'
updated_date: '2026-10-09 12:15'
labels: []
dependencies: []
modified_files:
  - deploy/topology.md
  - scripts/update-containers.sh
  - deploy/stable/README.md
type: task
ordinal: 11000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
A aplicação agora roda em dois hosts: dev no desenv-server, gerenciado pelo stack Compose usado pelo Komodo, e estável no infra-server, com Compose e dependências isoladas. É necessário deixar a topologia de entrada, redes Docker e serviços compreensível, além de facilitar a atualização das duas cópias remotas sem substituir configurações locais ou dados persistentes.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 Um diagrama representa o roteamento DNS/Caddy e a separação dos containers e dependências de dev e estável.
- [x] #2 Um script oferece atualização explícita para dev, stable ou ambos usando os arquivos e projetos Compose existentes.
- [x] #3 A atualização preserva os arquivos .env e volumes persistentes, e a opção de simulação não altera os hosts.
- [x] #4 A documentação informa pré-requisitos, uso e que o script envia o working tree atual.
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. Registrar em Mermaid a entrada DNS/Caddy, os hosts, stacks, redes e dependências isoladas observadas nos servidores. 2. Criar um script com alvos dev, stable e all, que sincroniza o working tree sem arquivos de ambiente/artefatos e roda os comandos Compose corretos por host. 3. Documentar uso, limites e comportamento de dry-run.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Topologia conferida no Caddyfile e nas redes Docker c4c-shared dos dois hosts; docker compose ls confirmou projetos, diretórios e arquivos Compose em execução. Os comandos docker compose config --quiet dos projetos dev e stable retornaram sucesso. bash -n e --help do script passaram. scripts/update-containers.sh all --dry-run retornou 0, listou os dois destinos e, após filtros adicionados, não listou .cursor, logs do Playwright nem qa-evidence; o modo retornou antes de executar Compose. Nenhuma atualização real dos containers foi feita.
<!-- SECTION:NOTES:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
Adicionei o diagrama Mermaid da topologia atual, o script executável scripts/update-containers.sh com alvos dev, stable e all e as instruções de uso. A sintaxe, a ajuda, as configurações Compose dos dois hosts e o dry-run de all foram validados; nenhum container foi atualizado.
<!-- SECTION:FINAL_SUMMARY:END -->
