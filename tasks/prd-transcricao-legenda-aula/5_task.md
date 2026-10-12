---
status: pending
task_kind: vertical
blocked_by: ["2.0", "4.0"]
gate: 'npm --prefix src/admin-spa run test -- video-transcription-status'
gate_expect: "Pelo menos 8 testes do admin-spa com o fragmento video-transcription-status passam"
---

# 5.0 A tela Vídeos mostra o estado da transcrição e o professor transcreve, com atualização automática

**Fatia:** V-01 (tela) · **Cobre:** RF-02 (estado na tela, Transcrever, atualização automática), RN-TL06 (estado) · **Spec:** `techspec.md#bloco-frontend` · **ADR:** 0005

## Comportamento

Na tela Vídeos (`/admin/videos`), cada linha de vídeo pronto mostra o estado da transcrição ao lado
do estado da preparação e das tags, conforme o desenho aprovado em 2.0: sem transcrição,
transcrevendo, pronta. *Transcrever* aparece quando `canStart` é verdadeiro e o estado é
`not-requested`; ao acionar, a linha passa a "transcrevendo" **sem** recarregar a página. O estado
vem do servidor (`Video.transcription`); nenhuma cópia local.

A reconsulta automática da lista (`useVideos`, hoje só para `received`/`preparing`) passa a incluir
`transcription.status === 'in-progress'`, até a linha chegar a `pronta`; o filtro, a busca por título
e as tags escolhidas se mantêm durante a atualização. Vídeo não pronto não oferece a ação. Se o
pedido falha (409, 403, rede), a linha mostra a mensagem pelo `code`, sem perder o estado anterior.
O schema zod estrito de vídeo aceita `transcription`. Sem `midia.enviar` a área continua
bloqueada como hoje. Nenhum log ou telemetria carrega título, texto ou dado pessoal.

Esta task mostra `pronta` mas ainda **sem** abrir a leitura (9.0) e **sem** motivos de falha nem
Refazer (8.0).

## Fora do escopo desta task

- Motivos de falha, Refazer e a vigente durante a refeita: 8.0.
- Leitura da transcrição pelo professor: 9.0.

## Decisões fechadas

- O estado e a ação vivem no servidor; a tela só reflete (TechSpec, Bloco Frontend).
- Design aprovado em 2.0 é a fonte do desenho; divergência volta ao responsável.
- Texto de professor em português, sem jargão do pipeline.

## Modificar / Referenciar

- **modificar:** `src/admin-spa/src/features/videos/api/get-videos.ts` (schema e reconsulta); `src/admin-spa/src/features/videos/components/videos-area-screen.tsx`; `src/admin-spa/src/features/videos/components/video-status-badge.tsx`; `src/admin-spa/src/testing/handlers.ts` e `authoring-video-handlers.ts` (vídeos com `transcription`)
- **ref:** `docs/design/wireframes-transcricao.md` (aprovado em 2.0); `docs/design/wireframes-videos.md`; `src/admin-spa/src/features/videos/api/replace-video-tags.ts` (padrão de mutação com `Idempotency-Key`); `api-contract.yaml` (`requestVideoTranscription`); skill `react`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/admin-spa` | `npm --prefix src/admin-spa run lint` | exit 0 | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run typecheck` | exit 0 | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run test -- video authoring-video-picker` | exit 0 (telas e fluxos existentes intactos) | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run build` | exit 0 | `ci-react-ts.yml` (build) |

## Pronto quando

- [ ] Gate focalizado passa (exit 0).
- [ ] *Transcrever* num vídeo pronto sem transcrição muda a linha para "transcrevendo" sem recarregar; ao chegar a `ready` a linha atualiza sozinha.
- [ ] Vídeo não pronto, ou com `canStart: false`, não oferece a ação.
- [ ] Filtros e tags escolhidas sobrevivem à atualização automática.
- [ ] Em dev (`scripts/remote-infra.sh migrate` e `scripts/apps.sh start --remote`), `http://localhost:8081/admin/videos` mostra o estado; a conclusão real depende do smoke de 13.0.
