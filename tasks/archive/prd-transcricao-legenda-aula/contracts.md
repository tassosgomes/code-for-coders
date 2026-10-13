---
tsg_artifact: contract
product: code-4-coders
capability: CAP-032
version: 1.0
status: approved
updated: 2026-10-12
sources: tasks/prd-transcricao-legenda-aula/prd.md@1.1, domains/entrega-de-midia-e-protecao/domain.md@1.1
---

# Contratos de integração — transcrição e legenda de aula

> - PRD: [prd.md](prd.md), v1.1 (errata de RF-06 em 2026-10-12), aprovado
> - Data da revisão: 2026-10-12
> - Estado do conjunto: **Aprovado para implementação (1.0)** em 2026-10-12 pelo responsável, após
>   responder Q-01 a Q-04 (registradas abaixo). Quatro documentos OpenAPI validados sem erros.

Este conjunto registra o acordo para a implementação de `CAP-032`. Os contratos vigentes das
fronteiras estão em `contracts/` e são o ponto de partida: este conjunto só os evolui, de forma
aditiva. Promoção aditiva por task e consolidação no fim do PRD, conforme `contracts/README.md`
(não acontece agora). Não afirma qual contrato está em produção; catalogação e atualização do
acervo são da plataforma.

## Seleção e escopo

| Integração identificada | Modalidade | Justificativa |
|---|---|---|
| `admin-spa` → `bff-admin` (tela Vídeos: estado, Transcrever/Refazer, leitura) | OpenAPI | RF-02, RF-08 |
| `bff-admin` → `media` (as mesmas operações, 1:1) | OpenAPI | `media` é dono da Transcrição (RN-M18) |
| `student-spa` → `bff-student` (transcrição da aula e legenda do player) | OpenAPI | RF-03 a RF-06 |
| `bff-student` → `media` (as mesmas operações, 1:1) | OpenAPI | RN-M21: mesma verificação da reprodução |
| Serviço de transcrição → `media` (aviso de término por webhook) | OpenAPI (webhook) | RF-07; endpoint **nosso**, dedicado, no molde de `contracts/billing/openapi-webhook.yaml` |
| `media` → serviço de transcrição (pedido e consulta) | **Dependência consumida**, não contrato nosso | O contrato é do repositório dele; ver "Dependência consumida" abaixo |
| `learning` (aula, versão vigente) | Não tocado | A página da aula já informa `lessonId`; `media` resolve o vídeo pela Referência de Uso (RN-M09) e `getStudentLesson` não muda. Nenhuma operação de `learning` muda |
| Fatos `midia.*` | AsyncAPI **não aplicável** | O domínio de Mídia (§7) não produz fato novo; nenhum consumidor real precisa de "transcrição pronta". O disparo automático (RF-01) é interno a `media` (o fim da preparação e o início da transcrição estão no mesmo serviço): não é mensagem entre aplicações. Se Conteúdo ou Aprendizagem vierem a precisar, nasce `midia.transcricao-pronta` em PRD próprio |
| Transcrição como produto de dados | ODCS **não aplicável** | Banco interno de `media`; nenhum consumidor de dados |

| Documento | Padrão e versão | Versão do contrato | Fronteira | Estado |
|---|---|---|---|---|
| [api-contract.yaml](api-contract.yaml) e [.md](api-contract.md) | OpenAPI 3.1.0 | `bff-admin` vídeos 1.2.0 → 1.3.0 | Recorte: `listVideos` e `getVideo` (corpo), `requestVideoTranscription`, `getVideoTranscription` | **Aprovado para implementação** em 2026-10-12; lint sem erros |
| [internal-api-contract-media.yaml](internal-api-contract-media.yaml) e [.md](internal-api-contract-media.md) | OpenAPI 3.1.0 | `media` interno 1.2.0 → 1.3.0 | Recorte: as quatro acima com sufixo `Internal`, mais `getLessonTranscriptionInternal` e `getPlaybackCaptionsInternal` | **Aprovado para implementação** em 2026-10-12; lint sem erros, 1 aviso registrado |
| [api-contract-student.yaml](api-contract-student.yaml) e [.md](api-contract-student.md) | OpenAPI 3.1.0 | `bff-student` aula 1.1.0 → 1.2.0 | Recorte: `getLessonTranscription`, `getPlaybackCaptions` | **Aprovado para implementação** em 2026-10-12; lint sem erros, 1 aviso registrado |
| [webhook-contract-media.yaml](webhook-contract-media.yaml) e [.md](webhook-contract-media.md) | OpenAPI 3.1.0 | `media` webhook 1.0.0 (novo) | Completo: `receiveTranscriptionEvent` | **Aprovado para implementação** em 2026-10-12; lint sem erros |
| [spectral-ruleset.yaml](spectral-ruleset.yaml) | Spectral | n/a | Ruleset vigente do projeto + `text/vtt` | Apoio à validação, não é contrato |

## Participantes e interfaces

| Documento e identificador técnico | Provedor | Consumidores | Comportamento ou compromisso | PRD |
|---|---|---|---|---|
| `listVideos` / `getVideo` (+ `*Internal`) — campo `Video.transcription` | `bff-admin` → `media` | `admin-spa` | Estado da transcrição por vídeo: `not-requested`, `in-progress`, `ready`, `failed`; `readable`, `readyAt`, `failureReason`, `canStart`. A tela atualiza sozinha reconsultando a listagem. `updateVideoTitle` e `replaceVideoTags` devolvem o mesmo `Video` | RF-02; RN-TL06, RN-TL07 |
| `requestVideoTranscription` / `requestVideoTranscriptionInternal` | `bff-admin` → `media` | `admin-spa` | **Transcrever** e **Refazer** numa só operação, 202 com o vídeo `in-progress`; só registra a intenção; repetir não cria duas; 409 `VIDEO_NOT_READY`, 409 `TRANSCRIPTION_NOT_RETRYABLE`; `midia.enviar`; outra escola = 404 | RF-01, RF-02; RN-M18, RN-M19, RN-M20, RN-M24 |
| `getVideoTranscription` / `getVideoTranscriptionInternal` | `bff-admin` → `media` | `admin-spa` | Leitura somente leitura da vigente, sem sessão nem Matrícula; 409 `TRANSCRIPTION_NOT_READY` | RF-08; RN-M27, RN-TL08 |
| `getLessonTranscription` / `getLessonTranscriptionInternal` | `bff-student` → `media` | `student-spa` | Trechos da aula (RN-M10 passos 1 e 2, falha fechada, sem sessão); `status: ready` com `segments`, ou `unavailable` (200) sem distinguir andamento e falha | RF-04, RF-05, RF-06; RN-M21, RN-TL03, RN-TL05 |
| `getPlaybackCaptions` / `getPlaybackCaptionsInternal` | `bff-student` → `media` | `student-spa` (player) | Legenda WebVTT da sessão; para quando a sessão não renova; 404 `CAPTIONS_NOT_AVAILABLE` sem transcrição pronta | RF-03; RN-M21, RN-TL05 |
| `receiveTranscriptionEvent` | `media` | Serviço de transcrição (único chamador) | Standard Webhooks v1 verificado; idempotente por `webhook-id`; 204 também para evento duplicado ou desconhecido; nunca 410 | RF-07; RN-M18, RN-M23 |

**Encadeamento.** `requestVideoTranscription` registra a tentativa → `media` pede a transcrição ao
serviço (dependência consumida) → o serviço avisa em `receiveTranscriptionEvent` → `media` consulta,
copia o texto para guarda própria (RN-M23) e passa a tentativa a `ready` ou `failed` → o professor
vê o novo estado em `listVideos`/`getVideo`, lê em `getVideoTranscription`, e o aluno recebe em
`getLessonTranscription` e `getPlaybackCaptions`. O tempo é sempre em milissegundos
(`startMs`/`endMs`), como o serviço devolve: não há conversão de unidade na fronteira.

## Decisões

Decisões novas deste conjunto (numeração `T-`, para não colidir com as exceções `C-` de `contracts/`).
Cada uma sai do PRD e do domain doc; as que o texto deixava em aberto estão marcadas e entram nas
questões.

| ID | Decisão | Motivo | Origem |
|---|---|---|---|
| T-01 | A transcrição da aula é uma leitura **por aula** (`GET /lessons/{lessonId}/transcription`), sem Sessão de Reprodução. O aluno informa a aula, nunca o vídeo | A página mostra o texto antes do play e o clique sem sessão abre a sessão pelo caminho normal (RN-TL02); informar só a aula fecha por construção o caso "vídeo de outro curso com conteúdo de A" (RF-06). Replica os passos 1 e 2 de RN-M10 | RF-04, RF-06 (v1.1); RN-M21; Q-01 fechada: 403 |
| T-02 | A legenda é uma leitura **por sessão** (`GET /playback-sessions/{sessionId}/captions`), em WebVTT | "Legenda para junto com a reprodução" (RF-03) exige acoplar à sessão e à renovação. WebVTT permite ao player usar faixa de texto nativa (inclusive tela cheia) em vez de desenhar a legenda | RF-03; RN-M21; Q-02 fechada: confirmado |
| T-03 | O estado da transcrição vai **dentro de `Video`**, sem endpoint de estado separado | A tela Vídeos já reconsulta `listVideos` enquanto há trabalho (DP-04 do contrato de vídeos); um campo evita segunda fonte do estado | RF-02 |
| T-04 | **Transcrever** e **Refazer** são **uma** operação `POST /videos/{videoId}/transcription-requests`; `canStart` diz se seria aceita. O pedido não depende do serviço de transcrição estar no ar | Evita decidir no cliente; RN-M20 (um só em andamento) e RN-M19 (nunca atrasa nem impede a Mídia) | RF-01, RF-02 |
| T-05 | A leitura do professor é `GET /videos/{videoId}/transcription`, **sem busca nem clique** que leve a um vídeo; só devolve a **vigente** | RF-08 e DP-08: leitura não é reprodução | RF-08 |
| T-06 | O receptor do aviso é um endpoint dedicado de `media` em `/webhooks/v1/transcription-events`, no molde do webhook de `billing` | Baseline: webhook entra por endpoint dedicado, validado por assinatura e idempotente. É o serviço de transcrição que assina; o que valida é o contrato dele | RF-07 |
| T-07 | O motivo da falha é uma **categoria do produto** (`duration-limit`, `size-limit`, `service-unavailable`, `processing-failed`, `timed-out`); `duration-limit` e `size-limit` não são refazíveis (`canStart: false`) | Camada anticorrupção: nenhum código do serviço de transcrição aparece; o PRD pede o motivo em linguagem de professor e sem ação ilusória (RN-M24). Textos na `description` do enum, para a tela | RF-02; RN-M24; Q-03 fechada: confirmado |
| T-08 | `text/vtt` é aceito como resposta de `getPlaybackCaptions*`. Na promoção, a exceção C-08 de `contracts/.spectral-exception.yaml` precisa listar `text/vtt` | A legenda não é JSON; a mesma situação da playlist HLS | T-02 |
| T-09 | `revision` opaco na transcrição (do professor e do aluno) | Dá ao cliente como saber se o texto que tem ainda é o vigente depois de um Refazer (DP-09), sem expor nada por aluno (RN-M22) | RF-02, RN-TL07 |
| T-10 | A busca (RN-TL01, RF-05) **não** é operação: roda no cliente sobre os trechos já recebidos | O texto não precisa de índice de servidor para uma aula (DP-06); evita enviar a consulta do aluno (termo digitado) ao servidor | RF-05 |

Fica **fora do contrato**, para a TechSpec: extração do áudio da Versão de Reprodução, prefixo e URL
assinada do áudio, polling e reconciliação, limite de espera, política de nova tentativa, armazenamento
e retenção do texto, geração e escape do WebVTT, tamanho máximo da resposta, intervalo de reconsulta
da tela, sinais de observabilidade (RN-M26) e a leitura das variáveis de allowlist.

## Dependência consumida: serviço de transcrição (`media` → serviço)

Não é contrato nosso e **não** foi copiado para `contracts/`. A TechSpec consome estes pontos por
trás de uma camada anticorrupção em `media`.

| Item | Valor consultado |
|---|---|
| Fonte | `~/github-tassosgomes/whisper/tasks/prd-api-transcricao-assincrona/api-contract.yaml` (repositório `tassosgomes/whisper-api`), `info.version` **1.0.0** |
| Revisão consultada | `main` em `b3ac8b8a38a0ca19ad915b9147237c12f52f95d4` (2026-10-12, merge do PR #2). O arquivo é idêntico ao do commit `df1e50bb31bbaa78bdf3f8abb03a20d8f94276a5` (2026-09-28), a última mudança dele |
| Pendências externas do PRD | PR #2 (allowlist de destinos privados, ADR-003) e PR #3 (fixa PyAV 18): **mergeados e implantados em 2026-10-12** (informado pelo responsável). Teste ponta a ponta contra o serviço implantado (`https://whisper.lab.tasso.dev.br`): trabalho `completed`, resultado `schemaVersion` 1, `pt-BR`. Resta configurar `TRUSTED_PRIVATE_SOURCES` e `TRUSTED_PRIVATE_WEBHOOKS` no deploy |

| Operação dele | O que `media` usa | Acordo de interface |
|---|---|---|
| `POST /v1/transcriptions` (`createTranscription`) | Cria uma transcrição de um áudio | `X-API-Key` do serviço; `Idempotency-Key` estável por tentativa (janela de 120 s dele); corpo `{sourceUrl, clientReference}` com a URL de leitura assinada do **áudio** extraído da própria Versão de Reprodução (validade mínima de 60 min); 202. `clientReference` = identificador da **tentativa de transcrição** de `media`, opaco (nunca título, texto, e-mail ou pessoa). A URL não vai a log |
| `GET /v1/transcriptions/{jobId}` (`getTranscription`) | Conciliação e motivo da falha | Estados `downloading`, `queued`, `processing`, `completed`, `failed`; `failure.code` mapeado para os cinco motivos de T-07 (`MEDIA_DURATION_LIMIT_EXCEEDED` → `duration-limit`; `MEDIA_SIZE_LIMIT_EXCEEDED` → `size-limit`; `SOURCE_UNAVAILABLE` e indisponibilidade → `service-unavailable`; `UNSUPPORTED_MEDIA_FORMAT` e `TRANSCRIPTION_FAILED` → `processing-failed`; esgotar a espera local → `timed-out`) |
| `GET /v1/transcriptions/{jobId}/result` (`getTranscriptionResult`) | Copia o texto | `schemaVersion` 1, `language` `pt-BR`, `durationMs`, `segments[{startMs, endMs, text}]`. Só dentro de 24 h do estado terminal; 409 enquanto não concluiu; `media` copia antes disso (RN-M23). Versão do schema desconhecida: tratar como falha, não adivinhar |
| Webhook terminal (Standard Webhooks v1) | Gatilho para consultar | Recebido em `receiveTranscriptionEvent`. Cabeçalhos `webhook-id`, `webhook-timestamp`, `webhook-signature`; corpo `{eventId, jobId, status, clientReference?}` sem texto. Reenvio por até 72 h; 410 desativa o destino |

Limites dele que o contrato nosso respeita sem expor: 2 horas, 5 GiB, formatos `.mp4 .mkv .webm .mp3 .wav .m4a`,
pt-BR, resultado retido 24 h, `schemaVersion` 1.

## Origem e decisões herdadas

| Entrada consultada | Versão e revisão | Decisão herdada ou alterada | Motivo |
|---|---|---|---|
| `prd.md` | v1.1, aprovado 2026-10-12 (v1.0 mais a errata de RF-06) | RF-01 a RF-08, RN-TL01 a RN-TL08, DP-01 a DP-10. Superfícies confirmadas: tela Vídeos (estado, Transcrever/Refazer, leitura), página da aula (transcrição, busca local), player (legenda) | Fonte do conjunto |
| `domains/entrega-de-midia-e-protecao/domain.md` | v1.1 | RN-M18 a RN-M27; Transcrição única por Mídia; legenda sai da mesma Transcrição; nenhum evento novo; serviço de transcrição atrás de camada anticorrupção | O contrato registra o acordo de interface, não as regras |
| `contracts/media/openapi-internal.yaml` | 1.1.0 (commit `695ffb3`); já inclui as operações de tags de vídeo promovidas por task | Base de `media`: tokens, `Problem`, respostas `TokenInvalid`, `LessonNotAvailable`, `AccessDecisionUnavailable`, `SessionExpired`, `AccessDenied`, `VideoNotFound`; mesmo `Video` | Mudança aditiva |
| `contracts/bff-admin/openapi-midia.yaml` | 1.1.1 (idem) | Cookie `staff_session`, `X-CSRF-Token` e `Idempotency-Key` nas escritas, 502/504 `MEDIA_UNAVAILABLE` | Mudança aditiva |
| `contracts/bff-student/openapi-aula.yaml` | 1.1.0 | Cookie `student_session`, leituras sem CSRF, 502/504 | Mudança aditiva |
| `contracts/billing/openapi-webhook.yaml` | 1.0.0 | Molde do endpoint dedicado de webhook (assinatura, idempotência, registro antes de responder, 503 transitório) | Padrão já adotado |
| `contracts/media/asyncapi.yaml` | 1.3.0 | Sem mudança | Nenhum fato novo |
| `tasks/prd-tags-video/` (`api-contract.yaml`, `internal-api-contract.yaml`) | 1.2.0 cada, aprovados em 2026-10-11 | Base de versão (1.2.0 → 1.3.0), pois o recorte de tags ainda não foi consolidado em `contracts/` | Q-04 fechada: tags consolidam antes |
| Serviço de transcrição | `api-contract.yaml` 1.0.0, `b3ac8b8` | Dependência consumida (acima) | Camada anticorrupção |

## Evolução e compatibilidade

Todas as mudanças são **aditivas**; nenhuma operação vigente é removida nem tem semântica alterada.

| Contrato | Mudança | Consumidores afetados |
|---|---|---|
| `bff-admin` vídeos 1.2.0 → 1.3.0 | `Video.transcription` **obrigatório na resposta** (listVideos, getVideo, updateVideoTitle, replaceVideoTags, e `Video` nas demais respostas que o carregam); duas operações novas | `admin-spa`. Cliente tolerante a campo novo não quebra. **Testes que validam a resposta contra o schema vigente** (`additionalProperties: false` no `Video`) falham até a promoção do schema na mesma task |
| `media` interno 1.2.0 → 1.3.0 | Idem, mais duas leituras do aluno | `bff-admin`, `bff-student`; testes de contrato de `media` |
| `bff-student` aula 1.1.0 → 1.2.0 | Duas operações novas; nenhuma existente muda | `student-spa` |
| `media` webhook 1.0.0 | Contrato novo, único chamador é o serviço de transcrição | Serviço de transcrição (cadastro do destino e do segredo pelo operador) |

Efeitos sem mudança de contrato: o estado do vídeo (`status`) **nunca** é alterado pela transcrição
(RN-M19); `openPlaybackSession` e `getStudentLesson` não mudam (a página chama a leitura da
transcrição em paralelo). `learning` e `media/asyncapi.yaml` não mudam.

Transição acordada: o vídeo pronto antes desta entrega aparece como `not-requested` com
`canStart: true`; a ação **Transcrever** é o único caminho (sem carga em massa, PRD).

Os recortes de tags de vídeo (CAP-006) e este partem do mesmo arquivo vigente. Por decisão do
responsável (Q-04), as tags consolidam primeiro; a numeração 1.2.0 → 1.3.0 fica como está.

## Validação e verificação

| Documento | Comando e versão da ferramenta | Schema/ruleset e versão | Resultado e avisos |
|---|---|---|---|
| api-contract.yaml | `cd tasks/prd-transcricao-legenda-aula && npx --yes @stoplight/spectral-cli lint api-contract.yaml --ruleset spectral-ruleset.yaml --fail-severity=error` (Spectral 6.17.0) | `.agents/skills/tsg-flow-contract-creator/rulesets/openapi.yaml` via `contracts/.spectral-exception.yaml` + `text/vtt` | Executado: 0 erros, 0 avisos |
| internal-api-contract-media.yaml | idem | idem | Executado: 0 erros, 1 aviso `tsg-collection-pagination-params` em `.../captions`: recurso único (a legenda de uma sessão), não coleção. Exceção registrada, como C-05 em `contracts/README.md` |
| api-contract-student.yaml | idem | idem | Executado: 0 erros, 1 aviso idêntico, mesma justificativa |
| webhook-contract-media.yaml | idem | idem | Executado: 0 erros, 0 avisos |
| AsyncAPI (`@asyncapi/cli` 6.2.1) | n/a | n/a | Não aplicável: nenhum documento AsyncAPI neste conjunto |
| ODCS | n/a | n/a | Não aplicável |

Compatibilidade foi **revisada à mão** contra os arquivos de `contracts/` listados em "Origem". Não há
comparação automática (`oasdiff` ou equivalente) e nenhum teste de conformidade foi executado: lint
estrutural não demonstra comportamento.

Cenários de verificação para a TechSpec (execução na implementação):

- **HTTP/professor:** vídeo pronto sem transcrição devolve `not-requested` e `canStart: true`; pedido
  duplicado ou simultâneo mantém uma só tentativa (RN-M20); vídeo não pronto devolve 409
  `VIDEO_NOT_READY`; `duration-limit` devolve `canStart: false` e 409 `TRANSCRIPTION_NOT_RETRYABLE`;
  Refazer mantém `readable: true` e a leitura devolve a vigente; refeita que falha mantém a vigente;
  ator sem `midia.enviar` recebe 403 e vídeo de outra escola 404 (G07).
- **HTTP/aluno:** sem direito 403 e sem resposta de Matrícula 503, nenhum texto em ambos; aula fora das
  Referências de Uso 404 antes de consultar Matrícula; transcrição em andamento ou falha devolve 200
  `unavailable`; `Cache-Control: private, no-store` em todas; sessão vencida devolve 410 em `captions`;
  sessão de outro aluno devolve o 404 de sessão desconhecida; a resposta não contém e-mail nem dado
  pessoal (RN-M22).
- **Webhook:** assinatura válida, inválida, ausente e fora de ±300 s; duplicata; evento de job
  desconhecido (204); falha transitória (503); nenhuma resposta 410.
- **Dependência:** teste de contrato do cliente de `media` contra o OpenAPI do serviço de transcrição
  (mock), incluindo `schemaVersion` desconhecida e resultado fora da janela de 24 h.

## Pendências e handoff

### Decisões do responsável em 2026-10-12 (fecham Q-01 a Q-04)

| ID | Decisão | Efeito no conjunto |
|---|---|---|
| Q-01 | **403 `ACCESS_DENIED`**: "quem não tem direito à aula não tem a transcrição". O RF-06 do PRD foi corrigido por errata (PRD 1.0 → 1.1) | Mantido como estava: `getLessonTranscription*` responde 403 com `reason`, igual a `getStudentLesson` e `openPlaybackSession`; 401 para visitante sem sessão; 404 só para aula fora das Referências de Uso. Aluno de outra escola continua com o 404 de recurso inexistente (G07) |
| Q-02 | **WebVTT por sessão** confirmado | T-02 e T-08 ficam; `getPlaybackCaptions*` e a exceção de `text/vtt` ficam |
| Q-03 | **Cinco motivos separados** confirmados, para deixar claro o motivo: `duration-limit`, `size-limit`, `service-unavailable`, `processing-failed`, `timed-out`. Os dois de limite não permitem Refazer (`canStart: false`); os outros três permitem | T-07 como estava |
| Q-04 | **Tags de vídeo (CAP-006) consolidam antes** | Versões ficam 1.2.0 → 1.3.0, 1.2.0 → 1.3.0 e 1.1.0 → 1.2.0 |

Nenhuma pendência de contrato resta aberta. Conjunto aprovado em 2026-10-12; próximo passo: TechSpec.

### Observações para a TechSpec e para o time do serviço de transcrição (não bloqueiam)

- O `text` dos segmentos do serviço vem com **espaço inicial**: o adaptador de `media` deve aparar antes
  de guardar e de entregar o texto (afeta a busca, a legenda e a leitura do professor).
- `TRUSTED_PRIVATE_SOURCES` e `TRUSTED_PRIVATE_WEBHOOKS` **ainda não estão configuradas** no deploy do
  serviço. Sem elas, nem o MinIO privado como origem nem o receptor de `media` como destino do aviso
  são aceitos: configurar antes da validação fim a fim do webhook (ADR-003 dele).
- O contrato do serviço (1.0.0) ainda declara `sourceUrl` com `pattern: '^https://'` e 422 para URL não
  HTTPS, embora o código aceite `http://` de destino em allowlist. É divergência **documental do lado
  do serviço**: o cliente de `media` segue o comportamento do código (inclusive a ADR-003) e não o
  `pattern`; não muda o nosso contrato.
- A tela Vídeos e a página da aula precisam do design aprovado (ASCII e Figma) antes do plano de
  implementação: estado, ações e leitura estão contratados, o layout não.
- Na promoção para `contracts/`, acrescentar `text/vtt` à exceção C-08 e `contracts/media/openapi-webhook.yaml`
  à tabela do `contracts/README.md`.

**Handoff para `tsg-flow-techspec-creator`:** partir deste `contracts.md` e dos quatro documentos
técnicos; mapear cada operação ao serviço e ao cenário de verificação acima, sem duplicar os schemas;
decidir os pontos listados em "Fica fora do contrato". A TechSpec só deve ser aberta depois que o
responsável aprovar este conjunto.
