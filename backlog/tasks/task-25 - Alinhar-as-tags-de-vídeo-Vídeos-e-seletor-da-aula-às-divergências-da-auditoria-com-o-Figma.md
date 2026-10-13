---
id: TASK-25
title: >-
  Alinhar as tags de vídeo (Vídeos e seletor da aula) às divergências da
  auditoria com o Figma
status: Done
assignee:
  - '@claude'
created_date: '2026-10-12 00:02'
updated_date: '2026-10-13 18:29'
labels:
  - frontend
dependencies: []
references:
  - admin-spa
modified_files:
  - src/admin-spa/src/features/videos/components/video-tag-field.tsx
  - src/admin-spa/src/features/videos/components/edit-video-tags-dialog.tsx
  - src/admin-spa/src/features/videos/components/videos-area-screen.tsx
  - >-
    src/admin-spa/src/features/course-authoring/components/course-video-picker.tsx
  - src/admin-spa/src/app/routes/authoring-course-route.tsx
  - src/admin-spa/src/assets/videos.css
  - src/admin-spa/src/assets/authoring.css
  - src/admin-spa/src/assets/app.css
type: bug
ordinal: 32000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Auditoria de 2026-10-11 comparou o código entregue na PR #220 (CAP-006, tags de vídeo) com os adendos de tags do Figma: Screens — Vídeos, seção 319:2437 (Tag Chip, Tag Field, V1.g–k, V2.g–h, V8.a–c) e Screens — Autoria, seção 324:40939 (A6.f–g). O comportamento bate; as divergências são visuais e de texto no componente compartilhado VideoTagField, na etiqueta, nos filtros (Vídeos e seletor da aula) e no diálogo Editar tags. Decisões do usuário: uma task e uma PR; o texto de ajuda segue o Figma ('Enter ou vírgula confirma a tag.') e os limites aparecem só no erro. Não alterar a troca de tema claro/escuro.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 Etiqueta segue o Tag Chip do DS: pílula, fundo accent-subtle, texto 12/16 médio accent-subtle-foreground, folga 3/6/3/10 com ✕ de 12 px; sem ✕ nas linhas da tabela e nos cartões do seletor
- [x] #2 Campo de tags na ordem rótulo, campo, etiquetas, sugestões e ajuda; placeholder 'Digite e tecle Enter'; ajuda 'Enter ou vírgula confirma a tag.'
- [x] #3 Sugestões com contagem ('N sugestões'), painel com cantos de 10 e sombra MD, item ativo com fundo accent, ponto de 6 px e texto accent-foreground
- [x] #4 Erro do campo com ícone circle-alert de 14 px, texto 12/16 danger e borda danger no campo (V2.h, V8.b)
- [x] #5 Tag equivalente destaca a etiqueta existente com borda primária e mostra '<tag> já incluída. Nenhuma etiqueta nova.' (V2.h)
- [x] #6 Filtro por tags em Vídeos (V1.i, V1.k) e no seletor da aula (A6.f) com rótulo 'Tags', lupa e placeholder 'Filtrar por tag', sem dica de teclado visível, e 'Combinar tags' visível antes de E/OU
- [x] #7 Diálogo Editar tags com 520 px, ajuda 'Remover todas deixa o vídeo sem tags.' no campo e falha de serviço como alerta danger no topo com link 'Tentar de novo' (V8.a, V8.b)
- [x] #8 Ação de editar tags na linha usa o ícone de etiqueta do Figma
- [x] #9 Lint, build e testes do admin-spa passam e as telas foram conferidas em dev
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. VideoTagField: ordem rótulo/campo/etiquetas/sugestões/ajuda; placeholder; ajuda configurável (padrão 'Enter ou vírgula confirma a tag.'); contagem e item ativo nas sugestões; erro com CircleAlert; tag equivalente com borda primária e frase visível; variante filtro (rótulo 'Tags', lupa, 'Filtrar por tag', etiquetas antes do campo, sem dica).
2. Etiqueta: .video-tag-chip pílula accent-subtle; .is-removable com ✕ de 12 px; sem ✕ na tabela e no seletor.
3. Linha da tabela: etiquetas dentro da coluna do título (gap 6), linha alinhada ao topo; mobile mantém etiquetas abaixo do estado (display: contents); ícone Tag.
4. Filtros: 'Combinar tags' visível antes de E/OU em Vídeos e no seletor; disposição em linha (Vídeos) e empilhada (seletor).
5. Editar tags: 520 px, ajuda 'Remover todas deixa o vídeo sem tags.' no campo, falha de serviço como alerta danger no topo com link 'Tentar de novo'.
6. Ajustar testes; lint, build e testes do admin-spa; conferir em dev após o deploy.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
PR aberta: fix/tags-video-figma. Lint, build e 352 testes do admin-spa passam. Nome acessível do filtro passou a 'Filtrar por Tags' (rótulo visível 'Tags' + prefixo oculto). Fora do escopo: no Figma a primeira sugestão aparece destacada enquanto se digita; o código mantém o destaque só após as setas, porque destacar por padrão mudaria o que o Enter confirma.

Conferência em dev (2026-10-13) achou regressão desta entrega: título e tags centralizados na coluna Título (a célula virou flex em coluna e herdou align-items:center de app.css). Corrigido na TASK-29 (align-items:flex-start em videos.css).

Conferência em dev (2026-10-13), sem gravar nada: chip pill accent-subtle 12 px sem X na linha; filtro com rótulo Tags muted, lupa, placeholder 'Filtrar por tag', campo de 240 px e nome acessível 'Filtrar por Tags'; filtro por python aplicado (3 vídeos); Editar tags com 520 px, ordem rótulo/campo/chips/sugestões/ajuda, ajuda 'Remover todas deixa o vídeo sem tags.', placeholder 'Digite e tecle Enter', fechado sem salvar; ícones lucide-pencil e lucide-tag. Achados corrigidos na TASK-29 (PR #228): título centralizado e sugestões do filtro empurrando a tabela.

Conferência final em dev em 2026-10-13, depois da #228, sem gravar nada:
- Editar tags de '0103 - Forca pt3': 'Python' duplicada destacou o chip com borda rgb(124,58,237) e mostrou 'python já incluída. Nenhuma etiqueta nova.' (role=status). Uma tag de 57 caracteres mostrou 'Cada tag precisa ter de 1 a 30 caracteres.' com circle-alert de 14 px, texto de 12 px em danger e campo com borda danger e aria-invalid. Cancelado sem salvar; o vídeo continua só com python.
- Filtro com python e react: 'Combinar tags' visível antes de E/OU.
- Seletor da aula (A6.f) em 'React - Fundamentos': rótulo Tags muted, lupa, 'Filtrar por tag', chips em pílula sem ✕ nos cartões. Fechado com Cancelar, sem vincular.
- Regressões corrigidas na TASK-29 e conferidas: título alinhado à esquerda e sugestões em popover.
<!-- SECTION:NOTES:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
Alinhadas ao Figma as tags de vídeo na tela Vídeos, no diálogo Editar tags e no seletor da aula: Tag Chip em pílula, campo na ordem do DS com contagem e item ativo nas sugestões, erro com ícone, aviso de tag equivalente, filtro com rótulo Tags, lupa e Combinar tags, diálogo de 520 px com alerta no topo e ícone de etiqueta. Verificado com lint, build e testes do admin-spa (PR #222) e conferência em dev. A conferência achou duas regressões (título centralizado e sugestões do filtro empurrando a tabela), corrigidas na TASK-29 (PR #228). Fora do escopo: o Figma destaca a primeira sugestão enquanto se digita; o código só destaca depois das setas.
<!-- SECTION:FINAL_SUMMARY:END -->
