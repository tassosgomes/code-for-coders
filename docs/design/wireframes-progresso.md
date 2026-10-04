# Wireframes ASCII — Progresso do aluno: meus cursos e retomada na aula (CAP-017, 1º PRD)

> **Status:** ASCII e Figma aprovados pelo responsável em 2026-10-04 (Figma aprovado como entregue: Dark em I1.a e I1.e; Módulo 2 aberto em A1.P; aviso de I1.f só com título). Código de tela liberado a partir da task 4.0.
> **Revisão:** responsável humano (Tasso) — reveja a [seção 7](#7-decisões-para-você-aprovar) (10 pontos) e os quadros I1.a–I1.h e A1.P–A1.S; o gate da task 1.0 só passa após o registro da aprovação neste cabeçalho.
> **Handoff:** aprovado o ASCII → desenho no Figma (task 2.0) → aprovação → código de tela (tasks 4.0 em diante). Pedido de ajuste volta a este documento antes de qualquer registro.
> **Objetivo:** definir fluxos, conteúdo e estados de **"meus cursos"** (o Início, `/`) e das **mudanças na tela da aula** (`student-spa`) antes do desenho no Figma.
> **Fontes:** [PRD](../../tasks/prd-progresso-aluno/prd.md) v1.0 (Experiência do Usuário, RF-04 a RF-06, RN-P01 a RN-P10, DP-01 a DP-09), [TechSpec](../../tasks/prd-progresso-aluno/techspec.md) v1.0 (Bloco Frontend, Habilitadores inevitáveis), [wireframes da tela da aula](wireframes-aula.md) (formato, tela da aula aprovada em CAP-007), [wireframes da conta do aluno](wireframes-conta-aluno.md) (T6 Início e sidebar), [Design System](../../DESIGN.md) e [componentes](Components.md).

---

## 1. Inventário

### 1.1 O que o PRD entrega em tela

| Entrega | RF | Tela ou efeito visível |
|---|---|---|
| Meus cursos: vigentes com Continuar/Começar, encerrados sem ação, vazio, indisponível, progresso indisponível | RF-04 | I1 Início = "meus cursos" (`/`), no lugar reservado hoje em `dashboard-screen.tsx` |
| Continuar leva à aula certa; abrir a aula retoma da última posição com aviso e Começar do início | RF-05 | I1 (Continuar/Começar) + A1 (retomada na aula) |
| Lista da aula marca concluídas e mostra o percentual; concluir atualiza sem recarregar | RF-06 | A1 tela da aula (lista + percentual) |
| Entrada pós-login sem origem cai em "meus cursos" | RF-04 | I1 como destino (DP-08; sem rota nova) |

### 1.2 Telas (`student-spa`, base `/student/`; origem `http://localhost:8082` no Compose)

| # | Tela | Rota | Estados principais |
|---|---|---|---|
| I1 | Início = meus cursos | `/student/` (rota do SPA: `/`) | carregando · com cursos (Seus cursos + Acesso encerrado) · vazio · indisponível da lista (com *Tentar de novo*) · progresso indisponível |
| A1 | Tela da aula (mudanças) | `/student/aulas/:lessonId` (rota do SPA: `/aulas/:lessonId`) | lista com concluídas + percentual do curso · aviso "Retomando de m:ss" com *Começar do início* · caminho de volta ao Início (o restante segue [wireframes-aula.md](wireframes-aula.md)) |

Nenhuma rota nova: "meus cursos" é o Início, destino do login sem página de origem (DP-08). Com página de origem (link de aula), o login volta à aula, como em CAP-007.

### 1.3 Decisões de desenho propostas (para aprovação)

| # | Ponto | Proposta e fundamento |
|---|---|---|
| G1 | Estrutura do Início | AppShell do aluno + cabeçalho "INÍCIO / Olá, Ana" + grade: coluna principal com "Seus cursos" e "Acesso encerrado" + coluna lateral (380) com o card de conta existente. Desktop 1440: lado a lado. Celular 390: empilhados, cursos primeiro, conta depois |
| G2 | Card do curso vigente | `Card` com título (H4/H5), `Progress` + texto "37% · 3 de 8 aulas" (percentual sempre com a contagem), meta "3 de 8 aulas concluídas" para leitor de tela, e um `Button default` por card: [ Continuar ] (com progresso) ou [ Começar ] (sem progresso). Um CTA por card, nunca três roxos lado a lado |
| G3 | Curso encerrado | Seção secundária "Acesso encerrado" (H3) com cards em `bg-muted`, sem ação: título + "50% · 5 de 10 aulas" + "Seu acesso terminou em 15/03/2028". Não parece clicável (sem hover de card clicável, sem botão, sem link) |
| G4 | Ordem da lista | Vigentes: com avanço mais recente primeiro; nunca começados por último, do acesso mais recente para o mais antigo. Encerrados: por data de término decrescente (decisão de desenho; a ordenação por dados é RN-P10/servidor) |
| G5 | Linguagem exata | Textos deste documento são os textos de tela (pt-BR simples, sem "sessão", "avanço" nem "registro"). Datas absolutas ("terminou em 15/03/2028"). Percentual sempre com contagem |
| G6 | Falhas que não mentem | Matrícula fora do ar → "Não foi possível carregar seus cursos agora. Tente de novo em instantes." + [ Tentar de novo ], nunca o vazio. Progresso fora do ar → lista sem percentual, com aviso "Seu progresso não pôde ser carregado agora", e Continuar/Começar levam à primeira aula |
| G7 | Tela da aula: concluídas | `LessonItem` concluído ganha texto "✓ Concluída" além do ícone (nunca só cor/ícone) + percentual do curso no cabeçalho da lista ("37% · 3 de 8 aulas"). Concluir assistindo atualiza a marca sem recarregar; nada de cadeado: aula não concluída abre normalmente (RN-R06, G20) |
| G8 | Tela da aula: retomada | Ao abrir aula com posição guardada, o player começa na última posição e mostra perto do player o aviso "Retomando de 4:12" + botão *Começar do início*. Some sozinho depois de alguns segundos ou ao interagir; não some enquanto tem foco; anunciado em região de status |
| G9 | Volta ao Início | Tela da aula ganha caminho `_← Voltar para o Início_` (acima do título). Sidebar mantém "Início" como único item do aluno nesta fase, agora significando "meus cursos" |
| G10 | Sidebar | Nenhum item novo. "Início" continua ativo nas duas telas (I1 e A1 são área do aluno). Demais itens chegam com os próximos CAPs |

---

## 2. Fluxos do usuário

### 2.1 Aluno: entrar, escolher, continuar, assistir, voltar

```text
  Login sem página de origem ──▶ I1 "meus cursos"
  Login vindo do link de uma aula ──▶ a aula (como em CAP-007)

  I1 carregando (cards em Skeleton; conta em Skeleton)
           │
           ├─ com cursos ──▶ Seus cursos (Continuar/Começar) + Acesso encerrado (sem ação)
           │                    │ [ Continuar ] ──▶ /aulas/{continueLessonId}, retoma da última posição (A1.P)
           │                    │ [ Começar ] ──▶ /aulas/{primeiraAula}, do início
           │                    └─ encerrado: sem ação, mostra data e progresso como estava
           ├─ sem concessão ──▶ "Você ainda não tem cursos" + caminho para a vitrine
           ├─ Matrícula fora do ar ──▶ "Não foi possível carregar seus cursos agora…" + [ Tentar de novo ]
           └─ progresso fora do ar ──▶ lista sem percentual + aviso + Continuar/Começar → primeira aula

  A1 assistir e concluir
           ▼
   abre a aula ──▶ posição inicial = última posição (salvo fim/<10 s do fim/além da duração → do início)
           │       + aviso "Retomando de m:ss" + [ Começar do início ] quando retoma no meio
           │       + lista com "✓ Concluída" e percentual
           ├─ passa dos 90% ──▶ a aula atual ganha "✓ Concluída" sem recarregar (até 1 min)
           └─ _← Voltar para o Início_ ──▶ I1
```

### 2.2 Continuar (regra de RF-05, só desenho do destino)

```text
  [ Continuar ] ──▶ abre a aula do avanço mais recente, se não concluída;
                    senão a próxima não concluída depois dela;
                    senão a primeira não concluída;
                    se todas concluídas, a do avanço mais recente;
                    se a mais recente saiu da versão vigente, a primeira não concluída.
  O SPA não recalcula: usa continueLessonId como veio.
```

---

## 3. Layout base e componentes

### 3.1 Layout do aluno logado (I1)

Reusa o AppShell do aluno (conta do aluno, aula): sidebar 264 + topbar 68 + conteúdo em `bg-muted` com `p-8`. Card de conta existente permanece na lateral. No celular 390, tudo empilhado: cursos primeiro, conta depois; sidebar vira `Sheet` (☰).

```text
Desktop 1440
┌──────────────────┬──────────────────────────────────────────────────────────────────────┐
│ [</>] Code4Coders│ [◐ tema]  ( AS ) Ana Souza ▾                                          │
│                  ├──────────────────────────────────────────────────┬───────────────────┤
│ ▣ Início  ◀ ativo│  INÍCIO                              (overline)  │  ┌─ Card (380) ─┐ │
│                  │  Olá, Ana 👋                                 (H2)│  │ Sua conta    │ │
│                  │  << I1 >> Seus cursos + Acesso encerrado        │  │ (existente,   │ │
│                  │                                                 │  │  mantido)     │ │
│                  │                                                 │  └──────────────┘ │
│ Sidebar 264      │                                                 │                   │
└──────────────────┴──────────────────────────────────────────────────┴───────────────────┘

Mobile 390
┌──────────────────────────────────┐
│ [</>] Code4Coders           ☰     │
│  << I1 >>                        │
│  INÍCIO → Olá → Seus cursos →     │
│  Acesso encerrado → Sua conta    │
│  (empilhados)                    │
└──────────────────────────────────┘
```

### 3.2 Tela da aula (mudanças sobre a aprovada em CAP-007)

Estrutura, player, marca d'água e demais estados seguem [wireframes-aula.md](wireframes-aula.md). Aqui só entram: marca de concluída + percentual na lista, aviso de retomada perto do player e volta ao Início.

```text
Desktop 1440 (lista à direita, 360)          Mobile 390 (lista abaixo do player)
┌────────────────────────────────────┬─────┐ ┌──────────────────────────────────┐
│ _← Voltar para o Início_            │LISTA│ │ _← Voltar para o Início_          │
│ Título da aula                 (H1) │36% ·│ │ Título da aula              (H1) │
│ ┌─ player ────────────────────┐    │3/8  │ │ ┌─ player ───────────────────┐ │
│ │                             │    │ ✓✓· │ │ │                            │ │
│ └─────────────────────────────┘    │     │ │ └────────────────────────────┘ │
│ aviso de retomada (quando há)      │     │ │ aviso de retomada (quando há)    │
└────────────────────────────────────┴─────┘ └──────────────────────────────────┘
```

### 3.3 Componentes

Reuso: `Sidebar`/`SiteHeader` do aluno, `Card`, `Button` (default 1 por card; outline/secondary; link), `Alert`, `Skeleton`, `Progress`, `Avatar`, `Accordion`, `Badge` (`success` para "✓ Concluída"), `Sheet`, `Sonner`. Ícones `lucide-react` com nome acessível.

**Composições propostas para o Figma:**
- `MyCoursesList` = seção "Seus cursos" + `CourseProgressCard` por curso vigente.
- `CourseProgressCard` = título + `Progress` + "37% · 3 de 8 aulas" + [ Continuar ] / [ Começar ].
- `EndedAccessCard` = título + percentual + "Seu acesso terminou em dd/mm/aaaa", sem ação.
- `ResumeNotice` = aviso "Retomando de m:ss" + *Começar do início*, em região de status.
- `LessonList+Progress` = `LessonList` de CAP-007 + "✓ Concluída" em texto + percentual do curso.

**Legenda dos desenhos:** `[ Ação ]` botão · `_link_` navegação · `( Estado )` texto/estado · `▒▒` Skeleton · `✓` concluída (sempre com texto) · `━━━━●━━` progresso.

---

## 4. Wireframes — Início = meus cursos

### I1.a · Com cursos — vigentes + encerrado (desktop 1440)

```text
I1.a  Com cursos (desktop 1440)
┌──────────────────┬──────────────────────────────────────────────────────────────────────┐
│ [</>] Code4Coders│                                          [◐ tema]  ( AS ) Ana Souza ▾ │
│                  ├──────────────────────────────────────────────────┬───────────────────┤
│ ▣ Início  ◀ ativo│  INÍCIO                              (overline)  │  ┌─ Sua conta ──┐ │
│                  │  Olá, Ana 👋                                 (H2)│  │ ( AS ) Ana    │ │
│                  │  Bora continuar de onde parou.                  │  │ Souza          │ │
│                  │                                                 │  │                │ │
│                  │  SEUS CURSOS                          (H3)        │  │                │ │
│                  │  ┌─ CourseProgressCard ────────────────────┐    │  │ ✓ E-mail       │ │
│                  │  │ React do zero                    (H4)    │    │  │ confirmado     │ │
│                  │  │ 37% · 3 de 8 aulas                       │    │  │                │ │
│                  │  │ ━━━━━━●━━━━━━━━━━━ (Progress 37)         │    │  │ [ Trocar senha │ │
│                  │  │ [ Continuar ]                            │    │  │ _Sair_         │ │
│                  │  └─────────────────────────────────────────┘    │  └───────────────┘ │
│                  │  ┌─ CourseProgressCard ────────────────────┐    │                   │
│                  │  │ TypeScript na prática            (H4)    │    │                   │
│                  │  │ 0% · 0 de 10 aulas                       │    │                   │
│                  │  │ ━━━━━━━━━━━━━━━━━━━ (Progress 0)         │    │                   │
│                  │  │ [ Começar ]                              │    │                   │
│                  │  └─────────────────────────────────────────┘    │                   │
│                  │                                                 │                   │
│                  │  ACESSO ENCERRADO                     (H3)        │                   │
│                  │  ┌─ EndedAccessCard (bg-muted) ────────────┐    │                   │
│                  │  │ CSS moderno                      (H4)    │    │                   │
│                  │  │ 50% · 5 de 10 aulas                     │    │                   │
│                  │  │ Seu acesso terminou em 15/03/2028.      │    │                   │
│                  │  │ (sem botão, sem link: não clicável)     │    │                   │
│                  │  └─────────────────────────────────────────┘    │                   │
└──────────────────┴──────────────────────────────────────────────────┴───────────────────┘
Percentual sempre com contagem. Leitor de tela: "3 de 8 aulas concluídas".
Encerrado é secundário e sem ação (DP-02). Conclusão nunca bloqueia aula (RN-R06, G20).
```

### I1.b · Mobile 390

```text
I1.b  Com cursos (mobile 390)
┌──────────────────────────────────┐
│ [</>] Code4Coders           ☰     │
│ INÍCIO                           │
│ Olá, Ana 👋                 (H2) │
│ Bora continuar de onde parou.    │
│ SEUS CURSOS                 (H3) │
│ ┌─ CourseProgressCard ──────────┐ │
│ │ React do zero             (H4)│ │
│ │ 37% · 3 de 8 aulas            │ │
│ │ ━━━●━━━━━━━━ (Progress 37)    │ │
│ │ [ Continuar ]                 │ │
│ └───────────────────────────────┘ │
│ ┌─ CourseProgressCard ──────────┐ │
│ │ TypeScript na prática    (H4)│ │
│ │ 0% · 0 de 10 aulas            │ │
│ │ [ Começar ]                   │ │
│ └───────────────────────────────┘ │
│ ACESSO ENCERRADO            (H3) │
│ ┌─ EndedAccessCard ─────────────┐ │
│ │ CSS moderno              (H4)│ │
│ │ 50% · 5 de 10 aulas           │ │
│ │ Seu acesso terminou em        │ │
│ │ 15/03/2028.                   │ │
│ └───────────────────────────────┘ │
│ ┌─ Sua conta ───────────────────┐ │
│ │ (card existente, empilhado)   │ │
│ └───────────────────────────────┘ │
└──────────────────────────────────┘
```

### I1.c · Carregando

```text
I1.c  Carregando (desktop e mobile na mesma geometria)
┌─ CourseProgressCard ─────────────┐
│ ▒▒▒▒▒▒▒▒▒▒ (Skeleton do título)  │
│ ▒▒▒▒▒▒▒ (Skeleton do percentual) │
│ ▒▒▒▒▒▒▒▒▒▒▒▒ (Skeleton da barra) │
│ ▒▒▒▒▒▒ (Skeleton do botão)       │
└──────────────────────────────────┘
Conta: Skeleton no formato do card existente. Título "INÍCIO / Olá" ainda não aparece.
Região anunciada como carregamento (role="status"), sem spinner em tela cheia.
```

### I1.d · Vazio (sem nenhuma concessão)

```text
I1.d  Vazio
┌─ Card ───────────────────────────────┐
│ ┌─ CodeWindow (pequeno) ─── ● ● ● ┐ │
│ │ // nenhum curso por aqui ainda   │ │
│ │ cursos.length === 0  // true     │ │
│ └──────────────────────────────────┘ │
│ Você ainda não tem cursos       (H4)│
│ Quando você se matricular em um     │
│ curso, ele aparece aqui.            │
│ [ Explorar cursos ]                 │
└─────────────────────────────────────┘
[ Explorar cursos ] leva à vitrine /cursos. Sem cursos, sem seções.
```

### I1.e · Indisponível (Matrícula fora do ar — nunca o vazio)

```text
I1.e  Indisponível
┌─ Card ───────────────────────────────┐
│ Não foi possível carregar seus       │
│ cursos agora.                        │
│ Tente de novo em instantes.          │
│ [ Tentar de novo ]                   │
└─────────────────────────────────────┘
Nunca mostra "Você ainda não tem cursos" neste estado. Conta ao lado mantida.
Erro anunciado (role="alert"). [ Tentar de novo ] refaz a leitura.
```

### I1.f · Progresso indisponível (Matrícula ok, progresso fora do ar)

```text
I1.f  Progresso indisponível
┌─ aviso (Alert) ──────────────────────┐
│ ( i ) Seu progresso não pôde ser     │
│       carregado agora.               │
└──────────────────────────────────────┘
┌─ CourseProgressCard (sem número) ────┐
│ React do zero                   (H4)│
│ (sem "37% · 3 de 8 aulas")           │
│ [ Continuar ]  → primeira aula       │
└──────────────────────────────────────┘
┌─ CourseProgressCard (sem número) ────┐
│ TypeScript na prática           (H4)│
│ [ Começar ]     → primeira aula       │
└──────────────────────────────────────┘
Lista sem percentual; aviso visível; Continuar/Começar levam à primeira aula do curso.
```

---

## 5. Wireframes — mudanças na tela da aula

O restante da tela segue [wireframes-aula.md](wireframes-aula.md) (player, marca d'água, estados de acesso). Aqui só as três mudanças da task.

### A1.P · Lista com concluídas + percentual (desktop 1440)

```text
A1.P  Tela da aula com progresso (desktop 1440)
┌────────────────────────────────────────────────────────────────────┬─────────────────────┤
│ _← Voltar para o Início_                                           │  AULAS DO CURSO       │
│ Injeção de dependência na prática                            (H1) │  37% · 3 de 8 aulas   │
│ ┌─ LessonPlayer (16:9) ────────────────────────────────────┐     │  ━━━━●━━━━━━━        │
│ │                                                           │     │  ▾ Módulo 1 ·         │
│ │   ⟨ marina.alves@example.com ⟩   (zona: superior direita) │     │    Fundamentos        │
│ │                                                           │     │  │ ✓ 1 Visão geral    │
│ │                        imagem do vídeo                    │     │  │   Concluída         │
│ │                                                           │     │  │ ✓ 2 Injeção de…    │
│ │ [❚❚] ━━━━●━━━━━━ 12:04 / 48:20   [🔊] [1x ▾] [⛶]        │     │  │   Concluída         │
│ └───────────────────────────────────────────────────────────┘     │  ▸ Módulo 2 ·         │
│ ( i ) Este conteúdo é de uso pessoal. O seu e-mail aparece        │    Persistência       │
│       sobre o vídeo durante a aula.              (PersonalUseNotice)│  │ ▶ 3 Persistindo…  │
│ ┌─ ResumeNotice ──────────────────────────────────────────┐     │  │   ( Aula atual )    │
│ │ ( i ) Retomando de 4:12    [ Começar do início ]         │     │                     │
│ └───────────────────────────────────────────────────────────┘     │                     │
└────────────────────────────────────────────────────────────────────┴─────────────────────┘
"✓ Concluída" em texto + Badge success (nunca só cor/ícone). Percentual com contagem.
Aula não concluída abre normalmente: sem cadeado, sem "bloqueada".
```

### A1.Q · Mobile 390 + aviso de retomada

```text
A1.Q  Mobile 390 (lista abaixo do player)
┌──────────────────────────────────┐
│ [</>] Code4Coders           ☰     │
│ _← Voltar para o Início_          │
│ Injeção de dependência           │
│ na prática                  (H1) │
│ ┌─ LessonPlayer ───────────────┐ │
│ │ ⟨ marina.alves@example.com ⟩ │ │
│ │      imagem do vídeo         │ │
│ │ [❚❚] ━━●━━ 04:12 / 48:20    │ │
│ │ [🔊] [1x ▾] [⛶]              │ │
│ └──────────────────────────────┘ │
│ ┌─ ResumeNotice ────────────────┐ │
│ │ ( i ) Retomando de 4:12       │ │
│ │ [ Começar do início ]         │ │
│ └───────────────────────────────┘ │
│ ( i ) Este conteúdo é de uso     │
│ pessoal. O seu e-mail aparece    │
│ sobre o vídeo durante a aula.    │
│ AULAS DO CURSO                   │
│ 37% · 3 de 8 aulas               │
│ ▾ Módulo 1 · Fundamentos         │
│ │ ✓ 1 Visão geral · Concluída    │
│ │ ▶ 2 Injeção de… ( Aula atual ) │
│ ▸ Módulo 2 · Persistência        │
└──────────────────────────────────┘
[ Começar do início ] volta a 0:00. O aviso some sozinho após alguns segundos
ou ao interagir; nunca some com foco dentro dele.
```

### A1.R · Sem retomada (começa do início)

```text
A1.R  Sem aviso: aula nova, avanço de fim, <10 s do fim ou posição além da duração
┌─ player ─────────────────────────┐
│ [▶] ━━●━━━━━━━━ 00:00 / 48:20    │
└──────────────────────────────────┘
Sem "Retomando de…". Reprodução começa do início (RN-P09).
```

### A1.S · Progresso fora do ar na aula

```text
A1.S  Progresso indisponível (vídeo toca do início, lista sem marcas)
┌─ player ─────────────────────────┐
│ [▶] ━━●━━━━━━━━ 00:00 / 48:20    │
└──────────────────────────────────┘
Lista: módulos e aulas sem "✓ Concluída" e sem percentual.
Falha do progresso nunca bloqueia o vídeo.
```

---

## 6. Acessibilidade

- Aula concluída indicada em texto ("✓ Concluída"), nunca só por ícone ou cor; na lista, o estado atual segue anunciado em texto ("Aula atual").
- Percentual sempre com texto equivalente para leitor de tela ("3 de 8 aulas concluídas"); barra `Progress` com valor e texto visível.
- Aviso "Retomando de m:ss" anunciado em região de status (`aria-live="polite"`); *Começar do início* é botão alcançável por teclado e o aviso não some enquanto tem foco.
- Seções "Seus cursos" e "Acesso encerrado" com títulos navegáveis (H3); curso encerrado sem semântica clicável (sem link/botão, sem hover de card clicável).
- Estados de erro anunciados (`role="alert"` em I1.e); carregamento em `role="status"` com Skeleton na geometria do conteúdo.
- `prefers-reduced-motion` respeitado no aviso de retomada e nos Skeleton; foco visível em Continuar, Começar, Tentar de novo e Começar do início.
- Datas absolutas em pt-BR ("Seu acesso terminou em 15/03/2028"); linguagem simples, sem "sessão", "avanço" nem "registro".

---

## 7. Decisões para você aprovar

1. **Início = "meus cursos"** no lugar reservado de `dashboard-screen.tsx`, mantendo o card de conta; sem rota nova (G1, I1.a, I1.b, DP-08).
2. **Seção "Seus cursos"** com título, "37% · 3 de 8 aulas" e [ Continuar ] / [ Começar ]; ordem: com avanço recente primeiro, nunca começados por último (G2, G4, I1.a).
3. **Seção "Acesso encerrado"** secundária, sem ação, com "Seu acesso terminou em 15/03/2028" e progresso como estava; curso encerrado não parece clicável (G3, DP-02).
4. **Estados**: carregando em Skeleton (I1.c); vazio "Você ainda não tem cursos" com [ Explorar cursos ] → `/cursos` (I1.d); indisponível "Não foi possível carregar seus cursos agora. Tente de novo em instantes." + [ Tentar de novo ], nunca o vazio (I1.e); progresso indisponível com "Seu progresso não pôde ser carregado agora", sem percentual, levando à primeira aula (I1.f, G6).
5. **Tela da aula**: "✓ Concluída" em texto além do ícone + "37% · 3 de 8 aulas" na lista; concluir assistindo marca sem recarregar; sem cadeado (G7, A1.P).
6. **Retomada**: player começa na última posição + aviso "Retomando de 4:12" com [ Começar do início ] perto do player; casos que voltam ao início sem aviso (G8, A1.Q, A1.R, RN-P09).
7. **Volta ao Início** na tela da aula (`_← Voltar para o Início_`) e sidebar com "Início" significando "meus cursos", sem item novo (G9, G10).
8. **Textos exatos** da seção 4 e de A1.P–A1.S, em pt-BR simples, com percentual sempre acompanhado da contagem e datas absolutas (G5).
9. **Acessibilidade** da seção 6 (texto além de cor/ícone, região de status, foco, títulos navegáveis, encerrado não clicável).
10. **Componentes novos:** `MyCoursesList`, `CourseProgressCard`, `EndedAccessCard`, `ResumeNotice`, `LessonList+Progress`.

---

## 8. Plano para o Figma (após aprovação deste ASCII)

Páginas novas no arquivo `Code4Coders — Design System`, sem mexer nas existentes:

1. **🧭 Fluxo — Progresso**: os fluxos da seção 2, com os frames das telas como nós.
2. **📱 Screens — Progresso**: I1.a a I1.f e A1.P a A1.S, todos os estados, em desktop 1440 e mobile 390 (lista ao lado no desktop, abaixo no celular). Tema Light; Dark em I1.a e I1.e.
3. **Components (proposta)**, na página de Screens: `MyCoursesList`, `CourseProgressCard`, `EndedAccessCard`, `ResumeNotice`, `LessonList+Progress` (marca "✓ Concluída" + percentual).

Tudo reusando o AppShell do aluno, `Card`, `Progress`, `Alert`, `Skeleton` e demais componentes do DS, sem valor fixo.

---

## 9. Handoff do Figma — aprovado

O desenho foi materializado no arquivo **Code4Coders — Design System** em 2026-10-04, nas páginas `Fluxo — Progresso` e `Screens — Progresso`, sobre o ASCII aprovado (seções 1 a 7), sem reabrir fluxo nem textos. A revisão começa pelo [índice de revisão](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=274-37125).

> **Aprovação:** Figma aprovado pelo responsável em 2026-10-04.

**Ajustes de desenho sem mudar decisão:** o card "Sua conta" do Início não mostra e-mail, porque o contrato de sessão não o fornece (decisão da aprovação do ASCII). No ASCII, I1.b e A1.Q são os celulares de I1.a e A1.P; no Figma, cada estado tem o seu par desktop e celular, com o mesmo código. Em A1.P o Módulo 2 aparece aberto para mostrar a aula atual. Em I1.f o aviso ("Seu progresso não pôde ser carregado agora") usa o `Alert` do DS, sem texto de descrição.

### 9.1 Fluxos e navegação

| Artefato | Descrição | Link e `node-id` |
|---|---|---|
| Índice de revisão | Links para cada grupo de telas | [274:37125](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=274-37125) |
| Fluxo 1 · Aluno — entrar, escolher, continuar, assistir, voltar | Entrada, estados do Início e tela da aula; cada nó tem **Revisar tela →** | [274:37058](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=274-37058) |
| Página Fluxo — Progresso | Página do arquivo | [270:4259](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=270-4259) |
| Página Screens — Progresso | Página do arquivo | [270:4260](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=270-4260) |

### 9.2 Inventário de telas e estados

Desktop em 1440 × 900; celular em 390 de largura (altura 844, ou a do conteúdo quando ele passa disso). Light em todas as telas; Dark em I1.a e I1.e (desktop).

#### I1 · Início = meus cursos — [seção 274:36985](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=274-36985)

| Tela / estado | Dimensão | Link e `node-id` |
|---|---|---|
| I1.a · Com cursos (vigentes + encerrado) — Desktop | 1440 × 900 | [270:4261](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=270-4261) |
| I1.b · Com cursos — Mobile | 390 × 1188 | [270:4296](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=270-4296) |
| I1.c · Carregando — Desktop | 1440 × 900 | [272:74](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=272-74) |
| I1.c · Carregando — Mobile | 390 × 844 | [272:108](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=272-108) |
| I1.d · Vazio — Desktop | 1440 × 900 | [272:139](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=272-139) |
| I1.d · Vazio — Mobile | 390 × 867 | [272:173](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=272-173) |
| I1.e · Indisponível — Desktop | 1440 × 900 | [272:204](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=272-204) |
| I1.e · Indisponível — Mobile | 390 × 844 | [272:238](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=272-238) |
| I1.f · Progresso indisponível — Desktop | 1440 × 900 | [272:269](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=272-269) |
| I1.f · Progresso indisponível — Mobile | 390 × 962 | [272:303](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=272-303) |
| I1.a · Com cursos — Desktop Dark | 1440 × 900 | [273:36334](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=273-36334) |
| I1.e · Indisponível — Desktop Dark | 1440 × 900 | [273:36371](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=273-36371) |

#### A1 · Tela da aula — [seção 274:36987](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=274-36987)

| Tela / estado | Dimensão | Link e `node-id` |
|---|---|---|
| A1.P · Lista com concluídas + percentual + retomada — Desktop | 1440 × 900 | [273:36681](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=273-36681) |
| A1.Q · Com progresso e retomada — Mobile | 390 × 1175 | [273:36841](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=273-36841) |
| A1.R · Sem retomada (começa do início) — Desktop | 1440 × 900 | [273:36998](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=273-36998) |
| A1.R · Sem retomada — Mobile | 390 × 1069 | [273:37148](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=273-37148) |
| A1.S · Progresso indisponível — Desktop | 1440 × 900 | [273:37295](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=273-37295) |
| A1.S · Progresso indisponível — Mobile | 390 × 1035 | [273:37404](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=273-37404) |

#### 🧩 Composições propostas — Progresso — [seção 274:36988](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=274-36988)

| Tela / estado | Dimensão | Link e `node-id` |
|---|---|---|
| CourseProgressCard (continuar · começar · sem número) | 708 × 618 | [270:36160](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=270-36160) |
| EndedAccessCard | 708 × 134 | [270:36175](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=270-36175) |
| ResumeNotice (desktop · mobile) | 888 × 220 | [270:36199](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=270-36199) |
| LessonItem+Progress (padrão · concluída · atual) | 328 × 236 | [270:36221](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=270-36221) |
| LessonList+Progress | 360 × 388 | [273:36456](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=273-36456) |

`MyCoursesList` é a composição "Seus cursos" + `CourseProgressCard` por curso vigente, desenhada diretamente em I1.a, I1.b e I1.f; não tem componente próprio. Os componentes novos acima estão registrados em [`docs/design/Components.md`](Components.md#progresso-do-aluno-cap-017).
