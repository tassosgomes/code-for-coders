---
tsg_artifact: domain
product: code-4-coders
version: 1.0
status: approved
updated: 2026-09-30
sources: vision.md@1.2, context/domain-map.md@1.2, backlog/capabilities.md@1.4, context/architecture-baseline.md@1.2, domains/conteudo-e-curriculo/domain.md@1.1, domains/identidade-e-acesso/domain.md@1.1, domains/auditoria-e-conformidade/domain.md@1.2
---

# Domain Document — Matrícula e Direito de Acesso

> Detalha o bounded context de **um** domínio do Domain Map. Não decide prioridade, ordem nem
> escopo de entrega — isso é do backlog de capacidades e do PRD. Forneça este arquivo junto com o
> `vision.md` ao iniciar um PRD de capacidade que toque este domínio.
>
> Detalhado **em par** com [Catálogo e Oferta](../catalogo-e-oferta/domain.md) (backlog,
> seção A, observação 1): aquele domínio **promete** a vigência, este a **concede**. As regras da
> costura (RN-D03 a RN-D05 aqui, RN-O08 a RN-O10 lá) foram escritas juntas e não mudam de um lado sem
> o outro.

**Domínio:** Matrícula e Direito de Acesso
**Capacidades atendidas:** `CAP-008`, `CAP-009`, `CAP-010` (dono) · consumido por `CAP-007`, `CAP-011`, `CAP-017`, `CAP-020`, `CAP-023`, `CAP-025`
**Restrições arquiteturais pertinentes:** DE01, DE05, DE08 (Domain Map) · BA04 (`Entitlement` com schema e contrato próprios) · BA07 e G11 (consulta síncrona ao dono, cache ≤ 30 s, falha fechada, sem réplica) · BA10/G07 (tenant) · G13 (auditoria só por evento) · G20 (direito ≠ liberação progressiva)

---

## 1. Propósito do Domínio (Domain Purpose)

### Responsabilidade Principal

Responder, a qualquer momento, se um aluno tem direito de acessar um curso — e até quando —
concedendo, mantendo, expirando, suspendendo e revogando esse direito a partir de fatos de origem.

### Problema que Resolve

O que a escola vende não é o vídeo: é o direito de assisti-lo por uma vigência. Se esse direito for
um campo do pedido ou da assinatura, cada nova forma de conceder (cortesia, turma, campanha que
estende o prazo, suspensão por inadimplência) vira caso especial espalhado pelo sistema, e duas
fontes passam a responder "este aluno pode?" de formas diferentes — o risco DE01, que a visão e o
baseline registraram em três lugares. Este domínio é a fonte única dessa resposta, e o nó mais
consultado do sistema.

### Fora do Escopo deste Domínio (Out of Scope)

- **Preço, vigência prometida, nível, pré-requisito** → Catálogo e Oferta. Este domínio recebe a
  vigência pronta; não a define nem a consulta (RN-D03).
- **Pedido, pagamento, cobrança, inadimplência, reembolso como fato financeiro** → Vendas e Checkout,
  Cobrança e Assinatura. Este domínio recebe o fato e decide o efeito sobre o acesso; nunca é
  Cobrança que bloqueia (restrição herdada de `CAP-009`).
- **Liberar a próxima aula, condicionar avanço a avaliação** → Aprendizagem e Progresso (DE05, G20).
  "Tem direito ao curso?" é daqui; "já pode ver a próxima aula?" não.
- **Autenticar o aluno e saber quem ele é** → Identidade e Acesso. Ser aluno não é ter acesso.
- **Entregar o vídeo, URL assinada, marca d'água** → Entrega de Mídia e Proteção, que pergunta a este
  domínio antes de liberar.
- **Registro imutável dos atos administrativos** → Auditoria e Conformidade. Este domínio pratica o
  ato e publica o fato.
- **Cronograma, encontro ao vivo, acompanhamento por professor de uma turma** → non-goal da versão
  atual (DE08). Turma aqui é só agrupamento de matrículas.

---

## 2. Usuários do Domínio (Domain Users)

| Perfil (Role) | O que faz neste domínio | Frequência de uso |
|---|---|---|
| Aluno | Não opera o domínio: tem ou não tem acesso. Vê, na própria conta, os cursos a que tem direito e até quando | Diária |
| Financeiro (ator interno) | Concede acesso de cortesia a um aluno, com vigência e motivo (RN-D11) | Eventual |
| Ator interno com permissão de revogação | Revoga acesso por decisão administrativa, com motivo (`CAP-009`) | Rara |
| Suporte | Diagnostica por que um aluno não tem acesso e solicita correção (Domain Map) | Diária |
| Vendas e Checkout (domínio) | Ordena a concessão após compra concluída, com a vigência congelada no pedido | A cada compra paga |
| Cobrança e Assinatura (domínio) | Informa inadimplência, regularização e reembolso | Por evento |
| Entrega de Mídia, Aprendizagem, Avaliação, Comunidade (domínios) | Perguntam "este aluno pode acessar este curso agora?" | A cada acesso |

---

## 3. Entidades Principais (Core Entities)

| Entidade | Descrição | Atributos Principais | Relacionamentos |
|---|---|---|---|
| Matrícula | O vínculo de um aluno com um curso, que agrupa todas as concessões dele sobre aquele curso | aluno, curso, momento da primeira concessão | agrupa: Concessões de Acesso · pode pertencer a: Turma |
| Concessão de Acesso | Um direito de um aluno sobre um curso, com origem e vigência | origem (compra, cortesia, assinatura, turma), referência da origem, vigência, situação (ativa / suspensa / revogada / expirada), momento da concessão | pertence a: Matrícula |
| Vigência | O período de validade de uma concessão: por período determinado ou vitalícia | início, término (ausente se vitalícia) | pertence a: Concessão |
| Suspensão | Interrupção temporária da concessão por causa financeira, reversível ao regularizar | causa, início, fim | afeta: Concessão |
| Revogação | Encerramento definitivo da concessão, por reembolso ou decisão administrativa | causa, autor, motivo, momento | encerra: Concessão |
| Turma (coorte) | Agrupamento de matrículas com data de início comum | oferta de origem, data de início | agrupa: Matrículas |

---

## 4. Capacidades Atendidas (Capabilities Served)

| Capacidade | O que este domínio entrega a ela |
|---|---|
| `CAP-008` | Concessão com origem e vigência (compra e cortesia), expiração automática e a decisão "pode acessar agora?" |
| `CAP-009` | Suspensão por inadimplência, restauração na regularização e revogação por reembolso ou decisão administrativa |
| `CAP-010` | Turma como agrupamento de matrículas sobre uma oferta |
| `CAP-007` | A decisão de acesso consultada antes de cada sessão de reprodução |
| `CAP-011` | A concessão ordenada pela compra concluída |
| `CAP-017`, `CAP-020`, `CAP-023`, `CAP-025` | A decisão de acesso (progresso, avaliação, comunidade) e o diagnóstico de acesso (suporte) |

---

## 5. Juntas com Outros Domínios (Domain Joints)

### Depende de (Upstream)

| Domínio | O que consome | Tipo | Dono do dado | Criticidade |
|---|---|---|---|---|
| Vendas e Checkout | Ordem de conceder acesso após compra concluída, **com a vigência congelada no pedido** | Evento | Vendas (pedido) · este domínio (concessão) | Alta |
| Catálogo e Oferta | A vigência prometida — **somente por meio da ordem de concessão**; nenhuma consulta direta (RN-D03) | Indireto (pelo pedido) | Catálogo (promessa) | Alta |
| Cobrança e Assinatura | Inadimplência, regularização e reembolso | Evento | Cobrança (fato financeiro) · este domínio (efeito no acesso) | Alta |
| Identidade e Acesso | Ator corrente e suas permissões; confirmação de que o beneficiário é conta de aluno | Síncrono (claim / consulta) | Identidade e Acesso | Alta |
| Conteúdo e Currículo | O curso a que a concessão se refere (identificador estável, com versão vigente) | Leitura | Conteúdo e Currículo | Média |

### Fornece para (Downstream)

| Domínio | O que fornece | Tipo | Dono do dado | Criticidade |
|---|---|---|---|---|
| Entrega de Mídia e Proteção | "Este aluno pode acessar este curso agora?" | Síncrono, cache ≤ 30 s, falha fechada (BA07) | Este domínio | **Alta** — caminho crítico da reprodução |
| Aprendizagem e Progresso, Avaliação, Comunidade | A mesma decisão | Síncrono (BA07) | Este domínio | Alta |
| Atendimento e Suporte | Diagnóstico das concessões de um aluno (origem, vigência, situação) | Síncrono (leitura) | Este domínio | Média |
| Auditoria e Conformidade | Atos de conceder cortesia e revogar por decisão administrativa | Evento (`auditoria.ato-praticado`) | Este domínio (conteúdo); Auditoria (forma) | Média |
| Notificação, Inteligência de Negócio | Fatos de acesso concedido, suspenso, restaurado, revogado e expirado | Evento | Este domínio | Baixa |

**Leitura da junta de vigência (DE01).** A linha do Domain Map "Matrícula conhece a vigência
prometida pela oferta, dono: Catálogo" é cumprida pelo pedido: Catálogo origina a promessa, Vendas a
congela, a ordem de concessão a entrega. Este domínio calcula o término a partir dela e nunca
pergunta ao Catálogo — senão uma oferta alterada entre a compra e a confirmação do pagamento mudaria
o que foi comprado. A redação da linha no Domain Map deve acompanhar na próxima revisão dele.

---

## 6. Regras de Negócio (Business Rules)

| ID | Regra | Origem |
|---|---|---|
| RN-D01 | **Este domínio é a fonte única da resposta "pode acessar agora?".** Nenhum outro domínio guarda réplica do direito; consumidores consultam o dono, com cache de até 30 s, e **falham fechados** se ele não responder | DE01 · BA07 · G11 |
| RN-D02 | **Toda concessão tem origem identificada e vigência.** Origem é compra, cortesia, assinatura ou turma, com referência ao fato que a gerou. Não existe concessão "avulsa" sem origem | Domain Map · `CAP-008` |
| RN-D03 | **A vigência de uma concessão por compra vem congelada na ordem de concessão.** Este domínio não consulta o Catálogo para conceder; oferta alterada ou despublicada depois da compra não muda a concessão | Decisão de 2026-09-30 · RN-O09 · DE01 |
| RN-D04 | **Vigência por período conta a partir do momento da concessão**, não da compra: boleto ou PIX pago dias depois não consome dias de acesso. A duração é em meses inteiros (RN-O08) e o término cai no mesmo dia do mês, N meses depois. Vitalícia não tem término | RN-O08 · CAP-011 (pedido pendente não concede) |
| RN-D05 | **Campanha que altera a vigência é honrada sem caso especial:** a vigência efetiva vem congelada na ordem como qualquer outra | RN-O16 · restrição herdada de `CAP-004` |
| RN-D06 | **A concessão expira sozinha no fim da vigência.** A decisão compara a vigência com o momento da pergunta; nenhuma ação humana ou rotina é necessária para que um acesso vencido deixe de valer | `CAP-008` |
| RN-D07 | **O aluno tem acesso ao curso se ao menos uma concessão dele sobre o curso está ativa e dentro da vigência.** Várias concessões convivem (ex.: cortesia e depois compra); uma não encerra nem estende a outra | `CAP-008` · DE01 |
| RN-D08 | **O direito é sobre o curso, não sobre uma versão.** Quem tem acesso vê a versão vigente, e nova publicação não cria, altera nem encerra concessão | RN-C08 · RN-C09 |
| RN-D09 | **Só conta de aluno recebe concessão.** Ator interno não recebe concessão; o acesso do professor ao que autora vem da permissão de autoria, não deste domínio | Identidade RN-13 |
| RN-D10 | **A mesma origem não gera duas concessões.** Ordem de concessão reentregue para o mesmo pedido não duplica acesso nem estende vigência | G09 · integridade |
| RN-D11 | **Cortesia é ato administrativo auditado**, com autor, aluno, curso, vigência declarada e **motivo obrigatório**. Não há pedido: a vigência é a que o autor declara, por período (em meses) ou vitalícia. A permissão de cortesia é concedida ao papel **financeiro**; o nome nasce no PRD | Restrição herdada de `CAP-008` · RN-A05 · decisão de 2026-09-30 (financeiro) |
| RN-D12 | **Suspensão só nasce de fato financeiro e é reversível:** a regularização restaura a concessão. Suspensão não é revogação e não apaga a concessão | Domain Map (Suspensão) · `CAP-009` |
| RN-D13 | **Revogação é definitiva**, por reembolso ou decisão administrativa. Revogação por decisão administrativa é ato auditado com autor e motivo obrigatório | Domain Map (Revogação) · restrição herdada de `CAP-009` |
| RN-D14 | **Quem bloqueia é este domínio, nunca Cobrança.** Cobrança informa o fato; o efeito sobre o acesso é decidido aqui e vale na próxima decisão (dentro do cache de 30 s) | Restrição herdada de `CAP-009` · BA07 |
| RN-D15 | **Direito de acesso não é liberação de aula.** Este domínio nunca decide se a próxima aula está liberada; PRD que misture os dois é recusado | DE05 · G20 |
| RN-D16 | **Turma é agrupamento de matrículas sobre uma oferta**, com data de início comum; não altera a regra de concessão | DE08 · `CAP-010` |
| RN-D17 | **Toda matrícula e concessão carrega a escola (tenant)**; a decisão nunca cruza tenant | BA10 · G07 |

---

## 7. Eventos do Domínio (Domain Events)

> O contrato real materializa no pacote de contratos e na TechSpec; aqui fica o fato de negócio.

### Produz (Publishes)

- `matricula.acesso-concedido` — uma concessão passou a valer (qualquer origem)
- `matricula.acesso-suspenso` — concessão suspensa por fato financeiro
- `matricula.acesso-restaurado` — suspensão encerrada pela regularização
- `matricula.acesso-revogado` — concessão encerrada definitivamente
- `matricula.acesso-expirado` — fato informativo de que a vigência terminou; a decisão já não
  dependia dele (RN-D06)
- Atos `cortesia-concedida` e `acesso-revogado` no envelope `auditoria.ato-praticado` — contrato
  do envelope é da Auditoria (RN-A14); o tipo do ato é vocabulário deste domínio

### Consome (Subscribes)

- Compra concluída (de: Vendas e Checkout) — ordena a concessão, com a vigência congelada. O nome do
  fato é de Vendas e nasce no seu domain doc
- Inadimplência, regularização e reembolso (de: Cobrança e Assinatura) — suspendem, restauram ou
  revogam. Nomes nascem no domain doc de Cobrança

---

## 8. Riscos de Fronteira (Boundary Risks)

| Risco | Probabilidade | Impacto | Mitigação |
|---|---|---|---|
| **Direito virar flag de pedido** — Vendas ou o player decidirem acesso pelo estado do pedido | Média | Alto | RN-D01; `CAP-008` antes de `CAP-011` (R2); G11 |
| **Vigência lida da oferta atual** em vez da ordem congelada | Média | Alto | RN-D03, espelhada em Catálogo RN-O09 |
| **Réplica local do direito** em mídia ou progresso "por performance" | Média | Alto | RN-D01 · G11 · cache ≤ 30 s como aceleração, nunca como fonte |
| **Confundir direito com liberação progressiva** | Média | Alto | RN-D15 · G20 |
| **Suspensão implementada como revogação** e a regularização não devolver o acesso | Baixa | Alto | RN-D12 separa as duas entidades |

---

## 9. Questões em Aberto (Open Questions)

- [x] **QD-01 — Quem concede cortesia. Fechada em 2026-09-30: financeiro** (RN-D11). Ainda não há
      operação comercial separada; se um papel comercial surgir, a permissão migra por decisão de
      Identidade, sem mudar a regra.
- [ ] **QD-02 — Suspensão pausa a contagem da vigência?** Se um aluno com 12 meses fica 30 dias
      suspenso, recupera os 30 dias? → PRD de `CAP-009` (Fase 2).
- [ ] **QD-03 — Concessão por assinatura** (vigência atrelada ao ciclo de cobrança). → PRD de
      `CAP-013`/`CAP-014` (Fase 2).
- [ ] **QD-04 — Término de vigência por período: data exata ou fim do dia** no fuso da escola, e o
      caso de mês sem o dia (concessão em 31/01 + 1 mês). → PRD de `CAP-008`.

---

## Histórico

| Versão | Data | Autor | Mudança |
|---|---|---|---|
| 1.0 | 2026-09-30 | Tasso Gomes | Criação, em par com Catálogo e Oferta: concessão com origem e vigência, junta de vigência pelo pedido, RN-D01 a RN-D17. Decisão de 2026-09-30: vigência congelada na ordem de concessão. Aprovado em 2026-09-30 |

---

*Domain Doc gerado com a skill `tsg-flow-domain-creator`. Para criar o PRD de uma capacidade que
toca este domínio, use `tsg-flow-prd-creator` fornecendo o `vision.md`, este arquivo, os demais
domain docs que a capacidade atravessa e o ID da capacidade.*
