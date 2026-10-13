# API Contract — Transcrição e legenda da aula (`student-spa` → `bff-student`)

> **Gerado a partir de:** [`api-contract-student.yaml`](api-contract-student.yaml) 1.2.0, PRD [`prd.md`](prd.md) v1.1  
> **Data:** 2026-10-12  
> **Status:** Aprovado para implementação em 2026-10-12  
> **Versão do contrato:** 1.2.0 (recorte)

Este documento descreve o acordo para a implementação deste PRD e deriva do YAML correspondente, que é a fonte técnica.
PRDs posteriores podem evoluí-lo. Catalogação e mecanismos de atualização cabem à plataforma.

---

## Premissas e Decisões

| Decisão | Escolha | Motivo |
|---|---|---|
| Autenticação | Cookie `student_session`; leituras, sem CSRF | Mesma do contrato vigente da aula |
| Transcrição na página | `GET /lessons/{lessonId}/transcription`, por aula, sem sessão de reprodução | A página mostra o texto antes do play e o clique abre a sessão (RN-TL02) (T-01) |
| Legenda no player | `GET /playback-sessions/{sessionId}/captions` em WebVTT, ligada à sessão | Para quando a sessão não renova (RN-M21); WebVTT atende ao player nativo (T-02) |
| Busca | Não há operação: local, sobre os trechos recebidos | RN-TL01 e RF-05; nada de texto no servidor |
| Versionamento | Prefixo `/api/v1` | Igual ao contrato vigente |
| Datas | ISO 8601 UTC; tempos de trecho em milissegundos inteiros | Precisão de legenda |

---

## Resumo de Endpoints

| Método | Path | operationId | Auth | Status |
|---|---|---|---|---|
| `GET` | `/api/v1/lessons/{lessonId}/transcription` | `getLessonTranscription` | StudentSessionCookie | 200, 401, 403, 404, 503, 502, 504 |
| `GET` | `/api/v1/playback-sessions/{sessionId}/captions` | `getPlaybackCaptions` | StudentSessionCookie | 200, 404, 410, 401, 502, 504 |

---

## Endpoints Detalhados

### `GET /api/v1/lessons/{lessonId}/transcription` — Obter a transcrição da aula

**Consumido por:** Frontend — `student-spa`, página da aula e player

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
| 401 | Sessão do aluno ausente, expirada ou revogada. | `SESSION_REQUIRED` |
| 403 | Matrícula respondeu que o aluno não pode acessar o curso agora. `reason` distingue nunca ter tido de ter vencido; nada do conteúdo do curso é revelado. | `ACCESS_DENIED` |
| 404 | Aula inexistente, de outra escola, de curso não publicado ou removida da versão vigente. A resposta **não distingue** o motivo (RN-R07). | `LESSON_NOT_AVAILABLE` |
| 503 | Matrícula não respondeu a tempo ou respondeu com erro. **Não é negação** (RN-R02): nenhum texto é entregue. O cliente oferece *Tentar de novo*. | `ACCESS_DECISION_UNAVAILABLE` |
| 502 | `media` ou `identity` não respondeu ou falhou. Nenhum texto é entregue; a tela mostra indisponibilidade, nunca "sem acesso" (RN-R02). | `UPSTREAM_UNAVAILABLE` |
| 504 | O serviço não respondeu no tempo limite. | `UPSTREAM_TIMEOUT` |

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

### `GET /api/v1/playback-sessions/{sessionId}/captions` — Obter a legenda da sessão (WebVTT)

**Consumido por:** Frontend — `student-spa`, página da aula e player

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
| 401 | Sessão do aluno ausente, expirada ou revogada. | `SESSION_REQUIRED` |
| 502 | `media` ou `identity` não respondeu ou falhou. Nenhum texto é entregue; a tela mostra indisponibilidade, nunca "sem acesso" (RN-R02). | `UPSTREAM_UNAVAILABLE` |
| 504 | O serviço não respondeu no tempo limite. | `UPSTREAM_TIMEOUT` |

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

### TranscriptSegment

Trecho: unidade da legenda, da busca e do clique que leva o vídeo ao tempo (domínio de Mídia). Tempos desde o início do vídeo.

| Campo | Tipo | Obrigatório | Descrição |
|---|---|---|---|
| `startMs` | integer | sim | Início do trecho, em milissegundos. |
| `endMs` | integer | sim | Fim do trecho, em milissegundos; nunca antes de `startMs`. |
| `text` | string | sim | Texto da fala, em português do Brasil, como o serviço de transcrição o produziu (DP-07). Sem dado do aluno (RN-M22). |

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
