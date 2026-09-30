---
tsg_artifact: techspec
product: code-4-coders
capability: CAP-030
version: 1.2
status: approved
updated: 2026-09-30
sources: tasks/prd-consulta-trilha-auditoria/prd.md@1.0, tasks/prd-consulta-trilha-auditoria/contracts.md@1.2, context/architecture-baseline.md@1.2
---

# Especificação Técnica — Consulta e complemento da trilha de auditoria

> **Escopo:** Full-stack  
> **Modo:** Pipeline  
> **PRD de origem:** [prd.md](prd.md) v1.0, aprovado  
> **Contratos de integração:** [contracts.md](contracts.md) v1.2; [API pública](api-contract.yaml),
> [API interna de Auditoria](internal-api-contract-audit.yaml),
> [API interna de Identidade](internal-api-contract-identity.yaml) e
> [mensagem de complemento](asyncapi-contract.yaml), aprovados para implementação  
> **Data:** 2026-09-27 (revisão 1.2 em 2026-09-30)  
> **Status:** Aprovado  
> **Handoff:** approved — pode alimentar o Task Creator

## Resumo Executivo

O `admin-spa` ganha uma área de Auditoria exclusiva de administrador. O `bff-admin` revalida a
sessão em Identity em **cada** ação, obtém JWT de audiência `audit` e compõe a lista e o detalhe
de `audit` com rótulos obtidos de Identity no momento da leitura. `audit` valida JWT, papel e tenant antes de
consultar seus registros imutáveis. Uma explicação confirmada no SPA é aceita pelo BFF apenas após
persistir confirmação idempotente e evento no mesmo commit; o consumidor de `audit` cria o
complemento como novo registro, sem mutar o original.

A paginação fixa os IDs elegíveis em snapshot temporário de Valkey no `audit`, conforme a
[ADR-0007 aceita](../../docs/adr/0007-snapshots-efemeros-da-consulta-de-auditoria.md). O ganho é
sequência estável diante de atos retroativos; o custo é memória proporcional à seleção e
reinício da busca após expiração. O texto livre da confirmação exige proteção no outbox existente
do BFF antes de qualquer publicação.

O responsável autorizou revisar as duas buscas HTTP para POST com filtros no corpo, preservando
os `operationId`s e a paginação; os contratos 1.1.0 passaram na validação. A ADR-0007 e esta
TechSpec foram aprovadas pelo responsável em 2026-09-27.

**Revisão 1.1 (2026-09-27):** o responsável aprovou a decisão 2 do
[wireframe de Auditoria](../../docs/design/wireframes-auditoria.md): a lista também mostra os
nomes de autor e alvo. O BFF resolve os rótulos da página, e não só do detalhe; o lookup em
Identity passa a nascer em V-01. O schema `AuditRecordSummary` já aceita `label` opcional, sem
mudança de contrato.

**Revisão 1.2 (2026-09-30):** errata para acompanhar [contracts.md](contracts.md) v1.2, aprovado
em 2026-09-28 e já implementado. O resumo da lista (`AuditRecordSummary`, APIs pública e interna
1.2.0) ganha `role` opcional e nullable, para a lista mostrar o papel junto ao tipo do ato, como no
Figma aprovado. Sem decisão nova: `audit` preenche `role` somente em `papel-concedido` e
`papel-revogado`, lendo o atributo `papel` já armazenado no complemento do original; nos demais
tipos, ou com complemento ausente ou ilegível, devolve `null`. O BFF repassa o valor sem
enriquecer. Nada muda em persistência, mensagem de auditoria ou no detalhe.

## Arquitetura da Solução

```text
admin-spa /admin/auditoria ──▶ bff-admin ──JWT audit──▶ audit ──▶ PostgreSQL de evidências
                                │                        └──▶ Valkey: IDs do snapshot (TTL)
                                ├──asserção + sessão────▶ identity: rótulos por referência
                                └──commit local─────────▶ outbox protegido ──▶ audit.events
                                                                    complemento-confirmado ──▶ audit
```

### Backend

- **Autorização:** o middleware do BFF verifica vigência da sessão em Identity em cada GET/POST;
  os endpoints exigem claim de papel `administrador`. Para chamar `audit`, o BFF pede à Identity
  JWT curto de audiência `audit`, sem guardar o token na sessão ou entregá-lo ao navegador.
  `audit` verifica assinatura via JWKS, issuer, audience, expiração, escopo
  `audit-records:read`, tenant e papel localmente;
  usa somente o tenant do JWT em todas as consultas. A revogação barra a próxima ação na borda
  (ADR-0005); o consumidor do evento não reconsulta papel, pois processa um fato já confirmado.
- **Lista e detalhe:** `audit` consulta apenas registros originais do tenant. A primeira lista
  chega por `POST /api/v1/audit-record-searches` ao BFF e por
  `POST /internal/v1/audit-record-searches` ao serviço, com filtros e paginação no corpo;
  o POST público exige CSRF vinculado à sessão e não cria evidência. A primeira lista
  seleciona os IDs elegíveis numa única visão consistente, ordenados por
  `coalesce(praticado_em, recebido_em)` e ID, ambos decrescentes; guarda IDs e metadados no Valkey
  por 30 minutos absolutos, vinculados a tenant, sessão, filtros e `_size`. O período filtra
  `praticado_em` com bordas inclusivas; `null` não satisfaz período informado. Páginas seguintes
  leem a mesma seleção. `hasComplements` é calculado da relação atual, sem alterar a seleção de
  IDs. Snapshot ausente, expirado ou incompatível recebe `422 AUDIT_FILTER_INVALID`; falha do
  cache não devolve página incompleta. O detalhe lê original e complementos do mesmo tenant,
  ordenados por `confirmedAt` e ID; nenhum complemento é aceito como ID de original.
- **Rótulos:** o BFF envia somente referências distintas (`conta-interna` e `convite-interno`)
  da página da lista ou do detalhe à operação em lote de Identity, em grupos de até 50. Rótulo retornado entra apenas
  na resposta e no cache de consulta do navegador. Referência sem resolução conserva tipo/ID e
  não recebe nome deduzido. Falha transitória de lookup deixa os rótulos ausentes e sinaliza
  indisponibilidade no detalhe (na lista, a célula mostra a referência curta); `401/403` de
  Identity fecha a resposta, pois pode representar
  sessão/papel revogado entre as duas chamadas. Identity lê inclusive conta desativada, sempre
  filtrada por tenant. Não se grava rótulo em `audit` ou no BFF.
- **Confirmação:** após sessão/CSRF, papel e lookup do original no próprio tenant, o BFF valida
  explicação (1–1000 caracteres, com conteúdo não branco), compara exatamente o texto recebido
  em retries e grava, em **uma transação do seu banco**, a chave de
  idempotência com hash autenticado de (`recordId`, corpo), `confirmationId`, ator, tenant e prazo
  de 24 horas, junto do evento no outbox. Unicidade por (`tenantId`, `actorId`, operação,
  `Idempotency-Key`) resolve concorrência. Repetição igual devolve o mesmo `202`/ID; corpo ou
  original divergente dá `422 IDEMPOTENCY_CONFLICT`. Falha antes do commit não aceita a ação;
  falha após commit preserva a confirmação para retry. `202` significa aceite durável, não
  consumo. O `confirmationId` no evento é o da resposta.
- **Publicação e consumo:** o worker do BFF envia o evento ao exchange `audit.events` com
  confirmação do broker e repetição segura; a mensagem fica protegida no outbox até o momento
  de publicação. Uma transação curta reserva a linha com lease; o worker publica fora da
  transação e outra transação marca o resultado. Lease expirado após queda pode republicar, o
  que o consumidor deduplica. Não há chamada de rede na transação do banco. `audit` usa fila própria e binding
  da nova routing key, sem alterar `auditoria.ato-praticado.v1`. Mensagem legível é validada
  contra original existente e do mesmo tenant, nunca outro complemento. Inserção do complemento
  e unicidade (`tenantId`, `confirmationId`) ocorrem no mesmo commit; redelivery idêntico faz ack
  sem nova linha. Conteúdo divergente com a mesma chave e referência/original inválido vão à DLQ
  com alerta, sem mutação. Falha transitória impede ack e permite reentrega dentro do limite;
  ack só depois do commit. No RabbitMQ 4.3, `basic.reject` marca falha e conta para
  `x-delivery-limit`, enquanto `basic.nack` não conta; a escolha deve ser comprovada com broker
  real, conforme a [documentação oficial](https://www.rabbitmq.com/docs/nack). Complementos
  chegam fora de ordem e são ordenados na leitura.
  O BFF recebe permissão de publicação apenas no exchange de Auditoria; a topologia da fila
  continua sob responsabilidade de `audit`.

### Frontend

- A navegação mostra **Auditoria** somente quando a sessão vigente traz papel `administrador`;
  a rota de lista e a de detalhe recusam acesso direto dos demais papéis e sempre deixam o BFF
  decidir sobre a API. A origem local é `http://localhost:8081`, base `/admin/`, rotas públicas
  `http://localhost:8081/admin/auditoria` e
  `http://localhost:8081/admin/auditoria/{recordId}`. Na implantação de laboratório documentada,
  a origem é `https://c4c-admin.lab.tasso.dev.br`, com os mesmos caminhos. `recordId` é UUID
  opaco; motivo e explicação nunca aparecem na rota.
- Filtros e página são estado da jornada; dados da lista/detalhe são estado do servidor. Ao mudar
  qualquer filtro ou `_size`, a UI descarta o snapshot e volta à página 1. Ao expirar, apresenta
  aviso e refaz a busca, sem misturar resultados de snapshots diferentes. O detalhe distingue
  original, momento do ato, momento do recebimento, faltas e complementos; exibe campos ausentes
  e referências não resolvidas. Do detalhe, o administrador pode iniciar lista filtrada por
  autor/alvo por estado de navegação em memória; o UUID não é colocado na URL. Recarregar uma
  lista aberta por esse link reinicia sem o filtro em memória e avisa que a busca foi reiniciada.
- A confirmação mantém um `Idempotency-Key` por tentativa do usuário e o reutiliza em retry da
  mesma ação. Depois de `202`, mostra estado "aguardando registro" e consulta o detalhe até
  encontrar o `confirmationId` (intervalo de 2 s por até 30 s); então mostra o complemento.
  Se ainda não aparecer, mantém aviso de pendência e ação de atualizar; não declara falha da
  confirmação nem envia outra chave automaticamente. Sessão encerrada elimina a consulta e o
  cache de rótulos/detalhes do navegador. Controles e estados têm rótulos operáveis por teclado.

## Mapa de Fatias Verticais

### V-01: Administrador entra na lista e percorre resultados estáveis

- **Cobre:** RF-01, RF-02, RF-05; US-01, US-04; RN-A04, RN-A06, RN-A08, RN-A09, RN-A10,
  RN-A12 a RN-A14; Identidade RN-13, RN-14, RN-16 a RN-20, RN-23 a RN-25.
- **Entrada / gatilho:** abrir `/admin/auditoria`, aplicar período/tipo/autor/alvo/conformidade ou
  avançar página; acesso direto por ator sem papel.
- **Processamento:** BFF revalida sessão, exige administrador e encaminha JWT de audiência `audit`;
  `audit` valida novamente e fixa os IDs da primeira busca. Filtros combinam por interseção;
  período é inclusivo; original não conforme com referência presente continua filtrável.
  Valkey mantém somente IDs/metadata temporários. A lista exclui complementos, indica se há
  algum e apresenta estado vazio sem perder filtros. O BFF resolve em Identity os rótulos das
  referências da página (revisão 1.1), sem gravá-los; falha transitória mostra a referência
  curta e `401/403` de Identity fecha a resposta. Concessões e revogações trazem o `role` do
  original no resumo (revisão 1.2); nos demais tipos o campo é `null` e a lista não mostra papel.
- **Saída observável:** navegação restrita, página ordenada com total/snapshot estáveis, ou
  `401/403/422` neutro. Ator de outro tenant jamais vê ou infere registro.
- **Evidência / checkpoint:** com dois tenants, professor e administrador, abrir origem e rota
  direta; revogar o papel com área aberta e tentar página seguinte. Inserir ato retroativo entre
  páginas e percorrer todos os IDs sem repetição/perda. Testar duas bordas do período, `null`,
  filtros combinados, vazio e expiração do snapshot. Verificar a URL pública no navegador.
- **Bloqueado por:** Nenhum.

### V-02: Administrador examina o fato recebido e suas referências

- **Cobre:** RF-01, RF-03, RF-05; US-02, US-04; RN-A01, RN-A04 a RN-A06, RN-A08 a RN-A11,
  RN-A13; Identidade RN-19, RN-20, RN-23, RN-24.
- **Entrada / gatilho:** abrir `/admin/auditoria/{recordId}` a partir da lista ou por link direto.
- **Processamento:** `audit` distingue original de complemento, preserva conteúdo recebido e
  ausências, lista razões e complementos. BFF resolve referências em Identity sem persistir
  rótulos. Indisponibilidade transitória da resolução mantém IDs; negativa de autorização
  fecha acesso. O convite aceito sem motivo é conforme, conforme a política existente.
- **Saída observável:** detalhe com dois momentos, tipo/origem/atributos/motivo, faltas e
  sequência separada; `404` indistinto para inexistente, outro tenant e complemento usado como
  original. Link de retorno abre a lista na origem/base path declarados.
- **Evidência / checkpoint:** caso de concessão e ato não conforme sem autor/motivo; conta
  desativada, convite inexistente, Identity temporariamente indisponível, outro tenant e ID de
  complemento. Verificar que o link abre o detalhe correto e o retorno abre a lista.
- **Bloqueado por:** V-01.

### V-03: Administrador confirma uma explicação e a vê como complemento imutável

- **Cobre:** RF-01, RF-04, RF-05; US-03, US-04; RN-A01 a RN-A03, RN-A07 a RN-A10, RN-A13.
- **Entrada / gatilho:** confirmar explicação no detalhe com CSRF e `Idempotency-Key`.
- **Processamento:** BFF verifica original/tenant e persiste confirmação + outbox protegido no
  mesmo commit; publicador entrega o fato; `audit` consome, verifica original e grava nova linha
  imutável com autor e momento próprios. UI concilia pelo `confirmationId` após `202`.
- **Saída observável:** complemento aparece separado do original; original e conformidade ficam
  byte a byte iguais. Explicação vazia, outro tenant, ator sem papel, CSRF inválido e chave
  divergente não criam complemento. Resposta perdida e redelivery igual produzem um só.
- **Evidência / checkpoint:** suspender broker após o commit e observar `202` + outbox pendente;
  restaurar broker e ver o complemento; repetir HTTP e mensagem; testar divergência, DLQ,
  original inexistente/de outro tenant, dois complementos fora de ordem e original sem
  complemento. Confirmar que texto não aparece em URL, logs, spans, métricas ou Problem.
- **Bloqueado por:** V-02.

Não há habilitador horizontal: configuração de JWT/Valkey entra em V-01; proteção do outbox,
mensageria e migrations geradas por EF entram em V-03.

## Contratos e Fronteiras

### Mapeamento HTTP

| Contrato e `operationId` | Caminho de aplicação | Regra além do schema / evidência |
|---|---|---|
| API pública `listAuditRecords` | SPA → POST BFF → `listAuditRecordsInternal` em `audit` → `resolveAuditIdentityReferencesInternal` em Identity | Sessão vigente, CSRF, administrador, tenant, filtro inclusivo e snapshot; rótulos da página como no detalhe; V-01 |
| API pública `getAuditRecord` | SPA → BFF → `getAuditRecordInternal` em `audit` → `resolveAuditIdentityReferencesInternal` em Identity | Lookup apenas de referências do tenant; falha transitória sem rótulo, revogação fecha; V-02 |
| API pública `confirmAuditRecordComplement` | SPA → BFF → banco próprio/outbox → RabbitMQ | `202` só após commit; idempotência 24 h e CSRF; V-03 |
| API interna `listAuditRecordsInternal`, `getAuditRecordInternal` | BFF com JWT `audit` → serviço dono | Busca POST com filtros no corpo; validação local de papel/tenant; sem dados pessoais de Identity; `role` do resumo derivado só do atributo `papel` de concessão/revogação (1.2.0); V-01/V-02 |
| API interna `resolveAuditIdentityReferencesInternal` | BFF com asserção `audit-references:read` e `X-Staff-Session` → Identity | Somente administrador e referências do tenant; ausência/outro tenant indistintos; V-01/V-02 |

Os schemas, parâmetros e códigos HTTP pertencem aos YAMLs. As revisões 1.1.0 das buscas
substituem os GETs 1.0.0 antes de qualquer implementação ou consumidor implantado. As revisões
1.2.0 acrescentam apenas `role` opcional e nullable ao resumo; a mudança é aditiva.

### Mensagem e dados

| Contrato e identificador | Participantes | Comportamento / evidência |
|---|---|---|
| AsyncAPI `receberComplementoConfirmado`, `auditoria.registro.complemento-confirmado.v1`, `ComplementoConfirmado` | `bff-admin` envia por outbox; `audit` recebe | `confirmationId` estável, deduplicação, alerta/DLQ para divergência e original inválido, ack após commit; V-03 |

O evento novo é aditivo ao `auditoria.ato-praticado.v1` do [primeiro PRD](../prd-trilha-auditoria/asyncapi-contract.yaml).
O `bff-admin` publica em `audit.events`, não no seu exchange `bff-admin.events`; a fila do
consumidor novo deve estar implantada antes de habilitar o produtor. Não há ODCS: o banco de
evidências não é produto de dados compartilhado.

**Percurso sensível:** a explicação nasce no formulário, atravessa SPA → BFF no corpo HTTPS e
fica em memória durante validação. O BFF grava somente hash autenticado do corpo na linha de
idempotência; o evento inteiro fica cifrado no seu outbox, com chave em secret manager e versão
de chave para rotação. O publicador decifra em memória imediatamente antes do envio; RabbitMQ
transporta o texto no corpo da fila de Auditoria e eventualmente da DLQ, com acesso restrito,
TLS e retenção operacional definida pela plataforma. `audit` grava a explicação no complemento
imutável em PostgreSQL; a mesma proteção de acesso e backups do banco de evidências se aplica.
O texto não entra em cache, headers, URLs, logs, spans, métricas ou erros. O outbox processado
e a linha de idempotência são descartados pela retenção operacional do BFF, respeitada a janela
de 24 h; o registro da Auditoria não é apagado nesta entrega. Rótulos de Identity passam por
memória do BFF e resposta ao SPA, sem persistência em Auditoria; o cache de consulta da sessão
é limpo no logout/revogação. O snapshot Valkey contém somente IDs de registros, tenant, sessão,
filtros normalizados e tamanho, sem motivo, rótulo ou explicação, e expira em 30 minutos.

### Jornada

| História | Tela e ação | Operação | Evidência |
|---|---|---|---|
| US-01 | Auditoria: filtros, paginação, vazio | `listAuditRecords` | V-01 |
| US-02 | Detalhe: evidência, faltas, referências e sequência | `getAuditRecord` | V-02 |
| US-03 | Detalhe: confirmar e aguardar complemento | `confirmAuditRecordComplement`, depois `getAuditRecord` | V-03 |
| US-04 | Navegação, rota direta e API | `listAuditRecords`, `getAuditRecord`, `confirmAuditRecordComplement` | V-01 a V-03 |

### Entidades do domínio

| Entidade | Representação técnica | Fronteira |
|---|---|---|
| Ato Administrativo | Conteúdo recebido do produtor; original já está no Registro | `audit`, leitura apenas nesta entrega |
| Registro de Auditoria | Original e novo tipo de registro complementar na tabela imutável; ligação por ID do original e tenant | `audit`, escrita de complemento só no consumidor |
| Conta / Convite | Fonte dos rótulos atuais; sem cópia na Auditoria | `identity` |

## Arquivos a Modificar e a Referenciar

### A modificar

| Caminho existente | Fatia | Alteração |
|---|---|---|
| `src/admin-spa/src/config/paths.ts`, `src/admin-spa/src/app/router.tsx`, `src/admin-spa/src/features/staff-session/utils/get-staff-areas.ts` | V-01/V-02 | Rotas públicas sob `/admin/`, navegação por papel e acesso direto |
| `src/admin-spa/src/app/routes/admin-layout-route.tsx`, `src/admin-spa/src/components/app-shell.tsx` | V-01 | Oferecer área somente ao administrador sem substituir o controle da API |
| `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Extensions/EndpointExtensions.cs` | V-01–V-03 | Registrar operações da borda |
| `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Extensions/ServiceConfigurationExtensions.cs`, `StaffIdentityExtensions.cs`, `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Security/ServiceAssertionTokenFactory.cs` | V-01–V-03 | Cliente `audit`, JWT de audiência `audit`, escopo de lookup de Identity |
| `src/bff-admin/src/CodeForCoders.BffAdmin.Api/appsettings.json`, `docker-compose.yml`, `docker-compose.coolify.yml` | V-01–V-03 | Endereços/credenciais de `audit`, Valkey de `audit`, exchange correto e chave de proteção do outbox |
| `src/bff-admin/src/CodeForCoders.BffAdmin.Infra.Data/BffAdminDbContext.cs`, `src/bff-admin/src/CodeForCoders.BffAdmin.Infra.Data/Outbox/OutboxMessageWriter.cs`, `OutboxMessage.cs`, `OutboxMessageConfiguration.cs` | V-03 | Confirmação idempotente transacional, evento protegido e destino por mensagem; migration nova via EF |
| `src/bff-admin/src/CodeForCoders.BffAdmin.Infra.Messaging/RabbitMqPublisher.cs`, `OutboxPublisherWorker.cs`, `Configuration/RabbitMqOptions.cs` | V-03 | Publicação em `audit.events`, decifragem antes do envio e nenhuma rede em transação |
| `src/identity/src/CodeForCoders.Identity.Api/Extensions/EndpointExtensions.cs`, `src/identity/src/CodeForCoders.Identity.Api/Security/ServiceAssertionVerifier.cs` | V-01/V-02 | Operação de lookup e escopo do BFF; manter isolamento de emissores |
| `src/identity/src/CodeForCoders.Identity.Api/appsettings.json`, `docker-compose.yml`, `docker-compose.coolify.yml` | V-01/V-02 | Permitir `audit-references:read` somente ao `bff-admin`, emissão de audiência `audit` com escopo de leitura |
| `src/audit/src/CodeForCoders.Audit.Api/Extensions/EndpointExtensions.cs`, `ServiceConfigurationExtensions.cs`, `src/audit/src/CodeForCoders.Audit.Api/appsettings.json` | V-01–V-03 | Leitura autenticada, configuração JWKS/Valkey e endpoint de consulta |
| `src/audit/src/CodeForCoders.Audit.Domain/Entities/AuditRecord.cs`, `src/audit/src/CodeForCoders.Audit.Infra.Data/Configuration/AuditRecordConfiguration.cs`, `AuditDbContext.cs` | V-02/V-03 | Complemento vinculado e índices/FK/unicidade do tenant; migration nova via EF |
| `src/audit/src/CodeForCoders.Audit.Infra.Messaging/AuditTopologyInitializer.cs`, `AuditEventConsumerWorker.cs`, `Configuration/RabbitMqOptions.cs` | V-03 | Fila e consumo separados do novo fato, sem tocar no ato existente |
| `src/admin-spa/src/testing/handlers.ts` | V-01–V-03 | Cenários de sessão, lista, detalhe e confirmação para a UI |

### A referenciar (não alterar)

| Caminho | Motivo |
|---|---|
| `tasks/prd-trilha-auditoria/techspec.md`, `tasks/prd-trilha-auditoria/asyncapi-contract.yaml` | Ingestão e imutabilidade já entregues; canal antigo permanece |
| `tasks/prd-acesso-interno/techspec.md`, `docs/adr/0005-sessao-e-servico-do-backoffice.md` | Vigência de sessão, asserção, JWKS e fronteira de autorização |
| `.agents/skills/dotnet/SKILL.md`, `.agents/skills/react/SKILL.md` | Convenções de implementação e gates da stack |
| `src/identity/src/CodeForCoders.Identity.Infra.Data/Outbox/OutboxPayloadProtector.cs` | Padrão já presente de proteção de payload com texto livre |
| `src/audit/src/CodeForCoders.Audit.Infra.Data/Configuration/AuditDatabasePermissions.sql` | Runtime de Auditoria recebe apenas `SELECT, INSERT` sobre evidências |
| `src/admin-spa/vite.config.ts`, `src/admin-spa/nginx.conf.template`, `README.md` | Base path `/admin/`, proxy `/api/v1/` e origens documentadas |

## Análise de Impacto

| Componente | Impacto e risco | Ação |
|---|---|---|
| `audit` / PostgreSQL | Leitura por filtro e complemento no mesmo acervo imutável; seleção ampla pode pressionar banco | Índices por tenant/filtros/ordem aferidos com volume representativo; migrations EF, sem editar as aplicadas |
| Valkey compartilhado | Novo consumidor temporário de memória, sem estado de negócio | Credencial/namespace de `audit`, TTL absoluto, métricas de tamanho e falha explícita |
| `bff-admin` / PostgreSQL / RabbitMQ | Outbox atual publica apenas no exchange próprio e guarda payload em claro | Destino por mensagem, proteção, retenção, permissão de publicar em `audit.events` e publicação fora de transação |
| Identity | Novo escopo e leitura em lote sob sessão de administrador; possível indisponibilidade | Configuração restrita e degradação só para falha transitória após autorização |
| `admin-spa` | Nova área com rótulos e explicações sensíveis | Limpar cache da sessão e excluir texto livre de URL/telemetria |
| Contratos anteriores | Nenhuma operação existente muda de semântica | Implantar consumidor antes do produtor; conservar `auditoria.ato-praticado.v1` |

## Riscos e Preocupações

| Preocupação | Local (`arquivo:linha`) | Impacto | Mitigação |
|---|---|---|---|
| O outbox do BFF grava JSON em claro | `src/bff-admin/src/CodeForCoders.BffAdmin.Infra.Data/Outbox/OutboxMessageWriter.cs:12` | Explicação fica legível no banco e backup do BFF | V-03 protege payload antes de persistir e controla chave/rotação; teste inspeciona coluna |
| O outbox persiste `exception.Message` em `last_error` | `src/bff-admin/src/CodeForCoders.BffAdmin.Infra.Data/Outbox/OutboxMessage.cs:35` | Uma exceção com texto do payload deixaria explicação em claro fora da evidência | V-03 registra somente código técnico de falha, sem payload, e testa coluna e logs |
| O publicador usa um exchange fixo próprio | `src/bff-admin/src/CodeForCoders.BffAdmin.Infra.Messaging/RabbitMqPublisher.cs:30` | Evento aceito não chega à fila de Auditoria | V-03 escolhe exchange `audit.events` por mensagem e comprova entrega ponta a ponta |
| BFF declara somente a topologia do seu exchange | `src/bff-admin/src/CodeForCoders.BffAdmin.Infra.Messaging/RabbitMqTopologyInitializer.cs:14` | Publicar antes do binding de `audit` deixa o fato sem consumidor | Implantar fila/binding do consumidor primeiro; BFF recebe ACL de publicação no exchange de Auditoria |
| Worker publica no broker dentro da transação do banco | `src/bff-admin/src/CodeForCoders.BffAdmin.Infra.Messaging/OutboxPublisherWorker.cs:65` | Viola baseline, prolonga lock e amplia falha parcial | V-03 separa claim/publicação/ack, mantém redelivery seguro |
| `audit` ainda não expõe leitura de negócio nem autenticação JWT | `src/audit/src/CodeForCoders.Audit.Api/Extensions/EndpointExtensions.cs:7` | Acesso direto poderia escapar da regra de administrador | V-01 adiciona validação local antes de toda busca e cenário de tenant/papel |
| Cache atual pertence ao BFF, não a `audit` | `src/audit/src/CodeForCoders.Audit.Api/Extensions/ServiceConfigurationExtensions.cs:9` | Falta infraestrutura para snapshot estável | V-01 configura Valkey isolado; ADR-0007 define TTL, vínculo e falha |
| Navegação atual considera só permissões e não oferece Auditoria | `src/admin-spa/src/features/staff-session/utils/get-staff-areas.ts:4` | Papel administrador pode não ver a área, ou uma permissão genérica a expõe | V-01 usa papel vigente e testa menu/rota direta |
| O middleware BFF trata POST como método inseguro | `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Security/BffSecurityMiddleware.cs:92` | A busca POST falha sem CSRF, embora seja só leitura | O contrato revisado exige `X-CSRF-Token`; V-01 usa a prova da sessão e testa ausência/origem inválida |
| Identity não emite hoje JWT para `audit` | `docker-compose.yml:125` | Chamada BFF→audit falha com `AUDIENCE_NOT_ALLOWED` | V-01 configura `AudienceScopes__audit` com `audit-records:read` em local e Coolify e testa emissão/validação |
| Comentário do consumidor atual contradiz o RabbitMQ 4.3 sobre `basic.reject` | `src/audit/src/CodeForCoders.Audit.Infra.Messaging/AuditEventConsumerWorker.cs:135` | Retentativa pode ser escolhida com premissa falsa e nunca alcançar DLQ | V-03 verifica `x-delivery-count`/limite em broker 4.3 e corrige o comentário ao ajustar o worker |

## Decisões Técnicas

1. **Snapshot de IDs em Valkey no serviço `audit`**, conforme ADR-0007. Alternativas e
   consequências permanentes estão no registro; nenhuma página é montada de uma seleção
   diferente após o primeiro pedido.
2. **Reusar o banco e outbox já existentes do BFF, com proteção de texto livre e chave de
   idempotência transacional.** Evita aceitar uma confirmação só em memória e evita criar outro
   produtor que precise consultar a trilha para decidir. Custa migração e tratamento de chave
   cifrada; o hash da comparação não guarda explicação em claro.
3. **Falha transitória de rótulos degrada o detalhe; falha de autorização fecha a resposta.**
   O registro continua investigável por referência, mas revogação não vira bypass por fallback.

## Verificação

- **Cenários críticos:** duas sessões e tenants; papel revogado entre abertura e próxima ação;
  filtros combinados/inclusivos; ID original não conforme; inserção retroativa entre páginas;
  snapshot expirado/perdido; Identity sem rótulo ou indisponível; dupla confirmação simultânea;
  mesmo ID com payload divergente; DLQ; falha do broker antes/depois da publicação; original
  idêntico antes/depois; ordenação de complementos fora de ordem.
- **Ambiente reproduzível:** `scripts/generate-local-env.sh`, `docker-compose.yml` com PostgreSQL,
  RabbitMQ e Valkey; migrations EF já entregues para CAP-002/primeiro PRD de CAP-030, depois as
  novas migrations desta entrega aplicadas pelo passo de deploy. Provisionar tenant e primeiro
  administrador pelo comando de Identity documentado, outro ator sem papel de administrador,
  dois tenants isolados, quatro tipos de ato e um não conforme. Usar nomes de fila/namespace de
  Valkey distintos por execução de teste e limpar só recursos da própria execução. Configurar
  namespace/ACL do Valkey para `audit`, permissão RabbitMQ do BFF em `audit.events` e chave
  de proteção do outbox em secret manager (valor local gerado fora do repositório). Validar a
  credencial runtime de `audit` com `SELECT, INSERT` e sem `UPDATE/DELETE` na evidência.
- **Contratos:** validar YAML com o ruleset/CLI do [índice de contratos](contracts.md), depois
  verificar respostas, erros e schemas reais de cada `operationId`; publicar e consumir
  `ComplementoConfirmado` no broker com outbox, redelivery, divergência e DLQ. Validar que o
  primeiro canal de Auditoria ainda consome atos. O smoke usa
  `http://localhost:8081/admin/auditoria` e detalhe sob a mesma origem/base path; em laboratório,
  `https://c4c-admin.lab.tasso.dev.br/admin/auditoria`. Testar navegação real, inclusive reload
  direto no detalhe, não só renderização de componente.
- **Observabilidade específica:** idade e falhas de publicação do novo tipo de outbox, DLQ de
  complemento, divergência de `confirmationId`, quantidade/tamanho de snapshots e tempo entre
  confirmação e visibilidade. Tags somente com IDs técnicos permitidos e desfecho; nunca
  explicação, motivo, nome, e-mail ou referência de autor/alvo.

## Questões em Aberto

- [ ] **Capacidade de Valkey por volume real:** plataforma dimensiona memória e alerta de
  snapshots com dados de carga antes de produção. Sem isso, a busca ampla pode falhar
  explicitamente sob pressão; não compromete a correção das páginas aceitas.

## Architecture Decision Records

- [ADR-0001: Monorepo de código](../../docs/adr/0001-monorepo-de-codigo.md) — serviços
  existentes permanecem unidades de deploy próprias.
- [ADR-0002: Plataforma de runtime Coolify](../../docs/adr/0002-plataforma-de-runtime-coolify.md) —
  PostgreSQL, RabbitMQ, Valkey e segredos são provisionados pela fundação.
- [ADR-0005: Sessão do ator interno](../../docs/adr/0005-sessao-e-servico-do-backoffice.md) —
  sessão vigente na borda e JWT curto validado pelo serviço dono.
- [ADR-0007: Snapshots efêmeros da consulta de Auditoria](../../docs/adr/0007-snapshots-efemeros-da-consulta-de-auditoria.md)
  — **Accepted**; adiciona cache temporário de IDs no `audit`, sem mutar evidência.
