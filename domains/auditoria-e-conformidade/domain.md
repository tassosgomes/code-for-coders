---
tsg_artifact: domain
product: code-4-coders
version: 1.1
status: approved
updated: 2026-09-24
sources: vision.md@1.2, context/domain-map.md@1.2, context/architecture-baseline.md@1.2, backlog/capabilities.md@1.4
---

# Domain Document — Auditoria e Conformidade

> Detalha o bounded context de **um** domínio do Domain Map. Não decide prioridade, ordem nem
> escopo de entrega — isso é do backlog de capacidades e do PRD. Forneça este arquivo junto com o
> `vision.md` ao iniciar um PRD de capacidade que toque este domínio.

> **Versão parcial (D3).** Cobre só o **registro de atos administrativos** — a trilha append-only e
> a sua consulta. A metade LGPD do domínio (Solicitação do Titular, Base Legal de Tratamento,
> coordenação de exclusão e anonimização) fica para a revisão D18, junto com `CAP-031`. O que ficou
> de fora está listado em §1 para que a revisão seja acréscimo, não reescrita.

**Domínio:** Auditoria e Conformidade
**Capacidades atendidas:** `CAP-030` · `CAP-031` (só na revisão D18)
**Restrições arquiteturais pertinentes:** BA03, BA10 · G07, G10, G13, G23 · DE13 · AB03

---

## 1. Propósito do Domínio (Domain Purpose)

### Responsabilidade Principal

Guardar, de forma imutável e fora do alcance de quem o praticou, o registro de cada ato
administrativo — quem fez, o quê, sobre quem, quando e por quê — e permitir que quem governa o
consulte.

### Problema que Resolve

Delegar poder operacional (conceder papel, dar cortesia, reembolsar, alterar nota, banir) sem
registro confiável amplia o risco na mesma medida em que amplia a equipe. Um log guardado pelo
próprio domínio que praticou o ato não serve: quem tem credencial para agir tem credencial para
apagar o rastro. Este domínio existe para que todo ato tenha responsável identificável e para que
essa evidência sobreviva a quem a gerou (DE13, BA03).

### Fora do Escopo deste Domínio (Out of Scope)

- Conceder, negar ou verificar permissão → **Identidade e Acesso**. A trilha registra que um papel
  foi concedido; não decide se alguém pode agir.
- Decidir se um ato é válido ou permitido → **domínio que praticou o ato**. A Auditoria registra o
  fato consumado, não o autoriza nem o desfaz.
- Log técnico de aplicação, rastreamento, métrica e erro → **observabilidade da plataforma**. A trilha
  é de ato com consequência de negócio, não de chamada de sistema.
- Medir desempenho do negócio → **Inteligência de Negócio**.
- Servir de fonte para decisão de outro domínio → **proibido** (baseline, regra 10). Nenhum domínio
  consulta a trilha para decidir; o backoffice lê e ninguém escreve por outro caminho que não o evento.
- **Adiado para a revisão D18** (`CAP-031`, depende de AB03):
  - Solicitação do Titular (acesso, correção, exclusão e anonimização de dado pessoal) e a
    coordenação dos domínios donos na execução.
  - Base Legal de Tratamento.
  - Política de retenção da própria trilha.

---

## 2. Usuários do Domínio (Domain Users)

| Perfil (Role) | O que faz neste domínio | Frequência de uso |
|---|---|---|
| Administrador | Consulta a trilha: quem praticou um ato, sobre quem, quando e com que motivo | Eventual — sob demanda, na investigação de um caso |
| Ator interno (professor, suporte, financeiro, administrador) | Não usa o domínio: é o **autor** registrado. Não lê a trilha nem interfere nela | — |
| Domínios com ato administrativo | Publicam o fato do ato; nunca escrevem nem leem a trilha diretamente | Contínua |

---

## 3. Entidades Principais (Core Entities)

> Vocabulário de negócio, não schema. As duas vêm do Domain Map. Solicitação do Titular e Base
> Legal de Tratamento, também do mapa, entram na revisão D18.

| Entidade | Descrição | Atributos Principais | Relacionamentos |
|---|---|---|---|
| Ato Administrativo | Ação de um ator interno com efeito sobre aluno, ator interno ou dinheiro, praticada e decidida em outro domínio | tipo do ato, domínio de origem, autor, alvo, motivo, momento em que foi praticado | é evidenciado por: Registro de Auditoria |
| Registro de Auditoria | A evidência imutável de um Ato Administrativo, como recebida pela Auditoria | o ato, momento do recebimento, identificação do fato de origem, conformidade (conforme / não conforme e por quê), registro que complementa (quando houver) | evidencia: Ato Administrativo · pode complementar: outro Registro de Auditoria |

**Autor** e **alvo** são referências a identidades de Identidade e Acesso (conta de ator interno,
conta de aluno), nunca cópia do dado pessoal delas — ver RN-A08.

---

## 4. Capacidades Atendidas (Capabilities Served)

> Só referência. Prioridade, fase, dependência entre capacidades e ordem de implementação vivem em
> `backlog/capabilities.md`.

| Capacidade | O que este domínio entrega a ela |
|---|---|
| `CAP-030` | Registrar de forma imutável todo ato administrativo publicado pelos domínios de origem e permitir a consulta pelo administrador |
| `CAP-031` | **Fora desta versão.** Ponto único de resposta às solicitações do titular, coordenando os domínios donos — detalhado na revisão D18 |

`CAP-030` deriva de `C16` na visão (versão mínima); `CAP-031` de `C16` completo.

---

## 5. Juntas com Outros Domínios (Domain Joints)

> Herdadas da tabela de dependências do Domain Map: *"Auditoria e Conformidade · Domínios com ato
> administrativo · Registrar de forma imutável quem fez o quê · dono: Auditoria e Conformidade"*.
> A junta é **uma só, repetida por domínio de origem**: o domínio de origem é dono do fato do ato;
> a Auditoria é dona do registro.

### Depende de (Upstream)

| Domínio | O que consome | Tipo | Dono do dado | Criticidade |
|---|---|---|---|---|
| Identidade e Acesso | Os atos de gestão de acesso interno: convite emitido, convite aceito, papel concedido, papel revogado ([domain doc](../identidade-e-acesso/domain.md) RN-19, §7) | Evento | Identidade e Acesso (fato) / este domínio (registro) | **Alta** |
| Identidade e Acesso | A identidade de autor e alvo, por referência — para a consulta mostrar quem é quem | Dados (leitura, na consulta) | Identidade e Acesso | Média |
| Conteúdo e Currículo | Publicação de curso | Evento | Conteúdo e Currículo (fato) / este domínio (registro) | Média |
| Matrícula e Direito de Acesso | Concessão de cortesia e demais concessões e revogações administrativas | Evento | Matrícula e Direito de Acesso (fato) / este domínio (registro) | Alta |
| Cobrança e Assinatura, Avaliação, Comunidade e Engajamento, Atendimento e Suporte | Reembolso, alteração de nota, banimento, moderação e demais atos listados pelo Domain Map | Evento | Domínio de origem (fato) / este domínio (registro) | Alta |

Cada linha passa a valer quando a capacidade que produz o ato é entregue; a regra da junta é a
mesma para todas (RN-A01 a RN-A06, RN-A14).

### Fornece para (Downstream)

| Domínio | O que fornece | Tipo | Dono do dado | Criticidade |
|---|---|---|---|---|
| Backoffice (administrador) | Consulta da trilha | Dados (leitura) | Este domínio | Média |
| — | **Nenhum domínio consome a trilha.** É assimetria de fronteira, não omissão (baseline, regra 10) | — | — | — |

### Integrações Externas (External Integrations)

Nenhuma nesta versão.

---

## 6. Regras de Negócio (Business Rules)

> Prefixo `RN-A` para não colidir com as RN de Identidade e Acesso, que os mesmos PRDs citam.

| ID | Regra | Origem |
|---|---|---|
| RN-A01 | **Nada se altera nem se apaga.** Um Registro de Auditoria, uma vez gravado, é permanente e idêntico ao que foi gravado — nem administrador, nem operação, nem o domínio de origem o modificam | DE13 · G13 |
| RN-A02 | O registro **só nasce do fato publicado** pelo domínio de origem. Não há cadastro manual, importação nem escrita por outro caminho | G13 · baseline regra 10 |
| RN-A03 | O mesmo ato publicado mais de uma vez gera **um único** registro. Reentrega do fato não duplica a trilha | Integridade da trilha |
| RN-A04 | O registro guarda **dois momentos**: quando o ato foi praticado (informado pela origem) e quando a Auditoria o recebeu. A trilha é lida pelo momento do ato; o do recebimento revela atraso ou reentrega | Evidência |
| RN-A05 | Todo ato tem **autor e alvo identificados**, e tem **motivo** quando o domínio de origem o exige para aquele tipo de ato (em Identidade: RN-19). O aceite de convite tem autor — o próprio convidado — e não tem motivo | Visão · baseline (menor privilégio) · Identidade RN-19 |
| RN-A06 | Ato recebido **sem autor, sem alvo ou sem motivo obrigatório é registrado mesmo assim**, marcado como **não conforme** com a razão, e a operação é alertada. Nenhum ato é descartado nem retido por estar incompleto: perder o fato é pior que registrá-lo imperfeito, e a falha do produtor fica visível na própria trilha. Decidido em 2026-09-24 | Decisão do produto · DE13 |
| RN-A07 | Um registro errado ou incompleto **não é corrigido**: é **complementado** por um novo registro que o referencia. A trilha mostra os dois | Consequência de RN-A01 |
| RN-A08 | Autor e alvo são guardados **por referência à identidade**, não por cópia de e-mail, nome ou outro dado pessoal. O motivo é texto do autor e não deve conter dado pessoal de terceiros além do alvo já referenciado | G10 · G23 · prepara `CAP-031` |
| RN-A09 | **Somente o administrador consulta a trilha** no MVP. Estender a outro papel é mudança posterior deliberada — delegar depois é fácil, recolher é difícil. Decidido em 2026-09-24 | Decisão do produto · coerente com Identidade RN-14 |
| RN-A10 | A trilha **não é fonte de decisão**: nenhum domínio a consulta para permitir, negar ou calcular nada. Ela responde "o que aconteceu", nunca "o que pode acontecer" | Baseline regra 10 |
| RN-A11 | A trilha não substitui o estado do domínio de origem: o registro de "papel concedido" não é o papel. Divergência entre trilha e estado de origem é sinal de falha a investigar, não algo que a trilha corrige | Fronteira com Identidade |
| RN-A12 | Atos anteriores à existência da trilha **não aparecem nela** e não são reconstruídos. O caso conhecido é o seed do primeiro administrador (Identidade RN-25) | Identidade RN-25 · OD10 |
| RN-A13 | Todo registro carrega `tenant_id` desde a Fase 1, e a consulta nunca cruza tenant | BA10 · G07 |
| RN-A14 | **O formato do ato é da Auditoria.** Todo domínio de origem comunica o ato no mesmo envelope, `auditoria.ato-praticado`, informando o tipo do ato no seu próprio vocabulário. O domínio de origem é dono do **conteúdo** (o que aconteceu); a Auditoria é dona da **forma** que aceita. Um domínio novo passa a ser auditado sem mudança na Auditoria. Decidido em 2026-09-24 | Decisão do produto · mesmo padrão de `notificacao.envio-solicitado` |

---

## 7. Eventos do Domínio (Domain Events)

> O contrato real materializa no pacote de contratos e na TechSpec; aqui fica o fato de negócio.

### Produz (Publishes)

Nenhum. A trilha não publica fato que outro domínio consuma (RN-A10). O alerta de ato não conforme
(RN-A06) é sinal operacional, não evento de domínio.

### Consome (Subscribes)

- `auditoria.ato-praticado` (de: todo domínio com ato administrativo) — pedido de registro de um ato,
  no envelope definido pela Auditoria (RN-A14). **O contrato é deste domínio**, por isso o prefixo:
  quem guarda define o que aceita. O domínio de origem informa o tipo do ato, o autor, o alvo, o
  motivo quando exigido, o momento em que o ato foi praticado e a identificação do fato.

Tipos de ato aceitos nesta versão, todos de Identidade e Acesso (RN-19 daquele domain doc):

- `convite-interno-emitido` — ato de convidar alguém à operação
- `convite-interno-aceito` — o convidado ativou a conta e assumiu o papel
- `papel-concedido` — ato de conceder papel, com autor e motivo
- `papel-revogado` — ato de revogar papel, com autor e motivo

Os atos dos demais domínios (§5) entram como novos tipos quando o domain doc de cada um os declarar.
A Auditoria não inventa o nome do ato de outro domínio.

---

## 8. Riscos de Fronteira (Boundary Risks)

| Risco | Probabilidade | Impacto | Mitigação |
|---|---|---|---|
| A trilha virar log técnico genérico — "registra tudo, por via das dúvidas" — e o ato com consequência se perder no volume | Alta | Médio | Só entra tipo de ato declarado pelo domínio de origem (§7); log técnico fica em observabilidade (§1) |
| Um domínio de origem passar a consultar a trilha para decidir (ex.: "quem concedeu este papel?" para autorizar) | Média | Alto | RN-A10 · baseline regra 10; o estado vigente é sempre perguntado ao dono |
| Domínio de origem registrar o próprio ato "também", criando duas verdades | Média | Médio | Junta única em §5: a origem publica o fato, a Auditoria guarda o registro |
| Dado pessoal copiado na trilha tornar a anonimização de `CAP-031` impossível sem violar RN-A01 | Média | Alto | RN-A08: referência, não cópia. A tensão entre imutabilidade e anonimização é o primeiro tema da revisão D18 |
| Ato praticado fora do produto (acesso direto ao banco, seed, script) não deixar rastro | Média | Alto | Declarado, não resolvido aqui: RN-A12 torna a lacuna explícita; a mitigação é de plataforma (acesso operacional restrito) |

---

## 9. Questões em Aberto (Open Questions)

- [x] **QA-A01 — Ato recebido incompleto. Fechada em 2026-09-24:** registra e marca como não
      conforme (RN-A06). Descartado: recusar e mandar para fila de erro — o ato sumiria da trilha até
      o produtor ser corrigido.
- [x] **QA-A02 — Quem consulta a trilha. Fechada em 2026-09-24:** só o administrador (RN-A09).
      Descartado: administrador e financeiro — exigiria recorte por tipo de ato já no MVP.
- [ ] **QA-A03 — "Alterar papel" é um ato próprio?** Identidade RN-19 lista *alterar papel* como ato
      administrativo, mas o §7 daquele domain doc só publica `papel-concedido` e `papel-revogado`.
      Precisa ser fechada no PRD de `CAP-002`: ou alteração é revogar + conceder (dois registros),
      ou nasce `identidade.papel-alterado`. Dono: Identidade e Acesso. Não bloqueia o primeiro PRD
      de `CAP-030` se ele consumir os quatro fatos já declarados.
- [ ] **QA-A04 — Lacuna na lista do Domain Map.** O §16 do mapa enumera os domínios de origem de
      atos, mas omite Identidade e Acesso e Conteúdo e Currículo, que o backlog (`CAP-030`) e o domain
      doc de Identidade já tratam como fonte. A tabela de dependências cobre pela linha genérica
      *"Domínios com ato administrativo"*; a enumeração do §16 deve ser completada na próxima
      revisão do mapa. Dono: dono do Domain Map. Não bloqueia.
- [ ] **QA-A05 — Retenção da trilha.** Por quanto tempo o registro é mantido e o que acontece com
      ele depois. É parte de AB03 (negócio + Auditoria), fora da Fase 1; até lá, nada é apagado
      (RN-A01). Revisão D18.

---

## Histórico de Revisões

| Versão | Data | Autor | Alterações |
|---|---|---|---|
| 1.0 | 2026-09-24 | Tasso Gomes | Versão parcial (D3): registro append-only de atos administrativos e sua consulta. LGPD adiada para D18 |
| 1.1 | 2026-09-24 | Tasso Gomes | RN-A14: envelope único `auditoria.ato-praticado`, contrato da Auditoria; os quatro atos de Identidade passam a ser tipos dentro dele (§7). Decidido no discovery do PRD de `CAP-030` |

*Domain Doc gerado com a skill `tsg-flow-domain-creator`. Para criar o PRD de uma capacidade que
toca este domínio, use `tsg-flow-prd-creator` fornecendo o `vision.md`, este arquivo, os demais
domain docs que a capacidade atravessa e o ID da capacidade.*
