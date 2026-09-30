---
tsg_artifact: domain
product: code-4-coders
version: 1.1
status: approved
updated: 2026-09-30
sources: vision.md@1.2, context/domain-map.md@1.2, backlog/capabilities.md@1.4, context/architecture-baseline.md@1.2, domains/conteudo-e-curriculo/domain.md@1.1, domains/identidade-e-acesso/domain.md@1.1, domains/auditoria-e-conformidade/domain.md@1.2
---

# Domain Document — Catálogo e Oferta

> Detalha o bounded context de **um** domínio do Domain Map. Não decide prioridade, ordem nem
> escopo de entrega — isso é do backlog de capacidades e do PRD. Forneça este arquivo junto com o
> `vision.md` ao iniciar um PRD de capacidade que toque este domínio.
>
> Detalhado **em par** com [Matrícula e Direito de Acesso](../matricula-e-direito-de-acesso/domain.md)
> (backlog, seção A, observação 1): este domínio **promete** a vigência, aquele a **concede**. As
> regras da costura entre os dois (RN-O08 a RN-O10 aqui, RN-D03 a RN-D05 lá) foram escritas juntas
> e não mudam de um lado sem o outro.

**Domínio:** Catálogo e Oferta
**Capacidades atendidas:** `CAP-003`, `CAP-004` (dono) · consumido por `CAP-008`, `CAP-010`, `CAP-011`
**Restrições arquiteturais pertinentes:** DE01, DE02, DE04 (Domain Map) · BA10/G07 (tenant em toda entidade) · BA15 e R8 (sem promessa de exclusividade) · G08 (um salto síncrono) · G13 (auditoria só por evento) · RN-18 de Identidade (permissão como claim)

---

## 1. Propósito do Domínio (Domain Purpose)

### Responsabilidade Principal

Definir o que está à venda — sobre qual curso, por qual preço e com qual direito de acesso
prometido — e expor isso ao público na vitrine, exibindo o nível e o pré-requisito que o curso declara.

### Problema que Resolve

O mesmo curso é vendido de formas diferentes ao longo do tempo (12 meses, vitalício, campanha,
assinatura) sem que uma vírgula do currículo mude, e preço e currículo têm donos e ritmos
diferentes — negócio e professor (DE02). Sem um dono próprio, cada forma de venda vira cópia do
curso ou campo no formulário de autoria, e a promessa feita ao comprador ("acesso por 12 meses")
fica espalhada entre tela, pedido e concessão — que é exatamente o risco DE01. Como a plataforma
decidiu **não travar** compra por pré-requisito (DE04), é na vitrine que a escolha é orientada:
orientar mal custa reembolso e churn (R12).

### Fora do Escopo deste Domínio (Out of Scope)

- **Estrutura do curso, módulos, aulas, publicação do currículo** → Conteúdo e Currículo. Este
  domínio referencia o curso publicado; não copia nem edita o currículo (RN-C14, RN-C17).
- **Conceder, manter, expirar, suspender ou revogar acesso** → Matrícula e Direito de Acesso. Este
  domínio promete a vigência; não a concede e não responde "este aluno pode?".
- **Pedido, pagamento, congelamento da condição comprada, aplicação de cupom no checkout** → Vendas e
  Checkout. Este domínio entrega a oferta vigente no momento da compra; o pedido guarda a cópia
  congelada (Domain Map, Pedido).
- **Cobrança, recorrência, parcelamento, reembolso** → Cobrança e Assinatura.
- **Declarar nível e pré-requisito** → Conteúdo e Currículo (RN-C18). São informação pedagógica, do
  professor; este domínio os exibe e filtra a partir da versão vigente, e não os edita.
- **Condicionar compra ou acesso a pré-requisito ou nível** → ninguém. É non-goal da visão (DE04).
- **Liberação progressiva de aula** → Aprendizagem e Progresso (DE05, G20).
- **Identidade visual, marca e design system** → time de design (OD3 fechada: `DESIGN.md` e o Figma
  do design system valem para a área pública).
- **Papel e permissão do ator interno** → Identidade e Acesso. Este domínio exige a permissão; quem
  a recebe é decisão de Identidade (RN-12).

---

## 2. Usuários do Domínio (Domain Users)

| Perfil (Role) | O que faz neste domínio | Frequência de uso |
|---|---|---|
| Financeiro (ator interno) | Cria, precifica, publica, altera e despublica ofertas; depois, campanhas e cupons | Semanal |
| Visitante (anônimo) | Navega na vitrine, filtra por nível, lê pré-requisito, preço e vigência prometida, e manifesta intenção de compra | Diária |
| Aluno (autenticado) | O mesmo que o visitante; a vitrine não depende de estar autenticado | Diária |
| Vendas e Checkout (domínio) | Lê a oferta vigente no momento da compra, para congelar no pedido | A cada compra |
| Matrícula e Direito de Acesso (domínio) | Recebe a vigência prometida **por meio da ordem de concessão**, nunca por consulta a este domínio (RN-O09) | A cada concessão |
| Professor | Não usa o domínio. Não vê nem define preço nem vigência (RN-16 de Identidade). Declara nível e pré-requisito no curso, em Conteúdo (RN-C18), e a vitrine os exibe | — |

---

## 3. Entidades Principais (Core Entities)

| Entidade | Descrição | Atributos Principais | Relacionamentos |
|---|---|---|---|
| Ficha de Vitrine | A apresentação comercial de **um curso** no catálogo: o que é comum a todas as ofertas daquele curso | curso referenciado, chamada comercial (opcional) | referencia: Curso (Conteúdo) · agrupa: Ofertas |
| Oferta | Uma forma de vender o curso: preço e vigência prometida | preço, vigência prometida, situação (rascunho / publicada / despublicada), nome da opção | pertence a: Ficha de Vitrine · lida por: Vendas |
| Preço | Valor cobrado pela oferta, em reais | valor em centavos, moeda (BRL) | pertence a: Oferta |
| Vigência Prometida | O direito de acesso que a oferta promete ao comprador | tipo (período / vitalícia), duração em meses quando por período | pertence a: Oferta · congelada em: Pedido (Vendas) |
| Vitrine | O conjunto público de fichas com ao menos uma oferta publicada | derivado | agrupa: Fichas de Vitrine |
| Campanha de Compra | Condição temporária que altera preço e/ou vigência de uma oferta | oferta, preço e/ou vigência alternativos, início, fim | altera: Oferta (sem editá-la) |
| Cupom | Desconto aplicável na compra, por percentual ou valor fixo | código, tipo e valor do desconto, validade, ofertas elegíveis | aplicado em: Pedido (Vendas) |

**Por que Ficha de Vitrine existe.** Com mais de uma oferta publicada por curso (RN-O04), a vitrine
lista o curso uma vez e apresenta as opções; a Ficha é esse ponto comum. Nível e pré-requisito
**não** estão nela: são do curso (Conteúdo, RN-C18) e valem para todas as ofertas, de modo que o
mesmo curso nunca aparece como "iniciante" numa opção e "avançado" na outra.

---

## 4. Capacidades Atendidas (Capabilities Served)

| Capacidade | O que este domínio entrega a ela |
|---|---|
| `CAP-003` | Ficha, oferta com preço e vigência prometida, publicação e despublicação, vitrine pública que exibe e filtra pelo nível e exibe o pré-requisito do curso |
| `CAP-004` | Campanha de compra e cupom: definição, validade e condição efetiva calculada no momento da compra |
| `CAP-008` | A vigência prometida que a concessão honra — entregue por meio da ordem de concessão (RN-O09) |
| `CAP-010` | A oferta sobre a qual uma turma é criada |
| `CAP-011` | A oferta vigente e sua condição efetiva no momento da compra, para congelar no pedido |

---

## 5. Juntas com Outros Domínios (Domain Joints)

### Depende de (Upstream)

| Domínio | O que consome | Tipo | Dono do dado | Criticidade |
|---|---|---|---|---|
| Conteúdo e Currículo | Existência de versão vigente do curso, título, estrutura publicada, nível e pré-requisito para exibir e filtrar na vitrine | Evento (`conteudo.versao-publicada`) + leitura | Conteúdo e Currículo | Alta |
| Identidade e Acesso | Ator corrente e sua permissão de gestão de oferta | Síncrono (claim) | Identidade e Acesso | Alta |

### Fornece para (Downstream)

| Domínio | O que fornece | Tipo | Dono do dado | Criticidade |
|---|---|---|---|---|
| Vendas e Checkout | Oferta vigente e condição efetiva (preço, vigência, campanha, cupom) no momento da compra | Síncrono (leitura) | Este domínio; a cópia congelada no pedido é de Vendas | Alta |
| Matrícula e Direito de Acesso | A vigência prometida, **via ordem de concessão** de Vendas | Indireto (pelo pedido) | Este domínio origina; o pedido congela; Matrícula calcula o término | Alta |
| Auditoria e Conformidade | Atos de publicar, despublicar e alterar preço ou vigência de oferta publicada | Evento (`auditoria.ato-praticado`) | Este domínio (conteúdo); Auditoria (forma e registro) | Média |
| Inteligência de Negócio | Fatos de oferta publicada, alterada e despublicada | Evento | Este domínio | Baixa |

**Leitura da junta de vigência (DE01).** O Domain Map declara "Matrícula conhece a vigência
prometida pela oferta, dono: Catálogo". Esta passada fixa **como**: a promessa nasce aqui, Vendas a
congela no pedido junto com o preço, e a ordem de concessão a entrega pronta a Matrícula. Matrícula
não lê o Catálogo para conceder. O dono da promessa continua sendo este domínio; o que muda é o
caminho, e é ele que impede uma oferta alterada entre a compra e a confirmação do pagamento (boleto,
PIX) de mudar o que foi prometido ao comprador. A redação da linha no Domain Map deve acompanhar na
próxima revisão dele.

---

## 6. Regras de Negócio (Business Rules)

| ID | Regra | Origem |
|---|---|---|
| RN-O01 | **Oferta só se monta sobre curso com versão vigente e da mesma escola.** Curso nunca publicado não tem Ficha de Vitrine nem oferta | RN-C17 · G07 |
| RN-O02 | **Este domínio não guarda nem edita currículo.** A vitrine mostra título e estrutura da versão vigente de Conteúdo; nova publicação do curso aparece na vitrine sem ação do Catálogo | RN-C14 · DE02 |
| RN-O03 | **Nível e pré-requisito vêm do curso, na versão vigente** (Conteúdo, RN-C18), e valem para todas as ofertas dele. Este domínio os exibe e filtra; não os guarda como seus nem os edita. **Só curso cuja versão vigente declara nível pode ter oferta publicada**; pré-requisito é opcional | Decisão de 2026-09-30 (OD54) · Visão (persona Professor, H4) · DE04 |
| RN-O04 | **Um curso pode ter várias ofertas publicadas ao mesmo tempo** (ex.: 12 meses e vitalício). A vitrine lista o curso uma vez e apresenta as opções | Decisão de 2026-09-30 · Domain Map (justificativa) |
| RN-O05 | **Nível e pré-requisito nunca condicionam compra, matrícula nem acesso.** Nenhuma regra, tela ou fluxo impede comprar por nível ou pré-requisito; a plataforma informa, o aluno escolhe | DE04 · non-goal da visão |
| RN-O06 | **Nenhuma oferta promete exclusividade de conteúdo nem proteção absoluta.** Texto comercial, nome da opção e vigência não afirmam que o conteúdo não pode ser copiado | BA15 · R8 |
| RN-O07 | **Toda oferta tem preço maior que zero, em reais.** Acesso gratuito não é oferta: é cortesia, e cortesia é concessão de Matrícula | Visão (venda avulsa) · CAP-008 |
| RN-O08 | **Toda oferta promete uma vigência: por período determinado ou vitalícia.** Por período, a duração é declarada na oferta **em meses inteiros** e conta a partir da concessão, não da compra (RN-D04). Vitalícia não tem término, mas continua sujeita a suspensão e revogação | Visão (§5, direito de acesso) · DE01 · decisão de 2026-09-30 (meses) |
| RN-O09 | **A vigência prometida chega a Matrícula congelada no pedido, pela ordem de concessão.** Matrícula nunca consulta o Catálogo para conceder; alterar ou despublicar uma oferta não muda o que já foi comprado, nem o que foi comprado e ainda aguarda pagamento | Decisão de 2026-09-30 · Domain Map (Pedido congela a condição) · DE01 |
| RN-O10 | **Alterar preço ou vigência de oferta publicada vale só para compras futuras.** Pedidos e concessões existentes não mudam. Não há "correção retroativa" de promessa | RN-O09 |
| RN-O11 | **Oferta publicada nunca é apagada**, só despublicada: pedidos a referenciam. Oferta em rascunho pode ser excluída | Rastreabilidade do pedido |
| RN-O12 | **Despublicar tira a oferta da vitrine e impede compra nova.** Não cancela pedido já criado, não revoga concessão, não altera vigência de ninguém. Curso sem nenhuma oferta publicada sai da vitrine | RN-O09 · DE01 |
| RN-O13 | **A vitrine é pública e igual para todos.** Visitante anônimo vê preço, vigência prometida, nível, pré-requisito e a estrutura publicada; nenhuma informação da vitrine depende de estar autenticado | Visão (persona Visitante) |
| RN-O14 | **Só ator interno com a permissão de gestão de oferta cria, altera, publica ou despublica.** A permissão é concedida ao papel **financeiro** (decisão de 2026-09-30); o nome da permissão nasce no PRD. Professor não tem a permissão | Identidade RN-12, RN-16, RN-18 · DP-03 de `CAP-002` |
| RN-O15 | **Publicar, despublicar e alterar preço ou vigência de oferta publicada são atos administrativos auditados**, comunicados no envelope `auditoria.ato-praticado` com autor e alvo (a oferta); motivo não é obrigatório. Edição de rascunho não é auditada | Decisão de 2026-09-30 · RN-A14 · paralelo com RN-C13 |
| RN-O16 | **Campanha altera preço e/ou vigência de uma oferta por um intervalo, sem editar a oferta.** Fora do intervalo, vale a oferta. A condição efetiva é a que vale no momento da compra, e é ela que o pedido congela — inclusive a vigência, que `CAP-008` honra sem caso especial | `CAP-004` · DE01 |
| RN-O17 | **Cupom é definido aqui e aplicado em Vendas.** Este domínio diz se o cupom existe, vale e é elegível para a oferta; o desconto efetivo fica congelado no pedido | Domain Map (Vendas × Catálogo) · `CAP-004` |
| RN-O18 | **Toda Ficha, oferta, campanha e cupom carrega a escola (tenant)**, e a vitrine nunca mostra oferta de outra escola | BA10 · G07 |

---

## 7. Eventos do Domínio (Domain Events)

> O contrato real materializa no pacote de contratos e na TechSpec; aqui fica o fato de negócio.

### Produz (Publishes)

- `catalogo.oferta-publicada` — uma oferta passou a estar à venda. Assinantes: Inteligência de Negócio
- `catalogo.oferta-alterada` — preço ou vigência de oferta publicada mudou, para compras futuras
- `catalogo.oferta-despublicada` — a oferta deixou de estar à venda
- Atos `oferta-publicada`, `oferta-despublicada` e `oferta-alterada` no envelope
  `auditoria.ato-praticado` — o contrato do envelope é da Auditoria (RN-A14); o tipo do ato é
  vocabulário deste domínio, e entra na Auditoria como tipo novo, aditivo (como C-01 de `CAP-005`)

### Consome (Subscribes)

- `conteudo.versao-publicada` (de: Conteúdo e Currículo) — o curso passa a ser referenciável
  (RN-O01) e a vitrine reflete a estrutura vigente (RN-O02)

---

## 8. Riscos de Fronteira (Boundary Risks)

| Risco | Probabilidade | Impacto | Mitigação |
|---|---|---|---|
| **Vigência virar dado lido "ao vivo"** — Matrícula ou Vendas consultarem a oferta atual na hora de conceder, e uma alteração mudar o que foi comprado | Média | Alto | RN-O09 e RN-O10; junta de vigência declarada em §5 e espelhada em Matrícula (RN-D03) |
| **Curso virar oferta** — preço ou vigência migrarem para o formulário de autoria | Média | Alto | RN-O02; Conteúdo RN-C14 do outro lado |
| **Nível ou pré-requisito voltarem a ser editados aqui** "porque a vitrine precisa" | Média | Médio | RN-O03: o dono é Conteúdo; a vitrine lê a versão vigente |
| **Pré-requisito virar gate** por pressão de reembolso | Média | Alto | RN-O05 é non-goal da visão; resposta legítima ao reembolso é `CAP-015`, não bloqueio (R12) |
| **Promessa de proteção em texto comercial** | Média | Alto | RN-O06; revisão de PRD e de conteúdo (R8) |
| **Campanha implementada como edição temporária da oferta** — e o pedido congelar a condição errada | Média | Médio | RN-O16: campanha é entidade própria sobre a oferta |

---

## 9. Questões em Aberto (Open Questions)

- [x] **QO-01 — Unidade da duração por período. Fechada em 2026-09-30: meses inteiros** (RN-O08).
      `CAP-008` calcula o término na mesma unidade (RN-D04).
- [x] **QO-02 — O que "intenção de compra" registra antes de `CAP-011` existir. Fechada em
      2026-09-30 (OD53):** o botão de compra leva a um aviso de compra disponível em breve e conta o
      clique por oferta, sem identificar a pessoa. Nenhum registro de intenção pessoal nasce aqui.
- [x] **QO-03 — Formato do pré-requisito.** Saiu deste domínio com a revisão 1.1: é decisão de
      Conteúdo (RN-C18), no PRD que acrescenta nível e pré-requisito ao curso.
- [ ] **QO-04 — Regras de campanha e cupom** (acúmulo de cupom com campanha, limite de uso, valor
      mínimo). → PRD de `CAP-004`.

---

## Histórico

| Versão | Data | Autor | Mudança |
|---|---|---|---|
| 1.1 | 2026-09-30 | Tasso Gomes | Nível e pré-requisito saem da Ficha de Vitrine e passam a ser do curso (Conteúdo RN-C18); RN-O03 reescrita: a vitrine exibe e filtra a partir da versão vigente, e só curso com nível declarado tem oferta publicada. QO-02 fechada (OD53). Decisão OD54, aprovada em 2026-09-30 |
| 1.0 | 2026-09-30 | Tasso Gomes | Criação, em par com Matrícula e Direito de Acesso: Ficha de Vitrine, oferta com vigência prometida, junta de vigência pelo pedido, RN-O01 a RN-O18. Decisões de 2026-09-30: financeiro gere oferta, várias ofertas por curso, vigência congelada na ordem, atos de oferta auditados. Aprovado em 2026-09-30 |

---

*Domain Doc gerado com a skill `tsg-flow-domain-creator`. Para criar o PRD de uma capacidade que
toca este domínio, use `tsg-flow-prd-creator` fornecendo o `vision.md`, este arquivo, os demais
domain docs que a capacidade atravessa e o ID da capacidade.*
