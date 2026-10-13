---
tsg_artifact: techspec
product: code-4-coders
capability: CAP-032
version: 1.0
status: approved
updated: 2026-10-12
sources: tasks/prd-transcricao-legenda-aula/prd.md@1.1, tasks/prd-transcricao-legenda-aula/contracts.md@1.0, context/architecture-baseline.md@1.2, domains/entrega-de-midia-e-protecao/domain.md@1.1, domains/conteudo-e-curriculo/domain.md@1.1, domains/matricula-e-direito-de-acesso/domain.md@1.0
---

# Especificação Técnica — Transcrição e legenda de aula

> **Escopo:** Full-stack (`media` API e worker, `bff-admin`, `bff-student`, `admin-spa`, `student-spa`, configuração de implantação)
> **Modo:** Pipeline + API-First
> **PRD de origem:** `tasks/prd-transcricao-legenda-aula/prd.md` (v1.1, aprovado)
> **Contratos de integração:** [contracts.md](contracts.md) 1.0 (aprovado em 2026-10-12) e os quatro OpenAPI: [api-contract.yaml](api-contract.yaml), [internal-api-contract-media.yaml](internal-api-contract-media.yaml), [api-contract-student.yaml](api-contract-student.yaml), [webhook-contract-media.yaml](webhook-contract-media.yaml)
> **Data:** 2026-10-12
> **Status:** Aprovado em 2026-10-12
> **Handoff:** approved — pode alimentar o Task Creator

---

## Resumo Executivo

A capacidade transforma a fala de um vídeo pronto em texto e o entrega de duas formas ao aluno com direito à aula: legenda no player e transcrição pesquisável na página da aula. O trabalho pesado e o estado vivem inteiros no `media`; os BFFs repassam; os SPAs exibem.

Decisões principais:

- **O áudio sai da Versão de Reprodução, pelo worker do `media`.** O original é apagado ao fim da preparação (`PrepareVideo.cs:183`), então o único insumo que existe para qualquer vídeo pronto é o HLS cifrado, cuja chave só o `media` tem (ADR-0006). O worker lê a variante de menor banda, usa a chave só no diretório temporário e extrai áudio mono 16 kHz em AAC. Um caminho serve ao automático, ao refazer e ao vídeo antigo.
- **A Transcrição é uma entidade-fila no banco do `media`**, com concessão (lease) e `FOR UPDATE SKIP LOCKED`, como a preparação. Não é estado do vídeo (RN-M19). O disparo automático é uma linha inserida na mesma transação que marca o vídeo pronto.
- **Serviço externo atrás de camada anticorrupção.** Envio por URL pré-assinada de leitura do áudio, `Idempotency-Key` estável por envio, `clientReference` igual ao id da tentativa. O webhook é só gatilho; a **consulta periódica é o caminho obrigatório** e cobre aviso perdido e ambiente que o serviço não alcança.
- **Texto sob guarda própria em `jsonb`** numa tabela de conteúdo, copiado antes das 24 h do serviço. Servido ao professor, à página da aula e à legenda (WebVTT gerado na hora). Refazer troca a vigente numa transação.
- **Busca e destaque rodam no cliente**; a legenda é uma faixa de texto do player alimentada pelo WebVTT da sessão.
- **ADR-0020 (Proposed)** registra a decisão durável (derivar de HLS cifrado, entidade-fila própria, aviso como gatilho, guarda do texto). Conforma a ADR-0006; não substitui nada.

**Trade-off primário.** Ganha-se um caminho único, reproduzível e sem tocar a Mídia, ao custo de **baixar os segmentos da menor qualidade a cada transcrição** (na ordem de 1 GiB para 2 horas a 480p, estimado) e de guardar o texto completo no banco do `media`. A alternativa barata (extrair o áudio na preparação) não serve a vídeo antigo nem a refazer.

---

## Arquitetura da Solução

### Componentes e responsabilidades

| Componente | O que esta feature faz nele |
|---|---|
| `media` — worker | Reivindica Transcrições, extrai o áudio da Versão de Reprodução, grava no prefixo privado, pede a transcrição ao serviço, concilia por consulta, copia o texto, apaga o áudio, emite os sinais (RN-M26). Papel `worker`, mesma imagem (ADR-0006) |
| `media` — API | Estado da transcrição dentro de `Video`, pedido Transcrever/Refazer, leitura do professor, leitura da aula do aluno, legenda WebVTT da sessão, receptor do aviso do serviço. Papel `api` |
| `bff-admin` | Repasse 1:1 das duas operações novas e do campo novo em `Video` (cookie `staff_session`, `X-CSRF-Token`, `Idempotency-Key`) |
| `bff-student` | Repasse 1:1 das duas leituras (cookie `student_session`), no molde do repasse de reprodução, preservando `Content-Type` e `Cache-Control` |
| `admin-spa` | Estado na tela Vídeos, ações Transcrever/Refazer, leitura somente leitura, atualização automática |
| `student-spa` | Painel de transcrição na página da aula, busca local, clique que leva o vídeo ao tempo, controle de legenda no player |
| Serviço de transcrição | Dependência externa. Contrato do repositório dele (`api-contract.yaml` 1.0.0, `b3ac8b8`); não é copiado para `contracts/` |

### Diagrama

```
 professor ──► admin-spa ──► bff-admin ──► media API ──┐
                                                       │ insere Transcrição (queued)
 vídeo fica pronto ─► PrepareVideo (mesma transação) ──┤
                                                       ▼
                                         video_transcriptions  ◄── webhook ── serviço de transcrição
                                                       │  (fila + lease)     (só sinaliza: signaled_at)
                                                       ▼
                              media WORKER ── lê HLS (menor banda) + chave ──► ffmpeg ──► áudio .m4a
                                   │                                              (prefixo privado)
                                   ├── URL pré-assinada GET (90 min, em memória) ─► POST /v1/transcriptions
                                   ├── consulta periódica / sinal ─► GET job ─► GET result
                                   ▼
                    video_transcription_contents (jsonb, trechos aparados)  ── apaga áudio
                                   ▲
 aluno ─► student-spa ─► bff-student ─► media API: /lessons/{id}/transcription  (Referência de Uso + Matrícula)
                                                 : /playback-sessions/{id}/captions (WebVTT, sessão válida)
```

### Ciclo de vida da Transcrição (fila do worker)

A linha `video_transcriptions` é uma **tentativa**. O estado do produto (`not-requested`, `in-progress`, `ready`, `failed`) é derivado dela; o estado interno é mais fino.

| `status` interno | Significado | Quem age | Sai para |
|---|---|---|---|
| `queued` | Aguardando o worker (inclui reenvio depois de falha transitória) | worker, quando `next_action_at <= agora` | `working` |
| `working` | Concessão ativa (extraindo áudio, enviando, consultando ou copiando) | worker dono da concessão; concessão vencida volta a elegível e conta tentativa | `waiting`, `queued`, `ready`, `failed` |
| `waiting` | Job aceito no serviço; aguarda término | consulta periódica ou sinal do webhook (`signaled_at`) | `working` (para consultar) |
| `ready` | Texto copiado, é a vigente | — | substituída por uma nova `ready` |
| `failed` | Terminou com motivo de produto | — | apagada quando outra tentativa começa |

`in-progress` = `queued` + `working` + `waiting`.

**Invariantes por índice único parcial** (não por código): no máximo **uma tentativa em andamento por vídeo** (RN-M20) e **no máximo uma vigente por vídeo** (`is_current`). Disparar de novo com uma em andamento não cria outra: o `INSERT` conflita e o pedido responde com o estado real (202, `in-progress`).

**Estado do vídeo derivado** (campo `Video.transcription`): em andamento ⇒ `in-progress`; senão, se a última tentativa falhou ⇒ `failed`; senão, se há vigente ⇒ `ready`; senão `not-requested`. `readable` = existe vigente. `readyAt` = `ready_at` da vigente. `canStart` = vídeo `ready` ∧ sem tentativa em andamento ∧ última falha não é `duration-limit` nem `size-limit`. Vídeo que não está `ready` é sempre `not-requested`, `readable: false`, `canStart: false`. Refazer inicia nova tentativa **sem tocar** a vigente (RN-TL07); só quando a nova fica `ready` a troca acontece, numa transação: nova `is_current`, antiga deixa de ser, conteúdo antigo apagado.

### Pipeline do worker (uma Transcrição)

1. **Pré-checagem local.** Se `Video.DurationSeconds` > 7200, falha na hora com `duration-limit`, sem extrair nada. O serviço continua autoridade para o caso limítrofe.
2. **Extração do áudio** (concessão renovada como em `PrepareVideo`):
   - Lê `master.m3u8` do prefixo `hls/` do vídeo e escolhe a variante de **menor `BANDWIDTH`**. Não usa a lista fixa de nomes de `PlaylistRewriter` (ver Riscos).
   - Baixa a playlist da variante e os segmentos para `{WorkDirectory}/transcription/{transcriptionId}/`. Valida que cada linha de recurso casa com `^[A-Za-z0-9]+/segment_[0-9]+\.ts$` e que a playlist não tem URI absoluta (guarda contra caminho arbitrário).
   - Obtém a chave por `IVideoKeyProtector.Unprotect` (a mesma custódia de `GetPlaybackResource`), grava `key.bin` (somente dono) no diretório e troca na cópia local da playlist o marcador `c4c-key:{videoId}` pelo caminho do arquivo. Nada disso vai a log.
   - Roda o ffmpeg com `-allowed_extensions ALL -protocol_whitelist file,crypto`, `-vn -ac 1 -ar 16000 -c:a aac -b:a 48k -movflags +faststart`, 1 thread. Evidência de viabilidade em 2026-10-12: com ffmpeg 9.0.1 a extração de um HLS AES-128 de 480p, com o marcador reescrito, gerou `.m4a` mono 16 kHz de ~6 KB/s (~43 MB para 2 h). A imagem usa o ffmpeg do Debian; a primeira task roda o mesmo comando na imagem real.
   - Sobe o `.m4a` para `{tenantId}/{videoId}/transcription/{transcriptionId}/audio.m4a` (fora de `hls/`, logo fora da política de distribuição por prefixo da ADR-0014) e **apaga o diretório de trabalho inteiro** (inclui a chave) com sucesso ou falha.
3. **Envio.** Gera a URL pré-assinada GET do áudio (90 min, em memória, nunca persistida nem logada) e faz `POST /v1/transcriptions` com `X-API-Key`, `Idempotency-Key = c4c-{transcriptionId}-{n}` (n = número do envio) e corpo `{sourceUrl, clientReference = transcriptionId}`. Grava `whisper_job_id`, `submitted_at`, `deadline_at` e passa a `waiting`.
4. **Espera e conciliação.** Consulta `GET /v1/transcriptions/{jobId}` quando `next_action_at` vence ou `signaled_at` é marcado. Cadência: a cada 60 s nos 10 primeiros minutos, a cada 5 min até 2 h, depois a cada 15 min. Quando o job sai de `downloading`, **apaga o áudio**.
5. **Cópia.** Em `completed`, busca o resultado, valida e aprova (abaixo), grava conteúdo e vigente numa transação, apaga o áudio se ainda existir. Em `failed`, mapeia o motivo (abaixo).
6. **Prazo.** Passado `deadline_at` sem estado terminal, termina em `failed` com `timed-out`.

**Validação do resultado.** `schemaVersion` diferente de 1 ⇒ falha `processing-failed` (nunca adivinhar). `text` de cada trecho é aparado (o serviço devolve espaço inicial); trecho vazio é descartado; `startMs ≥ 0` e `endMs ≥ startMs`; ordena por `startMs`; limite de **20.000 trechos e 8 MiB** de JSON, acima disso `processing-failed`. Idioma: guarda o que veio; o contrato diz `pt-BR`.

**Política de erro e nova tentativa.**

| Situação | Resultado | Nova tentativa |
|---|---|---|
| Vídeo > 7200 s (local) ou `MEDIA_DURATION_LIMIT_EXCEEDED` | `failed` / `duration-limit` | Nenhuma; `canStart: false` |
| `413` no envio ou `MEDIA_SIZE_LIMIT_EXCEEDED` | `failed` / `size-limit` | Nenhuma; `canStart: false` |
| Falha de rede, timeout, 429, 5xx, 401, 403, 422 no envio | continua `queued` com atraso 1, 5, 15 e 30 min | Até 5 falhas de envio; depois `failed` / `service-unavailable` (e sinal próprio de integração rejeitada) |
| Job `failed` com `SOURCE_UNAVAILABLE` | volta a `queued` (reextrai o áudio, novo envio) | Até 3 envios (atraso 5 e 30 min); depois `failed` / `service-unavailable` |
| Resultado expirou (404 após `completed`) | trata como o item acima | idem |
| Job `failed` com `UNSUPPORTED_MEDIA_FORMAT` ou `TRANSCRIPTION_FAILED`; ffmpeg falha; resultado inválido | `failed` / `processing-failed` | Extração: até 3 tentativas (1 e 5 min); o resto, nenhuma |
| `deadline_at` vencido | `failed` / `timed-out` | Nenhuma (Refazer manual) |

`deadline_at` = `submitted_at` + **30 h** (acima do SLO p95 de 24 h do serviço; configurável; a calibrar no piloto).

**Fluxo de dados sensíveis (regra 12).**

| Dado | Criado | Copiado/persistido | Lido | Descartado |
|---|---|---|---|---|
| Chave AES do vídeo | Já existe (ADR-0006), cifrada no banco | Só em `key.bin` (0600) no diretório temporário do worker, durante a extração; nunca em objeto, mensagem, log, span, resposta | ffmpeg, no mesmo contêiner | Diretório apagado em `finally`, com sucesso ou falha; chave zerada na memória |
| Áudio extraído | Worker | Prefixo privado `…/transcription/{id}/` | Serviço de transcrição, por URL pré-assinada | Apagado quando o job sai de `downloading`; terminal; varredura de sobras |
| URL pré-assinada do áudio | No envio, em memória | **Não persiste**; corpo do POST; sem log do corpo | Serviço de transcrição | Vence em 90 min; o objeto some antes (acima). Em HTTP de rede privada (dev) trafega sem TLS; a janela é curta |
| Texto da aula | Serviço; copiado pelo worker | Tabela de conteúdo (`jsonb`) no banco do `media`; sem cache compartilhado, sem objeto público | API do `media` ao professor/aluno com autorização | Substituído em Refazer; não vai a log, métrica, span nem URL |
| `X-API-Key` e segredo do webhook | Operador (serviço de transcrição) | Variáveis de ambiente do `media`; nunca no repositório | Cliente HTTP e receptor | Rotação por configuração |

### Bloco Backend (`media`)

- **Domínio.** `Transcription` (tentativa) com a máquina de estados acima e as regras RN-M18/M20/M24 como invariantes de entidade (transições válidas, motivo de falha entre os cinco, `can_restart`). `TranscriptionContent` (trechos). O domínio não conhece o serviço externo.
- **Aplicação.** Casos de uso: `RequestVideoTranscription`, `GetVideoTranscription`, `GetLessonTranscription`, `GetPlaybackCaptions`, `ReceiveTranscriptionEvent` e o fluxo do worker (`ProcessTranscription`). Porta nova de **serviço de transcrição** (criar, consultar, buscar resultado) com a camada anticorrupção como adaptador; portas existentes ganham operações de ler URL assinada de objeto e subir um arquivo (`IMediaStoragePort`). Gerador de WebVTT é função pura da camada de aplicação.
- **Onde a regra de negócio vive.** Elegibilidade (`canStart`), derivação do estado do produto, mapa de motivos e cadência de consulta ficam na aplicação/domínio; o adaptador só traduz.
- **Transação e consistência.** (a) Disparo automático: a linha nasce na mesma transação de `Video.MarkReady` + outbox (`PrepareVideo.cs:211-247`); não há chamada remota nesse caminho (RN-M19). (b) Fim bem-sucedido: conteúdo + troca de vigente + estado numa transação, protegida pela concessão. (c) Receptor: evento + `signaled_at` numa transação.
- **Autorização.** Professor: política existente de `midia.enviar` (`MediaAuthorization.PolicyName`). Aluno: política de reprodução existente (`PlaybackPolicyName`), mesma de `openPlaybackSession`. O receptor do aviso é anônimo e protegido por assinatura. Isolamento por escola pelo filtro de consulta existente; o worker e o receptor usam `IgnoreQueryFilters` e carregam o `tenant_id` da própria linha.
- **Leitura da aula (RN-M10 passos 1 e 2).** Reutiliza `FindLessonAsync` (Referência de Uso, 404 antes de Matrícula) e `IAccessDecisionClient` (403, 503 falha fechada) como `OpenPlaybackSession.cs:14-35`. Depois da decisão positiva, vídeo sem vigente (não pronto, em andamento, falhou, desligada) responde 200 `unavailable`. Em `ready` devolve os trechos da vigente.
- **Legenda por sessão.** Reutiliza `FindSessionAsync` (404 desconhecida/outro aluno, 410 vencida, como `GetPlaybackResource.cs:13-17`). Não consulta Matrícula de novo. Sem vigente: 404 `CAPTIONS_NOT_AVAILABLE`.
- **WebVTT.** `text/vtt; charset=utf-8`, cabeçalho `WEBVTT`, um cue por trecho. Escapar `&`, `<`, `>` no texto, remover `-->` e quebras que formariam linha em branco, tempos `hh:mm:ss.mmm`. Trecho com mais de 84 caracteres é dividido em cues de até 2 linhas de ~42 caracteres, com tempos proporcionais aos caracteres. Fim nunca passa do início do cue seguinte (sem sobreposição); duração mínima de 500 ms. Gerado por requisição a partir do `jsonb`; sem cache.
- **Receptor do aviso** (`receiveTranscriptionEvent`, `/webhooks/v1/transcription-events`, só no papel `api`, só mapeado se há segredo configurado): lê o corpo bruto (limite 16 KiB), exige `webhook-id`, `webhook-timestamp` (±300 s) e `webhook-signature`; calcula HMAC-SHA256 de `id.timestamp.corpo` com cada segredo configurado (decodifica o `whsec_` base64; aceita **dois** segredos para rotação), compara em tempo constante com cada assinatura `v1,` do cabeçalho; falha ⇒ 401 sem eco. Insere o evento por `webhook-id` (chave primária; conflito ⇒ já tratado); se `clientReference` é uma tentativa em andamento e `jobId` confere (ou a tentativa ainda não gravou o job), marca `signaled_at` e `next_action_at = agora`. Resposta **204 em todos os casos aceitos**, inclusive duplicado, desconhecido ou sem `clientReference`; **nunca 410**, nunca 3xx; falha ao gravar ⇒ 503. O corpo nunca vai a log. Eventos com mais de 7 dias são apagados pela manutenção do worker.
- **Concorrência de workers.** Reivindicação por `FOR UPDATE SKIP LOCKED` ordenada por `requested_at`; concorrência configurável (padrão 1). Orçamento de disco antes de reivindicar extração (mínimo livre de 2 GiB configurável), como `VideoPreparationWorker.cs:58`.
- **Manutenção do worker** (a cada ciclo): recuperar concessões vencidas; apagar áudio de tentativa terminal ou de job que saiu de `downloading` (sobras); apagar eventos de aviso antigos; apagar tentativas `failed` antigas quando outra tentativa começa.

### Bloco Frontend

**`admin-spa` — tela Vídeos.** Estado e ações moram no servidor (`Video.transcription` na listagem); nenhuma cópia local. Mostra o estado ao lado do estado da preparação e das tags; `canStart` decide **Transcrever** (`not-requested`) ou **Refazer** (`ready` ou `failed`); com `readable` oferece abrir o texto. A reconsulta automática de `useVideos` (`api/get-videos.ts:63`, hoje só para `received`/`preparing`) passa a incluir `transcription.status === 'in-progress'`. Motivos de falha em texto de professor (os cinco do PRD). A leitura é somente leitura: lista de trechos com tempo, sem busca, sem clique para vídeo, sem edição, e avisa quando há uma nova em andamento. Sem log nem telemetria do texto.

**`student-spa` — página da aula e player.**
- *Transcrição.* Um `GET` por abertura da página (em paralelo a `getStudentLesson` e à abertura da sessão), sem cache entre navegações (`staleTime` 0, `gcTime` 0, no molde de `get-student-lesson.ts`) e **sem armazenamento persistente** do texto. `status: unavailable` mostra a mensagem neutra do PRD; 403/503 seguem as mensagens já existentes da aula; 401 segue o fluxo de sessão.
- *Busca.* Roda no cliente: normaliza cada trecho uma vez (minúsculas, sem acentos, sem pontuação), busca por "contém", destaca a ocorrência mapeando posições da forma normalizada de volta ao texto original, conta ocorrências, anterior/próxima circular. Texto só com espaços ⇒ busca vazia. Renderiza texto como nós de texto do React (nunca HTML) para o destaque.
- *Acompanhar e clicar.* O painel precisa de dois pontos do player: o tempo atual e um comando "ir ao tempo e tocar" (RN-TL02). A sessão já abre ao carregar a página (`use-protected-playback.ts` abre quando o progresso está carregado); o comando aplica o tempo e toca se a sessão vale e, se a sessão ainda não existe ou venceu, define o ponto inicial e usa o caminho normal de abertura/retomada (`togglePlayback`/`retry`). O acompanhamento automático pausa quando o aluno rola manualmente e volta com "ir para o trecho atual".
- *Legenda.* Faixa de texto do elemento de vídeo alimentada pelo WebVTT da sessão buscado com a sessão autenticada do aluno (corpo em memória, sem endereço persistente). O controle só existe se a transcrição está `ready`. Preferência ligada/desligada em `localStorage` do dispositivo (padrão desligada; leitura e escrita protegidas por `try/catch`; sem dado do aluno). A legenda para quando a reprodução para por sessão não renovada (`stop()`), sem texto novo. Quanto ao desenho da legenda (faixa nativa do navegador ou camada própria) e à convivência com a marca d'água em tela cheia: decisão do design aprovado (EN-01); o ponto de partida é a faixa nativa, que fica em posição diferente das zonas da marca d'água (`protected-video-player.tsx`).
- Biblioteca de fetching, estado, estrutura de pastas e testes seguem a skill `react`; aqui só o desvio: **nenhum texto de aula em cache persistente, telemetria ou URL**.

### Configuração e operação

Parâmetros novos do `media` (nomes definitivos na task; valores padrão aqui):

| Parâmetro | Papel | Padrão / observação |
|---|---|---|
| `Transcription__Enabled` | api, worker | `true`. `false` nos ambientes que o serviço não alcança (ver Questões) |
| `Transcription__Service__BaseAddress` | worker | `https://whisper.lab.tasso.dev.br/v1` (a confirmar o prefixo `/v1` do deploy) |
| `Transcription__Service__ApiKey` | worker | **Segredo**, por ambiente, emitido pelo operador do serviço |
| `Transcription__Webhook__Secrets__0/1` | api | **Segredo** `whsec_…` atual e anterior (rotação); sem segredo, o receptor não é mapeado |
| `AwsMedia__EndpointTranscription` | worker | Endpoint S3 que o **serviço** alcança, para assinar a URL; padrão `EndpointPublic`. No MinIO remoto: `http://192.168.0.5:9000`; em produção, o endpoint do AWS S3 |
| `Transcription__SourceUrlLifetimeMinutes` | worker | 90 (mínimo contratual 60) |
| `Transcription__MaxWaitHours`, `__MaxConcurrency`, `__MaxSubmissions`, cadências | worker | 30; 1; 3; conforme acima |

Replicar em: `docker-compose.yml` (pilha local completa, `Enabled=false`), `docker-compose.remote.yml` (**obrigatório pela regra do projeto**: override com `AwsMedia__EndpointTranscription` apontando para o MinIO remoto, `Transcription__Service__*` e a chave via `${…:?}`), `docker-compose.komodo.apis.yml` (dev), `docker-compose.stable.yml`/`docker-compose.stable.infra.yml` (estável). O `media-worker` já tem o volume de trabalho (`docker-compose.yml:333`); `scripts/generate-local-env.sh` não gera a chave do serviço (ela é emitida pelo operador do serviço).

**No serviço de transcrição (fora deste repositório, por ambiente):**

| Item | Valor / regra |
|---|---|
| `TRUSTED_PRIVATE_SOURCES` | Origem do áudio quando é HTTP privado. Entrada `http://host:porta`. MinIO remoto de desenvolvimento: `http://192.168.0.5:9000`; MinIO do estável: `http://192.168.0.11:9000` (porta publicada em `docker-compose.stable.infra.yml:110`). Em produção com AWS S3 HTTPS público, sem entrada |
| `TRUSTED_PRIVATE_WEBHOOKS` | Receptor do `media`, `http://host:porta` da API do `media` onde o serviço alcança. Só nos ambientes com receptor alcançável; os demais ficam só com a consulta periódica |
| Conta/credencial | **Uma conta por ambiente** (chave da API, destino e segredo do webhook são por conta). O destino é cadastrado pelo operador (CLI do serviço), não enviado por pedido |
| Estado atual | As duas listas **ainda não estão configuradas** no deploy. Sem elas não há validação ponta a ponta com origem privada |

---

## Mapa de Fatias Verticais

Cada fatia atravessa as pontas necessárias (`media` → BFF → SPA). Onde há tela, a parte de SPA fica **bloqueada por EN-01** (design ASCII e Figma aprovados); as partes de `media` e BFF seguem e são verificáveis por HTTP. Não há fatia "só infra".

**Dependência externa de calendário.** A implementação aguarda `CAP-006` sair de `tasks` (a fatia de tags de vídeo altera os mesmos `Video`, tela Vídeos e contratos 1.2.0), conforme `flow-state.json` NA26.

### V-01: Professor transcreve um vídeo pronto e vê a transcrição ficar pronta

- **Cobre:** RF-02 (Transcrever, estado, atualização automática), RF-07 (cópia própria, apagar áudio, conciliação por consulta), RN-M18, RN-M20, RN-M23, RN-TL06 (estado), US "transcrever vídeo antigo".
- **Entrada / gatilho:** o professor aciona **Transcrever** num vídeo pronto sem transcrição (`requestVideoTranscription`).
- **Processamento:** insere a tentativa `queued` (conflito ⇒ devolve o estado real, sem segunda); o worker executa o pipeline completo (extração do áudio, envio, consulta periódica, cópia do texto aparado, apagar áudio). `Video.transcription` aparece em `listVideos`, `getVideo`, `updateVideoTitle`, `replaceVideoTags`. Inclui a entidade, a migração (ferramenta do EF Core, nunca à mão), a camada anticorrupção do serviço, a configuração e a réplica nos compose.
- **Saída observável:** a tela Vídeos mostra `not-requested` → `in-progress` → `ready` (`readable: true`, `readyAt`), atualizando sozinha; `ready` ainda sem leitura (V-05). Nenhum objeto de áudio resta ao fim.
- **Evidência / checkpoint:** (1) integração no `media` com serviço simulado (contrato do serviço) cobrindo o pipeline e a extração real com ffmpeg da imagem sobre HLS cifrado gerado pelo próprio transcodificador; (2) smoke fim a fim contra `https://whisper.lab.tasso.dev.br` com um vídeo curto real (exige EN-02 para origem privada); (3) varredura do prefixo `transcription/` vazia ao final.
- **Bloqueado por:** EN-01 (parte SPA), EN-02 (só o smoke real), `CAP-006` fora de `tasks`.

### V-02: Todo vídeo novo é transcrito sem ação de ninguém

- **Cobre:** RF-01, RN-M19, RN-M20, RN-M25.
- **Entrada / gatilho:** a preparação termina com sucesso (`CompletePreparationAsync`).
- **Processamento:** a mesma transação que marca `ready` e grava o fato `midia.ativo-pronto.v1` insere a tentativa `queued` (origem automática). Aviso duplicado não cria duas (índice). Vídeo que falhou na preparação não gera tentativa. Serviço fora do ar não altera o vídeo: continua pronto, vinculável, reproduzível; a tentativa conclui depois ou termina `failed`.
- **Saída observável:** o vídeo fica `ready` no mesmo instante de hoje e a tela mostra "transcrevendo".
- **Evidência / checkpoint:** teste de integração da preparação: vídeo `ready` + uma tentativa `queued`; reentrega do mesmo fato não duplica; serviço simulado indisponível não muda `Video.Status`; medição do tempo até pronto do vídeo (`MediaTelemetry.VideoTimeToReady`) sem piora.
- **Bloqueado por:** V-01.

### V-03: O aviso do serviço acelera o resultado sem ser necessário

- **Cobre:** RF-07 (aviso perdido, deduplicação), RN-M23, contrato `receiveTranscriptionEvent`.
- **Entrada / gatilho:** `POST /webhooks/v1/transcription-events` do serviço.
- **Processamento:** o receptor descrito no bloco backend; o worker, ao ver `signaled_at`, consulta já.
- **Saída observável:** a transcrição fica pronta logo após o término, sem esperar a próxima consulta; duplicata, evento de job desconhecido ou de tentativa encerrada devolvem 204 sem efeito; assinatura inválida 401.
- **Evidência / checkpoint:** testes do receptor (assinatura válida, inválida, ausente, fora de ±300 s, dois segredos em rotação, duplicata, job desconhecido, 503 em falha de gravação, nunca 410); ambiente com webhook configurado mostra o tempo até pronta menor que a cadência de consulta; **ambiente sem receptor** (dev remoto) fecha só por consulta.
- **Bloqueado por:** V-01. Validação ponta a ponta exige `TRUSTED_PRIVATE_WEBHOOKS` (EN-02).

### V-04: Falhas aparecem com motivo e o professor refaz sem perder a transcrição vigente

- **Cobre:** RF-02 (falha, Refazer, `canStart`, 409), RF-07 (limite de espera), RN-M18, RN-M24, RN-TL07, RN-TL06 (motivo).
- **Entrada / gatilho:** falha em qualquer ponto do pipeline; o professor aciona **Refazer**.
- **Processamento:** política de erro e nova tentativa da tabela acima; os cinco motivos; `duration-limit` e `size-limit` com `canStart: false` e 409 `TRANSCRIPTION_NOT_RETRYABLE`; 409 `VIDEO_NOT_READY` para vídeo não pronto; **Refazer mantém a vigente legível** (`status: in-progress`, `readable: true`) até a nova ficar pronta, e uma refeita que falha deixa `status: failed`, `readable: true`, vigente intacta; a troca ocorre numa transação.
- **Saída observável:** a tela mostra o motivo em texto de professor, a ação certa (ou nenhuma, nos limites) e o texto vigente continua acessível ao aluno durante o Refazer.
- **Evidência / checkpoint:** integração com serviço simulado para cada linha da política (inclusive resultado expirado e `schemaVersion` desconhecida); concorrência de dois pedidos simultâneos (uma tentativa); troca atômica da vigente sob falha no meio.
- **Bloqueado por:** V-01; para ver a vigente do aluno em teste de ponta a ponta, V-06.

### V-05: Professor lê a transcrição (somente leitura)

- **Cobre:** RF-08, RN-M27, RN-TL08.
- **Entrada / gatilho:** o professor abre a transcrição de um vídeo com `readable: true` (`getVideoTranscription`).
- **Processamento:** devolve a vigente com `revision`; 409 `TRANSCRIPTION_NOT_READY` sem vigente; 403 sem `midia.enviar`; 404 para outra escola; `Cache-Control: private, no-store`; não abre sessão nem consulta Matrícula.
- **Saída observável:** painel de leitura com tempo e texto por trecho, sem edição, com aviso de refeita em andamento.
- **Evidência / checkpoint:** autorização (sem permissão, outra escola), leitura durante Refazer devolve a vigente, nenhum texto em log ou URL (inspeção de logs do teste).
- **Bloqueado por:** V-01, EN-01 (SPA).

### V-06: Aluno lê, pesquisa e clica no texto da aula

- **Cobre:** RF-04, RF-05, RF-06 (parte do texto), RN-M21, RN-M22, RN-TL01, RN-TL02, RN-TL03, RN-TL05.
- **Entrada / gatilho:** o aluno abre a página da aula; digita na busca; clica num trecho.
- **Processamento:** `getLessonTranscription` na ordem 404 (Referência de Uso) → 403/503 (Matrícula) → estado; busca, destaque, navegação entre ocorrências e acompanhamento no cliente; clique leva o vídeo ao tempo e toca, abrindo a sessão pelo caminho normal se preciso.
- **Saída observável:** transcrição completa com tempo; busca que ignora maiúsculas, acentos e pontuação; contagem e anterior/próxima; mensagem neutra quando não há texto pronto; aula com vídeo trocado na publicação seguinte mostra o texto do vídeo novo.
- **Evidência / checkpoint:** HTTP: visitante 401; sem direito 403; sem resposta de Matrícula 503 sem texto; aula de outra escola/fora de versão 404; aluno com direito ao curso A pedindo aula do B recebe o 404/403 da aula (o vídeo nunca é informado); `unavailable` em andamento/falha; `Cache-Control: private, no-store`; resposta sem e-mail. SPA: busca com "introducao" acha "Introdução", texto só com espaços, 3.000 trechos sem atraso perceptível, clique antes de sessão ativa.
- **Bloqueado por:** V-01, EN-01.

### V-07: Aluno liga a legenda no player

- **Cobre:** RF-03, RF-06 (parte da legenda), RN-M21, RN-TL04, RN-TL05.
- **Entrada / gatilho:** o aluno liga o controle de legenda; a sessão renova ou deixa de renovar.
- **Processamento:** `getPlaybackCaptions` (WebVTT da sessão válida) alimenta a faixa do player; preferência por dispositivo; legenda para junto com a reprodução quando a sessão não renova.
- **Saída observável:** legenda sincronizada com pausa, avanço, retrocesso e velocidade, em tela cheia, sem cobrir controles nem a marca d'água; ligada nas próximas aulas do mesmo dispositivo; desligada em outro dispositivo; sem controle quando não há transcrição pronta.
- **Evidência / checkpoint:** HTTP: sessão de outro aluno 404, vencida 410, sem transcrição 404 `CAPTIONS_NOT_AVAILABLE`, `text/vtt` com `Cache-Control: private, no-store`; unidade do gerador (escape, divisão de cue longa, sem sobreposição, cabeçalho); SPA: renovação sem piscar, negada na renovação para a legenda; verificação manual em tela cheia nos navegadores da matriz do player.
- **Bloqueado por:** V-01, EN-01 (V-06 recomendada antes, por dividirem a tela da aula).

### V-08: A operação enxerga os sinais da transcrição

- **Cobre:** RF-07 (sinais), RN-M26.
- **Entrada / gatilho:** a operação abre o painel de mídia no Kibana.
- **Processamento:** instrumentos e spans abaixo; painel e alertas como saved objects versionados (ADR-0008) no mesmo script de importação.
- **Saída observável:** painel mostra tempo entre pronto e transcrição pronta, taxa de falha por motivo, duração transcrita e fila em espera (com idade da mais antiga), sem texto de aula nem dado pessoal.
- **Evidência / checkpoint:** teste de métricas no padrão de `MediaPreparationMetricsTests`; `import_observabilidade.py` valida e importa; verificação de que nenhum atributo contém URL assinada ou texto.
- **Bloqueado por:** V-01 (instrumentos nascem com cada fatia; esta fecha painel e alertas).

### Habilitadores inevitáveis

| Habilitador | Por que não cabe numa fatia | Menor escopo | Primeira fatia desbloqueada |
|---|---|---|---|
| EN-01 Design aprovado: wireframe ASCII e depois Figma da **tela Vídeos** (estado, ações, leitura) e da **página da aula** (player com legenda, painel, busca) | Preferência do projeto: design aprovado antes de qualquer código de tela. É decisão do usuário, não derivável | Plano de design por tela; **nenhum código**. Fora desta TechSpec | V-01 (SPA), V-05, V-06, V-07 |
| EN-02 Configuração operacional do serviço de transcrição por ambiente | Pertence a outro repositório e ao operador; sem ela a validação ponta a ponta com origem/receptor privados é impossível, mas o código e os testes com serviço simulado não dependem dela | Conta/credencial por ambiente; `TRUSTED_PRIVATE_SOURCES`/`TRUSTED_PRIVATE_WEBHOOKS`; segredo do webhook entregue ao `media`; cadastro do destino | Smoke real de V-01 e V-03 |

---

## Contratos e Fronteiras

### Mapeamento do contrato de API

Os schemas e erros vivem nos OpenAPI da pasta; esta spec não os duplica. Camadas: endpoint → caso de uso → repositório/porta (nomes definitivos pela skill `dotnet` na task).

| operationId | Fronteira | Fatia | Caminho de implementação |
|---|---|---|---|
| `listVideos`, `getVideo` (campo `transcription`) e `*Internal`; `updateVideoTitle`, `replaceVideoTags` devolvem o mesmo `Video` | `admin-spa` → `bff-admin` → `media` | V-01 | Endpoints de vídeo existentes → `ListVideos`/`GetVideo`/`UpdateVideoTitle`/`ReplaceVideoTags` → consulta em lote do estado da transcrição (como as tags) |
| `requestVideoTranscription` / `…Internal` | idem | V-01, V-04 | `VideoEndpoints` → `RequestVideoTranscription` (idempotência existente por `Idempotency-Key`, 24 h) → repositório da fila |
| `getVideoTranscription` / `…Internal` | idem | V-05 | → `GetVideoTranscription` → conteúdo da vigente |
| `getLessonTranscription` / `…Internal` | `student-spa` → `bff-student` → `media` | V-06 | `PlaybackSessionEndpoints`-like → `GetLessonTranscription` → `IPlaybackRepository.FindLessonAsync` + `IAccessDecisionClient` + conteúdo |
| `getPlaybackCaptions` / `…Internal` | idem | V-07 | → `GetPlaybackCaptions` → sessão + conteúdo + gerador WebVTT |
| `receiveTranscriptionEvent` | serviço de transcrição → `media` | V-03 | Endpoint anônimo dedicado → `ReceiveTranscriptionEvent` → tabela de eventos + marca na tentativa |

**Validações além do contrato.**

| operationId | Regra | Camada |
|---|---|---|
| `requestVideoTranscription` | Vídeo precisa estar `ready` (409 `VIDEO_NOT_READY`); última falha em `duration-limit`/`size-limit` ⇒ 409 `TRANSCRIPTION_NOT_RETRYABLE`; em andamento ⇒ 202 com o estado real | domínio/aplicação |
| `requestVideoTranscription` | Ator sem `midia.enviar` ⇒ 403; vídeo de outra escola ⇒ 404 (G07) | política de autorização/filtro de escola |
| `getLessonTranscription` | 404 antes de consultar Matrícula; sem resposta de Matrícula ⇒ 503, nunca "sem acesso"; decisão pode ser reaproveitada por até 30 s como na sessão | aplicação |
| `getPlaybackCaptions` | Não consulta Matrícula; sessão vencida ⇒ 410; de outro aluno/escola ⇒ 404 de sessão | aplicação |
| `receiveTranscriptionEvent` | Assinatura, janela de ±300 s, tamanho de corpo, dedupe por `webhook-id` | endpoint/aplicação |

**Exceção → resposta HTTP.** Reutiliza `MediaApiException` (código, status, mensagem) como o resto do `media`; os códigos novos são os do contrato (`VIDEO_NOT_READY`, `TRANSCRIPTION_NOT_RETRYABLE`, `TRANSCRIPTION_NOT_READY`, `CAPTIONS_NOT_AVAILABLE`, `SIGNATURE_INVALID`, `TEMPORARILY_UNAVAILABLE`). Respostas de erro nunca ecoam texto de aula nem corpo de aviso.

**Diferenças para o acordo anterior (versões de entrada).** Todas aditivas: `bff-admin` vídeos 1.2.0 → 1.3.0; `media` interno 1.2.0 → 1.3.0; `bff-student` aula 1.1.0 → 1.2.0; `media` webhook 1.0.0 (novo). Pela regra do `contracts/README.md`, **cada task acrescenta aditivamente a `contracts/` o que implementa**, com a validação Spectral; consolidação (versão, linha de origem, tabela do README, `text/vtt` na exceção C-08, linha de `contracts/media/openapi-webhook.yaml`) no fim do PRD. O campo `Video.transcription` é obrigatório na resposta: testes que validam `Video` contra o schema vigente (`additionalProperties: false`) falham até a promoção do schema na **mesma task**.

### Mapeamento do serviço de transcrição (dependência consumida)

| Operação dele | Uso no `media` | Comportamento a implementar | Evidência |
|---|---|---|---|
| `createTranscription` | Envio | `X-API-Key`; `Idempotency-Key` por envio; `{sourceUrl, clientReference}`; 202 ⇒ grava `jobId`; **o cliente segue o comportamento do código** (aceita `http://` de destino em lista), não o `pattern: '^https://'` documental; mapa 413/401/403/422/5xx conforme tabela | Teste de contrato do cliente contra um simulador do OpenAPI dele |
| `getTranscription` | Conciliação, motivo | Estados `downloading`, `queued`, `processing`, `completed`, `failed`; `failure.code` ⇒ cinco motivos; estado desconhecido ⇒ trata como em andamento e registra | idem |
| `getTranscriptionResult` | Cópia | `schemaVersion` 1; aparar `text`; 409 ainda não; 404 após 24 h ⇒ política de erro | idem, incluindo versão desconhecida e resultado expirado |
| Webhook terminal | Gatilho | Receptor acima | testes do receptor |

**Idempotência no envio.** A janela de idempotência do serviço é de 120 s. Reenvio de um POST ambíguo (sem resposta) depois disso pode criar um segundo job; é aceito: só vale o `jobId` gravado, e aviso de job que não é o gravado responde 204 e é ignorado. `clientReference` é o id da tentativa (opaco, sem título, texto, e-mail ou pessoa).

### Entidades do domínio

| Entidade do Domain Doc | Representação técnica |
|---|---|
| Transcrição (tentativa e vigente) | Tabela `video_transcriptions` no schema do `media`: id da tentativa (também `clientReference`), `tenant_id`, `video_id`, origem (automática/solicitada), solicitante (conta, só quando manual), `status` interno, `is_current`, motivo de falha, contadores (extrações, envios, falhas de envio), `whisper_job_id`, `submitted_at`, `deadline_at`, `next_action_at`, `signaled_at`, concessão (`lease_id`, `lease_until`), `requested_at`, `ready_at`, duração e quantidade de trechos, marca de áudio apagado. Índices únicos parciais: uma em andamento por vídeo; uma vigente por vídeo. Índice de reivindicação por `(status, next_action_at)` |
| Trecho / Legenda | Tabela `video_transcription_contents` (1:1 com a tentativa `ready`): idioma, `durationMs`, `jsonb` com `[{startMs, endMs, text}]`. A Legenda não é artefato: é derivada na hora |
| Aviso recebido | Tabela de eventos por `webhook-id` (chave primária), `job_id`, `status`, `received_at`; sem corpo |

`revision` (T-09): `tr_` + 8 últimos hexadecimais do id da tentativa vigente; opaco, igual para todos.

### Interfaces entre fatias

- **Estado `Video.transcription`** (V-01) é a interface que V-02, V-04 e V-05 produzem/consomem e que a tela Vídeos lê; o mapeamento de falha (cinco motivos) é compartilhado.
- **Conteúdo da vigente** (V-01) é lido por V-05, V-06 e V-07; um único repositório de leitura.
- **Player ↔ painel** (V-06/V-07, `student-spa`): o painel recebe o tempo atual e um comando "ir ao tempo e tocar" do player; a legenda recebe a sessão corrente. Qualquer forma de expor isso é decisão da skill `react`, desde que sirva às duas fatias.

---

## Arquivos a Modificar e a Referenciar

### A modificar

| Caminho | Fatia | Alteração |
|---|---|---|
| `src/media/src/CodeForCoders.Media.Application/UseCases/Videos/PrepareVideo/PrepareVideo.cs` | V-02 | Inserir a tentativa automática em `CompletePreparationAsync` (linhas 211-247), na transação do `MarkReady` |
| `src/media/src/CodeForCoders.Media.Application/UseCases/Videos/VideoOutput.cs` e casos de uso `ListVideos`, `GetVideo`, `UpdateVideoTitle`, `ReplaceVideoTags` | V-01 | Campo `transcription` em `Video` |
| `src/media/src/CodeForCoders.Media.Api/Endpoints/VideoEndpoints.cs`, `PlaybackSessionEndpoints.cs`, `Extensions/EndpointExtensions.cs` | V-01, V-05, V-06, V-07, V-03 | Rotas novas; mapear o receptor |
| `src/media/src/CodeForCoders.Media.Application/Interfaces/IMediaStoragePort.cs` e `Infra.Data/Adapters/S3MediaStorageAdapter.cs`, `S3MediaClientPair.cs`, `Configuration/AwsMediaOptions.cs` | V-01 | URL de leitura assinada de objeto (endpoint `EndpointTranscription`), subir arquivo |
| `src/media/src/CodeForCoders.Media.Infra.Data/MediaDbContext.cs`, `DependencyInjection.cs`, `Migrations/` | V-01 | Entidades, filtros de escola, migração gerada pelo EF Core |
| `src/media/src/CodeForCoders.Media.Application/DependencyInjection.cs`, `Infra.Messaging/DependencyInjection.cs` | V-01 | Registro de casos de uso, adaptador do serviço e do worker (papel `worker`) |
| `src/media/src/CodeForCoders.Media.Application/Common/MediaTelemetry.cs`, `Infra.Messaging/MediaVolumeMetricsWorker.cs` | V-08 | Instrumentos; fila e idade da mais antiga |
| `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Endpoints/VideoLibraryEndpoints.cs`, `Clients/VideoLibraryClient.cs`, `Clients/IVideoLibraryClient.cs`, `ApiModels/VideoResponses.cs` | V-01, V-04, V-05 | Campo novo; duas operações |
| `src/bff-student/src/CodeForCoders.BffStudent.Api/Endpoints/PlaybackSessionEndpoints.cs` (e `StudentLessonEndpoints.cs` se for o melhor lugar) | V-06, V-07 | Duas leituras, repassando corpo, `Content-Type` e `Cache-Control` |
| `src/admin-spa/src/features/videos/api/get-videos.ts`, `components/videos-area-screen.tsx`, `components/video-status-badge.tsx` e testes MSW (`testing/handlers.ts`) | V-01, V-04, V-05 | Schema, estado, ações, leitura, reconsulta |
| `src/student-spa/src/features/student-lessons/components/student-lesson-screen.tsx`, `protected-video-player.tsx`, `hooks/use-protected-playback.ts` | V-06, V-07 | Painel, comando de tempo, faixa de legenda, parada junto com a sessão |
| `docker-compose.yml`, `docker-compose.remote.yml`, `docker-compose.komodo.apis.yml`, `docker-compose.stable.yml`, `docker-compose.stable.infra.yml` | V-01 | Variáveis novas do `media` e `media-worker`; **override remoto obrigatório** |
| `scripts/kibana/observabilidade-midia.ndjson`, `scripts/kibana/import_observabilidade.py` | V-08 | Painéis e alertas, via script (ADR-0008) |
| `contracts/media/openapi-internal.yaml`, `contracts/bff-admin/openapi-midia.yaml`, `contracts/bff-student/openapi-aula.yaml`, `contracts/.spectral-exception.yaml`, `contracts/README.md` | cada task / consolidação | Promoção aditiva por task; consolidação no fim |

### A referenciar (não alterar)

| Caminho | Por que consultar |
|---|---|
| `src/media/src/CodeForCoders.Media.Infra.Data/Videos/VideoPreparationRepository.cs:28-56` | Padrão de reivindicação com concessão e `FOR UPDATE SKIP LOCKED` |
| `src/media/src/CodeForCoders.Media.Infra.Messaging/VideoPreparationWorker.cs` e `FfmpegVideoTranscoder.cs:148-199` | Laço do worker, orçamento de disco, execução do ffmpeg; o áudio da variante é AAC 128k estéreo |
| `src/media/src/CodeForCoders.Media.Application/UseCases/PlaybackSessions/GetPlaybackResource/GetPlaybackResource.cs:18-23` | Como a chave é obtida e zerada |
| `src/media/src/CodeForCoders.Media.Application/UseCases/PlaybackSessions/OpenPlaybackSession/OpenPlaybackSession.cs:14-35` | Ordem 404 → Matrícula → resposta (RN-M10) |
| `src/media/src/CodeForCoders.Media.Application/UseCases/Videos/UpdateVideoTitle/UpdateVideoTitle.cs` | Padrão de idempotência por `Idempotency-Key` |
| `src/billing/src/CodeForCoders.Billing.Api/Endpoints/PaymentEndpoints.cs:12-31` e `contracts/billing/openapi-webhook.yaml` | Molde de endpoint de webhook (mudando o esquema de assinatura para Standard Webhooks) |
| `docs/adr/0006`, `0008`, `0014`, `0019`, `0020` | Custódia da chave, observabilidade como código, prefixo `hls/*`, runtime, esta decisão |
| `~/github-tassosgomes/whisper/tasks/prd-api-transcricao-assincrona/api-contract.yaml` e `docs/adr/adr-003.md` | Contrato e política de destinos privados do serviço |

---

## Análise de Impacto

| Componente | Tipo | Impacto e risco | Ação requerida |
|---|---|---|---|
| `media` — preparação | Modificado | Uma linha a mais na transação de "pronto". Risco: falha de inserção derrubar a preparação | Inserção simples, sem chamada remota; teste de que a Mídia não depende da Transcrição |
| `media` — banco | Modificado | Três tabelas novas e índices; texto completo de aulas no banco (centenas de KB por aula de 2 h, estimado, não medido) | Migração pelo EF Core; incluir no backup já existente; apagar o conteúdo antigo no Refazer |
| `media` — worker (CPU, disco, rede) | Modificado | Baixa ~1 GiB por 2 h a 480p e roda ffmpeg ao lado da preparação | Concorrência 1, orçamento de disco, 1 thread; fila e idade como sinais |
| Armazenamento de objetos | Modificado | Novo prefixo `…/transcription/…` fora de `hls/` | Apagar após o uso; varredura; regra de ciclo de vida do bucket como defesa adicional (opcional, ver Questões) |
| Contratos de `Video` | Modificado | Campo obrigatório novo; testes de resposta contra o schema vigente quebram | Promover o schema na mesma task |
| `bff-admin`, `bff-student` | Modificado | Repasse de `text/vtt` e JSON grande (centenas de KB, estimado) | Preservar `Content-Type`; `no-store`; limite de resposta de 8 MiB |
| `admin-spa`, `student-spa` | Modificado | Tela Vídeos e página da aula; hook do player compartilhado com reprodução e progresso | Testes de regressão de `playback-*.test.tsx`; design aprovado antes |
| Serviço de transcrição | Dependência | Carga em CPU, fila; contrato `1.0.0` | Camada anticorrupção; fila e idade como sinais; sem carga em massa |
| Telemetria/Kibana | Modificado | Instrumentos e painel novos | Nomes de instrumentos são contrato com os saved objects (ADR-0008) |

---

## Riscos e Preocupações

| Preocupação | Local (`arquivo:linha`) | Impacto | Mitigação |
|---|---|---|---|
| O original é apagado ao fim da preparação; qualquer solução que dependa dele falha para vídeos prontos | `src/media/src/CodeForCoders.Media.Application/UseCases/Videos/PrepareVideo/PrepareVideo.cs:183` | RN-M24 (toda mídia pronta é transcritível) | Fonte única é a Versão de Reprodução (ADR-0020) |
| Fonte da chave: o worker precisa decifrar o HLS; vazar a chave no diretório de trabalho quebra a proteção | `src/media/src/CodeForCoders.Media.Application/UseCases/PlaybackSessions/GetPlaybackResource/GetPlaybackResource.cs:20-22` (custódia atual) | Segmentos cifrados e chave juntos fora do banco | Chave só em `key.bin` (0600) no diretório temporário, apagado em `finally`; teste de que nada da chave vai a log/objeto; sem o ffmpeg registrar o caminho em log |
| Vídeos com fonte abaixo de 480p ganham variante com nome fora da lista fixa (`360p`, etc.); a reescrita da playlist do aluno lança exceção, a reprodução desses vídeos provavelmente falha hoje | `src/media/src/CodeForCoders.Media.Application/Common/PlaylistRewriter.cs:26-31` e `src/media/src/CodeForCoders.Media.Infra.Messaging/VideoQualityLadder.cs:22-25` | Preexistente e fora do escopo; o extrator não pode herdar essa lista | O extrator lê o nome da variante do `master.m3u8` e valida por padrão genérico. Registrar a preexistência ao usuário; não corrigir aqui |
| Orçamento de disco: preparação usa `available/3` por vídeo e a extração baixa ~1 GiB | `src/media/src/CodeForCoders.Media.Infra.Data/Videos/VideoPreparationRepository.cs:18,58` e `VideoPreparationWorker.cs:58` | Extração e preparação disputando o mesmo volume de 2 h de vídeo | Mínimo livre configurável antes de reivindicar; concorrência 1; diretórios separados |
| A URL pré-assinada vem de `clients.Public`, que no ambiente local aponta para `localhost:9000`, inalcançável pelo serviço | `src/media/src/CodeForCoders.Media.Infra.Data/Adapters/S3MediaStorageAdapter.cs:78-84` e `docker-compose.yml:289,321` | Envio falha só no ambiente local completo | `EndpointTranscription` própria; `Enabled=false` na pilha local; `--remote` usa o MinIO alcançável |
| A janela de idempotência do serviço (120 s) e a URL que não renova: reenvio exige URL e chave novas | contrato do serviço, `createTranscription` | Job duplicado órfão num POST ambíguo | `Idempotency-Key` por envio, só vale o `jobId` gravado, aviso de outro job ignorado |
| O resultado some em 24 h do estado terminal; `media` parado por mais que isso perde o texto | contrato do serviço, `getTranscriptionResult` | Tentativa presa em `waiting` | Resultado expirado volta à fila como falha transitória (novo envio, dentro do limite); consulta periódica contínua |
| SLO do serviço é p95 de até 24 h; "transcrevendo" por horas | contrato do serviço, `info.description` | Expectativa do professor | Estado claro na tela; prazo de 30 h configurável; métrica de tempo até pronta para calibrar no piloto |
| Contrato do serviço declara `sourceUrl` apenas HTTPS, o código aceita HTTP em lista | contrato do serviço vs. `adr-003.md` | Cliente que seguir o `pattern` recusa a URL válida em dev | O cliente segue o código; divergência documental registrada ao time do serviço |
| Renderizar 3.000 trechos com busca a cada tecla e rolagem automática | n/a (SPA nova) | Atraso perceptível (RF-05) | Normalizar uma vez, `useDeferredValue`; teste de desempenho com 3.000 trechos; janelamento só se o teste reprovar |
| Texto da aula em resposta grande passando por BFF que lê o corpo inteiro | `src/bff-student/src/CodeForCoders.BffStudent.Api/Endpoints/PlaybackSessionEndpoints.cs:45-60` | Memória e latência | Limite de 8 MiB já na cópia; resposta única (contrato); compressão no proxy a avaliar |
| Cobertura de teste do caminho de reprodução usada pela legenda | `src/student-spa/src/app/playback-*.test.tsx` | Regressão no player | Manter e estender esses testes antes de alterar o hook |

---

## Decisões Técnicas

- **D-01. Fonte do áudio: Versão de Reprodução, lida pelo worker.** *Racional:* o original não existe (`PrepareVideo.cs:183`); o worker já é o dono da chave (ADR-0006); um caminho para automático, Refazer e vídeo antigo. *Trade-offs:* baixa ~1 GiB por 2 h; CPU e disco no worker. *Alternativas:* extrair na preparação (não serve a vídeo antigo nem Refazer); reter o original (reverte a política); entregar HLS ao serviço (entregaria a chave). **Direção já decidida com o usuário**; validada no código e por ensaio com ffmpeg.
- **D-02. A fila é a entidade Transcrição, não o estado do vídeo.** *Racional:* RN-M19 proíbe a Transcrição de alterar o estado da Mídia; ADR-0006 já usa o estado como fila só porque a preparação é o vídeo. Desvio justificado do padrão: mesma mecânica (lease, `SKIP LOCKED`), tabela separada. *Alternativas:* mensageria (rejeitada na ADR-0006 pelo mesmo motivo); coluna de estado no vídeo (acopla).
- **D-03. Menor qualidade como fonte.** Menor `BANDWIDTH` do `master.m3u8` (480p, ou a única variante em fontes menores). *Racional:* a fala não melhora com a imagem; menos bytes. *Trade-off:* dependência do nome lido do master (não da lista fixa). *Alternativa:* maior qualidade (mais banda sem ganho).
- **D-04. Conteúdo em `jsonb` no banco do `media`**, em tabela separada, não em objeto nem em tabela por trecho. *Racional:* leitura única e atômica com o estado; isolamento por escola; sem segundo local de conteúdo protegido; busca é no cliente (T-10), então não há índice de texto. *Trade-offs:* linhas grandes (TOAST); *Alternativas:* objeto S3 (leitura extra por aula, outro local a proteger, troca atômica difícil); tabela por trecho (milhares de linhas por aula sem uso de consulta).
- **D-05. Aviso é gatilho; consulta é a verdade.** O worker nunca aceita o corpo do aviso como resultado. A consulta periódica existe sempre. *Alternativa:* depender só do aviso (aviso perdido ou ambiente sem receptor deixa a tentativa presa).
- **D-06. Um receptor com dois segredos aceitos** (rotação planejada de 72 h do serviço) e receptor ausente se não houver segredo. *Alternativa:* um segredo (rotação quebraria entregas).
- **D-07. WebVTT por requisição, sem cache.** Divisão de cue longo e sem sobreposição no gerador. *Racional:* RN-M21/G22/G23, custo desprezível para alguns milhares de trechos.
- **D-08. Preferência de legenda em `localStorage` do dispositivo.** RN-TL04. Sem sincronização entre dispositivos.
- **D-09. Busca no cliente** (T-10): a consulta do aluno nunca vai ao servidor.
- **D-10. Prazo máximo de espera 30 h; áudio apagado quando o job sai de `downloading`; apagar o conteúdo antigo na troca; tentativas `failed` antigas apagadas ao iniciar outra.** Valores calibráveis por configuração.

> **ADR-0020 (Proposed)** reúne D-01, D-02 (parcial), D-04 e D-05 como decisão durável. D-03, D-06 a D-10 são locais a esta feature.

---

## Verificação

- **Cenários críticos não óbvios.**
  - Duas pessoas acionam Transcrever ao mesmo tempo; reentrega do fato de pronto; ambas resultam em **uma** tentativa em andamento (índice, não código).
  - Worker morre no meio da extração (concessão vencida volta a elegível e conta tentativa), no meio do envio (reenvio com a mesma `Idempotency-Key` dentro de 120 s; depois dela, o job órfão é ignorado) e entre a gravação do conteúdo e a troca de vigente (transação única).
  - Refazer com falha da nova: vigente intacta e legível pelo professor e pelo aluno.
  - Resultado com `schemaVersion` desconhecida, vazio, desordenado, enorme, fora da janela de 24 h.
  - Aviso: assinatura válida em rotação (dois segredos), fora de ±300 s, duplicata, `jobId` que não é o gravado, `clientReference` ausente.
  - Texto com `-->`, `<`, `&`, quebras de linha, emoji e cue de 3 minutos no gerador de WebVTT.
  - Busca: "introducao" ↔ "Introdução", `ç`, pontuação removida e destaque na posição certa do texto original.
  - Aluno com direito ao curso A e aula do B; aluno de outra escola (404, G07); renovação negada para a legenda.
  - Nenhum vestígio ao fim: prefixo `transcription/` vazio, diretório de trabalho vazio, `key.bin` ausente.
- **Dados ou ambiente especiais.** Serviço de transcrição simulado a partir do OpenAPI dele, para integração; ffmpeg real da imagem sobre HLS gerado pelo transcodificador do próprio `media`; Testcontainers/Postgres como nos testes de integração existentes (concorrência com `SKIP LOCKED`); MinIO no padrão dos testes de integração. Smoke real: pilha remota (`scripts/remote-infra.sh provision` e `migrate`, `scripts/apps.sh start --remote`) com `Transcription__Enabled=true`, `AwsMedia__EndpointTranscription=http://192.168.0.5:9000`, `TRUSTED_PRIVATE_SOURCES=http://192.168.0.5:9000` no serviço, e, para o webhook, receptor alcançável e `TRUSTED_PRIVATE_WEBHOOKS` (senão só consulta). Migrações novas exigem `scripts/remote-infra.sh migrate`.
- **Observabilidade além do padrão.** Spans por estágio com atributos de id apenas: `media.transcription.extract-audio`, `.upload-audio`, `.submit`, `.poll`, `.fetch-result`, `.webhook.receive`. Instrumentos: tempo da tentativa até pronta (rótulo de origem), pedidos por origem, resultados por motivo de falha, duração transcrita, fila em espera por estado e idade da mais antiga, desfecho do receptor, quantas conciliadas por consulta versus aviso, duração de cada estágio. **Proibido** em qualquer atributo, métrica, rótulo ou log: URL assinada, texto, `jobId` junto de dado pessoal, e-mail. Alertas sugeridos: idade da fila acima de limite; falha do tipo `service-unavailable` acima de limite; tentativa sem avanço. Painel e alertas via ADR-0008.
- **Evidência de integração e ambiente.** O que tem ambiente reproduzível: integração (simulador). O que depende de EN-02: smoke com origem privada e webhook reais, executado numa task final de validação.
- **Verificação dos contratos.**
  - HTTP professor/aluno e webhook: os cenários listados em `contracts.md` ("Cenários de verificação para a TechSpec") mapeados às fatias acima; `Cache-Control: private, no-store` em todas as leituras de texto.
  - Dependência: teste de contrato do cliente contra simulador do OpenAPI dele; `schemaVersion` desconhecida e resultado fora da janela.
  - Validação do YAML (Spectral) é distinta da conformidade: a conformidade é provada pelos testes de contrato existentes do projeto contra os arquivos promovidos de `contracts/`.
  - Links de navegação: **nenhum** (a feature não emite link). O destino do aviso é um endereço operacional, `…/webhooks/v1/transcription-events` na API do `media`, não exposto por Caddy/SPAs.

---

## Questões em Aberto

Aprovada pelo responsável em 2026-10-12 com as recomendações abaixo (Q-A e Q-F decididas; as demais seguem com o padrão proposto e não bloqueiam):

- [x] **Q-A. Pilha local completa (`Transcription__Enabled=false`).** Recomendação: com a feature desligada, o estado derivado é `not-requested`, `canStart: false`, o disparo automático não cria linha e o POST responde o 409 `TRANSCRIPTION_NOT_RETRYABLE` já contratado. Alternativa: acrescentar um código novo no contrato (mudança aditiva). — responsável — se ficar aberta, a pilha local não tem comportamento definido para Transcrever.
- [ ] **Q-B. Prazo máximo de espera (30 h) e cadência de consulta**, a calibrar no piloto de 30 dias (métrica "tempo até pronta"). — responsável — não bloqueia: é configuração.
- [ ] **Q-C. Receptor do aviso por ambiente**: qual `host:porta` da API do `media` o serviço alcança no estável e no dev, para `TRUSTED_PRIVATE_WEBHOOKS`; e quem cadastra o destino e entrega o segredo. — responsável/operador — sem isso, aquele ambiente fica só com consulta periódica.
- [ ] **Q-D. Segredos por ambiente (chave da API e segredo do webhook):** emissão e guarda nas variáveis de cada compose. — operador — bloqueia só o smoke real (EN-02).
- [ ] **Q-E. Regra de ciclo de vida do bucket para `…/transcription/` como defesa adicional** (por exemplo, expirar em poucos dias). — operador — não bloqueia; a varredura do worker já cobre.
- [x] **Q-F. Vídeos com fonte abaixo de 480p podem não reproduzir hoje** — virou TASK-27 no Backlog (gerar a variante 480p por upscale; decisão de 2026-10-12) (ver Riscos). Fora desta capacidade; decidir se vira item próprio no backlog. — responsável — não bloqueia a transcrição.
- [ ] **Q-G. Desenho da legenda** (faixa nativa ou camada própria) e convivência com a marca d'água em tela cheia: resolvido no design aprovado (EN-01). — design — bloqueia só a parte de SPA de V-07.

---

## Architecture Decision Records

Herdadas (conformadas, nenhuma substituída):

- [ADR-0006](../../docs/adr/0006-preparacao-de-video-e-custodia-de-chave.md) — worker próprio, fila com lease, custódia da chave. Conformidade: a chave só existe em arquivo no diretório temporário do worker e é apagada com ele; a fila segue o mesmo mecanismo (em tabela própria, D-02).
- [ADR-0014](../../docs/adr/0014-entrega-de-segmentos-por-credencial-opaca-e-porta-de-distribuicao.md) — política de distribuição por prefixo `hls/*`. Conformidade: o prefixo do áudio está fora dele; o texto não passa pela distribuição.
- [ADR-0019](../../docs/adr/0019-runtime-e-deploy-atual.md) — runtime Compose/Komodo e armazenamento S3 por ambiente. Conformidade: configuração replicada nos arquivos de cada ambiente, incluindo o override remoto.
- [ADR-0008](../../docs/adr/0008-observabilidade-kibana-como-codigo.md) — painéis e alertas como código, junto da instrumentação (V-08).
- [ADR-0003](../../docs/adr/0003-verificacao-de-sessao-do-aluno.md) e [ADR-0013](../../docs/adr/0013-jwt-de-aluno-validado-por-servicos-de-dominio.md) — JWT de aluno e política de reprodução usados nas leituras do aluno.

Nova (Proposed):

- [ADR-0020: Transcrição de aula derivada da Versão de Reprodução, com serviço externo assíncrono, aviso como gatilho e texto sob guarda própria](../../docs/adr/0020-transcricao-de-aula-derivada-da-versao-de-reproducao.md) — aguarda a aprovação do responsável junto com esta TechSpec.
