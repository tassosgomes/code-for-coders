---
tsg_artifact: prd
product: code-4-coders
capability: CAP-011
version: 1.0
status: approved
updated: 2026-10-05
sources: backlog/capabilities.md@1.4, vision.md@1.2, context/domain-map.md@1.2, context/architecture-baseline.md@1.2, domains/catalogo-e-oferta/domain.md@1.1, domains/matricula-e-direito-de-acesso/domain.md@1.0, domains/identidade-e-acesso/domain.md@1.1
---

# Compra avulsa de curso

## Visão Geral

Hoje a escola mostra a oferta na vitrine (`CAP-003`), sabe conceder e expirar acesso (`CAP-008`) e
entrega a aula com proteção e progresso (`CAP-007`, `CAP-017`). Mas não vende: o botão **Comprar**
abre um aviso de "em breve" e conta o clique. Todo acesso existente nasceu de cortesia.

Esta entrega fecha o ciclo do MVP. O aluno escolhe uma opção de compra, paga por cartão, PIX ou
boleto numa página de pagamento do gateway (Stripe), e o acesso ao curso é liberado sozinho quando
o pagamento se confirma — sem ninguém da escola intervir. O pedido guarda o que foi comprado, pelo
preço e pela vigência prometidos no momento da compra, mesmo que a oferta mude depois. O aluno
recebe o comprovante por e-mail, acompanha os próprios pedidos e retoma um PIX ou boleto pendente;
o financeiro consulta os pedidos no backoffice.

É a primeira receita da escola e o passo que prova, de ponta a ponta, que a plataforma própria
substitui o intermediário (critério do passo 11 do backlog: *uma compra real libera o acesso sem
intervenção manual*).

---

## Rastreabilidade

### Capacidade e fronteiras

- **Capacidade:** `CAP-011` — Compra avulsa de curso.
- **Escopo desta entrega:** `CAP-011` inteira no recorte do MVP (OD78): compra de **uma oferta
  publicada por um aluno autenticado**, paga à vista por cartão, PIX ou boleto em checkout hospedado
  do gateway; pedido com preço e vigência congelados; confirmação do pagamento que conclui o pedido e
  ordena a concessão; comprovante por e-mail; *Meus pedidos* do aluno com retomada de pagamento
  pendente; consulta de pedidos pelo financeiro; aviso de acesso já existente antes de comprar.
- **Fora desta entrega:**
  - cupom e campanha de compra → `CAP-004` (Fase 2);
  - assinatura e cobrança recorrente → `CAP-013` (Fase 2);
  - reembolso e chargeback como fato do sistema, com revogação do acesso → `CAP-015` e `CAP-009`
    (Fase 2). Até lá, reembolso feito no painel do gateway **não** revoga acesso (DP-07);
  - emissão de NFS-e → `CAP-016` (primeira da Fase 2);
  - parcelamento no cartão; compra de mais de um curso no mesmo pedido (carrinho); compra para
    terceiro (presente); compra por visitante sem conta;
  - atribuição de afiliado → `CAP-012` (Fase 5).
- **Domínios atravessados:**
  - **Vendas e Checkout** — dono desta capacidade. Sem domain doc: é o único PRD do domínio no
    horizonte visível (`CAP-012` é Fase 5). As regras do pedido nascem aqui (RN-V).
  - **Cobrança e Assinatura** — em fatia mínima: recebimento único à vista, sem recorrência. Sem
    domain doc ainda: o próximo PRD do domínio é `CAP-013`, fora da rodada seguinte. As regras do
    pagamento avulso nascem aqui (RN-CB) e serão absorvidas pelo domain doc quando ele existir.
  - **Catálogo e Oferta** — [domains/catalogo-e-oferta/domain.md](../../domains/catalogo-e-oferta/domain.md).
  - **Matrícula e Direito de Acesso** — [domains/matricula-e-direito-de-acesso/domain.md](../../domains/matricula-e-direito-de-acesso/domain.md).
  - **Identidade e Acesso** — [domains/identidade-e-acesso/domain.md](../../domains/identidade-e-acesso/domain.md)
    (aluno autenticado; permissão do financeiro).
  - **Notificação** — consumida no envio do comprovante (contrato vigente de `CAP-026`).
- **Junta entre os domínios** (Domain Map §6 e §7; domain docs de Catálogo e Matrícula):
  - *Catálogo → Vendas.* Vendas lê a oferta vigente no momento da compra e **congela** preço e
    vigência prometida no pedido. A oferta continua sendo de Catálogo; a cópia congelada é de Vendas
    (RN-O09, RN-O10).
  - *Vendas → Cobrança.* Vendas pede o recebimento do valor do pedido; Cobrança conduz o pagamento
    no gateway, atrás de camada anticorrupção, e informa pagamento confirmado ou não confirmado.
    O pedido não conhece o gateway; o pagamento não conhece a oferta.
  - *Vendas → Matrícula.* Com o pagamento confirmado, Vendas conclui o pedido e publica a **compra
    concluída**, que é a ordem de concessão com a vigência congelada (RN-D03). Matrícula concede;
    Vendas nunca decide acesso (RN-D01). Matrícula consome a compra concluída de Vendas, não o
    pagamento de Cobrança: é o pedido que carrega a vigência.
  - *Vendas → Notificação.* O pedido pago dispara o pedido de envio do comprovante, com finalidade
    transacional.
- **Dependências entre capacidades:** `CAP-003`, `CAP-001`, `CAP-008`, `CAP-026` (todas `done`);
  externa: gateway contratado — Stripe (OD5).
- **Restrições do baseline:**
  - dado de cartão não trafega nem é persistido pela plataforma (escopo PCI mínimo);
  - toda dependência externa tem camada anticorrupção; o webhook do gateway entra por ponto
    dedicado, validado por assinatura e idempotente;
  - toda escrita disparada por cliente externo, retry ou mensagem é idempotente (G09);
  - decisão de acesso só em Matrícula, consulta síncrona com cache ≤ 30 s e falha fechada (BA07,
    G11);
  - tudo carrega a escola (tenant) (BA10, G07);
  - e-mail do aluno mascarado fora das exceções declaradas (G10, G23, RN-21).

### Vision Doc

- **Objetivos de negócio atendidos:** `C08` (venda avulsa), único modelo de monetização obrigatório
  no MVP; H3 (a venda avulsa basta para validar a Fase 1); critério de conclusão da Fase 1.
- **Restrições globais aplicáveis:** PIX e boleto (F7); LGPD; gateway com cartão, PIX e boleto.
- **Non-Goals globais respeitados:** pré-requisito e nível não condicionam compra (DE04, RN-O05);
  nenhuma comunicação promete proteção absoluta do conteúdo (R8, RN-O06).

### Domain Docs

- **Entidades envolvidas:**
  - Vendas e Checkout: **Pedido**, Item do Pedido (um por pedido nesta entrega).
  - Cobrança e Assinatura: **Pagamento**.
  - Catálogo e Oferta: Oferta, Preço, Vigência Prometida, Ficha de Vitrine.
  - Matrícula e Direito de Acesso: Concessão de Acesso (origem *compra*), Vigência, Matrícula.
  - Identidade e Acesso: Conta (aluno), Papel e Permissão (financeiro).
- **Regras de negócio referenciadas:**
  - Catálogo: RN-O05, RN-O07, RN-O09, RN-O10, RN-O11, RN-O12, RN-O18.
  - Matrícula: RN-D01, RN-D02, RN-D03, RN-D04, RN-D07, RN-D10, RN-D17.
  - Identidade: RN-02, RN-13, RN-16, RN-21, RN-26, RN-27, RN-28.
- **Regras nascidas neste PRD:** RN-V01 a RN-V12 (Vendas e Checkout) e RN-CB01 a RN-CB06
  (Cobrança e Assinatura), na seção *Regras de negócio desta entrega*.
- **Eventos consumidos:** pagamento confirmado e pagamento não confirmado (de Cobrança, interno a
  esta entrega); fatos de oferta despublicada e alterada (de Catálogo, já publicados).
- **Eventos produzidos:** **compra concluída** (Vendas → Matrícula, ordem de concessão); pedido de
  envio do comprovante (Vendas → Notificação); fatos de pedido criado e pedido expirado, para
  Inteligência de Negócio.

## Termos Canônicos

| Termo | Definição de negócio | Escopo/Fonte |
|---|---|---|
| Pedido | Registro da compra de uma oferta por um aluno, com preço e vigência prometida congelados no momento em que foi criado. É o contrato do que foi comprado | Domain Map (Vendas) |
| Pagamento | A tentativa de receber o valor de um pedido no gateway, pelo meio escolhido pelo aluno | Domain Map (Cobrança) · esta entrega |
| Compra concluída | O fato de um pedido ter sido pago. É a ordem de concessão que Matrícula consome | Matrícula §7 · esta entrega |
| Pagamento pendente | PIX ou boleto gerado e ainda não pago. O pedido existe, mas não concede acesso | `CAP-011` (restrição herdada) |
| Situação do pedido | *Aguardando pagamento*, *Pago*, *Expirado* ou *Cancelado* (RN-V05) | Esta entrega |
| Comprovante de compra | E-mail enviado ao aluno quando o pedido é pago | Esta entrega · R5 do backlog |
| Página de pagamento | A página do gateway onde o aluno escolhe o meio e paga. A plataforma não a desenha nem recebe os dados digitados nela | DP-01 |

---

## Objetivos

- **Fechar o critério do MVP:** uma compra real, feita por um aluno sem ajuda, libera o acesso ao
  curso sem intervenção da escola.
- **Acesso rápido depois do pagamento:** pagamento confirmado vira acesso em segundos, não em horas.
- **Nunca cobrar sem entregar, nunca entregar sem cobrar:** todo pedido pago tem concessão; nenhuma
  concessão por compra existe sem pedido pago; nenhuma confirmação repetida duplica acesso,
  comprovante ou vigência.
- **Promessa honrada:** o aluno recebe exatamente o preço e a vigência que viu ao clicar em
  *Comprar*, mesmo que a oferta mude antes de o PIX ou o boleto ser pago.
- **Operação sem planilha:** o financeiro responde "este aluno pagou?" pelo backoffice.

---

## Histórias de Usuário

- Como **aluno**, quero comprar um curso pagando por cartão, PIX ou boleto, para começar a estudar
  sem depender de ninguém da escola.
- Como **aluno**, quero ver o acesso liberado logo depois de pagar, para não ficar em dúvida se a
  compra deu certo.
- Como **aluno que gerou um boleto ou PIX**, quero voltar depois e encontrar o código para pagar,
  ou desistir e pagar de outro jeito, para não perder a compra nem pagar duas vezes.
- Como **aluno**, quero receber um comprovante por e-mail, para ter registro do que comprei, por
  quanto e por quanto tempo.
- Como **aluno**, quero ver meus pedidos e a situação de cada um, para saber o que já paguei e o que
  ainda falta pagar.
- Como **aluno que já tem acesso a um curso** (por cortesia, por exemplo), quero ser avisado antes de
  comprar, para decidir se a compra faz sentido.
- Como **visitante**, quero que, ao clicar em *Comprar*, eu seja levado a entrar ou criar conta e
  depois volte direto para a compra que escolhi.
- Como **financeiro**, quero consultar os pedidos por situação, período, curso e aluno, para
  responder dúvidas de pagamento e acompanhar as vendas sem abrir o painel do gateway para cada caso.

---

## Regras de negócio desta entrega

Vendas e Checkout e Cobrança e Assinatura não têm domain doc; as regras abaixo nascem aqui e serão
absorvidas pelos domain docs quando eles existirem.

| ID | Regra | Origem |
|---|---|---|
| RN-V01 | **Só conta de aluno autenticada compra.** Visitante é levado a entrar ou criar conta e volta para a mesma opção de compra. Ator interno não compra | RN-02 · RN-13 · RN-D09 |
| RN-V02 | **Só oferta publicada pode ser comprada.** Oferta despublicada, em rascunho ou de outra escola não gera pedido | RN-O12 · RN-O18 |
| RN-V03 | **O pedido congela, no momento em que é criado, o preço e a vigência prometida da oferta** — o que o aluno viu ao confirmar. Alteração ou despublicação posterior da oferta não muda pedido existente, pago ou não | RN-O09 · RN-O10 · RN-O12 |
| RN-V04 | **Um pedido tem exatamente um item: uma oferta de um curso**, comprada à vista, em reais | OD78 |
| RN-V05 | **Situações do pedido:** *Aguardando pagamento* (criado, ainda sem pagamento confirmado), *Pago* (pagamento confirmado; terminal), *Expirado* (prazo de pagamento vencido sem confirmação) e *Cancelado* (o aluno desistiu antes de pagar). Só *Pago* gera concessão | `CAP-011` · Domain Map (Vendas) |
| RN-V06 | **No máximo um pedido aguardando pagamento por aluno e oferta.** Novo clique em *Comprar* na mesma oferta leva ao pedido pendente, com o preço e a vigência dele, em vez de criar outro | Evita pagamento em duplicidade |
| RN-V07 | **O aluno pode desistir de um pedido aguardando pagamento**, o que o torna *Cancelado* e pede ao gateway que cancele o pagamento pendente. A partir daí ele pode criar um pedido novo — pelas condições vigentes da oferta naquele momento, não as do pedido cancelado | DP-04 |
| RN-V08 | **Pagamento confirmado sempre conclui o pedido**, mesmo que ele esteja *Expirado* ou *Cancelado* no momento da confirmação (boleto compensado depois do vencimento, cancelamento que o gateway não conseguiu efetivar). Dinheiro recebido é venda realizada: o pedido vira *Pago* e concede, com a vigência que ele congelou | DP-05 |
| RN-V09 | **Pedido pago publica a compra concluída uma única vez**, com aluno, curso, pedido e a vigência congelada. Confirmação reentregue não gera segunda compra concluída, segunda concessão nem segundo comprovante | RN-D10 · G09 |
| RN-V10 | **O pedido nunca decide acesso.** A situação *Pago* não é consultada por player, aula nem progresso; o acesso vem da concessão em Matrícula | RN-D01 · DE01 · R2 |
| RN-V11 | **Comprar não depende de nível, pré-requisito nem de acesso já existente.** O aviso de acesso existente informa; não impede | RN-O05 · RN-D07 · DE04 |
| RN-V12 | **Pedido é registro permanente**: não é apagado, e referencia a oferta, que por isso nunca é apagada depois de publicada | RN-O11 · obrigação fiscal futura (`CAP-016`) |
| RN-CB01 | **O pagamento é recebido numa página do gateway.** Dado de cartão não passa pela plataforma, não é guardado e não aparece em tela nem log nossos | Baseline (PCI mínimo) |
| RN-CB02 | **Meios aceitos: cartão de crédito à vista, PIX e boleto.** Sem parcelamento | OD78 · visão F7 |
| RN-CB03 | **Prazo de pagamento por meio:** a página de pagamento vale **24 horas** depois de aberta; um PIX gerado vale **24 horas**; um boleto vence em **3 dias corridos**. Vencido o prazo sem confirmação, o pedido expira | DP-03 |
| RN-CB04 | **Só a confirmação do gateway, verificada como autêntica, confirma o pagamento.** O retorno do aluno à plataforma depois de pagar não confirma nada sozinho | Baseline (webhook validado por assinatura) |
| RN-CB05 | **O vocabulário do gateway não sai de Cobrança.** Pedido, concessão, comprovante e telas usam meio, situação e motivo traduzidos para os termos desta entrega | Baseline (camada anticorrupção) |
| RN-CB06 | **Recusa de cartão não encerra o pedido.** O aluno pode tentar de novo, com outro cartão ou outro meio, enquanto a página de pagamento estiver válida | Comportamento esperado de checkout |

---

## Funcionalidades Principais

### RF-01: Comprar a partir da vitrine

**Descrição**: Na página do curso, o botão **Comprar** de cada opção de compra passa a iniciar a
compra daquela oferta. O aviso de "compra em breve" e a contagem anônima de cliques de `CAP-003`
(RF-09 daquele PRD) deixam de existir para quem clica (DP-06). Visitante é levado a entrar ou criar
conta e, ao concluir, volta para a compra da mesma opção — sem precisar achar o curso de novo.
Antes de ir para o pagamento, o aluno vê um resumo: curso, opção, preço e vigência prometida
("acesso por 12 meses a partir da liberação" ou "acesso vitalício").

**Critérios de Aceitação**:

- **Given** um aluno autenticado na página de um curso com a oferta "12 meses — R$ 497,00"
  **When** clica em *Comprar* nessa oferta
  **Then** vê o resumo com curso, "12 meses", "R$ 497,00" e a informação de que o prazo conta a
  partir da liberação do acesso, e pode seguir para o pagamento

- **Given** um visitante sem sessão
  **When** clica em *Comprar* na oferta vitalícia
  **Then** é levado a entrar ou criar conta e, depois de entrar, chega ao resumo da compra da
  oferta vitalícia daquele curso

- **Given** um visitante que cria conta a partir do *Comprar*
  **When** confirma o e-mail e entra
  **Then** chega ao resumo da compra da oferta que escolheu

- **Given** uma oferta despublicada depois de a página ter sido aberta
  **When** o aluno clica em *Comprar*
  **Then** é informado de que a opção não está mais disponível e nenhum pedido é criado

- **Given** uma sessão de ator interno
  **When** tenta comprar, inclusive por chamada direta
  **Then** a compra é recusada e nenhum pedido é criado

- **Given** a página de um curso
  **When** o visitante clica em *Comprar*
  **Then** nenhum clique é somado à contagem anônima de intenção de compra

**Prioridade**: Must Have

**Rastreabilidade**: RN-V01, RN-V02, RN-V11, RN-O05, RN-O12, RN-O13

---

### RF-02: Aviso de acesso já existente

**Descrição**: Se o aluno já tem concessão ativa e dentro da vigência sobre o curso — de qualquer
origem —, o resumo da compra avisa antes do pagamento: "Você já tem acesso a este curso até
DD/MM/AAAA" (ou "acesso vitalício") e a origem em linguagem do aluno ("cortesia", "compra"). O
aluno pode seguir com a compra. A nova concessão convive com a existente; uma não estende nem
encerra a outra.

**Critérios de Aceitação**:

- **Given** um aluno com cortesia ativa até 30/11/2026 sobre o curso
  **When** abre o resumo da compra da oferta vitalícia
  **Then** vê o aviso "Você já tem acesso a este curso até 30/11/2026 (cortesia)" e pode seguir

- **Given** o mesmo aluno, que segue e paga
  **When** a compra é concluída
  **Then** ele passa a ter as duas concessões, e a cortesia continua terminando em 30/11/2026

- **Given** um aluno cuja única concessão sobre o curso expirou
  **When** abre o resumo da compra
  **Then** não vê o aviso

- **Given** a decisão de acesso indisponível no momento do resumo
  **When** o aluno abre o resumo
  **Then** o resumo aparece sem o aviso e a compra segue normalmente (o aviso é informativo; sua
  ausência não bloqueia nem libera nada)

**Prioridade**: Should Have

**Rastreabilidade**: RN-V11, RN-D07, OD60

---

### RF-03: Criar o pedido e congelar a condição

**Descrição**: Ao seguir do resumo para o pagamento, nasce o pedido em *Aguardando pagamento*, com
aluno, curso, oferta, preço e vigência prometida congelados naquele instante. Se já existe pedido
aguardando pagamento do mesmo aluno para a mesma oferta, o aluno é levado a ele (RF-06) em vez de
criar outro.

**Critérios de Aceitação**:

- **Given** a oferta "12 meses — R$ 497,00"
  **When** o aluno segue para o pagamento
  **Then** existe um pedido *Aguardando pagamento* com R$ 497,00 e 12 meses

- **Given** esse pedido aguardando um boleto
  **When** o financeiro muda a oferta para R$ 597,00 e 6 meses
  **Then** o pedido continua com R$ 497,00 e 12 meses, e é isso que o boleto cobra

- **Given** esse pedido aguardando um boleto
  **When** o financeiro despublica a oferta
  **Then** o pedido continua válido e pode ser pago

- **Given** um pedido aguardando pagamento do aluno para a oferta vitalícia
  **When** o aluno clica de novo em *Comprar* na oferta vitalícia
  **Then** é levado ao pedido pendente, e nenhum pedido novo é criado

- **Given** um pedido aguardando pagamento para a oferta de 12 meses
  **When** o aluno clica em *Comprar* na oferta vitalícia do mesmo curso
  **Then** um segundo pedido é criado, para a vitalícia (RN-V06 é por oferta)

- **Given** o mesmo clique enviado duas vezes por instabilidade de rede
  **When** a plataforma o recebe
  **Then** existe um único pedido

**Prioridade**: Must Have

**Rastreabilidade**: RN-V03, RN-V04, RN-V06, RN-O09, RN-O10, RN-O12, G09

---

### RF-04: Pagar na página do gateway

**Descrição**: O aluno é levado à página de pagamento do gateway, com o nome da escola, o curso e o
valor do pedido, e escolhe cartão de crédito, PIX ou boleto. Os dados que o meio exige (cartão,
CPF e nome para o boleto) são digitados lá e não passam pela plataforma. Ao terminar, sair ou
cancelar, o aluno volta para a página de retorno (RF-05).

**Critérios de Aceitação**:

- **Given** um pedido de R$ 497,00
  **When** o aluno chega à página de pagamento
  **Then** vê o nome da escola, o curso, a opção e R$ 497,00, e as opções cartão, PIX e boleto

- **Given** a página de pagamento
  **When** o aluno paga com cartão aprovado
  **Then** volta para a página de retorno

- **Given** um cartão recusado
  **When** o aluno tenta pagar
  **Then** vê a recusa na própria página de pagamento e pode tentar outro cartão ou outro meio; o
  pedido continua *Aguardando pagamento*

- **Given** o aluno escolhe PIX
  **When** gera o código
  **Then** vê o QR Code e o código copia-e-cola, com o prazo de 24 horas

- **Given** o aluno escolhe boleto
  **When** gera o boleto
  **Then** vê o boleto com vencimento em 3 dias corridos

- **Given** qualquer pagamento feito
  **When** se inspecionam telas, registros e logs da plataforma
  **Then** não há número de cartão, código de segurança nem validade do cartão

- **Given** o gateway indisponível ao seguir para o pagamento
  **When** o aluno confirma
  **Then** vê que não foi possível abrir o pagamento e pode tentar de novo; a nova tentativa usa o
  mesmo pedido

**Prioridade**: Must Have

**Rastreabilidade**: RN-CB01, RN-CB02, RN-CB03, RN-CB06

---

### RF-05: Página de retorno

**Descrição**: Ao voltar do gateway, o aluno vê a situação real do pedido — nunca presumida pelo
simples retorno (RN-CB04):
- **Pago** → "Compra confirmada" com o botão *Ir para o curso*, que leva ao curso assim que a
  concessão existir;
- **Confirmando** (o aluno pagou, a confirmação ainda não chegou) → a página espera e atualiza
  sozinha, sem o aluno precisar recarregar;
- **Aguardando pagamento** com PIX ou boleto gerado → instruções para pagar, o código ou boleto, o
  prazo e a informação de que o acesso será liberado sozinho quando o pagamento compensar;
- **Aguardando pagamento sem meio escolhido** (o aluno saiu da página de pagamento) → opção de
  *Continuar pagamento* ou *Desistir*.

**Critérios de Aceitação**:

- **Given** um pagamento com cartão aprovado
  **When** o aluno volta à plataforma
  **Then** vê "Compra confirmada" em até 10 segundos na maior parte dos casos, e *Ir para o curso*
  o leva a uma aula do curso, que reproduz

- **Given** a confirmação do gateway ainda não recebida
  **When** o aluno chega à página de retorno
  **Then** vê "Confirmando pagamento", e a página muda para "Compra confirmada" quando o pedido for
  pago, sem recarregar

- **Given** a confirmação demorando mais de 2 minutos
  **When** o aluno continua na página
  **Then** vê que a confirmação pode demorar, que o acesso será liberado sozinho e que ele receberá
  o comprovante por e-mail, com o caminho para *Meus pedidos*

- **Given** um boleto gerado
  **When** o aluno volta à plataforma
  **Then** vê "Aguardando pagamento do boleto", o vencimento e o acesso ao boleto, e o curso ainda
  não está liberado

- **Given** o aluno abre a página de retorno de um pedido de outro aluno
  **When** a página carrega
  **Then** a plataforma responde como se o pedido não existisse

**Prioridade**: Must Have

**Rastreabilidade**: RN-CB04, RN-V05, RN-V10

---

### RF-06: Retomar ou desistir de um pagamento pendente

**Descrição**: Enquanto o pedido estiver *Aguardando pagamento*, o aluno pode voltar a ele (por
*Meus pedidos*, pela página de retorno ou clicando de novo em *Comprar* na mesma oferta) e:
- ver de novo o código PIX ou o boleto já gerados, com o prazo restante;
- continuar o pagamento, quando saiu da página de pagamento sem escolher meio;
- **desistir**, o que cancela o pedido e o pagamento pendente. Depois de desistir, pode comprar de
  novo, criando outro pedido pelas condições vigentes da oferta.

**Critérios de Aceitação**:

- **Given** um boleto gerado ontem
  **When** o aluno abre o pedido em *Meus pedidos*
  **Then** vê o boleto, o vencimento e a opção *Desistir e pagar de outra forma*

- **Given** esse pedido
  **When** o aluno desiste
  **Then** o pedido fica *Cancelado* e o aluno pode clicar em *Comprar* para criar outro pedido
  pelas condições atuais da oferta

- **Given** um pedido cancelado cujo boleto o gateway ainda compensa
  **When** a confirmação chega
  **Then** o pedido vira *Pago* e concede, pela vigência que congelou (RN-V08)

- **Given** um pedido cuja página de pagamento expirou sem meio escolhido
  **When** o aluno tenta *Continuar pagamento*
  **Then** é informado de que o prazo venceu, o pedido está *Expirado* e ele pode comprar de novo

- **Given** um pedido *Pago*, *Expirado* ou *Cancelado*
  **When** o aluno tenta desistir dele, inclusive por chamada direta
  **Then** nada muda

**Prioridade**: Must Have (retomar) · Should Have (desistir)

**Rastreabilidade**: RN-V05, RN-V06, RN-V07, RN-V08, RN-CB03

---

### RF-07: Confirmação do pagamento conclui o pedido e libera o acesso

**Descrição**: Quando o gateway confirma o pagamento, Cobrança verifica que a confirmação é
autêntica e informa Vendas; Vendas marca o pedido *Pago* e publica a compra concluída; Matrícula
concede o acesso com origem *compra*, referência ao pedido e a vigência congelada, contando a
partir da concessão (RN-D04). Nada disso depende do aluno estar com a página aberta.

**Critérios de Aceitação**:

- **Given** um pedido de 12 meses pago por cartão em 05/10/2026
  **When** a confirmação chega
  **Then** o pedido fica *Pago* e o aluno passa a ter uma concessão de origem *compra*, que referencia
  o pedido, com término em 05/10/2027 (fim do dia, conforme a regra de término de `CAP-008`)

- **Given** um boleto de pedido de 12 meses gerado em 05/10 e pago em 08/10
  **When** a confirmação chega
  **Then** a concessão começa em 08/10 e termina em 08/10 do ano seguinte (RN-D04)

- **Given** um pedido vitalício
  **When** é pago
  **Then** a concessão não tem término

- **Given** a mesma confirmação entregue três vezes pelo gateway
  **When** as três são processadas
  **Then** existe um pedido *Pago*, uma concessão e um comprovante

- **Given** uma notificação de pagamento que não vem do gateway (assinatura inválida)
  **When** chega à plataforma
  **Then** é recusada; nenhum pedido muda e nenhuma concessão é criada

- **Given** a oferta alterada para 6 meses depois de o pedido ter sido criado com 12
  **When** o pedido é pago
  **Then** a concessão é de 12 meses (RN-D03)

- **Given** um pedido pago
  **When** se consulta a decisão de acesso do aluno ao curso dentro da vigência
  **Then** a resposta é *pode acessar*, vinda de Matrícula — nenhum consumidor consulta o pedido

- **Given** Matrícula indisponível no momento do pagamento
  **When** ela volta
  **Then** a concessão é criada sem ação humana e o pedido permanece *Pago*

**Prioridade**: Must Have

**Rastreabilidade**: RN-V08, RN-V09, RN-V10, RN-CB04, RN-CB05, RN-D02, RN-D03, RN-D04, RN-D10, G09

---

### RF-08: Expiração do pedido não pago

**Descrição**: Pedido *Aguardando pagamento* cujo prazo (RN-CB03) vence sem confirmação passa a
*Expirado* sozinho. Não concede, e libera o aluno para criar um pedido novo na mesma oferta.

**Critérios de Aceitação**:

- **Given** um boleto gerado em 05/10 e não pago
  **When** o prazo de 3 dias passa
  **Then** o pedido fica *Expirado* e o aluno não tem acesso

- **Given** um PIX gerado e não pago em 24 horas
  **When** o prazo passa
  **Then** o pedido fica *Expirado*

- **Given** um pedido expirado
  **When** o aluno clica em *Comprar* na mesma oferta
  **Then** um pedido novo é criado pelas condições vigentes da oferta

- **Given** um pedido expirado cujo boleto é compensado depois
  **When** a confirmação chega
  **Then** vale RN-V08: o pedido vira *Pago* e concede

**Prioridade**: Must Have

**Rastreabilidade**: RN-V05, RN-V08, RN-CB03

---

### RF-09: Comprovante de compra por e-mail

**Descrição**: Quando o pedido vira *Pago*, o aluno recebe um e-mail transacional com: curso,
opção, valor pago, meio de pagamento, data do pagamento, número do pedido e a vigência ("acesso por
12 meses a partir da liberação" ou "acesso vitalício"), com um link para o curso. O comprovante não
é documento fiscal e diz isso. Enviado uma vez por pedido.

**Critérios de Aceitação**:

- **Given** um pedido pago por PIX
  **When** a compra é concluída
  **Then** o aluno recebe um e-mail com curso, opção, valor, "PIX", data, número do pedido, vigência
  e o link para o curso

- **Given** a confirmação reentregue
  **When** é processada de novo
  **Then** nenhum segundo e-mail é enviado

- **Given** o serviço de envio indisponível
  **When** o pedido é pago
  **Then** o pedido e a concessão não esperam pelo e-mail, e o comprovante é enviado quando o envio
  voltar

- **Given** o comprovante
  **When** o aluno o lê
  **Then** ele não contém dado de cartão além do meio ("cartão de crédito")

**Prioridade**: Must Have

**Rastreabilidade**: RN-V09, RN-27, RN-28, R5 do backlog, QA-01

---

### RF-10: Meus pedidos

**Descrição**: Na área da conta, o aluno vê os próprios pedidos, do mais recente para o mais antigo:
curso, opção, valor, data, meio (quando já escolhido) e situação. Pedido *Aguardando pagamento*
abre a retomada (RF-06); pedido *Pago* leva ao curso. Só os pedidos do próprio aluno.

**Critérios de Aceitação**:

- **Given** um aluno com um pedido pago, um expirado e um aguardando boleto
  **When** abre *Meus pedidos*
  **Then** vê os três, com a situação de cada um, e o aguardando boleto em destaque com o vencimento

- **Given** um aluno sem pedidos
  **When** abre *Meus pedidos*
  **Then** vê que ainda não tem compras e um caminho para a vitrine

- **Given** um pedido de outro aluno
  **When** o aluno tenta abri-lo, inclusive por chamada direta
  **Then** a plataforma responde como se o pedido não existisse

- **Given** um pedido pago cuja oferta foi despublicada depois
  **When** aparece em *Meus pedidos*
  **Then** mostra o curso, a opção, o valor e a vigência congelados no pedido

**Prioridade**: Must Have

**Rastreabilidade**: RN-V03, RN-V05, RN-V12

---

### RF-11: Pedidos no backoffice

**Descrição**: Na área financeira do backoffice — hoje vazia, reservada para esta capacidade —, quem
tem `financeiro.ler` consulta os pedidos da escola. Lista com número, data, aluno (nome e e-mail),
curso, opção, valor, meio e situação, filtrável por situação, período, curso e e-mail do aluno.
O detalhe do pedido mostra a vigência congelada, os momentos de criação, pagamento, expiração ou
cancelamento, a referência do pagamento no gateway (para localizar no painel dele) e se a concessão
correspondente já existe. **Só leitura:** nenhuma ação sobre pedido, pagamento ou acesso nesta
entrega.

**Critérios de Aceitação**:

- **Given** um ator com o papel financeiro
  **When** abre a área financeira
  **Then** vê os pedidos da escola, os mais recentes primeiro

- **Given** a lista
  **When** o financeiro filtra por *Aguardando pagamento* e pelo curso X
  **Then** vê só os pedidos pendentes do curso X

- **Given** o financeiro informa o e-mail de um aluno
  **When** filtra
  **Then** vê os pedidos daquele aluno

- **Given** um pedido pago
  **When** o financeiro abre o detalhe
  **Then** vê a vigência congelada, o momento do pagamento, a referência no gateway e que a
  concessão existe

- **Given** um ator com papel professor, suporte ou administrador, sem o papel financeiro
  **When** tenta abrir a área financeira ou um pedido, inclusive por chamada direta
  **Then** é recusado

- **Given** pedidos de outra escola
  **When** o financeiro consulta
  **Then** eles nunca aparecem

**Prioridade**: Must Have

**Rastreabilidade**: RN-16, RN-V12, RN-O18, DP-03 de `CAP-002` (`financeiro.ler`)

---

## Experiência do Usuário

**Aluno — caminho principal (cartão):** página do curso → *Comprar* na opção → (entrar, se
visitante) → resumo da compra (com aviso de acesso existente, se houver) → *Ir para o pagamento* →
página do gateway → paga → volta para "Compra confirmada" → *Ir para o curso*.

**Aluno — PIX ou boleto:** mesmo caminho até o gateway → gera o código ou boleto → volta para
"Aguardando pagamento" com instruções → paga no banco → recebe o comprovante por e-mail com o link
do curso. A qualquer momento, *Meus pedidos* mostra o pendente, com o código e a opção de desistir.

**Financeiro:** backoffice → área financeira → lista de pedidos → filtro → detalhe.

Considerações:
- O resumo e a página de retorno deixam claro que a vigência **conta a partir da liberação do
  acesso**, não da compra — é o que torna aceitável o boleto que compensa dias depois.
- A página de pagamento é do gateway, com o nome e a cor da escola; a troca de contexto é anunciada
  no resumo ("você será levado ao ambiente seguro de pagamento").
- Mensagens de situação usam a linguagem desta entrega (RN-CB05), nunca códigos do gateway.
- Resumo da compra, página de retorno, *Meus pedidos* e área financeira são telas novas e seguem o
  fluxo de design do projeto (ASCII e Figma aprovados antes do código), sobre o design system atual.
- Acessibilidade: a mudança de "Confirmando" para "Compra confirmada" é anunciada a leitores de tela;
  situação do pedido nunca é comunicada só por cor.

## Decisões de Produto

| ID | Decisão confirmada | Alternativas descartadas e motivo | Impacto no PRD | Registro |
|---|---|---|---|---|
| DP-01 | Pagamento em **checkout hospedado do gateway** (Stripe) | Pagamento embutido na SPA — mais front, mais estados de erro, escopo PCI maior | RF-04, RN-CB01 | OD78 |
| DP-02 | **Cartão, PIX e boleto** na mesma entrega | Só cartão, ou cartão + PIX — o card de `CAP-011` promete os três e PIX já obriga a tratar pagamento assíncrono; boleto custa pouco a mais | RN-CB02, RF-08 | OD78 |
| DP-03 | **Prazos:** página de pagamento 24 h, PIX 24 h, boleto 3 dias corridos | PIX de 30–60 min (comum em e-commerce) — curto demais para quem retoma por *Meus pedidos*; boleto de 5+ dias — segura o pedido e adia a decisão do aluno sem ganho | RN-CB03, RF-08 | Aprovada em 2026-10-05 |
| DP-04 | Aluno pode **desistir** de pedido pendente e comprar de novo pelas condições vigentes | Sem desistência — o aluno com boleto gerado ficaria preso 3 dias para trocar de meio | RN-V07, RF-06 | Aprovada em 2026-10-05 |
| DP-05 | **Pagamento confirmado sempre conclui o pedido**, mesmo expirado ou cancelado | Recusar e devolver — exigiria reembolso, que está fora (`CAP-015`); o aluno pagou e ficaria sem nada | RN-V08 | Aprovada em 2026-10-05 |
| DP-06 | A contagem anônima de cliques em *Comprar* (RF-09 de `CAP-003`) **para de crescer**; o total histórico continua visível na oferta, rotulado como "cliques antes da venda abrir" | Continuar contando — mediria clique, não compra, ao lado de dado de venda real; apagar — perde o único sinal de demanda pré-venda | RF-01 | Aprovada em 2026-10-05 |
| DP-07 | **Reembolso ou chargeback no painel do gateway não revoga o acesso** nesta entrega | Revogar por evento do gateway — é `CAP-015`/`CAP-009`, com revogação definitiva (RN-D13) que ainda não existe | Não-Objetivos, Riscos | OD78 |
| DP-08 | Aviso de acesso existente **informa e não bloqueia** | Bloquear compra de quem já tem acesso — impediria trocar cortesia de 3 meses por compra vitalícia | RF-02, RN-V11 | OD78 |
| DP-09 | Pedidos no backoffice sob a permissão existente **`financeiro.ler`**, só leitura | Permissão nova `pedido.ler` — sem papel que precise de uma sem a outra; suporte com acesso — RN-16 restringe dado financeiro ao financeiro | RF-11 | Aprovada em 2026-10-05 |

---

## Restrições Técnicas de Alto Nível

- **Gateway:** Stripe, em checkout hospedado, com cartão, PIX e boleto habilitados para a conta da
  escola (ver QA-02).
- **PCI:** dado de cartão nunca trafega nem é persistido pela plataforma.
- **Camada anticorrupção** sobre o gateway; a confirmação entra por ponto dedicado, autenticada pela
  assinatura do gateway e idempotente.
- **LGPD:** CPF e nome do pagador do boleto são coletados pelo gateway, não pela plataforma. O
  gateway é operador de dado pessoal do aluno (e-mail e, no boleto, CPF).
- **Tenant:** pedido, pagamento e consulta do financeiro carregam e respeitam a escola.

---

## Não-Objetivos (Fora de Escopo)

- Cupom, desconto e campanha de compra (`CAP-004`).
- Assinatura, cobrança recorrente, régua e inadimplência (`CAP-013`, `CAP-014`).
- **Reembolso e chargeback como fato do sistema.** Reembolso feito no painel do gateway não revoga
  acesso, não muda a situação do pedido e não avisa o aluno pela plataforma (DP-07). Volta com
  `CAP-015`.
- Emissão de NFS-e e coleta de dado fiscal do comprador (`CAP-016`). O comprovante não é documento
  fiscal.
- Parcelamento, carrinho com vários cursos, compra para terceiro, compra sem conta.
- Ação do financeiro sobre pedido (cancelar, estornar, reenviar comprovante, conceder manualmente).
- Atribuição de afiliado (`CAP-012`).
- Aviso de pedido expirado ou lembrete de boleto a vencer.
- Métricas de negócio do produto (conversão, ticket médio) com meta — dependem de OD2.

---

## Plano de Rollout Faseado

### MVP (Fase 1) — esta entrega

- **Funcionalidades incluídas:** RF-01 a RF-11.
- **Critério para encerrar o MVP:** uma compra real (fora do sandbox) por cartão, uma por PIX e uma
  por boleto concluídas de ponta a ponta, com acesso liberado sem intervenção e comprovante
  recebido — além do critério da Fase 1 da visão.

### Fase 2 (outras capacidades)

- `CAP-016` (NFS-e da venda), `CAP-013` (recorrência), `CAP-015` + `CAP-009` (reembolso com
  revogação), `CAP-004` (cupom e campanha) — cada uma em PRD próprio, sobre o pedido e o pagamento
  desta entrega.

---

## Métricas de Sucesso

| Métrica | Definição | Alvo | Prazo |
|---|---|---|---|
| Tempo até o acesso | Do pagamento confirmado pelo gateway até a concessão existir | p95 ≤ 60 s | Desde o primeiro dia |
| Pedido pago sem concessão | Pedidos *Pago* há mais de 10 min sem concessão correspondente | 0 | Contínuo |
| Concessão por compra sem pedido pago | Concessões de origem *compra* cujo pedido não está *Pago* | 0 | Contínuo |
| Duplicidade | Pedidos com mais de uma concessão ou mais de um comprovante | 0 | Contínuo |
| Compra sem ajuda | Compras concluídas no primeiro mês que geraram contato de suporte sobre acesso não liberado | ≤ 2% | 30 dias após abrir a venda |
| Conversão pedido → pago | Pedidos pagos sobre pedidos criados, por meio | Medida, sem alvo (OD2) | Desde o primeiro dia |

---

## Riscos e Mitigações

- **Conta Stripe sem PIX ou boleto habilitado** — os dois dependem de conta brasileira e de
  aprovação do meio. Mitigação: QA-02 confirmada antes da TechSpec; se um meio não estiver
  disponível, ele sai do recorte por decisão explícita, sem bloquear o cartão.
- **Aluno paga duas vezes** (desiste do boleto, paga com cartão e paga o boleto mesmo assim).
  Mitigação: RN-V06 e cancelamento no gateway reduzem o caso; RN-V08 garante que ninguém pague sem
  receber; a devolução é manual pelo painel do gateway até `CAP-015`. O financeiro identifica o
  caso filtrando os pedidos do aluno (RF-11).
- **Reembolso mantém o acesso** (DP-07). Mitigação: limitação declarada; volume baixo esperado no
  MVP; a revogação chega com `CAP-015`/`CAP-009`.
- **Obrigação fiscal nasce com a primeira venda** (R13) e a NFS-e só vem em `CAP-016`. Mitigação:
  emissão manual pelo financeiro até lá, com a lista de pedidos pagos (RF-11) como fonte.
- **Venda real abrir antes da identidade visual** (A2). Mitigação: as telas novas seguem o design
  system vigente; a página do gateway usa nome e cor da escola.

---

## Alternativas Consideradas

### Abordagem Escolhida: checkout hospedado, pedido em Vendas, concessão por fato

- **Descrição:** o pedido nasce em Vendas com a condição congelada; o pagamento acontece na página
  do gateway; a confirmação autenticada conclui o pedido, que publica a compra concluída; Matrícula
  concede.
- **Por que foi escolhida:** respeita a junta do Domain Map (Vendas ≠ Cobrança ≠ Matrícula), o risco
  DE01/R2 (direito nunca vira flag de pedido) e o escopo PCI mínimo; reaproveita a concessão de
  `CAP-008` sem caso especial.

### Alternativa Rejeitada 1: liberar o acesso no retorno do aluno

- **Descrição:** conceder quando o aluno volta do gateway com sucesso.
- **Trade-offs:** mais simples, mas o retorno pode ser forjado ou não acontecer (aba fechada, PIX
  pago no celular).
- **Por que foi rejeitada:** RN-CB04 — só a confirmação autenticada do gateway confirma.

### Alternativa Rejeitada 2: Matrícula concede a partir do pagamento confirmado de Cobrança

- **Descrição:** pular o pedido e conceder direto do fato financeiro.
- **Trade-offs:** um salto a menos.
- **Por que foi rejeitada:** a vigência congelada está no pedido, não no pagamento (RN-D03); o
  pagamento não conhece a oferta.

---

## Questões em Aberto

- **QA-01 — Destinatário do comprovante × RN-26.** O pedido de envio a Notificação exige o e-mail do
  aluno como destinatário, e RN-26 de Identidade declara essa exceção como "segunda e última" — no
  contexto de Identidade pedindo envio. Vendas pedir envio do comprovante precisa ou de uma exceção
  nova (revisão do domain doc de Identidade), ou de um caminho em que o e-mail não saia de Identidade
  (Notificação resolver o destinatário pela conta). **Quem responde:** tasso, na TechSpec. **Impacto
  se não resolvido:** RF-09 sem caminho conforme às regras de mascaramento.
- **QA-02 — Meios habilitados na conta Stripe.** A conta (e o sandbox) é brasileira, com PIX e boleto
  habilitados? **Quem responde:** tasso, antes da TechSpec. **Impacto:** DP-02; um meio indisponível
  sai do recorte.
- **QA-03 — Registro do Domain Map.** A interação de Cobrança no Domain Map diz que Matrícula consome
  "pagamento confirmado"; o domain doc de Matrícula e este PRD fixam que Matrícula consome a compra
  concluída de Vendas. **Quem responde:** tasso, na próxima revisão do Domain Map (junto de OD77).
  **Impacto:** nenhum nesta entrega; evita leitura divergente depois.
