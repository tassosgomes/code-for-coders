# API Contract — Aviso de término da transcrição (serviço de transcrição → `media`)

> **Gerado a partir de:** [`webhook-contract-media.yaml`](webhook-contract-media.yaml) 1.0.0, PRD [`prd.md`](prd.md) v1.1  
> **Data:** 2026-10-12  
> **Status:** Aprovado para implementação em 2026-10-12  
> **Versão do contrato:** 1.0.0 (recorte)

Este documento descreve o acordo para a implementação deste PRD e deriva do YAML correspondente, que é a fonte técnica.
PRDs posteriores podem evoluí-lo. Catalogação e mecanismos de atualização cabem à plataforma.

---

## Premissas e Decisões

| Decisão | Escolha | Motivo |
|---|---|---|
| Autenticação | Standard Webhooks v1: HMAC-SHA256 com segredo exclusivo do destino | O serviço de transcrição assina assim (dependência consumida) (T-06) |
| Resposta | 204 em toda entrega processada, inclusive duplicada ou de job desconhecido; nunca 410 | 410 desativaria o destino no remetente |
| Versionamento | Prefixo `/webhooks/v1` | Mesmo molde do webhook de `billing` |

---

## Resumo de Endpoints

| Método | Path | operationId | Auth | Status |
|---|---|---|---|---|
| `POST` | `/webhooks/v1/transcription-events` | `receiveTranscriptionEvent` | WebhookSignature | 204, 400, 401, 503 |

---

## Endpoints Detalhados

### `POST /webhooks/v1/transcription-events` — Receber o aviso de término de uma transcrição

**Consumido por:** Serviço de transcrição próprio (repositório `tassosgomes/whisper-api`)

Receptor do aviso terminal do serviço de transcrição próprio (dependência consumida, repositório separado; contrato dele: `tassosgomes/whisper-api`, `tasks/prd-api-transcricao-assincrona/api-contract.yaml` 1.0.0). O serviço envia **depois** que o trabalho termina com sucesso ou falha, ao destino cadastrado pelo operador. O aviso é só um **gatilho**: não carrega texto nem motivo; `media` confirma o desfecho consultando o serviço e, se pronto, copia o texto para guarda própria (RN-M23).

Garantias observáveis (mecanismo na TechSpec):

- **Autenticidade** (Standard Webhooks v1): `webhook-signature` contém uma ou mais assinaturas `v1,<base64>` separadas por espaço (duas durante rotação planejada de segredo); vale se **alguma** confere com `HMAC-SHA256(segredo, webhook-id + "." + webhook-timestamp + "." + corpo bruto)` sobre os bytes exatos recebidos, comparada em tempo constante. `webhook-timestamp` (segundos Unix) fora de ±300 s da hora do servidor também é recusado. Assinatura ausente, inválida ou fora da tolerância → 401, nada muda. O segredo é exclusivo deste destino, independente da chave de API do serviço, é configuração de implantação, nunca de código, e nunca vai para log.
- **Idempotência**: a entrega é ao menos uma vez; `webhook-id` (igual ao `eventId`) é estável entre tentativas. Evento já tratado responde 204 sem segundo efeito, durante toda a janela de reenvio de 72 h.
- **Registro antes de responder**: 204 só depois de o evento estar gravado; a consulta ao serviço e a cópia do texto podem ocorrer depois, sem depender de nova entrega (RF-07).
- **Não faz acusar o remetente**: aviso de job desconhecido, de tentativa já encerrada ou sem `clientReference` responde 204 e é ignorado, para o serviço não insistir por 72 h. Este receptor **nunca** responde 410 (o serviço desativa o destino ao receber 410) nem 3xx.
- **Falha transitória** de `media` antes de gravar → 503 e o serviço reenvia pela política dele.

`clientReference` é a referência opaca que `media` enviou ao criar o pedido: o identificador da **tentativa de transcrição** de `media` (nunca o título, o texto, o e-mail ou o identificador de pessoa). É por ela que `media` acha o vídeo e a escola; o aviso não informa escola. O serviço não interpreta a referência.

Endpoint dedicado, sem sessão de usuário e sem BFF: o chamador é o serviço de transcrição. Exposto só na rede privada, ao serviço de transcrição (a liberação do destino privado do lado dele é a pendência externa `whisper-api#2`).

#### Parâmetros

| Nome | Em | Obrigatório | Descrição |
|---|---|---|---|
| `webhook-id` | header | sim | Identificador estável do evento; igual em todas as tentativas. Chave de deduplicação. |
| `webhook-timestamp` | header | sim | Horário Unix, em segundos, da tentativa atual; muda a cada reenvio. Tolerância de ±300 s. |

#### Corpo da requisição

Exemplo `concluida`:

```json
{
  "eventId": "evt_01J9M2YB8K4W6N7P3Q5R1S0ABC",
  "jobId": "trn_01J9M2YB8K4W6N7P3Q5R1S0ABC",
  "status": "completed",
  "clientReference": "5b1f7e0a-3c2d-4a98-8e65-1d0c9b7a6f42"
}
```

Exemplo `falhou`:

```json
{
  "eventId": "evt_01J9M2YB8K4W6N7P3Q5R1S0ABD",
  "jobId": "trn_01J9M2YB8K4W6N7P3Q5R1S0ABD",
  "status": "failed",
  "clientReference": "8c4d2b6e-9f10-4e37-a5b8-7c6d5e4f3a21"
}
```

#### Respostas

| Código | Quando | `code` |
|---|---|---|
| 204 | Evento aceito: novo, repetido, de job desconhecido ou de tentativa já encerrada. | — |
| 400 | Corpo que não é JSON ou sem `eventId`, `jobId` ou `status`. O corpo de erro não ecoa o evento. | `INVALID_REQUEST` |
| 401 | Assinatura ausente, inválida ou fora da tolerância de horário; nada muda. O corpo de erro não ecoa o evento. | `SIGNATURE_INVALID` |
| 503 | Falha transitória de `media` antes de gravar o evento; o serviço de transcrição reenvia. | `TEMPORARILY_UNAVAILABLE` |

---

## Schemas

### TranscriptionEvent

Aviso mínimo de estado terminal; sem mídia, texto nem motivo da falha. Campos desconhecidos são tolerados (leitor tolerante).

| Campo | Tipo | Obrigatório | Descrição |
|---|---|---|---|
| `eventId` | string | sim | Identificador estável entre as tentativas; igual a `webhook-id`. |
| `jobId` | string | sim | Identificador opaco do trabalho no serviço de transcrição. |
| `status` | enum: `completed`, `failed` | sim | Desfecho terminal. O motivo da falha é obtido por consulta ao serviço, não deste aviso. |
| `clientReference` | string | não | Referência enviada por `media` ao criar o pedido (id da tentativa de transcrição). Ausente se o serviço não a devolver. |

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
