---
tsg_artifact: prd
product: code-4-coders
capability: CAP-030
version: 1.0
status: draft
updated: 2026-09-24
sources: backlog/capabilities.md@1.4, vision.md@1.2, context/domain-map.md@1.2, context/architecture-baseline.md@1.2, domains/auditoria-e-conformidade/domain.md@1.1, domains/identidade-e-acesso/domain.md@1.1
---

# Trilha de auditoria — registro imutável de atos administrativos

## Visão Geral

A escola vai delegar poder de operação: o administrador convida professores, suporte e financeiro e
concede ou revoga papéis. Cada um desses atos muda quem pode fazer o quê na plataforma, e hoje não
existe lugar onde eles fiquem registrados de forma que ninguém — nem quem praticou o ato — consiga
apagar ou alterar.

Esta entrega cria esse lugar. Todo ato administrativo comunicado por um domínio de origem é
guardado como Registro de Auditoria permanente, com autor, alvo, motivo e momento. O que chega
incompleto é guardado mesmo assim, marcado, e a operação fica sabendo. Nada entra por outro caminho
e nada sai.

Nesta fatia não há tela: quem se beneficia é o administrador que vai consultar a trilha no próximo
PRD desta capacidade, e a própria `CAP-002`, que passa a ter onde entregar os seus atos desde o
primeiro convite.

---

## Rastreabilidade

### Capacidade e fronteiras

- **Capacidade:** `CAP-030` — Trilha de auditoria de atos administrativos.
- **Escopo desta entrega:** `CAP-030` em fatia mínima, primeiro PRD da capacidade: receber o ato
  administrativo no envelope da Auditoria, guardá-lo de forma permanente e inalterável, não duplicar
  reentregas e registrar e alertar o ato não conforme. Os tipos de ato aceitos são os quatro de
  Identidade e Acesso.
- **Fora desta entrega:**
  - Consulta da trilha pelo administrador no backoffice → **segundo PRD de `CAP-030`**, depois de
    `CAP-002` (depende do papel de administrador).
  - Complemento de registro (RN-A07) como ação de operação → entra com a consulta, que é onde ele
    é visto.
  - Atos de publicação de curso e de concessão de cortesia → entram como novos tipos com `CAP-005`
    e `CAP-008`/`CAP-009`.
  - Solicitação do titular, base legal e retenção da trilha → `CAP-031`, revisão D18.
- **Domínios atravessados:** Auditoria e Conformidade,
  [domain.md](../../domains/auditoria-e-conformidade/domain.md) v1.1; Identidade e Acesso,
  [domain.md](../../domains/identidade-e-acesso/domain.md) v1.1, como origem dos quatro tipos de ato.
- **Junta entre os domínios:** Identidade e Acesso é dona do **conteúdo** do ato — o que aconteceu,
  quem fez, sobre quem, por quê (Identidade RN-19). Auditoria e Conformidade é dona da **forma** em
  que aceita o ato, `auditoria.ato-praticado`, e do Registro de Auditoria (RN-A14). A Auditoria não
  decide se o ato era permitido nem desfaz o ato (RN-A10, RN-A11).
- **Dependências entre capacidades:** `CAP-001` (mecanismo de conta), entregue. O backlog também
  lista `CAP-002`; a dependência é circular e foi resolvida em **OD19**: `CAP-030` sai antes, como
  provedora, e `CAP-002` publica os seus atos no envelope desta entrega.
- **Restrições do baseline:** serviço próprio, fora do alcance de quem pratica o ato (BA03, DE13);
  escrita só por consumo de mensagem, sem alteração nem exclusão (G13, baseline regra 8); nenhum
  domínio consulta a trilha para decidir (baseline regra 10); `tenant_id` desde a Fase 1 (BA10, G07);
  nenhum dado pessoal em telemetria, log ou URL (G10, G23).

### Vision Doc

- **Objetivos de negócio atendidos:** objetivo 5, "ser operada — backoffice com permissões por
  papel, trilha de auditoria"; `C16` em versão mínima na Fase 1.
- **Restrições globais aplicáveis:** LGPD — dado pessoal no registro é referência, não cópia, para
  não inviabilizar a anonimização futura.

### Domain Docs

- **Entidades envolvidas:** Ato Administrativo e Registro de Auditoria (Auditoria e Conformidade);
  Conta de ator interno, Papel e Convite de Acesso Interno (Identidade e Acesso), apenas como
  referência de autor e alvo.
- **Regras de negócio referenciadas:** Auditoria RN-A01 a RN-A06, RN-A08, RN-A10 a RN-A14;
  Identidade RN-19, RN-20, RN-25.
- **Mensagem consumida:** `auditoria.ato-praticado`, com os tipos `convite-interno-emitido`,
  `convite-interno-aceito`, `papel-concedido` e `papel-revogado`.
- **Mensagens produzidas:** nenhuma. O alerta de não conformidade é sinal operacional (domain doc §7).

## Termos Canônicos

| Termo | Definição de negócio | Escopo/Fonte |
|---|---|---|
| Ato praticado | A comunicação, pelo domínio de origem, de que um ato administrativo aconteceu. É o que chega à Auditoria | Auditoria RN-A14 |
| Registro conforme | Registro de um ato que chegou com tipo conhecido, autor, alvo e motivo quando exigido | Esta entrega |
| Registro não conforme | Registro de um ato que chegou sem algum desses elementos. É guardado, marcado com a razão e alertado | Auditoria RN-A06 |
| Mensagem ilegível | Algo que chegou à Auditoria mas não é reconhecível como ato — não traz nem identificação do fato nem origem. Não vira registro | Esta entrega, DP-05 |

---

## Objetivos

- Todo ato administrativo praticado a partir do primeiro convite de `CAP-002` tem registro — nenhum
  se perde, nenhum aparece duas vezes.
- Nenhum registro pode ser alterado ou apagado depois de gravado, e isso é demonstrado, não apenas
  prometido.
- Falha do domínio de origem em informar autor, alvo ou motivo é descoberta no mesmo dia, pela
  operação, e não meses depois por quem consulta a trilha.
- `CAP-002` nasce com o destino dos seus atos já pronto, sem stub.

---

## Histórias de Usuário

- **US-01** — Como **administrador**, eu quero que todo convite, aceite, concessão e revogação de
  papel fique registrado com autor, alvo, motivo e momento para que, ao investigar um acesso
  indevido, eu saiba quem o concedeu e por quê.
- **US-02** — Como **administrador**, eu quero que ninguém consiga alterar ou apagar esse registro,
  nem eu, para que a trilha valha como evidência.
- **US-03** — Como **operação**, eu quero ser alertada quando um ato chega sem autor, alvo ou motivo
  obrigatório para que a falha do domínio de origem seja corrigida antes de virar lacuna.
- **US-04** — Como **domínio de origem** (Identidade e Acesso em `CAP-002`), eu quero comunicar o ato
  num formato único e estável para que passar a ser auditado não exija mudança na Auditoria.

---

## Funcionalidades Principais

### RF-01: Registrar o ato administrativo recebido

**Descrição:** ao receber um ato praticado de tipo conhecido, com autor, alvo e motivo quando o tipo
o exige, a Auditoria grava um Registro de Auditoria conforme. O registro guarda o tipo do ato, o
domínio de origem, o autor, o alvo, o motivo, o momento em que o ato foi praticado, o momento em que
a Auditoria o recebeu, a identificação do fato e o tenant. Autor e alvo são guardados por referência
à identidade, sem e-mail, nome ou outro dado pessoal copiado.

Exigência de motivo por tipo nesta entrega:

| Tipo | Autor | Alvo | Motivo |
|---|---|---|---|
| `convite-interno-emitido` | Administrador que convidou | Convite (e o e-mail convidado, por referência ao convite) | Obrigatório |
| `convite-interno-aceito` | O próprio convidado | Convite aceito | Não se aplica |
| `papel-concedido` | Ator que concedeu | Conta que recebeu o papel, e o papel | Obrigatório |
| `papel-revogado` | Ator que revogou | Conta que perdeu o papel, e o papel | Obrigatório |

**Critérios de Aceitação:**

- **Given** um ato `papel-concedido` com autor, alvo, papel, motivo e momento
  **When** a Auditoria o recebe
  **Then** existe um Registro de Auditoria conforme com todos esses elementos, com o momento do ato
  informado pela origem e o momento do recebimento, ambos preservados.

- **Given** um ato `convite-interno-aceito` sem motivo
  **When** a Auditoria o recebe
  **Then** o registro é conforme — o aceite não exige motivo.

- **Given** um ato cujo autor e alvo são contas identificadas
  **When** o registro é gravado
  **Then** ele contém a referência às contas e nenhum e-mail, nome ou outro dado pessoal delas.

- **Given** dois atos de tenants diferentes
  **When** ambos são registrados
  **Then** cada registro pertence ao tenant do seu ato e nenhum registro existe sem tenant.

- **Given** um ato recebido pela Auditoria
  **When** o registro é gravado
  **Then** nenhum log, métrica ou rastreamento produzido pelo processamento contém o motivo, e-mail
  ou nome envolvidos.

**Prioridade:** Must Have

**Rastreabilidade:** RN-A02, RN-A04, RN-A05, RN-A08, RN-A13, RN-A14 · Identidade RN-19

---

### RF-02: Não duplicar ato reentregue

**Descrição:** o mesmo ato — identificado pelo domínio de origem e pela identificação do fato — pode
chegar mais de uma vez. A trilha guarda um único registro por ato.

**Critérios de Aceitação:**

- **Given** um ato já registrado
  **When** a mesma comunicação chega de novo
  **Then** continua existindo um único registro, e o original não é alterado.

- **Given** dois atos distintos com o mesmo tipo, autor, alvo e momento, mas identificações de fato
  diferentes
  **When** ambos chegam
  **Then** existem dois registros — igualdade de conteúdo não é duplicidade.

- **Given** uma reentrega do mesmo fato com conteúdo divergente do registro original
  **When** ela chega
  **Then** o registro original permanece idêntico, nenhum segundo registro é criado e a divergência
  é alertada à operação.

**Prioridade:** Must Have

**Rastreabilidade:** RN-A01, RN-A03

---

### RF-03: Registrar e alertar o ato não conforme

**Descrição:** um ato que chega sem autor, sem alvo, sem motivo quando o tipo o exige, ou com tipo
que a Auditoria ainda não conhece, **é registrado mesmo assim**, como não conforme, com a razão de
cada falta. A operação é alertada a cada ocorrência. Nenhum ato é descartado nem retido por estar
incompleto.

**Critérios de Aceitação:**

- **Given** um ato `papel-revogado` sem motivo
  **When** a Auditoria o recebe
  **Then** existe um registro não conforme com a razão "motivo ausente", todos os demais elementos
  recebidos preservados, e a operação é alertada.

- **Given** um ato sem autor e sem alvo
  **When** a Auditoria o recebe
  **Then** o registro não conforme declara as duas razões, e há um único alerta para o ato.

- **Given** um ato com tipo que não está entre os aceitos
  **When** a Auditoria o recebe
  **Then** ele é registrado como não conforme com a razão "tipo desconhecido", preservando o tipo
  informado, e a operação é alertada.

- **Given** um ato não conforme já registrado
  **When** a mesma comunicação chega de novo
  **Then** vale RF-02: um único registro e nenhum alerta repetido.

**Prioridade:** Must Have

**Rastreabilidade:** RN-A05, RN-A06 · DP-03, DP-04

---

### RF-04: Tratar a mensagem ilegível sem perdê-la

**Descrição:** o que chega sem identificação do fato ou sem domínio de origem não pode ser registrado
como ato — não há como garantir RF-02 nem dizer de onde veio. Essa mensagem não vira registro: ela é
retida à parte, intacta, e a operação é alertada para investigar o produtor.

**Critérios de Aceitação:**

- **Given** uma mensagem sem identificação do fato
  **When** ela chega à Auditoria
  **Then** nenhum Registro de Auditoria é criado, a mensagem fica retida para investigação sem
  alteração e a operação é alertada.

- **Given** a operação corrigiu o produtor e o ato é comunicado de novo, de forma legível
  **When** a Auditoria o recebe
  **Then** ele segue RF-01 ou RF-03 normalmente.

**Prioridade:** Must Have

**Rastreabilidade:** RN-A02, RN-A03 · DP-05

---

### RF-05: Garantir que o registro não muda depois de gravado

**Descrição:** nenhum caminho do produto altera ou apaga um registro, e nenhuma credencial usada pela
Auditoria para gravar tem esse poder. A imutabilidade é demonstrada no aceite, não apenas declarada.
Nenhum outro domínio grava na trilha — a única entrada é o ato praticado.

**Critérios de Aceitação:**

- **Given** um registro gravado
  **When** se tenta alterá-lo ou apagá-lo com a mesma credencial que a Auditoria usa para gravar
  **Then** a tentativa é recusada e o registro permanece idêntico.

- **Given** o serviço de um domínio de origem
  **When** ele tenta gravar ou ler a trilha diretamente, sem passar pelo ato praticado
  **Then** não tem acesso.

- **Given** um conjunto de registros gravados
  **When** a Auditoria é reiniciada, atualizada ou reprocessa mensagens
  **Then** os registros continuam idênticos e em mesmo número.

**Prioridade:** Must Have

**Rastreabilidade:** RN-A01, RN-A02, RN-A10 · G13 · BA03

---

## Decisões de Produto

| ID | Decisão confirmada | Alternativas descartadas e motivo | Impacto no PRD | Registro |
|---|---|---|---|---|
| DP-01 | Primeiro PRD de `CAP-030` é só o registro dos quatro atos de Identidade; consulta vem no segundo PRD, após `CAP-002`. Confirmado em 2026-09-24 | `CAP-030` inteira num PRD — exigiria `CAP-002` antes ou nasceria sobre papel de administrador falso | Escopo; Não-Objetivos | OD19 |
| DP-02 | A Auditoria define o envelope único `auditoria.ato-praticado`; todo domínio comunica o ato nele. Confirmado em 2026-09-24 | Consumir os eventos próprios de cada domínio — o contrato de Identidade teria de sair antes e cada domínio novo exigiria tradução nova na Auditoria | RF-01; junta | RN-A14 |
| DP-03 | Ato incompleto é registrado como não conforme e alertado. Confirmado em 2026-09-24 | Recusar e mandar para fila de erro — o ato sumiria da trilha até o produtor ser corrigido | RF-03 | OD20 |
| DP-04 | Tipo de ato desconhecido é tratado como não conforme, não recusado | Recusar — um domínio que passe a publicar antes de o tipo ser aceito perderia atos | RF-03 | Derivada de DP-03; **a confirmar** |
| DP-05 | Mensagem sem identificação do fato ou sem origem não vira registro: fica retida intacta e é alertada | Registrá-la como não conforme — sem identificação não há como evitar duplicidade, e a trilha passaria a guardar coisas que não são atos | RF-04 | **A confirmar** |

---

## Restrições Técnicas de Alto Nível

- A Auditoria roda como serviço próprio (`audit`, já provisionado na Fase 0), com credencial que só
  acrescenta registros.
- Esta entrega define e publica o contrato de `auditoria.ato-praticado`. `CAP-002` o adota; o §7 do
  domain doc de Identidade será revisto no PRD de `CAP-002` para refletir isso.
- Dado pessoal: autor e alvo por referência; motivo é texto livre do autor, não sai em telemetria.
- O canal do alerta à operação segue o que a plataforma já usa; a escolha é da TechSpec.

---

## Não-Objetivos (Fora de Escopo)

- Qualquer tela ou consulta da trilha, inclusive para o administrador (segundo PRD de `CAP-030`).
- Complemento de registro por ação de operação (RN-A07), que acompanha a consulta.
- Atos de outros domínios: publicação de curso, cortesia, reembolso, nota, banimento, moderação.
- Reconstruir atos anteriores à trilha — o seed do primeiro administrador não aparece nela (RN-A12).
- Verificar se o ato era permitido, ou comparar a trilha com o estado de Identidade (RN-A10, RN-A11).
- Retenção, anonimização e solicitação do titular (`CAP-031`).
- Log técnico de aplicação: a trilha só guarda ato administrativo.

---

## Plano de Rollout Faseado

### MVP (Fase 1) — este PRD

- **Funcionalidades incluídas:** RF-01 a RF-05.
- **Gate para seguir a `CAP-002`:** os quatro tipos registrados de ponta a ponta a partir de atos
  comunicados no contrato publicado; reentrega sem duplicidade; não conforme e ilegível alertados; e
  a tentativa de alteração recusada.

### Segundo PRD de `CAP-030` (ainda MVP, após `CAP-002`)

- Consulta da trilha pelo administrador e complemento de registro.

### Rodadas seguintes

- Novos tipos de ato entram com as capacidades que os produzem, sem mudança no envelope.

---

## Métricas de Sucesso

| Medida | Definição | Meta e momento |
|---|---|---|
| Ato perdido | Ato praticado em `CAP-002` sem registro correspondente | Zero no aceite e desde o lançamento de `CAP-002` |
| Registro duplicado | Mais de um registro para a mesma identificação de fato | Zero no aceite e em operação |
| Registro alterado | Registro cujo conteúdo difere do gravado originalmente | Zero, sempre; demonstrado no aceite por RF-05 |
| Não conformidade | Registros não conformes por domínio de origem e razão | Mensurável desde o lançamento; após `CAP-002` estabilizar, zero não conformes por falha de produtor é o alvo |
| Tempo até alerta | Do recebimento de um ato não conforme ou ilegível ao alerta à operação | Mesmo dia; o número exato é fixado na TechSpec com o canal de alerta |

---

## Riscos e Mitigações

- **`CAP-002` publicar fora do contrato:** os atos chegariam não conformes ou ilegíveis. Mitigação:
  o contrato é publicado nesta entrega e o PRD de `CAP-002` o cita; RF-03 e RF-04 tornam a falha
  visível no mesmo dia.
- **Trilha pronta e sem uso até `CAP-002`:** o valor só aparece quando o primeiro convite é emitido.
  Mitigação: aceita por OD19 — é o preço de `CAP-002` nascer sem stub.
- **Motivo com dado pessoal de terceiros:** o motivo é texto livre e pode citar pessoas. Mitigação:
  RN-A08 como orientação ao autor na tela de `CAP-002`; o tratamento definitivo é da revisão D18.

---

## Questões em Aberto

- **QT-01 — "Alterar papel" é ato próprio? (OD22).** Dono: Identidade e Acesso, no PRD de
  `CAP-002`. Se nascer um tipo `papel-alterado`, ele entra aqui como tipo novo sem mudança no
  envelope. Não bloqueia esta entrega.
- **QT-02 — Retenção da trilha (QA-A05, AB03).** Dono: negócio + Auditoria. Até a revisão D18,
  nada é apagado. Não bloqueia.
- **QT-03 — Identidade §7.** O domain doc de Identidade ainda lista os quatro atos como eventos
  próprios. Dono: revisão do domain doc no PRD de `CAP-002`. Não bloqueia esta entrega; bloqueia o
  PRD de `CAP-002` se não for feita.
