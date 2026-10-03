---
tsg_artifact: techspec
product: code-4-coders
capability: CAP-006
version: 1.0
status: in_review
updated: 2026-09-28
sources: tasks/prd-observabilidade-midia/prd.md@1.0, context/architecture-baseline.md@1.2, domains/entrega-de-midia-e-protecao/domain.md@1.0
---

# TechSpec — Observabilidade operacional da ingestão de mídia

> **Escopo:** Backend (+ infraestrutura de telemetria; nenhum frontend)
> **Modo:** Pipeline
> **PRD de origem:** `tasks/prd-observabilidade-midia/prd.md`
> **Contratos de integração:** N/A — nenhum contrato HTTP, de mensageria ou de dados
> compartilhado é criado ou alterado; o outbox e os eventos `midia.*` permanecem como estão,
> apenas observados. O único "contrato" novo é a tabela de instrumentos desta spec: nomes,
> tipos, unidades e dimensões que o dashboard e os alertas assumem.
> **Data:** 2026-09-28
> **Status:** Em Revisão
> **Handoff:** draft — não gerar Tasks

---

## Resumo Executivo

- O serviço `media` (papéis `api` e `worker`, mesma imagem) passa a emitir métricas de domínio do
  pipeline — funil de envio, fila, preparação, outbox, DLQ — seguindo o padrão OTel existente
  (`MediaTelemetry` + `MediaVolumeMetricsWorker`), sem novo contrato externo e sem mudança de
  comportamento do pipeline.
- O Kibana do servidor de desenvolvimento ganha o dashboard **"Pipeline de Mídia"** e **5 regras de
  alerta** (threshold, sem conector externo), provisionados como saved objects versionados no
  repositório (ADR-0008, Proposed).
- **Trade-off primário:** cobertura operacional completa com a stack existente (Elastic/Kibana que
  já indexa traces/logs/metrics) ao custo de: (a) a granularidade de série ficar limitada aos
  conjuntos fechados de dimensão (etapa, motivo, evento, fila) — investigação pontual continua
  dependendo de trace/log via `video.id`; e (b) a disponibilidade da observabilidade ficar atrelada
  ao Elastic de uma máquina só, aceita pelo PRD.

### Princípios de emissão (herdados e estendidos)

1. **Gauge de estado é snapshot único do banco**, emitido só pelo papel `worker` (padrão D-07 do
   techspec anterior): o snapshot periodicamente consulta `videos`, `video_uploads` e
   `outbox_messages` e alimenta todos os gauges — nunca o papel `api`, nunca por requisição.
2. **Counter e histogram são incrementados no instante do evento, por quem executa o evento** — o
   papel que faz a ação incrementa; não há soma de snapshot.
3. **Dimensão é sempre conjunto fechado** (`status`, `reason`, `stage`, `event`, `queue`) — nunca
   Id, nunca tenant (DE10/D-06). O identificador do vídeo vive em tag de span e escopo de log
   (RF-07), onde Id é permitido.
4. Nomenclatura e unidade seguem o baseline: `{servico}.{agregado}.{evento}` com `unit` OTel
   (`s`, `By`, `{video}`, `{upload}`, `{message}`).

### Tabela de instrumentos (contrato com o dashboard e os alertas)

| Instrumento | Tipo | Unit | Dimensões | Papel emissor | Gatilho |
|---|---|---|---|---|---|
| `media.upload.created` | counter | `{upload}` | — | api | `CreateVideoUpload` cria sessão (novo upload; retomada por fingerprint não conta) |
| `media.upload.completed` | counter | `{upload}` | — | api | `CompleteVideoUpload` completa o multipart |
| `media.upload.size` | histogram | `By` | — | api | na conclusão, com `original_size_bytes`; buckets explícitos cobrindo 1 MiB–5 GiB |
| `media.upload.expired` | counter | `{upload}` | — | worker | `ExpirePendingVideoUploads` expira sessão abandonada |
| `media.uploads.pending` | gauge | `{upload}` | — | worker | snapshot: sessões com `completed_at IS NULL AND expired_at IS NULL` |
| `media.videos.claimed` | counter | `{video}` | — | worker | worker reclama vídeo da fila |
| `media.videos.retried` | counter | `{video}` | — | worker | claim com `preparation_attempts > 1` |
| `media.videos.wait` | histogram | `s` | — | worker | no claim: `now - uploaded_at`; buckets 1 s–1 h |
| `media.videos.completed` | counter | `{video}` | — | worker | commit em `ready` |
| `media.videos.failed` | counter | `{video}` | `reason` ∈ {unreadable-file, unsupported-format, duration-exceeded, attempts-exhausted} | worker | commit em `failed` |
| `media.videos.prepare_duration` | histogram | `s` | `stage` ∈ {download, probe, transcode, publish} | worker | fim de cada etapa (mesmos pontos dos spans `media.video.*`) |
| `media.videos.time_to_ready` | histogram | `s` | — | worker | commit em `ready`: `now - uploaded_at`; buckets 10 s–24 h |
| `media.videos.count{status}` | gauge | — | `status` (existente) | worker | já existe |
| `media.videos.stuck` | gauge | `{video}` | — | worker | já existe |
| `media.storage.used` | gauge | `By` | — | worker | já existe |
| `media.videos.oldest_waiting` | gauge | `s` | — | worker | snapshot: `min(uploaded_at)` com `status = 'received'`; 0 quando fila vazia |
| `media.outbox.pending` | gauge | `{message}` | — | worker | snapshot: `processed_on IS NULL` |
| `media.outbox.oldest_pending` | gauge | `s` | — | worker | snapshot: idade do `created_at`(equivalente) do pendente mais antigo; 0 quando vazio |
| `media.outbox.exhausted` | gauge | `{message}` | — | worker | snapshot: `processed_on IS NULL AND attempts >= max` |
| `media.outbox.published` | counter | `{message}` | `event` ∈ {ativo-pronto, preparacao-falhou} | api **e** worker | cada publicação bem-sucedida (quem publica incrementa) |
| `media.outbox.publish_failed` | counter | `{message}` | `event` idem | api **e** worker | cada `RegisterFailure` |
| `media.messaging.dlq.messages` | gauge | `{message}` | `queue` ∈ {media.platform-heartbeat.dlq} | worker | poll da RabbitMQ Management API |

Métrica sem medição em um ciclo de snapshot não é emitida com zero inventado: gauge reporta o que
consultou; ausência de série é interpretada pelo painel como *sem dado*, distinto de zero (regra de
staleness do RF-01).

---

## Arquitetura da Solução

### Bloco Backend

Instrumentação no próprio serviço `media`, sem novos agregados, portas ou eventos:

- **Instrumentos** ficam junto aos existentes em `MediaTelemetry` (Application), criados no Meter
  único do serviço, seguindo `HeartbeatsRecorded/Consumed` como padrão.
- **Snapshot de estado** estende o worker existente de volume (`MediaVolumeMetricsWorker`): cada
   ciclo (60 s) passa a consultar também sessões de envio pendentes, o `received` mais antigo e o
  outbox, na mesma transação de leitura `AsNoTracking` + `IgnoreQueryFilters` (multitenancy não é
  dimensão — DE10). Gauges continuam registrados uma única vez, no papel worker
  (`DependencyInjection.cs:42`).
- **Counters/histograms de envio** são incrementados nos casos de uso da API
  (`CreateVideoUpload`, `CompleteVideoUpload`, `ExpirePendingVideoUploads`) no ponto de sucesso —
  após o efeito acontecer, nunca antes.
- **Counters/histograms de preparação** são incrementados no workflow `PrepareVideo` (worker) junto
  aos pontos onde os spans `media.video.download|probe|transcode|publish` já existem, mais um no
  claim (`ClaimNextAsync`), no commit `ready` e no commit `failed`.
- **Outbox counters** são incrementados dentro de `OutboxPublisherWorker.PublishOneAsync` — o
  worker roda nos dois papéis (`DependencyInjection.cs:27`); counter é aditivo por instância que
  publica, então contar nos dois papéis é correto e sem dupla contagem. Os **gauges** de outbox
  ficam exclusivamente no snapshot do papel worker (senão duas réplicas reportam o mesmo pendente).
- **DLQ**: novo BackgroundService do papel worker consulta a **RabbitMQ Management API**
  (`GET /api/queues/{vhost}/{name}`, basic auth das credenciais já configuradas) a cada ciclo de
  snapshot e emite `media.messaging.dlq.messages{queue}`. Nova opção `RabbitMq:ManagementUri`
  (padrão `http://localhost:15672`). Falha de consulta degrada em log warning e gauge sem ponto —
  não derruba o worker, não vira exceção não tratada.
- **`video.id` em span e log (RF-07):** o helper `PrepareVideo.StartActivity`
  (`PrepareVideo.cs:399-404`) passa a fixar também a tag `video.id`, e os logs do workflow/complete
  carregam `VideoId` no escopo estruturado. Com isso o Discover filtra `video.id` em traces e logs
  sem reproduzir envio. (Id em trace/log é permitido; a proibição do baseline é dimensão de
  métrica.)

### Infraestrutura de visualização (Kibana)

- **Dashboard "Pipeline de Mídia"** com as seções do PRD: Estado (RF-01), Envio (RF-02), Fila
  (RF-03), Preparação (RF-04), Outbox/DLQ (RF-05), sobre o data view `metrics-generic*` existente.
  Séries de gauge em time series com janela explícita; um painel "última atualização" (timestamp do
  ponto mais recente do snapshot) materializa o requisito de staleness do RF-01 — sem ponto novo,
  a janela esvazia e a idade aparece.
- **5 regras de alerta** (Threshold/ES|QL sobre `metrics-generic*`), sem conector de notificação:

| # | Alerta | Condição (janela 15 min, checagem 1 min) |
|---|---|---|
| A1 | Vídeo preso | `media.videos.stuck > 0` |
| A2 | Taxa de falha de preparação | `failed/(completed+failed) > 0,10` **e** ≥ 4 preparações finalizadas na janela |
| A3 | Outbox esgotado/atrasado | `media.outbox.exhausted > 0` **ou** `media.outbox.oldest_pending > 600 s` |
| A4 | Fila parada | `media.videos.oldest_waiting > 1800 s` |
| A5 | DLQ não vazia | `media.messaging.dlq.messages > 0` |

  Resolução automática quando a condição deixa de valer. O mínimo de 4 amostras do A2 vem do risco
  "alarme falso com volume baixo" do PRD. Valores confirmados em revisão (2026-09-28); calibráveis
  pós-rollout sem mudança de código (a regra vive no Kibana, não na aplicação).
- **Provisionamento como código (ADR-0008, Proposed):** dashboard e regras são saved objects NDJSON
  versionados em `scripts/kibana/` e importados no Kibana do servidor de desenvolvimento por script
  (`scripts/remote-infra.sh` como referência de acesso), documentado em
  `docs/infra-servidor-desenv.md`. Nada é criado apenas na mão: o que está no Kibana tem origem no
  repositório.

---

## Mapa de Fatias Verticais

### V-01: Dashboard "Pipeline de Mídia" v1 sobre os sinais existentes

- **Cobre:** RF-01 (parcial: estado por etapa com os sinais já emitidos), Fase 1 do PRD.
- **Entrada / gatilho:** operador abre o Kibana do servidor de desenvolvimento.
- **Processamento:** dashboard criado por import do saved object versionado, consultando
  `media.videos.count{status}`, `media.videos.stuck` e `media.storage.used`; painel de idade da
  última atualização do snapshot.
- **Saída observável:** painel com contagem por estado (recebido, em preparação, pronto, falhou),
  preso e volume guardado, com staleness visível.
- **Evidência / checkpoint:** com a stack local enviando OTLP ao servidor dev (pré-requisito de
  V-07 ou produção rodando), `media.videos.count` aparece no painel e o timestamp do último ponto
  tem menos de 2 min; dashboard importável a partir do repositório por segunda pessoa.
- **Bloqueado por:** Nenhum (não depende de código novo).

### V-02: Funil e tamanho dos envios

- **Cobre:** RF-02, RF-01 (contagem de sessões pendentes).
- **Entrada / gatilho:** professor cria/conclui/expira sessão de envio; snapshot do worker roda.
- **Processamento:** counters `media.upload.created/completed/expired` e histogram `media.upload.size`
  nos casos de uso da API; gauge `media.uploads.pending` no snapshot do worker (consulta nova a
  `video_uploads`); seção "Envio" do dashboard (criadas, concluídas, expiradas comparáveis na mesma
  janela; distribuição de tamanho; pendentes).
- **Saída observável:** painel do funil distingue abandono (expirada) de conclusão e de falha de
  preparação.
- **Evidência / checkpoint:** teste de unidade com `MeterListener`-equivalente asserting os
  incrementos (sessão criada→1, retomada→0, concluída→1 com tamanho certo, expirada→1); no smoke,
  um envio concluído e um abandonado aparecem como tal no painel.
- **Bloqueado por:** Nenhum.

### V-03: Profundidade e idade da fila

- **Cobre:** RF-03.
- **Entrada / gatilho:** snapshot do worker; worker reclama vídeo.
- **Processamento:** gauge `media.videos.oldest_waiting` (min `uploaded_at` em `received`; 0 se
  vazio) no snapshot; counter `media.videos.claimed`, `media.videos.retried` e histogram
  `media.videos.wait` no claim do `PrepareVideo`; seção "Fila" do dashboard (profundidade =
  `media.videos.count{status=received}`, idade do mais antigo, entradas por espera).
- **Saída observável:** resposta a "a fila está andando?" sem abrir o banco; preso já visível desde
  V-01.
- **Evidência / checkpoint:** unidade: fila com 2 `received` (10 min e 2 h) → oldest_waiting ≈ 2 h;
  fila vazia → 0; smoke: vídeo enviado aparece na profundidade e sai dela ao ser reclamado.
- **Bloqueado por:** Nenhum.

### V-04: Resultado da preparação e rastreio por vídeo

- **Cobre:** RF-04, RF-07.
- **Entrada / gatilho:** execução do workflow `PrepareVideo` (worker).
- **Processamento:** histograms `media.videos.prepare_duration{stage}` nos mesmos pontos dos spans
  existentes e `media.videos.time_to_ready` no commit `ready`; counters `media.videos.completed`,
  `media.videos.failed{reason}` (motivo mapeado de `VideoFailureReasons`; caminho de exceção
  genérica engolido pelo workflow conta como `attempts-exhausted` quando esgota) e `media.videos.retried`;
  tag `video.id` no helper de span e `VideoId` no escopo de log do workflow; seção "Preparação" do
  dashboard (percentis por etapa, time-to-ready, tentativas, falhas por motivo em janela).
- **Saída observável:** falha com motivo no painel no dia em que acontece; investigação por
  `video.id` no Discover encontra spans e logs do vídeo.
- **Evidência / checkpoint:** unidade: vídeo que falha por `unsupported-format` → +1 em
  `media.videos.failed{reason=unsupported-format}` e nenhum `completed`; vídeo que passa por falha
  passageira e chega a pronto → `retried` +1, `failed` 0, `time_to_ready` com uma observação; todo
  span `media.video.*` carrega `video.id`. Smoke: falha provocada (arquivo corrompido) é achada no
  Discover pelo `video.id` sem abrir o banco.
- **Bloqueado por:** Nenhum (campos de que depende — `uploaded_at`, `failure_reason`,
  `preparation_attempts` — já existem).

### V-05: Saúde do outbox e da DLQ

- **Cobre:** RF-05, sinais obrigatórios do baseline (outbox atrasado/esgotado, DLQ não vazia).
- **Entrada / gatilho:** publicação do outbox (ambos os papéis); snapshot e poll de DLQ (worker).
- **Processamento:** counters `media.outbox.published{event}` e `media.outbox.publish_failed{event}`
  em `PublishOneAsync`; gauges `media.outbox.pending/oldest_pending/exhausted` no snapshot;
  `media.messaging.dlq.messages{queue}` via Management API (opção `RabbitMq:ManagementUri`);
  seção "Outbox e DLQ" do dashboard.
- **Saída observável:** pendentes, idade, esgotados e profundidade da DLQ no mesmo painel.
- **Evidência / checkpoint:** unidade: publicação ok → +1 `published`; `RegisterFailure` → +1
  `publish_failed`; snapshot com 3 pendentes (uma com `attempts >= max`) → pending 3, exhausted 1;
  integração (Compose): mensagem na DLQ do heartbeat → gauge > 0 no painel.
- **Bloqueado por:** Nenhum.

### V-06: Alertas de threshold no Kibana

- **Cobre:** RF-06 (regras A1–A5 da tabela acima).
- **Entrada / gatilho:** avaliação periódica (1 min) sobre `metrics-generic*`.
- **Processamento:** regras criadas por import do saved object versionado; A2 com mínimo de 4
  preparações na janela; nenhuma com conector de notificação.
- **Saída observável:** alertas ativos/resolvidos na tela de Alertas do Kibana, sozinhos.
- **Evidência / checkpoint:** smoke de duas regras com métrica real: vídeo corrompido disparado em
  lote ≥ 4 ativa A2 e resolve após janela; parar o `media-worker` (fila acumula) ativa A4; as duas
  resolvem sozinhas ao normalizar. A1/A3/A5 verificadas por revisão da definição + série sabida.
- **Bloqueado por:** V-02, V-03, V-04, V-05 (as regras referenciam os instrumentos delas).

### V-07: Paridade do ambiente local

- **Cobre:** suporte à evidência de V-01..V-06 fora do servidor de desenvolvimento.
- **Entrada / gatilho:** desenvolvedor quer os mesmos painéis com a stack local.
- **Processamento:** `docker-compose.yml` passa a interpolar o endpoint OTLP
  (`OTEL_EXPORTER_OTLP_ENDPOINT`, padrão `http://otel-collector:4317`); apontando para
  `http://192.168.0.5:4317`, a telemetria local vai ao Elastic do servidor dev sem mudar o
  collector local (que continua `debug`). Documentado em `docs/infra-servidor-desenv.md`.
- **Saída observável:** dashboard "Pipeline de Mídia" reflete uploads feitos na stack local.
- **Evidência / checkpoint:** com a variável apontada, enviar um vídeo local e vê-lo no painel em
  ≤ 2 min.
- **Bloqueado por:** V-01.

### Habilitadores inevitáveis

Nenhum: cada fatia entrega comportamento observável de ponta a ponta.

---

## Contratos e Fronteiras

Nenhum contrato HTTP, AsyncAPI ou ODCS é criado ou alterado. A tabela de instrumentos do Resumo
Executivo é a fronteira contratual interna desta feature: dashboard e alertas (consumidores)
assumem exatamente esses nomes, tipos, unidades e dimensões. Renomear ou redimensionar instrumento
depois do rollout exige atualizar dashboard, alertas e esta spec juntos.

### Credenciais e dados sensíveis

- O poll da Management API usa as mesmas credenciais RabbitMQ do serviço (`RabbitMqOptions`), com
  `ManagementUri` novo em configuração — senha não vai para log, métrica ou dashboard; o gauge
  reporta apenas a profundidade por fila.
- Nenhuma métrica carrega título, autor, e-mail ou tenant (DE10/G23): dimensões são conjuntos
  fechados. `video.id` existe apenas em span/log (RF-07), locais onde Id é permitido.
- Alertas e dashboards não têm conector externo: nada sai do Kibana.

---

## Arquivos a Modificar e a Referenciar

### A modificar

| Caminho | Fatia | Alteração |
|---|---|---|
| `src/media/src/CodeForCoders.Media.Application/Common/MediaTelemetry.cs` | V-02..V-05 | novos counters/histograms ao lado dos existentes |
| `src/media/src/CodeForCoders.Media.Application/UseCases/VideoUploads/CreateVideoUpload/CreateVideoUpload.cs` | V-02 | counter `upload.created` só em sessão nova |
| `src/media/src/CodeForCoders.Media.Application/UseCases/VideoUploads/CompleteVideoUpload/CompleteVideoUpload.cs` | V-02, V-04 | counters `upload.completed`/histogram `upload.size`; `VideoId` no log |
| `src/media/src/CodeForCoders.Media.Application/UseCases/VideoUploads/ExpirePendingVideoUploads/ExpirePendingVideoUploads.cs` | V-02 | counter `upload.expired` |
| `src/media/src/CodeForCoders.Media.Application/UseCases/Videos/PrepareVideo/PrepareVideo.cs` | V-03, V-04 | instruments de claim/etapa/fim + tag `video.id` no helper de span (`:399`) |
| `src/media/src/CodeForCoders.Media.Infra.Messaging/MediaVolumeMetricsWorker.cs` | V-02, V-03, V-05 | snapshot estendido (uploads pendentes, oldest_waiting, outbox) e novos gauges |
| `src/media/src/CodeForCoders.Media.Infra.Messaging/OutboxPublisherWorker.cs` | V-05 | counters `outbox.published/publish_failed` nos pontos de sucesso/falha |
| `src/media/src/CodeForCoders.Media.Infra.Messaging/Configuration/RabbitMqOptions.cs` | V-05 | opção `ManagementUri` |
| `src/media/src/CodeForCoders.Media.Infra.Messaging/DependencyInjection.cs` | V-05 | registrar o poller de DLQ no papel worker |
| `docker-compose.yml` | V-07 | interpolar `OTEL_EXPORTER_OTLP_ENDPOINT` com padrão atual |
| `docs/infra-servidor-desenv.md` | V-01, V-07 | provisionamento dos saved objects e override local do endpoint |

### A referenciar (não alterar)

| Caminho | Por que consultar |
|---|---|
| `src/media/src/CodeForCoders.Media.Api/Extensions/ObservabilityExtensions.cs` | wiring do Meter/ActivitySource no host — novos instrumentos entram pelo Meter existente |
| `src/media/src/CodeForCoders.Media.Infra.Messaging/RabbitMqTopologyInitializer.cs` | nomes canônicos de fila/DLX/DLQ que o gauge `queue` assume |
| `src/media/src/CodeForCoders.Media.Infra.Data/Videos/VideoPreparationRepository.cs` | invariantes do claim (`FOR UPDATE SKIP LOCKED`, orçamento de disco) — instrumentação não pode entrar na transação |
| `tasks/prd-ingestao-midia/techspec.md` (decisões D-01..D-07, fatia V-07) | padrões de emissão herdados; não reabrir |
| `otel-collector-config.yaml`, `docker-compose.coolify.yml` | roteamento OTLP atual (local `debug`; produção `192.168.0.5:4317`) |

---

## Análise de Impacto

| Componente | Tipo | Impacto e risco | Ação requerida |
|---|---|---|---|
| `media` api/worker | modificado | incremento de instrumentação em caminho quente (claim, publish outbox) — custo desprezível, mas o snapshot ganha 3 consultas/ciclo | consultas do snapshot com índice (`ix_videos_preparation_queue`, `ux_video_uploads_..._pending`) e `AsNoTracking`; sem lock |
| RabbitMQ Management API | novo consumo | autenticação básica existente; porta 15672 precisa alcançável do worker em cada ambiente | confirmar exposição no runtime de produção (Questão em Aberto); falha degrada em warning |
| Elastic do servidor dev | modificado | novos saved objects + volume de séries novo (≈ 20 séries, cardinalidade fechada) | ILM do `metrics-generic` com **retenção de 30 dias** (definido em revisão) |
| `admin-spa` / BFFs | — | nenhum: sem API, sem tela, sem evento | nenhuma |
| Outros serviços | — | nenhum contrato muda; padrão de instrumentos é reutilizável por outras fatias futuras | nenhuma |

---

## Riscos e Preocupações

| Preocupação | Local (`arquivo:linha`) | Impacto | Mitigação |
|---|---|---|---|
| `OutboxPublisherWorker` roda nos dois papéis — gauges de outbox contariam duplicado | `src/media/.../Infra.Messaging/DependencyInjection.cs:27` | dashboard/alerta A3 mentem por 2× | gauges só no snapshot do worker (D-07); counters nos dois papéis (aditivos, corretos) |
| Falha genérica de etapa é engolida pelo workflow sem marcação de motivo | `src/media/.../UseCases/Videos/PrepareVideo/PrepareVideo.cs:67-69` (supressão CA1031) | `media.videos.failed{reason}` poderia ficar sem ponto em crash inesperado | contar `failed` no commit de status (fonte: `failure_reason` persistido), não na exceção; caminho sem motivo vira `attempts-exhausted` ao esgotar |
| Spans hoje não carregam `video.id` | `src/media/.../UseCases/Videos/PrepareVideo/PrepareVideo.cs:399-404` | RF-07 não entrega: Discover não filtra por vídeo | helper central fixa a tag uma vez; todos os spans do workflow herdam |
| Snapshot consulta com filtro de tenant global ligado contaria só um tenant | `src/media/.../Infra.Messaging/MediaVolumeMetricsWorker.cs:37-48` | números do painel errados | manter `IgnoreQueryFilters()` como o snapshot atual já faz |
| Staleness confundido com fila vazia (gauge sem ponto ≠ 0) | dashboard (novo) | operador acha que fila zera quando worker morre — pior que escuro | painel de idade da última atualização + série em time series (gap visível), critério RF-01 |
| Alerta A2 com 1 vídeo = 100% de falha | regra A2 | alarme falso com volume baixo | mínimo de 4 preparações finalizadas na janela |
| Renomear instrumento quebra dashboard/alerta silenciosamente | tabela de instrumentos | painel esvazia sem erro | testes de contrato de instrumentos (nomes/dimensões) na suíte do Media; alteração exige esta spec + saved objects juntos |

---

## Decisões Técnicas

- **Decisão:** observação de fila por snapshot do banco (não por eventos de mensageria).
- **Racional:** a fila de preparação **é** estado no banco com lease (ADR-0006 rejeitou fila RabbitMQ
  para trabalho interno); medir a fonte da verdade evita modelo paralelo.
- **Trade-offs:** granularidade de 60 s; queries por ciclo.
- **Alternativas rejeitadas:** eventos incrementais por mudança de status (duplicaria a máquina de
  estado e perderia reconciliação); consulta do painel direto ao banco (violando DP-04 do PRD).

- **Decisão:** DLQ via RabbitMQ Management API no worker, e não exporter Prometheus do broker.
- **Racional:** uma série por fila resolve o sinal obrigatório do baseline sem nova stack de
  coleta; credenciais e imagem `management-alpine` já existem. URI e credenciais chegam por
  variáveis de ambiente (`.env`) com a mesma identidade já usada pelo serviço — o usuário
  provisiona os dados no `.env` (definido em revisão, 2026-09-28).
- **Trade-offs:** dependência da porta 15672; falha do poll vira gap de série (visível), não erro.
- **Alternativas rejeitadas:** plugin `rabbitmq_prometheus` + scrape no collector (infra nova para
  1 série); health check existente não dá histórico.

- **Decisão:** retenção das séries de métrica em **30 dias** (ILM do índice `metrics-generic` no
  servidor de desenvolvimento), aplicada pelo time de plataforma no rollout.
- **Racional:** cobre a janela de investigação mensal (comparar mês atual com anterior nos painéis
  de falha e tempo até pronto) sem estourar o disco da máquina única.
- **Trade-offs:** histórico mais antigo deixa de existir em métrica (traces/logs seguem a política
  deles); dashboards com janela > 30 d mostram série truncada.
- **Alternativas rejeitadas:** 7 dias (não cobre comparação mensal); 90 dias (volume além do
  conforto da máquina única desta fase).

- **Decisão:** dashboards e alertas provisionados como saved objects versionados no repositório.
- **Racional:** o que não é versionado apodrece divergente do repo; segunda pessoa precisa
  recriar ambiente sem conhecimento tribal; alerta sem revisão é pior que ausência.
- **Trade-offs:** NDJSON de Kibana é verboso e amarra versão 9.x do formato.
- **Alternativas rejeitadas:** criação manual documentada (irreproduzível); Terraform Elastic
  (ferramenta nova para 6 objetos).

---

## Verificação

- **Cenários críticos não óbvios:**
  - gauge de outbox emitido só pelo worker com **dois** processos publicando (api + worker):
    `pending` não dobra, `published` soma os dois;
  - retomada de sessão por fingerprint **não** incrementa `upload.created`;
  - `oldest_waiting` zera (e não some) quando a fila esvazia;
  - counter `failed{reason}` cobre o caminho de exceção engolida (CA1031) via status persistido;
  - spans do workflow todos com `video.id` (helper único).
- **Dados ou ambiente especiais:** Compose local com `OTEL_EXPORTER_OTLP_ENDPOINT` apontado ao
  servidor dev (V-07) e rede alcançando `192.168.0.5:4317`/Kibana; vídeo de teste corrompido para
  falha determinística; URI e credenciais da RabbitMQ Management API no `.env` (mesma identidade
  do serviço); ILM de `metrics-generic` em 30 dias aplicada no servidor dev antes do rollout da
  Fase 2.
- **Observabilidade além do padrão:** a própria tabela de instrumentos; painel de staleness; teste
  de contrato de nomes/dimensões para evitar renome silencioso.
- **Contratos:** nenhum contrato externo novo; verificação se limita aos instrumentos e aos saved
  objects importados (import limpo por segunda pessoa = evidência).

---

## Architecture Decision Records

- [ADR-0008: Observabilidade do Kibana provisionada como código](../../../docs/adr/0008-observabilidade-kibana-como-codigo.md)
  — **Proposed** (sobe para Accepted junto com esta spec): dashboards e alertas são saved objects
  versionados e importados por script; estabelece a convenção para os próximos pipelines.
- [ADR-0006: Preparação de vídeo em worker próprio e custódia da chave](../../../docs/adr/0006-preparacao-de-video-e-custodia-de-chave.md)
  — conformada: a fila observada é o estado no banco com lease, decisão mantida intacta.
