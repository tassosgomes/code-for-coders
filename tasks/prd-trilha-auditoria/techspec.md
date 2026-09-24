---
tsg_artifact: techspec
product: code-4-coders
capability: CAP-030
version: 1.0
status: approved
updated: 2026-09-24
sources: tasks/prd-trilha-auditoria/prd.md@1.1, tasks/prd-trilha-auditoria/contracts.md@1.1
---

# Especificação Técnica — Trilha de auditoria (CAP-030, fatia mínima)

> **Escopo:** Backend
> **Modo:** Pipeline
> **PRD de origem:** `tasks/prd-trilha-auditoria/prd.md` (v1.1, aprovado 2026-09-24)
> **Contratos de integração:** `tasks/prd-trilha-auditoria/contracts.md` e
> `tasks/prd-trilha-auditoria/asyncapi-contract.yaml` (AsyncAPI 3.0.0, v1.0.1, aprovado para implementação)
> **Data:** 2026-09-24
> **Status:** Aprovado
> **Handoff:** approved — pode alimentar o Task Creator

Este documento cobre apenas o serviço `audit` (`src/audit`). O lado produtor — Identidade e Acesso
publicar os quatro atos em `auditoria.ato-praticado.v1` — pertence ao PRD de `CAP-002`, ainda não
escrito. Nesta fatia, os atos são publicados pelos testes, no formato do contrato.

---

## Resumo Executivo

O `audit` já existe desde a Fase 0 como consumidor técnico genérico: recebe `AuditEventV1`
(`audit.audit.event.v1`), grava o payload inteiro em `audit_access.audit_records` e protege a tabela
com trigger `BEFORE UPDATE OR DELETE` e guarda no `SaveChanges`. Esta fatia **substitui esse
consumidor genérico** pelo receptor de `auditoria.ato-praticado.v1`, que grava um Registro de
Auditoria com campos de negócio (origem, tipo, autor, alvo, complemento, motivo, os dois momentos,
conformidade e razões), idempotente por (`origem`, `fatoId`), e fecha a lacuna de credencial que o
scaffold deixou aberta.

**Decisões principais:**

1. **`audit_access.audit_records` é reconstruída com a forma do Registro de Auditoria.** A migration
   remove a estrutura genérica da Fase 0 (e as linhas de smoke que ela tenha) e cria a nova, com o
   mesmo nome e a mesma proteção. O sistema não está em produção; não há evidência real a preservar.
2. **Idempotência pela unicidade no banco** de (`origem`, `fatoId`), com impressão digital do
   conteúdo para detectar reentrega divergente.
3. **Alerta = sinal de observabilidade emitido pela aplicação** (métrica + log estruturado sem dado
   pessoal); o roteamento até uma pessoa é da plataforma (baseline §Observabilidade).
4. **A aplicação passa a conectar com uma credencial que só tem `SELECT` e `INSERT`.** A credencial
   dona do banco fica restrita ao passo de migration.

**Trade-off primário:** ganha-se imutabilidade demonstrável em duas camadas independentes
(privilégio + trigger) e uma trilha consultável por campo no próximo PRD, sem reinterpretar JSON.
Abre-se mão de guardar a mensagem original inteira: campos fora do contrato são descartados, e o
que chega mal formado dentro de um campo é registrado como não conforme em vez de preservado
byte a byte — é o preço de não copiar dado pessoal que o produtor mande por engano (RN-A08).

---

## Arquitetura da Solução

### Bloco Backend

- **Receptor** de `auditoria.ato-praticado.v1`, no mesmo padrão do consumidor atual
  (`AsyncDefaultBasicConsumer`, `autoAck: false`, `BasicQos`, propagação de `traceparent`), ligado
  ao exchange de entrada do `audit` (`audit.events`). Produtores publicam nesse exchange — mesma
  convenção que Identity já usa para Notificação (`NotificationExchange`, `tasks/prd-conta-aluno/techspec.md`
  §Destino das mensagens). A routing key sozinha não cruza exchanges.
- **Classificação em três desfechos**, feita pelo receptor antes de qualquer gravação:
  1. **Ilegível** — corpo não é JSON objeto, ou falta/é inválido `fatoId` (uuid), `origem` (padrão
     do contrato) ou `tenantId` (uuid). Não chega ao caso de uso.
  2. **Legível** — segue para o caso de uso, que decide **conforme** ou **não conforme**.
- **Caso de uso de registro** (Application): recebe o ato já mapeado, calcula as razões de não
  conformidade, calcula a impressão digital, grava o registro e confirma a transação. A regra de
  conformidade vive no Domain (entidade do Registro de Auditoria, na criação); a tabela de tipos
  aceitos e a exigência de motivo por tipo vivem numa política de domínio única, para que um tipo
  novo seja uma linha, não uma condição espalhada.
- **Porta de escrita** continua só com "acrescentar"; ganha "ler a impressão digital por
  (`origem`, `fatoId`)", necessária para classificar a reentrega. Nenhum método de alteração ou
  exclusão existe.
- **Sem outbox, sem publicação, sem HTTP de negócio** (RN-A10). O endpoint `/internal/audit/smoke`
  permanece como está.

### Transação, consistência e ack

- Uma mensagem = uma transação com um `INSERT`. O `ack` acontece **depois** do commit.
- Violação da unicidade (`origem`, `fatoId`) no commit é o sinal de reentrega — inclusive na corrida
  de duas entregas simultâneas da mesma mensagem. O caso de uso não faz "consulta antes de gravar"
  como fonte de verdade; lê a impressão digital existente **depois** da violação para decidir entre
  reentrega idêntica e divergente.
- Falha transitória (banco indisponível, timeout, erro inesperado) → `nack(requeue: true)`. A fila
  quorum conta as entregas; ao atingir `x-delivery-limit` a mensagem vai para a DLQ.
- Ilegível → `nack(requeue: false)` → DLQ imediatamente, intacta.

### Dado sensível: `motivo` e referências

| Ponto | Tratamento |
|---|---|
| Mensagem no broker e na DLQ | Chega como publicada; a DLQ retém o corpo intacto por decisão (RF-04). Acesso à DLQ é operacional |
| Registro de Auditoria | `motivo` gravado como recebido; autor e alvo só como (`tipo`, `id`); `complemento` só pares chave/valor de texto curto |
| Campos fora do contrato | Descartados no mapeamento; não são gravados nem logados |
| Log, span, métrica | Só `fatoId`, `origem`, `tipo`, `tenantId`, razões e desfecho. **Nunca** `motivo`, `complemento`, ids de autor/alvo em tag de métrica (cardinalidade e perfilamento) |
| `EnableSensitiveDataLogging` | Hoje ligado em Development (`DependencyInjection.cs:29`); ver Riscos |

---

## Mapa de Fatias Verticais

### V-01: Ato conforme vira Registro de Auditoria

- **Cobre:** RF-01 · RN-A02, RN-A04, RN-A05, RN-A08, RN-A13, RN-A14 · US-01, US-04
- **Entrada / gatilho:** mensagem publicada em `audit.events` com routing key
  `auditoria.ato-praticado.v1`, com os quatro tipos de Identidade completos (exemplos do contrato).
- **Processamento:**
  - Declaração da fila própria do ato (quorum, DLX, `x-delivery-limit`) ligada a
    `auditoria.ato-praticado.v1`; o binding `audit.audit.event.v1` e o consumidor de `AuditEventV1`
    deixam de existir.
  - `audit_access.audit_records` reconstruída por uma migration EF nova (a aplicada não é editada):
    a estrutura genérica é removida — tabela, trigger e função — e recriada com: identificador próprio,
    `tenant_id`, `origem`, `fato_id`, `tipo`, `autor_tipo`/`autor_id`, `alvo_tipo`/`alvo_id`,
    `complemento` (jsonb), `motivo`, `praticado_em`, `recebido_em`, conformidade, razões,
    impressão digital. Unicidade em (`origem`, `fato_id`); índice por (`tenant_id`, `praticado_em`)
    para o próximo PRD. Trigger `BEFORE UPDATE OR DELETE` recriado na mesma migration. `DROP TABLE`
    não é barrado pelo trigger de linha, então a migration não precisa desligá-lo antes.
  - Motivo exigido por tipo: `convite-interno-emitido`, `papel-concedido`, `papel-revogado`; não em
    `convite-interno-aceito` (tabela do PRD RF-01).
  - `recebido_em` é o relógio do `audit` no processamento; `praticado_em` vem da mensagem.
- **Saída observável:** uma linha conforme por ato, `ack`, contador `audit.acts.recorded` com
  atributos `origin`, `type`, `conformity=conforming`.
- **Evidência / checkpoint:** integração (Testcontainers PostgreSQL + RabbitMQ, como
  `AuditIntegrationFixture`): publicar os quatro exemplos → quatro registros conformes com os
  campos esperados, `praticado_em` preservado, `recebido_em` ≥ `praticado_em` do teste; o aceite
  sem motivo é conforme; dois tenants distintos, cada registro no seu; nenhuma coluna contém e-mail
  ou nome (a mensagem de teste traz um campo extra `email` fora do contrato, que não aparece em
  lugar nenhum). Captura de logs do teste sem `motivo`.
- **Bloqueado por:** Nenhum.

### V-02: Reentrega não duplica; reentrega divergente é alertada

- **Cobre:** RF-02 · RN-A01, RN-A03
- **Entrada / gatilho:** a mesma mensagem publicada duas vezes; duas mensagens com mesmo conteúdo e
  `fatoId` diferentes; mesma (`origem`, `fatoId`) com conteúdo diferente.
- **Processamento:** a impressão digital é SHA-256 da forma canônica dos campos do contrato que
  entram no registro (`tenantId`, `tipo`, `praticadoEm` normalizado para UTC, `autor`, `alvo`,
  `complemento` com chaves ordenadas, `motivo`). Campos descartados não entram, para que um produtor
  que acrescente campo novo não gere falsa divergência. Na violação de unicidade: impressão igual →
  `ack`, nada mais; diferente → `ack`, nenhum registro, sinal de divergência.
- **Saída observável:** contagem de registros inalterada; contador `audit.acts.redelivered` com
  `outcome=identical|divergent`; log de aviso para `divergent` com `origem`, `fatoId` e as duas
  impressões.
- **Evidência / checkpoint:** integração — publicar a mesma mensagem 2× → 1 registro, um
  `redelivered{identical}`; mesmo conteúdo com `fatoId` novo → 2 registros; conteúdo alterado com
  mesmo `fatoId` → registro original byte a byte igual, `redelivered{divergent}` = 1. Teste de
  corrida: duas entregas simultâneas da mesma mensagem (dois consumidores ou prefetch > 1) → 1
  registro, nenhuma mensagem na DLQ.
- **Bloqueado por:** V-01.

### V-03: Ato incompleto ou de tipo desconhecido vira registro não conforme, alertado

- **Cobre:** RF-03 · RN-A05, RN-A06 · DP-03, DP-04 · US-03
- **Entrada / gatilho:** mensagem legível com uma ou mais faltas.
- **Processamento:** razões, cada uma um código estável gravado no registro:

  | Código | Quando |
  |---|---|
  | `tipo-desconhecido` | `tipo` ausente ou fora da política de tipos aceitos |
  | `autor-ausente` | `autor` ausente, ou sem `tipo`/`id` válidos |
  | `alvo-ausente` | `alvo` ausente, ou sem `tipo`/`id` válidos |
  | `motivo-ausente` | tipo exige motivo e ele falta ou é vazio |
  | `momento-ausente` | `praticadoEm` ausente ou inválido — o registro guarda `praticado_em` nulo e a consulta usará `recebido_em` para ordená-lo |
  | `complemento-invalido` | `complemento` não é objeto de textos até 100 caracteres; o complemento não é gravado |
  | `motivo-excede-limite` | `motivo` acima de 1000 caracteres; gravado integralmente mesmo assim |

  O que foi recebido é preservado nos campos correspondentes; o que faltou fica nulo. Tipo
  desconhecido preserva o texto recebido em `tipo`.
- **Saída observável:** registro `non_conforming` com todas as razões; **um** incremento de
  `audit.acts.nonconforming` por ato (atributos `origin`, `type`, sem razões na métrica para não
  multiplicar a contagem) e um log de aviso com as razões. Reentrega do mesmo ato segue V-02 e não
  repete o sinal.
- **Evidência / checkpoint:** unitários da política de conformidade para cada código e combinação;
  integração — `papel-revogado` sem motivo, ato sem autor e sem alvo (duas razões, um sinal), tipo
  `papel-alterado` (desconhecido nesta versão), reentrega de não conforme (sem sinal novo).
- **Bloqueado por:** V-01.

### V-04: Mensagem ilegível e falha transitória vão para a DLQ sem perda

- **Cobre:** RF-04 · RN-A02, RN-A03 · DP-05
- **Entrada / gatilho:** corpo não JSON; JSON sem `fatoId`, sem `origem` ou sem `tenantId` válido;
  banco indisponível durante uma mensagem legível.
- **Processamento:** ilegível → `nack(requeue: false)`, contador `audit.messages.illegible` com
  `reason=body|fatoId|origem|tenantId`, log de erro sem o corpo. Falha transitória → `nack(requeue: true)`
  até `x-delivery-limit`, depois DLQ. A DLQ não é consumida automaticamente; reprocessar é mover a
  mensagem de volta para a fila (operação de plataforma), e V-02 garante que não duplica.
- **Saída observável:** nenhum registro para o ilegível; corpo na DLQ idêntico ao publicado; sinal
  "DLQ não vazia" do baseline disponível para a plataforma.
- **Evidência / checkpoint:** integração — para cada caso de ilegibilidade, ler da DLQ e comparar
  bytes com o publicado; parar o PostgreSQL do fixture com uma mensagem legível em voo → mensagem
  termina na DLQ após o limite; religar, mover de volta, → 1 registro; mover de novo → continua 1.
- **Bloqueado por:** V-01.

### V-05: Imutabilidade demonstrada com a credencial de execução

- **Cobre:** RF-05 · RN-A01, RN-A02, RN-A10 · G13 · BA03 · US-02
- **Entrada / gatilho:** o serviço conectado com a credencial de execução; tentativas diretas de
  `UPDATE`, `DELETE` e `TRUNCATE` nessa conexão.
- **Processamento:**
  - Credencial de execução nova, com `LOGIN`, membro de `code_for_coders_audit_writer`, que recebe
    só `USAGE` no schema e `SELECT, INSERT` na tabela do Registro (e sequência, se houver). Não é
    dona de nada. A credencial dona (`code_for_coders_audit`) fica exclusiva do passo de migration.
  - `ConnectionStrings:DefaultConnection` passa a ser a credencial de execução; a string da
    migration é separada (a que `AuditDbContextFactory` usa).
  - Grants/revokes: a migration que cria a tabela concede ao papel `writer` e revoga de `PUBLIC`,
    para que ambiente novo nasça correto; `scripts/init-local-databases.sql` cria o login local.
- **Saída observável:** `UPDATE`/`DELETE`/`TRUNCATE` com a credencial de execução falham por
  **privilégio**; com a credencial dona, `UPDATE`/`DELETE` falham pelo **trigger**. Reinício do host
  e reprocessamento não mudam contagem nem conteúdo.
- **Evidência / checkpoint:** integração — as seis tentativas acima com erro esperado e o registro
  idêntico depois; reiniciar o host e republicar todas as mensagens anteriores → mesma contagem.
  Estrutural: o `DbContext` recusa `Modified`/`Deleted` para o novo registro (o teste atual de
  `AuditAppendOnlyGuardrailTests` estendido). Isolamento de produtor: o banco `code_for_coders_audit`
  não concede `CONNECT` aos papéis dos outros serviços (verificado no init local e no teste).
- **Bloqueado por:** V-01.

---

## Contratos e Fronteiras

### Mapeamento de mensagens e dados

| Contrato e identificador | Aplicação/produtor e consumidores | Comportamento a implementar | Evidência |
|---|---|---|---|
| AsyncAPI `receberAtoPraticado` · canal `auditoria.ato-praticado.v1` · mensagem `AtoPraticado` | Produtores: domínios com ato (nesta fatia, testes no papel de Identidade); consumidor: `audit` (receive) | Três desfechos (conforme, não conforme, ilegível); idempotência por (`origem`, `fatoId`); ack após commit; DLQ para ilegível e esgotamento | V-01 a V-04; cenários 1–10 de `contracts.md` |

Diferença para o acordo anterior do serviço: `AuditEventV1` (`src/audit/src/CodeForCoders.Audit.Contracts/AuditEventV1.cs`,
routing key `audit.audit.event.v1`) não tem produtor em nenhum serviço (busca em `src/`) e é
removido; nenhum consumidor afetado.

### Entidades do domínio

| Entidade do Domain Doc | Representação técnica | Local |
|---|---|---|
| Ato Administrativo | Não persiste sozinho: é o conteúdo do Registro (origem, tipo, autor, alvo, complemento, motivo, `praticado_em`) | Domain do `audit` |
| Registro de Auditoria | Entidade append-only com conformidade e razões; tabela `audit_access.audit_records`, reconstruída | Domain + Infra.Data do `audit` |
| Referência de identidade (autor, alvo) | Par (`tipo`, `id`) — sem chave estrangeira, o dono é outro serviço | Idem |

### Interfaces entre fatias ou times

- **Com `CAP-002` (Identidade):** publicar em `audit.events`, routing key `auditoria.ato-praticado.v1`,
  pelo outbox, com o schema do contrato. O nome do exchange é configuração do produtor, como
  `NotificationExchange` em Identity.

---

## Arquivos a Modificar e a Referenciar

### A modificar

| Caminho | Fatia | Alteração |
|---|---|---|
| `src/audit/src/CodeForCoders.Audit.Infra.Messaging/AuditEventConsumerWorker.cs` | V-01, V-04 | Passa a receber `AtoPraticado`; classificação ilegível/legível; `nack` com e sem requeue por desfecho; captura de toda exceção de processamento (ver Riscos) |
| `src/audit/src/CodeForCoders.Audit.Infra.Messaging/AuditTopologyInitializer.cs` | V-01 | Fila e binding de `auditoria.ato-praticado.v1`; remove o binding legado |
| `src/audit/src/CodeForCoders.Audit.Infra.Messaging/Configuration/RabbitMqOptions.cs` e `src/audit/src/CodeForCoders.Audit.Api/appsettings.json` | V-01 | Nome da fila e routing key novos; `DeliveryLimit` mantido em 5 |
| `src/audit/src/CodeForCoders.Audit.Infra.Messaging/AuditReceiptStore.cs` | V-01 | Ajustar ao novo desfecho ou remover — hoje só serve ao teste de integração |
| `src/audit/src/CodeForCoders.Audit.Application/Interfaces/IAuditEventRecorder.cs`, `IAuditRecordWriter.cs` | V-01, V-02 | Recebem o ato mapeado; escrita ganha leitura da impressão digital |
| `src/audit/src/CodeForCoders.Audit.Application/UseCases/Audit/RecordConsumedAuditEvent/*` | V-01–V-03 | Substituído pelo caso de uso de registro de ato |
| `src/audit/src/CodeForCoders.Audit.Application/Common/AuditTelemetry.cs` | V-01–V-04 | Contadores `audit.acts.recorded`, `audit.acts.nonconforming`, `audit.acts.redelivered`, `audit.messages.illegible` |
| `src/audit/src/CodeForCoders.Audit.Domain/Entities/AuditRecord.cs` e `src/audit/src/CodeForCoders.Audit.Infra.Data/Configuration/AuditRecordConfiguration.cs` | V-01, V-03 | A entidade passa a ser o Registro de Auditoria (campos de negócio, conformidade e razões) e o mapeamento acompanha a tabela reconstruída |
| `src/audit/src/CodeForCoders.Audit.Infra.Data/AuditDbContext.cs` | V-01, V-05 | Novo conjunto; guarda de mutação cobre a nova entidade |
| `src/audit/src/CodeForCoders.Audit.Infra.Data/Configuration/AuditDatabasePermissions.sql` e `AuditAppendOnlyGuardrail.md` | V-05 | Grants mantidos sobre `audit_records` (mesmo nome); credencial de execução; descrever as duas camadas |
| `src/audit/src/CodeForCoders.Audit.Infra.Data/DependencyInjection.cs` | V-05 | Conexão de execução ≠ conexão de migration |
| `src/audit/src/CodeForCoders.Audit.Contracts/AuditEventV1.cs` | V-01 | Removido; o projeto de contratos passa a ter o DTO de `AtoPraticado` |
| `scripts/init-local-databases.sql` e `docker-compose.yml` (serviço `audit`) | V-05 | Login de execução local; `DefaultConnection` do compose aponta para ele |
| `src/audit/tests/CodeForCoders.Audit.IntegrationTests/*`, `src/audit/tests/CodeForCoders.Audit.UnitTests/*` | V-01–V-05 | Testes existentes migram para o novo contrato |

### A referenciar (não alterar)

| Caminho | Por que consultar |
|---|---|
| `src/audit/src/CodeForCoders.Audit.Infra.Data/Migrations/20260920232840_InitialAudit.cs:42-58` | Trigger a recriar; não editar a migration aplicada — a nova vem por `dotnet ef migrations add` |
| `src/notification/src/CodeForCoders.Notification.Infra.Messaging/NotificationSendRequestConsumerWorker.cs` | Padrão já aprovado de receptor com desfecho de negócio acked e poison message em DLQ |
| `src/identity/src/CodeForCoders.Identity.Application/Common/OutboxDestinationOptions.cs` | Convenção "produtor publica no exchange do consumidor" que `CAP-002` seguirá |
| `context/architecture-baseline.md:253-262`, `:405-413` | Mensageria (ack manual, DLQ, idempotência) e divisão emissão/roteamento de alerta |
| `.claude/skills/dotnet` | Estrutura, migrations, testes e observabilidade do serviço |

---

## Análise de Impacto

| Componente | Tipo | Impacto e risco | Ação requerida |
|---|---|---|---|
| `audit_access.audit_records` | Reconstruído | Estrutura e linhas da Fase 0 (só smoke; sistema fora de produção) são descartadas pela migration | Aplicar a migration nova nos bancos locais e de teste; nenhum ambiente com dado real |
| Routing key `audit.audit.event.v1` | Removido | Sem produtor em `src/` | Nenhuma |
| Credencial do `audit` em ambientes já provisionados | Modificado | Se o deploy não criar o login de execução, o serviço não conecta | Pré-requisito de implantação declarado em Verificação; plataforma provisiona (QT-01) |
| `docker-compose.yml`, `scripts/init-local-databases.sql` | Modificado | Recriar o volume local do PostgreSQL ou rodar o init de novo | Documentar no checkpoint da V-05 |
| Identidade e Acesso (`CAP-002`) | Futuro produtor | Passa a depender do exchange `audit.events` e do contrato 1.0.1 | Registrado em Interfaces |

---

## Riscos e Preocupações

| Preocupação | Local (`arquivo:linha`) | Impacto | Mitigação |
|---|---|---|---|
| O papel `writer` é configurado e validado, mas **nunca usado**: o serviço conecta como dono do banco, que tem `UPDATE`/`DELETE` e pode derrubar o trigger | `src/audit/src/CodeForCoders.Audit.Infra.Data/DependencyInjection.cs:34-37`; `docker-compose.yml:207`; `scripts/init-local-databases.sql:44-47` | G13 ("permissão de banco") não é cumprida hoje; a única barreira é o trigger | V-05: credencial de execução só com `SELECT, INSERT` |
| Exceção que não seja `JsonException`/`AlreadyClosedException` (banco fora, `EntityValidationException`) não gera `ack` nem `nack`: a mensagem fica presa no canal, ocupando prefetch, sem ir à DLQ | `src/audit/src/CodeForCoders.Audit.Infra.Messaging/AuditEventConsumerWorker.cs:72-98` | Ato "em voo" indefinidamente; `x-delivery-limit` nunca atua | V-04: toda exceção de processamento termina em `nack` com a política de requeue do desfecho |
| `EnableSensitiveDataLogging` em Development registra parâmetros SQL, inclusive `motivo` | `src/audit/src/CodeForCoders.Audit.Infra.Data/DependencyInjection.cs:29` | Motivo em log local, contra o critério de RF-01 | Desligar para o `audit`: o motivo é o dado que o serviço mais guarda |
| Caso de uso atual grava `recordedOn` com `DateTimeOffset.UtcNow` direto | `src/audit/src/CodeForCoders.Audit.Application/UseCases/Audit/RecordConsumedAuditEvent/RecordConsumedAuditEvent.cs:31` | `recebido_em` não é testável de forma determinística | Usar `TimeProvider` no caso de uso novo |
| `ProjectArchitecture`/`LayerDependencyTest` validam o projeto de contratos atual | `src/audit/tests/CodeForCoders.Audit.ArchitectureTests/` | Remover `AuditEventV1` pode quebrar teste de arquitetura | Ajustar na V-01, mantendo a regra "Domain não referencia Contracts" |

---

## Decisões Técnicas

1. **Reconstruir `audit_records` em vez de manter duas tabelas.**
   - **Racional:** a forma da Fase 0 (evento técnico com payload inteiro) não serve ao Registro de
     Auditoria, e o trigger impede corrigir linhas existentes. Como o sistema não está em produção,
     a estrutura antiga e as linhas de smoke são descartadas numa migration e a tabela nasce de novo,
     com o mesmo nome, grants e proteção.
   - **Trade-offs:** a migration derruba dado — aceitável só porque não há ambiente com dado real.
     Depois do primeiro deploy em produção, nenhuma migration deste serviço pode fazer isso.
   - **Alternativas rejeitadas:** tabela nova ao lado da antiga (deixa lixo); evoluir a tabela com
     colunas nulas (o registro de negócio nasceria sem garantia de preenchimento e com linhas de smoke
     misturadas).

2. **Idempotência por unicidade + impressão digital, sem inbox separado.**
   - **Racional:** o efeito é um único `INSERT` na mesma tabela; a restrição de unicidade é atômica e
     cobre a corrida. A impressão digital é o que torna observável a reentrega divergente (RF-02).
   - **Trade-offs:** a impressão depende de uma canonicalização estável; mudar os campos que entram
     nela é mudança de comportamento, a ser tratada como tal.
   - **Alternativas rejeitadas:** tabela de inbox — duplicaria o que a unicidade já garante.

3. **Alerta como sinal emitido, não como integração com ferramenta.**
   - **Racional:** baseline §Observabilidade: a aplicação emite em OTLP; coleta, dashboard e
     roteamento são da plataforma. "Mesmo dia" (métrica do PRD) depende da regra de alerta, não do
     código.
   - **Trade-offs:** até a plataforma configurar a regra, o sinal existe e ninguém é chamado.
   - **Alternativas rejeitadas:** enviar e-mail/mensagem à operação pelo `audit` — criaria produtor
     e integração externa num serviço que "só recebe".

4. **Campos fora do contrato são descartados; mal formados dentro do contrato viram razão.**
   - **Racional:** RN-A08 — o `audit` não pode virar depósito de dado pessoal enviado por engano.
   - **Trade-offs:** a evidência do que exatamente o produtor enviou errado fica só na DLQ quando é
     ilegível; no não conforme, fica a razão.

Nenhuma decisão aqui cria convenção para outras features além do que o baseline já estabelece
(G13, outbox no produtor, ack manual) — sem ADR nova.

---

## Verificação

- **Cenários críticos não óbvios:** corrida de duas entregas da mesma mensagem (V-02); banco
  derrubado com mensagem em voo e reprocessamento da DLQ (V-04); as tentativas de mutação com as
  duas credenciais (V-05); campo extra com e-mail que não pode aparecer em lugar nenhum (V-01).
- **Dados e ambiente:** `AuditIntegrationFixture` (PostgreSQL 18 + RabbitMQ 4.3 via Testcontainers)
  passa a aplicar as migrations com a credencial dona e a rodar o host com a credencial de execução,
  criada no fixture com os mesmos grants do script. Local (Compose): recriar o volume do PostgreSQL
  ou reexecutar `scripts/init-local-databases.sql` para ter o login de execução.
- **Observabilidade além do padrão:** os quatro contadores de V-01–V-04 e os logs de aviso de
  não conformidade e divergência são o "alerta à operação" do PRD. Nenhum carrega `motivo`,
  `complemento` ou ids de autor/alvo.
- **Verificação do contrato:** publicar nos testes exatamente os exemplos do `asyncapi-contract.yaml`
  (V-01) e as variações dos cenários 1–10 de `contracts.md`. A validação do YAML já feita não
  comprova a implementação.

---

## Questões em Aberto

- [ ] **QT-01 — Provisionar o login de execução do `audit` nos ambientes da plataforma.** — time de
      plataforma — sem ele, o deploy desta fatia não conecta; no local e nos testes, o repositório
      cobre.
- [ ] **QT-02 — Regra de alerta sobre `audit.acts.nonconforming`, `audit.acts.redelivered{divergent}`,
      `audit.messages.illegible` e DLQ não vazia.** — plataforma — sem ela, o critério "mesmo dia"
      do PRD depende de alguém olhar o painel.

---

## Architecture Decision Records

Nenhuma ADR nova. Esta fatia aplica decisões existentes:

- [ADR-0001: Monorepo de código](../../docs/adr/0001-monorepo-de-codigo.md) — o `audit` é unidade
  de deploy própria no monorepo.
- [ADR-0002: Plataforma de runtime Coolify](../../docs/adr/0002-plataforma-de-runtime-coolify.md) —
  segredos e credenciais de banco são provisionados pela plataforma (QT-01). **Conformidade
  explícita:** a ADR fixa "um banco e uma credencial por serviço" e "migration como step de deploy".
  A V-05 separa a credencial do `audit` em duas — dona, usada só pelo step de migration, e de
  execução, só `SELECT, INSERT`. Ambas pertencem exclusivamente ao `audit`; o isolamento entre
  serviços, que é o objetivo da ADR, é preservado, e a separação decorre de G13 ("permissão de
  banco sem `UPDATE`/`DELETE`"), que com uma credencial só seria incumprível. Não substitui a ADR.
