---
tsg_artifact: techspec
product: code-4-coders
capability: CAP-017
version: 1.0
status: approved
updated: 2026-10-04
sources: tasks/prd-progresso-aluno/prd.md@1.0, tasks/prd-progresso-aluno/contracts.md@1.0, context/architecture-baseline.md@1.2, domains/entrega-de-midia-e-protecao/domain.md@1.0, domains/matricula-e-direito-de-acesso/domain.md@1.0, domains/conteudo-e-curriculo/domain.md@1.1
---

# TechSpec — progresso do aluno

> **Escopo:** Full-stack (`learning` módulo Progress, `commerce` Matrícula, `media` outbox e configuração, `bff-student`, `student-spa`, painel no Kibana)
> **Modo:** Pipeline, API-First
> **PRD de origem:** [prd.md](prd.md), v1.0, aprovado em 2026-10-04
> **Contratos de integração:** [contracts.md](contracts.md) 1.0 (aprovado em 2026-10-04); OpenAPI [do BFF do aluno](api-contract.yaml) 1.1.0, [interno de `learning`](internal-api-contract-learning.yaml) 1.3.0 e [interno de `commerce`](internal-api-contract-commerce.yaml) 1.3.0; AsyncAPI [de `learning`](asyncapi-contract.yaml) 1.2.0 e [de `media`](asyncapi-contract-media.yaml) 1.3.0
> **Data:** 2026-10-04
> **Status:** Aprovada em 2026-10-04
> **Handoff:** liberado para geração de Tasks

## Resumo Executivo

O progresso nasce no módulo **Progress** de `learning`, que já existe como módulo e schema declarados (`LearningModules.cs:6`, `LearningSchemas.cs:6`) e ainda não tem nada. Ele consome o avanço que `media` emite desde `CAP-007`, guarda cada fato bruto e mantém, por aluno e aula, a última posição e a conclusão. Percentual, retomada e Continuar são calculados **na leitura**, sobre a versão vigente de Conteúdo, que está no mesmo serviço.

- **O avanço retido já tem caminho de saída.** `media` grava o fato como "processado" sem publicar (`OutboxMessageWriter.cs:16`, configurado em `appsettings.json:36`) e já tem a operação de reenvio para a fila `learning.playback-progress` (`VideoReplayOperationsExtensions.cs:29`, `OutboxPublisherWorker.cs:134`). A entrega liga a fila em `learning`, **deixa de reter** e executa o reenvio uma vez. Nenhum código novo em `media`, só configuração e a ordem de implantação.
- **A duração vem do fato de vídeo pronto que `learning` já consome**, gravada no schema de progresso. A gravação **não** depende do recibo de Conteúdo, o que permite preencher a duração de vídeos antigos com o reenvio de fatos de vídeo que `media` já oferece (`VideoReplayOperationsExtensions.cs:23`). Assim o efeito aceito de C-07 deixa de ser necessário.
- **"Meus cursos" é composto por `learning`**: lista de Matrícula (`listStudentCourseAccessInternal`, nova em `commerce`) + título e aulas da versão vigente + progresso. O escopo de serviço novo está na [ADR-0015](../../docs/adr/0015-escopos-de-servico-media-learning-em-commerce-por-rota.md) (Accepted), que substitui a ADR-0012.
- **A tela "meus cursos" é o Início** (`/`), destino do login sem página de origem (`student-login-screen.tsx:76`). DP-08 já vale; a entrega preenche o lugar reservado em `dashboard-screen.tsx:63`.
- **O design vem antes do código de tela** (EN-01).

**Trade-off primário:** calcular percentual, retomada e Continuar na leitura, sobre a versão vigente, deixa o progresso correto em qualquer republicação sem reprocessar nada (RN-C07 a RN-C09), mas cada leitura de "meus cursos" lê versão vigente e progresso de até 500 cursos e faz uma chamada síncrona a `commerce`. O volume do MVP é pequeno; o custo é medido e só vira materialização com dado real.

## Arquitetura da Solução

```text
student-spa  /            (Início = meus cursos) ── GET /api/v1/my-courses ─────────────┐
             /aulas/:id   (tela da aula)         ── GET /api/v1/lessons/{id}   (CAP-007) │
                                                 ── GET /api/v1/courses/{id}/progress ───┤
bff-student  valida a sessão em Identity e pede JWT aud=learning (scope lessons:read)   │
             ──► learning  GET /internal/v1/student-courses ───────────────► commerce GET /internal/v1/course-access
                           GET /internal/v1/student-courses/{id}/progress ─► commerce GET /internal/v1/access-decision (cache ≤ 30 s)
learning  Progress (schema progress)
             ◄── fila learning.playback-progress ◄── media.events  midia.reproducao-avancou.v1
             ◄── fila learning.video-availability (existente) ◄── midia.ativo-pronto.v1 (duração)
             lê a versão vigente de Content (mesmo serviço, por interface de leitura do módulo Content)
media     deixa de reter midia.reproducao-avancou.v1; reenvio único dos retidos (operação existente)
```

**URL pública.** Nenhuma rota nova no SPA. "Meus cursos" é `<origem do student-spa><BASE_PATH>` (raiz; localmente `http://localhost:8082/student/`). Continuar e Começar levam a `<origem><BASE_PATH>aulas/{continueLessonId}`, a rota de `CAP-007`. Evidência: abrir a raiz logado, clicar Continuar e cair na aula esperada, com a reprodução na posição esperada (V-02, V-03).

### Bloco Backend

**`learning` — Progress.**

Três registros no schema `progress`, por escola (G07):

- **Avanço bruto**, um por fato, com `event_id` como chave: `session_id`, `student_id`, `course_id`, `lesson_id`, `sequence`, `position_seconds`, `reason`, `occurred_at`, `received_at`. É ao mesmo tempo o recibo de idempotência (inserção que conflita no `event_id` não faz nada) e o insumo de `CAP-029` (RN-P03, OD71). Índice por (escola, aluno, curso) e por (escola, `occurred_at`).
- **Progresso na aula**, um por (escola, aluno, aula): `course_id`, última posição, `occurred_at` e `sequence` do avanço que a definiu, `reason` desse avanço, maior posição já vista, `completed_at` (nulo enquanto não concluída), `last_activity_at`. Atualizado na **mesma transação** da inserção do avanço bruto, só quando a inserção aconteceu.
- **Duração do vídeo**, uma por (escola, vídeo): `duration_seconds`, `occurred_at`, `event_id`.

Regras do consumo de `midia.reproducao-avancou.v1` (por fato, na transação):

1. Inserir o avanço bruto; se já existia (`event_id`), confirmar a mensagem sem mais nada (RN-P01).
2. Última posição: o fato só substitui a posição se (`occurred_at`, `sequence`) for maior que o par guardado (RN-P02). Senão, só a maior posição e a atividade podem mudar.
3. Maior posição vista = máximo entre a guardada e a do fato.
4. Conclusão (RN-P04): `reason = ended`, ou maior posição ≥ 90% da duração do vídeo **que a aula usa na versão vigente**. A aula → vídeo vem da versão vigente de Content (`PublishedLesson.VideoId`, `PublishedLesson.cs:3`); a duração, do registro de duração. Sem versão vigente com a aula ou sem duração, só `ended` conclui por ora. `completed_at` só é gravado uma vez e nunca apagado (RN-P05).
5. `reason` desconhecido é guardado como veio e tratado como `heartbeat` (contrato, item 6).
6. Curso ou aula desconhecidos são registrados; nada é criado em Content.

**Duração (C-07).** A recepção de `midia.ativo-pronto.v1` (`VideoProjectionStore.cs`) passa a gravar também a duração no schema de progresso. Duas regras:

- A gravação **fica fora do bloco protegido pelo recibo de Content** (`VideoProjectionStore.cs:16`): é um upsert idempotente por (escola, vídeo), que só substitui com (`occurred_at`, `event_id`) maior. Assim um reenvio de fatos de vídeo, que Content ignora por já ter o recibo, ainda preenche a duração. Content continua sem duração (RN-C04, RN-C15).
- **Reavaliação ao chegar a duração:** na mesma transação, as aulas da versão vigente que usam aquele vídeo e cujo progresso tem maior posição ≥ 90% e `completed_at` nulo são concluídas, sem esperar novo avanço (contrato, item 4).

**Leituras do aluno** (`listStudentCoursesInternal`, `getStudentCourseProgressInternal`), com a política de aluno existente (`LearningAuthorization.Student`), aluno = `sub`:

- **Progresso do curso:** versão vigente do curso na escola (404 `COURSE_NOT_AVAILABLE`) → decisão de acesso pelo cliente existente (`AccessDecisionClient`, cache de 30 s compartilhado com a tela da aula; 403/503) → aulas da versão vigente + progresso do aluno nessas aulas → contagem, percentual arredondado para baixo (RN-P07), e por aula com registro: `completed`, `lastPositionSeconds`, `resumeAtSeconds` (RN-P09: 0 se o último avanço foi `ended`, se faltam menos de 10 s para a duração, ou se a posição passa da duração; sem duração conhecida, a própria posição, salvo `ended`).
- **Meus cursos:** `listStudentCourseAccessInternal` em `commerce` (cliente novo, mesma asserção com escopo `course-access:read`, **sem cache**, timeout de 2 s como a decisão). Sem resposta → 503 `COURSE_ACCESS_UNAVAILABLE`. Com resposta: versões vigentes dos cursos listados numa consulta (curso sem versão vigente é omitido), progresso do aluno nesses cursos numa consulta, e por curso: título, contagem e percentual, `started`, `lastActivityAt` e `continueLessonId` (RN-P10 sobre a ordem do currículo vigente). Ordenação: `active` por `lastActivityAt` decrescente, nunca começados por último por `since` decrescente; `ended` por `endedAt` decrescente. Falha na leitura do progresso (e só nela) → `progressAvailable: false`, `progress: null`, `started: null`, `continueLessonId` = primeira aula.

**Fronteira de módulo.** Progress lê a versão vigente por uma interface de leitura do módulo Content (as consultas de aula vigente que `GetStudentLesson` já usa, `IStudentLessonQueries.cs`), nunca pelas tabelas de `content`. Content não lê nada de Progress.

**`commerce` — Matrícula.** Rota nova `GET /internal/v1/course-access` com política própria (escopo `course-access:read`, ADR-0015). Consulta sobre `access_grants` por (escola, aluno), com o índice existente (`AccessGrantConfiguration.cs:32`), agregando por curso com a mesma semântica de `AccessDecisionQueries.cs:11`: `active` se há concessão com `status = 'active'` e (`expires_at` nulo ou maior que agora); senão `ended`, com `endedOn` = maior `ends_on` e `endedAt` = maior `expires_at` entre as concessões terminadas. `endedReason` = `grant-ended` (revogação não existe antes de `CAP-009`). Nada de motivo, origem ou `grantId` na resposta. `Cache-Control: private, no-store`.

**`media` — só configuração e operação.** Retirar `midia.reproducao-avancou.v1` de `Outbox:RetainedRoutingKeys` (`appsettings.json:36`) **depois** que a fila de `learning` existir: a publicação é `mandatory` (`RabbitMqPublisher.cs:43`) e falharia sem fila ligada. Depois, executar uma vez o reenvio existente com `Outbox__ProgressFactReplayTenantId` (fila `learning.playback-progress`, `OutboxOptions.cs:29`). O reenvio publica todas as linhas do canal da escola, retidas e já publicadas, em ordem de `occurred_on`, com o mesmo `eventId`; a deduplicação de `learning` absorve as repetidas. Opcional, para preencher duração de vídeos antigos: o reenvio de fatos de vídeo com `Outbox__VideoFactReplayTenantId`.

**Transação, consistência e eventos.** Cada fato de avanço é uma transação (bruto + progresso na aula). Nenhum fato é publicado por `learning` nesta entrega. Leituras são consistentes com o que foi consumido até o momento; o atraso esperado é o do consumo (alvo ≤ 1 min p95).

### Bloco Frontend

**Telas.** Duas mudanças, nenhuma rota nova:

- **Início (`/`) vira "meus cursos"**: substitui o lugar reservado em `dashboard-screen.tsx:63`. Seções "Seus cursos" (vigentes, com percentual, "N de M aulas", Continuar ou Começar) e "Acesso encerrado" (secundária, sem ação, "Seu acesso terminou em dd/mm/aaaa"). Estados: carregando; vazio ("Você ainda não tem cursos" com caminho para a vitrine `/cursos`); indisponível (503, com *Tentar de novo*; nunca o vazio); progresso indisponível (lista sem percentual, com aviso). O card de conta existente permanece.
- **Tela da aula** (`student-lesson-screen.tsx`, `student-lesson-nav.tsx`): a lista ganha a marca de concluída (texto além do ícone) e o percentual do curso; perto do player, o aviso "Retomando de m:ss" com *Começar do início*; caminho de volta para o Início.

**Onde mora o estado.** Servidor: meus cursos e progresso do curso no cache do `@tanstack/react-query`, sem persistência. Cliente: só se o aviso de retomada foi dispensado. O progresso **não** vai para armazenamento do navegador.

**Pontos que o implementador não deriva:**

- **Posição inicial do player.** O hls.js é criado com `startPosition: positionRef.current` (`use-protected-playback.ts:228`), hoje 0 numa aula nova. A posição inicial passa a ser `resumeAtSeconds` da aula aberta. A abertura do player **espera** o progresso do curso responder ou falhar, com teto de 2 s; passado o teto, começa do início e não salta depois (um salto no meio da reprodução é pior que começar do zero). Falha do progresso nunca bloqueia o vídeo (C-01).
- **Atualização da lista.** O progresso do curso é relido ao abrir a aula, ao pausar, ao chegar ao fim e a cada 60 s de reprodução (`x-frontend-notes` do contrato). Não usa `refetchInterval` fora da reprodução.
- **Continuar** usa `continueLessonId` como veio; o SPA não recalcula RN-P10.
- **Acessibilidade.** Concluída em texto; percentual com texto equivalente ("3 de 8 aulas concluídas"); aviso de retomada numa região de status, alcançável por teclado e sem sumir enquanto tem foco.

## Mapa de Fatias Verticais

A parte de tela de qualquer fatia só começa depois do EN-01.

### V-01: O avanço da reprodução vira progresso por aula, sem perder o que foi retido

- **Cobre:** RF-01, RF-02, RN-P01 a RN-P06, OD71, contrato `receberReproducaoAvancou`, `receberAtivoPronto` (revisada), `reenviarReproducaoAvancouRetido`; métricas do PRD "Avanço registrado" e "Atraso de progresso".
- **Entrada / gatilho:** fatos `midia.reproducao-avancou.v1` e `midia.ativo-pronto.v1`; reenvio dos retidos.
- **Processamento:** migration do schema `progress` (pela ferramenta do EF); fila `learning.playback-progress` com DLQ e limite de entregas, ligada a `media.events` pela rota do avanço, no inicializador de topologia de `learning`; consumidor no padrão de `VideoProjectionConsumerWorker` com as regras 1 a 6 do Bloco Backend; gravação da duração fora do recibo de Content e reavaliação de conclusão; configuração de `media` sem retenção do avanço; reenvio único. Métricas: fatos consumidos, mandados à fila de erro, e histograma de atraso (agora − `occurredAt`).
- **Saída observável:** em `learning`, cada aluno tem a última posição e a conclusão corretas por aula; os avanços retidos desde `CAP-007` aparecem registrados uma vez; o painel mostra fatos consumidos e o atraso.
- **Evidência / checkpoint:** integração de `learning` com Testcontainers: duplicado → um registro; fora de ordem (14:10/480 e 14:02/252) → 480; dois aparelhos → o mais recente pelo instante; aula de 600 s → 540 conclui e 530 não; `ended` conclui; avanço antes da duração e depois o fato de vídeo → conclui sem novo avanço; rever não desfaz; `reason` novo não conclui; aula fora da versão vigente registrada; nada além dos campos do fato guardado. Teste de contrato do payload recebido contra `contracts/media/asyncapi.yaml`. Integração de `media`: com a retenção desligada e a fila de teste ligada, o fato é publicado; o reenvio publica as linhas retidas com o mesmo `eventId`. Cenário no ambiente de desenvolvimento: avanços retidos de `CAP-007` → implantar `learning`, desligar retenção, reenviar → contagem de avanços brutos igual à de linhas do outbox de `media` daquela rota; repetir o reenvio → nenhuma mudança.
- **Bloqueado por:** Nenhum.

### V-02: Ao abrir a aula, o aluno retoma de onde parou e vê as concluídas e o percentual

- **Cobre:** RF-03, RF-05 (na aula), RF-06, RN-P07, RN-P09, DP-03, DP-04, DP-05, DP-07; `getCourseProgress` → `getStudentCourseProgressInternal`.
- **Entrada / gatilho:** o aluno abre `/aulas/{lessonId}`; pausa, chega ao fim ou assiste por 60 s.
- **Processamento:** BFF: rota nova com audiência `learning` (tabela de rotas, `BffSecurityOptions.cs:24`) e mapeamento dos erros do contrato. `learning`: verificações na ordem do contrato, cálculo na leitura. SPA: leitura em paralelo com `getStudentLesson`; posição inicial do player por `resumeAtSeconds` com teto de 2 s; marca de concluída e percentual na lista; aviso "Retomando de" com Começar do início; relê nos gatilhos.
- **Saída observável:** fechar o navegador em 4:12 e voltar abre em 4:12 com o aviso; Começar do início volta a 0:00; aula concluída marcada; passar dos 90% marca a aula em até 1 minuto sem recarregar; com o progresso fora do ar, o vídeo toca do início e a lista fica sem marcas.
- **Evidência / checkpoint:** integração de `learning`: 3 de 8 → 37; 2 de 3 → 66; 10/10 republicado com 12 → 83; removida fora das duas contas e de volta numa versão seguinte; RN-P09 nos três casos de reinício; sem concessão → 403 sem dado; `commerce` sem resposta → 503; curso de outra escola → 404. Integração do BFF (audiência, erros). Testes do SPA: posição inicial, teto de 2 s sem salto tardio, aviso e Começar do início, marca e percentual, releitura. Cenário no navegador com cortesia real: assistir até 4:12, fechar, voltar.
- **Bloqueado por:** V-01; EN-01 (tela).

### V-03: O aluno vê os cursos vigentes em "meus cursos" e continua de onde parou

- **Cobre:** RF-04 (vigentes, vazio), RF-05 (Continuar/Começar), RN-P08, RN-P10, DP-08, ADR-0015; `listMyCourses` → `listStudentCoursesInternal` → `listStudentCourseAccessInternal`.
- **Entrada / gatilho:** o aluno entra sem página de origem ou abre o Início.
- **Processamento:** `commerce`: rota, política e escopo novos (ADR-0015), agregação por curso; configuração do escopo de `learning` nos três compose. `learning`: cliente da lista (escopo como parâmetro na emissão da asserção, hoje fixo em `AccessDecisionAssertionFactory.cs:23`), composição, ordenação, Continuar. BFF: rota com audiência `learning`. SPA: Início com a seção de vigentes, Continuar/Começar, estado vazio.
- **Saída observável:** com cortesia em 2 cursos, o começado aparece primeiro com "37% · 3 de 8 aulas" e Continuar, que abre a aula certa na posição certa; o outro com Começar na primeira aula; sem concessão, "Você ainda não tem cursos" com caminho para a vitrine; progresso de curso sem concessão não aparece.
- **Evidência / checkpoint:** integração de `commerce`: asserção de `media` → 403 `SCOPE_DENIED`; asserção de `learning` com `access-decision:read` → 403; aluno de outra escola → lista vazia; várias concessões no mesmo curso → um item; resposta sem motivo, origem ou `grantId`. Integração de `learning`: os cinco casos de RN-P10, ordenação, curso sem versão vigente omitido. Testes do SPA para a lista, Continuar e vazio. Cenário no navegador: financeiro concede duas cortesias; o aluno entra, vê a lista e continua.
- **Bloqueado por:** V-01; EN-01 (tela).

### V-04: "Meus cursos" mostra o acesso encerrado e não mente quando algo falha

- **Cobre:** RF-04 (encerrados, indisponível, progresso indisponível, nova cortesia), DP-02, C-09.
- **Entrada / gatilho:** aluno com cortesia vencida; `commerce` sem resposta; progresso ilegível.
- **Processamento:** `learning` monta `ended` com `endedOn`, `endedReason` e progresso; 503 quando a lista não vem; `progressAvailable: false` quando só o progresso falha. SPA: seção "Acesso encerrado" sem ação; estado indisponível com *Tentar de novo*; aviso de progresso indisponível.
- **Saída observável:** cortesia vencida em 15/03/2028 com 5 de 10 → em "Acesso encerrado" com 50% e a data; nova cortesia → volta a "Seus cursos" com o progresso; `commerce` fora → mensagem de indisponibilidade, nunca o vazio; progresso fora → lista sem percentual com aviso.
- **Evidência / checkpoint:** integração de `learning` com `commerce` substituído por servidor de teste (lista com encerrado, sem resposta) e com falha induzida na leitura do progresso; testes do SPA dos três estados. Cenário no ambiente de desenvolvimento: antecipar o `expires_at` de uma concessão por script de apoio (como em `CAP-007`) e ver o curso migrar de seção.
- **Bloqueado por:** V-03.

### Habilitadores inevitáveis

| Habilitador | Por que não cabe numa fatia | Menor escopo | Primeira fatia desbloqueada |
|---|---|---|---|
| EN-01 | O fluxo do projeto exige wireframe ASCII → Figma → aprovação antes de código de tela; a aprovação é do responsável pelo produto | `docs/design/wireframes-progresso.md`: Início "meus cursos" (vigentes, encerrados, vazio, carregando, indisponível, progresso indisponível) e as mudanças na tela da aula (marca de concluída, percentual, aviso de retomada, volta ao Início), desktop e celular, com registro de aprovação no cabeçalho no formato de `wireframes-aula.md`; e os frames no Figma | V-02 (tela), V-03 (tela), V-04 (tela) |

## Contratos e Fronteiras

O conjunto de contratos deste PRD está em [contracts.md](contracts.md); esta spec não duplica schemas.

### Mapeamento de mensagens e dados

| Contrato e identificador | Aplicação/produtor e consumidores | Comportamento a implementar | Evidência |
|---|---|---|---|
| AsyncAPI `learning` 1.2.0 `receberReproducaoAvancou` | `media` → `learning` (receive) | Regras 1 a 6 do Bloco Backend; fila `learning.playback-progress`, DLQ, reprocessável sem efeito duplicado | V-01 |
| AsyncAPI `learning` 1.2.0 `receberAtivoPronto` | `media` → `learning` (receive) | Duração no schema de progresso fora do recibo de Content; reavaliação de conclusão | V-01 |
| AsyncAPI `media` 1.3.0 `reenviarReproducaoAvancouRetido` | `media` (send) | Operação existente `ReplayProgressFactsAsync`; só configuração e ordem | V-01 |

**Dado pessoal.** O avanço bruto liga aluno (identificador opaco), sessão, aula e instante: é dado pessoal pseudonimizado. Percurso: criado em `media` (outbox, retido ou publicado) → RabbitMQ (fila `learning.playback-progress` e sua DLQ) → `progress` em `learning` (avanço bruto e progresso por aula). Nenhum e-mail, nome ou título em nenhum ponto; nada disso em log, atributo de span, métrica ou routing key (G23): os logs do consumidor registram `eventId` e o motivo da recusa, nunca o payload. Descarte: o prazo de guarda é a QA-01 do PRD; até lá nada é apagado, e a solicitação do titular (`CAP-031`) terá de alcançar as duas tabelas e o outbox de `media`.

### Mapeamento do contrato de API

| operationId | Caminho de implementação |
|---|---|
| `listMyCourses` | BFF: rota autenticada, audiência `learning`, repasse e mapeamento de erros → `listStudentCoursesInternal` |
| `getCourseProgress` | BFF: idem → `getStudentCourseProgressInternal` |
| `listStudentCoursesInternal` | `learning` Api (política de aluno) → caso de uso de Progress → cliente de Matrícula + leitura da versão vigente + leitura do progresso |
| `getStudentCourseProgressInternal` | `learning` Api → caso de uso de Progress → versão vigente → `AccessDecisionClient` → leitura do progresso |
| `listStudentCourseAccessInternal` | `commerce` Api (política `course-access:read`) → caso de uso de Matrícula → consulta agregada de `access_grants` |

**Validações além do contrato:**

| operationId | Regra | Camada |
|---|---|---|
| `getStudentCourseProgressInternal` | Ordem: versão vigente → decisão → dados; nada do curso antes da decisão positiva | application |
| `listStudentCoursesInternal` | Curso da lista de Matrícula sem versão vigente na escola é omitido; progresso nunca acrescenta curso | application |
| `listStudentCourseAccessInternal` | `active` pela mesma regra de `decideAccessInternal`; `studentId` sempre o que veio na consulta, escola sempre a da asserção | application/infra |

**Exceção → resposta HTTP:**

| Situação | HTTP | `code` |
|---|---|---|
| Curso sem versão vigente na escola | 404 | `COURSE_NOT_AVAILABLE` |
| Decisão negada | 403 | `ACCESS_DENIED` (com `reason` e `accessEndedAt`, como em `CAP-007`) |
| Decisão sem resposta | 503 | `ACCESS_DECISION_UNAVAILABLE` |
| Lista de Matrícula sem resposta | 503 | `COURSE_ACCESS_UNAVAILABLE` |
| Identity indisponível no BFF, nas rotas novas | 502/504 | `UPSTREAM_UNAVAILABLE`/`UPSTREAM_TIMEOUT` (nunca 503: o SPA leria como Matrícula fora) |

### Mapeamento de jornada

| História (PRD) | Tela / componente | operationId ou ação local | Evidência |
|---|---|---|---|
| Ver os cursos num só lugar | Início | `listMyCourses` | V-03 |
| Botão Continuar | Início | `continueLessonId` → rota da aula | V-03 |
| Voltar do ponto em que parou | Tela da aula, player | `getCourseProgress` → `resumeAtSeconds` | V-02 |
| Ver concluídas e quanto falta | Tela da aula (lista) e Início | `getCourseProgress`, `listMyCourses` | V-02, V-03 |
| Acesso terminou | Início, "Acesso encerrado" | `listMyCourses` (`ended`) | V-04 |
| Dois aparelhos | Tela da aula | RN-P02 no consumo | V-01, V-02 |

### Entidades do domínio

| Entidade | Representação técnica | Local |
|---|---|---|
| Progresso (Aprendizagem) | Progresso na aula, por (escola, aluno, aula) | `learning`, schema `progress` |
| Conclusão de Aula (Aprendizagem) | `completed_at` do progresso na aula | idem |
| Avanço (Mídia, recebido) | Avanço bruto, por `event_id` | idem |
| Mídia — duração | Duração do vídeo, por (escola, vídeo) | idem |
| Concessão de Acesso e Vigência (Matrícula) | `access_grants`, só leitura agregada | `commerce` |

## Arquivos a Modificar e a Referenciar

### A modificar

| Caminho | Fatia | Alteração |
|---|---|---|
| `src/learning/src/CodeForCoders.Learning.Infra.Messaging/RabbitMqTopologyInitializer.cs` | V-01 | Fila `learning.playback-progress` + DLQ, ligada à rota do avanço em `media.events` |
| `src/learning/src/CodeForCoders.Learning.Infra.Messaging/Configuration/RabbitMqOptions.cs` | V-01 | Nome da fila do avanço (padrão `learning.playback-progress`, igual a `OutboxOptions.ProgressFactReplayQueue` de `media`) |
| `src/learning/src/CodeForCoders.Learning.Infra.Messaging/VideoProjectionStore.cs` | V-01 | Duração no schema de progresso fora do recibo; reavaliação de conclusão |
| `src/learning/src/CodeForCoders.Learning.Infra.Messaging/DependencyInjection.cs` | V-01 | Registrar o consumidor do avanço |
| `src/learning/src/CodeForCoders.Learning.Infra.Data/LearningDbContext.cs` | V-01 | Entidades de progresso no schema `progress` |
| `src/media/src/CodeForCoders.Media.Api/appsettings.json` | V-01 | Retirar `midia.reproducao-avancou.v1` de `Outbox:RetainedRoutingKeys` |
| `src/learning/src/CodeForCoders.Learning.Api/Extensions/EndpointExtensions.cs` | V-02, V-03 | Mapear as duas leituras do aluno |
| `src/learning/src/CodeForCoders.Learning.Api/Security/AccessDecisionAssertionFactory.cs` | V-03 | Escopo como parâmetro (hoje fixo, linha 23) |
| `src/learning/src/CodeForCoders.Learning.Api/Extensions/AccessDecisionConfigurationExtensions.cs` | V-03 | Registrar o cliente da lista de Matrícula com o mesmo timeout |
| `src/commerce/src/CodeForCoders.Commerce.Api/Security/ServiceAssertionScopes.cs` | V-03 | `course-access:read` |
| `src/commerce/src/CodeForCoders.Commerce.Api/Extensions/ServiceAssertionExtensions.cs` | V-03 | Política da rota nova |
| `src/commerce/src/CodeForCoders.Commerce.Api/Security/ServiceAssertionAuthenticationHandler.cs` | V-03 | Não contar a rota nova como leitura de vitrine (linhas 87–92) |
| `src/commerce/src/CodeForCoders.Commerce.Api/Extensions/EndpointExtensions.cs` | V-03 | Mapear a rota nova |
| `docker-compose.yml`, `docker-compose.remote.yml`, `docker-compose.coolify.yml` | V-03 | `ServiceAssertions__Issuers__learning__AllowedScopes__1: course-access:read` |
| `src/bff-student/src/CodeForCoders.BffStudent.Application/Common/BffSecurityOptions.cs` | V-02, V-03 | `RouteAudiences`: `/api/v1/my-courses` e `/api/v1/courses` → `learning` |
| `src/bff-student/src/CodeForCoders.BffStudent.Api/Security/BffSecurityMiddleware.cs` | V-02, V-03 | Falha de Identity nas rotas novas → 502/504, nunca 503 (linha 117) |
| `src/student-spa/src/features/student-dashboard/components/dashboard-screen.tsx` | V-03, V-04 | Lugar reservado (linha 63) vira "meus cursos" |
| `src/student-spa/src/app/routes/dashboard-route.tsx` | V-03 | Composição com a lista |
| `src/student-spa/src/features/student-lessons/components/student-lesson-screen.tsx` | V-02 | Leitura do progresso, aviso de retomada, caminho para o Início |
| `src/student-spa/src/features/student-lessons/components/student-lesson-nav.tsx` | V-02 | Marca de concluída e percentual |
| `src/student-spa/src/features/student-lessons/hooks/use-protected-playback.ts` | V-02 | Posição inicial (linha 228) e gatilhos de releitura |
| `scripts/kibana/` (mecanismo da ADR-0008) | V-01 | Consumo, fila de erro e atraso do progresso |
| `docs/adr/0012-autenticacao-de-servico-media-learning-em-commerce-revisada.md`, `docs/adr/index.md` | V-03 | `Superseded by ADR-0015` quando a ADR-0015 for aceita |

### A referenciar (não alterar)

| Caminho | Por que consultar |
|---|---|
| `src/media/src/CodeForCoders.Media.Infra.Messaging/OutboxPublisherWorker.cs:134` | Reenvio do avanço: ordem, mesmo `eventId`, fila esperada |
| `src/media/src/CodeForCoders.Media.Api/Extensions/VideoReplayOperationsExtensions.cs` | Como acionar os dois reenvios |
| `src/learning/src/CodeForCoders.Learning.Infra.Messaging/VideoProjectionConsumerWorker.cs` | Padrão de consumidor (retry, DLQ, métricas, telemetria) |
| `src/learning/src/CodeForCoders.Learning.Api/Clients/AccessDecisionClient.cs` | Cache, timeout e falha fechada da decisão |
| `src/learning/src/CodeForCoders.Learning.Application/UseCases/StudentLessons/GetStudentLesson/` | Ordem 404 → decisão e leitura da versão vigente |
| `src/commerce/src/CodeForCoders.Commerce.Infra.Data/Entitlement/AccessDecisionQueries.cs:11` | Semântica de "ativa" a reproduzir na lista |
| `src/bff-student/src/CodeForCoders.BffStudent.Api/Clients/StudentLessonLearningClient.cs` | Padrão de repasse e mapeamento de erros |
| `src/contract-testing/AsyncApiContract.cs` | Validação do payload recebido contra o AsyncAPI |
| `docs/design/wireframes-aula.md` | Formato do registro de aprovação e telas de base |

## Análise de Impacto

| Componente | Tipo | Impacto e risco | Ação requerida |
|---|---|---|---|
| `media` outbox | modificado (config) | Desligar a retenção antes da fila existir faz a publicação `mandatory` falhar e acender o alarme do outbox | Ordem: `learning` com fila → desligar retenção → reenvio |
| RabbitMQ | modificado | Fila nova `learning.playback-progress` e DLQ; ~2 mensagens por minuto por aluno assistindo | Medir profundidade e DLQ no painel |
| Postgres de `learning` | novo schema em uso | Tabela de avanço bruto cresce sem prazo até QA-01 | Índices por aluno/curso e por instante; prazo em QA-01 |
| `commerce` | modificado | Rota nova consultada a cada abertura do Início | Índice existente; sem cache, conforme ADR-0015 |
| `learning` → `commerce` | modificado | Segunda dependência síncrona de `learning` | Timeout de 2 s; 503 distinto |
| ADR-0012 | substituída | Item 2 muda | ADR-0015 aceita em 2026-10-04 |
| Contratos em `contracts/` | a promover | Cinco recortes | Na conclusão do PRD |
| Outros domínios | — | `CAP-029` passa a ter o avanço bruto para consultar | Nenhuma agora |

## Riscos e Preocupações

| Preocupação | Local (`arquivo:linha`) | Impacto | Mitigação |
|---|---|---|---|
| Publicação `mandatory` sem fila ligada | `src/media/src/CodeForCoders.Media.Infra.Messaging/RabbitMqPublisher.cs:43` | Desligar a retenção cedo demais esgota tentativas no outbox | Ordem de implantação de V-01; o smoke confere a fila antes |
| Retenção configurada só em `appsettings` | `src/media/src/CodeForCoders.Media.Api/appsettings.json:36` | Um override por ambiente poderia manter ou repor a retenção sem ninguém ver | Nenhum compose sobrescreve hoje (verificado); V-01 confere a publicação em desenvolvimento |
| Duração só dentro do recibo de Content | `src/learning/src/CodeForCoders.Learning.Infra.Messaging/VideoProjectionStore.cs:16` | Reenvio de fato de vídeo já visto não preencheria a duração | Gravar a duração fora do bloco do recibo (Bloco Backend) |
| Escopo fixo na asserção de `learning` | `src/learning/src/CodeForCoders.Learning.Api/Security/AccessDecisionAssertionFactory.cs:23` | A chamada da lista sairia com `access-decision:read` e levaria 403 | Escopo como parâmetro; teste dos dois escopos |
| Telemetria de vitrine conta toda rota que não é a decisão | `src/commerce/src/CodeForCoders.Commerce.Api/Security/ServiceAssertionAuthenticationHandler.cs:87` | A rota nova inflaria `ShowcaseReads` | Condição pela política da rota nova também |
| Erro de Identity nas rotas de aula vira 503 | `src/bff-student/src/CodeForCoders.BffStudent.Api/Security/BffSecurityMiddleware.cs:117` | Se as rotas novas entrassem na mesma regra, o SPA mostraria "Matrícula indisponível" por falha de Identity | Rotas novas mapeiam para 502/504 |
| Posição inicial fixa no início | `src/student-spa/src/features/student-lessons/hooks/use-protected-playback.ts:228` | Retomada não acontece, ou acontece com salto no meio | Espera do progresso com teto de 2 s, sem salto tardio |
| Mapa de aulas em armazenamento do navegador | `src/student-spa/src/features/student-lessons/utils/known-course-store.ts:28` | Já guarda identificadores de aula no `localStorage` (de `CAP-007`); não pode passar a guardar progresso | Progresso só no cache do react-query |

## Decisões Técnicas

- **Decisão:** progresso calculado na leitura, sobre a versão vigente; persistido só avanço bruto, progresso por aula e duração.
  **Racional:** republicação não exige reprocessamento (RN-C07 a RN-C09, DP-07); a regra de percentual vive num lugar.
  **Trade-offs:** cada leitura lê versão vigente e progresso.
  **Alternativas rejeitadas:** percentual materializado por curso (teria de ser recalculado a cada publicação e a cada fato).
- **Decisão:** o avanço bruto é a própria tabela de recibo do consumidor.
  **Racional:** uma escrita por fato em vez de duas; é o mesmo registro que `CAP-029` vai consultar.
  **Trade-offs:** a tabela cresce até QA-01.
  **Alternativas rejeitadas:** recibo separado + avanço bruto (duas tabelas com a mesma chave).
- **Decisão:** a duração é gravada fora do recibo de Content, por upsert idempotente.
  **Racional:** permite preencher vídeos antigos com o reenvio que `media` já tem, sem carga nova; Content segue sem duração.
  **Trade-offs:** a recepção do fato de vídeo faz duas escritas com regras de idempotência diferentes.
  **Alternativas rejeitadas:** receber a duração por fila própria (outra ligação ao mesmo fato); aceitar sem duração os vídeos antigos (C-07, efeito aceito, que deixa de ser necessário).
- **Decisão:** a abertura do player espera o progresso por até 2 s e nunca salta depois.
  **Racional:** retomar é o valor da entrega; um salto no meio da reprodução é pior que começar do zero.
  **Trade-offs:** até 2 s a mais no início, só quando o progresso demora.
  **Alternativas rejeitadas:** começar e saltar quando chegar; esperar sem teto (o progresso bloquearia o vídeo, contra C-01).

## Verificação

- **Cenários críticos não óbvios:** ordem de implantação de V-01 (retenção ligada sem fila → nada publicado; fila ligada e retenção desligada → publicado; reenvio depois → contagem igual, repetido → igual); concorrência de dois fatos da mesma aula em consumidores paralelos (a regra de par maior precisa valer sob concorrência, não só em sequência); duração chegando depois do avanço; tetos de 500 cursos e 20000 aulas.
- **Dados ou ambiente especiais:** Testcontainers de Postgres e RabbitMQ em `learning` e `media`, sequencialmente (instrução do projeto). Em desenvolvimento, conforme AGENTS.md: infraestrutura remota (`scripts/remote-infra.sh provision && migrate`, `scripts/apps.sh start --remote`), cortesia real concedida pelo financeiro, e o script de apoio de `CAP-007` para antecipar `expires_at` (V-04).
- **Observabilidade além do padrão:** em `learning`, contadores de fatos de avanço consumidos e mandados à fila de erro, e histograma de atraso (agora − `occurredAt`), sem identificador de aluno nos atributos; painel e alerta de atraso p95 > 60 s pelo mecanismo da ADR-0008. "Avanço registrado" (PRD) = avanços brutos ÷ linhas do outbox de `media` daquela rota, conferido por consulta no cenário de V-01.
- **Verificação dos contratos:** payload recebido validado contra `contracts/media/asyncapi.yaml` (helper de `src/contract-testing/`); respostas HTTP de `learning` e `commerce` validadas contra os recortes deste PRD nos testes de integração (os nomes dos testes de contrato existentes servem de padrão: `AccessDecisionContract`).

## Questões em Aberto

- [ ] Prazo de guarda do avanço bruto e das linhas do outbox de `media` depois do reenvio (QA-01 do PRD) — encarregado de dados + negócio — sem prazo, a tabela cresce e a solicitação do titular fica sem regra de descarte.
- [ ] Registrar a duração na junta Mídia → Aprendizagem do Domain Map (QA-02 do PRD) — Tasso — a junta existe só no PRD e aqui.

## Architecture Decision Records

- [ADR-0015: Autenticação de serviço de `media` e `learning` em `commerce` com um escopo por rota](../../docs/adr/0015-escopos-de-servico-media-learning-em-commerce-por-rota.md) — **Accepted** (2026-10-04); substitui a ADR-0012 (item 2): `course-access:read` só para `learning`, lista sem cache.
- [ADR-0013: JWT de aluno validado por serviços de domínio](../../docs/adr/0013-jwt-de-aluno-validado-por-servicos-de-dominio.md) — aluno = `sub`; as leituras novas usam a audiência e o escopo existentes de `learning`.
- [ADR-0003: Verificação de sessão do aluno](../../docs/adr/0003-verificacao-de-sessao-do-aluno.md) — o BFF valida a sessão a cada leitura.
- [ADR-0008: Observabilidade do Kibana como código](../../docs/adr/0008-observabilidade-kibana-como-codigo.md) — painel do progresso.
- [ADR-0012](../../docs/adr/0012-autenticacao-de-servico-media-learning-em-commerce-revisada.md) — Superseded by ADR-0015.
