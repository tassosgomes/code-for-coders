---
id: TASK-18.2
title: 'Autoria: cabeçalho do curso, lista, versão e textos conforme Figma'
status: In Progress
assignee:
  - '@claude'
created_date: '2026-10-10 20:19'
updated_date: '2026-10-10 20:29'
labels:
  - frontend
  - bug
dependencies: []
parent_task_id: TASK-18
type: bug
ordinal: 18000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Parte da auditoria (TASK-18) que cobre o cabeçalho do curso (A3), a lista de cursos (A1/A1.g), a seção Para quem é este curso (A3.h/A3.i), Histórico e Versão (A9/A10). A descrição do curso hoje aparece inteira com markdown cru; decisão do usuário: renderizar como markdown. Frames: A1.a 158:88, A1.g 196:14116, A3 160:2050, A3.h 193:22487, A3.i 193:24006, A9 161:20336, A10.h 196:14175.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 A descrição do curso no cabeçalho é renderizada como markdown seguro (sem HTML arbitrário)
- [ ] #2 'Editar dados' fica à direita do bloco de título, alinhado ao topo, em qualquer largura
- [x] #3 Breadcrumb mostra 'Autoria › Cursos › Título'
- [x] #4 Datas seguem o Figma: forma relativa ('hoje, 10:20') quando aplicável na lista e nos metadados; metadados 'Editado por X hoje, 10:20'; versão 'Publicada por X · data'
- [x] #5 Na lista, o indicador 'Sem nível' fica na coluna Estado, com ícone de alerta, como em A1.g
- [x] #6 Campo de pré-requisito tem o placeholder do Figma e 'Nenhum curso recomendado.' aparece na caixa com borda
- [x] #7 Testes do admin-spa cobrem os comportamentos alterados e passam
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. Utilitário format-relative-date (+teste). 2. react-markdown para descrição (curso e versão). 3. Breadcrumb, Editar dados, Sem nível na coluna Estado, placeholders. 4. Ajustar testes; lint/build/test.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Implementado: react-markdown@10 (MarkdownText, skipHtml, sem img, headings compactos) no cabeçalho e na versão; format-moment util (+teste); breadcrumb; Editar dados à direita; Sem nível na coluna Estado; placeholder e caixa do vazio. Lint/build/test verdes (274 testes).

Validação conjunta 2026-10-10 com TASK-18.1: lint ok, build ok, vitest 274/274 ok. AC #2 (Editar dados à direita em qualquer largura) é só CSS; fica aberto até conferência no navegador.
<!-- SECTION:NOTES:END -->
