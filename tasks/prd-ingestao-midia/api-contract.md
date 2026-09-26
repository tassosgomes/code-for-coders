# API HTTP — vídeos da escola no backoffice

> Derivado de [api-contract.yaml](api-contract.yaml), versão 1.1.0, OpenAPI 3.1.0. Recorte do PRD de `CAP-006` v1.0 (2026-09-25). Estado: Aprovado para implementação em 2026-09-25.

O SPA do backoffice usa o BFF do backoffice em `/api/v1`, com o cookie `staff_session` de `CAP-002`. **Uma exceção:** os bytes do vídeo vão do navegador direto ao armazenamento, por URLs de parte assinadas que o BFF entrega (C-11). Campos, respostas, erros e exemplos têm como fonte o YAML.

| Método | Rota | Operação | Sucesso | Segurança | PRD |
|---|---|---|---|---|---|
| `POST` | `/video-uploads` | `createVideoUpload` — Iniciar ou retomar envio | 201 / 200 | cookie + CSRF, `midia.enviar` | RF-02, RF-03 |
| `GET` | `/video-uploads` | `listPendingVideoUploads` — Envios pendentes do próprio ator | 200 | cookie, `midia.enviar` | RF-03 |
| `GET` | `/video-uploads/{uploadId}` | `getVideoUpload` — Partes já recebidas | 200 | cookie, `midia.enviar` | RF-03 |
| `POST` | `/video-uploads/{uploadId}/part-urls` | `createVideoUploadPartUrls` — URLs assinadas de parte | 200 | cookie + CSRF, `midia.enviar` | RF-02, RF-03 |
| `POST` | `/video-uploads/{uploadId}/complete` | `completeVideoUpload` — Concluir e criar o vídeo | 201 | cookie + CSRF, `midia.enviar` | RF-02 |
| `GET` | `/videos` | `listVideos` — Vídeos da escola | 200 | cookie, `midia.enviar` | RF-06, RF-08 |
| `GET` | `/videos/{videoId}` | `getVideo` — Consultar um vídeo | 200 | cookie, `midia.enviar` | RF-08 |
| `PATCH` | `/videos/{videoId}` | `updateVideoTitle` — Corrigir título | 200 | cookie + CSRF, `midia.enviar` | RF-07 |

## Fluxo de envio

1. `POST /video-uploads` com título, nome, tamanho, tipo e `fingerprint`. Tamanho acima de 5 GiB, ou tipo/extensão fora de MP4, MOV e MKV, é recusado **antes** de qualquer byte (`FILE_TOO_LARGE`, `FORMAT_NOT_SUPPORTED`). A resposta traz `partSize`, `partCount` e `receivedParts`.
2. `POST /video-uploads/{uploadId}/part-urls` com até 100 números de parte. Para cada URL, o SPA faz `PUT` dos bytes da parte **direto na URL**, sem cookie. A URL só escreve aquela parte e expira em `expiresAt`.
3. `POST /video-uploads/{uploadId}/complete`. Media confere no armazenamento que todas as partes chegaram e cria o vídeo em `received`. Faltando parte, responde `UPLOAD_INCOMPLETE`. Repetir a conclusão devolve o mesmo vídeo.

**Retomada (RF-03).** Ao escolher o arquivo de novo, o SPA repete o passo 1 com a mesma `fingerprint`. Se houver envio pendente do mesmo ator, a resposta é 200 com o envio existente e as partes já recebidas; o SPA pede URLs só das que faltam. `GET /video-uploads` lista os pendentes do próprio ator, para a tela avisar. O envio pode ser retomado até `expiresAt`, pelo menos 24 horas depois da validade da última URL emitida. Depois disso, `UPLOAD_NOT_FOUND`, e o envio recomeça do zero.

## Regras de integração

- **Permissão:** toda operação exige `midia.enviar`, que é do papel professor (DP-05). O BFF recusa com 403 `PERMISSION_DENIED`, e Media recusa de novo com o JWT do ator.
- **Biblioteca da escola:** `listVideos` devolve os vídeos de todos os autores da escola (DP-03). `uploadedBy.name` é o nome do autor no momento do envio. Nenhuma listagem cruza escola; vídeo de outra escola responde `VIDEO_NOT_FOUND`, igual a inexistente.
- **Envios pendentes são do ator:** envio de outro ator, expirado ou concluído responde sempre `UPLOAD_NOT_FOUND`.
- **Estado:** `received` → `preparing` → `ready` | `failed`; só anda para frente. `durationSeconds` só em `ready`, `failureReason` só em `failed`. O SPA traduz `failureReason` para o texto do PRD (RF-05) e consulta de novo a lista enquanto houver vídeo em `received` ou `preparing` (DP-04).
- **Seletor de vídeo da aula (RF-08):** `listVideos?status=ready` e `getVideo`. A resposta nunca traz endereço, chave nem nome de arquivo guardado. Em `CAP-005`, a tela de aula poderá exigir a permissão de autoria em vez de `midia.enviar` (C-12).
- **Título:** 1 a 200 caracteres; só espaços responde 422 `TITLE_REQUIRED`.
- **Escritas** exigem `Idempotency-Key` (24 horas) e `X-CSRF-Token`. Pedir URLs de parte não muda estado de negócio e dispensa a chave.
- **Listagens** paginam com `_page` e `_size` (máximo 50).
- **Erros** seguem RFC 9457 com `code` estável e `traceId`; nenhum erro carrega nome do autor, título ou URL de parte.

## Exemplo derivado do contrato

`POST /video-uploads/3f6d2c1a-8b7e-4f90-a1b2-c3d4e5f60718/complete`, com as 48 partes enviadas, responde 201 com `status: received` e `Location: /api/v1/videos/0b1c2d3e-4f50-4a61-8b72-9c8d7e6f5a41`. Quando a preparação termina, `getVideo` passa a responder `status: ready` e `durationSeconds: 2415`, e Media publica `midia.ativo-pronto` ([asyncapi-contract.yaml](asyncapi-contract.yaml)).

## Handoff

A chamada BFF → Media está em [internal-api-contract.yaml](internal-api-contract.yaml), com o JWT de ator interno (ADR-0005). Tipos e mocks podem ser derivados de `api-contract.yaml`. A escrita direta das partes, a retomada, o descarte após 24 horas e a recusa de leitura exigem testes da implementação.
