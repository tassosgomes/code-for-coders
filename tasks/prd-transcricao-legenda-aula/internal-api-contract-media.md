# API Contract — Transcrição e legenda (`bff-admin`/`bff-student` → `media`, interno)

> **Gerado a partir de:** [`internal-api-contract-media.yaml`](internal-api-contract-media.yaml) 1.3.0, PRD [`prd.md`](prd.md) v1.1  
> **Data:** 2026-10-12  
> **Status:** Aprovado para implementação em 2026-10-12  
> **Versão do contrato:** 1.3.0 (recorte)

Este documento descreve o acordo para a implementação deste PRD e deriva do YAML correspondente, que é a fonte técnica.
PRDs posteriores podem evoluí-lo. Catalogação e mecanismos de atualização cabem à plataforma.

---

## Premissas e Decisões

| Decisão | Escolha | Motivo |
|---|---|---|
| Autenticação | JWT Bearer de ator interno (`midia.enviar`) ou de aluno, audiência `media` | Como no contrato vigente; `media` valida localmente (ADR-0003, ADR-0005) |
| Escola | `tenantId` do token | G07: outra escola responde como inexistente |
| Camada anticorrupção | Nenhum nome, estado ou limite do serviço de transcrição neste contrato | Domínio de Mídia §5 |
| Versionamento | Prefixo `/internal/v1` | Igual ao contrato vigente |

---

## Resumo de Endpoints

| Método | Path | operationId | Auth | Status |
|---|---|---|---|---|
| `GET` | `/internal/v1/videos` | `listVideosInternal` | StaffUserToken | 200, 400, 401, 403 |
| `GET` | `/internal/v1/videos/{videoId}` | `getVideoInternal` | StaffUserToken | 200, 401, 403, 404 |
| `POST` | `/internal/v1/videos/{videoId}/transcription-requests` | `requestVideoTranscriptionInternal` | StaffUserToken | 202, 401, 403, 404, 409, 422, 400 |
| `GET` | `/internal/v1/videos/{videoId}/transcription` | `getVideoTranscriptionInternal` | StaffUserToken | 200, 401, 403, 404, 409 |
| `GET` | `/internal/v1/lessons/{lessonId}/transcription` | `getLessonTranscriptionInternal` | StudentUserToken | 200, 401, 403, 404, 503 |
| `GET` | `/internal/v1/playback-sessions/{sessionId}/captions` | `getPlaybackCaptionsInternal` | StudentUserToken | 200, 404, 410, 401 |

---

## Endpoints Detalhados

### `GET /internal/v1/videos` — Listar os vídeos da escola

**Consumido por:** `bff-admin` e `bff-student`

Recorte de `listVideosInternal` (contrato vigente): **muda só o corpo de cada vídeo**, que ganha `transcription`. Filtros, ordenação e paginação não mudam e não estão repetidos aqui.

A tela consulta de novo enquanto houver vídeo com preparação (`received`, `preparing`) **ou** com `transcription.status: in-progress`, e para quando não houver (RF-02: a tela atualiza sozinha; sem endpoint de estado separado, T-03).

#### Parâmetros

| Nome | Em | Obrigatório | Descrição |
|---|---|---|---|
| `_page` | query | não | Página, iniciando em 1. |
| `_size` | query | não | Itens por página. |

#### Respostas

| Código | Quando | `code` |
|---|---|---|
| 200 | Página de vídeos, cada um com o estado da transcrição. | — |
| 400 | JSON malformado, campo obrigatório ausente ou valor fora do schema. | `INVALID_REQUEST` |
| 401 | Token ausente, inválido, expirado, com audiência ou emissor errados. | `TOKEN_INVALID` |
| 403 | Token válido sem `midia.enviar`. | `PERMISSION_DENIED` |

Resposta 200, exemplo `variados`:

```json
{
  "data": [
    {
      "videoId": "0b1c2d3e-4f50-4a61-8b72-9c8d7e6f5a41",
      "title": "Aula 3 — Injeção de dependência",
      "status": "ready",
      "uploadedBy": {
        "accountId": "7a8b9c0d-1e2f-4a3b-9c4d-5e6f7a8b9c0d",
        "name": "Marina Alves"
      },
      "uploadedAt": "2026-09-26T09:40:00Z",
      "durationSeconds": 2415,
      "failureReason": null,
      "tags": [
        "Docker"
      ],
      "transcription": {
        "status": "in-progress",
        "readable": false,
        "readyAt": null,
        "failureReason": null,
        "canStart": false
      }
    },
    {
      "videoId": "5e6f7a8b-9c0d-4e1f-8a2b-3c4d5e6f7a8b",
      "title": "Aula 9 — Workshop gravado",
      "status": "ready",
      "uploadedBy": {
        "accountId": "7a8b9c0d-1e2f-4a3b-9c4d-5e6f7a8b9c0d",
        "name": "Marina Alves"
      },
      "uploadedAt": "2026-09-26T09:40:00Z",
      "durationSeconds": 9000,
      "failureReason": null,
      "tags": [
        "Docker"
      ],
      "transcription": {
        "status": "failed",
        "readable": false,
        "readyAt": null,
        "failureReason": "duration-limit",
        "canStart": false
      }
    }
  ],
  "pagination": {
    "page": 1,
    "size": 10,
    "total": 2,
    "totalPages": 1
  }
}
```

---

### `GET /internal/v1/videos/{videoId}` — Consultar um vídeo

**Consumido por:** `bff-admin` e `bff-student`

Recorte de `getVideoInternal` (contrato vigente): a resposta ganha `transcription`. Vídeo de outra escola responde o mesmo 404 de vídeo inexistente. As operações `updateVideoTitleInternal` e `replaceVideoTagsInternal` também devolvem `Video` e passam a trazer `transcription`, sem outra mudança.

#### Parâmetros

| Nome | Em | Obrigatório | Descrição |
|---|---|---|---|
| `videoId` | path | sim | Identificador opaco do vídeo, o mesmo usado nos fatos `midia.*`. |

#### Respostas

| Código | Quando | `code` |
|---|---|---|
| 200 | Vídeo encontrado, com o estado da transcrição. | — |
| 401 | Token ausente, inválido, expirado, com audiência ou emissor errados. | `TOKEN_INVALID` |
| 403 | Token válido sem `midia.enviar`. | `PERMISSION_DENIED` |
| 404 | Vídeo inexistente ou de outra escola — resposta idêntica. | `VIDEO_NOT_FOUND` |

Resposta 200, exemplo `VideoWithoutTranscription`:

```json
{
  "videoId": "0b1c2d3e-4f50-4a61-8b72-9c8d7e6f5a41",
  "title": "Aula 3 — Injeção de dependência",
  "status": "ready",
  "uploadedBy": {
    "accountId": "7a8b9c0d-1e2f-4a3b-9c4d-5e6f7a8b9c0d",
    "name": "Marina Alves"
  },
  "uploadedAt": "2026-09-26T09:40:00Z",
  "durationSeconds": 2415,
  "failureReason": null,
  "tags": [
    "Docker"
  ],
  "transcription": {
    "status": "not-requested",
    "readable": false,
    "readyAt": null,
    "failureReason": null,
    "canStart": true
  }
}
```

Resposta 200, exemplo `VideoTranscriptionReady`:

```json
{
  "videoId": "0b1c2d3e-4f50-4a61-8b72-9c8d7e6f5a41",
  "title": "Aula 3 — Injeção de dependência",
  "status": "ready",
  "uploadedBy": {
    "accountId": "7a8b9c0d-1e2f-4a3b-9c4d-5e6f7a8b9c0d",
    "name": "Marina Alves"
  },
  "uploadedAt": "2026-09-26T09:40:00Z",
  "durationSeconds": 2415,
  "failureReason": null,
  "tags": [
    "Docker"
  ],
  "transcription": {
    "status": "ready",
    "readable": true,
    "readyAt": "2026-10-14T10:12:00Z",
    "failureReason": null,
    "canStart": true
  }
}
```

Resposta 200, exemplo `VideoRedoFailed`:

```json
{
  "videoId": "0b1c2d3e-4f50-4a61-8b72-9c8d7e6f5a41",
  "title": "Aula 3 — Injeção de dependência",
  "status": "ready",
  "uploadedBy": {
    "accountId": "7a8b9c0d-1e2f-4a3b-9c4d-5e6f7a8b9c0d",
    "name": "Marina Alves"
  },
  "uploadedAt": "2026-09-26T09:40:00Z",
  "durationSeconds": 2415,
  "failureReason": null,
  "tags": [
    "Docker"
  ],
  "transcription": {
    "status": "failed",
    "readable": true,
    "readyAt": "2026-10-14T10:12:00Z",
    "failureReason": "service-unavailable",
    "canStart": true
  }
}
```

Resposta 200, exemplo `VideoTooLong`:

```json
{
  "videoId": "0b1c2d3e-4f50-4a61-8b72-9c8d7e6f5a41",
  "title": "Aula 9 — Workshop gravado",
  "status": "ready",
  "uploadedBy": {
    "accountId": "7a8b9c0d-1e2f-4a3b-9c4d-5e6f7a8b9c0d",
    "name": "Marina Alves"
  },
  "uploadedAt": "2026-09-26T09:40:00Z",
  "durationSeconds": 9000,
  "failureReason": null,
  "tags": [
    "Docker"
  ],
  "transcription": {
    "status": "failed",
    "readable": false,
    "readyAt": null,
    "failureReason": "duration-limit",
    "canStart": false
  }
}
```

---

### `POST /internal/v1/videos/{videoId}/transcription-requests` — Pedir a transcrição de um vídeo (Transcrever ou Refazer)

**Consumido por:** `bff-admin` e `bff-student`

Ação **Transcrever** (vídeo pronto sem transcrição) e **Refazer** (com transcrição pronta, ou cuja última tentativa falhou) da tela Vídeos (RF-02, RN-M20). Uma só operação: o serviço decide pelo estado. Qualquer ator com `midia.enviar` pede para qualquer vídeo da escola, de qualquer autor (RN-M03, RN-M04). Sem corpo.

O pedido apenas **registra a intenção** e responde 202 com o vídeo em `transcription.status: in-progress`; o disparo ao serviço de transcrição acontece depois, de forma assíncrona. Indisponibilidade desse serviço **não** falha o pedido nem muda o estado do vídeo (RN-M19). Pedir enquanto já há uma tentativa em andamento não cria outra: devolve 202 com o estado real (RN-M20, sem erro). Refazer mantém a transcrição vigente visível ao aluno até a nova ficar pronta (RN-M18, RN-TL07).

Recusas de regra: vídeo que não está `ready` → 409 `VIDEO_NOT_READY` (também para vídeo que falhou na preparação); última tentativa falhou por `duration-limit` ou `size-limit` → 409 `TRANSCRIPTION_NOT_RETRYABLE` (RN-M24). Vídeo de outra escola responde o mesmo 404 de vídeo inexistente (G07). Não publica fato `midia.*` (domínio de Mídia, §7).

#### Parâmetros

| Nome | Em | Obrigatório | Descrição |
|---|---|---|---|
| `videoId` | path | sim | Identificador opaco do vídeo, o mesmo usado nos fatos `midia.*`. |
| `Idempotency-Key` | header | sim | Chave propagada sem alteração pelo BFF. Isolada por tenant, operationId e ator; mesma janela e regra do contrato público. |

#### Respostas

| Código | Quando | `code` |
|---|---|---|
| 202 | Pedido aceito (ou já havia uma tentativa em andamento); o vídeo no estado real depois do pedido. | — |
| 401 | Token ausente, inválido, expirado, com audiência ou emissor errados. | `TOKEN_INVALID` |
| 403 | Token válido sem `midia.enviar`. | `PERMISSION_DENIED` |
| 404 | Vídeo inexistente ou de outra escola — resposta idêntica. | `VIDEO_NOT_FOUND` |
| 409 | O vídeo não está pronto, ou a última tentativa falhou por um motivo que refazer não resolve. | `VIDEO_NOT_READY`, `TRANSCRIPTION_NOT_RETRYABLE` |
| 422 | Chave de idempotência reutilizada em outro vídeo. | `IDEMPOTENCY_KEY_REUSED` |
| 400 | JSON malformado, campo obrigatório ausente ou valor fora do schema. | `INVALID_REQUEST` |

Resposta 202, exemplo `transcrevendo`:

```json
{
  "videoId": "0b1c2d3e-4f50-4a61-8b72-9c8d7e6f5a41",
  "title": "Aula 3 — Injeção de dependência",
  "status": "ready",
  "uploadedBy": {
    "accountId": "7a8b9c0d-1e2f-4a3b-9c4d-5e6f7a8b9c0d",
    "name": "Marina Alves"
  },
  "uploadedAt": "2026-09-26T09:40:00Z",
  "durationSeconds": 2415,
  "failureReason": null,
  "tags": [
    "Docker"
  ],
  "transcription": {
    "status": "in-progress",
    "readable": false,
    "readyAt": null,
    "failureReason": null,
    "canStart": false
  }
}
```

Resposta 202, exemplo `refazendo`:

```json
{
  "videoId": "0b1c2d3e-4f50-4a61-8b72-9c8d7e6f5a41",
  "title": "Aula 3 — Injeção de dependência",
  "status": "ready",
  "uploadedBy": {
    "accountId": "7a8b9c0d-1e2f-4a3b-9c4d-5e6f7a8b9c0d",
    "name": "Marina Alves"
  },
  "uploadedAt": "2026-09-26T09:40:00Z",
  "durationSeconds": 2415,
  "failureReason": null,
  "tags": [
    "Docker"
  ],
  "transcription": {
    "status": "in-progress",
    "readable": true,
    "readyAt": "2026-10-14T10:12:00Z",
    "failureReason": null,
    "canStart": false
  }
}
```

---

### `GET /internal/v1/videos/{videoId}/transcription` — Ler a transcrição vigente de um vídeo (professor)

**Consumido por:** `bff-admin` e `bff-student`

Texto completo da transcrição **vigente**, em ordem, com o tempo de cada trecho, para o professor avaliar a qualidade (RF-08, RN-M27). Somente leitura. **Não é reprodução**: não abre Sessão de Reprodução, não consulta Matrícula e não traz marca d'água (RN-TL08). Exige `midia.enviar`; vídeo de outra escola responde o mesmo 404 de vídeo inexistente (G07).

Só existe quando `transcription.readable` é `true`; senão 409 `TRANSCRIPTION_NOT_READY` e o professor vê o estado (RF-02). Com refeita em andamento, devolve a vigente. Resposta `Cache-Control: private, no-store`. O texto não aparece em log nem em URL (RN-M26).

#### Parâmetros

| Nome | Em | Obrigatório | Descrição |
|---|---|---|---|
| `videoId` | path | sim | Identificador opaco do vídeo, o mesmo usado nos fatos `midia.*`. |

#### Respostas

| Código | Quando | `code` |
|---|---|---|
| 200 | Transcrição vigente. | — |
| 401 | Token ausente, inválido, expirado, com audiência ou emissor errados. | `TOKEN_INVALID` |
| 403 | Token válido sem `midia.enviar`. | `PERMISSION_DENIED` |
| 404 | Vídeo inexistente ou de outra escola — resposta idêntica. | `VIDEO_NOT_FOUND` |
| 409 | O vídeo não tem transcrição pronta (sem transcrição, transcrevendo pela primeira vez ou falhou sem vigente). | `TRANSCRIPTION_NOT_READY` |

Resposta 200, exemplo `vigente`:

```json
{
  "videoId": "0b1c2d3e-4f50-4a61-8b72-9c8d7e6f5a41",
  "revision": "tr_7d2a91c4",
  "language": "pt-BR",
  "durationMs": 2415000,
  "readyAt": "2026-10-14T10:12:00Z",
  "segments": [
    {
      "startMs": 1240,
      "endMs": 4100,
      "text": "Hoje vamos ver o docker compose."
    },
    {
      "startMs": 4100,
      "endMs": 7900,
      "text": "Ele descreve vários containers em um só arquivo."
    }
  ]
}
```

---

### `GET /internal/v1/lessons/{lessonId}/transcription` — Obter a transcrição da aula

**Consumido por:** `bff-admin` e `bff-student`

Transcrição da Mídia vinculada à aula na versão vigente do curso, para a página da aula (RF-04, RF-05, RN-TL03). O aluno **não informa vídeo**: informa a aula, e Mídia resolve o vídeo pela Referência de Uso. Não exige Sessão de Reprodução aberta: a página mostra o texto antes de o aluno apertar play (RN-TL02).

Mesma verificação da reprodução, nesta ordem (RN-M21, RN-M10 passos 1 e 2): (1) a aula está entre as Referências de Uso de um vídeo da escola do aluno (404 `LESSON_NOT_AVAILABLE`, **antes** de consultar Matrícula); (2) Matrícula responde que o aluno pode acessar o curso **agora** (403 `ACCESS_DENIED`; sem resposta, 503 `ACCESS_DECISION_UNAVAILABLE`, falha fechada, nunca "sem acesso"); a resposta pode ser reaproveitada por até 30 s, como na sessão. Só depois se lê o estado da transcrição.

Depois da decisão positiva, vídeo sem transcrição pronta (não pronto, em andamento ou falhou) responde **200** com `status: unavailable`, sem texto e sem motivo técnico (RN-TL05); a aula continua reproduzível. Refazer troca o texto na próxima leitura, sem republicar o curso (DP-09). Não há endereço estável, público ou em cache compartilhado (`Cache-Control: private, no-store`); o texto não tem dado do aluno (RN-M22) e não vai a log, métrica nem URL (RN-M26).

#### Parâmetros

| Nome | Em | Obrigatório | Descrição |
|---|---|---|---|
| `lessonId` | path | sim | Identidade estável da aula (RN-C07). Opaca para quem a recebe. |

#### Respostas

| Código | Quando | `code` |
|---|---|---|
| 200 | Transcrição pronta, ou ausência dela em linguagem neutra. | — |
| 401 | Token ausente, inválido, expirado, com audiência ou emissor errados. | `TOKEN_INVALID` |
| 403 | Matrícula respondeu que o aluno não pode acessar o curso agora. `reason` distingue nunca ter tido de ter vencido; nada do conteúdo do curso é revelado. | `ACCESS_DENIED` |
| 404 | Aula inexistente, de outra escola, de curso não publicado ou removida da versão vigente. A resposta **não distingue** o motivo (RN-R07). | `LESSON_NOT_AVAILABLE` |
| 503 | Matrícula não respondeu a tempo ou respondeu com erro. **Não é negação** (RN-R02): nenhum texto é entregue. O cliente oferece *Tentar de novo*. | `ACCESS_DECISION_UNAVAILABLE` |

Resposta 200, exemplo `pronta`:

```json
{
  "lessonId": "3f6a1c52-7d84-4b0e-9a13-5c2e8f4d7b60",
  "status": "ready",
  "revision": "tr_7d2a91c4",
  "language": "pt-BR",
  "durationMs": 2415000,
  "segments": [
    {
      "startMs": 1240,
      "endMs": 4100,
      "text": "Hoje vamos ver o docker compose."
    },
    {
      "startMs": 4100,
      "endMs": 7900,
      "text": "Ele descreve vários containers em um só arquivo."
    }
  ]
}
```

Resposta 200, exemplo `indisponivel`:

```json
{
  "lessonId": "3f6a1c52-7d84-4b0e-9a13-5c2e8f4d7b60",
  "status": "unavailable",
  "revision": null,
  "language": null,
  "durationMs": null,
  "segments": []
}
```

---

### `GET /internal/v1/playback-sessions/{sessionId}/captions` — Obter a legenda da sessão (WebVTT)

**Consumido por:** `bff-admin` e `bff-student`

Legenda do vídeo da Sessão de Reprodução, em WebVTT, para o player (RF-03). Sai da **mesma** Transcrição, não é artefato separado (domínio de Mídia). Vale enquanto a sessão for válida e do aluno; a sessão já passou por Referência de Uso e Matrícula ao abrir ou renovar (RN-M10, RN-M11), então **não** consulta Matrícula de novo: perdido o direito, a legenda para no máximo na renovação, quando a sessão deixa de se estender e o texto novo não é entregue (RN-M21). Sessão de outro aluno ou de outra escola responde o mesmo 404 de sessão desconhecida.

Formato: `text/vtt` em UTF-8, cabeçalho `WEBVTT`, um *cue* por Trecho, tempos `hh:mm:ss.mmm`, sem identificação do aluno, sem marcação além do texto escapado (formato exato e escape: TechSpec). Sem transcrição pronta, 404 `CAPTIONS_NOT_AVAILABLE`: o player não mostra o controle de legenda (RN-TL05). Exceção ao gate HTTP (T-08, no molde de C-08 da entrega HLS): corpo `text/vtt`, não JSON. `Cache-Control: private, no-store`.

#### Parâmetros

| Nome | Em | Obrigatório | Descrição |
|---|---|---|---|
| `sessionId` | path | sim | Identificador da Sessão de Reprodução. |

#### Respostas

| Código | Quando | `code` |
|---|---|---|
| 200 | Legenda em WebVTT. | — |
| 404 | Sessão desconhecida, de outro aluno ou de outra escola (`PLAYBACK_SESSION_NOT_FOUND`), ou o vídeo não tem transcrição pronta (`CAPTIONS_NOT_AVAILABLE`). O cliente trata o `code`. | `PLAYBACK_SESSION_NOT_FOUND`, `CAPTIONS_NOT_AVAILABLE` |
| 410 | A validade da sessão terminou sem renovação. O player abre uma sessão nova; nenhum texto novo é entregue (RN-M21). | `PLAYBACK_SESSION_EXPIRED` |
| 401 | Token ausente, inválido, expirado, com audiência ou emissor errados. | `TOKEN_INVALID` |

Resposta 200, exemplo `legenda`:

```text
WEBVTT

1
00:00:01.240 --> 00:00:04.100
Hoje vamos ver o docker compose.

2
00:00:04.100 --> 00:00:07.900
Ele descreve vários containers em um só arquivo.

```

---

## Schemas

### Video

Vídeo da escola. **Evolução aditiva** sobre o contrato vigente: acrescenta `transcription` (obrigatório na resposta). Os demais campos permanecem como em `contracts/`; aparecem aqui só para que o recorte seja válido.

| Campo | Tipo | Obrigatório | Descrição |
|---|---|---|---|
| `videoId` | string (uuid) | sim |  |
| `title` | string | sim | Nome do vídeo para reconhecê-lo no vínculo à aula. |
| `status` | enum: `received`, `preparing`, `ready`, `failed` | sim | `received` aguardando preparação; `preparing` em preparação; `ready` pronto; `failed` a preparação esgotou as tentativas. Só anda para frente (RN-M05). **A transcrição nunca altera este estado** (RN-M19). |
| `uploadedBy` | object | sim |  |
| `uploadedAt` | string (date-time) | sim |  |
| `durationSeconds` | integer | null | sim | Duração em segundos; preenchida só em `ready`. |
| `failureReason` | enum: `unreadable-file`, `unsupported-format`, `duration-exceeded`, `preparation-failed` ou null | sim | Motivo da falha **da preparação**; preenchido só em `failed`. Não confundir com `transcription.failureReason`. |
| `tags` | array de string | sim | Tags do vídeo (contrato vigente, sem mudança neste PRD). |
| `transcription` | `VideoTranscription` | sim |  |

### VideoPage



| Campo | Tipo | Obrigatório | Descrição |
|---|---|---|---|
| `data` | array de `Video` | sim |  |
| `pagination` | `Pagination` | sim |  |

### Pagination



| Campo | Tipo | Obrigatório | Descrição |
|---|---|---|---|
| `page` | integer | sim |  |
| `size` | integer | sim |  |
| `total` | integer | sim |  |
| `totalPages` | integer | sim |  |

### TranscriptionStatus

Estado da **última tentativa** de transcrição do vídeo, na linguagem do professor (RN-TL06): `not-requested` sem transcrição; `in-progress` transcrevendo; `ready` pronta; `failed` falhou. Vídeo que não está `ready` (RN-M05) é sempre `not-requested`. Refazer mantém a transcrição vigente legível enquanto a nova roda (`status: in-progress`, `readable: true`) e depois que a nova falha (`status: failed`, `readable: true`): ver `readable` (RN-M18, RN-TL07).

Valores: `not-requested`, `in-progress`, `ready`, `failed`

### TranscriptionFailureReason

Categoria do motivo da falha, em termos do produto; nenhum código, estado ou limite do serviço de transcrição aparece aqui (camada anticorrupção). O SPA mostra o texto do PRD: `duration-limit` "vídeo mais longo que o limite de 2 horas"; `size-limit` "vídeo maior que o tamanho aceito para transcrição"; `service-unavailable` "serviço de transcrição indisponível no momento"; `processing-failed` "não foi possível transcrever este vídeo"; `timed-out` "a transcrição demorou mais que o limite". `duration-limit` e `size-limit` não se resolvem refazendo (RN-M24): `canStart` é `false`. Clientes devem tolerar valor novo.

Valores: `duration-limit`, `size-limit`, `service-unavailable`, `processing-failed`, `timed-out`

### VideoTranscription

Estado da transcrição do vídeo para a tela Vídeos (RF-02, RF-08). Sempre presente; para vídeo que não está pronto vale `not-requested`, `readable: false`, `canStart: false`.

| Campo | Tipo | Obrigatório | Descrição |
|---|---|---|---|
| `status` | `TranscriptionStatus` | sim |  |
| `readable` | boolean | sim | Existe uma transcrição vigente pronta que o professor pode abrir em `getVideoTranscription` e que o aluno recebe (RN-M18, RN-M27). Pode ser `true` com `status` `in-progress` ou `failed` (refazer em andamento ou que falhou). |
| `readyAt` | string | null (date-time) | sim | Quando a transcrição **vigente** ficou pronta; `null` se não há vigente. |
| `failureReason` | `TranscriptionFailureReason` ou null | sim | Preenchido só com `status: failed` (motivo da última tentativa). |
| `canStart` | boolean | sim | Se `requestVideoTranscription` seria aceita agora: vídeo `ready`, nenhuma tentativa em andamento e, se a última falhou, o motivo não é `duration-limit` nem `size-limit`. Dirige a ação **Transcrever** (`status: not-requested`) ou **Refazer** (`ready` ou `failed`) (RF-02). |

### TranscriptSegment

Trecho: unidade da legenda, da busca e do clique que leva o vídeo ao tempo (domínio de Mídia). Tempos desde o início do vídeo.

| Campo | Tipo | Obrigatório | Descrição |
|---|---|---|---|
| `startMs` | integer | sim | Início do trecho, em milissegundos. |
| `endMs` | integer | sim | Fim do trecho, em milissegundos; nunca antes de `startMs`. |
| `text` | string | sim | Texto da fala, em português do Brasil, como o serviço de transcrição o produziu (DP-07). Sem dado do aluno (RN-M22). |

### VideoTranscriptionContent

Transcrição vigente de um vídeo, para leitura do professor no backoffice (RF-08, RN-M27). Somente leitura: não há operação de edição (DP-07).

| Campo | Tipo | Obrigatório | Descrição |
|---|---|---|---|
| `videoId` | string (uuid) | sim |  |
| `revision` | string | sim | Identificador opaco da transcrição vigente; muda quando uma refeita a substitui (DP-09). Igual para todos os alunos e sem dado pessoal (RN-M22). Serve para o cliente saber se o texto que tem ainda é o vigente. |
| `language` | string | sim | Idioma fixo neste corte (RN-M25). |
| `durationMs` | integer | sim | Duração coberta pela transcrição, em milissegundos. |
| `readyAt` | string (date-time) | sim | Quando esta transcrição ficou pronta. |
| `segments` | array de `TranscriptSegment` | sim | Trechos em ordem crescente de `startMs`. Resposta única, sem paginação: uma aula de 2 horas tem alguns milhares de trechos (a busca roda no cliente, RF-05). Limites exatos de tamanho: TechSpec. |

### LessonTranscription

Transcrição da aula para o aluno com direito a ela (RF-04). É a da Mídia vinculada na versão vigente do curso (RN-TL03). `unavailable` cobre "ainda não pronta", "em andamento" e "falhou" sem distinguir (RN-TL05): não é erro nem falta de acesso, e o player toca normalmente.

| Campo | Tipo | Obrigatório | Descrição |
|---|---|---|---|
| `lessonId` | string (uuid) | sim |  |
| `status` | enum: `ready`, `unavailable` | sim | `ready` com `segments`; `unavailable` sem texto. |
| `revision` | string ou null | sim | Preenchida só em `ready`. |
| `language` | enum: `pt-BR`, `null` | sim | `pt-BR` em `ready`; `null` em `unavailable`. |
| `durationMs` | integer | null | sim | Preenchida só em `ready`. |
| `segments` | array de `TranscriptSegment` | sim | Trechos em ordem crescente de `startMs`; `[]` em `unavailable`. |

## Formato de erro

RFC 9457, `application/problem+json`, com `type`, `title`, `status`, `code` e `traceId` (extensões do repositório). O cliente trata o `code`, nunca o texto livre.

```json
{
  "type": "about:blank",
  "title": "Sessão de reprodução não encontrada.",
  "status": 404,
  "code": "PLAYBACK_SESSION_NOT_FOUND",
  "traceId": "00-9f2b5d6c4e7a8b90123456789abcdef0-0123456789abcdef-01"
}
```

## Questões em aberto

Ver "Pendências e handoff" em [`contracts.md`](contracts.md).
