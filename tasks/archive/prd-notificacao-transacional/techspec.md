# Especificação Técnica — Notificação transacional por e-mail (fatia de fundação)

> **Escopo:** Backend
> **Modo:** Pipeline
> **PRD de origem:** `tasks/prd-notificacao-transacional/prd.md` (v1.1, aprovado 2026-09-21)
> **Contratos de integração:** `tasks/prd-notificacao-transacional/contracts.md` e
> `tasks/prd-notificacao-transacional/asyncapi-contract.yaml` (AsyncAPI 3.0.0, v1.0.0, aprovado para
> implementação)
> **Data:** 2026-09-21
> **Status:** Aprovado
> **Handoff:** approved — pode alimentar o Task Creator

Este documento cobre apenas o lado **Notificação** (`src/notification`) desta fatia. O lado
Identidade e Acesso (publicar `notificacao.envio-solicitado` na criação de conta e na solicitação de
recuperação) pertence ao PRD de `CAP-001`, ainda não escrito — esta entrega "não depende de nenhuma
capacidade" (PRD, §Rastreabilidade) e existe para que `CAP-001` tenha o que consumir.

---

## Resumo Executivo

O serviço `notification` ganha um consumidor RabbitMQ para `notificacao.envio-solicitado.v1`, que
aceita ou recusa cada pedido por validade, verifica consentimento (mecanismo real, resposta fixa
nesta fatia), renderiza um de dois Modelos de Mensagem fixos no código, entrega por e-mail e publica
o desfecho pelo outbox já existente no serviço. Toda a canalização de outbox→RabbitMQ (`OutboxMessage`,
`OutboxPublisherWorker`, `RabbitMqPublisher`) e o adaptador de envio (`ITransactionalEmailSender`,
`HttpTransactionalEmailSender`) já existem no scaffold de fundação e são reaproveitados sem alteração
de contrato.

**Decisão arquitetural principal:** a garantia de entrega ao **broker** (mensagem chega ao consumidor)
e a garantia de entrega ao **provedor de e-mail** (RF-06, RN-N11) são desacopladas. O consumidor grava
o Registro de Entrega e confirma (`ack`) a mensagem assim que o pedido é validado — não a retém presa
no RabbitMQ. A tentativa de entrega ao provedor, com espaçamento crescente até um limite, roda como um
processo próprio sobre o Registro de Entrega persistido, no mesmo padrão de *poll* já usado por
`OutboxPublisherWorker`.

**Trade-off primário:** ganha-se controle total sobre o espaçamento crescente e sobre a idempotência
por `pedidoId` sem depender de plugin de retardo do RabbitMQ (a imagem `rabbitmq:4.3-management-alpine`
do `docker-compose.yml` não o inclui). Perde-se o aproveitamento automático do sinal "DLQ não vazia" do
baseline (`context/architecture-baseline.md:405-406`) para o caso de esgotamento de tentativas ao
provedor — esta fatia precisa emitir seu próprio sinal para RN-N12, já que a mensagem não fica retida
na fila até esgotar (ver **Decisões Técnicas**, item 1).

---

## Arquitetura da Solução

Componentes que esta fatia cria ou altera em `src/notification`. `identity`, `bff-*`, `commerce` etc.
não são tocados.

### Bloco Backend

- **Consumidor** de `notificacao.envio-solicitado.v1`, no mesmo padrão de
  `HeartbeatConsumerWorker` (`ack` manual, `BasicQos`, `AsyncDefaultBasicConsumer`): mensagem
  malformada (payload que não deserializa) é `nack(requeue: false)` e cai no DLQ nativo da fila — é
  poison message, precisa de correção de código, não de negócio. Mensagem bem formada mas
  inválida por regra de negócio (finalidade desconhecida, modelo desconhecido, dado do modelo
  faltante) é **aceita e acked**, e o Registro de Entrega nasce com situação "recusado" — recusa é
  desfecho de negócio, não falha de transporte.
- **Caso de uso** que orquestra: validar → dedupe/gravar Registro de Entrega → verificar consentimento
  → renderizar Modelo de Mensagem → entregar → registrar desfecho → publicar pelo outbox. A regra de
  negócio de aceitação/recusa (RF-01) e de consentimento (RF-04) vive na Application; a máquina de
  estados do Registro de Entrega (aceito → entregue | recusado | falhou) vive no agregado de Domain.
- **Dois Modelos de Mensagem** fixos no código, selecionados por `Finalidade` (`confirmacao-de-conta`,
  `recuperacao-de-senha`), cada um renderizando `nome`, `link` e a validade vigente (parâmetro por
  finalidade — RN-N13/14/15), sem link de descadastro (RN-N06). Não são editáveis nem persistidos —
  isso é `CAP-027`.
- **Um processo próprio de reentrega ao provedor**, no mesmo padrão de `OutboxPublisherWorker`
  (`PeriodicTimer`, seleção do próximo pendente, retentativa com backoff crescente, limite de
  tentativas parametrizado), operando sobre o Registro de Entrega — não sobre a fila AMQP.
- **Um processo de expurgo** dos campos pessoais do Registro de Entrega após a janela de retenção
  (RN-N16), preservando um fato agregado sem dado pessoal.
- **Reaproveitado sem alteração de contrato:** `IOutboxMessageWriter`/`OutboxMessage`/
  `OutboxPublisherWorker`/`RabbitMqPublisher` para publicar `notificacao.mensagem-entregue.v1` e
  `notificacao.entrega-falhou.v1`; `ITransactionalEmailSender`/`HttpTransactionalEmailSender` para o
  envio em si (a classificação de erro do provedor é um acréscimo sobre o adaptador existente, não uma
  porta nova).

### Transação, consistência e eventos

- Aceitar o pedido = uma transação: grava o Registro de Entrega (situação "aceito" ou "recusado") e o
  `ack` da mensagem AMQP acontece só depois do commit — mesma disciplina que já vale para o outbox
  (baseline, princípio 6: nenhuma chamada de rede dentro de transação de banco).
- A chamada de rede ao provedor de e-mail acontece **fora** dessa transação, no processo de reentrega.
  Sucesso ou falha definitiva grava a transição de estado do Registro de Entrega **no mesmo
  `SaveChangesAsync`** que grava a linha do outbox para `mensagem-entregue`/`entrega-falhou` — outbox
  obrigatório no produtor, nunca publicação direta (G06).
- Reentrega do mesmo `pedidoId` pelo broker é absorvida na gravação do Registro de Entrega (chave
  `tenant_id + pedido_id` única): a segunda tentativa de insert não produz segundo registro nem
  segunda entrega (RN-N09).

---

## Mapa de Fatias Verticais

### V-01: Aceitar e entregar confirmação de conta (caminho feliz)

- **Cobre:** RF-01 (aceite válido), RF-02, RF-04 (consentimento permite transacional), RF-05
  (registro "entregue", corpo não conservado), RF-07 (publicar `mensagem-entregue`); RN-N01–N08,
  RN-N10, RN-N13, RN-N14; RN-26–28 (Identidade e Acesso, do lado do contrato consumido).
- **Entrada / gatilho:** mensagem em `notificacao.envio-solicitado.v1` com `finalidade:
  confirmacao-de-conta`, modelo conhecido, `dados` completos (`nome`, `link`).
- **Processamento:** o consumidor deserializa e valida a forma do payload; o caso de uso grava
  Registro de Entrega "aceito" (chave `pedidoId`), confirma a mensagem; verifica consentimento
  (`IConsentimentoService` — para as duas finalidades desta fatia, sempre permite, RN-N05); renderiza
  o Modelo de Mensagem de confirmação com `nome`, `link` e a validade vigente do parâmetro
  `confirmacao-de-conta` (RN-N14/15); chama `ITransactionalEmailSender`; em sucesso, marca "entregue" e
  grava no mesmo commit o outbox de `mensagem-entregue` (com `destinatario` em claro, por exigência de
  RF-07 — exceção declarada, não vazamento).
- **Saída observável:** e-mail recebido pelo destinatário (ou pelo fake do provedor em teste);
  Registro de Entrega com situação "entregue" e timestamps de cada transição; evento
  `notificacao.mensagem-entregue.v1` publicado sem o corpo da mensagem.
- **Evidência / checkpoint:** teste de integração (Testcontainers Postgres+RabbitMQ, já usados em
  `NotificationIntegrationFixture`) publicando o pedido no broker e observando: Registro de Entrega
  final = "entregue"; requisição chegou ao fake do provedor com o texto certo; evento publicado na fila
  de saída sem o corpo; nenhum e-mail em claro em log/span/métrica emitidos durante o fluxo (RN-N07).
- **Bloqueado por:** Nenhum.

### V-02: Recusar pedido inválido

- **Cobre:** RF-01 (segundo a quinto critério).
- **Entrada / gatilho:** pedido bem formado sem `finalidade`, ou com `modelo` desconhecido, ou com
  `dados` faltando `nome`/`link`.
- **Processamento:** o caso de uso recusa **antes** de qualquer renderização ou chamada ao provedor —
  nenhuma mensagem parcial é composta. Grava Registro de Entrega "recusado" com o motivo (finalidade
  ausente | modelo desconhecido | dado faltante) e confirma a mensagem (recusa é desfecho de negócio,
  não erro de transporte).
- **Saída observável:** Registro de Entrega "recusado" com motivo; nenhuma chamada ao provedor;
  nenhum outbox de `mensagem-entregue` gravado.
- **Evidência / checkpoint:** teste de integração por cenário (`finalidade` ausente, `modelo`
  desconhecido, `dados` incompleto) verificando o motivo persistido e a ausência de chamada ao fake do
  provedor.
- **Bloqueado por:** V-01.

### V-03: Idempotência — reentrega vs. reenvio

- **Cobre:** RF-01 (sexto critério), RF-02 (terceiro critério); RN-N09.
- **Entrada / gatilho:** (a) o mesmo `pedidoId` chega duas vezes (redelivery do broker); (b) um
  `pedidoId` novo chega com a mesma `finalidade` e o mesmo destinatário (reenvio deliberado, ex.:
  aluno pede reenvio de confirmação).
- **Processamento:** (a) a segunda gravação do mesmo `pedidoId` colide com a chave única
  `tenant_id + pedido_id` — o caso de uso trata a colisão como "já processado", confirma a mensagem e
  **não** chama o provedor de novo. (b) `pedidoId` novo é um Registro de Entrega novo, com seu próprio
  ciclo completo — os dois registros coexistem.
- **Saída observável:** (a) um único Registro de Entrega, um único e-mail entregue, mesmo com duas
  entregas da mensagem AMQP. (b) dois Registros de Entrega distintos, dois e-mails entregues.
- **Evidência / checkpoint:** teste de integração publicando o mesmo `pedidoId` duas vezes em sequência
  e verificando uma única chamada ao fake do provedor; teste publicando dois `pedidoId`s distintos com
  mesma finalidade/destinatário e verificando dois Registros de Entrega e duas chamadas.
- **Bloqueado por:** V-01.

### V-04: Entregar recuperação de senha (segundo modelo)

- **Cobre:** RF-03; RN-N15; prova o Objetivo 3 do PRD (acrescentar modelo não altera RF-01/04/05/06).
- **Entrada / gatilho:** pedido aceito com `finalidade: recuperacao-de-senha`.
- **Processamento:** mesmo pipeline de V-01, selecionando o Modelo de Mensagem de recuperação e a
  validade vigente do parâmetro `recuperacao-de-senha` (RN-N15 — prazo independente do de confirmação
  de conta). Sem link de descadastro (RN-N06, mesmo motivo de V-01).
- **Saída observável:** e-mail de recuperação entregue; Registro de Entrega "entregue" com
  `finalidade: recuperacao-de-senha`.
- **Evidência / checkpoint:** teste de integração análogo a V-01, trocando a finalidade; teste
  garantindo que nenhum teste de V-01/V-02/V-03 precisou mudar para este cenário passar (evidência do
  Objetivo 3).
- **Bloqueado por:** V-01.

### V-05: Falha transitória do provedor até esgotar as tentativas

- **Cobre:** RF-06 (primeiro, terceiro e quarto critério), RF-07 (falha); RN-N09, RN-N11, RN-N12.
- **Entrada / gatilho:** o provedor responde com erro classificado como transitório (5xx, 429,
  timeout) em chamadas sucessivas; e, em paralelo, novos pedidos continuam chegando enquanto isso
  acontece.
- **Processamento:** cada falha transitória incrementa a contagem de tentativas do Registro de Entrega
  e agenda a próxima com espaçamento crescente (RF-06, primeiro critério), sem re-enfileirar no
  RabbitMQ — o pedido já foi aceito e confirmado (RN-N11: aceito e retido, não recusado). Pedidos novos
  continuam sendo aceitos normalmente mesmo com o provedor indisponível, porque a aceitação (V-01/V-02)
  não depende da entrega. Ao esgotar o limite de tentativas, a situação vira "falhou" com
  `esgotouTentativas: true`, o outbox de `entrega-falhou` é gravado no mesmo commit, e um sinal de
  observabilidade próprio de "pedido em tratamento manual" é emitido (ver **Decisões Técnicas**, item 1
  — esta fatia não usa o DLQ nativo do broker para este caso, então o sinal não é automático).
- **Saída observável:** Registro de Entrega "falhou", `esgotouTentativas: true`; evento
  `notificacao.entrega-falhou.v1` publicado com o motivo; sinal de "tratamento manual" emitido;
  nenhum pedido novo recusado durante a indisponibilidade.
- **Evidência / checkpoint:** teste de integração com fake do provedor configurado para falhar
  transitoriamente N vezes seguidas, verificando o espaçamento crescente entre tentativas, o estado
  final e o evento publicado; teste publicando um segundo pedido durante a janela de falha e
  verificando que ele é aceito normalmente.
- **Bloqueado por:** V-01.

### V-06: Falha definitiva imediata do provedor

- **Cobre:** RF-06 (segundo critério).
- **Entrada / gatilho:** o provedor responde com erro classificado como definitivo (endereço
  inexistente, recusa permanente — 4xx que não seja 429).
- **Processamento:** nenhuma nova tentativa é agendada; a situação vai direto para "falhou" com
  `esgotouTentativas: false`, com o motivo classificado; outbox de `entrega-falhou` gravado no mesmo
  commit.
- **Saída observável:** Registro de Entrega "falhou" após uma única tentativa; evento
  `notificacao.entrega-falhou.v1` com `esgotouTentativas: false`.
- **Evidência / checkpoint:** teste de integração com fake do provedor retornando erro definitivo na
  primeira chamada, verificando ausência de reentrega e o evento publicado.
- **Bloqueado por:** V-05 (reaproveita a classificação transitória/definitiva introduzida ali).

### V-07: Expurgo do dado pessoal do Registro de Entrega

- **Cobre:** DP-08, RN-N16.
- **Entrada / gatilho:** um Registro de Entrega ultrapassa a janela de retenção com dado pessoal
  (parâmetro — número pendente de QP-01).
- **Processamento:** um processo agendado limpa `destinatario` e o motivo detalhado (recusa/falha) do
  Registro de Entrega após a janela, preservando `finalidade`, situação final e timestamps agregados —
  e incrementa, no mesmo commit da transição original de estado (não no expurgo), um contador por
  `tenant_id + finalidade + situação + dia`, sem dado pessoal, que **não** é expurgado. É o contador,
  não a linha expurgada, que é "o fato agregado" que o PRD pede que permaneça.
- **Saída observável:** após a janela, consulta ao Registro de Entrega não revela mais o destinatário
  nem o motivo em claro; o contador agregado por finalidade/situação/dia permanece consultável.
- **Evidência / checkpoint:** teste de integração criando um Registro de Entrega com data de corte já
  vencida, rodando o job de expurgo e verificando: campos pessoais nulos, contador agregado
  incrementado e estável antes/depois do expurgo.
- **Bloqueado por:** V-01.

---

## Contratos e Fronteiras

### Mapeamento de mensagens e dados

| Contrato e identificador | Aplicação/produtor e consumidores | Comportamento a implementar | Evidência |
|---|---|---|---|
| AsyncAPI: operação `receberPedidoDeEnvio` (canal `notificacao.envio-solicitado.v1`) | Identidade e Acesso publica (fora desta fatia); Notificação recebe | Aceite/recusa por validade (V-01, V-02); idempotência por `pedidoId` (V-03); pedido aceito mesmo com provedor indisponível (V-05) | Cenários de `contracts.md` §Validação e verificação |
| AsyncAPI: operação `publicarMensagemEntregue` (canal `notificacao.mensagem-entregue.v1`) | Notificação publica, pelo outbox existente | Publicado só em entrega confirmada, sem o corpo, com `destinatario` em claro (exceção declarada de RF-07) | V-01, V-04 |
| AsyncAPI: operação `publicarEntregaFalhou` (canal `notificacao.entrega-falhou.v1`) | Notificação publica, pelo outbox existente | Publicado em falha definitiva imediata (`esgotouTentativas: false`, V-06) ou após esgotar tentativas (`esgotouTentativas: true`, V-05), sempre com `motivo` | V-05, V-06 |

Schemas e exemplos vivem em `asyncapi-contract.yaml`; não duplicados aqui. Não há contrato AsyncAPI
anterior para esta junta — nenhuma diferença de versão a tratar.

### Entidades do domínio

| Entidade (Domain Map / PRD) | Representação técnica nesta fatia | Local |
|---|---|---|
| Registro de Entrega | Agregado raiz persistido, chave única `tenant_id + pedido_id`; duplo papel de inbox de idempotência (RN-N09) e registro consultável (RF-05) — ver **Decisões Técnicas**, item 2. Guarda finalidade, modelo, situação, timestamps de cada transição e motivo — **nunca o corpo renderizado da mensagem** (RN-N08). Campos pessoais (`destinatario`, motivo detalhado) sujeitos a expurgo por V-07 | `CodeForCoders.Notification.Domain`, schema `notification_access` |
| Contador agregado por finalidade/situação/dia | Linha incrementada no mesmo commit da transição de estado do Registro de Entrega, sem dado pessoal, nunca expurgada — sobrevive ao V-07 | `CodeForCoders.Notification.Domain`, schema `notification_access` |
| Canal | Enum de valor único (`email`), não é tabela nesta fatia (Domain Map admite push/WhatsApp — `CAP-027`) | `CodeForCoders.Notification.Application` (já existe: `NotificationChannels.Email`) |
| Modelo de Mensagem | Renderer fixo no código por `Finalidade` (dois casos: confirmação de conta, recuperação de senha); não editável, não persistido — `CAP-027` acrescenta edição pelo negócio | `CodeForCoders.Notification.Application` |
| Consentimento | Porta de decisão (`IConsentimentoService` ou equivalente) sem estado próprio nesta fatia: para as duas finalidades transacionais, sempre permite (RN-N05). Descadastro e estado persistido de consentimento promocional ficam para `CAP-027` — mas a porta existe desde este primeiro envio (Objetivo 2 do PRD) | `CodeForCoders.Notification.Application` |
| Pedido de envio (`Notificação`, no Domain Map) | Não persistido isoladamente; é a entrada que origina o Registro de Entrega | — |

---

## Arquivos a Modificar e a Referenciar

### A modificar

| Caminho | Fatia | Alteração |
|---|---|---|
| `src/notification/src/CodeForCoders.Notification.Infra.Messaging/Configuration/RabbitMqOptions.cs` | V-01 | Nova fila/routing key para o pedido de envio e sua DLQ; parâmetro de limite de tentativas de reentrega ao provedor (distinto do `DeliveryLimit` já existente, que é do broker) |
| `src/notification/src/CodeForCoders.Notification.Infra.Messaging/RabbitMqTopologyInitializer.cs` | V-01 | Declarar e vincular a nova fila (`notificacao.envio-solicitado.v1`) e sua DLQ, seguindo a mesma convenção já aplicada à fila de heartbeat |
| `src/notification/src/CodeForCoders.Notification.Infra.Data/Configuration/EmailOptions.cs` (ou seção de opções nova ao lado) | V-01, V-04 | Parâmetros RN-N13/14/15: domínio de envio (se distinto do remetente) e validade por finalidade (`confirmacao-de-conta`, `recuperacao-de-senha`) |
| `src/notification/src/CodeForCoders.Notification.Infra.Data/Adapters/HttpTransactionalEmailSender.cs` | V-05, V-06 | Classificar a resposta do provedor em transitória/definitiva antes de propagar (hoje `EnsureSuccessStatusCode()` trata todo erro igual — ver **Riscos e Preocupações**) |
| `src/notification/src/CodeForCoders.Notification.Infra.Data/NotificationDbContext.cs` | V-01, V-07 | Registrar o novo `DbSet` do Registro de Entrega e do contador agregado |
| `src/notification/src/CodeForCoders.Notification.Infra.Data/Migrations/` | V-01, V-07 | Nova migration para as tabelas acima (gerada, não escrita à mão) |
| `src/notification/src/CodeForCoders.Notification.Infra.Data/DependencyInjection.cs` | V-01, V-05, V-07 | Registrar os novos serviços/opções |
| `src/notification/src/CodeForCoders.Notification.Infra.Messaging/DependencyInjection.cs` | V-01, V-05 | Registrar o novo consumidor e o novo worker de reentrega ao provedor |
| `src/notification/src/CodeForCoders.Notification.Application/DependencyInjection.cs` | V-01 | Registrar o novo caso de uso e a porta de consentimento |

### A referenciar (não alterar)

| Caminho | Por que consultar |
|---|---|
| `src/notification/src/CodeForCoders.Notification.Infra.Messaging/HeartbeatConsumerWorker.cs` | Padrão de consumidor manual-ack já estabelecido: `BasicQos`, `AsyncDefaultBasicConsumer`, `nack(requeue:false)` para payload malformado, propagação de `traceparent` na `Activity` de consumo |
| `src/notification/src/CodeForCoders.Notification.Infra.Messaging/OutboxPublisherWorker.cs` | Padrão de *poll* com `PeriodicTimer` + seleção `FOR UPDATE SKIP LOCKED` a reaproveitar no novo processo de reentrega ao provedor |
| `src/notification/src/CodeForCoders.Notification.Application/Interfaces/ITransactionalEmailSender.cs` | Porta de envio já existente; reaproveitar sem mudar a assinatura pública, só o resultado/exceção de erro |
| `context/architecture-baseline.md:253-262` | Convenção de mensageria (outbox obrigatório, inbox no consumidor, DLQ por fila) que esta fatia aplica |
| `context/architecture-baseline.md:405-413` | Sinais de observabilidade obrigatórios desde a Fase 1 e a divisão emissão (aplicação) / roteamento de alerta (plataforma) — resolve como RN-N12 se cumpre sem inventar pipeline de alerta |
| `domains/identidade-e-acesso/domain.md:119-146` | Contrato completo da junta do lado de quem publica (RN-26–28), para não reabrir a decisão de modelagem já fechada em `contracts.md` |

---

## Análise de Impacto

| Componente | Tipo | Impacto e risco | Ação requerida |
|---|---|---|---|
| Fila `notificacao.envio-solicitado.v1` e sua DLQ no RabbitMQ | Novo | Consumo adicional de recursos do broker do grupo inicial; nenhuma mudança em serviço existente | Nenhuma nos demais serviços — Identidade e Acesso só passa a publicar quando `CAP-001` for implementado |
| Schema `notification_access` (novas tabelas) | Novo | Cresce armazenamento Postgres do serviço `notification`; nenhum outro serviço lê este schema (regra de propriedade dos dados) | Nenhuma |
| Provedor de e-mail transacional externo | Dependência externa ainda não contratada (risco já registrado no PRD) | Bloqueia tráfego real em produção; não bloqueia desenvolvimento/testes, que usam um fake da porta `ITransactionalEmailSender` | Contratação é caminho crítico do MVP, fora do escopo de código desta TechSpec |

---

## Riscos e Preocupações

| Preocupação | Local (`arquivo:linha`) | Impacto | Mitigação |
|---|---|---|---|
| `HttpTransactionalEmailSender` trata toda resposta de erro do provedor da mesma forma (`response.EnsureSuccessStatusCode()` lança para qualquer status ≥ 400) | `src/notification/src/CodeForCoders.Notification.Infra.Data/Adapters/HttpTransactionalEmailSender.cs:29` | RF-06 exige diferenciar falha transitória (retry) de definitiva (sem retry); sem essa distinção, toda falha vira retry infinito ou toda falha vira definitiva, os dois errados | Classificar a resposta antes de propagar (ver **Arquivos a Modificar**, mesma linha, e **Decisões Técnicas**, item 4) |
| `EmailOptions` não tem parâmetro de validade por finalidade nem de domínio de envio separado do remetente | `src/notification/src/CodeForCoders.Notification.Infra.Data/Configuration/EmailOptions.cs:1-13` | RN-N13/14/15 exigem esses parâmetros; sem eles, a validade fica hardcoded no texto do modelo, exatamente o que RN-N14 proíbe | Nova seção de opções (ver **Arquivos a Modificar**) |
| Nenhum mecanismo de mascaramento de e-mail existe hoje em nenhum dos serviços do monorepo (busca não encontrou utilitário de máscara) | — (ausência, não um arquivo) | RN-N07/G10/G23 exigem mascaramento em log/span/métrica desde o primeiro envio; é requisito novo desta fatia, não dívida herdada | Aplicar a máscara nos pontos de log/span deste caso de uso e do consumidor (a instrumentação segue o padrão OpenTelemetry já usado em `HeartbeatConsumerWorker`, só a formatação do valor muda) |
| `OutboxPublisherWorker` para de selecionar uma mensagem quando `attempts >= MaxAttempts`, mas não emite nenhum sinal de esgotamento hoje | `src/notification/src/CodeForCoders.Notification.Infra.Messaging/OutboxPublisherWorker.cs:71-75` | O sinal "outbox esgotado", obrigatório desde a Fase 1 (baseline), não está implementado; fora do escopo direto desta fatia (mecanismo pré-existente do scaffold de fundação) | Não corrigir aqui; mas o novo processo de reentrega ao provedor (V-05) não deve repetir a lacuna — emitir o sinal de esgotamento explicitamente é parte do próprio RF-06 (RN-N12) |
| Corrida entre duas reentregas simultâneas do mesmo `pedidoId` antes do primeiro insert commitar (duas instâncias do consumidor, mesma mensagem redelivered em paralelo) | — (comportamento a garantir na modelagem nova, não um arquivo existente) | Sem restrição de unicidade, duas linhas de Registro de Entrega para o mesmo `pedidoId` violam RN-N09 | Restrição única `tenant_id + pedido_id` na tabela nova; tratar violação de unicidade como "já processado", não como erro |

---

## Decisões Técnicas

- **Decisão:** desacoplar a garantia de entrega ao broker (ack imediato do pedido) da garantia de
  entrega ao provedor de e-mail (retry com backoff crescente em um processo próprio sobre Postgres, no
  padrão de `OutboxPublisherWorker`), em vez de usar `nack`/requeue e uma escada de filas de retry com
  TTL crescente no próprio RabbitMQ.
  - **Racional:** RN-N11 exige que a indisponibilidade do provedor "não vire erro no cadastro" e que o
    pedido seja "aceito e retido" — reter no broker via requeue prenderia a garantia de reentrega ao
    `x-delivery-limit` da fila e multiplicaria o problema de idempotência a cada redelivery física. Reter
    em Postgres dá controle total sobre o espaçamento crescente sem depender de um plugin de mensagens
    atrasadas que a imagem `rabbitmq:4.3-management-alpine` do `docker-compose.yml` não inclui.
  - **Trade-offs:** perde-se o aproveitamento de graça do sinal "DLQ não vazia" do baseline para o caso
    de esgotamento de tentativas ao provedor — esta fatia precisa emitir seu próprio sinal (RN-N12).
  - **Alternativas rejeitadas:** escada de filas de retry com TTL crescente e dead-lettering nativo do
    broker — rejeitada por exigir plugin de delayed-message não presente no ambiente atual e por acoplar
    a contagem de "tentativas ao provedor" ao número de redeliveries brutas do broker, que deixa de ser
    1:1 assim que houver mais de uma instância do consumidor.

- **Decisão:** o Registro de Entrega funciona simultaneamente como inbox de idempotência (RN-N09,
  chave `pedidoId`) e como o registro consultável exigido por RF-05 — não há tabela de inbox separada.
  - **Racional:** as duas exigências compartilham a mesma chave natural (`pedidoId`); duas tabelas
    duplicariam a garantia sem ganho observável.
  - **Trade-offs:** se um dia o dedup precisar de um ciclo de vida diferente do registro de entrega
    (por exemplo, TTL curto de dedup vs. retenção longa de auditoria), será preciso separar — não é o
    caso nesta fatia (RN-N16 já define retenção em duas camadas sobre a mesma linha).
  - **Alternativas rejeitadas:** inbox key-value dedicado em Valkey (já disponível no serviço, usado
    hoje só para o heartbeat) — rejeitada porque o dedup aqui precisa sobreviver além do ciclo de vida
    típico de um cache e já tem lugar natural na tabela transacional que RF-05 exige de qualquer forma.

- **Decisão:** retenção em duas camadas (RN-N16) implementada como (a) expurgo agendado dos campos
  pessoais do próprio Registro de Entrega após a janela paramétrica, e (b) um contador agregado por
  `tenant_id + finalidade + situação + dia`, incrementado no mesmo commit da transição de estado
  original (não no momento do expurgo), sem dado pessoal e nunca expurgado.
  - **Racional:** literal ao texto do PRD — "o fato agregado... permanece" enquanto "o registro com
    dado pessoal... é descartado". O contador é o que sobrevive ao expurgo sem reter e-mail nem motivo
    detalhado.
  - **Trade-offs:** consulta operacional por destinatário individual deixa de funcionar após a janela
    de retenção — é exatamente o que a LGPD exige (princípio da necessidade), não um efeito colateral
    indesejado.
  - **Alternativas rejeitadas:** manter a linha por mensagem indefinidamente, só anonimizando os campos
    (sem contador separado) — rejeitada porque não produz um "fato agregado" no sentido que o PRD pede;
    o volume de linhas por mensagem individual cresceria sem limite por um dado cuja finalidade
    operacional (diagnosticar "não recebi o e-mail") já expirou.
  - O número da janela (dias) permanece pendência aberta — ver **Questões em Aberto**, QP-01.

- **Decisão:** a classificação de erro do provedor (transitório vs. definitivo) mora no adaptador de
  Infra.Data e cruza para a Application já traduzida (resultado de domínio ou exceção própria do
  vocabulário de Notificação) — nunca o código de status HTTP do provedor.
  - **Racional:** princípio de camada anticorrupção do baseline — tipo, código de erro ou DTO de
    terceiro não cruza para `Domain` nem para o vocabulário da Application.
  - **Trade-offs:** nenhum relevante nesta fatia.

Nenhuma das decisões acima estabelece convenção nova para outros serviços do monorepo — todas aplicam
princípios já aceitos (outbox, camada anticorrupção, poll com backoff) a um problema local desta
fatia. Nenhuma ADR nova é necessária (ver **Architecture Decision Records**).

---

## Verificação

- **Cenários críticos não óbvios:**
  - Redelivery do broker do mesmo `pedidoId` chegando **enquanto** o primeiro processamento ainda está
    em voo (corrida entre duas instâncias do consumidor) — a segunda deve resultar em no-op, não em
    exceção não tratada.
  - Timeout do provedor (nem sucesso nem erro explícito) deve ser classificado como transitório, não
    como definitivo.
  - Esgotamento de tentativas durante uma indisponibilidade prolongada do provedor — verificar que o
    sinal de "tratamento manual" é emitido exatamente uma vez por Registro de Entrega, não uma vez por
    tentativa.
  - Expurgo (V-07) não deve alterar o contador agregado já incrementado, nem incrementá-lo de novo.
- **Dados ou ambiente especiais:** um fake configurável de `ITransactionalEmailSender` (sucesso, falha
  transitória, falha definitiva, timeout) para os testes de integração e E2E — `HttpTransactionalEmailSender`
  aponta hoje para um endpoint fictício (`EmailOptions.Endpoint` em `appsettings.json`), e não há
  provedor real contratado ainda (ver **Riscos**, PRD).
- **Observabilidade além do padrão:** métrica própria de "pedido em tratamento manual" (contagem),
  necessária porque esta fatia não usa o DLQ nativo do broker para o esgotamento de tentativas ao
  provedor (ver **Decisões Técnicas**, item 1) — sem ela, RN-N12 não tem sinal correspondente. A
  `Activity` de consumo segue o padrão já usado em `HeartbeatConsumerWorker` (nome
  `notification.<agregado>.<evento>.consume`, `ActivityKind.Consumer`, propagação de `traceparent`).
- **Verificação dos contratos:** os seis cenários já listados em `contracts.md` §Validação e
  verificação são a lista de aceite desta implementação; não repetidos aqui.

---

## Questões em Aberto

- [ ] **QP-01 — número da janela de retenção do Registro de Entrega (RN-N16).** — encarregado de
  dados + negócio — impacto se não resolvido: o job de expurgo (V-07) nasce com um valor provisório
  (o PRD recomenda a validar, da ordem de 90 dias) até a decisão chegar.
- [ ] **QP-04 — quem recebe o alerta de "tratamento manual" (RN-N12).** — time/operação — impacto se
  não resolvido: nenhum, para esta TechSpec — a aplicação garante a **emissão** do sinal (V-05,
  **Verificação**); o roteamento para uma pessoa é responsabilidade de plataforma, já coberta pela
  divisão declarada no baseline (`context/architecture-baseline.md:412-413`), independente de quem
  ocupa o papel de operação hoje.
- [ ] **Sincronização entre o parâmetro de validade de Notificação e a validade real do Token de
  Verificação de Identidade e Acesso** (`contracts.md`, item 3 de "Origem e decisões") — dono:
  time/plataforma — impacto se não resolvido: se os dois valores divergirem, a mensagem promete um
  prazo que a Conta já não honra (ou honra por mais tempo do que o texto promete). É disciplina
  operacional de configuração, não algo que o código desta fatia resolva sozinho — registrar como
  checklist de deploy.

---

## Architecture Decision Records

Nenhuma ADR nova. As decisões desta TechSpec (mecanismo de retry ao provedor, modelagem de retenção em
duas camadas, classificação de erro do provedor) são locais a esta feature e aplicam, sem alterar,
`ADR-0001` (monorepo) e `ADR-0002` (Coolify) — nenhuma delas estabelece uma convenção nova que outro
serviço precise seguir. `docs/adr/index.md` consultado; nenhuma decisão `Accepted` conflita com o
desenho acima.
