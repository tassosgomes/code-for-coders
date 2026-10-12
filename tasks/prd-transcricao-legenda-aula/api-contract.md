# API Contract — Transcrição de vídeo na tela Vídeos (`admin-spa` → `bff-admin`)

> **Gerado a partir de:** [`api-contract.yaml`](api-contract.yaml) 1.3.0, PRD [`prd.md`](prd.md) v1.1  
> **Data:** 2026-10-12  
> **Status:** Aprovado para implementação em 2026-10-12  
> **Versão do contrato:** 1.3.0 (recorte)

Este documento descreve o acordo para a implementação deste PRD e deriva do YAML correspondente, que é a fonte técnica.
PRDs posteriores podem evoluí-lo. Catalogação e mecanismos de atualização cabem à plataforma.

---

## Premissas e Decisões

| Decisão | Escolha | Motivo |
|---|---|---|
| Autenticação | Cookie `staff_session` + `X-CSRF-Token` na escrita | Mesma do contrato vigente de vídeos; exige `midia.enviar` (RN-M03) |
| Estado da transcrição | Campo `transcription` em `Video`, sem endpoint de estado | A tela já consulta `listVideos` enquanto há trabalho em andamento (T-03) |
| Disparo | Uma operação `POST /videos/{videoId}/transcription-requests` para Transcrever e Refazer | O serviço decide pelo estado; disparo repetido não cria duas (RN-M20) (T-04) |
| Leitura pelo professor | `GET /videos/{videoId}/transcription`, somente leitura | RN-M27/DP-08: não é reprodução e não passa por Matrícula (T-05) |
| Versionamento | Prefixo `/api/v1` | Igual ao contrato vigente |
| Datas | ISO 8601 UTC; tempos de trecho em milissegundos inteiros | Precisão de legenda |
| Paginação | `_page`/`_size` só em `listVideos`, já vigente | Sem mudança |

---

## Resumo de Endpoints

| Método | Path | operationId | Auth | Status |
|---|---|---|---|---|
| `GET` | `/api/v1/videos` | `listVideos` | StaffSessionCookie | 200, 400, 401, 403, 502, 504 |
| `GET` | `/api/v1/videos/{videoId}` | `getVideo` | StaffSessionCookie | 200, 401, 403, 404, 502, 504 |
| `POST` | `/api/v1/videos/{videoId}/transcription-requests` | `requestVideoTranscription` | StaffSessionCookie | 202, 401, 403, 404, 409, 422, 400, 502, 504 |
| `GET` | `/api/v1/videos/{videoId}/transcription` | `getVideoTranscription` | StaffSessionCookie | 200, 401, 403, 404, 409, 502, 504 |

---

## Endpoints Detalhados

### `GET /api/v1/videos` — Listar os vídeos da escola

**Consumido por:** Frontend — `admin-spa`, tela Vídeos (estado, Transcrever/Refazer, leitura)

Recorte de `listVideos` (contrato vigente): **muda só o corpo de cada vídeo**, que ganha `transcription`. Filtros, ordenação e paginação não mudam e não estão repetidos aqui.

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
| 401 | Sessão ausente, expirada ou encerrada — inclusive por revogação de papel (RN-17). | `SESSION_REQUIRED` |
| 403 | Token válido sem `midia.enviar`. | `PERMISSION_DENIED` |
| 502 | Media indisponível ou falha de infraestrutura atrás do BFF. Nenhum detalhe interno é exposto; o cliente pode repetir com a mesma `Idempotency-Key`. | `MEDIA_UNAVAILABLE` |
| 504 | Media não respondeu dentro do tempo limite do BFF. Mesmo tratamento de `MediaUnavailable`. | `MEDIA_UNAVAILABLE` |

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

### `GET /api/v1/videos/{videoId}` — Consultar um vídeo

**Consumido por:** Frontend — `admin-spa`, tela Vídeos (estado, Transcrever/Refazer, leitura)

Recorte de `getVideo` (contrato vigente): a resposta ganha `transcription`. Vídeo de outra escola responde o mesmo 404 de vídeo inexistente. As operações `updateVideoTitle` e `replaceVideoTags` também devolvem `Video` e passam a trazer `transcription`, sem outra mudança.

#### Parâmetros

| Nome | Em | Obrigatório | Descrição |
|---|---|---|---|
| `videoId` | path | sim | Identificador opaco do vídeo, o mesmo usado nos fatos `midia.*`. |

#### Respostas

| Código | Quando | `code` |
|---|---|---|
| 200 | Vídeo encontrado, com o estado da transcrição. | — |
| 401 | Sessão ausente, expirada ou encerrada — inclusive por revogação de papel (RN-17). | `SESSION_REQUIRED` |
| 403 | Token válido sem `midia.enviar`. | `PERMISSION_DENIED` |
| 404 | Vídeo inexistente ou de outra escola — resposta idêntica. | `VIDEO_NOT_FOUND` |
| 502 | Media indisponível ou falha de infraestrutura atrás do BFF. Nenhum detalhe interno é exposto; o cliente pode repetir com a mesma `Idempotency-Key`. | `MEDIA_UNAVAILABLE` |
| 504 | Media não respondeu dentro do tempo limite do BFF. Mesmo tratamento de `MediaUnavailable`. | `MEDIA_UNAVAILABLE` |

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

### `POST /api/v1/videos/{videoId}/transcription-requests` — Pedir a transcrição de um vídeo (Transcrever ou Refazer)

**Consumido por:** Frontend — `admin-spa`, tela Vídeos (estado, Transcrever/Refazer, leitura)

Ação **Transcrever** (vídeo pronto sem transcrição) e **Refazer** (com transcrição pronta, ou cuja última tentativa falhou) da tela Vídeos (RF-02, RN-M20). Uma só operação: o serviço decide pelo estado. Qualquer ator com `midia.enviar` pede para qualquer vídeo da escola, de qualquer autor (RN-M03, RN-M04). Sem corpo.

O pedido apenas **registra a intenção** e responde 202 com o vídeo em `transcription.status: in-progress`; o disparo ao serviço de transcrição acontece depois, de forma assíncrona. Indisponibilidade desse serviço **não** falha o pedido nem muda o estado do vídeo (RN-M19). Pedir enquanto já há uma tentativa em andamento não cria outra: devolve 202 com o estado real (RN-M20, sem erro). Refazer mantém a transcrição vigente visível ao aluno até a nova ficar pronta (RN-M18, RN-TL07).

Recusas de regra: vídeo que não está `ready` → 409 `VIDEO_NOT_READY` (também para vídeo que falhou na preparação); última tentativa falhou por `duration-limit` ou `size-limit` → 409 `TRANSCRIPTION_NOT_RETRYABLE` (RN-M24). Vídeo de outra escola responde o mesmo 404 de vídeo inexistente (G07). Não publica fato `midia.*` (domínio de Mídia, §7).

#### Parâmetros

| Nome | Em | Obrigatório | Descrição |
|---|---|---|---|
| `videoId` | path | sim | Identificador opaco do vídeo, o mesmo usado nos fatos `midia.*`. |
| `Idempotency-Key` | header | sim | Chave opaca única por intenção de escrita. Repetição na janela de 24 horas devolve o mesmo resultado; a mesma chave em outro vídeo retorna 422 `IDEMPOTENCY_KEY_REUSED`. |
| `X-CSRF-Token` | header | sim | Prova CSRF vinculada à sessão, obtida de GET /staff-sessions/current (CAP-002). |

#### Respostas

| Código | Quando | `code` |
|---|---|---|
| 202 | Pedido aceito (ou já havia uma tentativa em andamento); o vídeo no estado real depois do pedido. | — |
| 401 | Sessão ausente, expirada ou encerrada — inclusive por revogação de papel (RN-17). | `SESSION_REQUIRED` |
| 403 | Sessão válida sem `midia.enviar`, ou prova CSRF ausente ou inválida. | `PERMISSION_DENIED`, `CSRF_INVALID` |
| 404 | Vídeo inexistente ou de outra escola — resposta idêntica. | `VIDEO_NOT_FOUND` |
| 409 | O vídeo não está pronto, ou a última tentativa falhou por um motivo que refazer não resolve. | `VIDEO_NOT_READY`, `TRANSCRIPTION_NOT_RETRYABLE` |
| 422 | Chave de idempotência reutilizada em outro vídeo. | `IDEMPOTENCY_KEY_REUSED` |
| 400 | JSON malformado, campo obrigatório ausente ou valor fora do schema. | `INVALID_REQUEST` |
| 502 | Media indisponível ou falha de infraestrutura atrás do BFF. Nenhum detalhe interno é exposto; o cliente pode repetir com a mesma `Idempotency-Key`. | `MEDIA_UNAVAILABLE` |
| 504 | Media não respondeu dentro do tempo limite do BFF. Mesmo tratamento de `MediaUnavailable`. | `MEDIA_UNAVAILABLE` |

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

### `GET /api/v1/videos/{videoId}/transcription` — Ler a transcrição vigente de um vídeo (professor)

**Consumido por:** Frontend — `admin-spa`, tela Vídeos (estado, Transcrever/Refazer, leitura)

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
| 401 | Sessão ausente, expirada ou encerrada — inclusive por revogação de papel (RN-17). | `SESSION_REQUIRED` |
| 403 | Token válido sem `midia.enviar`. | `PERMISSION_DENIED` |
| 404 | Vídeo inexistente ou de outra escola — resposta idêntica. | `VIDEO_NOT_FOUND` |
| 409 | O vídeo não tem transcrição pronta (sem transcrição, transcrevendo pela primeira vez ou falhou sem vigente). | `TRANSCRIPTION_NOT_READY` |
| 502 | Media indisponível ou falha de infraestrutura atrás do BFF. Nenhum detalhe interno é exposto; o cliente pode repetir com a mesma `Idempotency-Key`. | `MEDIA_UNAVAILABLE` |
| 504 | Media não respondeu dentro do tempo limite do BFF. Mesmo tratamento de `MediaUnavailable`. | `MEDIA_UNAVAILABLE` |

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
