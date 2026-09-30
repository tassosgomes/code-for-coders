---
id: TASK-4
title: Desenhar Autoria no Figma e obter aprovação visual (task_01 / CAP-005)
status: Done
assignee:
  - '@codex'
created_date: '2026-09-29 22:09'
updated_date: '2026-09-29 22:53'
labels:
  - design
  - cap-005
dependencies: []
references:
  - tasks/prd-autoria-curso/1_task.md
  - 'https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn?node-id=176-11040'
documentation:
  - docs/design/wireframes-autoria-curso.md
modified_files:
  - docs/design/wireframes-autoria-curso.md
  - tasks/prd-autoria-curso/1_task.md
  - tasks/prd-autoria-curso/tasks.md
  - tasks/prd-autoria-curso/techspec.md
type: task
ordinal: 4000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Executar tasks/prd-autoria-curso/1_task.md a partir do ASCII aprovado. O desenho real e a aprovação explícita do responsável são pré-requisitos das tasks 2–7; a aprovação do ASCII não libera implementação.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 A1–A12 e estados, fluxos, mobile e Dark previstos têm frames navegáveis e node-id registrados no wireframe.
- [x] #2 Responsável recebe o link e aprova explicitamente o desenho no Figma após revisão.
- [x] #3 Data e responsável são registrados apenas após aprovação; o gate passa e as tasks 2–7 podem ser liberadas.
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. Inspecionar telas, componentes, tokens e fontes existentes no Figma e referências do projeto.
2. Desenhar fluxos, A1–A12 e estados, mobile, Dark e composições propostas reaproveitando o DS.
3. Validar estrutura editável, navegação, fontes e aparência; documentar cada node-id.
4. Entregar o desenho para aprovação explícita, mantendo task_01 em validating e implementação bloqueada até resposta.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Desenho materializado no Figma: 106 frames de telas/estados, 13 mobile, 4 Dark, 2 fluxos, índice e 5 composições. Referências de node-id registradas no wireframe. groma view não encontrou owner para este documento; nenhuma referência arquitetural foi inventada. Aprovação visual ainda pendente.

task_01 atualizada para validating com evidências e primeiro critério materializado. Não foi registrada aprovação presumida; tasks 2–7 seguem bloqueadas.

Validação: Markdown e 14 links locais OK; 116 node-ids únicos no inventário; seções 1–4 e decisões G1–G13 preservadas. Figma: 106 frames, 1586 destinos NAVIGATE válidos, A1–A12 alcançáveis a partir de A12, fontes do DS, sem rasterização da UI e sem painéis fora do viewport. git diff --check passou. Gate de aprovação retornou exit 1, esperado enquanto não houver aprovação explícita. Escopo exclusivo de Figma/documentação: build, lint e testes das aplicações não se aplicam; não houve alteração de código. TASK-4 permanece In Progress (Backlog não possui validating); task_01 permanece validating.

Aprovação explícita recebida do responsável pelo produto (usuário desta conversa) em 2026-09-29: “Está aprovado”, após a entrega do link do Figma real. Registro atualizado no wireframe e no índice do Figma; task_01 marcada done e checkbox do plano concluído. Pendência visual da TechSpec satisfeita. Gate literal do frontmatter executado com exit 0; Markdown, links locais e git diff --check passaram. Tasks 2–7 seguem pending, com dependências técnicas preservadas; nenhuma implementação iniciada. Alterações exclusivas de documentação e metadados: lint, build e testes das aplicações não se aplicam.
<!-- SECTION:NOTES:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
Entregues no Figma 106 telas/estados, dois fluxos e cinco composições, com inventário de node-ids no wireframe. Responsável aprovou explicitamente em 2026-09-29; gate de aprovação passou (exit 0), documentação e plano atualizados. Task_01 concluída e bloqueio visual das tasks 2–7 satisfeito. Navegação e layouts do Figma revisados; Markdown, links locais e git diff --check verificados.
<!-- SECTION:FINAL_SUMMARY:END -->
