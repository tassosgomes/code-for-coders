# API Contract — Tags de vídeo (`admin-spa` → `bff-admin`)

> **Gerado a partir de:** [`api-contract.yaml`](api-contract.yaml) 1.2.0, PRD [`prd.md`](prd.md) v1.0
> **Data:** 2026-10-11
> **Status:** Aprovado
> **Versão do contrato:** `bff-admin` vídeos 1.1.1 → 1.2.0 (recorte)

Este documento deriva do YAML. Em caso de divergência, vale o YAML. O recorte traz as operações
cujo comportamento muda e as novas. `listPendingVideoUploads`, `getVideoUpload` e
`updateVideoTitle` ficam de fora e passam a devolver `tags`, porque `VideoUpload` e `Video` ganham
o campo. `createVideoUploadPartUrls` não muda.

---

## Premissas e Decisões

| Decisão | Escolha | Motivo |
|---|---|---|
| Autenticação | Cookie `staff_session` + `midia.enviar`; escrita com `X-CSRF-Token` e `Idempotency-Key` | Herdado de 1.1.1 |
| Representação da tag | String na grafia canônica da escola, sem identificador (C-01) | Sem renomear nesta fatia; a equivalência é por forma normalizada (RN-T02) |
| Edição | `PUT /videos/{videoId}/tags` troca o conjunto inteiro (C-02) | `updateVideoTitle` continua como está; `[]` remove todas |
| Concorrência | A última gravação prevalece por inteiro (C-03) | Aprovada pelo responsável; tag não afeta acesso nem publicação |
| Sugestão | `GET /video-tags?q=`, até 20, sem paginação (C-04) | Coleção de teto fixo; o Spectral emite o aviso `tsg-collection-pagination-params` |
| Filtro | `tag` repetível (até 10) + `tagMatch=all\|any`, `all` por padrão (C-07) | RF-04 a RF-06 |
| Versionamento | `servers: /api/v1` | Herdado |

---

## Resumo de Endpoints

| Método | Path | Descrição | Mudança | Status possíveis |
|---|---|---|---|---|
| `POST` | `/api/v1/video-uploads` | Iniciar ou retomar envio | `tags` opcional no pedido e na resposta | 200, 201, 400, 401, 403, 422, 502, 504 |
| `POST` | `/api/v1/video-uploads/{uploadId}/complete` | Concluir envio | O vídeo nasce com as tags do envio | 201, 400, 401, 403, 404, 422, 502, 504 |
| `GET` | `/api/v1/videos` | Listar vídeos | Filtros `tag` e `tagMatch`; `tags` em cada vídeo | 200, 400, 401, 403, 502, 504 |
| `GET` | `/api/v1/videos/{videoId}` | Consultar vídeo | `tags` na resposta | 200, 401, 403, 404, 502, 504 |
| `PUT` | `/api/v1/videos/{videoId}/tags` | Substituir as tags | **Nova** | 200, 400, 401, 403, 404, 422, 502, 504 |
| `GET` | `/api/v1/video-tags` | Sugerir tags da escola | **Nova** | 200, 400, 401, 403, 502, 504 |

---

## Endpoints Detalhados

### `POST /api/v1/video-uploads` — Iniciar ou retomar envio (RF-01)

**Consumido por:** formulário de envio da tela Vídeos.

As tags são validadas junto com formato e tamanho, antes de qualquer byte ser transferido, e
ficam guardadas no envio. **Na retomada (200) valem as tags do envio pendente, como o título**: as
tags do novo pedido são ignoradas (C-05).

```json
{
  "title": "Aula 3 — Injeção de dependência",
  "tags": ["docker", "Módulo 2"],
  "fileName": "aula-3-injecao-de-dependencia.mp4",
  "fileSize": 3221225472,
  "contentType": "video/mp4",
  "fingerprint": "6c1f0b9e2d7a4e53a1c8f4d2b9e07a31"
}
```

A resposta `VideoUpload` traz `"tags": ["Docker", "Módulo 2"]` já na grafia canônica.

| HTTP | code | Quando |
|---|---|---|
| 422 | `TAG_INVALID` | Tag vazia ou com mais de 30 caracteres, depois da normalização |
| 422 | `TAGS_LIMIT_EXCEEDED` | Mais de 10 tags distintas depois de juntar as equivalentes |
| 400 | `INVALID_REQUEST` | Acima dos limites de proteção (50 itens, 100 caracteres por item) |

Os demais erros são os da 1.1.1.

### `POST /api/v1/video-uploads/{uploadId}/complete` — Concluir envio (RF-01)

O vídeo nasce com as tags do envio, resolvidas de novo para a grafia canônica que vale na escola no
momento da conclusão. Fora isso, nada muda.

### `GET /api/v1/videos` — Listar vídeos (RF-04, RF-05, RF-06)

**Consumido por:** tela Vídeos e seletor de vídeo da aula (`status=ready`).

| Parâmetro | Tipo | Default | Descrição |
|---|---|---|---|
| `status` | enum, repetível | todos | Sem mudança |
| `q` | string | — | Trecho do título; sem mudança |
| `tag` | string, repetível, até 10 | — | Tag da escola, comparada sem maiúsculas, acentos nem espaços extras. Valor desconhecido não casa com nenhum vídeo |
| `tagMatch` | `all` \| `any` | `all` | E / OU entre as tags; sem efeito com menos de duas |
| `_page`, `_size` | integer | 1, 10 | Sem mudança |

Os critérios `status`, `q` e o conjunto de tags valem todos ao mesmo tempo. Exemplo:
`GET /api/v1/videos?status=ready&tag=docker&tag=m%C3%B3dulo%202&tagMatch=any`.

```json
{
  "data": [
    {
      "videoId": "0b1c2d3e-4f50-4a61-8b72-9c8d7e6f5a41",
      "title": "Aula 3 — Injeção de dependência",
      "status": "ready",
      "uploadedBy": { "accountId": "7a8b9c0d-1e2f-4a3b-9c4d-5e6f7a8b9c0d", "name": "Marina Alves" },
      "uploadedAt": "2026-09-26T09:40:00Z",
      "durationSeconds": 2415,
      "failureReason": null,
      "tags": ["Docker", "Módulo 2"]
    }
  ],
  "pagination": { "page": 1, "size": 10, "total": 1, "totalPages": 1 }
}
```

### `GET /api/v1/videos/{videoId}` — Consultar vídeo

Inclui `tags`. Nenhuma outra mudança.

### `PUT /api/v1/videos/{videoId}/tags` — Substituir as tags (RF-02) · **nova**

**Consumido por:** edição de tags na tela Vídeos.

Qualquer ator com `midia.enviar` pode editar as tags de qualquer vídeo da escola, em qualquer
estado. A chamada troca o conjunto inteiro, e `[]` remove todas. Ela não muda título, estado nem
vínculos, e não publica fato `midia.*`. Se duas pessoas gravarem quase ao mesmo tempo, a última
gravação vale por inteiro, e a resposta traz o estado gravado. Uma tag que deixa de estar em todos
os vídeos some da sugestão e do filtro. Vídeo de outra escola responde o mesmo 404 de vídeo
inexistente.

Headers: `Idempotency-Key`, `X-CSRF-Token`.

```json
{ "tags": ["kubernetes"] }
```

Response 200: o `Video` completo, com `"tags": ["Kubernetes"]`.

| HTTP | code | Quando |
|---|---|---|
| 403 | `PERMISSION_DENIED` / `CSRF_INVALID` | Sem `midia.enviar`, ou prova CSRF inválida |
| 404 | `VIDEO_NOT_FOUND` | Vídeo inexistente ou de outra escola |
| 422 | `TAG_INVALID`, `TAGS_LIMIT_EXCEEDED` | Ver envio |
| 422 | `IDEMPOTENCY_KEY_REUSED` | Mesma chave com outro corpo |

### `GET /api/v1/video-tags` — Sugerir tags da escola (RF-03) · **nova**

**Consumido por:** campo de tags no envio e na edição; opções do filtro na tela Vídeos e no seletor.

Devolve as tags usadas em pelo menos um vídeo da escola, em qualquer estado, que contêm `q` (sem
diferenciar maiúsculas nem acentos). Primeiro vêm as que começam por `q`, depois as demais, cada
grupo em ordem alfabética, até 20. Sem `q`, devolve as primeiras 20 em ordem alfabética.

```json
{ "data": ["Docker", "Docker Compose"] }
```

---

## Schemas de Entidades

### `Video` e `VideoUpload` (alterados)

| Campo | Tipo | Obrigatório | Descrição |
|---|---|---|---|
| `tags` | `Tag[]`, até 10, únicos | ✅ (`[]` quando nenhuma) | Em ordem alfabética, sem diferenciar maiúsculas nem acentos |

### `Tag`

String de 1 a 30 caracteres, na grafia canônica da escola (a da primeira vez que a tag foi usada
nela).

### Entrada de tags (`TagInputList`)

A entrada aceita até 50 itens de até 100 caracteres (limites de proteção). O servidor tira os
espaços das pontas, reduz espaços repetidos a um, junta as tags equivalentes sem dar erro e aplica
a grafia canônica. Só então valida os limites de negócio, que são 1 a 30 caracteres por tag e até
10 tags distintas.

---

## Códigos de Erro novos

| HTTP | code | Descrição |
|---|---|---|
| 422 | `TAG_INVALID` | Cada tag precisa ter de 1 a 30 caracteres. |
| 422 | `TAGS_LIMIT_EXCEEDED` | Um vídeo pode ter no máximo 10 tags. |

Os demais seguem a 1.1.1 (RFC 9457, `application/problem+json`, `code` e `traceId`).

---

## Como usar este contrato

- **Frontend:** os schemas zod `.strict()` de `Video` e `VideoUpload` precisam aceitar `tags` na
  mesma entrega em que o backend passa a enviar o campo (ver `contracts.md`, Evolução).
- **Testes de contrato:** leem `contracts/bff-admin/openapi-midia.yaml` depois da promoção aditiva
  das operações implementadas, nunca este arquivo.
