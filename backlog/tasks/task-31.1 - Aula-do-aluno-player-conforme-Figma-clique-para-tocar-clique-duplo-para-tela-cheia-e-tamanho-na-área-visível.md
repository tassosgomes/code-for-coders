---
id: TASK-31.1
title: >-
  Aula do aluno: player conforme Figma, clique para tocar, clique duplo para
  tela cheia e tamanho na área visível
status: In Progress
assignee:
  - '@claude'
created_date: '2026-10-13 19:54'
updated_date: '2026-10-13 20:05'
labels:
  - frontend
dependencies: []
references:
  - >-
    https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=255-202
  - >-
    https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=256-535
  - >-
    https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=345-608
  - student-spa
modified_files:
  - >-
    src/student-spa/src/features/student-lessons/components/protected-video-player.tsx
  - src/student-spa/src/features/student-lessons/components/player-captions.tsx
  - src/student-spa/src/assets/app.css
  - src/student-spa/src/app/playback-open.test.tsx
parent_task_id: TASK-31
priority: medium
type: bug
ordinal: 39000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
PlayerControls (255:202), LessonPlayer (256:535), WatermarkTag (255:42), PersonalUseNotice (255:236) e CaptionToggle (TA4.a 345:608). Hoje os controles são botões de texto e sliders nativos sobre fundo preto.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 Controles em botões de ícone de 40 px sobre fundo code: tocar/pausar (play/pause 20), linha do tempo própria (trilha 4 px, assistido em primary, marcador 12), tempo 'mm:ss / mm:ss' em mono 12/18, volume, velocidade '1x ▾' em mono 12 e tela cheia (maximize); em tela cheia, minimize + 'Sair'
- [ ] #2 Desktop: barra de 56 px sobre a base do vídeo, padding 8/16, gap 12; celular: barra abaixo do vídeo em duas linhas (padding 8/12)
- [ ] #3 Botão de legenda como CaptionToggle: ícone CC 20, ligado com fundo primary e sublinhado
- [ ] #4 Contêiner do player com raio 16 e fundo code; marca d'água com padding 4/8, raio 10, 12/16 peso 500, a 24 px das bordas (12 no celular) e fora da faixa dos controles
- [ ] #5 Aviso de uso pessoal em caixa muted, raio 10, padding 12/16, ícone info 16 e texto 14/20 muted
- [ ] #6 Clique na imagem toca ou pausa; clique duplo entra ou sai da tela cheia sem trocar o estado de reprodução
- [ ] #7 O player inteiro (imagem e controles) cabe na altura visível da janela no desktop
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. Ler PlayerControls (255:202), LessonPlayer, WatermarkTag, PersonalUseNotice e CaptionToggle (TA4.a).
2. Trocar botões de texto e sliders nativos por botões de ícone, linha do tempo estilizada e tempo; barra sobre a imagem no desktop e em duas linhas no celular.
3. Clique na imagem toca/pausa (com janela de 250 ms para o clique duplo), clique duplo alterna a tela cheia; altura máxima da imagem = janela − 220 px no desktop.
4. Testes, prévia local com o DOM renderizado e o CSS do build; conferência em dev depois do deploy.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Prévia local (DOM do teste + CSS do build, janela 2560×911): player de 188 a 879 px, cabe na altura visível; barra de 56 px sobre a base; botões de 40 px; marca a 24 px das bordas. Celular 390 (iframe): barra abaixo da imagem com 100 px em duas linhas de 40, marca a 12 px, sem rolagem horizontal.
Decisões: os nomes acessíveis atuais ficam (Reproduzir/Pausar, Posição do vídeo, Velocidade de reprodução); o volume em repouso é só o ícone (silenciar/ativar o som) e o controle deslizante abre com o ponteiro ou o foco; no desktop a imagem é limitada pela altura e fica com faixas pretas nas laterais quando a coluna é larga.
Fora do escopo (já existia): o menu de velocidade abre em portal no body, então não aparece em tela cheia.
Teste novo: clique toca, clique duplo pede a tela cheia sem tocar nem pausar.
<!-- SECTION:NOTES:END -->
