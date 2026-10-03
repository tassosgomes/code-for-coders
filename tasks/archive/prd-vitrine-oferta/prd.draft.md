---
tsg_artifact: prd
product: code-4-coders
capability: CAP-003
version: 1.0
status: in_review
updated: 2026-09-30
sources: backlog/capabilities.md@1.4, vision.md@1.2, context/domain-map.md@1.2, context/architecture-baseline.md@1.2, domains/catalogo-e-oferta/domain.md@1.1, domains/matricula-e-direito-de-acesso/domain.md@1.0, domains/conteudo-e-curriculo/domain.md@1.1, domains/identidade-e-acesso/domain.md@1.1, domains/auditoria-e-conformidade/domain.md@1.2, tasks/prd-nivel-prerequisito-curso/prd.md@1.0
---

# Vitrine e oferta de curso — o que está à venda, por quanto e por quanto tempo

## Visão Geral

A escola já publica cursos (`CAP-005`), mas nada está à venda: não há preço, não há promessa de
quanto tempo o aluno terá acesso, e não há lugar onde um visitante veja o que existe. Esta entrega
cria as duas pontas disso:

- no **backoffice**, o financeiro monta ofertas sobre cursos publicados — preço e vigência do acesso
  (por N meses ou vitalícia) — e as publica ou despublica;
- na **área pública**, qualquer visitante, sem conta, vê a vitrine, filtra por nível, abre a página
  do curso com o que se aprende, o nível, o pré-requisito recomendado e as opções de compra.

É aqui que o direito de acesso é **prometido** (DE01). A promessa precisa estar certa desde o
primeiro dia, porque `CAP-008` a concede e `CAP-011` a congela no pedido — e nenhuma das duas volta
ao Catálogo para perguntar de novo (RN-O09).

A compra em si ainda não existe (`CAP-011`, que depende do gateway — OD5). O botão de compra existe,
leva a um aviso de que a compra estará disponível em breve e conta o clique por oferta, sem
identificar ninguém (OD53). Quando `CAP-011` chegar, ela troca o destino do botão.

---

## Rastreabilidade

### Capacidade e fronteiras

- **Capacidade:** `CAP-003` — Vitrine e oferta de curso.
- **Escopo desta entrega (primeiro PRD de `CAP-003`, fatia OD46 ajustada por OD54):** permissão de
  gestão de oferta ao financeiro; Ficha de Vitrine sobre curso publicado; ofertas com preço e
  vigência prometida (meses ou vitalícia), várias por curso; publicar, alterar, despublicar e
  excluir rascunho de oferta; atos auditados; vitrine pública com filtro por nível; página pública
  do curso; botão de compra com aviso e contagem anônima.
- **Fora desta entrega:**
  - pedido, checkout, pagamento e o destino real do botão → `CAP-011` (depende de OD5);
  - campanha de compra e cupom → `CAP-004` (Fase 2);
  - concessão de acesso e cortesia → `CAP-008`;
  - declarar nível e pré-requisito → `CAP-005` (segundo PRD, [prd-nivel-prerequisito-curso](../prd-nivel-prerequisito-curso/prd.md));
  - busca textual, ordenação configurável, SEO avançado → depois do MVP;
  - indicar ao aluno autenticado que ele já tem acesso a um curso → depois de `CAP-008`.
- **Domínios atravessados:**
  - Catálogo e Oferta (dono) — [domain.md](../../../domains/catalogo-e-oferta/domain.md) v1.1;
  - Conteúdo e Currículo — [domain.md](../../../domains/conteudo-e-curriculo/domain.md) v1.1, pelo
    curso publicado, sua estrutura, nível e pré-requisito;
  - Matrícula e Direito de Acesso — [domain.md](../../../domains/matricula-e-direito-de-acesso/domain.md)
    v1.0, pela forma da vigência que esta entrega promete e `CAP-008` honra;
  - Auditoria e Conformidade — [domain.md](../../../domains/auditoria-e-conformidade/domain.md) v1.2,
    pelos atos de oferta;
  - Identidade e Acesso — [domain.md](../../../domains/identidade-e-acesso/domain.md) v1.1, pela
    permissão nova.
- **Juntas entre os domínios:**
  - *Conteúdo → Catálogo (RN-O02, RN-O03).* O Catálogo referencia o curso publicado e exibe título,
    descrição pedagógica, estrutura, nível e pré-requisito **da versão vigente**; não os copia como
    seus nem os edita.
  - *Catálogo → Matrícula (RN-O08, RN-O09, RN-D03, RN-D04).* O Catálogo é dono da promessa. Ela chega
    a Matrícula congelada no pedido, nunca por consulta. Esta entrega só precisa **prometer em forma
    que o pedido possa congelar**: tipo (meses ou vitalícia) e duração em meses inteiros.
  - *Catálogo → Auditoria (RN-O15, RN-A14).* O Catálogo comunica o ato no envelope da Auditoria; a
    Auditoria grava.
- **Dependências entre capacidades:** `CAP-005` (curso publicado, `done`; nível e pré-requisito no
  segundo PRD, aprovado). Consumida por `CAP-008`, `CAP-011`, `CAP-010`, `CAP-004`.
- **Restrições do baseline:** `tenant_id` em toda entidade e consulta (G07); fato por outbox (G06);
  auditoria só por evento (G13); escrita externa idempotente (G09); o SPA fala só com o BFF (G15);
  nenhum dado pessoal em log ou métrica (G10); IP não é sinal de nada (G26).

### Vision Doc

- **Objetivos de negócio atendidos:** `C02` em versão mínima com nível; primeiro passo do fluxo
  "Descobrir → comprar → acessar" da Fase 1.
- **Restrições globais aplicáveis:** direito de acesso configurável por oferta, por período ou
  vitalício (§5); nível e pré-requisito sem gate (§5); nenhuma comunicação de venda promete
  exclusividade de conteúdo (§5, BA15); identidade visual vem do time de design (OD3 fechada:
  `DESIGN.md` e o Figma valem para a área pública).
- **Non-Goals globais respeitados:** não bloqueia compra por pré-requisito; não é ferramenta de
  campanha ou e-mail marketing.

### Domain Docs

- **Entidades envolvidas (Catálogo e Oferta):** Ficha de Vitrine, Oferta, Preço, Vigência
  Prometida, Vitrine.
- **Entidades envolvidas (Conteúdo e Currículo):** Curso, Versão de Publicação — só leitura.
- **Entidades envolvidas (Auditoria):** Ato Administrativo, Registro de Auditoria.
- **Regras de negócio referenciadas:** Catálogo RN-O01 a RN-O15, RN-O18; Conteúdo RN-C17, RN-C18;
  Matrícula RN-D03, RN-D04; Auditoria RN-A05, RN-A06, RN-A08, RN-A14; Identidade RN-12, RN-16,
  RN-18.
- **Regras precisadas por esta entrega:** nome da permissão (DP-01); limites de preço e de duração
  (DP-03); comportamento da vitrine quando a versão vigente perde o nível (DP-04).
- **Eventos produzidos:** `catalogo.oferta-publicada`, `catalogo.oferta-alterada`,
  `catalogo.oferta-despublicada`; atos `oferta-publicada`, `oferta-alterada`, `oferta-despublicada`
  no envelope `auditoria.ato-praticado`.
- **Eventos consumidos:** `conteudo.versao-publicada` (com nível e pré-requisito, do segundo PRD de
  `CAP-005`).

## Termos Canônicos

| Termo | Definição de negócio | Escopo/Fonte |
|---|---|---|
| Ficha de Vitrine | O curso no catálogo: o ponto comum às ofertas dele, com chamada comercial opcional | Catálogo RN-O03, RN-O04 |
| Oferta | Uma opção de compra do curso: nome da opção, preço e vigência prometida | Domain Map · Catálogo |
| Vigência prometida | "Acesso por N meses" (contados da liberação do acesso) ou "Acesso vitalício" | RN-O08 · RN-D04 · OD51 |
| Curso na vitrine | Curso com ao menos uma oferta publicada e com nível na versão vigente | RN-O03, RN-O12 · DP-04 |
| Chamada comercial | Frase curta de venda, opcional, escrita pelo financeiro. Não substitui a descrição pedagógica | Esta entrega |
| Intenção de compra | Clique em *Comprar* numa oferta, contado por oferta, sem identificar a pessoa | OD53 |

---

## Objetivos

- Um visitante sem conta entende, só pela vitrine, **o que se aprende, para quem é, quanto custa e
  por quanto tempo terá acesso**.
- O financeiro publica e corrige ofertas sem ajuda do time, e toda mudança visível ao público tem
  autor na trilha.
- `CAP-008` e `CAP-011` recebem uma promessa de vigência em forma única e congelável.
- A escola tem um primeiro sinal de demanda por oferta antes de a compra existir.

---

## Histórias de Usuário

- Como **financeiro**, quero criar ofertas sobre um curso publicado, com preço e vigência, para
  colocá-lo à venda.
- Como **financeiro**, quero oferecer o mesmo curso de mais de uma forma (12 meses e vitalício), para
  atender perfis diferentes de comprador.
- Como **financeiro**, quero corrigir o preço de uma oferta publicada sabendo que só vale para quem
  comprar depois, para não mudar o que já foi prometido.
- Como **financeiro**, quero tirar uma oferta da vitrine sem afetar quem já comprou.
- Como **visitante**, quero filtrar os cursos por nível, para achar o que serve para mim.
- Como **visitante**, quero ver o pré-requisito recomendado e o conteúdo do curso antes de decidir,
  para não comprar algo para o qual não estou preparado.
- Como **visitante**, quero ver claramente por quanto tempo terei acesso em cada opção.
- Como **administrador**, quero ver na trilha quem publicou, alterou ou despublicou uma oferta.

---

## Funcionalidades Principais

### RF-01: Permissão de gestão de oferta

**Descrição**: Passa a existir a permissão `oferta.editar`, concedida ao papel **financeiro**
(OD47), que abre a área **Catálogo** do backoffice e autoriza criar, alterar, publicar, despublicar
e excluir ofertas e fichas. Ninguém sem ela vê ou altera oferta no backoffice. A decisão é tomada
pelo serviço dono a partir das claims, não pelo BFF nem pela tela.

**Critérios de Aceitação**:

- **Given** um ator com o papel financeiro
  **When** as permissões dele são consultadas
  **Then** ele tem `financeiro.ler` e `oferta.editar`

- **Given** um ator com o papel professor, suporte ou administrador, sem o papel financeiro
  **When** tenta abrir a área Catálogo ou chamar qualquer operação de oferta diretamente
  **Then** é recusado, e a área não aparece no menu

- **Given** um financeiro que teve o papel revogado
  **When** tenta qualquer operação de oferta na sessão que tinha aberta
  **Then** é recusado (RN-17 de Identidade), e as ofertas que ele publicou continuam publicadas

**Prioridade**: Must Have

**Rastreabilidade**: Catálogo RN-O14; Identidade RN-12, RN-16, RN-17, RN-18; OD47; DP-03 de `CAP-002`

---

### RF-02: Ficha de Vitrine sobre curso publicado

**Descrição**: Na área Catálogo, o financeiro vê os cursos publicados da escola e, para cada um,
se já tem ficha e quantas ofertas publicadas. Abre a ficha de um curso para gerir as ofertas dele. A
ficha mostra, só para leitura, título, nível e pré-requisito da versão vigente (vindos de Conteúdo),
e permite escrever uma **chamada comercial** opcional de até 160 caracteres. Curso nunca publicado
não aparece. Curso cuja versão vigente não tem nível aparece com o aviso de que não pode ter oferta
publicada até o professor declarar o nível.

**Critérios de Aceitação**:

- **Given** a escola com um curso publicado e outro nunca publicado
  **When** o financeiro abre a área Catálogo
  **Then** vê só o curso publicado

- **Given** um curso publicado com nível intermediário
  **When** o financeiro abre a ficha
  **Then** vê o nível e o pré-requisito sem ação de alteração, e a indicação de que quem os altera é
  o professor, no curso

- **Given** um curso cuja versão vigente não tem nível
  **When** o financeiro abre a ficha
  **Then** vê o aviso de que nenhuma oferta pode ser publicada até o professor declarar o nível, e
  pode mesmo assim criar ofertas em rascunho

- **Given** uma chamada comercial com mais de 160 caracteres
  **When** o financeiro tenta salvar
  **Then** é recusada com a indicação do campo e do limite

**Prioridade**: Must Have

**Rastreabilidade**: Catálogo RN-O01, RN-O02, RN-O03, RN-O04; Conteúdo RN-C17, RN-C18

---

### RF-03: Criar e editar oferta em rascunho

**Descrição**: Na ficha, o financeiro cria uma oferta informando **nome da opção** (até 60
caracteres, ex.: "Acesso por 12 meses"), **preço** em reais e **vigência prometida**: *por período*,
com a duração em meses inteiros, ou *vitalícia*. A oferta nasce em rascunho, invisível ao público,
e pode ser editada livremente. Um curso pode ter várias ofertas (OD48). Oferta em rascunho pode ser
excluída.

**Critérios de Aceitação**:

- **Given** a ficha de um curso publicado
  **When** o financeiro cria a oferta "Acesso por 12 meses", R$ 497,00, por período de 12 meses
  **Then** a oferta aparece na ficha como *rascunho*

- **Given** a mesma ficha
  **When** o financeiro cria uma segunda oferta, "Acesso vitalício", R$ 897,00, vitalícia
  **Then** as duas coexistem

- **Given** um preço zero, negativo ou acima de R$ 99.999,99
  **When** o financeiro tenta salvar
  **Then** é recusado com a indicação do campo (RN-O07, DP-03)

- **Given** vigência por período com 0 meses, fração de mês ou mais de 60 meses
  **When** o financeiro tenta salvar
  **Then** é recusada com a indicação do campo (OD51, DP-03)

- **Given** uma oferta em rascunho
  **When** o financeiro a exclui, após confirmar
  **Then** ela deixa de existir

- **Given** a mesma criação reenviada com a mesma chave de idempotência
  **When** o serviço a recebe duas vezes
  **Then** existe uma única oferta (G09)

**Prioridade**: Must Have

**Rastreabilidade**: Catálogo RN-O04, RN-O07, RN-O08, RN-O11; OD48; OD51

---

### RF-04: Publicar oferta

**Descrição**: O financeiro publica uma oferta em rascunho. Ela passa a estar à venda na vitrine a
partir desse momento. Só publica se o curso tem versão vigente **com nível** (RN-O03). A publicação
comunica o fato `catalogo.oferta-publicada` e o ato `oferta-publicada` à Auditoria, com autor e alvo
(a oferta); motivo não é pedido. Uma oferta despublicada pode ser publicada de novo, pelo mesmo
caminho.

**Critérios de Aceitação**:

- **Given** uma oferta em rascunho de curso com nível
  **When** o financeiro publica, após confirmar o que o visitante vai ver (nome, preço, vigência)
  **Then** a oferta aparece como *publicada* no backoffice e na página pública do curso

- **Given** um curso cuja versão vigente não tem nível
  **When** o financeiro tenta publicar uma oferta dele
  **Then** a publicação é recusada com o motivo, e a oferta continua em rascunho

- **Given** uma oferta publicada
  **When** a trilha é consultada pelo administrador
  **Then** há um registro `oferta-publicada` com o financeiro como autor e a oferta como alvo, sem
  motivo e **conforme** (RN-O15, RN-A05)

- **Given** uma oferta despublicada
  **When** o financeiro a publica de novo
  **Then** ela volta à vitrine, com novo ato `oferta-publicada`

**Prioridade**: Must Have

**Rastreabilidade**: Catálogo RN-O03, RN-O13, RN-O15; Auditoria RN-A05, RN-A14; OD50

---

### RF-05: Alterar oferta publicada

**Descrição**: O financeiro pode alterar nome, preço e vigência de uma oferta publicada. A mudança
vale **só para compras futuras** (RN-O10): nada do que já foi prometido muda. Antes de confirmar, a
tela mostra o valor anterior e o novo e diz isso explicitamente. Mudança de **preço ou vigência**
comunica o fato `catalogo.oferta-alterada` e o ato `oferta-alterada` à Auditoria, com os valores
anterior e novo; mudança só de nome não é ato auditado.

**Critérios de Aceitação**:

- **Given** uma oferta publicada a R$ 497,00
  **When** o financeiro muda para R$ 397,00 e confirma
  **Then** a vitrine passa a mostrar R$ 397,00, e a trilha registra `oferta-alterada` com R$ 497,00 →
  R$ 397,00

- **Given** uma oferta publicada por 12 meses
  **When** o financeiro muda para vitalícia
  **Then** a vitrine mostra "Acesso vitalício", e o ato registra a vigência anterior e a nova

- **Given** uma oferta publicada
  **When** o financeiro muda só o nome da opção
  **Then** a vitrine mostra o novo nome, e nenhum ato é registrado na trilha

- **Given** a tela de confirmação de mudança de preço ou vigência
  **When** o financeiro a lê
  **Then** ela diz que a mudança vale para compras futuras e não altera quem já comprou

**Prioridade**: Must Have

**Rastreabilidade**: Catálogo RN-O09, RN-O10, RN-O15; OD49; OD50

---

### RF-06: Despublicar oferta

**Descrição**: O financeiro despublica uma oferta publicada. Ela sai da vitrine e deixa de aceitar
compra nova; não é apagada (RN-O11) e continua visível no backoffice como *despublicada*. Curso sem
nenhuma oferta publicada sai da vitrine. Despublicar comunica o fato `catalogo.oferta-despublicada` e
o ato `oferta-despublicada`.

**Critérios de Aceitação**:

- **Given** um curso com duas ofertas publicadas
  **When** o financeiro despublica uma
  **Then** a página do curso passa a mostrar só a outra

- **Given** um curso com uma oferta publicada
  **When** o financeiro a despublica
  **Then** o curso sai da vitrine, e o endereço público do curso mostra que ele não está disponível

- **Given** uma oferta despublicada
  **When** o financeiro tenta excluí-la
  **Then** a ação não é oferecida, e a chamada direta é recusada (RN-O11)

- **Given** uma despublicação
  **When** a trilha é consultada
  **Then** há um registro `oferta-despublicada` com autor e alvo

**Prioridade**: Must Have

**Rastreabilidade**: Catálogo RN-O11, RN-O12, RN-O15

---

### RF-07: Vitrine pública com filtro por nível

**Descrição**: Qualquer pessoa, **sem conta**, abre a vitrine da escola e vê os cursos na vitrine:
título, nível, chamada comercial (ou, sem ela, o início da descrição pedagógica) e o menor preço
entre as ofertas publicadas ("a partir de R$ 397,00" quando há mais de uma). Pode filtrar por nível
— todos, iniciante, intermediário, avançado — e o filtro fica no endereço, para ser compartilhado.
Os cursos aparecem do mais recentemente colocado na vitrine para o mais antigo. Aluno autenticado vê
a mesma vitrine (RN-O13).

**Critérios de Aceitação**:

- **Given** três cursos na vitrine, um de cada nível
  **When** o visitante filtra por *iniciante*
  **Then** vê só o curso iniciante, e o endereço reflete o filtro

- **Given** um curso com ofertas de R$ 497,00 e R$ 897,00
  **When** aparece na vitrine
  **Then** mostra "a partir de R$ 497,00"

- **Given** um curso publicado sem oferta publicada, ou cuja versão vigente não tem nível
  **When** o visitante abre a vitrine
  **Then** o curso não aparece (DP-04)

- **Given** um filtro sem nenhum curso
  **When** o visitante o aplica
  **Then** vê um estado vazio que oferece voltar para *todos*

- **Given** um curso de outra escola
  **When** a vitrine é montada
  **Then** ele nunca aparece (RN-O18)

**Prioridade**: Must Have

**Rastreabilidade**: Catálogo RN-O03, RN-O04, RN-O05, RN-O12, RN-O13, RN-O18

---

### RF-08: Página pública do curso

**Descrição**: Cada curso na vitrine tem um **endereço público estável**, que pode ser compartilhado.
A página mostra: título; nível; descrição pedagógica; **pré-requisito recomendado** — o texto e os
cursos recomendados (com link quando o recomendado está na vitrine, só o título quando não está);
a **estrutura** da versão vigente — módulos e títulos das aulas, sem vídeo; e as **opções de
compra**, uma por oferta publicada, com nome, preço e vigência em linguagem clara: "Acesso por 12
meses, contados a partir da liberação" ou "Acesso vitalício". O pré-requisito é apresentado como
recomendação, nunca como condição (DE04).

**Critérios de Aceitação**:

- **Given** um curso na vitrine com nível avançado, pré-requisito "Git e C# básico" e o curso
  recomendado *Fundamentos de C#*, também na vitrine
  **When** o visitante abre a página
  **Then** vê "Avançado", o texto e um link para *Fundamentos de C#*

- **Given** um curso recomendado que não está na vitrine
  **When** a página é aberta
  **Then** mostra o título dele, sem link

- **Given** um curso republicado pelo professor com uma aula nova
  **When** o visitante reabre a página
  **Then** vê a estrutura da nova versão vigente, sem nenhuma ação do financeiro (RN-O02)

- **Given** o endereço de um curso que saiu da vitrine, ou que não existe
  **When** é aberto
  **Then** mostra que o curso não está disponível e oferece a vitrine

- **Given** qualquer texto da página, inclusive a chamada comercial
  **When** é exibido
  **Then** nenhum trecho gerado pela plataforma promete exclusividade de conteúdo ou proteção contra
  cópia (RN-O06)

**Prioridade**: Must Have

**Rastreabilidade**: Catálogo RN-O02, RN-O05, RN-O06, RN-O08, RN-O13; Conteúdo RN-C18; Matrícula RN-D04

---

### RF-09: Botão de compra com aviso e contagem anônima

**Descrição**: Cada opção de compra tem um botão **Comprar**. Enquanto `CAP-011` não existe, o clique
abre um aviso de que a compra estará disponível em breve, sem pedir dado nenhum, e soma **um** à
contagem daquela oferta. A contagem não guarda quem clicou, nem identificador de navegador, nem IP
(G10, G26). No backoffice, o financeiro vê, em cada oferta, o total de cliques desde a publicação.

**Critérios de Aceitação**:

- **Given** a página de um curso com duas ofertas
  **When** o visitante clica em *Comprar* na oferta vitalícia
  **Then** vê o aviso de compra em breve, e a contagem da oferta vitalícia aumenta em um

- **Given** a contagem registrada
  **When** é inspecionada
  **Then** contém só oferta, escola e momento — nenhum identificador de pessoa, sessão, navegador ou
  IP

- **Given** uma oferta com 14 cliques
  **When** o financeiro abre a ficha
  **Then** vê "14 cliques em Comprar" na oferta

- **Given** uma oferta despublicada
  **When** alguém tenta registrar clique nela por chamada direta
  **Then** o clique não é contado

**Prioridade**: Must Have

**Rastreabilidade**: OD53; G10; G26

---

### RF-10: Rótulos dos novos atos na consulta da trilha

**Descrição**: A consulta da trilha (`CAP-030`) passa a mostrar os três novos tipos com rótulos em
português — *Oferta publicada*, *Oferta alterada*, *Oferta despublicada* — e, no detalhe de
*Oferta alterada*, os valores anterior e novo. O administrador pode filtrar por esses tipos como
pelos demais.

**Critérios de Aceitação**:

- **Given** um ato `oferta-alterada` na trilha
  **When** o administrador abre o detalhe
  **Then** vê "Oferta alterada", o autor, a oferta e o preço ou a vigência anterior e novo

- **Given** o filtro por tipo da consulta
  **When** o administrador o abre
  **Then** os três tipos novos estão disponíveis

**Prioridade**: Must Have

**Rastreabilidade**: Auditoria RN-A14; OD50; C-01/C-08 de `CAP-005` como precedente

---

## Experiência do Usuário

**Personas.**
- *Financeiro:* monta a oferta uma vez e mexe em preço raramente; precisa ter certeza do que o
  público vai ver e de que corrigir não quebra quem comprou.
- *Visitante:* chega por link compartilhado ou pela vitrine, muitas vezes pelo celular; decide entre
  cursos e entre opções do mesmo curso.

**Fluxo do financeiro.** Backoffice → **Catálogo** → lista de cursos publicados (com ou sem oferta,
com ou sem nível) → abre a ficha → *Nova oferta* (nome, preço, vigência) → *Publicar* → confirmação
mostra exatamente o cartão que o visitante verá → publicada. Alterar preço ou vigência abre
confirmação com antes/depois e a frase "vale para compras futuras".

**Fluxo do visitante.** Vitrine → filtro por nível → cartão do curso → página do curso → lê
pré-requisito, estrutura e opções → *Comprar* → aviso de compra em breve.

**Linguagem.** Vigência sempre explícita e com o ponto de partida ("contados a partir da
liberação"). Pré-requisito como "Recomendamos saber antes". Nenhuma promessa de exclusividade ou de
proteção (RN-O06). Preço em reais com centavos, sem parcelamento nesta entrega.

**Design.** Área pública sobre `DESIGN.md` e o Figma do design system (OD3); backoffice sobre o
design system do backoffice. Fluxo: wireframe ASCII → Figma → aprovação → código.

**Acessibilidade.** Filtro como grupo de opções com rótulo e estado anunciado; preço e vigência em
texto, não só em destaque visual; aviso de compra em breve focável e anunciado; página do curso
navegável por títulos.

## Decisões de Produto

| ID | Decisão confirmada | Alternativas descartadas e motivo | Impacto no PRD | Registro |
|---|---|---|---|---|
| DP-01 | **Permissão `oferta.editar`, concedida ao financeiro**, abre a área Catálogo e cobre criar, alterar, publicar e despublicar | Papel novo "comercial" e administrador: ver OD47 | RF-01 | OD47 |
| DP-02 | **Botão de compra com aviso e contagem anônima por oferta** até `CAP-011` | Exigir login e registrar interesse; botão desabilitado: ver OD53 | RF-09 | OD53 |
| DP-03 | **Limites:** preço de R$ 0,01 a R$ 99.999,99; vigência por período de 1 a 60 meses inteiros; nome da opção até 60 caracteres; chamada comercial até 160 | Sem limite: erro de digitação (um zero a mais) iria direto à vitrine | RF-02, RF-03 | — |
| DP-04 | **Curso some da vitrine se a versão vigente perder o nível**, sem despublicar as ofertas; a ficha avisa o financeiro | Despublicar as ofertas automaticamente: geraria atos sem autor humano e exigiria republicar depois | RF-02, RF-07 | — |
| DP-05 | **Mudança só do nome da opção não é ato auditado**; preço e vigência são | Auditar qualquer mudança: poluiria a trilha com correção de texto (risco de fronteira da Auditoria) | RF-05 | OD50 |

Herdadas e não reabertas: várias ofertas por curso (OD48); vigência em meses (OD51) congelada no
pedido (OD49); nível e pré-requisito do curso (OD54); publicar, despublicar e alterar preço ou
vigência são atos auditados, motivo opcional (OD50).

---

## Restrições Técnicas de Alto Nível

- Serviços com mudança: `commerce` (módulo Catálogo, primeiro conteúdo de negócio do serviço),
  `bff-admin`/`admin-spa` (área Catálogo e rótulos da trilha), `bff-student`/`student-spa` (vitrine
  e página públicas, **sem autenticação**), `identity` (permissão nova no catálogo de permissões),
  `audit` (três tipos de ato novos, aditivos, como C-01 de `CAP-005`), e o dono de Conteúdo (a
  leitura de nível e pré-requisito já prevista no segundo PRD de `CAP-005`).
- A área pública é a primeira superfície anônima de leitura de catálogo: precisa suportar leitura
  frequente sem expor dado interno (autor, identificadores de pessoa) nem dado de outra escola.
- Evidência de ordem de implantação: `audit` com os tipos novos antes de o Catálogo publicar o
  primeiro ato, senão o ato chega não conforme (precedente C-01 de `CAP-005`).

---

## Não-Objetivos (Fora de Escopo)

- Pedido, checkout, pagamento, parcelamento (`CAP-011`).
- Campanha de compra e cupom (`CAP-004`).
- Concessão de acesso, cortesia, "você já tem este curso" (`CAP-008`).
- Declarar ou editar nível e pré-requisito no Catálogo (OD54).
- Bloquear ou desencorajar compra por nível ou pré-requisito (DE04).
- Imagem de capa do curso, vídeo de apresentação, aula gratuita de amostra.
- Duração total do curso ou das aulas na página pública.
- Busca textual, ordenação escolhida pelo visitante, SEO além de título e descrição da página.
- Coleta de e-mail ou lista de espera no botão de compra.

---

## Plano de Rollout Faseado

### MVP (Fase 1)

- **Funcionalidades incluídas:** RF-01 a RF-10.
- **Critério para liberar `CAP-008`:** um curso real na vitrine com ao menos uma oferta por período
  e uma vitalícia publicadas, e os três atos na trilha.

### Depois desta entrega

- `CAP-011` troca o destino do botão de compra e congela oferta e vigência no pedido.
- `CAP-004` acrescenta campanha e cupom sobre a oferta, sem editá-la (RN-O16).

---

## Métricas de Sucesso

| Métrica | Definição | Alvo | Prazo |
|---|---|---|---|
| Mudança pública sem autor | Publicações, despublicações e alterações de preço ou vigência sem ato correspondente na trilha | 0 | Desde a primeira oferta publicada |
| Curso vendável fora da vitrine | Cursos com oferta publicada e nível na versão vigente que não aparecem na vitrine | 0 | Contínuo |
| Sinal de demanda | Cliques em *Comprar* por oferta publicada, por semana | Medido (sem alvo: A1/OD2 em aberto) | Desde a primeira oferta publicada |
| Primeira oferta sem ajuda | Oferta real publicada pelo financeiro sem ajuda do time | 1 | Antes de iniciar `CAP-008` |

---

## Riscos e Mitigações

- **Visitante tenta comprar e encontra "em breve"**, o que frustra quem chega por link. —
  Mitigação: só divulgar a vitrine quando `CAP-011` estiver perto; a contagem mede o tamanho desse
  efeito.
- **Chamada comercial com promessa de exclusividade ou proteção** (R8). — Mitigação: orientação na
  tela; a plataforma não gera essa promessa (RN-O06); revisão pela escola.
- **Preço errado publicado.** — Mitigação: confirmação mostra o cartão exato; limites de DP-03;
  alterar vale só para o futuro e fica na trilha.
- **Venda sem NFS-e** assim que `CAP-011` existir (R13). — Não é risco desta entrega, mas a vitrine
  é o primeiro passo visível dessa venda.

---

## Alternativas Consideradas

### Abordagem Escolhida: oferta editável com promessa congelada no pedido

- **Descrição:** RF-01 a RF-10. A oferta é editável depois de publicada; o que já foi comprado não
  muda porque o pedido congela a condição (OD49).
- **Por que foi escolhida:** simples para o financeiro e segura para o comprador, sem versionar a
  oferta.

### Alternativa Rejeitada 1: oferta imutável depois de publicada, mudança = nova oferta

- **Trade-offs:** histórico explícito de cada condição.
- **Por que foi rejeitada:** a proteção do comprador já vem do pedido; exigir nova oferta para
  corrigir um preço multiplica ofertas e confunde a vitrine.

### Alternativa Rejeitada 2: vitrine só para aluno autenticado

- **Trade-offs:** mais sinal de demanda identificado.
- **Por que foi rejeitada:** a persona Visitante da visão avalia antes de ter conta; exigir conta
  para ver preço é atrito na única superfície que converte.

---

## Questões em Aberto

Nenhuma bloqueante. Próxima etapa: contratos (`commerce` Catálogo, BFFs, `identity`, `audit`) com
`tsg-flow-contract-creator`, junto com os do segundo PRD de `CAP-005`.
