---
tsg_artifact: techspec
product: code-4-coders
capability: CAP-007
version: 1.0
status: approved
updated: 2026-10-03
sources: tasks/prd-reproducao-protegida/prd.md@1.0, tasks/prd-reproducao-protegida/contracts.md@1.0, context/architecture-baseline.md@1.2, domains/entrega-de-midia-e-protecao/domain.md@1.0
---

# TechSpec — reprodução protegida

> **Escopo:** Full-stack (`identity`, `commerce` só configuração, `learning`, `media`, `bff-student`, `student-spa`, distribuição de vídeo e borda de desenvolvimento, painel no Kibana)
> **Modo:** Pipeline, API-First
> **PRD de origem:** [prd.md](prd.md), v1.0, aprovado em 2026-10-03
> **Contratos de integração:** [contracts.md](contracts.md) 1.0 (aprovado em 2026-10-03); OpenAPI [do BFF do aluno](api-contract.yaml) 1.0.0, [interno de `media`](internal-api-contract-media.yaml) 1.1.0, [interno de `learning`](internal-api-contract-learning.yaml) 1.1.0 e [interno de Identity](internal-api-contract-identity.yaml) 1.1.0; AsyncAPI [de `media`](asyncapi-contract.yaml) 1.2.0
> **Data:** 2026-10-03
> **Status:** Aprovada em 2026-10-03
> **Handoff:** liberado para geração de Tasks

## Resumo Executivo

A reprodução protegida cria, em `media`, a **Sessão de Reprodução** (um aluno, um vídeo, uma aula, 5 minutos) e a entrega do vídeo atrás dela; em `learning`, a leitura da aula e da estrutura do curso só para quem tem direito; no `bff-student` e no `student-spa`, a tela da aula, o player e a marca d'água. As decisões principais:

- **Identidade viaja em JWT de aluno por audiência, validado localmente** (ADR-0013). `identity` passa a publicar também a chave de aluno no JWKS e a emitir `media` e `learning`; a claim `email` existe só em `media`. O e-mail da marca d'água não passa por nenhum outro lugar.
- **A decisão de acesso é perguntada por quem serve o conteúdo** (ADR-0012): `learning` antes de devolver a estrutura, `media` antes de abrir ou renovar a sessão. Cada um com cache de até 30 s, falha fechada e **indisponível distinto de negado**.
- **Segmentos por credencial opaca e porta de distribuição** (ADR-0014). Os segmentos têm endereço único por vídeo, sem credencial na playlist; o player acrescenta a `segmentAccess.query` vigente, trocada a cada renovação. Produção usa CloudFront com política por prefixo; **desenvolvimento usa uma borda nginx (`media-edge`) na frente do MinIO**.
- **Playlists e chave passam pelo BFF sem alteração; segmentos nunca passam por ele.**
- **Avanço retido até haver consumidor.** O fato `midia.reproducao-avancou.v1` é gravado no outbox já como retido, sem ser publicado, e reenviado sob demanda quando `CAP-017` existir. Sem isso, a publicação `mandatory` sem fila ligada esgotaria as tentativas e acenderia o alarme do outbox.
- **O desenho de tela vem antes do código de tela** (EN-01).

**Trade-off primário:** a sessão curta, renovada com nova decisão, dá revogação em minutos e nenhuma réplica do direito, mas põe **uma chamada de decisão e uma validação de sessão em Identity a cada ~4 minutos por aluno assistindo**, além de uma validação de Identity por pedido de playlist, chave e avanço. O custo é aceito e medido (RF-08); o ganho é não manter estado de autorização por segmento nem uma segunda fonte do direito.

## Arquitetura da Solução

```text
student-spa (/student/aulas/:lessonId) ── /api/v1 (mesma origem, pelo nginx do SPA) ──► bff-student
   │  hls.js: playlists e chave pelo BFF; segmentos direto da distribuição + segmentAccess.query
   │
bff-student  ─ valida a sessão em Identity a cada requisição protegida (ADR-0003) e pede o JWT da audiência da rota
   ├─ GET  /lessons/{lessonId}                        ──► learning  (JWT aud=learning, scope lessons:read)
   │                                                          └─ asserção de learning ──► commerce  decideAccessInternal
   └─ POST /lessons/{id}/playback-sessions            ──► media     (JWT aud=media, scope playback:use, claim email)
      POST /playback-sessions/{id}/renewals                  ├─ Referência de Uso (lessonId → curso, vídeo)
      GET  /playback-sessions/{id}/playlist|variants|key     ├─ vídeo pronto?
      POST /playback-sessions/{id}/progress                  ├─ asserção de media ──► commerce  decideAccessInternal  (cache ≤ 30 s)
                                                              ├─ playback_sessions (Postgres de media)
                                                              ├─ S3/MinIO: playlists e chave cifrada (leitura privada)
                                                              └─ outbox ──► midia.reproducao-avancou.v1 (retido; reenvio sob demanda)

segmentos:  player ──(endereço sem credencial + segmentAccess.query)──► [CloudFront | media-edge nginx] ──► S3 | MinIO
identity:   publica a chave de aluno no JWKS; emite aud media|learning; claim email só em media
```

**URL pública.** A feature cria a rota de aula do SPA do aluno: `<origem do student-spa><BASE_PATH>aulas/{lessonId}`, com `BASE_PATH` por ambiente (padrão `/student/`, `vite.config.ts:11`; localmente `http://localhost:8082/student/aulas/{lessonId}`). Nenhuma mensagem e nenhum e-mail emitem esse link nesta entrega; a evidência é abri-lo com e sem sessão (V-01). Sem sessão, o SPA leva ao login e **volta à mesma aula** depois de entrar (RF-01).

### Bloco Backend

**`identity` — emissão e chave.** Hoje a emissão de JWT de aluno existe sem audiência configurada (`StudentSessionTokenOptions.AudienceScopes` vazio, `appsettings.json:48`) e o JWKS só publica a chave de ator (`UserTokenSigningKeySet.cs:7`). A entrega: publicar a chave de aluno no mesmo JWKS (com `PreviousSigningPublicKeys` de aluno para rotação); configurar `media` → `playback:use` e `learning` → `lessons:read`; incluir `email` no JWT **somente** quando a audiência estiver em `StudentSessionTokens:EmailAudiences`. O e-mail vem do mesmo registro de conta que a renovação da sessão já lê (`IdentitySessionStore.cs:112`, `StudentSessionDetails` ganha o campo); nenhuma consulta nova.

**`commerce` — só configuração.** Dois emissores novos em `ServiceAssertionOptions.Issuers`, `media` e `learning`, escopo `access-decision:read` (ADR-0012). A operação, o cache de 30 s e o contrato não mudam.

**`learning` — a aula e a estrutura.** Leitura nova: dada uma `lessonId`, achar a aula na **versão vigente** de um curso publicado da escola do JWT, devolver `LessonScreen` e, **antes**, consultar a decisão de acesso do aluno (`sub`) sobre o curso. Ordem fixa: aula na versão vigente (404 `LESSON_NOT_AVAILABLE`) → decisão (403 `ACCESS_DENIED`, 503 `ACCESS_DECISION_UNAVAILABLE`) → corpo. A busca por aula percorre as versões (`course_versions.modules` é `jsonb`, `CourseVersionConfiguration.cs:30`) e confirma que a versão achada é a de maior `VersionNumber` do curso; um índice sobre o `jsonb` acompanha a migration. `learning` ganha o cliente da decisão (asserção própria) e a política de aluno ao lado da de ator.

**`media` — sessão, entrega e avanço.** O núcleo:

- **Abertura (RN-M10).** Pela ordem: (1) Referência de Uso por (`tenant`, `lessonId`) → curso e vídeo; ausente → 404 sem consultar Matrícula; (2) vídeo `ready` (`Video.cs:211`); (3) decisão de acesso do aluno sobre o curso; (4) e-mail presente na claim. Cria uma `playback_sessions` e devolve a sessão com `expiresAt` = agora + 5 min, `renewAfter` = `expiresAt` − 90 s, a política (marca d'água, avanço) e a `segmentAccess`.
- **Renovação.** Mesma `sessionId`, só se a sessão ainda não venceu e pertence ao aluno do JWT. Repete a decisão (cache de 30 s não ajuda aqui, porque a renovação ocorre ~4 min depois). Estende a validade para agora + 5 min e devolve uma `segmentAccess` nova. Sessão vencida → 410, sem estender.
- **Entrega.** Playlist principal, variantes e chave são recursos da sessão. A playlist guardada no S3 é lida e reescrita em memória por requisição (ADR-0014): variantes viram `variants/{quality}`, segmentos viram endereços absolutos da distribuição sem credencial, e o marcador `c4c-key:{videoId}` (`PrepareVideo.cs:105`) vira `../key`. A chave sai do banco cifrada (`IVideoKeyProtector`), é decifrada na requisição, devolvida com `Cache-Control: no-store` e **nunca guardada em claro**. Toda leitura confere que a sessão existe, não venceu e é do aluno do JWT.
- **Avanço.** Aceito para sessão do aluno até **60 minutos depois** de `expiresAt` (a negação não apaga o que foi assistido; um player que acordou depois de suspensão ainda informa). Atômico por `UPDATE` condicional: `sequence` maior que a última e intervalo mínimo de 10 s desde o último avanço aceito; só então grava o fato no outbox, na mesma transação.
- **Porta de distribuição.** `CreateSegmentAccess(prefixo do vídeo, validade)` devolve a base e a cadeia opaca. Adaptadores: CloudFront (política por prefixo, assinatura local) e borda de desenvolvimento (`secure_link`).

**Transação, consistência e eventos.** Abertura e renovação gravam só a sessão (sem evento). O avanço grava sessão (`last_sequence`, `last_progress_at`, `last_position`) e o fato no outbox na mesma transação (G06). O `eventId` do fato é **determinístico** por (`sessionId`, `sequence`).

### Bloco Frontend

**Jornada e tela.** Uma tela nova, a **tela da aula** (`/aulas/:lessonId`), dentro do bloco autenticado do roteador: título, player, aviso de uso pessoal e a lista de módulos e aulas, com a aula atual destacada. Em celular a lista vai abaixo do player. Estados: carregando; reproduzindo; sem acesso; acesso encerrado (com data); indisponibilidade da decisão (com *Tentar de novo*); aula não disponível; vídeo indisponível; não foi possível iniciar (sem e-mail na sessão); navegador sem suporte.

**Onde mora o estado.** *Servidor:* a aula e a estrutura (cache do `@tanstack/react-query`, sem persistência), a decisão, a sessão. *Cliente:* a sessão de reprodução corrente (`sessionId`, `expiresAt`, `renewAfter`, `segmentAccess`), a posição de reprodução (para retomar depois de uma sessão vencida), o contador de `sequence` e o instante do último avanço. **Nada do player vai para armazenamento do navegador.** O e-mail existe só no estado do componente da marca d'água e é descartado ao sair da tela.

**Player.** Um componente de player sobre **hls.js** (dependência nova; ver Decisões). Pontos que o implementador não deriva:

- **Playlist e chave** pelo BFF, na mesma origem (`/api/v1/playback-sessions/...`), com credenciais do cookie.
- **Segmentos:** o player reescreve cada pedido de segmento com `xhrSetup(xhr, url)` (`xhr.open` com o endereço mais a `segmentAccess.query` vigente; documentação do hls.js consultada). O valor vigente é trocado a cada renovação.
- **Marca d'água:** elemento sobre o **contêiner** do player, não sobre o `<video>`, e a tela cheia é pedida **ao contêiner**, para a marca existir em tela cheia. `pointer-events: none`, fora da ordem de leitura de leitor de tela, posições em zonas fixas que não tocam a barra de controles, troca a cada `watermark.repositionSeconds` sem repetir a posição anterior, visível também com o vídeo pausado. Os controles são próprios (a geometria dos nativos não é conhecida), com teclado, nome acessível e foco visível.
- **Renovação:** só enquanto o vídeo toca. A partir de `renewAfter`, chama a renovação; com 503, repete a cada 5 s até `expiresAt`; com 403, para e mostra o estado; ao vencer sem renovar, para com *Tentar de novo*. Em pausa que passou de `expiresAt`, o próximo *play* **abre uma sessão nova** e retoma da posição guardada.
- **Avanço:** `heartbeat` a cada `progress.intervalSeconds` de reprodução contínua; `paused`; `ended`; `left` na saída da aula ou da aba, enviado com `fetch` em `keepalive` (a escrita exige `X-CSRF-Token`, que `sendBeacon` não permite).
- **Telemetria:** a instrumentação de requisições do navegador registra URLs. A `segmentAccess.query` é registrada como segredo e removida de todo atributo de span antes da exportação, por extensão do processador existente (`telemetry-url-redaction.ts:5` hoje só cobre `token`).

## Mapa de Fatias Verticais

A ordem de construção está em `Bloqueado por`. Toda fatia cruza as pontas que o comportamento exige. **A parte de tela de qualquer fatia só começa depois do EN-01** (wireframe ASCII → Figma → aprovação).

### V-01: O aluno com direito abre a aula e vê a lista de aulas; sem direito, vê o motivo

- **Cobre:** RF-01 (estados de acesso, aula indisponível, volta ao login), RF-06 (lista e versão vigente), RN-R02, RN-R06, RN-R07, RN-R08, US de abrir a aula e de ver mensagem clara.
- **Entrada / gatilho:** o aluno abre `<origem>/student/aulas/{lessonId}`, com ou sem sessão de login.
- **Processamento:** sem sessão, o SPA leva ao login com a rota de retorno e volta depois de entrar (retorno só para caminho interno do próprio SPA). O SPA chama `getStudentLesson`. O BFF valida a sessão em Identity, pede o JWT `learning` e chama `learning`. `learning` valida o JWT de aluno (chave de aluno no JWKS, escopo `lessons:read`, sem `permissions`), acha a aula na versão vigente, consulta a decisão em `commerce` com a asserção de `learning` (cache ≤ 30 s, timeout de 2 s, falha fechada) e devolve `LessonScreen`. O BFF mapeia 403 `ACCESS_DENIED` (com `reason`, `accessEndedAt`), 404 indistinto e 503 distinto.
- **Saída observável:** com cortesia vigente, a tela mostra título e lista de módulos e aulas na ordem do currículo, com a aula atual destacada, e a área do player em estado de carregamento. Sem concessão: "Você não tem acesso a este curso", sem nenhum título. Concessão vencida: "Seu acesso a este curso terminou em dd/mm/aaaa". Decisão indisponível: "Não foi possível confirmar seu acesso agora" com *Tentar de novo*. Aula inexistente, de outra escola, de curso não publicado ou removida: "Esta aula não está disponível".
- **Evidência / checkpoint:** testes de integração de `learning` (os três desfechos da decisão, 404 indistinto, JWT de ator recusado, JWT sem escopo recusado, `commerce` sem resposta → 503 e nada devolvido) e de `bff-student` (audiência da rota, mapeamento de erros). Cenário no navegador contra o ambiente de desenvolvimento: financeiro concede cortesia a um aluno (CAP-008), o aluno abre o link e vê a lista; um segundo aluno sem concessão vê a mensagem; sair da sessão e abrir o link leva ao login e volta à aula.
- **Bloqueado por:** EN-01 (só a parte de tela).

### V-02: O aluno assiste à aula, com marca d'água e sem link solto

- **Cobre:** RF-01 (abertura: vídeo não pronto, sem e-mail), RF-02 (abrir sessão; várias sessões simultâneas), RF-03, RF-04, RF-05 (tocar, pausar, avançar e voltar, volume, tela cheia, teclado, qualidade automática), RN-R03, RN-R04, RN-M01, RN-M07, RN-M08, RN-M12, RN-M13, RN-M15.
- **Entrada / gatilho:** o aluno com direito aperta *play* na tela da aula (a abertura da sessão acontece ao abrir a aula).
- **Processamento:** o BFF pede o JWT `media` (com `email`) e chama `openPlaybackSession`. `media` confere Referência de Uso, vídeo pronto, decisão e e-mail, grava a sessão e devolve `PlaybackSession`. O player busca a playlist principal e as variantes pelo BFF; `media` as reescreve (variantes relativas, segmentos absolutos sem credencial, chave relativa). O player pede a chave pelo BFF e os segmentos à distribuição com `segmentAccess.query`. Em desenvolvimento a distribuição é a borda `media-edge` (nginx `secure_link`, na frente do MinIO); em produção, CloudFront. A marca d'água é composta sobre o contêiner. O aviso de uso pessoal aparece sob o player.
- **Saída observável:** o vídeo toca com o e-mail do aluno sobre ele, em tela cheia também; trocar de posição a cada 30 s; `ACCESS_DENIED` e indisponibilidade no lugar do player quando for o caso; "Esta aula está indisponível no momento" se o vídeo não está pronto; "Não foi possível iniciar a aula" sem e-mail.
- **Evidência / checkpoint:** integração de `media`: abertura nos quatro desfechos e na ordem de RN-M10 (aula com vídeo de outra aula → 404 **sem** consulta a `commerce`); playlist, variante e chave recusadas sem sessão, com sessão vencida e de outro aluno; credencial de segmento recusada para outro prefixo e depois de `expiresAt`; três sessões simultâneas do mesmo aluno; e-mail ausente em log, span, métrica, endereço, nome de objeto e mensagem (busca do e-mail de teste em toda a saída); mesmo arquivo e mesma chave para dois alunos. Cenário no navegador: assistir com cortesia real, com a marca visível em tela cheia e trocando de posição; abrir o endereço de um segmento copiado sem a credencial e com a credencial vencida e obter recusa. Verificação de linguagem: nenhuma mensagem diz "protegido contra cópia".
- **Bloqueado por:** V-01; EN-01 (tela).

### V-03: A reprodução continua sem interrupção e para quando o direito acaba

- **Cobre:** RF-02 (renovação, falha fechada, sessão vencida), RN-R01, RN-R02, DP-01, DP-08, DP-09.
- **Entrada / gatilho:** o aluno assiste por mais de 4 minutos; ou o direito termina enquanto ele assiste; ou Matrícula fica indisponível na hora de renovar.
- **Processamento:** a partir de `renewAfter`, o player chama `renewPlaybackSession`. `media` repete a decisão e devolve validade e `segmentAccess` novas, que o player adota sem recarregar a playlist. Decisão negada → 403 e o player para; indisponível → 503 e o player repete a cada 5 s até `expiresAt`; vencida sem renovação → 410. Em pausa além de `expiresAt`, o *play* seguinte abre sessão nova.
- **Saída observável:** assistir 20 minutos sem pausa nem recarga; revogar ou vencer o acesso no meio da aula para a reprodução em até 5 minutos mais o trecho carregado, com a mensagem do estado; Matrícula fora do ar por menos de 90 s não interrompe; por mais, a reprodução para com a indisponibilidade e *Tentar de novo*.
- **Evidência / checkpoint:** integração de `media` com `commerce` substituído por um servidor de teste (negado, indisponível, volta a responder) e relógio controlado; teste do player com sessão e relógio simulados (renovação sem pausa, repetição, parada). Cenário no ambiente de desenvolvimento: aluno com cortesia assistindo; o `expires_at` da concessão no banco de `commerce` é antecipado, por script de apoio do cenário, e a reprodução para dentro da janela. A revogação como ato do produto é de `CAP-009` e não existe aqui.
- **Bloqueado por:** V-02.

### V-04: O aluno troca de aula e ajusta a velocidade

- **Cobre:** RF-05 (velocidade), RF-06 (trocar de aula), RN-R06, RN-R08, DP-04, DP-10.
- **Entrada / gatilho:** o aluno escolhe outra aula na lista, ou muda a velocidade.
- **Processamento:** trocar de aula navega para `/aulas/{outra}`, que repete V-01 (estrutura) e V-02 (nova sessão e nova decisão); a aula anterior para de tocar e sua sessão simplesmente vence. Velocidade: 0,5x, 1x, 1,25x, 1,5x e 2x, no próprio player.
- **Saída observável:** a aula 5 abre sem ter visto as 2, 3 e 4; a lista mostra a ordem e o nome da versão vigente depois de uma republicação, e o endereço de uma aula renomeada ou reordenada continua apontando para ela; a aula removida na republicação mostra "Esta aula não está disponível" com a lista da versão nova à mão; a velocidade tem efeito imediato.
- **Evidência / checkpoint:** integração de `learning` com republicação (renomear, reordenar, remover); teste do SPA para troca de aula e velocidade; cenário no navegador com duas aulas.
- **Bloqueado por:** V-02.

### V-05: O avanço da reprodução chega como fato, sem e-mail e sem perder nada

- **Cobre:** RF-07, RN-R05, RN-M14, DP-02.
- **Entrada / gatilho:** o aluno assiste, pausa, sai ou chega ao fim.
- **Processamento:** o player envia `heartbeat` (a cada 30 s), `paused`, `ended` e `left` (`keepalive`) com `sequence` crescente. `media` aceita enquanto a sessão for do aluno e estiver dentro da janela (até 60 min depois de `expiresAt`); recusa `positionSeconds` acima da duração do vídeo mais 10 s (400). Aceita só `sequence` maior que a última e intervalo mínimo de 10 s desde o último aceito; responde 200 com `recorded`. Aceito, grava o fato no outbox, na mesma transação, **já retido** (não publicado): `midia.reproducao-avancou.v1`, `eventId` determinístico. Existe uma operação de reenvio, no padrão do reenvio dos fatos de vídeo, que publica os fatos retidos de uma escola em ordem de gravação numa fila indicada, sem alterar o registro nem gerar fato novo.
- **Saída observável:** 10 minutos contínuos produzem cerca de 20 fatos; pausa aos 252 s produz um fato com a posição 252; reenvio da mesma `sequence` não duplica; dois avanços a menos de 10 s: o segundo com `recorded: false`; nenhum fato leva e-mail, nome, título ou percentual; o outbox **não** acusa mensagem pendente nem esgotada.
- **Evidência / checkpoint:** integração de `media` com Testcontainers: cadência, idempotência, janela e coalescência; o fato retido não conta na saúde do outbox; a operação de reenvio publica os fatos retidos numa fila de teste, cada `eventId` uma vez, também depois de uma sessão que terminou por decisão negada. Inspeção do fato publicado contra o AsyncAPI.
- **Bloqueado por:** V-02.

### V-06: A equipe vê custo, erros e cache da entrega

- **Cobre:** RF-08, RN-M17.
- **Entrada / gatilho:** reproduções ocorrendo.
- **Processamento:** `media` emite: sessões abertas e recusadas por motivo (negada, indisponível, referência ausente, vídeo não pronto, sem e-mail), latência e falha da consulta de decisão, e o número de alunos ativos nos últimos 30 dias (sessões distintas por aluno), que junto do armazenamento já medido compõe o custo por aluno ativo. O `student-spa` emite um intervalo "tempo até o vídeo começar" (da abertura ao primeiro quadro) pelo OpenTelemetry que já usa. Atributos sem e-mail, nome, título ou identificador de aluno. A taxa de acerto de cache vem da distribuição, pela plataforma; o painel e o alerta ficam no Kibana como código (ADR-0008), com limiar inicial de 85% em 15 minutos para recalibrar com dado real.
- **Saída observável:** painel com sessões por desfecho, decisões indisponíveis, tempo até começar (mediana e p95), alunos ativos e custo por aluno; alerta de cache definido.
- **Evidência / checkpoint:** teste de que os instrumentos são emitidos com os atributos esperados e **sem** dado pessoal; importação do painel no Kibana de desenvolvimento e verificação de que ele mostra uma reprodução de teste; busca do e-mail de teste na telemetria exportada: nenhuma ocorrência.
- **Bloqueado por:** V-02.

### Habilitadores inevitáveis

| Habilitador | Por que não cabe numa fatia | Menor escopo | Primeira fatia desbloqueada |
|---|---|---|---|
| EN-01 | O PRD exige wireframe ASCII → Figma → aprovação antes de código de tela, e a aprovação é do responsável pelo produto; nenhuma fatia a produz | Novo `docs/design/wireframes-aula.md` com a tela da aula (desktop e celular): player, tela cheia, marca d'água em suas posições, aviso, lista de aulas, os estados de V-01 e V-02 (carregando, sem acesso, encerrado, indisponível, aula indisponível, vídeo indisponível, sem e-mail, navegador sem suporte, renovação falhando), com registro de aprovação no cabeçalho, e o Figma de cada estado | V-01 (parte de tela), V-02, V-03, V-04 |

## Contratos e Fronteiras

O conjunto de contratos deste PRD está em [contracts.md](contracts.md). Esta spec o mapeia à implementação; não duplica schemas.

### Mapeamento de mensagens e dados

| Contrato e identificador | Aplicação/produtor e consumidores | Comportamento a implementar | Evidência |
|---|---|---|---|
| AsyncAPI `publicarReproducaoAvancou` · `midia.reproducao-avancou.v1` | `media` (send) · nenhum consumidor nesta entrega; `learning` em `CAP-017` | Gravar no outbox já retido, na mesma transação do avanço; `eventId` determinístico; sem e-mail, nome, título, percentual ou `videoId`; reenvio sob demanda de fatos retidos | V-05 |
| Recepção de `conteudo.versao-publicada.v1` (existente, `tasks/prd-autoria-curso/asyncapi-contract-media.yaml` 1.1.0) | `learning` → `media` | **Sem mudança.** A Referência de Uso por (`tenant`, `lessonId`) passa a ser lida na abertura; a leitura precisa de índice | V-02 |

**Diferença para o acordo anterior e transição.** A mensagem é nova; nada a migrar. Como o sistema só vai a homologação ao fim do MVP (OD67), não há dado legado a reconciliar: as Referências de Uso de cursos já publicados em desenvolvimento voltam a existir publicando de novo, se for o caso.

**Percurso do e-mail (dado pessoal, ponto de exposição declarado).**

| Ponto | O que acontece | Proteção |
|---|---|---|
| Criação | Já existe em Identity (conta); entra na claim `email` do JWT da audiência `media` na validação da sessão | Só a audiência `media`; vida de 5 min; RS256; ADR-0013 |
| Trânsito | `identity` → `bff-student` → `media` | TLS interno; o BFF não o guarda (nem em Valkey) nem o registra |
| Uso em `media` | Lido da claim e copiado em `watermark.text` na resposta | Nunca gravado em banco, outbox, cache, log, span ou métrica; `playback_sessions` não tem coluna de e-mail |
| Resposta | `media` → BFF → SPA, em `PlaybackSession` | `Cache-Control: no-store`; resposta de abertura e de renovação |
| SPA | Estado do componente da marca d'água | Sem armazenamento do navegador; descartado ao sair da tela; fora de atributo de span |
| Descarte | Fim do componente e da sessão | — |

**Percurso da credencial dos segmentos e da chave.**

| Ponto | `segmentAccess.query` | Chave AES |
|---|---|---|
| Criação | `media`, na abertura e a cada renovação, por política até `expiresAt` | Gerada na preparação (ADR-0006), guardada cifrada no banco de `media` |
| Trânsito | `media` → BFF → SPA (`no-store`); SPA → distribuição, no endereço de cada segmento | `media` → BFF → SPA (`no-store`), só com sessão válida |
| Persistência | Nunca em banco, cache ou mensagem; aparece nos logs de acesso da distribuição e da borda | Só cifrada, em repouso; em claro só na memória da requisição |
| Leitura | A borda e a distribuição validam por prefixo e validade | Decifrada por requisição |
| Descarte | Vence em ≤ 5 min; o SPA a registra como segredo de telemetria e a remove dos spans | Não é guardada em claro em lugar nenhum |

O log de acesso da borda de desenvolvimento **não registra a consulta** (formato sem argumentos). Os logs da distribuição de produção registram a consulta, e por isso a credencial é de vida curta e restrita a um prefixo.

### Mapeamento do contrato de API

Endpoints, schemas, autenticação e erros vivem nos YAMLs do PRD. Esta tabela só aponta o caminho de implementação.

| operationId | Caminho de implementação |
|---|---|
| `getStudentLesson` | `bff-student` handler de aula → cliente de `learning` (JWT `learning`) → `getStudentLessonInternal` |
| `getStudentLessonInternal` | `learning` endpoint → caso de uso de leitura da aula → consulta da versão vigente + cliente da decisão (`decideAccessInternal`) |
| `openPlaybackSession`, `renewPlaybackSession` | `bff-student` handler → cliente de `media` (JWT `media`) → `…Internal` |
| `openPlaybackSessionInternal` | `media` endpoint → caso de uso de abertura → Referência de Uso, vídeo, decisão, e-mail, `playback_sessions`, porta de distribuição |
| `renewPlaybackSessionInternal` | `media` endpoint → caso de uso de renovação → decisão, `playback_sessions`, porta de distribuição |
| `getPlaybackPlaylist`, `getPlaybackVariantPlaylist`, `getPlaybackKey` (+ `Internal`) | BFF repassa o corpo sem alteração (tipo de conteúdo e `no-store`); `media` lê o S3 e reescreve (playlists) ou decifra (chave) |
| `recordPlaybackProgress` (+ `Internal`) | `media` caso de uso de avanço → `UPDATE` condicional da sessão + outbox na mesma transação |
| `validateStudentSessionInternal` (revisada) | `identity` caso de uso existente, com o campo de e-mail no resultado da sessão, e o emissor do JWT com `email` condicional |

**Validações além do contrato:**

| operationId | Regra | Camada |
|---|---|---|
| `recordPlaybackProgressInternal` | `positionSeconds` ≤ duração do vídeo + 10 s, senão 400 `VALIDATION_ERROR` | application |
| `recordPlaybackProgressInternal` | `sequence` ≤ última aceita → `recorded: false`, sem erro | application |
| `renewPlaybackSessionInternal` | sessão com `expiresAt` no passado → 410, sem consultar `commerce` | application |
| `openPlaybackSessionInternal` | `studentId` é sempre o `sub` do JWT; nenhum parâmetro o substitui | api |
| `*Internal` de `media` e `learning` | política de aluno: escopo da audiência **e** ausência da claim `permissions` | api |

**Exceção → resposta HTTP:**

| Exceção | HTTP | code do contrato |
|---|---|---|
| Aula sem Referência de Uso, de outra escola ou removida da versão vigente | 404 | `LESSON_NOT_AVAILABLE` |
| Vídeo não `ready` | 409 | `MEDIA_NOT_READY` |
| Decisão negada | 403 | `ACCESS_DENIED` (com `reason`, `accessEndedAt`) |
| `commerce` sem resposta dentro de 2 s, ou erro | 503 | `ACCESS_DECISION_UNAVAILABLE` |
| Sem e-mail na claim | 422 | `WATERMARK_UNAVAILABLE` |
| Sessão inexistente, de outro aluno ou de outra escola | 404 | `PLAYBACK_SESSION_NOT_FOUND` |
| Sessão vencida (renovação, playlist, chave) | 410 | `PLAYBACK_SESSION_EXPIRED` |
| Avanço fora da janela de aceitação | 410 | `PLAYBACK_SESSION_EXPIRED` |
| JWT de aluno ausente, inválido, de ator ou de outra audiência | 401 | `TOKEN_INVALID` (interno) / `SESSION_REQUIRED` (BFF) |

### Mapeamento de jornada

| User Story | Tela / componente | operationId ou ação local | Evidência |
|---|---|---|---|
| Aluno com acesso abre a aula e o vídeo começa | Tela da aula, player | `getStudentLesson`, `openPlaybackSession` | V-01, V-02 |
| Aluno troca de aula pela lista | Lista de aulas | `getStudentLesson`, `openPlaybackSession` | V-04 |
| Aluno sem acesso ou vencido entende o que houve | Estados da tela da aula | `getStudentLesson` (403) | V-01 |
| Aluno não é desconectado por falha momentânea | Player, renovação | `renewPlaybackSession` (503 repetido) | V-03 |
| Escola: link copiado não serve; e-mail aparece no vídeo | Player, marca d'água | ação local, playlist/chave/segmentos | V-02 |
| Escola: acesso revogado ou vencido para a reprodução em minutos | Player | `renewPlaybackSession` (403) | V-03 |
| `CAP-017` recebe o avanço | Player, avanço | `recordPlaybackProgress` | V-05 |
| Equipe vê custo e erros | Painel no Kibana | telemetria | V-06 |

### Entidades do domínio

| Entidade do Domain Doc | Representação técnica | Local |
|---|---|---|
| Sessão de Reprodução | Tabela `playback_sessions` (`id`, `tenant_id`, `student_id`, `course_id`, `lesson_id`, `video_id`, `opened_at`, `expires_at`, `last_sequence`, `last_progress_at`, `last_position_seconds`). **Sem coluna de e-mail.** Índice por `(tenant_id, expires_at)` para a limpeza; linhas removidas 24 h depois de `expires_at` por rotina do papel `worker` | `media`, banco próprio, por migration do EF |
| Referência de Uso | Tabela `course_video_references` existente, chave `(tenant, curso, aula)`; **novo índice** por `(tenant_id, lesson_id)` para a abertura | `media` |
| Marca d'Água | Sem persistência. `watermark.text` na resposta da sessão, composta no cliente | `media` (resposta), SPA |
| Versão de Reprodução | Objetos privados existentes (`master.m3u8`, `{quality}.m3u8`, segmentos) sob o prefixo `{tenant}/{video}/hls/` | S3/MinIO, sem mudança de formato |
| Curso, Módulo, Aula (leitura) | `course_versions.modules` (`jsonb`) existente; **novo índice** `jsonb` para achar a aula | `learning`, migration do EF |

### Interfaces entre fatias ou times

- **Porta de distribuição** (entre `media` e os adaptadores): `CreateSegmentAccess` recebe o prefixo do vídeo e o instante de validade; devolve a base dos segmentos, a cadeia opaca e a validade. É o que V-02 entrega e V-03 reusa na renovação.
- **Configuração de política** (`Playback`): `SessionMinutes=5`, `RenewLeadSeconds=90`, `WatermarkRepositionSeconds=30`, `ProgressIntervalSeconds=30`, `ProgressMinGapSeconds=10`, `ProgressGraceMinutes=60`, `DecisionTimeoutMilliseconds=2000`, `SessionRetentionHours=24`, `Delivery:Adapter` (`cloudfront` ou `edge`). `media` devolve a política ao cliente; o SPA nunca fixa os valores.
- **Retenção do outbox** (`Outbox:RetainedRoutingKeys`): chaves de roteamento gravadas já retidas. Com `midia.reproducao-avancou.v1` na lista, o fato fica retido; **tirá-la da lista** passa a publicá-lo ao vivo. Reenvio: `Outbox:ProgressFactReplayTenantId` e `Outbox:ProgressFactReplayQueue`, no padrão de `VideoFactReplay`.

## Arquivos a Modificar e a Referenciar

Arquivos **a criar** não são listados: a estrutura é determinística pelas skills `dotnet` e `react`.

### A modificar

| Caminho | Fatia | Alteração |
|---|---|---|
| `src/identity/src/CodeForCoders.Identity.Api/Security/UserTokenSigningKeySet.cs` | V-01 | Publicar também a chave de aluno e as anteriores |
| `src/identity/src/CodeForCoders.Identity.Api/Security/StudentSessionTokenOptions.cs`, `StudentSessionTokenIssuer.cs` | V-01, V-02 | `PreviousSigningPublicKeys`; `EmailAudiences`; claim `email` condicional; escopos por audiência |
| `src/identity/src/CodeForCoders.Identity.Api/Extensions/ServiceConfigurationExtensions.cs` | V-01 | Validar as opções novas |
| `src/identity/src/CodeForCoders.Identity.Application/Common/StudentSessionDetails.cs`, `…/ValidateStudentSession/ValidateStudentSession.cs` | V-02 | Levar o e-mail no resultado da sessão |
| `src/identity/src/CodeForCoders.Identity.Infra.Data/Accounts/IdentitySessionStore.cs` | V-02 | Selecionar o e-mail da conta no mesmo `select` da renovação (linha 112) |
| `src/commerce/src/CodeForCoders.Commerce.Api/appsettings.json`, `docker-compose*.yml` | V-01, V-02 | Emissores `learning` e `media` em `ServiceAssertionOptions.Issuers` |
| `src/learning/src/CodeForCoders.Learning.Api/Security/LearningJwtBearerOptionsSetup.cs`, `LearningAuthorization.cs`, `Extensions/ServiceConfigurationExtensions.cs` | V-01 | Política de aluno ao lado da de ator; cliente da decisão e sua asserção |
| `src/learning/src/CodeForCoders.Learning.Infra.Data/Configurations/CourseVersionConfiguration.cs`, `Queries/CourseQueries.cs` | V-01 | Índice `jsonb` e a busca da aula na versão vigente |
| `src/media/src/CodeForCoders.Media.Api/Security/MediaJwtBearerOptionsSetup.cs`, `Extensions/ServiceConfigurationExtensions.cs`, `Extensions/EndpointExtensions.cs` | V-02 | Política de aluno ao lado de `VideoLibraryAccess`; mapear os endpoints de entrega |
| `src/media/src/CodeForCoders.Media.Infra.Data/MediaDbContext.cs`, `CourseReferences/CourseVideoReferenceConfiguration.cs`, `DependencyInjection.cs`, `Adapters/S3MediaStorageAdapter.cs` | V-02 | Sessão; índice por aula; leitura de playlist e de chave; adaptadores da porta de distribuição |
| `src/media/src/CodeForCoders.Media.Infra.Data/Outbox/OutboxMessageWriter.cs`, `Configuration/OutboxOptions.cs` (em `Infra.Messaging`) | V-05 | Gravar retido por `Outbox:RetainedRoutingKeys` |
| `src/media/src/CodeForCoders.Media.Infra.Messaging/OutboxPublisherWorker.cs`, `RabbitMqPublisher.cs`, `Api/Extensions/VideoReplayOperationsExtensions.cs` | V-05 | Reenvio de fatos de avanço, no padrão do reenvio dos fatos de vídeo (`OutboxPublisherWorker.cs:~115`) |
| `src/media/src/CodeForCoders.Media.Application/Common/MediaTelemetry.cs` | V-06 | Instrumentos de sessão, decisão e alunos ativos |
| `src/media/src/CodeForCoders.Media.Api/appsettings.json` | V-02 | Seção `Playback` |
| `src/bff-student/src/CodeForCoders.BffStudent.Api/Security/BffSecurityMiddleware.cs`, `BffSecurityOptions.cs` (`Application/Common`) | V-01, V-02 | Audiência por rota no lugar da audiência única de `/proxy` (linha 91) |
| `src/bff-student/src/CodeForCoders.BffStudent.Api/Extensions/EndpointExtensions.cs`, `appsettings.json` | V-01, V-02 | Mapear as rotas; endereços e timeouts de `learning` e `media` |
| `src/student-spa/src/app/router.tsx`, `src/config/paths.ts` | V-01 | Rota `/aulas/:lessonId` |
| `src/student-spa/src/app/routes/dashboard-route.tsx`, `student-login-route.tsx` | V-01 | Retorno à rota original depois do login (só caminho interno) |
| `src/student-spa/src/lib/telemetry-url-redaction.ts`, `telemetry.ts` | V-02 | Registro de segredos de telemetria; remover a credencial dos spans |
| `src/student-spa/package.json` | V-02 | Dependência do player (hls.js) |
| `docker-compose.yml`, `docker-compose.remote.yml`, `docker-compose.coolify.yml`, `scripts/generate-local-env.sh`, `scripts/apps.sh` | V-01, V-02 | Chave de aluno de Identity e escopos; chaves de asserção de `media` e `learning`; container `media-edge` e segredo compartilhado; porta da borda |
| `scripts/kibana/` (painel de mídia) | V-06 | Painel e alerta de reprodução |

### A referenciar (não alterar)

| Caminho | Por que consultar |
|---|---|
| `tasks/prd-concessao-acesso/internal-api-contract-commerce.yaml` | `decideAccessInternal`, o cache de 30 s e o formato da decisão |
| `src/commerce/src/CodeForCoders.Commerce.Api/Security/ServiceAssertionVerifier.cs` | Como o emissor, o escopo e o `jti` são verificados |
| `src/bff-student/src/CodeForCoders.BffStudent.Api/Clients/ShowcaseCommerceClient.cs`, `Security/ServiceAssertionTokenFactory.cs` | Padrão de cliente tipado e de asserção de serviço a seguir em `learning` e `media` |
| `src/media/src/CodeForCoders.Media.Application/UseCases/Videos/PrepareVideo/PrepareVideo.cs` | Marcador de chave `c4c-key:` (linha 105) e prefixo `{tenant}/{video}/hls/` |
| `src/media/src/CodeForCoders.Media.Infra.Messaging/FfmpegVideoTranscoder.cs` | Formato das playlists guardadas (`master.m3u8`, `{quality}.m3u8`, `{quality}/segment_%05d.ts`) |
| `docs/design/wireframes-cortesias.md` | Formato do registro de aprovação do desenho |
| `tasks/prd-autoria-curso/contracts.md` | Identidades `lessonId` e `videoId` e a Referência de Uso |

## Análise de Impacto

| Componente | Tipo | Impacto e risco | Ação requerida |
|---|---|---|---|
| `identity` | modificado | JWKS passa a publicar a chave de aluno; JWT de aluno ganha audiências e `email` condicional; um novo par de chaves | Teste de que tokens de ator e de aluno não se aceitam nas rotas do outro; chave de aluno no provisionamento local |
| `commerce` | modificado (configuração) | Dois emissores novos na consulta da decisão; mais consultas por aluno assistindo | Acompanhar latência e taxa de `jti` rejeitado (ADR-0012, item 6) |
| `learning` | modificado | Leitura nova com consulta síncrona a `commerce` no caminho da tela; índice `jsonb` | Medir a busca por aula; falha fechada |
| `media` | modificado | Passa a servir o aluno e a ler S3 por requisição de playlist; nova tabela de sessões e escrita a cada avanço | Cache em memória das playlists originais (imutáveis); limpeza de sessões; medir escrita por aluno ativo |
| `bff-student` | modificado | Audiência por rota; mais validações de sessão em Identity (playlist, chave, avanço) | Medir `validateStudentSessionInternal` por aluno ativo (ADR-0003) |
| `student-spa` | modificado | Dependência nova (hls.js); tela nova; telemetria do player | Redação de segredos; matriz de navegadores |
| Distribuição de vídeo (CloudFront) | novo, só produção | Política de cache sem consulta; chave privada de assinatura | Configurar na primeira implantação com CloudFront (homologação) |
| `media-edge` (nginx) | novo, só desenvolvimento | Mais um container no compose local, remoto e Coolify de dev | Segredo compartilhado com `media`; CORS; `Range` |
| RabbitMQ | sem mudança | O fato de avanço **não** é publicado nesta entrega (retido) | Nenhuma fila nova até `CAP-017` |
| Outbox de `media` | modificado | Fatos retidos acumulam e não saem sozinhos | Limpeza e consumo ficam com `CAP-017` (Questões em Aberto) |
| Kibana | modificado | Painel e alerta de reprodução | Importação como código (ADR-0008) |

## Riscos e Preocupações

| Preocupação | Local (`arquivo:linha`) | Impacto | Mitigação |
|---|---|---|---|
| O JWKS só publica a chave de ator; `media` e `learning` não conseguem validar o JWT de aluno | `src/identity/src/CodeForCoders.Identity.Api/Security/UserTokenSigningKeySet.cs:7` | Toda requisição do aluno a `media` e `learning` seria recusada | V-01 publica a chave de aluno no mesmo JWKS (ADR-0013); teste de validação ponta a ponta |
| Sem consumidor, o fato de avanço é publicado com `mandatory: true` e volta como não roteável, esgotando as tentativas e deixando o outbox "esgotado" | `src/media/src/CodeForCoders.Media.Infra.Messaging/RabbitMqPublisher.cs:43`, `OutboxPublisherWorker.cs:72`, `Health/OutboxHealthCheck.cs:17` | Alarme permanente do outbox e fatos presos fora da ordem | O fato nasce retido (D-07); a saúde do outbox só conta o que não foi processado |
| A audiência do JWT que o BFF pede é uma só e vale só para `/proxy` | `src/bff-student/src/CodeForCoders.BffStudent.Api/Security/BffSecurityMiddleware.cs:91` | As rotas de aula e de sessão receberiam o token errado ou nenhum | Audiência por rota (V-01), com teste por rota |
| O e-mail não está no resultado da sessão do aluno | `src/identity/src/CodeForCoders.Identity.Infra.Data/Accounts/IdentitySessionStore.cs:112`, `StudentSessionTokenIssuer.cs:32` | Sem e-mail não há marca d'água, e sem marca não há reprodução | V-02 inclui o e-mail no mesmo `select` e na claim de `media` |
| A instrumentação de requisições do navegador registra a URL de cada segmento, com a credencial na consulta | `src/student-spa/src/lib/telemetry-url-redaction.ts:5` | Credencial de sessão exportada para o coletor | Registro de segredos e remoção dos spans (V-02); busca da credencial de teste na telemetria |
| A referência de uso só tem chave por curso e aula; a abertura busca por aula | `src/media/src/CodeForCoders.Media.Infra.Data/CourseReferences/CourseVideoReferenceConfiguration.cs:11` | Varredura de tabela a cada abertura | Índice por `(tenant_id, lesson_id)` (V-02) |
| `modules` é `jsonb` sem índice; achar a aula percorre as versões | `src/learning/src/CodeForCoders.Learning.Infra.Data/Configurations/CourseVersionConfiguration.cs:30` | Latência na tela da aula com muitos cursos e versões | Índice sobre o `jsonb` e confirmação da versão de maior número (V-01); medir |
| A redireção de volta ao login pode virar redirecionamento aberto | `src/student-spa/src/app/routes/dashboard-route.tsx:23` | Um link malicioso levaria o aluno a outro site depois do login | Aceitar apenas caminho interno do SPA (começa com uma barra, não com duas); teste |
| `StudentSessionTokens` não tem chave nem audiência no compose, e `generate-local-env.sh` não gera chave de aluno | `scripts/generate-local-env.sh:130` | O ambiente local sobe sem token de aluno | V-01 acrescenta a geração e as variáveis |
| A limpeza das linhas retidas do outbox não existe: o registro cresce | `src/media/src/CodeForCoders.Media.Infra.Data/Outbox/OutboxMessage.cs` | Crescimento de tabela, a ~2 fatos por minuto por aluno assistindo | Aceito no MVP; a retenção do registro é decidida por `CAP-017` ao consumir |

## Decisões Técnicas

- **D-01 — Sessão em Postgres de `media`.** **Racional:** o avanço precisa de idempotência por (sessão, sequência) e de gravação atômica com o outbox, e a leitura de playlist e chave confere a sessão por chave primária. **Trade-offs:** escrita por avanço (~2 por minuto por aluno assistindo) no banco de `media`. **Alternativas rejeitadas:** Valkey com TTL (perde a atomicidade com o outbox e acrescenta um segundo armazém de estado de autorização); sessão sem estado, só em token assinado (não dá idempotência nem coalescência do avanço).
- **D-02 — Cache da decisão em memória, por instância, em `media` e em `learning`.** Chave `{tenant}:{aluno}:{curso}:v1`, validade absoluta de até 30 s e nunca além do término informado; guarda `allowed` e `denied`, **nunca** indisponível. **Racional:** o cache é aceleração e nunca réplica (BA07); sem estado compartilhado a mais. **Trade-offs:** com várias instâncias, a mesma pergunta pode ser repetida uma vez por instância; uma concessão nova demora até 30 s para valer para quem acabou de ser negado, tolerância já aceita no PRD de `CAP-008`. **Alternativas rejeitadas:** cache em Valkey compartilhado (mais um local de estado do direito, contra G11); sem cache (mais carga em `commerce` sem ganho de consistência).
- **D-03 — Timeouts.** `media` → `commerce` e `learning` → `commerce`: **2 s**, sem nova tentativa no caminho da requisição (quem repete é o cliente). `bff-student` → `learning` e `media`: 5 s na abertura, renovação, playlist e chave; 3 s no avanço. `renewAfter` = fim da validade − 90 s dá ao player de 15 a 18 tentativas de 5 s antes de vencer. **Racional:** o orçamento de 5 s cobre a validação de sessão, o JWT, a decisão e o banco; valores em configuração. **Trade-offs:** uma decisão lenta vira "indisponível" mesmo que respondesse em 3 s. **Alternativas rejeitadas:** nova tentativa no servidor (multiplica a espera do aluno e a carga em `commerce` quando ele está lento).
- **D-04 — Reescrever a playlist por requisição, com cache só das originais.** `media` guarda em memória as playlists originais (imutáveis por vídeo, até 64 vídeos) e reescreve a cada resposta. **Racional:** evita uma leitura do S3 por pedido de playlist e mantém a reescrita barata. **Trade-offs:** memória por instância. **Alternativas rejeitadas:** reescrever uma vez e guardar a playlist reescrita no S3 (a base de endereço mudaria entre ambientes e o objeto guardado deixaria de ser neutro, contra a ADR-0006, item 4).
- **D-05 — hls.js como player.** **Racional:** é o que permite reescrever o endereço de cada pedido de segmento (`xhrSetup(xhr, url)`) e enviar credenciais do cookie nas playlists e na chave; o HLS nativo do navegador não permite as duas coisas. **Trade-offs:** dependência nova de frontend; navegadores sem suporte a MSE (incerto para iPhone anterior ao suporte; **não foi confirmado nesta spec**, matriz a validar em QA) mostram uma mensagem própria. Licença e versão a conferir ao instalar. **Alternativas rejeitadas:** `<video>` nativo (sem controle do endereço dos segmentos); Shaka Player (mesma função, sem vantagem que justifique a troca do que o time conhece).
- **D-06 — Controles próprios no player e tela cheia no contêiner.** **Racional:** a geometria dos controles nativos não é conhecida, e uma marca d'água que cubra os controles viola o PRD; tela cheia no `<video>` esconderia a marca. **Trade-offs:** mais código de acessibilidade. **Alternativas rejeitadas:** controles nativos com a marca em zona fixa (a posição da barra varia por navegador).
- **D-07 — Fato de avanço retido no outbox, publicado só quando houver consumidor.** `Outbox:RetainedRoutingKeys` lista as chaves cujas mensagens nascem já processadas e **não são publicadas**; o reenvio publica as retidas em ordem de gravação, numa fila indicada. A virada para o ao vivo é uma ordem fixa: o consumidor declara a fila e passa a consumir; `midia.reproducao-avancou.v1` sai da lista; **depois** o reenvio roda (publicando também o que já tenha saído ao vivo, o que é seguro porque o `eventId` é determinístico e o consumidor deduplica). **Racional:** é a forma que preserva a regra de nunca perder o avanço e não acende o alarme do outbox, no padrão do reenvio de fatos de vídeo já usado em `CAP-005`. **Trade-offs:** o registro cresce sem limpeza até `CAP-017` decidir. **Alternativas rejeitadas:** declarar uma fila de retenção do `media` (crescimento no broker e a posse da fila ficaria ambígua com `CAP-017`); publicar sem fila (esgota as tentativas); gravar numa tabela própria fora do outbox (duplica o mecanismo de reenvio).
- **D-08 — `eventId` determinístico do fato por (`sessionId`, `sequence`).** **Racional:** republicar ou reenviar nunca cria fato novo. **Trade-offs:** nenhum relevante.
- **D-09 — Janela de aceitação do avanço: 60 minutos depois de `expiresAt`.** **Racional:** cobre o aluno que fecha a aula depois do fim da validade e o computador que acorda de suspensão, sem aceitar avanço de sessão antiga indefinidamente. **Trade-offs:** um avanço de uma hora e um minuto depois é perdido (`410`). **Alternativas rejeitadas:** sem limite (sessão antiga vira canal de escrita); só até `expiresAt` (perderia o `left` do aluno que saiu na validade).
- **D-10 — Credencial do adaptador de desenvolvimento: `secure_link` do nginx.** Credencial `st` (hash do prefixo do vídeo, da expiração e de um segredo compartilhado com `media`) e `e` (expiração); o nginx a valida sem chamar `media`. **Racional:** a mesma forma de credencial por prefixo que o CloudFront usa, sem container nem endpoint de autorização. **Trade-offs:** o hash é MD5 (limite do módulo padrão), o que serve ao desenvolvimento e não vai além dele; o `secure_link` e o `auth_request` são módulos que **precisam estar na imagem escolhida do nginx, o que não foi verificado nesta spec** e é a primeira checagem de V-02. **Alternativas rejeitadas:** `auth_request` chamando `media` (um endpoint a mais, sem ganho).
- **D-11 — Política cliente-servidor.** Marca d'água, intervalo de avanço e intervalo mínimo vêm na resposta da sessão (contrato C-05). **Racional:** mudar a cadência não republica o SPA. **Trade-offs:** o player precisa ler e respeitar os valores.
- **D-12 — Sem cache de JWT de aluno nem da asserção de serviço.** **Racional:** ADR-0003 e ADR-0012 (item 6); medir primeiro. **Trade-offs:** uma validação em Identity e uma assinatura por requisição protegida; o volume estimado é de cerca de 3 validações por minuto por aluno assistindo.
- **D-13 — Fuso da data de término mostrada ao aluno.** O SPA formata `accessEndedAt` (último dia = dia anterior ao instante exclusivo) no fuso da escola, lido de configuração de execução (`SCHOOL_TIME_ZONE`, padrão `America/Sao_Paulo`, como a DP-03 de `CAP-008` fixa para a Fase 1). **Racional:** o BFF e os serviços não conhecem o fuso da escola. **Trade-offs:** o fuso é de ambiente, não por escola, até a Fase 1 acabar.

## Verificação

Só o que foge do padrão das skills de teste e observabilidade.

- **Cenários críticos não óbvios:**
  - **Renovação sob relógio:** usar relógio controlado em `media` e no player; cobrir a janela entre `renewAfter` e `expiresAt` com decisão indisponível que volta, e a que não volta.
  - **Separação ator × aluno:** token de ator em rota de aluno, e de aluno em rota de ator, em `media` e `learning`; token de aluno com audiência errada.
  - **Concorrência de sessões:** três sessões do mesmo aluno, e duas renovações simultâneas da mesma sessão (a validade final é a de uma delas, sem estender além de 5 min).
  - **Avanço concorrente:** dois avanços da mesma sessão ao mesmo tempo com a mesma `sequence` e com sequências adjacentes; só um fato por `sequence`.
  - **Republicação no meio da aula:** a aula some da versão vigente e a renovação mostra "não disponível".
  - **Fato retido:** saúde do outbox sem pendência; reenvio publica cada `eventId` uma vez; reenvio repetido não duplica efeito no consumidor de teste.
  - **E-mail em toda a saída:** busca do e-mail de teste em log, spans, métricas, mensagens do outbox, nomes de objeto, endereços e chaves de cache, em `identity`, `bff-student`, `media`, `learning` e no SPA.
  - **Credencial em telemetria do navegador:** `segmentAccess.query` ausente nos spans exportados.
- **Dados ou ambiente especiais:** ambiente de desenvolvimento do servidor (PostgreSQL, RabbitMQ, Valkey e MinIO do servidor; não subir infraestrutura local), com `scripts/remote-infra.sh provision` e `migrate` e `scripts/apps.sh start --remote` (AGENTS.md). Um vídeo preparado de verdade (pipeline de `CAP-006`) com pelo menos duas aulas publicadas e um aluno com cortesia (`CAP-008`). A borda `media-edge` sobe junto das aplicações. Testcontainers (PostgreSQL) para `media` e `learning`, **sem paralelismo** entre projetos de teste pesados.
- **Observabilidade além do padrão:** instrumentos de sessão por desfecho, decisão (latência e indisponibilidade), alunos ativos e tempo até começar; todos sem dado pessoal. A taxa de acerto de cache e o custo de distribuição vêm da distribuição e do provedor, fora da aplicação.
- **Verificação dos contratos:**
  - **HTTP:** cada `operationId` de [contracts.md](contracts.md) contra o serviço real; os cenários de "A implementação deve verificar" do índice são o roteiro.
  - **Mensagens:** envio do fato com os campos e sem os proibidos; idempotência por `eventId`; retenção e reenvio para uma fila de teste.
  - Validação de YAML (Spectral e parser) é distinta de conformidade; o contrato 1.0.0 não foi implantado em nenhum ambiente.
- **Matriz de navegadores:** o player por segmentos reescritos tem de ser conferido em Chrome, Firefox, Edge, Safari desktop e Safari de iPhone (incerto, ver D-05). Responsabilidade do QA de V-02.

## Questões em Aberto

- [ ] **O efeito da consulta de assinatura na chave de cache do CloudFront** não foi confirmado na documentação consultada; a documentação lista os parâmetros (`Policy`, `Signature`, `Key-Pair-Id`) mas não descreve a exclusão da chave de cache. — Tasso e plataforma, na primeira implantação com CloudFront (homologação). — Se a consulta entrar na chave de cache, cada sessão cria um objeto de cache próprio, derrubando o acerto (G22): é a primeira coisa a medir, e a política de cache sem consulta é a mitigação planejada.
- [ ] **De onde vem a taxa de acerto de cache no Kibana**, que depende de a plataforma ingerir métricas ou logs da distribuição, hoje inexistente em desenvolvimento. — Plataforma — Sem a fonte, o alerta de G22 é definido como código mas não dispara; só afeta homologação e produção.
- [ ] **Limiar de alerta do cache** (85% em 15 minutos) é inicial, sem dado real. — Tasso, depois dos primeiros alunos reais — Alerta pode ser ruidoso ou cego até ser recalibrado.
- [ ] **Navegadores suportados pelo player** (em especial Safari de iPhone) — QA de V-02 — Uma parte dos alunos pode ver "navegador sem suporte".
- [ ] **Limpeza e retenção das linhas retidas no outbox** depois do consumo — `CAP-017` — Crescimento de tabela; sem efeito funcional.
- [ ] **Quem materializa a medição de sobreposição de aulas distintas (BA16, QA-01 do PRD)** — Tasso, antes do PRD de `CAP-017` — O sinal que o baseline exige desde a Fase 1 fica sem dono.
- [ ] **Imagem do nginx da borda de desenvolvimento** tem de trazer `secure_link` (D-10) — implementador de V-02 — Se faltar, trocar de imagem ou de módulo, sem mudar o contrato.

## Architecture Decision Records

ADRs herdadas, aplicadas sem conflito: [ADR-0002](../../docs/adr/0002-plataforma-de-runtime-coolify.md) (S3 e CloudFront só para mídia); [ADR-0003](../../docs/adr/0003-verificacao-de-sessao-do-aluno.md) (validação de sessão em Identity a cada ação protegida, sem cache; conformado, D-12); [ADR-0004](../../docs/adr/0004-autenticacao-de-servico-bff-identity.md) e [ADR-0009](../../docs/adr/0009-autenticacao-de-servico-bff-aluno-em-servicos-de-dominio.md) (asserção de serviço); [ADR-0006](../../docs/adr/0006-preparacao-de-video-e-custodia-de-chave.md) (marcador de chave opaco e reescrita da playlist; conformado, D-04); [ADR-0008](../../docs/adr/0008-observabilidade-kibana-como-codigo.md) (painel como código).

ADRs novas, `Accepted` em 2026-10-03:

- [ADR-0012: `media` e `learning` como chamadores de `commerce` desde `CAP-007`](../../docs/adr/0012-autenticacao-de-servico-media-learning-em-commerce-revisada.md) — **substitui a [ADR-0011](../../docs/adr/0011-autenticacao-de-servico-media-learning-em-commerce.md)**, que era `Accepted` e previa `learning` só em `CAP-017`. Quando aceita, a ADR-0011 passa a `Superseded by ADR-0012`; os itens 1, 2, 3 e 5 são mantidos, o 4 muda e o 6 (sem cache de asserção) é novo.
- [ADR-0013: JWT de aluno validado localmente por serviços de domínio, com o e-mail só em `media`](../../docs/adr/0013-jwt-de-aluno-validado-por-servicos-de-dominio.md) — a chave de aluno no JWKS, a separação ator × aluno por escopo e `permissions`, e a exposição declarada do e-mail.
- [ADR-0014: Entrega de segmentos por credencial opaca e porta de distribuição](../../docs/adr/0014-entrega-de-segmentos-por-credencial-opaca-e-porta-de-distribuicao.md) — a credencial por prefixo, o endereço único por vídeo, e os adaptadores CloudFront e borda de desenvolvimento.
