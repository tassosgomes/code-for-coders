---
tsg_artifact: contract
product: code-4-coders
capability: CAP-006
version: 1.0
status: approved
updated: 2026-10-11
sources: tasks/prd-tags-video/prd.md@1.0, domains/entrega-de-midia-e-protecao/domain.md@1.0, domains/conteudo-e-curriculo/domain.md@1.1, contracts/bff-admin/openapi-midia.yaml@1.1.1, contracts/media/openapi-internal.yaml@1.1.0, contracts/media/asyncapi.yaml@1.3.0
---

# Contratos de integração — tags de vídeo

> - PRD: [prd.md](prd.md), v1.0, aprovado em 2026-10-11
> - Data da revisão: 2026-10-11
> - Estado do conjunto: **Aprovado para implementação (1.0)** em 2026-10-11. Todas as decisões foram
>   aprovadas pelo responsável e os dois documentos foram validados sem erros.

Este conjunto registra o acordo para a terceira fatia de `CAP-006`. Os contratos vigentes da
fronteira estão em `contracts/` e são o ponto de partida: este conjunto só os evolui. Promoção
aditiva por task e consolidação no fim do PRD, conforme `contracts/README.md`.

## Seleção e escopo

| Integração identificada | Modalidade | Justificativa |
|---|---|---|
| `admin-spa` → `bff-admin` (tela Vídeos, envio, seletor da aula) | OpenAPI | Tags no envio, edição, sugestão e filtro (RF-01 a RF-06) |
| `bff-admin` → `media` | OpenAPI | `media` é dono da Mídia e das tags; o BFF repassa 1:1 |
| Fatos `midia.*` (`media` → `learning`) | Não aplicável | A tag não é fato de negócio para outro domínio (RN-T01). `learning` mantém a própria visão de vídeos prontos só para o vínculo (RN-C04), e o seletor filtra pela listagem do BFF |
| Tags como produto de dados | Não aplicável | Banco interno de `media`; nenhum consumidor de dados |

| Documento | Padrão e versão | Versão do contrato | Fronteira | Estado |
|---|---|---|---|---|
| [api-contract.yaml](api-contract.yaml) e [.md](api-contract.md) | OpenAPI 3.1.0 | `bff-admin` vídeos 1.1.1 → 1.2.0 | Recorte: `createVideoUpload`, `completeVideoUpload`, `listVideos`, `getVideo` e as novas `replaceVideoTags` e `listVideoTags` | **Aprovado para implementação** em 2026-10-11; lint sem erros, 1 aviso registrado |
| [internal-api-contract.yaml](internal-api-contract.yaml) e [.md](internal-api-contract.md) | OpenAPI 3.1.0 | `media` interno 1.1.0 → 1.2.0 | As mesmas seis operações `*Internal`; reprodução fora | **Aprovado para implementação** em 2026-10-11; lint sem erros, 1 aviso registrado |

## Participantes e interfaces

| Identificador | Provedor | Consumidores | Comportamento | PRD |
|---|---|---|---|---|
| `createVideoUpload` / `*Internal` | `bff-admin` → `media` | `admin-spa` (envio) | `tags` opcional, validado antes de transferir bytes; na retomada valem as do envio pendente | RF-01; RN-T02, RN-T03 |
| `completeVideoUpload` / `*Internal` | idem | `admin-spa` | O vídeo nasce com as tags do envio, na grafia canônica vigente | RF-01 |
| `listVideos` / `*Internal` | idem | `admin-spa`: tela Vídeos e seletor da aula (`status=ready`) | `tag` (até 10) e `tagMatch` `all`/`any`, combinados com `status` e `q`; `tags` em cada vídeo | RF-04, RF-05, RF-06; RN-C04 |
| `getVideo` / `*Internal` | idem | `admin-spa` | `tags` na resposta | RF-02 |
| `replaceVideoTags` / `*Internal` · **nova** | idem | `admin-spa` (tela Vídeos) | `PUT` do conjunto inteiro; a última gravação prevalece; 404 para outra escola | RF-02; RN-T04, RN-T05, RN-T06 |
| `listVideoTags` / `*Internal` · **nova** | idem | `admin-spa` (envio, edição, filtros) | Até 20 tags da escola que contêm `q`; as que começam por `q` primeiro | RF-03; RN-T05, RN-T06 |

## Origem e decisões

| Entrada consultada | Versão e data | Decisão herdada |
|---|---|---|
| [PRD](prd.md) | v1.0, 2026-10-11 | RF-01 a RF-06; RN-T01 a RN-T06; DP-01 a DP-06 |
| `contracts/bff-admin/openapi-midia.yaml` | 1.1.1 (`main` em 2026-10-11) | Cookie, CSRF, `Idempotency-Key`, Problem com `code` e `traceId`, `_page`/`_size`, 404 igual para outra escola |
| `contracts/media/openapi-internal.yaml` | 1.1.0 (`main` em 2026-10-11) | Espelho 1:1 do BFF, JWT de ator interno, `TOKEN_INVALID` |
| `contracts/media/asyncapi.yaml` | 1.3.0 | Os fatos não carregam título nem dados de exibição; as tags seguem a mesma linha |
| Código do `admin-spa` (`features/videos/api`, `features/course-authoring/api/get-ready-videos.ts`) | `main` em 2026-10-11 | Base da análise de compatibilidade (abaixo) |

Decisões deste contrato:

| ID | Decisão | Alternativa descartada | Estado |
|---|---|---|---|
| C-01 | **A tag é uma string na grafia canônica, sem identificador próprio.** Filtro e edição usam o texto; a equivalência é por forma normalizada (RN-T02) | Objeto `{tagId, name}`: só faz sentido com renomear, que está fora da fatia (DP-04); pode entrar depois de forma aditiva | **Aprovada em 2026-10-11** |
| C-02 | **Edição por `PUT /videos/{videoId}/tags`, com o conjunto inteiro; `[]` remove todas.** `updateVideoTitle` não muda | Ampliar o `PATCH` do título: mudaria o significado e a obrigatoriedade de uma operação vigente | **Aprovada em 2026-10-11** |
| C-03 | **Concorrência: a última gravação prevalece por inteiro**; a resposta traz o estado gravado. Sem `ETag`/`If-Match` | `If-Match` com 412: nada se perderia em silêncio, mas o SPA precisaria de um fluxo de recarga para um dado de baixo risco | **Aprovada em 2026-10-11** |
| C-04 | **Sugestão em `GET /video-tags`, teto fixo de 20, sem paginação.** Ordem: as que começam por `q`, depois as que só contêm `q`, cada grupo alfabético | Paginar: a sugestão nunca precisa da página 2; o professor refina digitando | **Aprovada em 2026-10-11** |
| C-05 | **Na retomada do envio valem as tags do envio pendente**, como já vale para o título; as do novo pedido são ignoradas | Substituir pelas do novo pedido: a retomada passaria a alterar o envio, o que hoje não acontece com nenhum campo | **Aprovada em 2026-10-11** |
| C-06 | **O servidor normaliza e junta as tags equivalentes sem erro.** Os limites de negócio respondem 422 (`TAG_INVALID`, `TAGS_LIMIT_EXCEEDED`); os limites do schema (50 itens, 100 caracteres) são só de proteção (400) | Recusar duplicata com 422: penaliza "docker" e "Docker" no mesmo pedido, que o PRD trata como a mesma tag | **Aprovada em 2026-10-11** |
| C-07 | **Filtro: `tag` repetível (até 10) e `tagMatch` `all` (padrão) ou `any`.** Tag desconhecida não casa com nenhum vídeo, sem erro | Recusar tag desconhecida: o filtro de uma tag que acabou de sumir (RN-T05) quebraria a tela | **Aprovada em 2026-10-11** |
| C-08 | **A edição de tags exige `Idempotency-Key` e `X-CSRF-Token`**, como `updateVideoTitle` | Dispensar `Idempotency-Key` por o `PUT` já ser idempotente: quebraria a convenção de escrita do BFF | **Aprovada em 2026-10-11** |

## Evolução e compatibilidade

- **Campos novos em respostas: `Video.tags` e `VideoUpload.tags`.** Estão sempre presentes, como
  `[]` quando vazios. Os schemas declaram `additionalProperties: false`.
  - **Não são aditivos para o `admin-spa` atual.** `features/videos/api/get-videos.ts`,
    `create-video-upload.ts` e `get-pending-video-uploads.ts` validam a resposta com zod
    `.strict()`, que recusa campo desconhecido. Uma aba aberta com o SPA antigo deixaria de
    carregar a tela Vídeos depois que o backend passasse a enviar `tags`.
  - **Transição acordada:** o `admin-spa` passa a aceitar `tags` na mesma entrega e implanta
    junto com `media` e `bff-admin`. Uma aba antiga precisa ser recarregada; não há dados nem
    usuários externos afetados (sem dados legados até o MVP).
  - `features/course-authoring/api/get-ready-videos.ts` (seletor) não é estrito e ignora o campo
    até ser atualizado.
- **`bff-admin` → `media`:** o BFF é o único cliente da API interna e evolui junto. Se ele mapear a
  resposta de `media` para um tipo próprio, precisa incluir `tags`, senão o campo some na resposta
  ao SPA. Isso é detalhe da TechSpec.
- **Pedido de envio com `tags`:** o campo é opcional, então clientes antigos continuam válidos.
- **Parâmetros `tag` e `tagMatch`:** opcionais. Sem eles a listagem se comporta como na 1.1.1.
- **Operações novas** (`replaceVideoTags`, `listVideoTags`) não afetam quem não as chama.
- **Fatos `midia.*` e reprodução:** não mudam.
- Compatibilidade com o que está em produção não foi verificada: o sistema ainda não está em
  homologação nem em produção.

## Validação e verificação

| Documento | Comando e ferramenta | Padrão/ruleset | Resultado |
|---|---|---|---|
| [api-contract.yaml](api-contract.yaml) | `npx --yes @stoplight/spectral-cli lint tasks/prd-tags-video/api-contract.yaml --ruleset .claude/skills/tsg-flow-contract-creator/rulesets/openapi.yaml --fail-severity=error` (Spectral 6.17.0) | OpenAPI 3.1.0, ruleset local | 0 erros; 1 aviso `tsg-collection-pagination-params` em `/video-tags`, que é a exceção de teto fixo (C-04) |
| [internal-api-contract.yaml](internal-api-contract.yaml) | mesmo comando | OpenAPI 3.1.0, ruleset local | 0 erros; o mesmo aviso, pelo mesmo motivo. Os componentes de reprodução que sobraram do vigente foram removidos do recorte |

O lint verifica a estrutura, não o comportamento. A implementação deve verificar:

- envio com `["docker", "Docker ", "DOCKER"]` → o vídeo tem uma tag só; com 11 tags distintas → 422
  `TAGS_LIMIT_EXCEEDED`, antes de qualquer URL de parte; com tag de 31 caracteres ou só de espaços
  → 422 `TAG_INVALID`;
- retomada com outras tags → 200 com as tags do envio pendente;
- tag "introducao" quando a escola já tem "Introdução" → o vídeo recebe "Introdução";
- `listVideos` com A(`docker`, `módulo 2`), B(`docker`) e C(`módulo 2`): `all` traz A; `any` traz
  A, B e C; tag desconhecida com `all` → `data: []`; com `status=ready`, vídeo em preparação nunca
  aparece;
- `replaceVideoTags` com `[]` e a tag usada só nesse vídeo → a tag some de `listVideoTags`;
  vídeo de outra escola → 404 `VIDEO_NOT_FOUND`; duas gravações seguidas → vale a segunda inteira;
- `listVideoTags` nunca devolve tag de outra escola, nem mais de 20;
- nenhuma mensagem `midia.*` é publicada por edição de tags;
- o `admin-spa` aceita `tags` em `Video` e `VideoUpload`.

## Pendências e handoff

- Próximo passo: `tsg-flow-techspec-creator` com este `contracts.md`,
  [api-contract.yaml](api-contract.yaml) e [internal-api-contract.yaml](internal-api-contract.yaml).
  A TechSpec decide armazenamento e normalização das tags em `media`, o repasse no `bff-admin` e a
  ordem de implantação descrita em Evolução.
