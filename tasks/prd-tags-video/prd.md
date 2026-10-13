---
tsg_artifact: prd
product: code-4-coders
capability: CAP-006
version: 1.0
status: approved
updated: 2026-10-11
sources: backlog/capabilities.md@1.4, vision.md@1.2, context/domain-map.md@1.2, context/architecture-baseline.md@1.2, domains/entrega-de-midia-e-protecao/domain.md@1.1, domains/conteudo-e-curriculo/domain.md@1.1
---

# Tags de vídeo: achar o vídeo certo quando a biblioteca cresce

## Visão Geral

A tela **Vídeos** e o **seletor de vídeo da aula** (Autoria) encontram um vídeo só pelo trecho do
título. Isso bastava com poucos vídeos. Com a biblioteca da escola crescendo, o professor precisa
lembrar o título exato ou rolar a lista inteira para achar o vídeo que quer vincular. Títulos
parecidos entre cursos ("Aula 3 — Introdução") pioram a busca.

Esta entrega permite **rotular cada vídeo com tags** e usar essas tags como mais um filtro, junto
com o trecho do título, na tela Vídeos e no seletor da aula. A tag é livre: quem rotula digita, o
campo sugere as tags que a escola já usa e uma tag nova nasce no momento do uso.

Quem usa: o ator interno com a permissão `midia.enviar` (professor), que envia, rotula e vincula
vídeos. O valor é menos tempo e menos erro ao montar o currículo, sobretudo ao vincular o vídeo
errado por causa de títulos parecidos.

## Rastreabilidade

### Capacidade e fronteiras

- **Capacidade:** `CAP-006` — Ingestão e preparação de mídia protegida.
- **Escopo desta entrega:** terceira fatia de `CAP-006`. Tags livres por vídeo, atribuídas no
  envio e editáveis na tela Vídeos, e filtro por tags (com combinação **E** ou **OU**) na tela
  Vídeos e no seletor de vídeo da aula.
- **Fora desta entrega:** gestão das tags da escola (renomear, mesclar ou excluir uma tag de todos
  os vídeos de uma vez), tags em Material, tags em curso ou aula, e qualquer uso das tags fora do
  backoffice. Voltam em fatia futura de `CAP-006` se o vocabulário da escola ficar sujo a ponto de
  atrapalhar a busca.
- **Domínios atravessados:**
  - Entrega de Mídia e Proteção — `domains/entrega-de-midia-e-protecao/domain.md` (dono da Mídia e
    das suas tags).
  - Conteúdo e Currículo — `domains/conteudo-e-curriculo/domain.md` (o seletor de vídeo da aula
    consome a busca; nada muda no vínculo).
- **Junta entre os domínios:** a tag é atributo da Mídia e pertence a Entrega de Mídia e Proteção.
  Conteúdo e Currículo só consulta vídeos prontos para vínculo (junta síncrona existente, RN-C04) e
  passa a poder filtrar essa consulta por tag. Conteúdo e Currículo não guarda tag, e a tag não
  participa do vínculo, da publicação nem da Referência de Uso.
- **Dependências entre capacidades:** `CAP-002` (permissão `midia.enviar`); `CAP-005` (seletor de
  vídeo da aula, que ganha o filtro).
- **Restrições do baseline:** isolamento por escola (G07): tag, sugestão e filtro nunca cruzam
  escolas.

### Vision Doc

- **Objetivos de negócio atendidos:** `C03` e `C04`. Publicar curso com menos atrito operacional
  conforme a biblioteca de vídeos cresce.
- **Restrições globais aplicáveis:** multi-escola (tenant) desde o início.
- **Non-Goals globais respeitados:** nenhuma funcionalidade voltada ao aluno; nada muda na
  proteção ou na entrega do vídeo.

### Domain Docs

- **Entidades envolvidas:** Mídia (Entrega de Mídia e Proteção); Aula (Conteúdo e Currículo, só
  como ponto de uso do seletor).
- **Regras de negócio referenciadas:** RN-M02, RN-M03, RN-M04, RN-M05 (Mídia); RN-C04 (Conteúdo).
- **Regras nascidas neste PRD:** RN-T01 a RN-T06 (abaixo). Devem ser absorvidas pelo domain doc
  de Entrega de Mídia e Proteção na próxima revisão (como RN-M18 em diante).
- **Eventos consumidos / produzidos:** nenhum. Tag não é fato de negócio para outro domínio.

#### Regras desta entrega

| ID | Regra | Origem |
|---|---|---|
| RN-T01 | **Tag é um rótulo livre de Mídia, da escola.** Não tem significado para a plataforma: o domínio não relaciona tag a curso, aula ou autor, e nenhuma decisão de acesso, vínculo ou publicação depende dela | RN-M02 · DP-01 |
| RN-T02 | **Tags são equivalentes quando diferem só em maiúsculas, acentos ou espaços nas pontas e repetidos.** "Docker", "docker" e " DOCKER " são a mesma tag. A grafia exibida é a da primeira vez que a tag foi usada na escola | DP-01 |
| RN-T03 | **Uma tag tem de 1 a 30 caracteres** depois de removidos os espaços das pontas. **Um vídeo tem no máximo 10 tags** e nunca a mesma tag duas vezes (RN-T02) | DP-05 |
| RN-T04 | **Qualquer ator com `midia.enviar` atribui e remove tags de qualquer vídeo da escola, em qualquer estado.** Mesma regra do título | RN-M03 · RN-M04 · DP-03 de `prd-ingestao-midia` |
| RN-T05 | **A tag existe na escola enquanto pelo menos um vídeo a usa.** Quando o último vídeo deixa de usá-la, ela some das sugestões e dos filtros | DP-04 |
| RN-T06 | **Tag, sugestão e filtro respeitam a escola do ator.** Nunca se vê, sugere ou filtra por tag de outra escola | G07 |

## Termos Canônicos

| Termo | Definição de negócio | Escopo/Fonte |
|---|---|---|
| Tag | Rótulo livre que o professor dá a um vídeo para achá-lo depois. Não é categoria, pasta nem curso | Esta entrega |
| Tags da escola | Conjunto das tags usadas em pelo menos um vídeo da escola. É de onde vêm as sugestões e as opções de filtro | Esta entrega (RN-T05) |
| Combinação **E** / **OU** | Com duas ou mais tags no filtro: **E** traz o vídeo que tem todas elas; **OU** traz o vídeo que tem pelo menos uma | Esta entrega (DP-03) |

## Objetivos

- O professor encontra o vídeo a vincular **sem depender do título exato**, filtrando pelas tags
  que ele mesmo ou um colega atribuiu.
- O vocabulário de tags da escola **converge sozinho**: a sugestão de tags existentes faz o
  professor reaproveitar a tag em vez de criar variações.
- Nenhuma mudança no comportamento do vínculo, da publicação ou da reprodução.

## Histórias de Usuário

- Como **professor**, eu quero informar tags ao enviar um vídeo para que ele já entre organizado
  na biblioteca.
- Como **professor**, eu quero adicionar ou remover tags de um vídeo já enviado para que eu
  organize a biblioteca que existia antes das tags ou corrija um rótulo errado.
- Como **professor**, ao digitar uma tag, eu quero ver as tags que a escola já usa para que eu
  reaproveite "docker" em vez de criar "Docker-compose" quando a intenção era a mesma.
- Como **professor montando uma aula**, eu quero filtrar o seletor de vídeo por tag para que eu
  ache o vídeo certo entre dezenas de prontos com títulos parecidos.
- Como **professor**, com várias tags no filtro, eu quero escolher entre "tem todas" (E) e "tem
  qualquer uma" (OU) para que eu possa tanto estreitar quanto ampliar a busca.
- Como **professor**, eu quero combinar tag com trecho do título e com o estado do vídeo na tela
  Vídeos para que eu ache, por exemplo, os vídeos com a tag "módulo 2" que falharam.

## Funcionalidades Principais

### RF-01: Tags no envio do vídeo

**Descrição**: O formulário de envio passa a ter um campo opcional de tags. O professor digita,
recebe sugestões das tags da escola (RF-03) e confirma cada tag. As tags valem para o vídeo
criado por esse envio. O título continua obrigatório como hoje; tags não são.

**Critérios de Aceitação**:

- **Given** o professor preenche título e as tags "docker" e "módulo 2"
  **When** o envio é concluído
  **Then** o vídeo aparece na tela Vídeos com essas duas tags, em qualquer estado da preparação.

- **Given** o professor não informa nenhuma tag
  **When** envia o vídeo
  **Then** o envio segue como hoje e o vídeo fica sem tags.

- **Given** o professor tenta incluir a 11ª tag, uma tag com mais de 30 caracteres ou uma tag só
  de espaços
  **When** confirma a tag
  **Then** a tag não é incluída e o campo explica o motivo (limite de 10 tags, limite de 30
  caracteres, tag vazia).

- **Given** o professor já incluiu "Docker"
  **When** tenta incluir "docker" ou "DOCKER "
  **Then** a tag não é duplicada (RN-T02).

- **Given** o envio falha e o vídeo não é criado
  **When** o professor tenta de novo
  **Then** as tags precisam ser informadas no novo envio; nada de tag fica órfão na escola.

**Prioridade**: Must Have

**Rastreabilidade**: RN-T02, RN-T03, RN-T04

---

### RF-02: Editar as tags de um vídeo na tela Vídeos

**Descrição**: Na tela Vídeos, cada vídeo mostra suas tags e permite adicionar e remover tags,
com as mesmas sugestões e limites do envio. Vale para vídeo em qualquer estado, inclusive
recebido, em preparação e falhou. Editar tags não muda título, estado nem vínculos do vídeo.

**Critérios de Aceitação**:

- **Given** um vídeo pronto, vinculado a uma aula publicada, com a tag "docker"
  **When** o professor adiciona "kubernetes" e remove "docker"
  **Then** o vídeo passa a ter só "kubernetes", e a aula e a versão publicada não mudam.

- **Given** um vídeo enviado por outro professor da mesma escola
  **When** o professor edita as tags dele
  **Then** a edição é aceita (RN-T04).

- **Given** um ator sem `midia.enviar`
  **When** tenta editar tags de um vídeo
  **Then** a edição é recusada, como hoje acontece com o título.

- **Given** um vídeo de outra escola
  **When** o professor tenta editar suas tags por qualquer caminho
  **Then** a resposta é a mesma de vídeo inexistente (RN-T06).

- **Given** dois professores editam as tags do mesmo vídeo quase ao mesmo tempo
  **When** ambos salvam
  **Then** nenhum dos dois vê o vídeo num estado misturado sem saber: ou a segunda edição é
  recusada com aviso para recarregar, ou ela prevalece por inteiro; a escolha é da TechSpec, mas
  o resultado exibido após salvar é sempre o estado real do vídeo.

- **Given** "docker" estava só neste vídeo
  **When** o professor a remove
  **Then** "docker" deixa de aparecer nas sugestões e nos filtros da escola (RN-T05).

**Prioridade**: Must Have

**Rastreabilidade**: RN-T03, RN-T04, RN-T05, RN-T06

---

### RF-03: Sugestão de tags da escola

**Descrição**: Ao digitar uma tag (no envio, na edição ou no filtro), o campo sugere as tags da
escola que contêm o trecho digitado, ignorando maiúsculas e acentos. Cada sugestão mostra a
grafia canônica (RN-T02). No envio e na edição, confirmar um texto sem sugestão correspondente
cria a tag nova; no filtro, só é possível escolher tags existentes.

**Critérios de Aceitação**:

- **Given** a escola usa "Docker" e "Docker Compose"
  **When** o professor digita "dock"
  **Then** as duas aparecem como sugestão.

- **Given** a escola usa "Introdução"
  **When** o professor digita "introducao" e confirma
  **Then** o vídeo recebe "Introdução", e nenhuma tag nova é criada (RN-T02).

- **Given** a escola não usa nenhuma tag com "terraform"
  **When** o professor digita "Terraform" no envio e confirma
  **Then** "Terraform" passa a existir na escola e a ser sugerida para os próximos vídeos.

- **Given** "terraform" só existe em outra escola
  **When** o professor desta escola digita "terra"
  **Then** nada é sugerido (RN-T06).

**Prioridade**: Must Have

**Rastreabilidade**: RN-T02, RN-T05, RN-T06

---

### RF-04: Filtro por tags na tela Vídeos

**Descrição**: A tela Vídeos ganha um filtro por uma ou mais tags da escola, que se combina com
o filtro de estado e com a busca por trecho do título já existentes (todos os critérios valem ao
mesmo tempo). Com duas ou mais tags no filtro, o professor escolhe a combinação **E** (padrão) ou
**OU** (RF-06). Os vídeos listados mostram suas tags.

**Critérios de Aceitação**:

- **Given** vídeos A ("docker", "módulo 2"), B ("docker") e C ("módulo 2")
  **When** o professor filtra por "docker" e "módulo 2" com **E**
  **Then** só A aparece.

- **Given** os mesmos vídeos
  **When** o professor troca para **OU**
  **Then** A, B e C aparecem.

- **Given** o filtro por "docker", o estado "falhou" e o trecho "aula 3"
  **When** a lista é carregada
  **Then** aparecem só vídeos que atendem aos três critérios.

- **Given** nenhum vídeo atende ao filtro
  **When** a lista é carregada
  **Then** a tela informa que nenhum vídeo atende aos filtros e oferece limpar os filtros.

- **Given** um filtro por tag aplicado e vídeos ainda em preparação
  **When** a tela atualiza o estado sozinha, como hoje
  **Then** o filtro continua aplicado.

**Prioridade**: Must Have

**Rastreabilidade**: RN-T05, RN-T06

---

### RF-05: Filtro por tags no seletor de vídeo da aula

**Descrição**: O seletor de vídeo da aula, na Autoria, ganha o mesmo filtro por tags (com E/OU),
combinado com a busca por trecho do título. O seletor continua oferecendo **só vídeos prontos**
da escola (RN-C04); a tag só estreita ou amplia dentro desse conjunto. Cada vídeo do seletor
mostra suas tags, para o professor distinguir vídeos de títulos parecidos.

**Critérios de Aceitação**:

- **Given** os vídeos prontos A ("docker") e B ("kubernetes"), e o vídeo C ("docker") em preparação
  **When** o professor filtra o seletor por "docker"
  **Then** só A aparece; C não é oferecido por não estar pronto.

- **Given** duas aulas com vídeos "Aula 3 — Introdução" de cursos diferentes, com tags distintas
  **When** o professor abre o seletor
  **Then** as tags aparecem junto de cada título e permitem diferenciá-los.

- **Given** o professor escolhe um vídeo achado pelo filtro de tag
  **When** confirma o vínculo
  **Then** o vínculo segue as regras de hoje; a tag não é registrada na aula nem no curso.

- **Given** a tag escolhida só existe em vídeos que ainda não estão prontos
  **When** o filtro é aplicado no seletor
  **Then** o seletor informa que nenhum vídeo pronto atende aos filtros.

**Prioridade**: Must Have

**Rastreabilidade**: RN-C04, RN-T01, RN-T06

---

### RF-06: Combinação E / OU entre tags

**Descrição**: Sempre que houver duas ou mais tags no filtro (tela Vídeos ou seletor), o
professor alterna entre **E** (o vídeo tem todas as tags) e **OU** (o vídeo tem pelo menos uma).
**E** é o padrão. Com uma só tag, a alternância não aparece. A combinação vale apenas entre as
tags; tag, estado e título continuam sempre combinados entre si como "todos ao mesmo tempo".

**Critérios de Aceitação**:

- **Given** uma tag no filtro
  **When** o professor olha o filtro
  **Then** não há escolha de combinação.

- **Given** duas tags no filtro e nenhuma escolha feita
  **When** a lista é carregada
  **Then** a combinação aplicada é **E**, e a tela mostra isso.

- **Given** a combinação **OU** escolhida com três tags
  **When** o professor remove tags até sobrar uma
  **Then** o resultado é o da tag restante, e a escolha some.

**Prioridade**: Must Have

**Rastreabilidade**: DP-03

## Experiência do Usuário

- **Envio:** o campo de tags fica abaixo do título, opcional, com as tags confirmadas exibidas
  como etiquetas removíveis. Enter (ou vírgula) confirma a tag digitada; a lista de sugestões é
  navegável por teclado.
- **Tela Vídeos:** cada linha mostra as tags do vídeo. A edição acontece no próprio vídeo, sem
  sair da tela. O filtro por tags fica junto dos filtros de estado e de título.
- **Seletor da aula:** filtro por tags ao lado da busca por título; cada vídeo listado mostra
  suas tags.
- **Acessibilidade:** campo de tags e sugestões operáveis só por teclado; etiquetas com rótulo
  acessível de remoção ("Remover tag docker"); a escolha E/OU é um controle com estado anunciado.
- **Design:** as telas alteradas (envio, tela Vídeos e seletor da aula) passam por wireframe ASCII
  e Figma aprovados antes do código, como nas entregas anteriores do SPA admin.

## Decisões de Produto

| ID | Decisão confirmada | Alternativas descartadas e motivo | Impacto no PRD | Registro |
|---|---|---|---|---|
| DP-01 | **Tag livre com sugestão das tags da escola**; tag nova nasce no uso. Equivalência ignora maiúsculas, acentos e espaços | Catálogo gerenciado (tag criada antes numa tela própria): vocabulário mais limpo, mas um passo a mais e uma tela nova para um ganho que a sugestão já entrega na maior parte | RF-01, RF-02, RF-03; RN-T01, RN-T02 | — |
| DP-02 | **Tags no envio (opcional) e editáveis na tela Vídeos**, por qualquer ator com `midia.enviar` | Só na tela Vídeos: obriga voltar a cada vídeo depois do envio | RF-01, RF-02; RN-T04 | — |
| DP-03 | **Filtro por tag na tela Vídeos e no seletor da aula, com E/OU escolhível; E é o padrão** | Só E: impede ampliar a busca; só OU: impede estreitar; só no seletor: a tela Vídeos perderia o mesmo ganho | RF-04, RF-05, RF-06 | — |
| DP-04 | **Sem gestão de tags da escola nesta fatia.** Remove-se tag de cada vídeo; tag sem vídeo some | Renomear/mesclar/excluir em massa: tela e regras novas antes de saber se o vocabulário vai sujar | Não-objetivos; RN-T05 | — |
| DP-05 | **Limites: até 10 tags por vídeo, 1 a 30 caracteres por tag** | Sem limite: abre espaço para usar a tag como descrição e polui sugestões | RF-01, RF-02; RN-T03 | — |
| DP-06 | **Editar tags não entra na trilha de auditoria**, como a edição de título | Auditar: o ato não afeta acesso, dinheiro nem conteúdo publicado | Não-objetivos | — |

## Não-Objetivos (Fora de Escopo)

- Renomear, mesclar ou excluir uma tag de todos os vídeos de uma vez; tela de gestão de tags.
- Tags em Material, curso, módulo ou aula; herdar tags do vídeo para a aula.
- Hierarquia, cor, categoria ou descrição de tag.
- Busca por tag para o aluno, na vitrine ou na reprodução.
- Aplicar ou remover tags em vários vídeos de uma vez.
- Sugerir tags automaticamente a partir do título ou do conteúdo do vídeo.
- Copiar as tags de um vídeo que falhou para o reenvio (o reenvio é um novo vídeo, RN-M05).
- Registro de auditoria da edição de tags (DP-06).
- Carga inicial de tags nos vídeos existentes: eles começam sem tags e são rotulados pela tela
  Vídeos (não há dados legados a migrar até o MVP).

## Plano de Rollout Faseado

### Fase única

- **Funcionalidades incluídas:** RF-01 a RF-06.
- **Pré-requisito de código:** wireframe ASCII e Figma aprovados para envio, tela Vídeos e
  seletor da aula.
- **Critério de conclusão:** em dev, um professor rotula vídeos no envio e na tela Vídeos, filtra
  por tags com E e OU nas duas telas e vincula um vídeo achado pelo filtro, sem mudança no
  comportamento de vínculo e publicação.

## Métricas de Sucesso

| Métrica | Definição | Alvo | Prazo |
|---|---|---|---|
| Vídeos prontos rotulados | % de vídeos prontos da escola com pelo menos uma tag | ≥ 70% | 30 dias após a entrega |
| Uso do filtro no seletor | % de vínculos de vídeo feitos com filtro por tag aplicado no seletor | ≥ 30% | 30 dias após a entrega |
| Convergência do vocabulário | % de tags atribuídas que já existiam na escola (escolhidas da sugestão) | ≥ 60% | 30 dias após a entrega |

## Riscos e Mitigações

- **Adoção baixa nos vídeos já existentes:** a biblioteca antiga fica sem tags e o filtro não
  ajuda. — Mitigação: edição rápida na própria tela Vídeos; filtro por estado e título continua
  funcionando como hoje.
- **Vocabulário sujo** ("docker", "Docker 101", "dockerfile" para a mesma coisa). — Mitigação:
  sugestão sempre visível e equivalência por maiúsculas/acentos; a métrica de convergência mostra
  se a gestão de tags (DP-04) precisa voltar.
- **Tag usada como nome de curso** ("Curso Kubernetes 2026"), tentando aproximar Mídia de
  Conteúdo. — Mitigação: RN-T01; a tag não tem significado para a plataforma e nada é decidido
  por ela.

## Alternativas Consideradas

### Abordagem Escolhida: tag livre por vídeo com sugestão, filtro E/OU nas duas telas

- **Descrição:** rótulo livre na Mídia, atribuído no envio ou na tela Vídeos, sugerido a partir
  das tags da escola e usado como filtro combinável com título e estado.
- **Por que foi escolhida:** resolve a busca com o menor atrito, sem tela nova de gestão e sem
  tocar no vínculo nem na publicação.

### Alternativa Rejeitada 1: catálogo gerenciado de tags

- **Descrição:** tags criadas antes, numa tela própria; ao rotular só se escolhe da lista.
- **Trade-offs:** vocabulário limpo × um passo a mais e uma tela nova.
- **Por que foi rejeitada:** a sugestão entrega a maior parte da limpeza sem o custo; a gestão
  pode vir depois se a métrica de convergência mostrar necessidade.

### Alternativa Rejeitada 2: pastas ou coleções de vídeos

- **Descrição:** organizar a biblioteca em pastas, com cada vídeo numa pasta.
- **Trade-offs:** navegação familiar × um vídeo reutilizado em dois cursos não cabe em duas pastas.
- **Por que foi rejeitada:** a tag permite vários rótulos por vídeo e serve de filtro, que é o
  problema relatado.

## Questões em Aberto

- **Concorrência na edição de tags (RF-02):** recusar a segunda edição com aviso ou deixar a
  última prevalecer por inteiro. — Responde: TechSpec. Impacto se não resolvido: nenhum no
  escopo de produto; o critério de aceite já exige que a tela mostre o estado real após salvar.
