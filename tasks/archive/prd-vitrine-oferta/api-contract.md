# API — `admin-spa` → `bff-admin`: área Catálogo

> Derivado de [api-contract.yaml](api-contract.yaml), versão 1.0.0, OpenAPI 3.1.0. PRD: [prd.md](prd.md) v1.0. Estado: Aprovado para implementação em 2026-09-30.

Operações novas do BFF do backoffice. Toda operação exige a sessão `staff_session` com `oferta.editar` (financeiro, OD47); escritas exigem `X-CSRF-Token` e `Idempotency-Key` (exceto `DELETE`, que só exige CSRF). Erros em `application/problem+json` com `code` estável.

## Premissas e decisões

| Decisão | Escolha | Motivo |
|---|---|---|
| Preço | `priceCents`, inteiro em centavos de real, 1 a 9 999 999 | Sem ponto flutuante; limites de DP-03 (C-05) |
| Vigência | `accessPeriod`: `{type: months, months: 1–60}` ou `{type: lifetime}` | OD51; mesma forma em HTTP e nos fatos |
| Ficha | Existe para todo curso publicado que o Catálogo conhece; não há "criar ficha" | Cursos chegam pelo fato de versão (C-01) |
| Estados da oferta | `draft`, `published`, `unpublished` | RN-O11: só `draft` pode ser excluída |
| Concorrência | Última gravação vence | Mesmo acordo da autoria (OD44 de CAP-005) |
| Paginação | `_page`/`_size` na lista de cursos; ofertas de um curso até 50 na ficha, sem paginar | Teto declarado em `maxItems` (C-06) |

## Resumo de endpoints

| Método | Path | Descrição | Status |
|---|---|---|---|
| `GET` | `/catalog/courses` | Cursos publicados, com nível, `inShowcase` e contagem de ofertas | 200, 400, 401, 403, 502, 504 |
| `GET` | `/catalog/courses/{courseId}` | Ficha: dados da versão vigente, chamada comercial, ofertas e cliques | 200, 401, 403, 404, 502, 504 |
| `PATCH` | `/catalog/courses/{courseId}` | Alterar a chamada comercial (≤ 160) | 200, 400, 401, 403, 404, 422, 502, 504 |
| `POST` | `/catalog/courses/{courseId}/offers` | Criar oferta em rascunho | 201, 400, 401, 403, 404, 422, 502, 504 |
| `PATCH` | `/catalog/offers/{offerId}` | Alterar nome, preço ou vigência | 200, 400, 401, 403, 404, 422, 502, 504 |
| `DELETE` | `/catalog/offers/{offerId}` | Excluir oferta em rascunho | 204, 401, 403, 404, 422, 502, 504 |
| `POST` | `/catalog/offers/{offerId}/publish` | Publicar (exige nível no curso) | 200, 401, 403, 404, 422, 502, 504 |
| `POST` | `/catalog/offers/{offerId}/unpublish` | Despublicar | 200, 401, 403, 404, 422, 502, 504 |

## Efeitos colaterais

| Ação | Fato | Ato na trilha |
|---|---|---|
| `publishOffer` | `catalogo.oferta-publicada.v1` | `oferta-publicada` |
| `updateOffer` em oferta publicada, com preço ou vigência mudando | `catalogo.oferta-alterada.v1` | `oferta-alterada`, com anterior e novo |
| `updateOffer` só de nome, ou em rascunho/despublicada | nenhum | nenhum (DP-05) |
| `unpublishOffer` | `catalogo.oferta-despublicada.v1` | `oferta-despublicada` |

Códigos de erro próprios: `CATALOG_COURSE_NOT_FOUND`, `OFFER_NOT_FOUND`, `FIELD_INVALID`, `COURSE_LEVEL_REQUIRED`, `OFFER_STATE_CONFLICT`, `IDEMPOTENCY_KEY_REUSED`, `COMMERCE_UNAVAILABLE`, `COMMERCE_TIMEOUT`.

Esquemas, respostas e exemplos têm como fonte o YAML.
