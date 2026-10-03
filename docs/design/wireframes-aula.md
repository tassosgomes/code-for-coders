# Wireframes ASCII — Tela da aula do aluno (CAP-007, 1º PRD)

> **Status:** ASCII aprovado pelo responsável em 2026-10-03
> **Figma:** [Índice de revisão](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=260-2332) · frames por tela na [seção 8](#8-handoff-do-figma--aguardando-aprovação) · arquivo Code4Coders — Design System.
> **Handoff:** aprovado o ASCII → desenho no Figma (task 2.0) → aprovação → código de tela (tasks 3.0 em diante). Pedido de ajuste volta a este documento antes de qualquer registro.
> **Objetivo:** definir fluxos, conteúdo e estados da **tela da aula** (`student-spa`) antes do desenho no Figma.
> **Fontes:** [PRD](../../tasks/prd-reproducao-protegida/prd.md) v1.0 (Experiência do Usuário, RF-01 a RF-07, RN-R01 a RN-R08, DP-01 a DP-10), [TechSpec](../../tasks/prd-reproducao-protegida/techspec.md) v1.0 (Bloco Frontend, V-01 a V-06, Habilitadores inevitáveis, D-05, D-06, D-11, D-13), [contrato do BFF do aluno](../../tasks/prd-reproducao-protegida/api-contract.md) 1.0.0 ([YAML](../../tasks/prd-reproducao-protegida/api-contract.yaml)), [wireframes de Cortesias](wireframes-cortesias.md) (formato do documento e do registro de aprovação), [wireframes do Catálogo e vitrine](wireframes-catalogo-vitrine.md) e [wireframes da conta do aluno](wireframes-conta-aluno.md) (design system do aluno, layout público, AppShell logado), [componentes](Components.md) e [Design System](../../DESIGN.md).

---

## 1. Inventário

### 1.1 O que o PRD entrega em tela

| Entrega | RF | Tela ou efeito visível |
|---|---|---|
| Abrir a aula com decisão de acesso | RF-01 | A1 tela da aula: título, player, lista de aulas; estados de acesso e de aula |
| Sessão individual e renovação invisível | RF-02 | A1 em reprodução contínua; parada por fim de direito ou por indisponibilidade até o fim da validade |
| Marca d'água com o e-mail | RF-04 | E-mail sobre o vídeo, em zonas que mudam; aviso fixo de uso pessoal sob o player |
| Player essencial | RF-05 | Tocar e pausar, linha do tempo, volume, velocidade 0,5x–2x, tela cheia, qualidade automática |
| Lista de aulas e troca de aula | RF-06 | Módulos e aulas na ordem do currículo, aula atual destacada; escolha abre a outra aula do início |
| Entrada e retorno pelo login | RF-01 | Sem login: leva ao login e volta à mesma aula depois de entrar |

### 1.2 Tela (`student-spa`, base `/student/`; origem `http://localhost:8082` no Compose)

| # | Tela | Rota | Estados principais |
|---|---|---|---|
| A1 | Tela da aula | `/student/aulas/:lessonId` (rota do SPA: `/aulas/:lessonId`) | carregando · reproduzindo · pausado · sem acesso · acesso encerrado · indisponibilidade da decisão (com *Tentar de novo*) · aula não disponível · vídeo indisponível · não foi possível iniciar · navegador sem suporte · reprodução interrompida (direito encerrado / indisponibilidade até o fim da validade) · renovação em curso (invisível) |
| L1 | Entrar (existente) | `/student/entrar` | retorno à aula original depois de entrar |

Sem login por link direto da aula → L1 com retorno. Rota direta de aula negada → A1 no estado correspondente, sem revelar conteúdo.

### 1.3 Decisões de desenho propostas (para aprovação)

| # | Ponto | Proposta e fundamento |
|---|---|---|
| G1 | Estrutura da tela | Cabeçalho do aluno + título da aula (H1) + área do player 16:9 + aviso fixo de uso pessoal sob o player + lista de módulos e aulas. Desktop 1440: lista ao lado do player (coluna direita, 360). Celular 390: lista abaixo do player, na mesma ordem |
| G2 | Aula atual | Destacada na lista com fundo `bg-secondary` + indicador de reprodução (▶) + texto "Aula atual" (nunca só cor). A lista é navegável como lista (`ul`/`li`), com o estado atual anunciado em texto |
| G3 | Player próprio | Controles próprios (não nativos): tocar/pausar, linha do tempo, volume, velocidade, tela cheia. Ordem de foco: tocar/pausar → linha do tempo → volume → velocidade → tela cheia. Cada controle tem nome acessível e foco visível. Sem botão de baixar o vídeo |
| G4 | Velocidades | 0,5x · 1x · 1,25x · 1,5x · 2x, em menu do botão de velocidade. Padrão 1x. A escolha vale para a aula aberta; trocar de aula recomeça em 1x |
| G5 | Qualidade automática | Sem escolha manual de qualidade. O player troca sozinho conforme a rede, sem interromper. Nenhum controle de qualidade aparece |
| G6 | Tela cheia | Pedida ao **contêiner** do player (não ao `<video>`), para a marca d'água continuar visível. Em tela cheia a barra de controles e a lista de aulas somem; sair volta ao desenho anterior |
| G7 | Marca d'água: zonas | Quatro zonas fixas sobre a imagem do vídeo, nenhuma tocando a barra de controles: superior esquerda, superior direita, inferior esquerda, inferior direita (todas com margem interna da borda do vídeo). Só uma zona por vez mostra o e-mail |
| G8 | Marca d'água: troca | Troca de zona a cada 30 s, nunca repetindo a zona anterior. Visível tocando, pausado e em tela cheia. Texto pequeno com fundo translúcido: legível sem esconder o conteúdo da aula. `pointer-events: none`, fora da ordem de leitura do leitor de tela (`aria-hidden="true"`) |
| G9 | Aviso de uso pessoal | Fixo sob o player, sempre visível quando há player: "Este conteúdo é de uso pessoal. O seu e-mail aparece sobre o vídeo durante a aula." Nenhum texto da tela apresenta o conteúdo como garantia contra cópia (verificação de linguagem na seção 7) |
| G10 | Mensagens em linguagem simples | Todos os estados falam português simples, sem termo técnico. Datas absolutas ("terminou em 15/03/2028"). A data de término é formatada no fuso da escola |
| G11 | Renovação invisível | A renovação em curso **não aparece** para o aluno. Só se vê algo se a reprodução precisar parar (direito encerrado ou indisponibilidade até o fim da validade) |
| G12 | Lista mostra a versão vigente | Módulos e aulas na ordem do currículo da versão vigente. Republicação (renomear, reordenar, remover) aparece na lista; a aula removida mostra "Esta aula não está disponível" com a lista nova à mão |
| G13 | Nada de progresso nesta tela | Sem percentual, sem "continuar de onde parou", sem "meus cursos". A aula sempre começa do início (DP-06) |

---

## 2. Fluxos do usuário

### 2.1 Aluno: abrir e assistir

```text
  Link /student/aulas/{lessonId}
           │ sem login ──▶ /entrar ──▶ depois de entrar, volta à mesma aula
           ▼
  A1 carregando (player com indicação de carregamento; lista em Skeleton)
           │
           ├─ com direito ──▶ vídeo começa do início + lista com aula atual
           │                      │ escolhe outra aula ──▶ abre a outra do início (nova decisão)
           │                      │ pausa / linha do tempo / volume / velocidade / tela cheia
           │                      │ renovação em curso: nada muda na tela
           │                      ├─ direito encerrado na renovação ──▶ para + estado de acesso encerrado
           │                      └─ indisponível até o fim da validade ──▶ para + indisponível + [ Tentar de novo ]
           │
           ├─ sem acesso ──▶ "Você não tem acesso a este curso." (sem título de curso nem de aula)
           ├─ acesso encerrado ──▶ "Seu acesso a este curso terminou em DD/MM/AAAA."
           ├─ indisponível ──▶ "Não foi possível confirmar seu acesso agora." + [ Tentar de novo ]
           ├─ aula inexistente / outra escola / curso não publicado / removida ──▶ "Esta aula não está disponível."
           ├─ vídeo não pronto ──▶ "Esta aula está indisponível no momento."
           ├─ sem e-mail na sessão ──▶ "Não foi possível iniciar a aula. Tente de novo."
           └─ navegador sem suporte ──▶ "Seu navegador não consegue mostrar este vídeo. …"
```

### 2.2 Rota direta de aula negada

```text
  /student/aulas/{lessonId} aberta direto (favorito, link repassado)
           ▼
  A1 resolve sem mostrar conteúdo antes: cai no estado correspondente
  (sem acesso / encerrado / indisponível / não disponível), com a lista
  visível quando fizer sentido (indisponível) e sem título quando negado.
```

---

## 3. Layout base e componentes

### 3.1 Layout do aluno logado

Reusa o AppShell do aluno (conta do aluno, catálogo): cabeçalho com marca, navegação e menu da conta; conteúdo em largura de leitura. No celular 390, a lista vai abaixo do player.

```text
Desktop 1440
┌──────────────────────────────────────────────────────────────────────────────────────────┐
│ [</>] Code4Coders        Cursos                       [◐ tema]  ( AS ) Ana Souza ▾        │
├──────────────────────────────────────────────────────────────────────────────────────────┤
│  << A1 >>  título + player + aviso (esquerda) + lista de aulas (direita 360)              │
├──────────────────────────────────────────────────────────────────────────────────────────┤
│ © Code4Coders · cursos de programação                                                    │
└──────────────────────────────────────────────────────────────────────────────────────────┘

Mobile 390
┌──────────────────────────────────┐
│ [</>] Code4Coders           ☰     │
│  << A1 >>                        │
│  título → player → aviso → lista │
│  (empilhados, lista abaixo)      │
└──────────────────────────────────┘
```

### 3.2 Componentes

Reuso: `Sidebar`/`SiteHeader` do aluno, `Card`, `Button`, `Alert`, `Skeleton`, `Accordion`, `Badge`, `Tooltip`, `Sheet`, `Sonner`. Ícones `lucide-react` com nome acessível nos botões.

**Composições propostas para o Figma:**
- `LessonPlayer` = contêiner do player 16:9 + barra de controles própria + marca d'água (`WatermarkTag`) + região de status.
- `WatermarkTag` = e-mail sobre o vídeo (`aria-hidden="true"`, `pointer-events: none`), em uma das 4 zonas.
- `PlayerControls` = tocar/pausar, linha do tempo (`slider` com nome "Linha do tempo"), volume, velocidade (menu 0,5x–2x), tela cheia.
- `LessonList` = `Accordion` de módulos + `LessonItem` por aula, com a aula atual em texto ("Aula atual").
- `LessonState` = estado no lugar do player (título + texto + ação quando há): sem acesso, encerrado, indisponível, não disponível, vídeo indisponível, sem e-mail, sem suporte, interrompida.
- `PersonalUseNotice` = aviso fixo sob o player.

**Legenda dos desenhos:** `[ Ação ]` botão · `_link_` navegação · `( Estado )` badge/texto de estado · `[▶]` tocar · `[❚❚]` pausar · `━━━━●━━` linha do tempo · `( Aula atual )` texto do item atual · `⟨e-mail⟩` marca d'água.

---

## 4. Wireframes — tela da aula

### A1 · Estrutura — `/student/aulas/:lessonId`

```text
A1.a  Reproduzindo (desktop 1440)
┌──────────────────────────────────────────────────────────────────────────────────────────┐
│ [</>] Code4Coders        Cursos                       [◐ tema]  ( AS ) Ana Souza ▾        │
├────────────────────────────────────────────────────────────────────┬─────────────────────┤
│  _← Voltar para o curso_                                           │  AULAS DO CURSO       │
│  Injeção de dependência na prática                            (H1) │  (lista na ordem do   │
│  ┌─ LessonPlayer (16:9) ────────────────────────────────────┐     │   currículo)          │
│  │                                                           │     │  ▾ Módulo 1 ·         │
│  │   ⟨ marina.alves@example.com ⟩   (zona: superior direita) │     │    Fundamentos        │
│  │                                                           │     │  │ 1 Visão geral      │
│  │                        imagem do vídeo                    │     │  │ ▶ 2 Injeção de…     │
│  │                                                           │     │  │   ( Aula atual )    │
│  │                                                           │     │  ▸ Módulo 2 ·         │
│  │ [❚❚] ━━━━●━━━━━━ 12:04 / 48:20   [🔊] [1x ▾] [⛶]        │     │    Persistência       │
│  └───────────────────────────────────────────────────────────┘     │  (aula atual com     │
│  ( i ) Este conteúdo é de uso pessoal. O seu e-mail aparece        │   fundo + ▶ + texto)  │
│        sobre o vídeo durante a aula.              (PersonalUseNotice)│                     │
└────────────────────────────────────────────────────────────────────┴─────────────────────┘
Ordem de foco nos controles: [❚❚] → linha do tempo → [🔊] → [1x ▾] → [⛶].
Nomes acessíveis: "Pausar", "Linha do tempo", "Volume", "Velocidade", "Tela cheia".
Nenhum botão de baixar o vídeo. Marca fora da barra de controles, sem cobrir cliques.
```

```text
A1.b  Mobile 390 (lista abaixo do player)
┌──────────────────────────────────┐
│ [</>] Code4Coders           ☰     │
│ _← Voltar para o curso_          │
│ Injeção de dependência           │
│ na prática                  (H1) │
│ ┌─ LessonPlayer ───────────────┐ │
│ │ ⟨ marina.alves@example.com ⟩ │ │
│ │      imagem do vídeo         │ │
│ │ [❚❚] ━━●━━ 12:04 / 48:20    │ │
│ │ [🔊] [1x ▾] [⛶]              │ │
│ └──────────────────────────────┘ │
│ ( i ) Este conteúdo é de uso     │
│ pessoal. O seu e-mail aparece    │
│ sobre o vídeo durante a aula.    │
│ AULAS DO CURSO                   │
│ ▾ Módulo 1 · Fundamentos         │
│ │ 1 Visão geral                  │
│ │ ▶ 2 Injeção de… ( Aula atual ) │
│ ▸ Módulo 2 · Persistência        │
└──────────────────────────────────┘
```

```text
A1.c  Tela cheia (desktop e mobile)
┌──────────────────────────────────────────────────────────────────────────────────────────┐
│                                                                                          │
│   ⟨ marina.alves@example.com ⟩   (zona: inferior esquerda; exemplo após a troca)          │
│                                                                                          │
│                                   imagem do vídeo                                        │
│                                                                                          │
│   [❚❚] ━━━━━━━━●━━━━━━━━━━ 12:34 / 48:20   [🔊] [1,5x ▾] [⛶ Sair]                           │
│                                                                                          │
└──────────────────────────────────────────────────────────────────────────────────────────┘
A marca continua visível em tela cheia (o pedido é ao contêiner). A lista some em tela cheia.
Sair da tela cheia volta a A1.a / A1.b sem recarregar o vídeo.
```

### Marca d'água — zonas e troca

```text
Zonas fixas (só uma por vez; nenhuma toca a barra de controles):

┌─ vídeo ────────────────────┐
│ ⟨e-mail⟩                   │   Zona 1: superior esquerda
│                 ⟨e-mail⟩   │   Zona 2: superior direita (exemplo de A1.a)
│                            │
│                            │
│   (barra de controles aqui: sem marca nesta faixa)
└────────────────────────────┘

┌─ vídeo ────────────────────┐
│                            │
│                            │   Zona 3: inferior esquerda (exemplo de A1.c)
│ ⟨e-mail⟩                   │
│   (barra de controles aqui: sem marca nesta faixa)
└────────────────────────────┘   Zona 4: inferior direita

Regra: a cada 30 s a marca muda de zona e nunca repete a zona anterior
(ex.: 2 → 3 → 1 → 4 → 2 …). Com o vídeo pausado, a marca fica onde está,
visível sobre a imagem parada. Texto pequeno com fundo translúcido:
dá para ler o e-mail sem esconder a aula.
```

### Player — controles e velocidade

```text
Barra de controles (sempre nesta ordem de foco):

[▶ Tocar] / [❚❚ Pausar] · ━━━━●━━━━━━ Linha do tempo · [🔊 Volume] · [1x ▾ Velocidade] · [⛶ Tela cheia]

Velocidade (menu de [1x ▾]):
┌─ Velocidade ─────────┐
│ ( ) 0,5x             │
│ (•) 1x               │
│ ( ) 1,25x            │
│ ( ) 1,5x             │
│ ( ) 2x               │
└──────────────────────┘
Escolher tem efeito imediato e mantém o ponto da aula. Foco visível em
todos os controles; todos operáveis só com teclado e anunciados pelo leitor de tela.
```

### A1 · Estados (mensagens exatas em tela)

Linguagem: português simples, sem termo técnico. Datas absolutas. Cada estado
é anunciado em região de status (`aria-live="polite"`, erros com `role="alert"`).

```text
A1.d  Carregando
┌─ LessonPlayer ───────────────────┐
│ ▒▒▒▒▒▒▒▒▒▒ (Skeleton do vídeo)   │
│ Carregando a aula…               │
└──────────────────────────────────┘
Lista: Skeleton de módulos e aulas. Título ainda não aparece.

A1.e  Pausado (com marca visível)
│ imagem parada + ⟨ marina.alves@example.com ⟩ (na zona atual) │
│ [▶] ━━━━●━━━━━━ 12:04 / 48:20   [🔊] [1x ▾] [⛶]              │

A1.f  Sem acesso (nunca teve acesso)
┌─ no lugar do player ─────────────┐
│ Você não tem acesso a este curso. │
└──────────────────────────────────┘
Sem título de curso nem de aula. Sem lista de aulas. Com [ Voltar para o início ] e _Entrar com outra conta_.

A1.g  Acesso encerrado (tinha acesso, terminou)
┌─ no lugar do player ─────────────┐
│ Seu acesso a este curso           │
│ terminou em 15/03/2028.           │
└──────────────────────────────────┘
Sem vídeo. Data absoluta no fuso da escola. Sem lista de aulas.

A1.h  Indisponibilidade da decisão (distinta de "sem acesso")
┌─ no lugar do player ─────────────┐
│ Não foi possível confirmar seu    │
│ acesso agora.                     │
│ Tente de novo em instantes.       │
│ [ Tentar de novo ]                │
└──────────────────────────────────┘
Lista de aulas visível quando já carregada. Texto nunca diz que o aluno não tem acesso.

A1.i  Aula não disponível (mensagem única para os quatro casos)
┌─ no lugar do player ─────────────┐
│ Esta aula não está disponível.    │
└──────────────────────────────────┘
Vale igual para: aula inexistente, aula de outra escola, aula de curso não
publicado e aula removida da versão vigente. A tela não revela qual é o caso.
Lista da versão nova à mão quando houver republicação.

A1.j  Vídeo indisponível (não pronto)
┌─ no lugar do player ─────────────┐
│ Esta aula está indisponível       │
│ no momento.                       │
└──────────────────────────────────┘
Título e lista aparecem; nenhum detalhe técnico.

A1.k  Não foi possível iniciar a aula (sem e-mail na sessão)
┌─ no lugar do player ─────────────┐
│ Não foi possível iniciar a aula.  │
│ Tente de novo.                    │
│ [ Tentar de novo ]                │
└──────────────────────────────────┘

A1.l  Navegador sem suporte ao player
┌─ no lugar do player ─────────────┐
│ Seu navegador não consegue        │
│ mostrar este vídeo.               │
│ Atualize o navegador ou tente     │
│ em outro aparelho.                │
└──────────────────────────────────┘

A1.m  Reprodução interrompida — direito encerrado no meio da aula
┌─ no lugar do player ─────────────┐
│ Seu acesso a este curso           │
│ terminou em 15/03/2028.           │
│ [ Ver outras aulas ]              │
└──────────────────────────────────┘
A reprodução para em até 5 minutos depois do término (mais o trecho já
carregado). O que foi assistido até ali continua valendo.

A1.n  Reprodução interrompida — indisponível até o fim da validade
┌─ no lugar do player ─────────────┐
│ Não foi possível confirmar seu    │
│ acesso agora.                     │
│ [ Tentar de novo ]                │
└──────────────────────────────────┘

A1.o  Renovação em curso (invisível)
Nada muda na tela: sem spinner, sem faixa, sem mensagem. A aula segue
tocando. Só aparece algo se cair em A1.m ou A1.n.
```

```text
A1.p  Mobile 390 — exemplos de estado
┌──────────────────────────────────┐
│ Injeção de dependência           │
│ na prática                  (H1) │
│ ┌──────────────────────────────┐ │
│ │ Não foi possível confirmar   │ │
│ │ seu acesso agora.            │ │
│ │ [ Tentar de novo ]           │ │
│ └──────────────────────────────┘ │
│ AULAS DO CURSO                   │
│ ▾ Módulo 1 · Fundamentos         │
└──────────────────────────────────┘

┌──────────────────────────────────┐
│ ┌──────────────────────────────┐ │
│ │ Você não tem acesso          │ │
│ │ a este curso.                │ │
│ └──────────────────────────────┘ │
│ (sem título e sem lista)         │
└──────────────────────────────────┘
```

### Entrada e retorno

```text
A1.q  Sem login → login → volta à mesma aula
  /student/aulas/{lessonId} sem login ──▶ /entrar ──▶ depois de entrar ──▶ /student/aulas/{lessonId}
  Nada da aula aparece antes de entrar. O retorno só aceita caminho interno do próprio SPA.

A1.r  Rota direta de aula negada
  Abrir /student/aulas/{lessonId} direto cai em A1.f / A1.g / A1.h / A1.i
  conforme o caso, sem exibir conteúdo antes da resposta.
```

---

## 5. Acessibilidade

- Todos os controles do player por teclado, na ordem de A1.a, com nome acessível e foco visível; a linha do tempo é operável por setas.
- Mensagem de estado anunciada em região de status quando o vídeo não abre ou a reprodução para (`aria-live="polite"`; erros com `role="alert"`).
- Marca d'água fora da ordem de leitura do leitor de tela (`aria-hidden="true"`) e sem interceptar cliques (`pointer-events: none`); contraste suficiente para ler sem comprometer a leitura do vídeo.
- Lista de aulas navegável como lista (`ul`/`li` por módulo e aula), com a aula atual indicada em texto ("Aula atual"), nunca só por cor ou ícone.
- Velocidade e volume anunciados ao mudar; tela cheia mantém o foco no contêiner e devolve o foco ao sair.
- `prefers-reduced-motion` respeitado nas transições da marca e nos Skeleton.

---

## 6. Plano para o Figma (após aprovação deste ASCII)

Páginas novas no arquivo `Code4Coders — Design System`, sem mexer nas existentes:

1. **🧭 Fluxo — Aula**: os fluxos da seção 2, com os frames das telas como nós.
2. **📱 Screens — Aula**: A1.a a A1.r, todos os estados, em desktop 1440 e mobile 390 (lista ao lado no desktop, abaixo no celular). Tema Light; Dark em A1.a e A1.f.
3. **Components (proposta)**, na página de Screens: `LessonPlayer`, `WatermarkTag`, `PlayerControls`, `LessonList`, `LessonState`, `PersonalUseNotice`, item "Aula atual" do `LessonItem`.

Tudo reusando o AppShell do aluno, `Alert`, `Skeleton`, `Accordion`, `Badge` e demais componentes do DS, sem valor fixo.

---

## 7. Decisões para você aprovar

1. **Estrutura da tela** com título, player 16:9, aviso fixo de uso pessoal e lista de módulos e aulas na ordem do currículo; lista ao lado no desktop e abaixo no celular (G1, A1.a, A1.b).
2. **Player próprio** com tocar/pausar, linha do tempo, volume, velocidade e tela cheia; ordem de foco, nome acessível e foco visível; sem baixar e sem escolha manual de qualidade (G3–G5).
3. **Tela cheia no contêiner**, com a marca visível (G6, A1.c).
4. **Marca d'água em 4 zonas fixas** fora dos controles, trocando a cada 30 s sem repetir a anterior, visível pausado e em tela cheia, fora da leitura do leitor de tela (G7–G8).
5. **Aviso fixo sob o player** com o texto de G9: "Este conteúdo é de uso pessoal. O seu e-mail aparece sobre o vídeo durante a aula."
6. **Estados com as mensagens exatas** de A1.d a A1.o, incluindo "sem acesso" sem título, "encerrado" com data, "indisponível" com *Tentar de novo*, "aula não disponível" única para os quatro casos, e **renovação invisível** (G10–G11).
7. **Entrada e retorno**: sem login vai ao login e volta à mesma aula; rota direta de aula negada não mostra conteúdo antes (A1.q, A1.r).
8. **Linguagem simples** sem termo técnico; verificação de linguagem: nenhum texto de tela usa os termos proibidos citados no PRD (RN-M15) nem apresenta garantia contra cópia.
9. **Acessibilidade** da seção 5 (teclado, região de status, marca fora da leitura, lista como lista com "Aula atual" em texto).
10. **Componentes novos:** `LessonPlayer`, `WatermarkTag`, `PlayerControls`, `LessonList`, `LessonState`, `PersonalUseNotice`.

---

## 8. Handoff do Figma — aguardando aprovação

O desenho foi materializado no arquivo **Code4Coders — Design System** em 2026-10-03, nas páginas `Fluxo — Aula` e `Screens — Aula`, sobre o ASCII aprovado (seções 1 a 7), sem reabrir fluxo nem textos. A revisão começa pelo [índice de revisão](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=260-2332).

> **Aprovação:** aguardando a aprovação explícita do responsável sobre o Figma. Pedido de ajuste volta ao Figma (ou ao ASCII, se mudar conteúdo) antes do registro. Nenhum código de tela começa antes dele.

### 8.1 Fluxos e navegação

| Artefato | Link e `node-id` |
|---|---|
| Índice de revisão | [260:2332](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=260-2332) |
| Fluxo 1 · Aluno — abrir e assistir a aula | [261:2](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=261-2) |
| Fluxo 2 · Rota direta de aula negada (A1.r) | [261:199](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=261-199) |
| Página Fluxo — Aula | [255:2](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=255-2) |
| Página Screens — Aula | [255:3](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=255-3) |

Cada nó dos fluxos tem o botão **Revisar tela →**, que abre o frame correspondente pelo link do arquivo. A entrada sem login (A1.q) reusa o frame existente de Entrar da Conta do aluno ([51:537](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=51-537)). A1.o (renovação em curso) é invisível por decisão (G11): o nó do fluxo aponta para A1.a, sem frame próprio. Os dados são ilustrativos e o desenho não executa reprodução, decisão de acesso nem renovação; foco, teclado, leitor de tela e regras do servidor seguem as seções 1 a 5.

### 8.2 Inventário de telas e estados

Desktop em 1440 × 900; mobile em 390 de largura (altura 844, ou a do conteúdo quando ele passa disso); tela cheia no celular na horizontal (844 × 390). Light em todas as telas; Dark em A1.a e A1.f.

**Ajustes de desenho sem mudar decisão:** o nome no menu da conta é o da aluna do exemplo (Marina Alves), o mesmo do e-mail da marca d'água, para não sugerir que a marca mostra outra pessoa; a imagem do vídeo é ilustrada com o `Code Window` do DS (princípio "código é a estrela"); no celular o vídeo 16:9 fica acima da barra de controles em duas linhas, dentro do mesmo contêiner, e as zonas da marca ficam sobre a imagem. Estados a mais que o ASCII desenhava em quadro próprio: A1.a2 (menu de velocidade aberto, desktop e celular) e A1.c no celular na horizontal. A1.p (exemplos de estado no celular) está coberto pelos frames de A1.f e A1.h em Mobile 390. Para A1.i, o desenho mostra o caso com a lista da versão vigente à mão (republicação), sem aula atual destacada.

#### 🧩 Composições propostas — Aula — [grupo 255:4](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=255-4)

| Tela / estado | Dimensão | Link e `node-id` |
|---|---|---|
| Icon/play | 24 × 24 | [255:14](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=255-14) |
| Icon/volume-2 | 24 × 24 | [255:19](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=255-19) |
| Icon/maximize | 24 × 24 | [255:25](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=255-25) |
| Icon/minimize | 24 × 24 | [255:31](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=255-31) |
| Icon/monitor-x | 24 × 24 | [255:38](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=255-38) |
| WatermarkTag | 177 × 24 | [255:42](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=255-42) |
| PlayerControls (layout desktop · mobile · tela-cheia × estado reproduzindo · pausado) | 1490 × 594 | [255:202](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=255-202) |
| SpeedOption | 370 × 66 | [255:213](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=255-213) |
| SpeedMenu | 170 × 198 | [255:214](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=255-214) |
| PersonalUseNotice | 888 × 44 | [255:236](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=255-236) |
| LessonItem (aluno) — estado padrão · atual | 378 × 170 | [256:89](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=256-89) |
| ModuleHeader (aluno) — aberto sim · não | 378 × 146 | [256:98](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=256-98) |
| LessonList — carregada · carregando | 794 × 404 | [256:144](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=256-144) |
| LessonState | 888 × 500 | [256:148](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=256-148) |
| StudentHeader — desktop · mobile | 1490 × 210 | [256:202](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=256-202) |
| StudentFooter — desktop · mobile | 1490 × 194 | [256:207](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=256-207) |
| LessonPlayer — 4 zonas × desktop · mobile | 1900 × 1399 | [256:535](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=256-535) |
| Ordem de foco do player (anotação) | 900 × 152 | [260:2292](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=260-2292) |

#### A1 · Tela da aula — reproduzir, velocidade, pausar, carregar — [grupo 257:419](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=257-419)

| Tela / estado | Dimensão | Link e `node-id` |
|---|---|---|
| A1.a · Reproduzindo (marca na zona superior direita) | 1440 × 900 | [257:420](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=257-420) |
| A1.a2 · Menu de velocidade aberto | 1440 × 900 | [257:530](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=257-530) |
| A1.e · Pausado (marca visível) | 1440 × 900 | [257:659](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=257-659) |
| A1.d · Carregando | 1440 × 900 | [257:793](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=257-793) |

#### A1.c · Tela cheia — [grupo 257:831](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=257-831)

| Tela / estado | Dimensão | Link e `node-id` |
|---|---|---|
| A1.c · Tela cheia (marca na zona inferior esquerda) | 1440 × 900 | [257:832](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=257-832) |
| A1.c · Tela cheia, celular na horizontal | 844 × 390 | [257:898](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=257-898) |

#### A1 · Estados — mensagem no lugar do player — [grupo 258:836](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=258-836)

| Tela / estado | Dimensão | Link e `node-id` |
|---|---|---|
| A1.f · Sem acesso (sem título e sem lista) | 1440 × 900 | [258:837](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=258-837) |
| A1.g · Acesso encerrado (sem título e sem lista) | 1440 × 900 | [258:878](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=258-878) |
| A1.h · Indisponibilidade da decisão | 1440 × 900 | [258:919](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=258-919) |
| A1.i · Aula não disponível (lista da versão vigente) | 1440 × 900 | [258:1001](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=258-1001) |
| A1.j · Vídeo indisponível | 1440 × 900 | [258:1080](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=258-1080) |
| A1.k · Não foi possível iniciar a aula | 1440 × 900 | [258:1167](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=258-1167) |
| A1.l · Navegador sem suporte | 1440 × 900 | [258:1249](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=258-1249) |
| A1.m · Reprodução interrompida — direito encerrado | 1440 × 900 | [258:1333](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=258-1333) |
| A1.n · Reprodução interrompida — indisponível até o fim da validade | 1440 × 900 | [258:1414](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=258-1414) |

#### Mobile 390 — tela da aula e estados — [grupo 259:1353](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=259-1353)

| Tela / estado | Dimensão | Link e `node-id` |
|---|---|---|
| A1.b · Reproduzindo (marca na zona superior direita) | 390 × 1035 | [259:1354](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=259-1354) |
| A1.a2 · Menu de velocidade aberto | 390 × 1035 | [259:1461](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=259-1461) |
| A1.e · Pausado (marca visível) | 390 × 1035 | [259:1587](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=259-1587) |
| A1.d · Carregando | 390 × 844 | [259:1721](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=259-1721) |
| A1.f · Sem acesso (A1.p) | 390 × 844 | [259:1753](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=259-1753) |
| A1.g · Acesso encerrado | 390 × 844 | [259:1788](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=259-1788) |
| A1.h · Indisponibilidade da decisão (A1.p) | 390 × 932 | [259:1823](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=259-1823) |
| A1.i · Aula não disponível | 390 × 844 | [259:1899](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=259-1899) |
| A1.j · Vídeo indisponível | 390 × 844 | [259:1972](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=259-1972) |
| A1.k · Não foi possível iniciar a aula | 390 × 904 | [259:2053](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=259-2053) |
| A1.l · Navegador sem suporte | 390 × 896 | [259:2129](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=259-2129) |
| A1.m · Reprodução interrompida — direito encerrado | 390 × 896 | [259:2207](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=259-2207) |
| A1.n · Reprodução interrompida — indisponível até o fim da validade | 390 × 896 | [259:2282](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=259-2282) |

#### Dark mode — validação de tokens (Aula) — [grupo 260:2140](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=260-2140)

| Tela / estado | Dimensão | Link e `node-id` |
|---|---|---|
| A1.a · Reproduzindo — Dark | 1440 × 900 | [260:2141](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=260-2141) |
| A1.f · Sem acesso — Dark | 1440 × 900 | [260:2251](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=260-2251) |
