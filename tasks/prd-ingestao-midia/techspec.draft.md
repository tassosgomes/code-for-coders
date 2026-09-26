---
tsg_artifact: techspec
product: code-4-coders
capability: CAP-006
version: 1.0
status: in_review
updated: 2026-09-25
sources: tasks/prd-ingestao-midia/prd.md@1.0, tasks/prd-ingestao-midia/contracts.md@1.0
---

# TechSpec — ingestão e preparação de mídia protegida

> - **Escopo:** Full-stack (`media` API + worker, `bff-admin`, `admin-spa`, `identity`)
> - **Modo:** Pipeline, API-First
> - **PRD de origem:** [prd.md](prd.md), v1.0, aprovado em 2026-09-25
> - **Contratos:** [contracts.md](contracts.md) 1.0 — [OpenAPI SPA → BFF 1.1.0](api-contract.yaml), [OpenAPI BFF → Media 1.0.0](internal-api-contract.yaml), [AsyncAPI Media 1.0.0](asyncapi-contract.yaml)
> - **Data:** 2026-09-25
> - **Status:** Em Revisão
> - **Handoff:** draft — não gerar Tasks

## Resumo Executivo

O serviço `media` deixa de ser esqueleto e passa a receber, guardar e preparar vídeo. Ele se divide em
**dois papéis da mesma imagem** (ADR-0006, nova). A **API** emite URLs assinadas de parte, confere as
partes no armazenamento e cria o vídeo. O **worker** reivindica vídeos recebidos, roda ffprobe e ffmpeg,
grava a escada HLS cifrada no armazenamento, descarta o original e publica o fato pelo outbox. O
`bff-admin` ganha as oito operações do contrato, no mesmo padrão da área financeira de CAP-002: valida a
sessão em Identity pedindo JWT com `audience: media` e repassa. O `admin-spa` ganha a área **Vídeos**, com
envio em partes direto ao armazenamento, retomada e lista que se atualiza. Identity só acrescenta
`midia.enviar` ao papel professor e a audiência `media` (C-14).

**Decisões principais:**

- Bytes do navegador direto ao S3 em partes de 64 MiB por URL assinada de uma hora (C-11); Media nunca
  recebe o arquivo pela API e confere as partes com o próprio armazenamento, sem receber comprovante do
  cliente.
- Preparação em container `media-worker` separado, com fila no próprio estado do vídeo e lease renovada; uma
  chave AES-128 por vídeo, cifrada no banco do `media` por chave-mestra secreta (ADR-0006).
- Três tentativas para falha passageira; falha determinística do arquivo (ilegível, formato, duração) vai
  direto a `failed`.

**Trade-off primário:** o envio direto ao S3 e o worker separado tiram 5 GB e a CPU do ffmpeg do caminho
da API. O custo é a única exceção a "o SPA só fala com o BFF" (CORS no bucket, URL de escrita no navegador),
mais um container e um segredo novo (a chave-mestra), cuja perda inutiliza os vídeos preparados.

---

## Arquitetura da Solução

```text
admin-spa ──/api/v1/video-*──▶ bff-admin ──valida sessão (audience media)──▶ identity
 (/admin/videos)                  │
   │                              └──JWT aud=media──▶ media API (/internal/v1/video-*, /videos)
   │                                                     │  presign / ListParts / Complete
   └──PUT parte (URL assinada, sem cookie)──▶ S3 ◀───────┤
                                               ▲          │ Postgres media: videos, video_uploads, chaves cifradas, outbox
                                               │          │
                                  media-worker ┴── reivindica received ─ ffprobe/ffmpeg ─ grava hls/ ─ apaga original
                                               └──outbox──▶ media.events (midia.ativo-pronto.v1 | midia.preparacao-falhou.v1)
```

**URL pública (local / Compose):** a área de vídeos fica em `http://localhost:8081/admin/videos`, e o
nginx do SPA encaminha `/api/v1/` ao `bff-admin` (sem mudança no nginx). As URLs de parte apontam para o
endpoint **público** do armazenamento: `http://localhost:9000/...` no MinIO local, e o endpoint S3 regional
em produção. Nenhum link é emitido por e-mail.

### Bloco Backend

**Media — modelo** (entidades do domain doc; nenhuma noção de curso ou aula — RN-M02)

- **Envio (`VideoUpload`):** tenant, ator (`sub` do JWT), título, nome do arquivo, tamanho, tipo,
  fingerprint, `partSize`, `partCount`, o identificador do upload em várias partes do provedor (**nunca**
  exposto), chave do objeto original, `expiresAt`, situação (pendente / concluído / expirado). No máximo um
  pendente por (tenant, ator, fingerprint) — índice único parcial.
- **Vídeo (`Video`, a Mídia do domain doc):** tenant, `videoId`, título, título normalizado (minúsculas, sem
  acento — base da busca `q`), autor (`accountId` e nome retratado — C-13), momento do envio, `uploadId` de
  origem (único — é o que torna a conclusão idempotente), estado, motivo da falha, duração, tentativas,
  lease (`claimedBy`, `claimedUntil`), próximo momento elegível, bytes guardados.
- **Chave de cifra:** por vídeo, 16 bytes aleatórios, gravada **cifrada** (AES-GCM, chave-mestra
  `MediaKeyProtection`, com o identificador da chave-mestra ao lado) na mesma transação que leva o vídeo a
  `ready`. Nenhuma operação desta entrega a lê de volta, exceto a de testes de conformidade (RN-M08).
- **Layout de objetos** (RN-M01, G23): `{prefix}/{tenantId}/{videoId}/original` e
  `{prefix}/{tenantId}/{videoId}/hls/{master.m3u8 | 1080p/… | 720p/… | 480p/…}`. Nenhum título, nome ou
  e-mail em chave de objeto. Bucket privado; nenhum objeto com ACL pública.

**Media API — regras** (cada item é um commit; idempotência pelo mecanismo do serviço, janela de 24 h,
escopo tenant + operação + ator)

- *Autorização:* JWT de ator interno validado localmente pelo JWKS de Identity (emissor `identity`,
  audiência `media`), no mesmo desenho do `commerce` de CAP-002. Toda operação exige `midia.enviar`;
  ausente → 403 `PERMISSION_DENIED`; token inválido → 401 `TOKEN_INVALID`.
- *Iniciar envio* (`createVideoUploadInternal`): título só com espaços → `TITLE_REQUIRED`; tamanho acima de
  5 GiB → `FILE_TOO_LARGE`; tipo e extensão fora de {mp4 → `video/mp4`, mov → `video/quicktime`, mkv →
  `video/x-matroska`} → `FORMAT_NOT_SUPPORTED`. Se há pendente não expirado do mesmo ator com a mesma
  fingerprint → devolve esse (200). Senão, inicia o upload em várias partes no armazenamento e grava o envio
  (201). `partCount = ceil(fileSize / 64 MiB)`.
- *URLs de parte:* número fora de 1..`partCount` → `PART_OUT_OF_RANGE`; emite URLs de `PUT` de uma parte,
  validade de 60 minutos, assinadas com o cliente **público** do armazenamento; atualiza
  `expiresAt = validade da última URL + 24 h`. Não muda o estado de negócio.
- *Consultar envio:* `receivedParts` vem do próprio armazenamento (listagem de partes do upload), nunca de
  memória do servidor.
- *Concluir:* se o `uploadId` já gerou vídeo → devolve o mesmo vídeo (201, mesmo `videoId`). Senão lista as
  partes; faltando alguma, ou com tamanho divergente do esperado → `UPLOAD_INCOMPLETE`. Completa o upload no
  armazenamento com as partes listadas e, num commit, marca o envio como concluído e cria o vídeo
  `received`, com o autor do `sub` e o nome de `uploaderName`. A preparação não é disparada por mensagem: o
  worker encontra o vídeo.
- *Envio de outro ator, expirado ou concluído* → `UPLOAD_NOT_FOUND` em todas as operações de envio.
- *Listar / consultar / titular vídeos:* sempre filtrados pelo tenant do token (G07); vídeo de outro tenant
  → `VIDEO_NOT_FOUND`; ordenação por momento do envio, decrescente; `q` compara com o título normalizado.
  Troca de título só altera título e título normalizado.

**Media worker — preparação** (ADR-0006)

1. **Reivindicar:** um vídeo por vez por worker (`Preparation:MaxConcurrency`, padrão 1), com
   `FOR UPDATE SKIP LOCKED` entre os `received` elegíveis. Só reivindica se o disco de trabalho tem livre
   ≥ 3× o tamanho do original. Grava `preparing`, lease de 5 minutos e `attempts + 1`; renova a lease a cada
   minuto enquanto trabalha.
2. **Baixar e sondar:** baixa o original para o diretório de trabalho e roda ffprobe.
   - sonda falha ou não há faixa de vídeo → `unreadable-file`;
   - o codec da faixa de vídeo não é decodificável pelo ffmpeg da imagem → `unsupported-format`;
   - duração acima de 10.800 s → `duration-exceeded`.
   Essas três são **determinísticas**: vão direto a `failed`, sem nova tentativa.
3. **Preparar:** gera a chave e o arquivo de informação de chave no diretório de trabalho; um único
   processo ffmpeg produz as qualidades cabíveis (sem ampliar: altura do original ≥ 1080 → três; ≥ 720 →
   duas; menor → só 480p, ou a própria altura se abaixo de 480), H.264 + AAC, segmentos de 6 s, playlists de
   variante e mestre. A URI de chave gravada nas playlists é o marcador opaco do ADR-0006.
4. **Publicar os objetos:** envia a árvore `hls/` ao armazenamento (sobrescreve, então repetir é seguro).
5. **Concluir:** num commit, grava `ready`, duração, bytes guardados, a chave cifrada e o outbox
   `midia.ativo-pronto`. **Depois** do commit, apaga o original do armazenamento e limpa o diretório de
   trabalho (DP-06). Se o apagamento falhar, a varredura do passo 7 o refaz.
6. **Falhar:** erro não determinístico (armazenamento, processo encerrado, lease vencida) volta o vídeo a
   `received` com próximo momento elegível em +1 min e depois +5 min; na 3ª tentativa esgotada grava `failed`
   com `preparation-failed`. Toda ida a `failed` grava o outbox `midia.preparacao-falhou` no mesmo commit;
   depois apaga o original e qualquer objeto `hls/` parcial.
7. **Varreduras periódicas:** (a) envio pendente com `expiresAt` vencido → aborta o upload em várias partes
   no armazenamento e marca expirado (RF-03); (b) vídeo em estado final com original ainda presente → apaga;
   (c) lease vencida em `preparing` → trata como falha não determinística.

**Media — publicação:** o outbox existente, routing key `midia.ativo-pronto.v1` /
`midia.preparacao-falhou.v1`, exchange do serviço (`RabbitMq:Exchange`, `media.events`). `eventId` gerado
na transação da transição; toda republicação o repete. `correlationId` = `traceparent` guardado no vídeo na
conclusão do envio. Os dois papéis publicam o outbox; a reivindicação por `SKIP LOCKED` já impede
publicação dupla.

**Identity** (C-14, depois de CAP-002 integrada): `midia.enviar` entra no catálogo, no papel professor; a
audiência `media` passa a ser emitida para o `bff-admin`, com escopo `videos:write`. Nada mais muda em
Identity.

**BFF do backoffice:** oito endpoints que seguem `FinanceAreaEndpoints` de CAP-002. Recusam sem
`midia.enviar` na sessão validada, pedem a Identity o JWT com `audience: media` e repassam a Media com
`Idempotency-Key` e corpo intactos. Em `createVideoUpload`, acrescentam `uploaderName` com o `name` da
validação. Media indisponível → 502/504 `MEDIA_UNAVAILABLE`, sem expor detalhe.

### Bloco Frontend

- **Rota** `/videos` (`paths.videos`) no `admin-spa`, protegida pelo loader de sessão de CAP-002. A área
  **Vídeos** entra em `getStaffAreas` com a permissão `midia.enviar`. A posição e o nome finais dependem do
  wireframe (QP-02 do PRD).
- **Estado de servidor** (lista, envios pendentes, vídeo) no padrão de fetching do projeto. A lista consulta
  de novo a cada 10 s **somente enquanto** houver item em `received`/`preparing` e a aba estiver visível; a
  retomada de envio consulta `listPendingVideoUploads` ao abrir a área.
- **Estado de cliente:** a fila de partes do envio em curso (número, progresso, tentativas) vive no
  componente de envio, não em store global (G16). O arquivo nunca é lido inteiro para a memória: cada parte
  é um recorte do `File`.
- **Envio:** valida extensão, tipo e tamanho no cliente antes da primeira chamada (a mesma regra é refeita no
  servidor); calcula a fingerprint a partir de nome, tamanho e data de modificação; pede URLs em lotes de até
  100; envia até 3 partes em paralelo, cada uma com `PUT` sem credencial; repete a parte com falha de rede
  até 3 vezes; ao fim, conclui. Enquanto transfere, avisa antes de sair da página. Se `completeVideoUpload`
  responder `UPLOAD_INCOMPLETE`, reconsulta o envio e reenvia as partes que faltam.
- **Texto:** motivos de falha e avisos com o texto do PRD (RF-05, Experiência do Usuário); nenhuma menção a
  proteção contra cópia (RN-M15).

---

## Mapa de Fatias Verticais

### V-01: professor abre a área Vídeos; quem não tem a permissão é barrado nas três camadas

- **Cobre:** RF-01, RF-06 (lista vazia e escopo por tenant), RF-08 (`getVideo`), RN-M03, RN-M04, US-07, DP-03, DP-05, C-14
- **Entrada / gatilho:** professor entra no backoffice e abre `/admin/videos`; suporte chama `GET /api/v1/videos` direto; um JWT sem `midia.enviar` chama Media direto.
- **Processamento:** `midia.enviar` no catálogo do professor e audiência `media` em Identity; enums de
  `Permission` dos contratos de CAP-002 atualizados (1.1.0); Media valida JWT via JWKS e exige a permissão;
  tabela de vídeos com tenant; `listVideosInternal` e `getVideoInternal`; BFF com `listVideos` e `getVideo`;
  SPA com a área, a rota e o estado vazio.
- **Saída observável:** o professor vê o menu Vídeos e "nenhum vídeo ainda"; o suporte recebe 403
  `PERMISSION_DENIED` no BFF; Media responde 403 ao JWT sem a permissão e 401 ao token de audiência
  `commerce`; `getVideo` de id inexistente → 404 `VIDEO_NOT_FOUND`.
- **Evidência / checkpoint:** testes de integração de Media (JWT com e sem permissão, audiência errada,
  tenant cruzado); testes do BFF; teste de componente do menu por permissão; smoke no Compose com professor e
  suporte reais.
- **Bloqueado por:** EN-01, EN-02, integração de CAP-002 em `main`

### V-02: professor envia um vídeo e ele aparece na lista como recebido

- **Cobre:** RF-02, RF-06, RF-08, RN-M01 (original privado), RN-M04, RN-M15, US-01, US-06, C-11, C-13
- **Entrada / gatilho:** o professor escolhe um MP4 de teste, confirma o título e envia.
- **Processamento:** adaptador real de armazenamento (iniciar upload em várias partes, assinar parte com o
  cliente público, listar partes, completar, abortar); `createVideoUpload`, `createVideoUploadPartUrls`,
  `completeVideoUpload`, `getVideoUpload` em Media e no BFF; recusas antes do primeiro byte; conclusão
  idempotente; SPA com seleção, validação, progresso, envio em partes e conclusão.
- **Saída observável:** barra de progresso; ao fim, linha "recebido" com título, autor e data; o original
  existe no bucket sob `{tenant}/{videoId}/original` e não é legível sem assinatura; arquivo de 6 GB e `.avi`
  são recusados sem nenhuma URL emitida; repetir a conclusão devolve o mesmo `videoId`.
- **Evidência / checkpoint:** integração de Media com MinIO (Testcontainers): upload de 3 partes pela URL
  assinada, `UPLOAD_INCOMPLETE` com parte faltando, conclusão repetida; teste de componente do envio com
  handlers; smoke no Compose pelo navegador.
- **Bloqueado por:** V-01

### V-03: envio interrompido continua de onde parou; envio abandonado some em 24 h

- **Cobre:** RF-03, DP-02 (retomada), US-02
- **Entrada / gatilho:** o professor fecha a aba com 10 de 48 partes enviadas e volta; ou não volta.
- **Processamento:** retomada por fingerprint em `createVideoUpload`; `listPendingVideoUploads`;
  `receivedParts` vindo do armazenamento; varredura (a) do worker abortando uploads vencidos; SPA com o aviso
  de envio incompleto e a continuação só das partes faltantes.
- **Saída observável:** ao escolher o mesmo arquivo, o progresso começa em 10/48; outro arquivo cria envio
  novo; depois de `expiresAt`, o envio não aparece, as partes somem do bucket e escolher o arquivo recomeça do
  zero; o envio pendente de um professor não aparece para outro.
- **Evidência / checkpoint:** integração com MinIO e relógio controlado para a expiração; teste de
  componente da retomada; smoke no Compose interrompendo o envio.
- **Bloqueado por:** V-02

### V-04: vídeo recebido fica pronto em três qualidades cifradas, e a lista mostra isso sozinha

- **Cobre:** RF-04, RF-05 (`ready` final), RF-06 (atualização automática), RF-09 (`midia.ativo-pronto`), RF-10, RN-M01, RN-M05, RN-M07, RN-M08, DP-01, DP-04, DP-06, US-05
- **Entrada / gatilho:** vídeo `received` de V-02.
- **Processamento:** papel `worker` e container `media-worker`; ffmpeg na imagem; reivindicação com lease;
  ffprobe; escada sem ampliar; chave por vídeo cifrada no banco; publicação de `hls/`; commit com `ready`,
  duração, bytes e outbox; apagamento do original; varredura (b) e (c); SPA consultando de novo enquanto há
  vídeo em andamento.
- **Saída observável:** a linha passa a "em preparação" e depois a "pronto · 0:20" sem recarregar; o bucket
  tem `master.m3u8` e três variantes com segmentos cifrados e **não** tem o original; `midia.ativo-pronto.v1`
  chega à fila de teste com `durationSeconds` e sem título; um 720p gera só 720p e 480p; nenhum GET anônimo em
  original, segmento, playlist ou chave é atendido; a chave não aparece em log, mensagem nem objeto.
- **Evidência / checkpoint:** integração do worker com Postgres, MinIO e ffmpeg reais sobre vídeos sintéticos
  (1080p e 720p de 20 s, gerados por ffmpeg no teste); verificação de que os segmentos não tocam sem a chave e
  tocam com ela; dois workers sobre o mesmo vídeo → um só processa; worker morto → outro retoma após a lease;
  smoke no Compose com o navegador.
- **Bloqueado por:** V-02, EN-03

### V-05: vídeo com problema falha com motivo claro e não fica preso

- **Cobre:** RF-04 (duração e ilegível), RF-05, RF-09 (`midia.preparacao-falhou`), RN-M05, RN-M06, DP-06
- **Entrada / gatilho:** arquivo `.mp4` com bytes aleatórios; vídeo de 3h01 (gerado com baixa resolução);
  MOV com codec que o ffmpeg da imagem não decodifica; armazenamento indisponível durante a preparação.
- **Processamento:** classificação determinística do ffprobe; retentativa com espera de 1 e 5 min para
  falha passageira; `failed` com motivo e outbox; limpeza do original e de `hls/` parcial.
- **Saída observável:** a linha mostra "arquivo de vídeo ilegível", "duração acima de 3 horas" ou "formato
  de vídeo não suportado" sem código técnico; falha passageira se recupera sozinha; depois da 3ª tentativa,
  "não foi possível preparar este vídeo — envie novamente"; `midia.preparacao-falhou.v1` com `reason`; nenhum
  objeto do vídeo fica no bucket.
- **Evidência / checkpoint:** integração do worker com os arquivos de teste e com falha injetada no
  adaptador de armazenamento; teste de componente das mensagens.
- **Bloqueado por:** V-04

### V-06: professor corrige o título e encontra vídeos por estado e por nome

- **Cobre:** RF-06 (filtro e busca), RF-07, RN-M04
- **Entrada / gatilho:** o professor filtra por "falhou", busca "injecao" e renomeia um vídeo de colega.
- **Processamento:** `updateVideoTitle` em Media e no BFF; título normalizado; filtros `status` e `q`; SPA
  com filtro, busca e edição.
- **Saída observável:** só os falhados aparecem; "injecao" encontra "Injeção de dependência"; o título novo
  aparece para todos, e o estado não muda; título em branco → `TITLE_REQUIRED`.
- **Evidência / checkpoint:** integração de Media (busca sem acento, filtro repetível, tenant); teste de
  componente da edição.
- **Bloqueado por:** V-02

### V-07: volume guardado e vídeos por estado visíveis na telemetria

- **Cobre:** RF-11, RN-M17, métrica "vídeo preso" do PRD, D-06
- **Entrada / gatilho:** vídeos em vários estados na escola.
- **Processamento:** o worker emite `media.storage.used` (bytes, sem dimensão), `media.videos.count`
  (dimensão `status`) e `media.videos.stuck` (vídeos em `received`/`preparing` há mais de 4× a duração
  estimada ou, sem duração, há mais de 12 h), lidos do banco a cada 60 s.
- **Saída observável:** no coletor OTLP local, os três instrumentos com valores coerentes com o banco;
  nenhuma dimensão com id de tenant, vídeo ou autor.
- **Evidência / checkpoint:** teste de integração lendo os instrumentos por `MeterListener`; smoke no
  Compose conferindo no backend de métricas.
- **Bloqueado por:** V-04

### Habilitadores inevitáveis

| Habilitador | Por que não cabe numa fatia | Menor escopo | Primeira fatia desbloqueada |
|---|---|---|---|
| EN-01 | O ambiente local precisa de um S3 compatível acessível pelo navegador e pelos containers, com endereço público diferente do interno, antes de qualquer envio | MinIO no `docker-compose.yml` com bucket privado criado na subida e CORS para a origem do backoffice; `AwsMedia` ganha endpoint interno, endpoint público de assinatura e path-style; credenciais só locais | V-01 (a configuração sobe com o Media) |
| EN-02 | O fluxo de design do backoffice exige wireframe e Figma aprovados antes do código de tela (fluxo ASCII → Figma → aprovação) | Wireframe e Figma da área Vídeos: lista, envio, retomada, estados e falhas; decide QP-02 | V-01 (parte de tela) |
| EN-03 | O worker precisa existir como processo implantável antes de preparar o primeiro vídeo, e a imagem precisa de ffmpeg | Papel `worker` por configuração; container `media-worker` no Compose e no Coolify com volume de trabalho; ffmpeg na imagem final; ffmpeg no job de CI de Media | V-04 |

---

## Contratos e Fronteiras

### Mapeamento do contrato de API

| operationId (BFF → Media) | Caminho de implementação |
|---|---|
| `createVideoUpload` → `createVideoUploadInternal` | endpoint do BFF (acrescenta `uploaderName`) → caso de uso de iniciar/retomar envio → porta de armazenamento (iniciar upload em várias partes) + repositório de envio |
| `listPendingVideoUploads` → `…Internal` | caso de uso de listar pendentes do ator |
| `getVideoUpload` → `…Internal` | caso de uso de consultar envio → porta (listar partes) |
| `createVideoUploadPartUrls` → `…Internal` | caso de uso de emitir URLs → porta (assinar parte) |
| `completeVideoUpload` → `…Internal` | caso de uso de concluir → porta (listar e completar) + vídeo `received` |
| `listVideos`, `getVideo` → `…Internal` | consultas de vídeo por tenant |
| `updateVideoTitle` → `…Internal` | caso de uso de titular |

**Validações além do contrato:**

| operationId | Regra | Camada |
|---|---|---|
| `createVideoUploadInternal` | extensão de `fileName` coerente com `contentType`; retomada só do mesmo ator e tenant | application |
| `completeVideoUploadInternal` | todas as partes presentes e com tamanho esperado (a última pode ser menor) | application + porta |
| `createVideoUploadPartUrlsInternal` | envio pendente, do ator, não expirado | application |
| todas | `midia.enviar` no JWT, tenant do JWT em toda consulta | api + application |

**Exceção → resposta HTTP:**

| Situação | HTTP | code |
|---|---|---|
| Título em branco | 422 | `TITLE_REQUIRED` |
| Tamanho acima do limite | 422 | `FILE_TOO_LARGE` |
| Formato não aceito | 422 | `FORMAT_NOT_SUPPORTED` |
| Parte fora do intervalo | 422 | `PART_OUT_OF_RANGE` |
| Partes faltando | 422 | `UPLOAD_INCOMPLETE` |
| Envio inexistente, de outro ator, expirado ou concluído | 404 | `UPLOAD_NOT_FOUND` |
| Vídeo inexistente ou de outro tenant | 404 | `VIDEO_NOT_FOUND` |
| Chave de idempotência com outro corpo | 422 | `IDEMPOTENCY_KEY_REUSED` |
| Armazenamento indisponível na API | 503 | `STORAGE_UNAVAILABLE` (não previsto no contrato — ver Questões em Aberto) |

### Mapeamento de mensagens e dados

| Contrato e identificador | Aplicação/produtor e consumidores | Comportamento a implementar | Evidência |
|---|---|---|---|
| AsyncAPI `publicarAtivoPronto` / `AtivoPronto` | Media (send) pelo outbox; `learning` previsto em CAP-005 | Um fato por vídeo que chega a `ready`, na mesma transação; `eventId` fixo por transição | V-04: fila de teste recebe um fato; republicação forçada repete o `eventId` |
| AsyncAPI `publicarPreparacaoFalhou` / `PreparacaoFalhou` | Media (send) pelo outbox | Um fato por vídeo que chega a `failed`, com `reason` | V-05 |

**Dados sensíveis e credenciais transitórias:**

| Dado | Criado | Copiado / trafega | Persistido | Descartado | Proteção |
|---|---|---|---|---|---|
| Chave AES do vídeo | worker, na preparação | arquivo de informação de chave no diretório de trabalho; memória do ffmpeg | banco do `media`, cifrada (AES-GCM, chave-mestra) | arquivo apagado ao fim da preparação, com sucesso ou falha | nunca em objeto, log, span, mensagem, resposta; logging de parâmetros do EF desligado para a tabela de chaves (ver Riscos) |
| Chave-mestra | secret manager / variável de deploy | configuração do processo | nenhum lugar do repositório nem da imagem | — | validada na partida (tamanho), nunca logada |
| URL de parte assinada | Media API | BFF → SPA (corpo da resposta) → navegador → S3 | não persistida | expira em 60 min | não entra em log do BFF nem de Media; só escreve uma parte |
| Nome do autor | validação de sessão em Identity | BFF → Media (`uploaderName`) → respostas de lista | banco do `media` | com o vídeo | nunca em log, métrica, fato nem nome de objeto |
| Título | professor | SPA → BFF → Media | banco do `media` | com o vídeo | nunca em fato, métrica nem nome de objeto |

### Entidades do domínio

| Entidade do Domain Doc | Representação técnica | Local |
|---|---|---|
| Ativo Protegido (tipo vídeo) / Mídia | agregado `Video` e tabela de vídeos | serviço `media` |
| Versão de Reprodução | árvore `hls/` no armazenamento + estado `ready` e bytes no vídeo | `media` + S3 |
| Chave de cifra (atributo da Versão de Reprodução) | registro cifrado por vídeo | `media` |
| — (envio, termo do PRD) | `VideoUpload` | `media` |

### Interfaces entre fatias ou times

- **Marcador de URI de chave nas playlists (para CAP-007):** as playlists de variante gravam
  `#EXT-X-KEY:METHOD=AES-128,URI="c4c-key:{videoId}"`. Quem entregar a playlist ao aluno substitui o marcador
  pelo endpoint de chave da sessão (ADR-0006). Nenhuma playlist guardada é servida sem essa reescrita.
- **Chave cifrada (para CAP-007):** registro por vídeo com o identificador da chave-mestra; CAP-007 lê e
  decifra para servir à sessão.

---

## Arquivos a Modificar e a Referenciar

Os caminhos de `identity`, `bff-admin` e `admin-spa` existem depois que CAP-002 for integrada; os números de
linha foram lidos na branch `feature/acesso-interno` (`893b8be`).

### A modificar

| Caminho | Fatia | Alteração |
|---|---|---|
| `src/identity/src/CodeForCoders.Identity.Domain/Entities/StaffRoleCatalog.cs` | V-01 | `midia.enviar` no professor |
| `tasks/prd-acesso-interno/api-contract.yaml`, `internal-api-contract.yaml` | V-01 | `midia.enviar` no enum `Permission`, versão 1.1.0 (C-14) |
| `docker-compose.yml`, `docker-compose.coolify.yml` | EN-01, V-01, EN-03 | MinIO local; `StaffSessionTokens__AudienceScopes__media` em Identity; configuração de JWT, armazenamento e chave-mestra em Media; `Media__BaseAddress` no BFF; serviço `media-worker` com volume de trabalho |
| `src/media/Dockerfile` | EN-03 | ffmpeg na imagem final |
| `.github/workflows/media.yml` | EN-03 | ffmpeg disponível para os testes de integração do worker |
| `Directory.Packages.props` | V-02 | SDK de S3 da AWS |
| `src/media/src/CodeForCoders.Media.Application/Interfaces/IMediaStoragePort.cs` | V-02 | porta passa a cobrir upload em várias partes, assinatura de parte, listagem, conclusão, abortar, baixar, enviar árvore e apagar; `IMediaCdnPort` sai do uso nesta entrega |
| `src/media/src/CodeForCoders.Media.Infra.Data/Adapters/S3MediaStorageAdapter.cs` | V-02 | adaptador real, com cliente interno e cliente público de assinatura |
| `src/media/src/CodeForCoders.Media.Infra.Data/Configuration/AwsMediaOptions.cs` | EN-01 | endpoint interno, endpoint público, path-style |
| `src/media/src/CodeForCoders.Media.Infra.Data/DependencyInjection.cs` | V-01, V-02 | registro do cliente de armazenamento; logging sensível do EF (ver Riscos) |
| `src/media/src/CodeForCoders.Media.Api/Extensions/ServiceConfigurationExtensions.cs`, `Program.cs` | V-01, EN-03 | autenticação JWT via JWKS; papel `api`/`worker` escolhendo endpoints e hosted services |
| `src/media/src/CodeForCoders.Media.Api/appsettings.json` | V-01, EN-01 | seções de token, armazenamento, preparação e proteção de chave |
| `src/media/src/CodeForCoders.Media.Infra.Messaging/DependencyInjection.cs` | EN-03 | hosted services por papel |
| `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Extensions/ServiceConfigurationExtensions.cs`, `EndpointExtensions.cs`, `appsettings.json` | V-01 | cliente de Media e mapeamento dos endpoints de vídeo |
| `src/admin-spa/src/config/paths.ts`, `features/staff-session/utils/get-staff-areas.ts`, `app/router.tsx`, `testing/handlers.ts` | V-01 | rota e área Vídeos |
| `docs/adr/index.md` | — | ADR-0006 |

### A referenciar (não alterar)

| Caminho | Por que consultar |
|---|---|
| `src/commerce/src/CodeForCoders.Commerce.Api/Security/FinanceAreaJwksConfigurationManager.cs`, `FinanceAreaJwtBearerOptionsSetup.cs` (branch de CAP-002) | desenho de validação de JWT via JWKS de Identity a repetir em Media |
| `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Endpoints/FinanceAreaEndpoints.cs:21-60` (branch de CAP-002) | padrão BFF: checar permissão na sessão, pedir JWT com audiência, repassar, mapear indisponibilidade |
| `src/identity/src/CodeForCoders.Identity.Api/Security/StaffSessionTokenIssuer.cs:14-29` (branch de CAP-002) | audiência só é emitida se estiver em `AudienceScopes` |
| `src/media/src/CodeForCoders.Media.Infra.Messaging/OutboxPublisherWorker.cs:71-74` | reivindicação do outbox por `SKIP LOCKED` — seguro com dois papéis publicando |
| `src/media/src/CodeForCoders.Media.Infra.Data/Outbox/OutboxMessage.cs` | routing key e `TraceParent` por mensagem |
| `domains/entrega-de-midia-e-protecao/domain.md` | RN-M01 a RN-M08, RN-M17 |

---

## Análise de Impacto

| Componente | Tipo | Impacto e risco | Ação requerida |
|---|---|---|---|
| `media` | modificado | de esqueleto a serviço com escrita no S3 e JWT; novo segredo (chave-mestra) | configuração por ambiente; segredo no secret manager |
| `media-worker` | novo (mesma imagem) | CPU e disco em rajada; precisa de volume de trabalho | serviço no Compose e no Coolify |
| Bucket S3 de mídia | modificado | passa a receber escrita direta do navegador | CORS só da origem do backoffice para `PUT`; bucket privado; regra de ciclo de vida abortando uploads incompletos após 3 dias (rede de segurança da varredura) — plataforma |
| Identity | modificado | catálogo e audiência; contratos de CAP-002 1.1.0 | só após CAP-002 integrada; ordem de implantação: Identity antes do BFF expor a área |
| `bff-admin` / `admin-spa` | modificados | área nova no menu para professor | nenhuma mudança nas áreas existentes |
| `learning` | nenhum agora | será consumidor dos fatos em CAP-005 | — |

---

## Riscos e Preocupações

| Preocupação | Local (`arquivo:linha`) | Impacto | Mitigação |
|---|---|---|---|
| A porta atual devolve URL de entrega **não assinada** para qualquer objeto | `src/media/src/CodeForCoders.Media.Application/Interfaces/IMediaStoragePort.cs:16-18`, `Adapters/CloudFrontMediaCdnAdapter.cs:13-21` | usar esse método exporia vídeo sem sessão (G21) | a porta é refeita em V-02 sem `DeliveryUri`; `IMediaCdnPort` não é usado nesta entrega e é redesenhado com assinatura em CAP-007 |
| O recibo de armazenamento carrega `s3://bucket/...` | `Adapters/S3MediaStorageAdapter.cs:23` | referência do provedor pode vazar para resposta ou log | nenhuma referência de provedor sai do adaptador; testes de contrato confirmam que respostas não têm host de armazenamento |
| Imagem final sem ffmpeg | `src/media/Dockerfile:30-34` | worker não prepara nada | EN-03 instala ffmpeg na imagem final |
| Logging sensível do EF em Development | `src/media/src/CodeForCoders.Media.Infra.Data/DependencyInjection.cs:28-31` | parâmetros de SQL (chave cifrada, nome do autor, título) em log local | desligar `EnableSensitiveDataLogging` no `media` (os logs de dev também chegam ao coletor OTLP) |
| Hosted services registrados sem distinção de papel | `src/media/src/CodeForCoders.Media.Infra.Messaging/DependencyInjection.cs:25-27` | worker e API rodando os mesmos serviços; preparação dentro da API se o papel não for respeitado | registro por papel em EN-03; teste de arquitetura: papel `api` não registra preparação |
| Assinatura da URL inclui o host | `docker-compose.yml:139-147` (Media fala com o MinIO pelo nome interno) | URL assinada para `minio:9000` não abre no navegador | cliente de assinatura configurado com o endpoint público (EN-01) |
| Validação de JWT por JWKS é cópia local em cada serviço | `src/commerce/.../Security/FinanceAreaJwksConfigurationManager.cs` (branch de CAP-002) | duas cópias podem divergir em cache e rotação | repetir o mesmo desenho e os mesmos testes; extrair para biblioteca comum é decisão separada, fora desta entrega |

---

## Decisões Técnicas

- **D-01 — Papéis `api` e `worker` da mesma imagem; fila no estado do vídeo com lease; chave cifrada no
  banco; marcador de URI de chave.** Registradas em [ADR-0006](../../docs/adr/0006-preparacao-de-video-e-custodia-de-chave.md).
  Worker em container separado **decidido pelo usuário em 2026-09-25**.
- **D-02 — Parte de 64 MiB, URL de parte de 60 min, retomada até validade da última URL + 24 h.**
  Racional: 5 GiB viram no máximo 80 partes (bem abaixo do limite de 10.000 do S3 e acima do mínimo de
  5 MiB); perder uma parte custa no máximo 64 MiB; uma hora cobre uma parte em conexão de 150 kbit/s.
  Alternativas: 16 MiB (mais chamadas, mais URLs); 256 MiB (retransmissão cara em rede ruim).
- **D-03 — Três tentativas para falha passageira (espera de 1 e 5 min), nenhuma para falha determinística
  do arquivo.** Racional: reprocessar arquivo ilegível não muda o resultado, só atrasa o motivo para o
  professor. Alternativa rejeitada: tentar tudo três vezes.
- **D-04 — Escada: 1080p a 5 Mbit/s, 720p a 3 Mbit/s, 480p a 1,2 Mbit/s (máximos), H.264 + AAC 128 kbit/s,
  segmentos de 6 s, preset rápido e threads limitadas por configuração.** Valores iniciais a ajustar pelo
  "tempo até pronto" dos primeiros 90 dias (métrica do PRD). Alternativa: CRF sem teto (tamanho imprevisível,
  pior para R10).
- **D-05 — Busca por título normalizado gravado pela aplicação**, não por extensão `unaccent` do Postgres.
  Racional: sem dependência de extensão no banco gerenciado; a regra de normalização é testável em unidade.
- **D-06 — Métricas de volume sem dimensão de tenant.** O baseline proíbe id como dimensão; com uma escola
  (DE10), o total é o da escola. O critério de RF-11 fica atendido assim, e o detalhe por escola vem de
  consulta quando houver multi-tenant. **Decidido pelo usuário em 2026-09-25.**
- **D-07 — Emitidas só pelo papel `worker`**, para que duas réplicas da API não dupliquem o gauge.

---

## Verificação

- **Cenários críticos não óbvios:**
  - dois workers disputando o mesmo vídeo → um processa, o outro pega o próximo;
  - worker encerrado no meio do ffmpeg → lease vence, outra tentativa começa, e nenhum `hls/` parcial
    sobrevive se o vídeo terminar em `failed`;
  - commit de `ready` falha depois de enviar `hls/` → reprocessar sobrescreve os mesmos objetos, e a chave
    nova substitui a antiga porque ambos entram só no mesmo commit;
  - apagamento do original falha depois de `ready` → varredura (b) apaga depois;
  - conclusão concorrente do mesmo envio → um vídeo só (índice único por `uploadId`);
  - retomada no mesmo segundo em duas abas → um envio só (índice único por ator + fingerprint);
  - envio expira no meio de uma parte → a parte em voo falha, e a próxima chamada responde `UPLOAD_NOT_FOUND`.
- **Dados ou ambiente especiais:** MinIO e Postgres por Testcontainers; ffmpeg instalado no job de CI
  (**incerto:** não confirmei se a imagem padrão do runner já o traz — o workflow instala explicitamente);
  vídeos sintéticos gerados por ffmpeg dentro do teste (`testsrc`), nenhum binário de vídeo no repositório;
  arquivo ilegível = bytes aleatórios com extensão `.mp4`; vídeo acima de 3 h em 144p para ficar pequeno.
- **CORS no MinIO local:** **incerto** — a configuração exata de CORS da versão do MinIO adotada não foi
  verificada. EN-01 confirma que o navegador consegue fazer `PUT` a partir de `http://localhost:8081` antes de
  V-02.
- **Observabilidade além do padrão:** as três métricas de V-07; span por etapa da preparação (baixar, sondar,
  preparar, publicar), com `video.status` e qualidades geradas, nunca título nem id de autor.
- **Verificação dos contratos:** respostas de Media e do BFF validadas contra os dois OpenAPI (inclusive a
  ausência de host de armazenamento fora de `url` de parte); fatos consumidos de fila de teste validados
  contra o AsyncAPI; recusas 401/403 de Media chamadas direto com JWT forjado, expirado, de outra audiência e
  sem permissão.

---

## Questões em Aberto

- [ ] **Armazenamento indisponível na API** — o contrato não tem resposta para falha do S3 em
  `createVideoUpload`/`completeVideoUpload`. Proposta: 503 `STORAGE_UNAVAILABLE` em Media e 502
  `MEDIA_UNAVAILABLE` no BFF, como a indisponibilidade de Identity em CAP-002; exige versão patch dos dois
  OpenAPI antes das tasks. — dono do contrato (você) — sem isso, o implementador inventa o código.
- [ ] **QP-02 — nome e posição da área** — design, em EN-02 — só navegação.
- [ ] **Chave-mestra em produção** — plataforma: criar o segredo e o backup antes da primeira preparação em
  ambiente real — sem ela, o worker não parte.

---

## Architecture Decision Records

- [ADR-0005: Sessão do ator interno e autenticação de serviço do BFF do backoffice](../../docs/adr/0005-sessao-e-servico-do-backoffice.md) — Media é o segundo serviço a validar o JWT do ator interno via JWKS.
- [ADR-0002: Plataforma de runtime Coolify](../../docs/adr/0002-plataforma-de-runtime-coolify.md) — o worker roda no Coolify; AWS só para S3 + CloudFront.
- [ADR-0006: Preparação de vídeo em worker próprio do media e custódia da chave HLS](../../docs/adr/0006-preparacao-de-video-e-custodia-de-chave.md) — **nova, Proposed**.
