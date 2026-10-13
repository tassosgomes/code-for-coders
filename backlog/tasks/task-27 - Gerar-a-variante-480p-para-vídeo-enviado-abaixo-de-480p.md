---
id: TASK-27
title: Gerar a variante 480p para vídeo enviado abaixo de 480p
status: To Do
assignee: []
created_date: '2026-10-12 20:38'
labels:
  - media
  - bug
dependencies: []
references:
  - src/media/src/CodeForCoders.Media.Infra.Messaging/VideoQualityLadder.cs
  - src/media/src/CodeForCoders.Media.Application/Common/PlaylistRewriter.cs
  - contracts/media/openapi-internal.yaml
priority: high
type: bug
ordinal: 34000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Vídeo enviado com altura menor que 480p fica pronto, mas não reproduz. A preparação (VideoQualityLadder.Select) nomeia a única variante pela altura real (ex.: 360p), enquanto a reprodução (PlaylistRewriter) e os contratos vigentes (Quality em contracts/media/openapi-internal.yaml e contracts/bff-student/openapi-aula.yaml) só admitem 480p, 720p e 1080p (DP-01 de CAP-006). Ao montar a sessão, a reescrita da playlist lança "The playlist contains an unsupported resource".

Decisão do usuário em 2026-10-12: quando o original for menor que 480p, gerar a variante em 480p (upscale), sem mudar contrato nem reprodução. Alternativas descartadas: aceitar qualquer altura (muda dois contratos e o player) e recusar o envio abaixo de 480p (decisão de produto nova). Sem dados legados até o MVP: vídeos já preparados assim em dev são reenviados, sem migração.

Encontrado durante a TechSpec de CAP-032 (transcrição), que lê a variante de menor banda; a extração do áudio não depende desta correção.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 Original com altura menor que 480p gera exatamente uma variante chamada 480p, com altura 480 e banda de 480p
- [ ] #2 Um vídeo preparado a partir de original abaixo de 480p reproduz pela sessão de reprodução (master e variante reescritas sem erro)
- [ ] #3 Originais de 480p ou maiores continuam com a mesma escada de qualidades de hoje
- [ ] #4 Testes cobrem o caso abaixo de 480p na escada de qualidades e na reescrita da playlist
<!-- AC:END -->
