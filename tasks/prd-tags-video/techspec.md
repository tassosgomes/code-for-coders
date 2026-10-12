---
tsg_artifact: techspec
product: code-4-coders
capability: CAP-006
version: 1.0
status: approved
updated: 2026-10-11
sources: tasks/prd-tags-video/prd.md@1.0, tasks/prd-tags-video/contracts.md@1.0, context/architecture-baseline.md@1.2, domains/entrega-de-midia-e-protecao/domain.md@1.1, domains/conteudo-e-curriculo/domain.md@1.1
---

# Especificação Técnica — Tags de vídeo

> **Escopo:** Full-stack (`media`, `bff-admin`, `admin-spa`)
> **Modo:** Pipeline + API-First
> **PRD de origem:** [`prd.md`](prd.md) v1.0
> **Contratos de integração:** [`contracts.md`](contracts.md) 1.0, [`api-contract.yaml`](api-contract.yaml) 1.2.0 e [`internal-api-contract.yaml`](internal-api-contract.yaml) 1.2.0
> **Data:** 2026-10-11
> **Status:** Aprovado (2026-10-11)
> **Handoff:** approved — pode alimentar o Task Creator

## Resumo Executivo

- **Onde mora:** as tags pertencem à Mídia, em `media` (RN-T01). O `bff-admin` repassa o contrato
  1:1 e o `admin-spa` ganha o campo de tags no envio e na edição, além do filtro na tela Vídeos e
  no seletor da aula. `learning` e os fatos `midia.*` não mudam.
- **Persistência:** duas tabelas novas no schema de `media`.
  - **Catálogo de tags da escola:** uma linha por tag, única por escola e forma normalizada, com a
    grafia canônica.
  - **Vínculo vídeo–tag.**
  - O envio pendente guarda as tags informadas numa coluna própria até a conclusão.
- **Escrita das tags:** troca do conjunto inteiro (`replaceVideoTags`), numa transação que trava a
  linha do vídeo. Assim, a última gravação vale por inteiro (C-03) sem tocar a linha de `videos`.
- **Leitura:** listagem, consulta, título e conclusão passam a devolver `tags`. O filtro `all`/`any`
  é resolvido em SQL sobre o vínculo, e a sugestão consulta o catálogo restrito a tags com pelo
  menos um vídeo (RN-T05).
- **Trade-off primário:** o catálogo separado garante a grafia canônica única mesmo com dois
  professores criando a mesma tag ao mesmo tempo. Em troca, mantém linhas de tag sem vídeo (são
  invisíveis, mas guardam a grafia) e exige uma junção a mais na listagem.

## Arquitetura da Solução

```
admin-spa ──cookie+CSRF──▶ bff-admin ──JWT ator (aud media)──▶ media ──▶ PostgreSQL media_access
  envio / tela Vídeos /          repassa 1:1,                   tags, video_tags,
  seletor da aula                acrescenta tags nos DTOs        video_uploads.tags
```

### Bloco Backend — `media`

**Domínio.**

- **Tag como conceito da escola.** Duas formas derivadas do texto informado:
  - **Grafia de exibição:** tira os espaços das pontas e reduz espaços repetidos a um.
  - **Forma normalizada:** a de exibição em minúsculas e sem acentos. Usa a mesma dobra de
    `Video.NormalizeTitle` (`Video.cs:232`), mais a redução de espaços.
- **Regras:** a grafia de exibição tem de 1 a 30 caracteres, sem caracteres de controle. Fora disso
  → `TAG_INVALID`.
- **Conjunto de tags:** é normalizado no domínio. Junta as tags equivalentes pela forma normalizada
  (fica a primeira ocorrência), aplica `TAG_INVALID` a cada uma e, depois de juntar,
  `TAGS_LIMIT_EXCEEDED` acima de 10. A mesma regra vale no envio e na substituição.
- **Ordem de saída:** alfabética pela forma normalizada, com a grafia de exibição como desempate.

**Persistência (schema `media_access`).**

| Estrutura | Conteúdo | Restrições |
|---|---|---|
| Catálogo de tags | id, escola, grafia canônica, forma normalizada, criada em | Única por (escola, forma normalizada); filtro de tenant como as demais entidades (`MediaDbContext.cs:35`) |
| Vínculo vídeo–tag | vídeo, tag, escola | PK (vídeo, tag); FK para vídeo e tag; índice por (escola, tag) para o filtro |
| `video_uploads.tags` | Grafias informadas no envio, já normalizadas | Lista de texto, vazia por padrão; envios existentes ficam com lista vazia |

A migration é gerada pela ferramenta do EF Core do projeto (`dotnet ef migrations add`), nunca
escrita à mão.

**Resolução para a grafia canônica.** Uma operação de repositório recebe o conjunto já normalizado
e garante cada tag no catálogo da escola: `INSERT … ON CONFLICT (escola, forma normalizada) DO
NOTHING`, seguido da leitura das linhas. A grafia que fica é a de quem inseriu primeiro. É esse
passo que torna a escolha da grafia segura quando dois professores criam a mesma tag ao mesmo tempo
(RN-T02). Ele é usado em três pontos:

- no início do envio, para devolver `VideoUpload.tags` já na grafia canônica (C-05);
- na conclusão, de novo, dentro da transação que cria o vídeo, porque uma tag pode ter surgido entre
  o início e a conclusão;
- na substituição.

**Casos de uso.**

| Caso de uso | Mudança |
|---|---|
| `CreateVideoUpload` | Recebe `tags` (ausente = `[]`), normaliza e valida **antes** de iniciar o multipart no armazenamento (a validação de formato e tamanho já acontece antes, em `VideoUpload.Create`). Resolve a grafia canônica e grava no envio. O hash de idempotência inclui `tags`. Na retomada, a saída usa as tags do envio pendente e ignora as do pedido (C-05) |
| `CompleteVideoUpload` | Em `VideoUploadRepository.CompleteAsync`, depois de `dbContext.Videos.Add(video)` (`VideoUploadRepository.cs:200`) e na mesma transação: resolve as tags do envio e cria os vínculos. Conclusão repetida devolve o vídeo com as tags que ele tem hoje |
| `ReplaceVideoTags` (novo) | Valida a chave de idempotência e o conjunto. Numa transação: `SELECT … FROM videos … FOR UPDATE` (o mesmo padrão de `VideoUploadRepository.cs:168`), 404 `VIDEO_NOT_FOUND` se a linha não existe na escola, resolve as tags, apaga os vínculos do vídeo e insere os novos. Grava o registro de idempotência `replaceVideoTags` e devolve o vídeo com as tags gravadas. Não altera a linha de `videos` e não grava outbox |
| `ListVideos` | Novos filtros `tags` (normalizados) e `tagMatch`. Valor que não está no catálogo da escola não casa: com `all`, o resultado é vazio; com `any`, ele só não contribui. As tags da página vêm numa segunda consulta pelos ids da página |
| `GetVideo`, `UpdateVideoTitle` | Saída com `tags` |
| `ListVideoTags` (novo) | Tags do catálogo da escola com pelo menos um vínculo, cuja forma normalizada contém a de `q`. Ordena primeiro as que começam por `q` e depois alfabeticamente; no máximo 20. Sem `q`, as 20 primeiras em ordem alfabética |

**Filtro em SQL.** `all`: o vídeo tem, entre seus vínculos, tantas tags distintas do filtro quanto
o filtro tem. `any`: existe um vínculo com tag do filtro. Os dois se combinam com os filtros de
estado e título já existentes (`VideoQueries.cs:9`) e com a mesma ordenação e paginação.

**Saídas.** `VideoOutput` e `VideoUploadOutput` ganham `Tags` (lista, nunca nula).

**Idempotência.** O registro guarda a resposta serializada (`CreateVideoUpload.cs:201`,
`UpdateVideoTitle.cs:57`). Uma resposta gravada antes desta entrega não tem `tags`, e o replay
precisa devolver `[]`, não `null`.

**Telemetria.** A preparação, o outbox e os sinais de custo não mudam. Contadores novos no
`MediaTelemetry` existente, para as métricas do PRD:

| Instrumento | Atributos | Mede |
|---|---|---|
| `media.video_tags.assigned` | `origin` = `existing` \| `new` | Convergência do vocabulário (tag já existia na escola ou nasceu agora) |
| `media.videos.listed` | `tag_filter` = `none` \| `all` \| `any`; `ready_only` = bool | Uso do filtro; o seletor é aproximado por `ready_only=true` com filtro de tag |

Nenhum atributo leva o texto da tag, o título ou a escola (cardinalidade e G23).

### Bloco Backend — `bff-admin`

- `IVideoLibraryClient` e `VideoLibraryClient`:
  - repassam `tag` (repetido) e `tagMatch` em `ListVideosAsync` (`VideoLibraryClient.cs:22`);
  - ganham `ReplaceVideoTagsAsync` (`PUT internal/v1/videos/{id}/tags`, com `Idempotency-Key`) e
    `ListVideoTagsAsync`.
- `VideoResponse` e `VideoUploadResponse` (`ApiModels/VideoResponses.cs:5`,
  `ApiModels/VideoUploadResponses.cs:20`) ganham `Tags`. Sem isso o campo some na desserialização
  do BFF.
- `CreateVideoUploadRequest` e `CreateVideoUploadInternalRequest` ganham `Tags` opcional.
- Novas rotas `PUT /api/v1/videos/{videoId}/tags` e `GET /api/v1/video-tags` em
  `VideoLibraryEndpoints`, com o mesmo `GetMediaAccessAsync` (`VideoLibraryEndpoints.cs:112`).
  Validam no BFF:
  - `Idempotency-Key`;
  - os limites de proteção do contrato (`tag` ≤ 10 valores de até 100 caracteres, `q` de 1 a 100,
    `tagMatch` ∈ {`all`, `any`}).

  Os limites de negócio ficam com `media`. O CSRF do `PUT` já é coberto pelo middleware
  (`BffSecurityMiddleware.cs:194`).

### Bloco Frontend — `admin-spa`

| Jornada | Tela | Estado |
|---|---|---|
| Rotular no envio | `video-upload-dialog.tsx` | Tags no formulário (cliente) até o envio; depois, as do servidor |
| Ver e editar tags | `videos-area-screen.tsx` + diálogo de edição de tags | Servidor (React Query); o diálogo guarda o conjunto em edição |
| Filtrar a tela Vídeos | `videos-area-route.tsx` | **URL**: `tag` repetido e `tagMatch`, junto de `status`, `q` e `page` (`videos-area-route.tsx:59`) |
| Filtrar o seletor da aula | `course-video-picker.tsx` | Estado local do seletor, como a busca por título hoje (`course-video-picker.tsx:15`) |

- **Campo de tags com sugestão.** É um componente compartilhado pelo envio, pela edição e pelos dois
  filtros, seguindo o padrão WAI-ARIA *combobox* com lista de sugestões e etiquetas removíveis.
  - Enter ou vírgula confirma a tag digitada. A sugestão vem de `listVideoTags` com o trecho
    digitado, com espera curta entre teclas.
  - **No envio e na edição:** aceita texto livre e aplica no cliente os mesmos limites do servidor,
    para dar a mensagem antes de enviar. O servidor continua sendo a autoridade.
  - **Nos filtros:** só aceita uma sugestão.
- **E/OU.** O controle só aparece com duas ou mais tags no filtro e tem `all` como padrão (RF-06).
  Na tela Vídeos, ele entra na URL.
- **Schemas zod.** Os estritos de `Video` e `VideoUpload` (`get-videos.ts:8`,
  `create-video-upload.ts:5`, `get-pending-video-uploads.ts`, `get-video-upload.ts`) passam a
  exigir `tags`. O seletor (`get-ready-videos.ts:6`) passa a ler `tags` para exibi-las.
- **Mensagens de lista vazia.** Hoje são montadas por combinação de estado e título
  (`videos-area-screen.tsx:79`) e passam a considerar as tags. No seletor, a mensagem "Nenhum vídeo
  pronto atende aos filtros", com a ação de limpar os filtros.
- **Nenhuma dependência nova.** O combobox usa os primitivos que o projeto já tem.

## Mapa de Fatias Verticais

### V-01: Professor edita as tags de um vídeo na tela Vídeos e as vê na lista

- **Cobre:** RF-02; RN-T02, RN-T03, RN-T04, RN-T05 (a escrita), RN-T06; C-01, C-02, C-03, C-06, C-08;
  e a transição de compatibilidade de `contracts.md`.
- **Entrada / gatilho:** o professor abre a edição de tags de um vídeo na tela Vídeos, digita as
  tags (texto livre, ainda sem sugestão) e salva.
- **Processamento:**
  - `media`: migration das duas tabelas e da coluna do envio; domínio de tag e de conjunto;
    `ReplaceVideoTags` com trava da linha do vídeo; `tags` em todas as saídas de vídeo e envio
    (envios e vídeos existentes devolvem `[]`); replay de idempotência antigo devolve `[]`.
  - `bff-admin`: `Tags` nos DTOs e a rota `PUT`.
  - `admin-spa`: schemas aceitam `tags`; as tags aparecem como etiquetas em cada linha; diálogo de
    edição com a validação de 1 a 30 caracteres e até 10 tags; mensagens de 422 por `code`.
- **Saída observável:** a linha do vídeo mostra as tags gravadas, na grafia canônica e em ordem
  alfabética. Remover todas deixa a linha sem etiquetas.
- **Evidência / checkpoint:**
  - Integração de `media` (Testcontainers):
    - "docker", " Docker " e "DOCKER" → uma tag;
    - 11 distintas → `TAGS_LIMIT_EXCEEDED`;
    - 31 caracteres ou só espaços → `TAG_INVALID`;
    - vídeo de outra escola → 404;
    - duas substituições concorrentes no mesmo vídeo → o resultado é exatamente o conjunto de uma
      delas, nunca a união;
    - substituição durante a preparação não quebra `MarkPreparing`/`MarkReady`;
    - replay com a mesma chave → mesma resposta; mesma chave com outro corpo →
      `IDEMPOTENCY_KEY_REUSED`.
  - E2E do `bff-admin`: `PUT` sem CSRF → 403 `CSRF_INVALID`; sem `midia.enviar` → 403.
  - `admin-spa` (Vitest + MSW): editar, salvar e ver as etiquetas; 422 mostra a mensagem.
  - Em dev: editar as tags de um vídeo real e recarregar a tela.
- **Bloqueado por:** EN-01.

### V-02: Ao digitar uma tag, o professor vê as tags que a escola já usa

- **Cobre:** RF-03; RN-T02, RN-T05, RN-T06; C-04.
- **Entrada / gatilho:** digitar no campo de tags do diálogo de edição.
- **Processamento:** `ListVideoTags` em `media` com o filtro de "tem vínculo", a ordem e o teto de
  20. Rota `GET /api/v1/video-tags` no BFF. O campo de tags vira o combobox com sugestões; confirmar
  "introducao" grava "Introdução" quando ela existe.
- **Saída observável:** a lista de sugestões, e a etiqueta confirmada na grafia canônica.
- **Evidência / checkpoint:**
  - Integração de `media`:
    - "dock" sugere "Docker" antes de "Docker Compose" e antes de "Moby Docker";
    - tag cujo último vídeo perdeu a tag não é sugerida;
    - tag de outra escola nunca aparece;
    - no máximo 20.
  - `admin-spa`: navegação só por teclado (setas, Enter, Esc) e anúncio da lista.
- **Bloqueado por:** V-01.

### V-03: O professor informa tags ao enviar um vídeo

- **Cobre:** RF-01; RN-T02, RN-T03; C-05, C-06.
- **Entrada / gatilho:** o formulário de envio com o campo de tags (o combobox de V-02).
- **Processamento:**
  - `CreateVideoUpload`: valida as tags antes do multipart, resolve e grava no envio, e o hash de
    idempotência inclui as tags.
  - Retomada: devolve as tags do pendente.
  - `CompleteAsync`: resolve de novo e cria os vínculos na mesma transação.
  - O BFF repassa `tags`.
- **Saída observável:** o vídeo aparece na tela Vídeos com as tags informadas desde o estado
  `received`.
- **Evidência / checkpoint:**
  - Integração de `media`:
    - envio com tags inválidas → 422 e nenhum multipart iniciado no armazenamento (dublê de
      `IMediaStoragePort`);
    - retomada com outras tags → 200 com as tags originais;
    - conclusão repetida → um vídeo, mesmas tags;
    - tag criada por outro vídeo entre o início e a conclusão do envio, com outra grafia → o vídeo
      fica com a grafia canônica.
  - Em dev: enviar um vídeo curto com tags e ver as etiquetas na lista.
- **Bloqueado por:** V-02.

### V-04: O professor filtra a tela Vídeos por tags, com E ou OU

- **Cobre:** RF-04, RF-06; RN-T05, RN-T06; C-07.
- **Entrada / gatilho:** escolher tags no filtro da tela Vídeos e, com duas ou mais, alternar
  E/OU.
- **Processamento:** `ListVideos` com `tags`/`tagMatch` em `media`; repasse no BFF; estado na
  URL; mensagens de lista vazia; a atualização automática enquanto há vídeo em preparação mantém o
  filtro.
- **Saída observável:** a lista filtrada, e a URL com `tag=…&tagMatch=…`, que pode ser compartilhada
  e sobrevive ao recarregamento.
- **Evidência / checkpoint:**
  - Integração de `media` com A(`docker`, `módulo 2`), B(`docker`) e C(`módulo 2`):
    - `all` → A;
    - `any` → A, B e C;
    - tag desconhecida com `all` → vazio, e com `any` junto de "docker" → A e B;
    - combinação com `status=failed` e `q`;
    - paginação com o total correto.
  - `admin-spa`: a escolha aparece só com duas tags ou mais; remover até sobrar uma tag esconde a
    escolha.
- **Bloqueado por:** V-02.

### V-05: O professor filtra o seletor de vídeo da aula por tags

- **Cobre:** RF-05, RF-06 (no seletor); RN-C04, RN-T01.
- **Entrada / gatilho:** na tela de aula da Autoria, abrir o seletor de vídeo e escolher tags.
- **Processamento:** `get-ready-videos.ts` envia `tag`/`tagMatch` com `status=ready`; o seletor
  ganha o combobox de filtro, a escolha E/OU e as etiquetas por vídeo. O vínculo (`updateLesson`)
  não muda.
- **Saída observável:** só aparecem vídeos prontos com as tags pedidas. A mensagem de vazio oferece
  limpar os filtros.
- **Evidência / checkpoint:**
  - `admin-spa`: vídeo em preparação com a tag nunca aparece (MSW reproduz o filtro do servidor);
    vincular um vídeo achado pelo filtro segue o fluxo atual.
  - Em dev: montar uma aula filtrando por tag.
- **Bloqueado por:** V-04.

### Habilitadores inevitáveis

| Habilitador | Por que não cabe numa fatia | Menor escopo | Primeira fatia desbloqueada |
|---|---|---|---|
| EN-01 — Design: wireframe ASCII e Figma aprovados | O projeto exige o design aprovado antes do código de tela. Três telas mudam juntas (envio, tela Vídeos e seletor da aula) e precisam de um componente único de tags | Estender `docs/design/wireframes-videos.md` (envio, linha com etiquetas, diálogo de tags, filtro E/OU) e `docs/design/wireframes-autoria-curso.md` (seletor) e o Figma correspondente | V-01 |

## Contratos e Fronteiras

### Mapeamento do contrato de API

| operationId (BFF / interno) | Caminho de implementação |
|---|---|
| `createVideoUpload` / `createVideoUploadInternal` | `VideoUploadEndpoints` (BFF) → `VideoUploadClient` → `VideoUploadEndpoints` (media) → `CreateVideoUpload` → resolução de tags → `VideoUploadRepository` |
| `completeVideoUpload` / `*Internal` | … → `CompleteVideoUpload` → `VideoUploadRepository.CompleteAsync` (vínculos na transação) |
| `listVideos` / `*Internal` | `VideoLibraryEndpoints.ListVideosAsync` → `VideoLibraryClient` → `VideoEndpoints.ListVideosAsync` → `ListVideos` → `VideoQueries.ListAsync` + tags da página |
| `getVideo` / `*Internal` | … → `GetVideo` + tags do vídeo |
| `replaceVideoTags` / `*Internal` (novo) | `VideoLibraryEndpoints` → `VideoLibraryClient.ReplaceVideoTagsAsync` → `VideoEndpoints` → `ReplaceVideoTags` |
| `listVideoTags` / `*Internal` (novo) | `VideoLibraryEndpoints` → `VideoLibraryClient.ListVideoTagsAsync` → `VideoEndpoints` → `ListVideoTags` |

**Validações além do contrato:**

| operationId | Regra | Camada |
|---|---|---|
| `createVideoUpload`, `replaceVideoTags` | Normalização, junção das equivalentes, 1 a 30 caracteres, até 10 (RN-T02, RN-T03) | domain (`media`) |
| `createVideoUpload` | Tags validadas antes de iniciar o multipart | application (`media`) |
| `replaceVideoTags` | Trava da linha do vídeo; a última gravação prevalece por inteiro | infra.data (`media`) |
| `listVideos`, `listVideoTags` | Tag "existe" só com vínculo (RN-T05); escola pelo filtro de tenant (RN-T06) | infra.data (`media`) |

**Erros → resposta HTTP** (só os novos):

| Condição | HTTP | `code` |
|---|---|---|
| Tag vazia, com mais de 30 caracteres ou com caractere de controle, depois da normalização | 422 | `TAG_INVALID` |
| Mais de 10 tags distintas | 422 | `TAGS_LIMIT_EXCEEDED` |
| Acima dos limites de proteção (50 itens, 100 caracteres; `tag` > 10; `tagMatch` inválido) | 400 | `INVALID_REQUEST` |

O `bff-admin` repassa os 422 de `media` com o mesmo `code`, como já faz com `TITLE_REQUIRED`.

### Mapeamento de jornada

| História (PRD) | Tela / componente | operationId | Evidência |
|---|---|---|---|
| Informar tags ao enviar | Diálogo de envio | `listVideoTags`, `createVideoUpload`, `completeVideoUpload` | V-03 |
| Editar tags depois | Tela Vídeos, diálogo de tags | `replaceVideoTags` | V-01 |
| Ver as tags da escola ao digitar | Combobox de tags | `listVideoTags` | V-02 |
| Filtrar o seletor da aula | `course-video-picker` | `listVideos` (`status=ready`) | V-05 |
| Escolher E ou OU | Filtros da tela Vídeos e do seletor | `listVideos` (`tagMatch`) | V-04, V-05 |
| Combinar tag, título e estado | Tela Vídeos | `listVideos` | V-04 |

### Entidades do domínio

| Entidade (domain doc / PRD) | Representação técnica | Local |
|---|---|---|
| Mídia | `Video` (sem coluna nova) | `src/media/src/CodeForCoders.Media.Domain/Entities/Video.cs` |
| Tag (RN-T01, RN-T02) | Catálogo de tags da escola | domínio e `Infra.Data` de `media` |
| Tags de uma Mídia | Vínculo vídeo–tag | `Infra.Data` de `media` |
| Envio | `VideoUpload` + lista de tags | `src/media/src/CodeForCoders.Media.Domain/Entities/VideoUpload.cs` |

## Arquivos a Modificar e a Referenciar

### A modificar

| Caminho | Fatia | Alteração |
|---|---|---|
| `src/media/src/CodeForCoders.Media.Domain/Entities/Video.cs` | V-01 | Expor a dobra de normalização para reuso pela tag (sem mudar a de título) |
| `src/media/src/CodeForCoders.Media.Domain/Entities/VideoUpload.cs` | V-03 | Tags do envio em `Create` |
| `src/media/src/CodeForCoders.Media.Infra.Data/MediaDbContext.cs` | V-01 | Conjuntos e filtro de tenant das entidades novas |
| `src/media/src/CodeForCoders.Media.Infra.Data/Videos/VideoUploadConfiguration.cs` | V-03 | Coluna das tags do envio |
| `src/media/src/CodeForCoders.Media.Infra.Data/Videos/VideoQueries.cs` | V-01, V-04 | Tags por vídeo; filtro `all`/`any` |
| `src/media/src/CodeForCoders.Media.Infra.Data/Videos/VideoUploadRepository.cs` | V-03 | Vínculos na transação de `CompleteAsync` |
| `src/media/src/CodeForCoders.Media.Application/UseCases/Videos/VideoOutput.cs` | V-01 | `Tags` |
| `src/media/src/CodeForCoders.Media.Application/UseCases/VideoUploads/VideoUploadOutputs.cs` | V-01 | `Tags` |
| `src/media/src/CodeForCoders.Media.Application/UseCases/Videos/ListVideos/ListVideos.cs`, `GetVideo/GetVideo.cs`, `UpdateVideoTitle/UpdateVideoTitle.cs` | V-01, V-04 | Saída com tags; filtros |
| `src/media/src/CodeForCoders.Media.Application/UseCases/VideoUploads/CreateVideoUpload/*`, `CompleteVideoUpload/*` | V-03 | Tags no envio e na conclusão |
| `src/media/src/CodeForCoders.Media.Application/Common/MediaTelemetry.cs` | V-01, V-04 | Os dois contadores |
| `src/media/src/CodeForCoders.Media.Api/Endpoints/VideoEndpoints.cs` | V-01, V-02, V-04 | Rotas novas; `tag`/`tagMatch` |
| `src/media/src/CodeForCoders.Media.Api/Endpoints/VideoUploadEndpoints.cs`, `ApiModels/VideoUploadRequests.cs` | V-03 | `Tags` no pedido |
| `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Endpoints/VideoLibraryEndpoints.cs` | V-01, V-02, V-04 | Rotas novas; repasse dos filtros |
| `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Clients/IVideoLibraryClient.cs`, `VideoLibraryClient.cs` | V-01, V-02, V-04 | Chamadas novas; filtros |
| `src/bff-admin/src/CodeForCoders.BffAdmin.Api/ApiModels/VideoResponses.cs`, `VideoUploadResponses.cs` | V-01, V-03 | `Tags` |
| `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Endpoints/VideoUploadEndpoints.cs`, `Clients/VideoUploadClient.cs` | V-03 | Repasse de `tags` |
| `src/bff-admin/tests/CodeForCoders.BffAdmin.EndToEndTests/VideoLibraryHandler.cs`, `VideoUploadHandler.cs` | V-01, V-03 | Dublê de `media` com tags e rotas novas |
| `src/admin-spa/src/features/videos/api/*.ts` | V-01, V-03, V-04 | Schemas com `tags`; chamadas novas; filtros |
| `src/admin-spa/src/features/videos/components/videos-area-screen.tsx`, `video-upload-dialog.tsx` | V-01, V-03, V-04 | Etiquetas, edição, campo no envio, filtro e mensagens |
| `src/admin-spa/src/app/routes/videos-area-route.tsx` | V-04 | `tag`/`tagMatch` na URL |
| `src/admin-spa/src/features/course-authoring/api/get-ready-videos.ts`, `components/course-video-picker.tsx` | V-05 | Filtro e etiquetas |
| `src/admin-spa/src/testing/handlers.ts`, `authoring-video-handlers.ts` | V-01 a V-05 | MSW com `tags`, filtro e rotas novas |
| `contracts/bff-admin/openapi-midia.yaml`, `contracts/media/openapi-internal.yaml` | cada fatia | Promoção aditiva do que a fatia implementa (`contracts/README.md`, regra 2) |
| `docs/design/wireframes-videos.md`, `docs/design/wireframes-autoria-curso.md` | EN-01 | Telas com tags |

### A referenciar (não alterar)

| Caminho | Por que consultar |
|---|---|
| `src/media/src/CodeForCoders.Media.Infra.Data/Videos/VideoConfiguration.cs:73` | `PreparationLeaseId` é token de concorrência: a edição de tags não pode gravar a linha de `videos` |
| `src/media/src/CodeForCoders.Media.Infra.Data/Videos/VideoUploadRepository.cs:167` | Padrão de transação com `FOR UPDATE` a reproduzir em `ReplaceVideoTags` |
| `src/media/src/CodeForCoders.Media.Application/UseCases/VideoUploads/VideoUploadUseCaseHelpers.cs` | Idempotência (hash, replay, `EnsureSameRequest`) |
| `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Security/BffSecurityMiddleware.cs:194` | CSRF já cobre `PUT` |
| `src/learning/src/CodeForCoders.Learning.Infra.Messaging/VideoAvailabilityFact.cs` | Confirma que `learning` não lê nada além dos fatos; nada muda ali |
| `contracts/README.md` | Regra de promoção aditiva e consolidação |

## Análise de Impacto

| Componente | Tipo | Impacto e risco | Ação requerida |
|---|---|---|---|
| Banco de `media` (`media_access`) | modificado | Duas tabelas e uma coluna novas; envios e vídeos existentes ficam sem tags | Migration via EF; `scripts/remote-infra.sh migrate` em dev |
| `admin-spa` ↔ `bff-admin` ↔ `media` | modificado | `tags` obrigatório nas respostas quebra os schemas zod estritos de uma aba antiga | Publicar os três juntos (V-01); recarregar a aba |
| `learning`, fatos `midia.*`, reprodução | sem mudança | Nenhum | — |
| Worker de preparação | sem mudança | A trava de linha em `ReplaceVideoTags` pode esperar alguns milissegundos por um `UPDATE` da preparação, e vice-versa | Transação curta, sem I/O externo dentro dela |
| Listagem de vídeos | modificado | Uma consulta a mais por página para as tags; filtro com junção | Índice (escola, tag) no vínculo; página máxima de 50 já existente |

## Riscos e Preocupações

| Preocupação | Local (`arquivo:linha`) | Impacto | Mitigação |
|---|---|---|---|
| Gravar tags na linha de `videos` colidiria com o token de concorrência da preparação | `src/media/src/CodeForCoders.Media.Infra.Data/Videos/VideoConfiguration.cs:73` | Edição de tags falhando ao acaso durante a preparação | Tags em tabela própria; `ReplaceVideoTags` não altera a linha de `videos` |
| Substituição concorrente por "apaga e insere" pode produzir a união dos dois conjuntos | — (desenho novo) | Viola C-03 | `SELECT … FOR UPDATE` na linha do vídeo antes de apagar; teste de concorrência em V-01 |
| Replay de idempotência gravado antes da entrega devolve `tags: null` | `src/media/src/CodeForCoders.Media.Application/UseCases/VideoUploads/VideoUploadUseCaseHelpers.cs:35` | Resposta fora do contrato; o SPA quebra o parse | Saídas tratam `null` como `[]` na desserialização; teste de replay de registro sem `tags` |
| Schemas zod estritos recusam o campo novo | `src/admin-spa/src/features/videos/api/get-videos.ts:19`, `create-video-upload.ts:14` | Tela Vídeos não carrega numa aba antiga | Mesmo deploy; V-01 entrega SPA e backend juntos |
| BFF desserializa em DTO próprio e descartaria `tags` | `src/bff-admin/src/CodeForCoders.BffAdmin.Api/ApiModels/VideoResponses.cs:5` | Campo some na resposta ao SPA | `Tags` nos DTOs em V-01; E2E do BFF confere o campo |
| O hash de idempotência do envio hoje não conhece `tags` | `src/media/src/CodeForCoders.Media.Application/UseCases/VideoUploads/CreateVideoUpload/CreateVideoUpload.cs:39` | Mesma chave com tags diferentes seria aceita como repetição | Incluir `tags` no input do hash (V-03) |
| Busca com `%` ou `_` no trecho | `src/media/src/CodeForCoders.Media.Infra.Data/Videos/VideoQueries.cs:20` | Padrão de `LIKE` indevido na sugestão | Usar o mesmo `Contains` do EF que a busca por título já usa (parâmetro escapado pelo provedor) e testar "dock%" |

## Decisões Técnicas

- **Decisão:** catálogo de tags por escola com unicidade na forma normalizada, mais um vínculo
  vídeo–tag.
  - **Racional:** grafia canônica garantida pelo banco, mesmo com criação concorrente (RN-T02);
    sugestão e filtro por junção indexada; a linha de `videos` não é tocada.
  - **Trade-offs:** linhas de tag sem vídeo ficam no catálogo. São invisíveis (RN-T05), mas guardam
    a grafia, de modo que uma tag recriada volta com a grafia original, coerente com "a da primeira
    vez que foi usada".
  - **Alternativas rejeitadas:**
    - Só o vínculo com a grafia em cada linha: dois professores criando "Docker" e "docker" ao
      mesmo tempo gravariam duas grafias.
    - Coluna de lista em `videos`: grava a linha com token de concorrência, e o filtro `all`/`any`
      e a sugestão ficariam sem índice simples.
- **Decisão:** a troca de tags trava a linha do vídeo (`FOR UPDATE`), sem `ETag`.
  - **Racional:** C-03 (a última vale por inteiro) exige serializar as trocas do mesmo vídeo; a
    trava não altera a linha.
  - **Trade-offs:** espera curta com a preparação.
  - **Alternativas rejeitadas:** `ETag`/`If-Match`, descartado no contrato (C-03).
- **Decisão:** o filtro da tela Vídeos fica na URL; o do seletor, em estado local.
  - **Racional:** segue o padrão que cada tela já usa para estado e título.
  - **Alternativas rejeitadas:** URL no seletor. Ele é um diálogo dentro da aula, e a busca por
    título dele já é local.

Nenhuma decisão aqui fixa convenção para outras features; nenhuma ADR nova.

## Verificação

- **Cenários críticos não óbvios:**
  - concorrência na troca de tags (V-01);
  - troca de tags durante a preparação (V-01);
  - tag nascida por outro vídeo entre o início e a conclusão de um envio (V-03);
  - replay de idempotência gravado sem `tags` (V-01);
  - `any` com uma tag desconhecida e uma conhecida (V-04);
  - sugestão com caracteres curinga (V-02).
- **Dados ou ambiente especiais:** os testes de integração de `media` usam o
  `MediaIntegrationFixture` existente (PostgreSQL em contêiner). Em dev, o servidor compartilhado
  com `scripts/remote-infra.sh provision && scripts/remote-infra.sh migrate` e
  `scripts/apps.sh start --remote`. A migration nova precisa do `migrate` antes do smoke.
- **Observabilidade além do padrão:** os dois contadores do Bloco Backend. O que não dá para medir
  pelo servidor está em Questões em Aberto.
- **Verificação dos contratos:**
  - os casos de "A implementação deve verificar" em `contracts.md`, distribuídos pelas fatias;
  - cada fatia promove a `contracts/` as operações que implementa e roda o Spectral nos arquivos
    tocados (`contracts/README.md`).

## Questões em Aberto

- [ ] **A métrica "uso do filtro no seletor" (PRD) é aproximada.** O servidor não distingue o
  seletor de um filtro "prontos" com tag na tela Vídeos. Medir de forma exata exige telemetria no
  `admin-spa`, que esta entrega não cria. — Responde: responsável pelo produto. — Impacto: a meta
  de 30% é lida pela aproximação `ready_only=true`.
- [ ] **A percentagem de vídeos prontos rotulados** sai de consulta ao banco de `media`, não de
  métrica contínua. — Responde: responsável pelo produto. — Impacto: leitura pontual em 30 dias.

## Architecture Decision Records

Esta entrega só aplica decisões existentes:

- [ADR-0005: Sessão do ator interno e autenticação de serviço do BFF do backoffice](../../docs/adr/0005-sessao-e-servico-do-backoffice.md) — JWT de ator com audiência `media` e `midia.enviar` nas rotas novas.
- [ADR-0006: Preparação de vídeo em worker próprio do media](../../docs/adr/0006-preparacao-de-video-e-custodia-de-chave.md) — o worker grava a linha de `videos` com lease; as tags ficam fora dela.
- [ADR-0019: Runtime e deploy atuais](../../docs/adr/0019-runtime-e-deploy-atual.md) — implantação conjunta de `admin-spa`, `bff-admin` e `media` (transição de V-01).
