---
tsg_artifact: prd
product: code-4-coders
capability: CAP-005
version: 1.1
status: in_review
updated: 2026-09-30
sources: backlog/capabilities.md@1.4, vision.md@1.2, context/domain-map.md@1.2, context/architecture-baseline.md@1.2, domains/conteudo-e-curriculo/domain.md@1.1, domains/catalogo-e-oferta/domain.md@1.1, domains/identidade-e-acesso/domain.md@1.1
---

# Nível e pré-requisito do curso — o professor orienta a escolha do aluno

## Visão Geral

A vitrine (`CAP-003`) vai mostrar a um visitante se um curso é para ele: o **nível** (iniciante,
intermediário ou avançado) e o **pré-requisito** recomendado. Como a plataforma decidiu não travar
compra por pré-requisito (DE04), essa informação é tudo o que orienta a escolha — e orientar mal
custa reembolso e churn (R12).

É informação pedagógica, e quem a tem é o professor (persona da visão). Por isso ela nasce no
editor de curso, no rascunho, e vai ao público **junto com a versão publicada**, como o resto do
currículo (OD54). Esta entrega acrescenta os dois campos ao editor da `CAP-005` já entregue e os
leva no fato de publicação, para que o Catálogo os exiba e filtre.

Esta entrega **não** mostra nada ao visitante: ela termina quando um curso publicado carrega nível e
pré-requisito na versão vigente e o Catálogo consegue lê-los. É a provedora de `CAP-003`.

---

## Rastreabilidade

### Capacidade e fronteiras

- **Capacidade:** `CAP-005` — Autoria e publicação de curso.
- **Escopo desta entrega (segundo PRD de `CAP-005`):** o professor declara nível e pré-requisito no
  rascunho; o pré-requisito é texto e/ou cursos da escola; os dois são publicados com a versão e
  aparecem no histórico; o editor avisa quando falta nível; o fato de publicação passa a levá-los.
- **Fora desta entrega:**
  - exibir, filtrar e ligar os cursos recomendados na vitrine → primeiro PRD de `CAP-003`;
  - material complementar → PRD próprio de `CAP-005` (OD43);
  - qualquer bloqueio de compra, matrícula ou acesso por nível ou pré-requisito → nunca (DE04).
- **Domínios atravessados:**
  - Conteúdo e Currículo (dono) — [domain.md](../../../domains/conteudo-e-curriculo/domain.md) v1.1,
    RN-C18;
  - Catálogo e Oferta (consumidor) — [domain.md](../../../domains/catalogo-e-oferta/domain.md) v1.1,
    RN-O03;
  - Identidade e Acesso — [domain.md](../../../domains/identidade-e-acesso/domain.md), pela permissão de
    autoria já existente.
- **Junta entre os domínios:** *Conteúdo → Catálogo.* Conteúdo é dono de nível e pré-requisito e os
  publica com a versão; o Catálogo os lê da versão vigente, não os guarda como seus e não os edita
  (RN-O03). O que a vitrine faz com eles é de `CAP-003`.
- **Dependências entre capacidades:** evolui o primeiro PRD de `CAP-005`
  ([prd-autoria-curso](../prd-autoria-curso/prd.md), entregue). `CAP-003` consome esta entrega e
  exige nível na versão vigente para publicar oferta (RN-O03).
- **Restrições do baseline:** `tenant_id` em toda entidade e consulta (G07); fato por outbox (G06);
  mudança de contrato aditiva (G14); o serviço dono autoriza a partir das claims (RN-18 de
  Identidade).

### Vision Doc

- **Objetivos de negócio atendidos:** `C02` (catálogo por nível, pré-requisito informativo) e `C03`
  (autoria), na Fase 1.
- **Restrições globais aplicáveis:** nível e pré-requisito são informação pedagógica, **sem gate**
  (§5, "Segmentação por nível"); nível é do curso inteiro, não de módulo ou aula (H4).
- **Non-Goals globais respeitados:** não bloqueia compra, matrícula ou acesso por pré-requisito.

### Domain Docs

- **Entidades envolvidas (Conteúdo e Currículo):** Curso, Rascunho, Versão de Publicação.
- **Entidades envolvidas (Catálogo e Oferta):** Ficha de Vitrine, só como consumidora.
- **Regras de negócio referenciadas:** Conteúdo RN-C03, RN-C05, RN-C06, RN-C08, RN-C10, RN-C11,
  RN-C12, RN-C13, RN-C18; Catálogo RN-O03, RN-O05; Identidade RN-12, RN-18.
- **Regras precisadas por esta entrega:** nível é opcional para publicar (DP-01); pré-requisito
  combina texto e cursos da escola (DP-02).
- **Eventos produzidos:** `conteudo.versao-publicada`, acrescido de nível e pré-requisito (aditivo).
- **Eventos consumidos:** nenhum novo.

## Termos Canônicos

| Termo | Definição de negócio | Escopo/Fonte |
|---|---|---|
| Nível | Iniciante, intermediário ou avançado; do curso inteiro. Orienta, não restringe | Visão · RN-C18 |
| Pré-requisito declarado | Recomendação pedagógica do professor: um texto e/ou uma lista de cursos da escola. Opcional; não restringe | RN-C18 · DP-02 |
| Curso recomendado | Curso da escola apontado no pré-requisito de outro. É referência, não dependência | Esta entrega |
| Sem nível | Estado do curso cuja versão (ou rascunho) não declara nível. Publica normalmente; não pode ter oferta publicada (RN-O03) | Esta entrega |

---

## Objetivos

- Todo curso que a escola pretende vender chega a `CAP-003` com nível declarado pelo professor, sem
  depender do financeiro para uma informação pedagógica.
- A informação que orienta a escolha segue a mesma regra do currículo: o visitante só vê o que foi
  publicado (RN-C05), e mudar exige republicar.
- `CAP-003` começa sem stub: lê nível e pré-requisito reais da versão vigente.

---

## Histórias de Usuário

- Como **professor**, quero declarar o nível do meu curso para que o aluno certo o encontre.
- Como **professor**, quero recomendar o que o aluno deve saber antes — em texto ou apontando outro
  curso da escola — para que ninguém compre um curso para o qual não está preparado.
- Como **professor**, quero ser avisado de que um curso sem nível não entra na vitrine, sem ser
  impedido de publicar uma correção de aula.
- Como **professor**, quero ver no histórico o nível e o pré-requisito de cada versão, para saber o
  que o aluno viu em cada momento.
- Como **Catálogo** (`CAP-003`), quero ler nível e pré-requisito da versão vigente de um curso para
  exibi-los e filtrar a vitrine.

---

## Funcionalidades Principais

### RF-01: Declarar o nível no rascunho

**Descrição**: No editor do curso, o professor escolhe o nível entre **iniciante**,
**intermediário** e **avançado**, ou deixa **sem nível**. A escolha vai para o rascunho como qualquer
outra alteração (RN-C05): num curso publicado, ela fica em *alterações não publicadas* até a
próxima publicação. Exige `autoria.editar`; quem só tem `autoria.ler` vê o nível sem poder alterá-lo.

**Critérios de Aceitação**:

- **Given** um curso em rascunho
  **When** o professor escolhe *intermediário* e salva
  **Then** o rascunho passa a ter nível intermediário

- **Given** um curso publicado na versão 2, sem nível
  **When** o professor escolhe *iniciante* e não republica
  **Then** a versão vigente continua sem nível, e o curso aparece como *publicado com alterações não
  publicadas*

- **Given** um rascunho com nível avançado
  **When** o professor escolhe *sem nível*
  **Then** o rascunho deixa de ter nível

- **Given** um valor fora dos três níveis
  **When** é enviado por chamada direta
  **Then** é recusado com a indicação do campo

- **Given** um ator com `autoria.ler` e sem `autoria.editar`
  **When** abre o editor
  **Then** vê o nível sem ação de alteração, e a alteração por chamada direta é recusada

**Prioridade**: Must Have

**Rastreabilidade**: Conteúdo RN-C05, RN-C10, RN-C18; Visão H4

---

### RF-02: Declarar o pré-requisito no rascunho

**Descrição**: No editor, o professor pode escrever um **texto de pré-requisito** (até 1 000
caracteres) e/ou apontar **até 5 cursos recomendados** da escola, em ordem escolhida por ele. Os dois
são opcionais e independentes. Só podem ser apontados cursos **da mesma escola com versão vigente**,
e nunca o próprio curso. O seletor mostra os cursos publicados pelo título. Como qualquer alteração,
vai para o rascunho (RN-C05).

**Critérios de Aceitação**:

- **Given** um curso em rascunho
  **When** o professor escreve "Lógica de programação e noções de Git" e aponta o curso publicado
  *Fundamentos de C#*
  **Then** o rascunho guarda o texto e a referência, nessa ordem

- **Given** o seletor de cursos recomendados
  **When** o professor o abre
  **Then** vê só cursos publicados da escola, sem o curso que está editando e sem cursos nunca
  publicados

- **Given** um rascunho com 5 cursos recomendados
  **When** o professor tenta apontar o sexto
  **Then** a ação não é oferecida, e a chamada direta é recusada com a indicação do limite

- **Given** um texto com mais de 1 000 caracteres
  **When** o professor tenta salvar
  **Then** é recusado com a indicação do campo e do limite

- **Given** a referência a um curso de outra escola, a um curso nunca publicado ou ao próprio curso
  **When** é enviada por chamada direta
  **Then** é recusada, e curso de outra escola responde como inexistente (G07)

- **Given** o curso A recomenda B e B recomenda A
  **When** os dois são salvos
  **Then** ambos são aceitos — recomendação não é dependência, e não há ciclo a impedir

- **Given** um rascunho com texto e cursos recomendados
  **When** o professor apaga o texto e remove os cursos
  **Then** o rascunho fica sem pré-requisito, e isso é válido

**Prioridade**: Must Have

**Rastreabilidade**: Conteúdo RN-C05, RN-C17, RN-C18; Catálogo RN-O05; DP-02

---

### RF-03: Publicar nível e pré-requisito com a versão

**Descrição**: A publicação leva nível e pré-requisito do rascunho para a nova versão, com as mesmas
validações de estrutura de hoje (RN-C11) — nenhuma validação nova impede publicar. Uma republicação
em que só mudou o nível ou o pré-requisito é uma republicação válida: gera versão, ato na trilha e
fato, como qualquer outra (RN-C13). A versão publicada é imutável (RN-C06): o nível e o
pré-requisito de uma versão nunca mudam depois.

**Critérios de Aceitação**:

- **Given** um rascunho completo com nível iniciante e pré-requisito
  **When** o professor publica
  **Then** a nova versão vigente tem nível iniciante e o mesmo pré-requisito do rascunho

- **Given** um curso publicado em que só o nível mudou no rascunho
  **When** o professor republica
  **Then** nasce a versão seguinte, com o ato na trilha e o fato publicado, e a estrutura de aulas é a
  mesma (RN-C07)

- **Given** um rascunho completo sem nível
  **When** o professor publica
  **Then** a publicação acontece (DP-01), com o aviso de RF-04

- **Given** um rascunho que recomenda um curso publicado
  **When** o professor publica
  **Then** a versão guarda a referência — e ela continua válida, porque curso publicado não é
  excluído nem despublicado no MVP (OD45)

**Prioridade**: Must Have

**Rastreabilidade**: Conteúdo RN-C05, RN-C06, RN-C07, RN-C11, RN-C13, RN-C18

---

### RF-04: Aviso de curso sem nível

**Descrição**: Quando o rascunho ou a versão vigente não tem nível, o editor mostra um aviso
permanente: *sem nível, este curso não pode entrar na vitrine*. A janela de publicação repete o
aviso, sem bloquear. A lista de cursos indica os cursos cuja versão vigente não tem nível.

**Critérios de Aceitação**:

- **Given** um curso publicado sem nível
  **When** o professor abre o editor
  **Then** vê o aviso, com atalho para o campo de nível

- **Given** um rascunho sem nível
  **When** o professor abre a janela de publicação
  **Then** vê o aviso e o botão de publicar habilitado

- **Given** a lista de cursos
  **When** o professor a abre
  **Then** os cursos cuja versão vigente não tem nível estão identificados em texto, não só por cor

- **Given** um curso com nível no rascunho, mas não na versão vigente
  **When** o professor abre o editor
  **Then** o aviso diz que o nível só vale depois de publicar

**Prioridade**: Must Have

**Rastreabilidade**: Catálogo RN-O03; DP-01

---

### RF-05: Histórico mostra nível e pré-requisito de cada versão

**Descrição**: No histórico de versões (RF-08 do primeiro PRD), cada versão exibe o nível e o
pré-requisito com que foi publicada, incluindo os cursos recomendados pelo título que eles tinham
**no momento da publicação**. Versões anteriores a esta entrega aparecem *sem nível* e *sem
pré-requisito*.

**Critérios de Aceitação**:

- **Given** um curso com versões 1 (sem nível) e 2 (iniciante)
  **When** o professor abre o histórico
  **Then** vê *sem nível* na versão 1 e *iniciante* na versão 2

- **Given** uma versão que recomendava o curso *X*, depois renomeado para *Y*
  **When** o histórico é aberto
  **Then** a versão mostra *X*, o título publicado na época

**Prioridade**: Should Have

**Rastreabilidade**: Conteúdo RN-C06

---

### RF-06: Nível e pré-requisito disponíveis ao Catálogo

**Descrição**: O fato `conteudo.versao-publicada` passa a levar o nível e o pré-requisito da versão
(texto e cursos recomendados, por identificador). É mudança **aditiva** (G14): quem já consome o
fato — Mídia — continua funcionando sem mudança. Para que o Catálogo comece com os cursos já
publicados antes desta entrega, há uma **carga inicial por reenvio**: uma única vez, na implantação,
o fato da versão vigente de cada curso publicado é reenviado com o mesmo identificador de evento; o
Catálogo mantém a própria visão a partir dos fatos e não lê o Conteúdo sob demanda (errata 1.1, OD56).
Nenhum dado pessoal vai no fato: o autor segue por referência, como hoje.

**Critérios de Aceitação**:

- **Given** uma publicação com nível avançado e dois cursos recomendados
  **When** o fato é publicado
  **Then** ele leva o nível e os identificadores dos dois cursos, na ordem declarada, e o texto

- **Given** uma publicação sem nível e sem pré-requisito
  **When** o fato é publicado
  **Then** ele declara explicitamente a ausência dos dois, sem campo inventado

- **Given** a Mídia, consumidora atual do fato
  **When** recebe um fato com os campos novos
  **Then** registra as Referências de Uso como antes

- **Given** cursos publicados antes desta entrega
  **When** a carga inicial é executada na implantação
  **Then** o fato da versão vigente de cada um é reenviado uma única vez, com o mesmo identificador de
  evento da publicação original; cursos nunca publicados ficam de fora; uma segunda execução não
  reenvia nada

- **Given** a Mídia, que já aplicou essas versões
  **When** recebe o reenvio
  **Then** confirma sem alterar as Referências de Uso

- **Given** um curso publicado de uma escola
  **When** o fato (original ou reenviado) é publicado
  **Then** ele leva a escola do curso, e o Catálogo o aplica só a ela (G07)

**Prioridade**: Must Have

**Rastreabilidade**: Conteúdo RN-C12, RN-C18; Catálogo RN-O03; G14; OD56 (carga inicial por reenvio)

---

## Experiência do Usuário

**Persona.** O professor que já monta e publica cursos (`CAP-005`). Declara o nível uma vez e
raramente o muda; o pré-requisito, ele revisa quando o curso ou o catálogo da escola mudam.

**Fluxo principal.** Editor do curso → seção **Para quem é este curso**, ao lado de título e
descrição pedagógica → escolhe o nível (três opções e *sem nível*) → escreve o pré-requisito e/ou
aponta cursos recomendados no seletor (busca por título, reordenação por teclado) → salva →
publica ou republica.

**Mudança no editor já entregue.** O primeiro PRD dizia, na ajuda contextual, que nível e
pré-requisito eram da oferta. A ajuda passa a dizer que **preço e vigência** são da oferta; nível e
pré-requisito são do professor.

**Linguagem.** Nível e pré-requisito são apresentados como **recomendação**. Nenhum texto do editor
sugere que o pré-requisito impede a compra ou o acesso (DE04).

**Design.** Fluxo já usado no backoffice: wireframe ASCII → Figma → aprovação → código, sobre o
design system do backoffice.

**Acessibilidade.** Nível como grupo de opções com rótulo; aviso de *sem nível* em texto e
anunciado a leitor de tela; seletor de cursos operável por teclado, com a ordem alterável sem
arrastar.

## Decisões de Produto

| ID | Decisão confirmada | Alternativas descartadas e motivo | Impacto no PRD | Registro |
|---|---|---|---|---|
| DP-01 | **Nível é opcional para publicar**; o editor e a publicação avisam que, sem nível, o curso não entra na vitrine | Obrigatório para publicar: mudaria RN-C11 e travaria correção de aula em curso já publicado até alguém decidir o nível | RF-03, RF-04 | OD55 |
| DP-02 | **Pré-requisito é texto e/ou até 5 cursos publicados da escola** | Só texto: orienta menos e não liga um curso ao outro na vitrine | RF-02, RF-06 | OD55 |

Herdadas e não reabertas: nível e pré-requisito são do curso, publicados com a versão (OD54, RN-C18);
nenhum dos dois condiciona compra ou acesso (DE04); publicação sem motivo obrigatório (OD42); uma
permissão cobre editar e publicar (DP-02 do primeiro PRD).

---

## Restrições Técnicas de Alto Nível

- Serviços com mudança: o dono de Conteúdo e Currículo e `bff-admin`/`admin-spa` (editor). Mídia não
  muda. Auditoria não muda: o ato de publicação já existe (RN-C13).
- O fato `conteudo.versao-publicada` evolui de forma aditiva (G14); a carga inicial do Catálogo é o
  reenvio único do fato da versão vigente, sem operação de leitura sob demanda (OD56). Contratos em
  [contracts.md](contracts.md).
- Tudo isolado por escola (G07).

---

## Não-Objetivos (Fora de Escopo)

- Exibir nível e pré-requisito na vitrine, filtrar por nível, apresentar os cursos recomendados como
  links (`CAP-003`).
- Qualquer bloqueio de compra, matrícula ou acesso por nível ou pré-requisito (DE04).
- Nível por módulo ou por aula (H4).
- Preencher nível dos cursos já publicados automaticamente: o professor declara e republica.
- Avisar alunos de que o nível ou o pré-requisito mudou.

---

## Plano de Rollout Faseado

### MVP (Fase 1)

- **Funcionalidades incluídas:** RF-01 a RF-06.
- **Critério para liberar `CAP-003`:** ao menos um curso real publicado com nível na versão vigente,
  e o Catálogo lendo nível e pré-requisito dele.

---

## Métricas de Sucesso

| Métrica | Definição | Alvo | Prazo |
|---|---|---|---|
| Curso pronto para vitrine | Cursos publicados que a escola pretende vender e têm nível na versão vigente | Todos | Antes de publicar a primeira oferta (`CAP-003`) |
| Fato sem nível ou pré-requisito explícito | Fatos de versão publicada que omitem os campos em vez de declarar a ausência | 0 | Desde a primeira publicação após a entrega |

---

## Riscos e Mitigações

- **Professor não declara nível** e o curso não pode ir à vitrine. — Mitigação: aviso permanente no
  editor e na lista (RF-04); o financeiro vê o motivo ao tentar publicar a oferta (`CAP-003`).
- **Republicar só para mudar o nível parece burocracia.** — Mitigação: é a regra do currículo
  (RN-C05) e protege o que o visitante já viu; a republicação é barata.
- **Pré-requisito usado como pressão de gate** depois de um reembolso. — Mitigação: DE04 é non-goal
  da visão; a resposta legítima é `CAP-015`.

---

## Alternativas Consideradas

### Abordagem Escolhida: atributos do curso, publicados com a versão

- **Descrição:** RF-01 a RF-06.
- **Por que foi escolhida:** quem tem a informação é o professor; publicá-la com a versão mantém a
  regra única de que o visitante só vê o que foi publicado (OD54).

### Alternativa Rejeitada 1: nível e pré-requisito na Ficha de Vitrine, pelo financeiro

- **Trade-offs:** não tocaria o editor de curso.
- **Por que foi rejeitada:** o financeiro não tem a informação pedagógica (OD54).

### Alternativa Rejeitada 2: atributos do curso fora da versão, com efeito imediato

- **Trade-offs:** mudar o nível não exigiria republicar.
- **Por que foi rejeitada:** criaria um pedaço do curso visível ao público sem publicação, quebrando
  RN-C05 e o histórico por versão.

---

## Questões em Aberto

Nenhuma.

**Revisão 1.1 (2026-09-30), errata do RF-06:** a "leitura sob demanda pelo Catálogo" passa a ser a
carga inicial por reenvio do fato da versão vigente, com o mesmo identificador de evento (C-01 de
[contracts.md](contracts.md), OD56). O critério de isolamento por escola passa a valer para o fato.
Nenhum outro requisito muda.
