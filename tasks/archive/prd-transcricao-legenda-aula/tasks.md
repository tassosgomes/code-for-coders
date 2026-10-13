---
tsg_artifact: tasks
product: code-4-coders
capability: CAP-032
version: 1.0
status: approved
updated: 2026-10-12
sources: tasks/prd-transcricao-legenda-aula/prd.md@1.1, tasks/prd-transcricao-legenda-aula/techspec.md@1.0, tasks/prd-transcricao-legenda-aula/contracts.md@1.0
---

# Plano de Implementação — Transcrição e legenda de aula (CAP-032)

> **TechSpec de origem:** [techspec.md](techspec.md) 1.0, aprovada em 2026-10-12
> **Escopo:** Full-stack — `media` (API e worker), `bff-admin`, `bff-student`, `admin-spa`, `student-spa`, configuração de implantação
> **ADRs pertinentes:** nova [0020](../../docs/adr/0020-transcricao-de-aula-derivada-da-versao-de-reproducao.md) (Accepted); herdadas [0003](../../docs/adr/0003-verificacao-de-sessao-do-aluno.md), [0005](../../docs/adr/0005-sessao-e-servico-do-backoffice.md), [0006](../../docs/adr/0006-preparacao-de-video-e-custodia-de-chave.md), [0008](../../docs/adr/0008-observabilidade-kibana-como-codigo.md), [0013](../../docs/adr/0013-jwt-de-aluno-validado-por-servicos-de-dominio.md), [0014](../../docs/adr/0014-entrega-de-segmentos-por-credencial-opaca-e-porta-de-distribuicao.md), [0019](../../docs/adr/0019-runtime-e-deploy-atual.md)
> **Contratos:** [contracts.md](contracts.md) 1.0 — [api-contract.yaml](api-contract.yaml), [internal-api-contract-media.yaml](internal-api-contract-media.yaml), [api-contract-student.yaml](api-contract-student.yaml), [webhook-contract-media.yaml](webhook-contract-media.yaml)
> **Design:** `docs/design/wireframes-transcricao.md` (a criar em 1.0; ASCII e depois Figma)
> **Status do plano:** Aprovado pelo responsável em 2026-10-12 (divisão de V-01 em 3.0/4.0/5.0 aceita; uso da busca e da legenda segue sem instrumento até o piloto; forma do smoke da 13.0 — teste explícito ou script — fica com quem implementar)

## Visão Geral

Ao final, todo vídeo novo é transcrito sem ação de ninguém; o professor vê na tela Vídeos o estado da
transcrição, transcreve vídeos antigos, refaz e lê o texto (somente leitura); e o aluno com direito à
aula liga a legenda no player, lê a transcrição completa na página da aula, pesquisa uma palavra e
clica num trecho para levar o vídeo ao ponto. O trabalho e o estado vivem no `media`; os BFFs repassam;
os SPAs exibem. O texto de aula nunca vai a log, métrica, span, URL nem cache persistente.

**Design antes do código.** As duas primeiras tasks são o wireframe ASCII e o Figma aprovados da tela
Vídeos e da página da aula. Toda task com código de tela (5.0, 8.0, 9.0, 10.0, 11.0) fica bloqueada por
2.0.

**Dependência de calendário.** A implementação aguarda `CAP-006` sair de `tasks` (a fatia de tags de
vídeo altera os mesmos `Video`, tela Vídeos e contratos 1.2.0; `flow-state.json` NA26). As versões de
entrada dos contratos assumem as tags já consolidadas (Q-04).

**Dependência operacional (EN-02, fora deste repositório).** O smoke real (13.0) depende de
`TRUSTED_PRIVATE_SOURCES` e `TRUSTED_PRIVATE_WEBHOOKS` no deploy do serviço de transcrição, de uma conta
por ambiente e dos segredos por ambiente (chave da API e segredo do webhook; Q-C e Q-D). Hoje as duas
listas não estão configuradas. Código e testes com serviço simulado não dependem disso.

## Fases

### Fase 0 — Design aprovado

1.0 ASCII e 2.0 Figma. **Checkpoint:** o cabeçalho de `docs/design/wireframes-transcricao.md` traz
`> **Status:** aprovado (ASCII e Figma) em <data>`.

### Fase 1 — Do pedido ao texto pronto, para o professor

3.0 pedido e estado, 4.0 worker, 5.0 tela Vídeos, 6.0 disparo automático, 7.0 aviso do serviço.
**Checkpoint (com 13.0 possível):** com `scripts/remote-infra.sh migrate` e `scripts/apps.sh start --remote`
e `Transcription__Enabled=true`, em `http://localhost:8081/admin/videos` um vídeo pronto vai de "sem
transcrição" a "transcrevendo" e a "pronta" sozinho, e o vídeo recém-preparado já nasce "transcrevendo".

### Fase 2 — Confiabilidade e leitura do professor

8.0 falhas, motivos e Refazer; 9.0 leitura somente leitura. **Checkpoint:** um vídeo acima de 2 h mostra o
motivo sem Refazer; Refazer mantém a vigente legível; o professor abre o texto.

### Fase 3 — O aluno

10.0 transcrição, busca e clique; 11.0 legenda no player. **Checkpoint:** na aula, o aluno pesquisa
"introducao", acha "Introdução", clica no trecho e o vídeo vai ao ponto; liga a legenda e ela segue o
vídeo, inclusive em tela cheia, e a preferência persiste no dispositivo.

### Fase 4 — Operação, smoke e contratos

12.0 sinais no Kibana, 13.0 smoke real, 14.0 consolidação dos contratos. **Checkpoint:** painel de mídia com
os sinais da transcrição; um vídeo real transcrito ponta a ponta; `contracts/` consolidado.

## Mapa de Entrega

| Fatia | Task | Comportamento observável | Gate | Bloqueado por |
|---|---|---|---|---|
| EN-01 (ASCII) | 1.0 | Wireframe ASCII da transcrição aprovado | `rg` do status no cabeçalho de `docs/design/wireframes-transcricao.md` | Nenhum |
| EN-01 (Figma) | 2.0 | Figma aprovado e registrado | `rg` de `aprovado (ASCII e Figma)` no mesmo cabeçalho | 1.0 |
| V-01 (pedido) | 3.0 | O professor pede a transcrição e o estado aparece no vídeo (HTTP) | Media `TranscriptionEligibilityTests` + `VideoTranscriptionRequestTests` + BFF E2E `VideoTranscriptionTests` | Nenhum |
| V-01 (pipeline) | 4.0 | A tentativa da fila vira texto pronto, a partir do HLS cifrado | Media `TranscriptionResultValidationTests` + `TranscriptionServiceClientTests` + `TranscriptionPipelineTests` + `TranscriptionWorkerCompositionTests` | 3.0 |
| V-01 (tela) | 5.0 | A tela Vídeos mostra o estado e o professor transcreve | SPA `video-transcription-status` | 2.0, 4.0 |
| V-02 | 6.0 | Todo vídeo novo é transcrito sozinho | Media `AutomaticTranscriptionTests` | 4.0 |
| V-03 | 7.0 | O aviso do serviço acelera o resultado | Media `TranscriptionWebhookSignatureTests` + `TranscriptionEventReceiverTests` | 4.0 |
| V-04 | 8.0 | Falhas com motivo; Refazer sem perder a vigente | Media `TranscriptionFailurePolicyTests` + `TranscriptionRestartTests` + BFF E2E `VideoTranscriptionRestartTests` + SPA `video-transcription-restart` | 2.0, 5.0 |
| V-05 | 9.0 | O professor lê a transcrição, somente leitura | Media `VideoTranscriptionReadTests` + BFF E2E `VideoTranscriptionReadTests` + SPA `video-transcription-reader` | 2.0, 5.0 |
| V-06 | 10.0 | O aluno lê, pesquisa e clica na transcrição | Media `LessonTranscriptionTests` + BFF E2E `LessonTranscriptionTests` + SPA `lesson-transcription` | 2.0, 4.0 |
| V-07 | 11.0 | O aluno liga a legenda no player | Media `WebVttBuilderTests` + `PlaybackCaptionsTests` + BFF E2E `PlaybackCaptionsTests` + SPA `playback-captions` | 2.0, 10.0 |
| V-08 | 12.0 | A operação vê os sinais no painel de mídia | Media `MediaTranscriptionMetricsTests` + `import_observabilidade.py --verify-only` | 4.0 |
| V-01 e V-03 (smoke) | 13.0 | Vídeo real transcrito ponta a ponta contra o serviço real | Media `TranscriptionRealServiceSmokeTests` (explícito, com variáveis do smoke) | 4.0, 7.0 |
| Encerramento | 14.0 | Contratos consolidados em `contracts/` | Spectral nos quatro arquivos promovidos | 3.0 a 13.0 |

V-01 foi dividida em três tasks (3.0, 4.0, 5.0) porque o pipeline do worker é a maior peça técnica da
capacidade e porque o código de tela precisa esperar o design aprovado, enquanto `media` e `bff-admin`
podem começar já (verificáveis por HTTP). A divisão é por **comportamento observável** (pedir e ver o
estado; a fila virar texto; ver e agir na tela), não por camada técnica. 3.0 e 4.0 não têm tela. As demais
fatias que têm tela (V-04, V-05, V-06, V-07) atravessam `media`, BFF e SPA numa task só.

6.0 e 7.0 dependem só de 4.0 e não compartilham caso de uso; rodam em sequência porque o orquestrador é
sequencial e ambas tocam o `media`.

### Habilitadores

| Enabler | Task | Por que não cabe numa fatia | Desbloqueia |
|---|---|---|---|
| EN-01 (ASCII) | 1.0 | Preferência do projeto: wireframe aprovado antes de qualquer código de tela. É decisão do responsável, não derivável. Cobre as duas telas (Vídeos e aula) de uma vez, incluindo a decisão de Q-G (legenda nativa ou camada própria, marca d'água em tela cheia) | 5.0, 8.0, 9.0, 10.0, 11.0 (via 2.0) |
| EN-01 (Figma) | 2.0 | A aprovação visual é uma segunda decisão do responsável e parte do ASCII aprovado | 5.0, 8.0, 9.0, 10.0, 11.0 |
| EN-02 (configuração do serviço) | — | Pertence a outro repositório e ao operador (listas de destinos privados, conta e segredos por ambiente); não é task do plano, é pré-requisito de 13.0 | Smoke real de 13.0 |
| Consolidação | 14.0 | Regra 2 de `contracts/README.md`: versão, origem e tabela só no fim do PRD | Encerramento do PRD |

## Tasks

- [x] 1.0 Wireframe ASCII da transcrição (tela Vídeos e página da aula) aprovado
- [x] 2.0 Figma da transcrição (tela Vídeos e página da aula) aprovado
- [x] 3.0 O professor pede a transcrição de um vídeo pronto e o estado aparece no vídeo
- [x] 4.0 O worker leva a transcrição da fila ao texto pronto, a partir do vídeo cifrado
- [x] 5.0 A tela Vídeos mostra o estado da transcrição e o professor transcreve, com atualização automática
- [x] 6.0 Todo vídeo novo é transcrito sem ação de ninguém
- [x] 7.0 O aviso do serviço acelera o resultado sem ser necessário
- [x] 8.0 Falhas aparecem com motivo e o professor refaz sem perder a transcrição vigente
- [x] 9.0 O professor lê a transcrição pronta na tela Vídeos, somente leitura
- [x] 10.0 O aluno lê, pesquisa e clica na transcrição da aula
- [x] 11.0 O aluno liga a legenda no player
- [x] 12.0 A operação enxerga os sinais da transcrição no painel de mídia
- [x] 13.0 Uma aula real é transcrita ponta a ponta contra o serviço de transcrição
- [x] 14.0 Contratos de transcrição consolidados em contracts/

## Verificação herdada

| Componente | Fonte | Checks e limites | Estado da base | Resolução planejada |
|---|---|---|---|---|
| `src/media` | `.github/workflows/media.yml` → `tassosgomes/template-pipeline/.github/workflows/ci-dotnet.yml@v1` | `dotnet format --verify-no-changes`; `dotnet test` (Microsoft.Testing.Platform) com cobertura ≥ 70%; `dotnet publish`; imagem | Não medido nesta sessão; o workflow reutilizável é de outro repositório e não foi lido (passos herdados de planos anteriores, ex.: `prd-tags-video`); paridade com o CI não reverificada | Nenhuma falha herdada comprovada. Cobertura agregada medida na validação full |
| `src/bff-admin`, `src/bff-student` | `.github/workflows/bff-admin.yml`, `bff-student.yml` → `ci-dotnet.yml@v1` | Idem, cobertura ≥ 70% | Não medido | Nenhuma |
| `src/admin-spa`, `src/student-spa` | `.github/workflows/admin-spa.yml`, `student-spa.yml` → `ci-react-ts.yml@v1` (`coverage-threshold: 70`) | lint; typecheck; `vitest run --coverage` ≥ 70%; `vite build`; imagem | Não medido | Nenhuma |
| Migrations de `media` | AGENTS.md; `dotnet-ef` em `.config/dotnet-tools.json` | Migration gerada pela ferramenta; `has-pending-model-changes` sem pendência | Não medido | 3.0 gera a migration; 7.0 só se o modelo do aviso exigir |
| Contratos | `contracts/README.md` regra 2 | Promoção aditiva por task; Spectral sem erros com `contracts/.spectral-exception.yaml` | Recortes validados em 2026-10-12 (`contracts.md`) | Cada task de 3.0 a 11.0 promove o que implementa; 14.0 consolida |
| Observabilidade | ADR-0008; `scripts/kibana/import_observabilidade.py` | `--verify-only` (verificação estrutural); sem CI dedicado | Não medido | 12.0 |
| Compose | AGENTS.md | Config nova replicada em `docker-compose.remote.yml` | n/a | 3.0, 4.0 e 7.0 replicam o que criam |

Falha preexistente conhecida e **fora do escopo**: vídeos com fonte abaixo de 480p podem não reproduzir
(`PlaylistRewriter`, TASK-27 no Backlog). O extrator desta capacidade não herda essa lista.

## Cobertura

| Requisito | Task(s) |
|---|---|
| RF-01 | 6.0, 13.0 |
| RF-02 | 1.0, 2.0, 3.0, 4.0, 5.0, 8.0 |
| RF-03 | 1.0, 2.0, 11.0 |
| RF-04 | 1.0, 2.0, 10.0 |
| RF-05 | 1.0, 2.0, 10.0 |
| RF-06 | 10.0, 11.0 |
| RF-07 | 4.0, 7.0, 8.0, 12.0, 13.0 |
| RF-08 | 1.0, 2.0, 9.0 |
| RN-M18 | 3.0, 4.0, 8.0 |
| RN-M19 | 3.0, 6.0 |
| RN-M20 | 3.0, 6.0 |
| RN-M21 | 10.0, 11.0 |
| RN-M22 | 10.0 |
| RN-M23 | 3.0, 4.0, 7.0, 13.0 |
| RN-M24 | 4.0, 8.0 |
| RN-M25 | 6.0 |
| RN-M26 | 4.0, 12.0 |
| RN-M27 | 9.0 |
| RN-TL01 | 1.0, 10.0 |
| RN-TL02 | 1.0, 10.0 |
| RN-TL03 | 10.0 |
| RN-TL04 | 1.0, 11.0 |
| RN-TL05 | 1.0, 10.0, 11.0 |
| RN-TL06 | 1.0, 3.0, 5.0, 8.0 |
| RN-TL07 | 8.0 |
| RN-TL08 | 1.0, 9.0 |
| US-01 (aluno liga a legenda) | 11.0 |
| US-02 (aluno vê o texto completo) | 10.0 |
| US-03 (aluno pesquisa uma palavra) | 10.0 |
| US-04 (aluno clica no trecho) | 10.0 |
| US-05 (professor não faz nada para vídeo novo) | 6.0 |
| US-06 (professor vê estado e motivo) | 5.0, 8.0 |
| US-07 (professor lê a transcrição) | 9.0 |
| US-08 (professor transcreve vídeo antigo e refaz) | 3.0, 5.0, 8.0 |
| US-09 (dono da escola: texto só a quem tem direito) | 10.0, 11.0 |
| Métricas do PRD (cobertura, tempo até pronta, falha) | 12.0 (instrumentos e painel). **Uso da busca e uso da legenda** não têm instrumento na TechSpec: lacuna registrada para decisão do responsável, sem task artificial |

> Verificado por `python3 .claude/skills/tsg-flow-task-creator/scripts/validate_plan.py tasks/prd-transcricao-legenda-aula/` antes do handoff.
