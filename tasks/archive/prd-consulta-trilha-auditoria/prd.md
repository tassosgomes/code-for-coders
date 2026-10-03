---
tsg_artifact: prd
product: code-4-coders
capability: CAP-030
version: 1.0
status: approved
updated: 2026-09-27
sources: backlog/capabilities.md@1.4, vision.md@1.2, context/domain-map.md@1.2, context/architecture-baseline.md@1.2, domains/auditoria-e-conformidade/domain.md@1.2, domains/identidade-e-acesso/domain.md@1.1, tasks/prd-trilha-auditoria/prd.md@1.2, tasks/prd-acesso-interno/prd.md@1.1
---

# Consulta e complemento da trilha de auditoria

## Visão Geral

O administrador já consegue delegar acesso, e a Auditoria já guarda os atos de convite, aceite,
concessão e revogação de papel. Falta ao administrador consultar essa evidência no backoffice. Esta
entrega torna a trilha investigável por período, tipo, autor, alvo e conformidade, sem dar a quem
consulta o poder de alterar o que aconteceu. Também permite acrescentar um complemento vinculado a
um registro, preservando o original. O administrador confirma no backoffice uma explicação
vinculada; ela só é gravada depois que a Auditoria recebe o fato dessa ação.

---

## Rastreabilidade

### Capacidade e fronteiras

- **Capacidade:** `CAP-030` — Trilha de auditoria de atos administrativos.
- **Escopo desta entrega:** segundo PRD da capacidade, ainda no MVP: consulta da trilha no
  backoffice somente pelo administrador e complemento de registro (OD19, OD21; Auditoria RN-A07 e
  RN-A09). O primeiro PRD, [registro imutável](../prd-trilha-auditoria/prd.md), já foi entregue.
- **Fora desta entrega:** novos tipos de ato de publicação de curso e concessão de cortesia, que
  entram com `CAP-005` e `CAP-008`/`CAP-009`; solicitações do titular, retenção e anonimização da
  trilha, que voltam em `CAP-031` na Fase 5; consulta por outros papéis, exportação em massa e
  reconstrução de atos anteriores à trilha.
- **Domínios atravessados:** Auditoria e Conformidade,
  [domain.md](../../../domains/auditoria-e-conformidade/domain.md) v1.2, dona dos registros e da
  consulta; Identidade e Acesso, [domain.md](../../../domains/identidade-e-acesso/domain.md) v1.1,
  dona da sessão, do papel de administrador e das identidades referenciadas.
- **Revisão da regra compartilhada:** RN-A02, RN-A07 e RN-A09 foram esclarecidas no domain doc
  v1.2 conforme OD38 e OD39.
- **Junta:** Auditoria guarda referências de autor e alvo, sem copiar seus dados pessoais.
  Identidade decide se o ator corrente é administrador e fornece a identificação legível dessas
  referências. A consulta não altera papel nem usa a trilha para autorizar atos (Auditoria RN-A08 a
  RN-A11; Identidade RN-16 a RN-18).
- **Dependências:** `CAP-002` entregue para papel, sessão e backoffice; primeiro PRD de `CAP-030`
  entregue para registros. A dependência circular do backlog foi fatiada em OD19.
- **Restrições herdadas:** registros permanecem imutáveis e isolados de quem pratica o ato
  (BA03, DE13, G13); leitura e complemento limitados ao tenant do administrador (BA10, G07,
  RN-A13); dados pessoais e motivos não aparecem em logs, métricas ou URLs (G10, G23).

### Vision Doc e Domain Docs

- **Objetivo de negócio:** objetivo 5 da visão, operar o backoffice com permissões por papel e
  trilha de auditoria; `C16` em versão mínima na Fase 1.
- **Entidades:** Ato Administrativo e Registro de Auditoria (Auditoria); Conta, Papel, Permissão e
  Convite de Acesso Interno (Identidade), estes últimos somente como fontes de identificação.
- **Regras referenciadas:** Auditoria RN-A01, RN-A02, RN-A04, RN-A06 a RN-A13; Identidade RN-13,
  RN-14, RN-16 a RN-20, RN-23 a RN-25.
- **Eventos:** a consulta não consome nem publica novo tipo de ato. Os quatro tipos de Identidade
  já são registrados pelo primeiro PRD. A confirmação de um complemento pelo administrador
  comunica o fato à Auditoria; o novo registro só nasce do consumo desse fato (G13, OD38). O
  contrato exato dessa comunicação pertence à etapa de contratos.

## Termos Canônicos

| Termo | Significado nesta entrega | Origem |
|---|---|---|
| Registro original | Evidência imutável recebida de um ato administrativo, conforme ou não conforme | Auditoria RN-A01, RN-A06 |
| Complemento | Novo registro com explicação posterior que referencia um original sem substituir seus campos ou sua conformidade | Auditoria RN-A01, RN-A07; OD39 |
| Momento do ato | Instante em que o domínio de origem diz que o ato ocorreu; ordena a trilha | Auditoria RN-A04 |
| Momento do recebimento | Instante em que a Auditoria guardou o ato; indica atrasos | Auditoria RN-A04 |

---

## Objetivos

- Permitir que o administrador encontre um ato da gestão de acesso e veja quem o praticou, o alvo,
  o momento, o motivo recebido e a conformidade, inclusive quando algum dado faltou.
- Preservar a evidência: a consulta e o complemento não mudam nem apagam o registro original.
- Impedir que outro papel ou outro tenant tenha acesso à trilha, inclusive por acesso direto à
  interface de consulta.

## Histórias de Usuário

- **US-01:** Como administrador, quero localizar atos de gestão de acesso por período, tipo,
  autor ou alvo para investigar uma concessão ou revogação.
- **US-02:** Como administrador, quero ver o conteúdo recebido e as faltas de um registro não
  conforme para distinguir o ato ocorrido de uma falha do produtor.
- **US-03:** Como administrador, quero acrescentar contexto a um registro sem apagar a evidência
  original para deixar explícito o que a operação apurou depois.
- **US-04:** Como ator interno sem papel de administrador, não devo conseguir consultar ou
  complementar a trilha, mesmo conhecendo o endereço da tela ou a referência de um registro.

---

## Funcionalidades Principais

### RF-01: Restringir a trilha ao administrador do tenant

**Descrição:** somente uma sessão interna vigente com papel de administrador pode consultar a
trilha ou iniciar um complemento. A área de Auditoria não é oferecida aos demais papéis. A
autorização é reavaliada em cada ação, para que a revogação do papel tenha efeito na ação seguinte.
Nenhuma consulta ou complemento cruza o tenant da sessão.

**Critérios de aceitação:**

- **Given** um administrador com sessão vigente **When** abre a área de Auditoria **Then** vê
  somente registros de seu tenant.
- **Given** um professor, financeiro, suporte, ator sem papel ou aluno **When** tenta abrir a área
  pela navegação ou por acesso direto **Then** não vê registros nem detalhes ou complementos.
- **Given** um administrador com a área aberta **When** seu papel de administrador é revogado
  **Then** a próxima consulta ou tentativa de complemento é recusada.
- **Given** uma referência de registro de outro tenant **When** um administrador a usa **Then**
  não recebe o conteúdo nem a confirmação de que esse registro existe.

**Prioridade:** Must Have. **Rastreabilidade:** RN-A09, RN-A13; Identidade RN-16 a RN-18.

### RF-02: Localizar registros da trilha

**Descrição:** o administrador vê os registros do próprio tenant, mais recentes primeiro pelo
momento do ato. Pode restringir a lista por intervalo de data e hora do ato, tipo, autor, alvo e
conformidade. Pode combinar filtros e percorrer resultados sem perder o filtro aplicado. Cada linha
identifica o tipo, o momento do ato, as referências de autor e alvo e se o registro é conforme;
motivo e texto livre ficam no detalhe. O intervalo selecionado inclui os dois instantes extremos.

**Critérios de aceitação:**

- **Given** registros de atos em momentos diferentes **When** o administrador abre a lista
  **Then** os mais recentes aparecem primeiro e o momento do recebimento não substitui o do ato
  na ordenação.
- **Given** atos de convite, aceite, concessão e revogação **When** filtra por tipo e período
  **Then** aparecem apenas os que satisfazem ambos os filtros, inclusive os extremos do período.
- **Given** uma referência de autor ou alvo **When** aplica o filtro correspondente **Then**
  aparecem os registros ligados àquela referência, incluindo os não conformes que ainda a trazem.
- **Given** mais resultados do que os apresentados de uma vez **When** percorre a lista
  **Then** consegue alcançar todos sem repetir ou perder registros por mudança de página.
- **Given** nenhum registro no filtro **When** a busca termina **Then** a área apresenta estado
  vazio e permite alterar os filtros.

**Prioridade:** Must Have. **Rastreabilidade:** RN-A04, RN-A06, RN-A09, RN-A13; US-01.

### RF-03: Examinar o registro e suas referências

**Descrição:** o detalhe distingue momento do ato e do recebimento e mostra origem, tipo, autor,
alvo, motivo recebido, conformidade e cada razão de não conformidade. A identificação legível de
autor ou alvo vem de Identidade no momento da consulta; a Auditoria conserva apenas a referência.
Se a identidade não puder ser resolvida, a referência continua visível. Valor ausente é mostrado
como ausente, sem ser preenchido por dedução. O administrador pode partir do detalhe para a lista
filtrada pela referência disponível.

**Critérios de aceitação:**

- **Given** um registro conforme de `papel-concedido` **When** abre o detalhe **Then** vê autor,
  conta alvo, papel, motivo e os dois momentos, identificados separadamente.
- **Given** um registro não conforme sem autor ou motivo obrigatório **When** abre o detalhe
  **Then** vê as faltas e o conteúdo recebido, sem autor ou motivo inventado.
- **Given** uma conta ou convite cuja identificação legível não está disponível **When** abre o
  detalhe **Then** vê a referência preservada, sem alterar o registro.
- **Given** um registro de convite aceito sem motivo **When** abre o detalhe **Then** não há aviso
  de motivo ausente, pois esse tipo não o exige.

**Prioridade:** Must Have. **Rastreabilidade:** Auditoria RN-A04 a RN-A06, RN-A08, RN-A11;
Identidade RN-23; US-02.

### RF-04: Acrescentar um complemento vinculado ao original

**Descrição:** o administrador confirma no backoffice uma explicação sobre um registro encontrado.
A Auditoria recebe o fato dessa ação e então cria outro Registro de Auditoria, com referência ao
original, autor do complemento, momento e texto explicativo. O original permanece idêntico; sua
conformidade não muda automaticamente. O complemento não substitui autor, alvo, tipo ou motivo
recebidos e não apaga a indicação de não conformidade. Uma nova explicação requer outro
complemento, sem edição ou exclusão do anterior.

**Critérios de aceitação:**

- **Given** um administrador vendo um registro de seu tenant **When** confirma um complemento
  com explicação **Then** vê um novo registro vinculado ao original, com sua identidade e o
  momento da ação.
- **Given** um registro não conforme **When** recebe complemento **Then** continua não conforme;
  a explicação não reescreve o que chegou da origem.
- **Given** complemento vazio **When** o administrador tenta confirmar **Then** nada é criado e a
  área pede uma explicação.
- **Given** uma tentativa repetida por falha de resposta **When** a mesma confirmação é
  reprocessada **Then** existe um único complemento para aquela confirmação.
- **Given** um registro de outro tenant ou ator sem papel de administrador **When** tenta
  complementar **Then** a ação é recusada e nenhum complemento é criado.

**Prioridade:** Must Have. **Rastreabilidade:** RN-A01, RN-A07, RN-A09, RN-A13; US-03.

### RF-05: Mostrar a sequência do registro e dos complementos

**Descrição:** o detalhe de um registro mostra seus complementos por momento de criação,
distinguindo claramente o fato original do contexto acrescentado depois. Um complemento aponta
sempre para o original; complementos posteriores não alteram os anteriores. A lista principal
preserva o original como ato investigável e sinaliza quando há complementos.

**Critérios de aceitação:**

- **Given** um original com dois complementos **When** o administrador abre o detalhe **Then**
  vê os três registros em ordem, cada um com seu autor e momento, sem confundir texto acrescentado
  com o conteúdo que chegou da origem.
- **Given** um original sem complemento **When** abre o detalhe **Then** não aparece contexto
  acrescentado.
- **Given** um complemento ligado a um registro **When** a lista é filtrada pelo ato original
  **Then** o original continua localizável e a indicação de complemento permanece visível.

**Prioridade:** Must Have. **Rastreabilidade:** RN-A01, RN-A07; US-02, US-03.

---

## Experiência do Usuário

O administrador entra na área de Auditoria pelo backoffice, filtra os atos e abre o detalhe.
Os dois momentos têm rótulos distintos. Referências não resolvidas e campos ausentes recebem
marcação explícita; não são ocultados. Registros não conformes exibem as razões. O complemento,
quando confirmado, aparece junto do original como uma ação posterior de autoria própria.
Lista, filtros, detalhe e confirmação são operáveis por teclado e têm rótulos legíveis.

## Decisões de Produto Herdadas

| ID | Decisão | Impacto | Origem |
|---|---|---|---|
| DP-01 | Consulta após `CAP-002`, no segundo PRD da `CAP-030` | Esta entrega | OD19 |
| DP-02 | Apenas administrador consulta no MVP | RF-01 | OD21, RN-A09 |
| DP-03 | Novo registro complementa o original, sem alterá-lo | RF-04, RF-05 | RN-A01, RN-A07 |
| DP-04 | O complemento acrescenta somente uma explicação posterior; autor, alvo, tipo, motivo recebido e conformidade do original permanecem como chegaram. Confirmado em 2026-09-27 | RF-04, RF-05 | OD39 |
| DP-05 | O administrador confirma o complemento no backoffice; a Auditoria só grava o novo registro após receber o fato dessa ação. Confirmado em 2026-09-27 | RF-01, RF-04; RN-A02 e RN-A07 esclarecidas no draft do domínio | OD38, G13 |

## Restrições de Alto Nível

- O acesso à consulta usa a sessão do backoffice de `CAP-002`; ocultar a navegação não substitui
  a recusa de acesso direto.
- A trilha só serve à investigação humana. Nenhum domínio pode consultá-la para autorizar,
  decidir ou calcular estado (RN-A10, RN-A11).
- O registro e seus complementos são imutáveis. A consulta não duplica dados pessoais de
  Identidade na Auditoria; motivo e texto livre não entram em telemetria.

## Não-Objetivos

- Expor a trilha a professor, suporte, financeiro ou aluno.
- Editar ou excluir registro ou complemento, transformar registro não conforme em conforme,
  corrigir o estado do domínio de origem ou usar a trilha como fonte de autorização.
- Buscar por texto livre do motivo, exportar a trilha ou produzir relatórios agregados.
- Acrescentar nesta entrega atos de Conteúdo, Matrícula, Comércio ou outros domínios; eles entram
  com as capacidades que os produzem.
- Definir retenção, anonimização e atendimento ao titular (`CAP-031`, Fase 5).

## Plano de Rollout Faseado

- **MVP, esta entrega:** RF-01 a RF-05 sobre os quatro tipos de ato de Identidade já recebidos.
  Aceite: administrador localiza e examina um caso de concessão/revogação e um ato não conforme;
  ator sem papel e outro tenant não leem a trilha; o complemento preserva o original.
- **MVP, capacidades seguintes:** novos tipos de ato entram com `CAP-005` e `CAP-008`/`CAP-009`,
  sob as mesmas regras de leitura. Esta entrega não antecipa seus fatos.
- **Fase 5:** `CAP-031` resolve retenção, anonimização e solicitação do titular.

## Métricas de Sucesso

| Medida | Definição | Meta e momento |
|---|---|---|
| Caso investigável | Nos cenários de aceite, o administrador encontra o ato e distingue conteúdo original, faltas e complementos | Todos os cenários de aceite desta entrega |
| Leitura indevida | Ator sem papel de administrador ou de outro tenant obtém conteúdo da trilha | Zero nos cenários de aceite e em operação |
| Original alterado | Registro original diferente após consulta ou complemento | Zero no aceite e em operação |
| Complemento órfão ou duplicado | Complemento sem original do mesmo tenant, ou repetido pela mesma confirmação | Zero no aceite e em operação |

## Riscos e Mitigações

| Risco de produto | Efeito | Mitigação |
|---|---|---|
| Complemento ser entendido como correção do fato original | Investigação usa explicação posterior como se fosse o ato recebido | Exibir conteúdo original e complementos separados, com autor e momento de cada um; RN-A01, RN-A07 |
| Identidade de autor ou alvo temporariamente indisponível | Administrador não reconhece imediatamente a pessoa | Mostrar a referência preservada e a indisponibilidade sem inventar nome; RF-03 |
| Compartilhamento inadvertido da trilha com outro papel | Exposição de motivos e atos sensíveis | Papel de administrador exigido em toda ação; RF-01 |

---

*Segundo PRD aprovado da CAP-030; o primeiro PRD permanece como registro da fatia de ingestão.*
