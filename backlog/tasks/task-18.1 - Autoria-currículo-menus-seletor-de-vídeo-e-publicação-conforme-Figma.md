---
id: TASK-18.1
title: 'Autoria: currículo, menus, seletor de vídeo e publicação conforme Figma'
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
ordinal: 17000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Parte da auditoria (TASK-18) que cobre o editor de currículo (A3/A4/A5), o seletor de vídeo (A6) e o modal de publicação (A7/A7.h). Frames de referência: A3 Alterações não publicadas 160:2050, A3 Publicado v1 160:1853, A3 Curso sem módulos 160:1574, A5 Menu da aula 160:17427, A5 Mover aula 160:17620, A6 Trocar ou desvincular 161:6939, A7 Primeira publicação 161:18215, A7 Republicar v2 161:18438, A7.h 196:13940, composições 157:2.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 Menu da aula mostra um único item 'Mover para outro módulo' que abre submenu com os outros módulos (desabilitado com dica quando só há um módulo); não há item duplicado
- [x] #2 Itens destrutivos dos menus (Remover aula…, Remover módulo…) têm tratamento destrutivo como no Figma
- [x] #3 Contagens usam singular e plural corretos (1 aula, 2 aulas, 1 módulo, 2 módulos) no cabeçalho do módulo e no modal de publicação
- [x] #4 Numeração de módulos e aulas segue o Figma ('1 Título', sem ponto) no editor e no modal de publicação
- [x] #5 Rascunho com pendências mostra a faixa 'N pendências · Ver pendências' no rodapé, que leva às pendências
- [x] #6 Publicado com alterações mostra o aviso 'As alterações ficam no rascunho até você publicar uma nova versão.'
- [x] #7 Publicado sem alterações mostra 'O rascunho está igual à versão vigente.' e o botão 'Publicar nova versão', como no Figma A3 Publicado v1
- [ ] #8 Ações de módulo e aula ficam alinhadas à direita do card em qualquer largura
- [x] #9 Estado vazio 'Comece pelos módulos' tem o botão primário '+ Adicionar módulo'; 'Excluir curso' é link de texto como no Figma
- [ ] #10 Seletor de vídeo mantém o rodapé (Cancelar/Vincular vídeo) visível com a lista longa; 'Desvincular vídeo' é botão outline
- [x] #11 Modal de publicação: título 'Publicar curso' (1ª) ou 'Publicar nova versão', descrição, resumo 'N aulas com vídeo', linha de nível e pré-requisito e rótulo 'Nota de versão' conforme A7/A7.h
- [x] #12 Testes do admin-spa cobrem os comportamentos alterados e passam
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. course-curriculum: plural util, numeração sem ponto, menu da aula com submenu único 'Mover para outro módulo ›' (+Cancelar), dicas de limite, itens destrutivos, faixa de pendências no rodapé, aviso/estado de rascunho igual à vigente, estado vazio com CTA primário.
2. course-publication: botão sempre visível, título/descrição por 1ª ou nova versão, resumo 'N módulos · N aulas com vídeo' sem Revisão, linha de nível/pré-requisito, numeração sem ponto; label 'Nota de versão (opcional)'.
3. CSS (authoring.css): ações alinhadas à direita, destrutivo, seletor de vídeo com lista rolável e rodapé visível, 'Desvincular vídeo' outline, 'Excluir curso' como link de texto.
4. Atualizar/adicionar testes; lint/build/test.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Implementado em course-curriculum/publication/item-actions/delete/video-picker, authoring.css e revision-note-form (rótulo 'Nota de versão'). Botão Publicar nova versão agora sempre visível; o contrato OpenAPI não prevê recusa por falta de alterações, então nenhum tratamento extra além do fluxo de erro existente. Testes novos em authoring-editor-figma.test.tsx; lint/build/test ok.

Validação conjunta 2026-10-10 com TASK-18.2: npm run lint ok, npm run build ok, vitest 50 arquivos/274 testes ok. AC #8 (ações alinhadas à direita) e #10 (rodapé do seletor visível com lista longa) dependem de layout real; jsdom não cobre. Ficam abertos até conferência no navegador.
<!-- SECTION:NOTES:END -->
