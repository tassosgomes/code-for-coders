# API Contract — Tags de vídeo (`bff-admin` → `media`, interno)

> **Gerado a partir de:** [`internal-api-contract.yaml`](internal-api-contract.yaml) 1.2.0, PRD [`prd.md`](prd.md) v1.0
> **Data:** 2026-10-11
> **Status:** Aprovado
> **Versão do contrato:** `media` interno 1.1.0 → 1.2.0 (recorte)

Espelha 1:1 o recorte público [`api-contract.md`](api-contract.md): mesmos paths sob
`/internal/v1`, mesmos schemas, regras e códigos de erro. As diferenças são as de sempre desta
fronteira:

| Aspecto | Público (`bff-admin`) | Interno (`media`) |
|---|---|---|
| Autenticação | Cookie `staff_session` | `Authorization: Bearer` JWT de ator interno (`aud: media`), validado localmente; exige `midia.enviar` |
| CSRF | `X-CSRF-Token` na escrita | Não há |
| 401 | `SESSION_REQUIRED` | `TOKEN_INVALID` |
| Escola | Da sessão | `tenantId` do token; delimita tag, sugestão e filtro (RN-T06) |

| Operação interna | Mudança |
|---|---|
| `createVideoUploadInternal` | `tags` opcional; na retomada valem as do envio pendente |
| `completeVideoUploadInternal` | O vídeo nasce com as tags do envio |
| `listVideosInternal` | Filtros `tag` e `tagMatch`; `tags` em cada vídeo |
| `getVideoInternal` | `tags` na resposta |
| `replaceVideoTagsInternal` | **Nova**: `PUT /videos/{videoId}/tags` |
| `listVideoTagsInternal` | **Nova**: `GET /video-tags` |

`listPendingVideoUploadsInternal`, `getVideoUploadInternal` e `updateVideoTitleInternal` passam a
devolver `tags`, sem outra mudança. As operações de reprodução (`CAP-007`) não mudam e não estão
no recorte.
