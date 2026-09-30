# Wireframes ASCII — Autoria de curso (CAP-005)

> **Status:** ASCII e Figma aprovados pelo responsável em 2026-09-29.
> **Responsável:** responsável pelo produto (usuário desta conversa), com aprovação explícita “Está aprovado” após receber o link do desenho real.
> **Figma:** [Índice de revisão](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=176-11040) · task_01 concluída; gate visual das tasks 2.0–7.0 satisfeito, preservadas as demais dependências.
> **Handoff:** usar estes fluxos, telas, estados e decisões no Figma; submeter o desenho visual à aprovação antes da implementação.
> **Objetivo:** definir fluxos, conteúdo e estados das telas de Autoria do `admin-spa` antes do desenho no Figma.
> **Fontes:** [PRD de autoria](../../tasks/prd-autoria-curso/prd.md) v1.0 (RF-01…RF-12, Experiência do Usuário),
> [TechSpec](../../tasks/prd-autoria-curso/techspec.md) v1.0 (Bloco Frontend, V-01…V-06),
> [contrato HTTP](../../tasks/prd-autoria-curso/api-contract.md) 1.0.0,
> [wireframes de Vídeos](wireframes-videos.md), [Acesso interno](wireframes-acesso-interno.md),
> [componentes](Components.md) e [Design System](../../DESIGN.md).

---

## 1. Inventário

### 1.1 O que o PRD entrega em tela

| Entrega | RF | Tela ou efeito visível |
|---|---|---|
| Permissões de leitura e edição | RF-01 | Menu e ações por permissão; leitura sem controles de escrita; B12 por link sem acesso |
| Criar curso | RF-02 | A2 sobre A1; curso novo abre A3 |
| Estruturar módulos e aulas | RF-03 | A3, A4 e A5; ordem e movimento por mouse ou teclado |
| Vincular vídeo pronto | RF-04 | A6 sobre A3; erro de vínculo sem perda do rascunho |
| Lista da escola | RF-05 | A1; três estados textuais e última edição |
| Publicar versão | RF-06 | A7; pendências por item ou conferência com nota opcional |
| Republicar e descartar | RF-07 | A3, A7 e A8; rascunho separado da versão vigente |
| Histórico de versões | RF-08 | A9 no editor e A10 para retrato imutável |
| Excluir curso nunca publicado | RF-09 | A11; indisponível para curso já publicado |
| Fato da versão publicada | RF-10 | Sem tela própria; sucesso de A7 inicia a propagação |
| Ato de publicação | RF-11 | Sem tela de Autoria; o administrador o encontra na trilha existente de Auditoria |
| Referências de Uso na Mídia | RF-12 | Sem tela própria; consumidas pela reprodução de CAP-007 no futuro |

### 1.2 Telas e sobreposições (`admin-spa`, base `/admin/`)

| # | Tela | Rota | Permissão | Estados principais |
|---|---|---|---|---|
| A1 | Cursos da escola | `/admin/autoria` | `autoria.ler` | lista · vazia · filtro vazio · carregando · erro · somente leitura |
| A2 | Novo curso | Dialog em A1 | `autoria.editar` | formulário · título inválido · criando · erro |
| A3 | Editor do rascunho | `/admin/autoria/{courseId}` | `autoria.ler` | vazio · estruturado · publicado · alterações não publicadas · somente leitura · carregando · erro |
| A4 | Criar/editar curso, módulo ou aula | Dialog em A3 | `autoria.editar` | formulário contextual · inválido · salvando · erro |
| A5 | Mover item e confirmar remoção | menu + AlertDialog em A3 | `autoria.editar` | mover por teclado · remover aula · remover módulo com contagem |
| A6 | Escolher vídeo pronto | Sheet em A3 | `autoria.editar` e `midia.enviar` | resultados · sem vídeo · sem resultado · carregando · indisponível · vínculo ainda não aceito |
| A7 | Publicar/republicar | Dialog em A3 | `autoria.editar` | pendências · conferência · publicando · revisão mudou · erro |
| A8 | Descartar alterações | AlertDialog em A3 | `autoria.editar` | confirmar · revisão mudou · erro |
| A9 | Histórico | aba em A3 | `autoria.ler` | versões · vazio · carregando · erro |
| A10 | Versão histórica | `/admin/autoria/{courseId}/versoes/{versionNumber}` | `autoria.ler` | vigente · anterior · carregando · não encontrada · erro |
| A11 | Excluir rascunho | AlertDialog em A1 ou A3 | `autoria.editar` | confirmar · excluindo · erro |
| A12 | Início do professor | `/admin/` | sessão; cards por permissão | card Autoria ao lado do card Vídeos existente |
| B12/B13 | Sem permissão / erro geral | rotas protegidas | — | reusa os frames de Acesso interno |

**Rotas:** são relativas à origem pública do backoffice. No Compose, a lista é
`http://localhost:8081/admin/autoria`; no desenvolvimento remoto, o prefixo é
`https://c4c-admin.lab.tasso.dev.br/admin/autoria`. A versão histórica tem URL própria; A2 e A4–A9
são estados do editor/lista, sem rota nova. O link de pendência usa `#modulo-{moduleId}` ou
`#aula-{lessonId}` no editor.

### 1.3 Decisões de desenho aprovadas (PRD × TechSpec × contrato × DS)

| # | Ponto | Proposta e fundamento |
|---|---|---|
| G1 | Lugar no menu | Item **Autoria** no grupo **Conteúdo**, junto de **Vídeos**; a área reservada de CAP-002 passa a abrir A1. O agrupamento já foi previsto no wireframe aprovado de Vídeos. |
| G2 | Lista | Abas **Todos · Rascunhos · Publicados** filtram `status`; publicados com mudanças continuam em Publicados. A API não oferece busca por título. Ordem fixa: última edição, mais recente primeiro. |
| G3 | Identidade visual dos estados | `Badge` com ícone e texto: **Rascunho**, **Publicado · vN**, **Publicado · vN · alterações não publicadas**. Cor reforça o estado, nunca o substitui. |
| G4 | Editor | `Accordion` por módulo e `LessonItem` por aula, conforme `Components.md`; ações de item ficam em `DropdownMenu`. O topo mantém status, criador e última edição visíveis. |
| G5 | Gravação | Cada ação por item salva imediatamente e mostra confirmação discreta; não há botão global “Salvar”. Título/descrição são editados em Dialog, de acordo com o contrato de escrita por item. |
| G6 | Ordenação acessível | Alça para arrastar e, no menu, **Mover para cima/baixo**; aula também tem **Mover para outro módulo**. O foco permanece no mesmo item após a operação. |
| G7 | Seletor de vídeo | `Sheet` lateral no desktop e inferior no mobile, porque é uma lista. Só consulta vídeos `ready` da escola; mostra título, duração e autor, sem miniatura nem player (CAP-007). |
| G8 | Publicação | Ao abrir A7, o rascunho exibido orienta a conferência local. O servidor decide ao confirmar: `COURSE_INCOMPLETE` apresenta a mesma lista de pendências; `DRAFT_CHANGED` exige recarregar e confirmar novamente. |
| G9 | Versão e rascunho | No editor, abas **Rascunho · Histórico**; A10 lê um retrato imutável com rota própria. “Alterações não publicadas” nunca aparece como versão nova. |
| G10 | Exclusão | Só rascunho nunca publicado oferece **Excluir curso**. Remover aula/módulo também exige confirmação; remover módulo informa quantas aulas saem. |
| G11 | Paginação | 20 cursos/vídeos/versões por página, com `Pagination`; dentro do teto `_size` de 50. |
| G12 | Ajuda sobre oferta | Texto contextual: “Preço, nível e oferta pertencem à oferta do curso, prevista para uma etapa futura.” Não existem campos correspondentes neste editor. |
| G13 | Entrada pelo Início | Acrescentar card Autoria ao lado do card Vídeos do professor, sem contagem, visível com `autoria.ler`. |

---

## 2. Fluxos do usuário

### 2.1 Criar, montar e publicar

```text
  A12 Início ou menu Conteúdo
             │ autoria.ler
             ▼
  ┌───────────────────────────────┐  [ Novo curso ]  ┌──────────────────────────┐
  │ A1 Cursos da escola           │─────────────────▶│ A2 Título + descrição    │
  │ todos / rascunhos / publicados│                  │ título obrigatório       │
  └──┬───────────────────────┬────┘                  └────────────┬─────────────┘
     │ abrir curso           │ sem autoria.editar                │ criar
     │                       └──▶ lista somente leitura         ▼
     │                          sem [ Novo curso ]      ┌─────────────────────────┐
     └──────────────────────────────────────────────────▶│ A3 Editor · Rascunho    │
                                                         │ módulos → aulas         │
                                                         └───┬──────┬─────────┬────┘
                                                             │      │         │
                                     [ + Módulo / + Aula ] ───┘      │         │
                                             ▼                       │         │
                                     ┌──────────────┐  [ Vídeo ] ────┘         │
                                     │ A4 Formulário│        ▼                  │
                                     │ curso/item   │  ┌──────────────┐         │
                                     └──────┬───────┘  │ A6 Prontos   │         │
                                            │ salvo    │ da escola    │         │
                                            └─────────▶│ selecionar   │──▶ A3   │
                                                       └──────────────┘         │
                                             ⋯ item → A5 mover/remover           │
                                                                                │
                                                            [ Publicar ] ◀───────┘
                                                                 ▼
                                  ┌───────────────────────────────────────────────┐
                                  │ A7 Conferência                                │
                                  │ incompleto → pendências com link para A3      │
                                  │ completo → resumo + nota opcional → confirmar │
                                  └───────────────┬───────────────────────────────┘
                                                  │ sucesso: v1
                                                  ▼
                                  A3 Publicado · v1 + A9 Histórico → A10 Versão 1
```

### 2.2 Corrigir, republicar, descartar e excluir

```text
  A3 Publicado · v1 ── editar curso/módulo/aula/vídeo ──▶ A3 v1 · alterações não publicadas
          │                                                   │
          │ [ Histórico ] → A9 → A10 v1 (imutável)            ├─ [ Publicar nova versão ]
          │                                                   │         A7 → v2 vigente
          │                                                   │         v1 continua em A9/A10
          │                                                   └─ [ Descartar alterações ]
          │                                                             A8 → rascunho = v1
          └─ nunca há [ Excluir curso ]

  A1/A3 Rascunho nunca publicado ── [ Excluir curso ] ──▶ A11 confirmar ──▶ A1 sem curso

  Em A7 ou A8: 409 DRAFT_CHANGED → atualizar A3 → professor revê o novo rascunho → nova confirmação.
  Em A6: VIDEO_NOT_AVAILABLE → manter A3 intacto e oferecer [ Atualizar vídeos ] / [ Tentar de novo ].
  Sem autoria.ler por link direto → B12; sessão revogada → B1 Entrar (comportamento de CAP-002).
```

---

## 3. Layout base e componentes

### 3.1 AppShell

Reusa `Sidebar` 264, `Topbar` 68 e conteúdo `bg-muted` do backoffice. Em mobile, a sidebar vira
`Sheet` aberto pelo botão ☰. O grupo **Conteúdo** só aparece com alguma área autorizada; **Autoria**
exige `autoria.ler` e **Vídeos** exige `midia.enviar`. O papel professor tem as duas permissões.

```text
┌──────────────────────┬──────────────────────────────────────────────────────────────────────┐
│ [</>] Code4Coders    │                                     [◐ tema] ( RS ) Rafael Silva ▾  │
│       Backoffice     ├──────────────────────────────────────────────────────────────────────┤
│ ▣ Início             │                                                                      │
│ CONTEÚDO             │   << A1, A3 ou A10 >>                                                │
│ ▤ Autoria  ◀ ativo   │                                                                      │
│ 🎬 Vídeos            │                                                                      │
│                      │                                                                      │
└──────────────────────┴──────────────────────────────────────────────────────────────────────┘
```

### 3.2 Componentes

Reuso: `Sidebar`, `Breadcrumb`, `Card`, `Badge`, `Table`, `Tabs`, `Accordion`, `DropdownMenu`,
`Dialog`, `AlertDialog`, `Sheet`, `FormField`, `Input`, `Textarea`, `Button`, `Alert`, `Skeleton`,
`Pagination`, `EmptyState` e `Sonner`. Os ícones são `lucide-react` com nome acessível nos botões.

**Composições propostas para o Figma:** `CourseStatusBadge`, `ModuleAccordionHeader`, `LessonItem`
com estado “sem vídeo” e “vídeo pronto”, `PublicationPendencyList` e `VideoOption`. São composições
visuais; a decisão de criar um `block` em código segue a regra de três usos de `Components.md`.

**Legenda dos desenhos:** `[ Ação ]` botão · `_link_` navegação · `( Estado )` badge · `⋯` menu
com rótulo acessível · `≡` alça de ordenação · `(!)` aviso · `✓` confirmação. Os textos e exemplos
são ilustrativos; identificadores técnicos nunca aparecem como navegação normal.

---

## 4. Wireframes

### A1 · Cursos da escola — `/admin/autoria`

```text
A1.a  Lista (desktop 1440, com autoria.editar)
┌──────────────────────┬──────────────────────────────────────────────────────────────────────┐
│ ▣ Início             │  AUTORIA                                                             │
│ CONTEÚDO             │  Cursos da escola                                  [ + Novo curso ]  │
│ ▤ Autoria ◀          │  Monte o currículo e publique quando estiver pronto.                 │
│ 🎬 Vídeos            │                                                                      │
│                      │  [ Todos ] [ Rascunhos ] [ Publicados ]                               │
│                      │  ┌────────────────────────────────────────────────────────────────┐  │
│                      │  │ Curso                    Estado               Última edição     │  │
│                      │  ├────────────────────────────────────────────────────────────────┤  │
│                      │  │ .NET do zero à API       (Publicado · v2 ·    Marina Alves       │  │
│                      │  │                          alterações não      hoje, 10:20        │  │
│                      │  │                          publicadas)         [ Abrir → ]         │  │
│                      │  │ Testes na prática        (Rascunho)           Rafael Souza       │  │
│                      │  │                                               ontem, 15:02       │  │
│                      │  │                                               [ Abrir → ]  ⋯     │  │
│                      │  │ Introdução a APIs       (Publicado · v1)    Júlia Lima          │  │
│                      │  │                                               24/09, 09:30       │  │
│                      │  │                                               [ Abrir → ]         │  │
│                      │  └────────────────────────────────────────────────────────────────┘  │
│                      │                                            ‹ 1 2 3 › · 20 por página   │
└──────────────────────┴──────────────────────────────────────────────────────────────────────┘
⋯ só no curso nunca publicado: “Excluir curso”. Cada autor da mesma escola pode abrir/editar.
```

```text
A1.b  Escola sem cursos                         A1.c  Filtro sem resultados
┌─ EmptyState ─────────────────────────────┐    ┌──────────────────────────────────────────┐
│ ┌─ CodeWindow ──────────── ● ● ● ─────┐  │    │ [ Todos ] [ Rascunhos ] [ Publicados ]  │
│ │ // nenhum curso por aqui ainda      │  │    │                                          │
│ └──────────────────────────────────────┘  │    │ Nenhum curso publicado ainda.            │
│ Nenhum curso ainda.                       │    │ _Ver todos os cursos_                     │
│ Crie o primeiro e organize suas aulas.   │    └──────────────────────────────────────────┘
│ [ + Novo curso ]                          │
└───────────────────────────────────────────┘

A1.d  Carregando: Skeleton de 5 linhas; abas visíveis.
A1.e  Serviço indisponível: Alert “Não foi possível carregar os cursos.” [ Tentar de novo ].
A1.f  Só autoria.ler: mesmos cursos, sem [ Novo curso ] e sem ⋯; A3 também é somente leitura.
```

### A2 · Novo curso — Dialog sobre A1

```text
┌─ Dialog (máx. 520) ───────────────────────────────────────── ✕ ┐
│ Novo curso                                                 (H4) │
│ Crie o rascunho. Você poderá montar as aulas depois.           │
│                                                                │
│ Título *                                                        │
│ [.NET do zero à API____________________________]           18/200│
│                                                                │
│ Descrição pedagógica (opcional)                                 │
│ [Do primeiro projeto à API e aos testes...__________]          │
│ [____________________________________________________]      44/5000│
│ O que a pessoa aprenderá neste curso.                          │
│                                                                │
│ Preço, nível e oferta pertencem à oferta futura. (G12)         │
│                                  [ Cancelar ] [ Criar curso ]   │
└────────────────────────────────────────────────────────────────┘
Título vazio/só espaços → erro junto ao campo: “Dê um título ao curso”; Criar desabilitado.
Criando → “Criando…”; sucesso → abre A3 na rota do `courseId`; falha → mantém os valores e mostra
Alert “Não foi possível criar o curso. Tente de novo.”
```

### A3 · Editor do rascunho — `/admin/autoria/{courseId}`

```text
A3.a  Curso criado, sem módulo
┌──────────────────────┬──────────────────────────────────────────────────────────────────────┐
│ ▤ Autoria ◀          │  Autoria › Cursos › .NET do zero à API                               │
│                      │  .NET do zero à API                           (Rascunho)            │
│                      │  Do primeiro projeto à API e aos testes.        [ Editar dados ]     │
│                      │  Criado por Rafael Souza · Editado por Rafael hoje, 10:20          │
│                      │                                                                      │
│                      │  [ Rascunho ] [ Histórico ]                 [ Publicar curso ]       │
│                      │  ┌─ EmptyState ──────────────────────────────────────────────────┐  │
│                      │  │ Comece pelos módulos. Cada módulo terá ao menos uma aula      │  │
│                      │  │ com vídeo antes da publicação.      [ + Adicionar módulo ]    │  │
│                      │  └───────────────────────────────────────────────────────────────┘  │
│                      │  _Excluir curso_ (só porque nunca foi publicado)                  │
└──────────────────────┴──────────────────────────────────────────────────────────────────────┘
```

```text
A3.b  Rascunho estruturado
┌─────────────────────────────────────────────────────────────────────────────────────────────┐
│ Autoria › Cursos › .NET do zero à API                    (Rascunho)  [ Editar dados ]        │
│ Criado por Rafael · Última edição: Marina hoje, 10:20                                    │
│ [ Rascunho ] [ Histórico ]                                     [ Publicar curso ]           │
│                                                                                             │
│ Módulos                                                        [ + Adicionar módulo ]      │
│ ┌─ Accordion · modulo-1 ──────────────────────────────────────────────────────────────────┐ │
│ │ ≡ 1  Fundamentos da linguagem  (2 aulas)               [ + Aula ] [ Recolher ] [ ⋯ ] │ │
│ ├─────────────────────────────────────────────────────────────────────────────────────────┤ │
│ │ ≡ 1  Tipos e variáveis     Vídeo: Aula 1 — tipos · 42:15          [ Trocar vídeo ] [ ⋯ ] │ │
│ │      Conceitos básicos de tipos.                                                      │ │
│ │ ≡ 2  Controle de fluxo    (Sem vídeo)                         [ Escolher vídeo ] [ ⋯ ] │ │
│ └─────────────────────────────────────────────────────────────────────────────────────────┘ │
│ ┌─ Accordion · modulo-2 ──────────────────────────────────────────────────────────────────┐ │
│ │ ≡ 2  Coleções  (0 aulas)                              [ + Aula ] [ Expandir ] [ ⋯ ] │ │
│ └─────────────────────────────────────────────────────────────────────────────────────────┘ │
│                                                                                             │
│ (!) 2 pendências antes de publicar. [ Ver pendências ] → A7.a                              │
└─────────────────────────────────────────────────────────────────────────────────────────────┘
≡ aceita arrastar; ⋯ oferece os mesmos movimentos por teclado. A ordem salva vem da resposta.
```

```text
A3.c  Curso publicado, sem mudanças                    A3.d  Curso publicado, com mudanças
┌───────────────────────────────────────────────┐      ┌────────────────────────────────────────────┐
│ .NET do zero à API                             │      │ .NET do zero à API                        │
│ (Publicado · v1)                               │      │ (Publicado · v1 · alterações não         │
│                                               │      │  publicadas)                              │
│ O rascunho está igual à versão vigente.       │      │ As alterações ficam no rascunho até você │
│ [ Rascunho ] [ Histórico ]                     │      │ publicar uma nova versão.                 │
│ [ Publicar nova versão ]                       │      │ [ Publicar nova versão ]                  │
│ _Ver versão vigente_ → A10                     │      │ [ Descartar alterações ] → A8             │
│ Sem “Excluir curso”.                           │      │ _Ver versão vigente_ → A10                │
└───────────────────────────────────────────────┘      └────────────────────────────────────────────┘

A3.e  Só autoria.ler: mesmos títulos, ordem, vídeos, estado e histórico; sem alças, ⋯,
      [ Editar dados ], [ + ], [ Escolher/Trocar vídeo ], [ Publicar ] ou [ Descartar ].
A3.f  Mídia não respondeu ao enriquecer uma aula: “Vídeo vinculado · ID 6f1e…5d6e” com
      Alert discreto “Os detalhes deste vídeo não estão disponíveis agora.” O editor continua.
A3.g  Carregando: Skeleton de cabeçalho + módulos; 404: B13 “Curso não encontrado”;
      502/504: Alert “Não foi possível carregar o curso.” [ Tentar de novo ].
```

### A4 · Formulários curtos de curso, módulo e aula — Dialog

```text
A4.a  Editar curso                          A4.b  Novo/editar módulo
┌─ Dialog ───────────────────────────── ✕ ┐  ┌─ Dialog ─────────────────────────── ✕ ┐
│ Editar dados do curso                   │  │ Novo módulo                          │
│ Título * [ .NET do zero à API_____ ]    │  │ Título * [ Fundamentos da linguagem ] │
│ Descrição pedagógica (opcional)         │  │                  [ Cancelar ] [ Criar ]│
│ [ Do primeiro projeto à API...____ ]   │  └───────────────────────────────────────┘
│ [ Cancelar ] [ Salvar alterações ]      │
└─────────────────────────────────────────┘

A4.c  Nova/editar aula
┌─ Dialog ───────────────────────────────────────── ✕ ┐
│ Nova aula                                             │
│ Módulo: Fundamentos da linguagem                      │
│ Título * [ Tipos e variáveis____________________ ]     │
│ Descrição pedagógica (opcional)                       │
│ [ Conceitos básicos de tipos.___________________ ]     │
│                [ Cancelar ] [ Criar aula ]            │
└───────────────────────────────────────────────────────┘
Depois de criar, “Escolher vídeo” abre A6. Títulos: até 200 caracteres; descrições: até 5 000.
Erro de campo fica junto dele; erro de rede mantém o Dialog e os valores. Salvo → resposta do
servidor substitui A3 e toast “Alterações salvas”. Limites: 100 módulos/curso e 200 aulas/módulo;
`STRUCTURE_LIMIT_REACHED` explica qual limite foi atingido e não acrescenta item na tela.
```

### A5 · Ordenar, mover e remover módulos/aulas

```text
A5.a  Menu do módulo                  A5.b  Menu da aula
┌─────────────────────────┐          ┌─────────────────────────────┐
│ Editar título           │          │ Editar aula                  │
│ Mover para cima         │          │ Mover para cima              │
│ Mover para baixo        │          │ Mover para baixo             │
│ Remover módulo…         │          │ Mover para outro módulo  ›   │
└─────────────────────────┘          │ Remover aula…               │
                                     └─────────────────────────────┘
Comando impossível (primeiro/último item, módulo único) fica desabilitado com razão acessível.
“Mover para outro módulo” abre submenu com os outros módulos e coloca a aula no fim do escolhido;
arrastar e comandos de menu preservam o ID da aula. Ao concluir, foco volta ao item movido.

A5.c  Remover módulo                         A5.d  Remover aula
┌─ AlertDialog ──────────────────────────┐    ┌─ AlertDialog ─────────────────────────┐
│ Remover “Fundamentos da linguagem”?    │    │ Remover “Tipos e variáveis”?          │
│ As 2 aulas deste módulo também saem   │    │ Ela sairá do rascunho. Uma aula nova  │
│ do rascunho. Uma versão já publicada   │    │ terá outra identidade, mesmo com o   │
│ continua como estava.                  │    │ mesmo título.                        │
│ [ Cancelar ] [ Remover módulo ]         │    │ [ Cancelar ] [ Remover aula ]         │
└────────────────────────────────────────┘    └───────────────────────────────────────┘
```

### A6 · Escolher vídeo pronto — Sheet sobre A3

```text
A6.a  Com vídeos (desktop: lateral; mobile: inferior)
┌─ Sheet · Escolher vídeo para “Controle de fluxo” ────────────────────── ✕ ┐
│ Só vídeos prontos desta escola podem entrar na aula.                    │
│ [ Buscar título____________________________________________ ]           │
│                                                                         │
│ ○ Aula 1 — tipos                  42:15 · Júlia Lima   (Pronto)          │
│ ○ Controle de fluxo explicado    18:32 · Rafael      (Pronto)          │
│ ○ Parte 3 — exercícios           26:10 · Júlia Lima   (Pronto)          │
│                                                                         │
│                                            ‹ 1 2 › · 20 por página       │
│ [ Ir para Vídeos ]                   [ Cancelar ] [ Vincular vídeo ]    │
└─────────────────────────────────────────────────────────────────────────┘
Na troca, a opção atual vem selecionada; [ Desvincular vídeo ] é ação secundária explícita.
Nenhum item permite assistir aqui. O título exibido é o atual em Mídia, não uma cópia no curso.

A6.b  Sem vídeo pronto: “Ainda não há vídeos prontos. Envie um vídeo na área Vídeos e volte
      quando a preparação terminar.” [ Ir para Vídeos ] [ Atualizar lista ].
A6.c  Busca sem resultado: “Nenhum vídeo pronto com esse título.” [ Limpar busca ].
A6.d  Carregando: Skeleton de opções; falha de Mídia: Alert “Não foi possível carregar os vídeos.”
      [ Tentar de novo ]. A aula não muda.
A6.e  `VIDEO_NOT_AVAILABLE` depois de escolher: Alert “Este vídeo ainda não está disponível
      para vínculo. Aguarde alguns instantes e tente de novo.” [ Atualizar vídeos ]. Mantém
      o Sheet aberto e o vínculo anterior intacto. Nunca oferece vídeo em preparação/falhou.
```

### A7 · Publicar ou republicar — Dialog sobre A3

```text
A7.a  Pendências: não há publicação
┌─ Dialog ────────────────────────────────────────────────────────────── ✕ ┐
│ Antes de publicar                                                        │
│ (!) Complete o currículo. Nenhuma versão foi criada.                    │
│                                                                         │
│ 1. Coleções · sem aulas                   _Abrir módulo_               │
│ 2. Controle de fluxo · sem vídeo           _Abrir aula_                 │
│                                                                         │
│                                               [ Voltar ao editor ]     │
└─────────────────────────────────────────────────────────────────────────┘
Se o curso não tiver nenhum módulo, a pendência é “Curso sem módulos” com _Adicionar módulo_. A
lista mostra só as pendências do currículo atual, em ordem pedagógica; quando o servidor as devolve,
preserva a ordem recebida. Cada link fecha o Dialog,
navega para `#modulo-{moduleId}` ou `#aula-{lessonId}`,
expande o módulo, rola até o item e leva o foco ao título/ação correspondente. “Curso sem módulos”
leva foco a [ + Adicionar módulo ]. O título do aviso recebe foco e a lista usa `aria-live`.

A7.b  Currículo completo (primeira publicação)
┌─ Dialog ────────────────────────────────────────────────────────────── ✕ ┐
│ Publicar curso                                                           │
│ Confira o que será publicado. Esta será a versão 1.                      │
│                                                                         │
│ .NET do zero à API · 2 módulos · 3 aulas com vídeo                        │
│ 1  Fundamentos da linguagem                                              │
│    1 Tipos e variáveis — Aula 1 — tipos                                   │
│    2 Controle de fluxo — Controle de fluxo explicado                     │
│ 2  Coleções                                                              │
│    1 Listas — Parte 3 — exercícios                                        │
│                                                                         │
│ Nota de versão (opcional)                                                │
│ [ Primeira versão do curso._____________________________________ ]       │
│                                                               25/1000   │
│                              [ Cancelar ] [ Publicar versão 1 ]        │
└─────────────────────────────────────────────────────────────────────────┘
A7.c  Republicação: mesmo desenho, título “Publicar nova versão”; informa “A versão 2 ficará
      vigente para todos; a versão 1 continuará no histórico.” Botão [ Publicar versão 2 ].
A7.d  Enviando: botão “Publicando…” desabilitado contra clique duplo. Sucesso fecha o Dialog,
      atualiza A3/A9 e mostra toast “Versão 2 publicada”. O fato e o ato seguem em segundo plano;
      a tela não afirma que os consumidores já processaram as mensagens.
A7.e  `COURSE_INCOMPLETE`: muda para A7.a com todas as pendências do servidor.
A7.f  `DRAFT_CHANGED`: Alert “O rascunho mudou desde que você abriu esta confirmação.”
      [ Atualizar curso ]; após atualizar, fecha A7 e exige nova revisão/confirmação.
A7.g  Falha de rede/serviço: “Não foi possível confirmar a publicação. Confira o estado do curso
      e tente novamente.” A mesma intenção usa a mesma chave no retry; não mostra sucesso sem 201.
```

### A8 · Descartar alterações — AlertDialog

```text
┌─ AlertDialog ──────────────────────────────────────────────────────────┐
│ Descartar alterações não publicadas?                                   │
│ O rascunho voltará a ser igual à versão 1 vigente. O que foi publicado │
│ permanece igual; as mudanças no rascunho não poderão ser recuperadas.  │
│                                [ Cancelar ] [ Descartar alterações ]   │
└────────────────────────────────────────────────────────────────────────┘
Só aparece com `hasUnpublishedChanges: true`. Sucesso → A3 “Publicado · v1”, sem aviso de
alterações. `DRAFT_CHANGED` → recarrega A3, informa que um colega editou e pede nova confirmação.
```

### A9 e A10 · Histórico e versão publicada

```text
A9.a  Aba Histórico no editor
┌──────────────────────────────────────────────────────────────────────────┐
│ [ Rascunho ] [ Histórico ]                                               │
│ Versões publicadas                                                       │
│ ┌──────────────────────────────────────────────────────────────────────┐ │
│ │ v3 (Vigente) · 05/10/2026 10:00 · Rafael Souza                       │ │
│ │ “Aula 4 regravada com o SDK novo”                    [ Ver versão → ] │ │
│ │ v2 · 03/10/2026 14:10 · Marina Alves                 [ Ver versão → ] │ │
│ │ v1 · 01/10/2026 09:30 · Marina Alves                 [ Ver versão → ] │ │
│ └──────────────────────────────────────────────────────────────────────┘ │
│                                                ‹ 1 2 › · 20 por página    │
└──────────────────────────────────────────────────────────────────────────┘
A9.b  Nunca publicado: “Este curso ainda não tem versões publicadas.” _Voltar ao rascunho_.
A9.c  Carregando: Skeleton de linhas; erro: Alert [ Tentar de novo ].

A10  `/admin/autoria/{courseId}/versoes/{versionNumber}`
┌──────────────────────────────────────────────────────────────────────────┐
│ Autoria › .NET do zero à API › Histórico › Versão 2                     │
│ Versão 2  (Anterior)                 _Voltar ao rascunho_                │
│ Publicada por Marina Alves · 03/10/2026 14:10                            │
│ Nota: “Corrigidas as aulas de testes.”                                   │
│ (!) Retrato da publicação. Alterações posteriores não mudam esta versão.│
│                                                                          │
│ 1  Fundamentos da linguagem                                              │
│    1 Tipos e variáveis · Vídeo vinculado: 6f1e…5d6e                      │
│    2 Controle de fluxo · Vídeo vinculado: 4b2c…9a10                     │
│ 2  Coleções                                                              │
│    1 Listas · Vídeo: Parte 3 — exercícios                                 │
└──────────────────────────────────────────────────────────────────────────┘
Na versão vigente, badge “Vigente”; em versão anterior, “Anterior”. O retrato histórico guarda o
`videoId`, não o título histórico do vídeo; a referência completa pode ser copiada. Sem ações de
edição nem player. A rota pode ser recarregada diretamente. Versão inexistente → B13 “Versão não
encontrada”.
```

### A11 · Excluir curso nunca publicado — AlertDialog

```text
┌─ AlertDialog ──────────────────────────────────────────────────────────┐
│ Excluir “Testes na prática”?                                             │
│ Este curso nunca foi publicado. O rascunho, seus módulos e suas aulas  │
│ serão removidos. Esta ação não pode ser desfeita.                       │
│                                      [ Cancelar ] [ Excluir curso ]     │
└────────────────────────────────────────────────────────────────────────┘
Sucesso → volta a A1 e toast “Curso excluído”. Erro mantém o Dialog sem apagar a linha.
`COURSE_ALREADY_PUBLISHED` após mudança concorrente → fecha, atualiza A3/A1 e explica que um
curso publicado não pode ser excluído. Nenhum controle de exclusão em A3 publicado ou A10.
```

### A12 · Início do professor — `/admin/`

O card Autoria aparece para quem tem `autoria.ler`, ao lado do card Vídeos de
[CAP-006](wireframes-videos.md). Cada card obedece à própria permissão; não mostra contagem que
exigiria consulta extra.

```text
┌──────────────────────┬──────────────────────────────────────────────────────────────┐
│ ▣ Início ◀           │  INÍCIO                                                      │
│ CONTEÚDO             │  Olá, Rafael · (Professor)                                    │
│ ▤ Autoria            │                                                              │
│ 🎬 Vídeos            │  ┌─ Card ──────────────────┐  ┌─ Card ─────────────────────┐  │
│                      │  │ ▤  Autoria              │  │ 🎬  Vídeos                │  │
│                      │  │ Monte módulos e aulas,  │  │ Envie gravações e        │  │
│                      │  │ depois publique.        │  │ acompanhe a preparação. │  │
│                      │  │ [ Abrir Autoria → ]      │  │ [ Abrir Vídeos → ]        │  │
│                      │  └─────────────────────────┘  └───────────────────────────┘  │
└──────────────────────┴──────────────────────────────────────────────────────────────┘
```

### Reflexo na trilha de Auditoria (RF-11)

Depois que a Auditoria processar o ato, o administrador vê a publicação na tela existente
`/admin/auditoria`, como “Versão publicada”, com alvo curso e estado “Conforme”. Na lista e no
detalhe, o BFF tenta resolver o título do curso; se não conseguir, permanece a referência sem
título. A nota de versão não é motivo de auditoria. Este recorte ajusta o rótulo e a resolução do
alvo nos [wireframes de Auditoria](wireframes-auditoria.md); não cria tela nova de Autoria.

```text
Auditoria · linha existente, após consumo do ato
┌───────────────────────────────────────────────────────────────────────────────────┐
│ Versão publicada · Curso “.NET do zero à API” · Rafael Souza · 05/10 10:00        │
│ (✓ Conforme)                                      [ Ver registro → ]              │
└───────────────────────────────────────────────────────────────────────────────────┘
```

### Mobile · 390 px (frames representativos)

```text
A1.mobile                                    A3.mobile
┌──────────────────────────────────────┐     ┌──────────────────────────────────────┐
│ ☰  AUTORIA                    (RS)   │     │ ☰  AUTORIA                    (RS)   │
│ Cursos da escola                     │     │ ‹ Cursos                             │
│ [ + Novo curso ]                     │     │ .NET do zero à API                   │
│ [ Todos ][ Rasc. ][ Publicados ]     │     │ (Publicado · v1 · alterações não    │
│ ┌─ Card ───────────────────────────┐ │     │  publicadas)                        │
│ │ .NET do zero à API               │ │     │ [ Publicar nova versão ]            │
│ │ (Publicado · v2)                 │ │     │ [ Rascunho ][ Histórico ]          │
│ │ Editado por Marina · hoje        │ │     │ [ + Adicionar módulo ]              │
│ │ [ Abrir → ]                      │ │     │ ┌─ Accordion ─────────────────────┐ │
│ └───────────────────────────────────┘ │     │ │ ≡ 1 Fundamentos        [ ⋯ ]     │ │
│ ┌─ Card ───────────────────────────┐ │     │ │ 2 aulas · expandir              │ │
│ │ Testes na prática · Rascunho   ⋯ │ │     │ └─────────────────────────────────┘ │
│ │ [ Abrir → ]                      │ │     │ _Descartar alterações_              │
│ └───────────────────────────────────┘ │     └──────────────────────────────────────┘
└──────────────────────────────────────┘
A6 vira Sheet inferior, com opções roláveis; A2/A4/A7 são Dialogs adaptados à largura, com
rolagem interna e ações alcançáveis por teclado. A7.a mantém links de pendências individualmente
focáveis; títulos e badges não dependem da cor para comunicar o estado.
```

### Estados transversais e acessibilidade

- **Permissão:** `autoria.ler` sem `autoria.editar` mantém A1, A3, A9 e A10, sem controles de
  mutação. Ausência de leitura esconde o item de menu e link direto leva a B12; esconder ações
  nunca substitui a autorização no servidor. Se a sessão for revogada, segue B1 de CAP-002.
- **Foco:** abrir Dialog/Sheet foca seu título ou primeiro campo; fechar devolve foco ao gatilho.
  Arrastar tem alternativa de menu acionável por teclado; após salvar ou mover, foco volta ao item.
  Pendência vai ao item mesmo com módulo recolhido e anuncia seu texto ao leitor de tela.
- **Resposta do servidor:** toda escrita atualiza o curso a partir da resposta completa e invalida
  lista/histórico afetados. Erro de rede preserva os valores do formulário. `DRAFT_CHANGED` nunca
  reenvia publicação/descarte automaticamente. Alterações de um colega aparecem na próxima leitura;
  esta entrega não promete edição colaborativa em tempo real.
- **Leitura histórica:** A10 mostra título, descrição e ordem do retrato publicado, inclusive
  títulos de aula originais, mas só a referência do vídeo (`videoId`). A3 pode mostrar o título
  atual do vídeo ou apenas `videoId` quando Mídia não responde; isso não muda o vínculo.
- **Sem escopo novo:** nenhuma tela de oferta, preço, nível, material complementar, avaliação,
  aula sem vídeo, pré-visualização/player ou acesso do aluno.

---

## 5. Handoff do Figma — aprovado

1. **Fluxo — Autoria:** os dois diagramas da seção 2, com A1–A12 como nós e as transições de erro.
2. **Screens — Autoria:** desktop 1440 para A1–A12 e seus estados; mobile 390 para A1, A2, A3,
   A6, A7 e A9. Light em todos; Dark em A1, A3, A6 e A7.
3. **Components (proposta):** `CourseStatusBadge`, `ModuleAccordionHeader`, `LessonItem`,
   `PublicationPendencyList` e `VideoOption`, reaproveitando os tokens e componentes aprovados.

O desenho foi materializado no arquivo **Code4Coders — Design System** em 2026-09-29.
A revisão começa pelo [índice de Autoria](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=176-11040).
O responsável pelo produto aprovou explicitamente o desenho nesta conversa em **2026-09-29**,
com a manifestação **“Está aprovado”**, após receber o link do Figma real.
A task_01 está concluída; o gate visual das tasks 2.0–7.0 foi satisfeito.
As dependências entre as tasks de implementação permanecem válidas.

### 5.1 Fluxos e navegação

| Artefato | Link e `node-id` |
|---|---|
| Índice de revisão | [176:11040](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=176-11040) |
| Fluxo — Criar, montar e publicar | [169:2](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=169-2) |
| Fluxo — Republicar, descartar e excluir | [169:152](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=169-152) |
| Página Fluxo — Autoria | [155:7450](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=155-7450) |
| Página Screens — Autoria | [155:7451](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=155-7451) |

O modo **Present** tem quatro entradas: **Criar e publicar** (A12), **Republicar ou descartar**
(A3 com mudanças), **Mobile 390** (A1) e **Dark** (A3). Botões, abas, menus de item, confirmação,
cancelamento e links de pendência navegam entre frames. Os fluxos e o índice abrem os frames
correspondentes pelo link do arquivo. Estados de falha também podem ser abertos diretamente
pelos links do inventário abaixo.

Os dados são ilustrativos e os estados são amostras para revisão visual. O protótipo não executa
escritas reais, busca, arraste, cópia para a área de transferência ou autorização do servidor.
Foco, teclado, preservação de valores e confirmação após conflito seguem a especificação das
seções 1–4; o Figma mostra sua apresentação e os caminhos, sem substituir a validação da aplicação.

### 5.2 Inventário de telas e estados

**Desktop · Light**

| Tela / estado | Dimensão | Link e `node-id` |
|---|---|---|
| A1.a · Cursos da escola · Todos | 1440 × 900 | [158:88](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=158-88) |
| A2.a · Novo curso · formulário | 1440 × 900 | [158:217](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=158-217) |
| A3.b · Rascunho estruturado | 1440 × 900 | [158:370](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=158-370) |
| A1 · Escola sem cursos | 1440 × 900 | [160:366](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=160-366) |
| A1 · Filtro sem resultados | 1440 × 900 | [160:448](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=160-448) |
| A1 · Carregando | 1440 × 900 | [160:526](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=160-526) |
| A1 · Serviço indisponível | 1440 × 900 | [160:596](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=160-596) |
| A1 · Filtro Rascunhos | 1440 × 900 | [160:691](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=160-691) |
| A1 · Filtro Publicados | 1440 × 900 | [160:779](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=160-779) |
| A1.f · Somente leitura | 1440 × 900 | [160:877](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=160-877) |
| A11 sucesso · Curso excluído | 1440 × 900 | [160:985](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=160-985) |
| A2 · Título inválido | 1440 × 900 | [160:1107](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=160-1107) |
| A2 · Criando | 1440 × 900 | [160:1258](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=160-1258) |
| A2 · Erro preserva formulário | 1440 × 900 | [160:1407](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=160-1407) |
| A3 · Curso sem módulos | 1440 × 900 | [160:1574](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=160-1574) |
| A3 · Currículo completo | 1440 × 900 | [160:1662](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=160-1662) |
| A3 · Publicado · v1 | 1440 × 900 | [160:1853](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=160-1853) |
| A3 · Alterações não publicadas | 1440 × 900 | [160:2050](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=160-2050) |
| A3 · Detalhes de vídeo indisponíveis | 1440 × 900 | [160:2272](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=160-2272) |
| A3 · Carregando | 1440 × 900 | [160:12831](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=160-12831) |
| A3 · Serviço indisponível | 1440 × 900 | [160:12886](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=160-12886) |
| A3 · Curso não encontrado | 1440 × 900 | [160:12966](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=160-12966) |
| A3 · Publicado · v2 | 1440 × 900 | [160:13046](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=160-13046) |
| A3.e · Somente leitura | 1440 × 900 | [160:13243](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=160-13243) |
| A3 · Módulo salvo | 1440 × 900 | [160:13348](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=160-13348) |
| A3 · Aula salva | 1440 × 900 | [160:13529](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=160-13529) |
| A3 · Item reordenado | 1440 × 900 | [160:13710](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=160-13710) |
| A3 · Aula movida para Coleções | 1440 × 900 | [160:13891](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=160-13891) |
| A3 · Módulo removido | 1440 × 900 | [160:14087](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=160-14087) |
| A3 · Aula removida | 1440 × 900 | [160:14180](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=160-14180) |
| A3 · Rascunho restaurado | 1440 × 900 | [160:14361](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=160-14361) |
| A3 · Rascunho atualizado para nova revisão | 1440 × 900 | [160:14558](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=160-14558) |
| A3 · Pendência · foco no módulo Coleções | 1440 × 900 | [160:14776](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=160-14776) |
| A3 · Pendência · foco na aula Controle de fluxo | 1440 × 900 | [160:14952](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=160-14952) |
| A4 · Editar curso | 1440 × 900 | [160:15128](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=160-15128) |
| A4 · Novo módulo | 1440 × 900 | [160:15336](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=160-15336) |
| A4 · Editar módulo | 1440 × 900 | [160:15535](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=160-15535) |
| A4 · Nova aula | 1440 × 900 | [160:15734](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=160-15734) |
| A4 · Editar aula | 1440 × 900 | [160:15942](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=160-15942) |
| A4 · Título inválido | 1440 × 900 | [160:16150](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=160-16150) |
| A4 · Salvando | 1440 × 900 | [160:16360](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=160-16360) |
| A4 · Falha preserva valores | 1440 × 900 | [160:16568](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=160-16568) |
| A4 · Limite de módulos | 1440 × 900 | [160:16794](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=160-16794) |
| A4 · Limite de aulas | 1440 × 900 | [160:17011](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=160-17011) |
| A5 · Menu do módulo | 1440 × 900 | [160:17237](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=160-17237) |
| A5 · Menu da aula | 1440 × 900 | [160:17427](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=160-17427) |
| A5 · Mover aula para outro módulo | 1440 × 900 | [160:17620](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=160-17620) |
| A5 · Confirmar remoção do módulo | 1440 × 900 | [160:17803](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=160-17803) |
| A5 · Confirmar remoção da aula | 1440 × 900 | [160:17995](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=160-17995) |
| A5 · Falha de remoção | 1440 × 900 | [160:18187](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=160-18187) |
| A6 · Vídeos prontos | 1440 × 900 | [160:18397](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=160-18397) |
| A6 · Seleção pendente | 1440 × 900 | [161:5623](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=161-5623) |
| A6 · Sem vídeo pronto | 1440 × 900 | [161:5850](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=161-5850) |
| A6 · Busca sem resultados | 1440 × 900 | [161:6054](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=161-6054) |
| A6 · Carregando | 1440 × 900 | [161:6257](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=161-6257) |
| A6 · Mídia indisponível | 1440 × 900 | [161:6459](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=161-6459) |
| A6 · Vídeo ainda indisponível para vínculo | 1440 × 900 | [161:6685](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=161-6685) |
| A6 · Trocar ou desvincular vídeo | 1440 × 900 | [161:6939](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=161-6939) |
| A6 · Vinculando vídeo | 1440 × 900 | [161:7169](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=161-7169) |
| A7 · Pendências | 1440 × 900 | [161:17755](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=161-17755) |
| A7 · Curso sem módulos | 1440 × 900 | [161:17989](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=161-17989) |
| A7 · Primeira publicação | 1440 × 900 | [161:18215](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=161-18215) |
| A7 · Republicar como versão 2 | 1440 × 900 | [161:18438](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=161-18438) |
| A7 · Publicando | 1440 × 900 | [161:18661](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=161-18661) |
| A7 · Pendências retornadas pelo servidor | 1440 × 900 | [161:18884](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=161-18884) |
| A7 · Rascunho mudou · nova revisão obrigatória | 1440 × 900 | [161:19118](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=161-19118) |
| A7 · Erro de publicação | 1440 × 900 | [161:19362](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=161-19362) |
| A8 · Confirmar descarte | 1440 × 900 | [161:19606](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=161-19606) |
| A8 · Rascunho mudou | 1440 × 900 | [161:19839](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=161-19839) |
| A8 · Erro de descarte | 1440 × 900 | [161:20089](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=161-20089) |
| A9 · Versões publicadas | 1440 × 900 | [161:20336](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=161-20336) |
| A9 · Nenhuma versão | 1440 × 900 | [161:20423](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=161-20423) |
| A9 · Carregando | 1440 × 900 | [161:20493](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=161-20493) |
| A9 · Erro de consulta | 1440 × 900 | [161:20557](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=161-20557) |
| A10 · Versão vigente · leitura imutável | 1440 × 900 | [161:20638](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=161-20638) |
| A10 · Versão anterior · leitura imutável | 1440 × 900 | [161:20738](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=161-20738) |
| A10 · Carregando | 1440 × 900 | [161:20838](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=161-20838) |
| A10 · Versão não encontrada | 1440 × 900 | [161:20901](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=161-20901) |
| A10 · Erro de consulta | 1440 × 900 | [161:20980](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=161-20980) |
| A11 · Confirmar exclusão | 1440 × 900 | [161:21059](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=161-21059) |
| A11 · Excluindo | 1440 × 900 | [161:21190](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=161-21190) |
| A11 · Erro preserva rascunho | 1440 × 900 | [161:21321](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=161-21321) |
| A11 · Publicado por colega · exclusão recusada | 1440 × 900 | [161:21470](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=161-21470) |
| A12 · Início do professor | 1440 × 900 | [161:21667](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=161-21667) |
| RF-11 · Auditoria · Versão publicada com título | 1440 × 960 | [161:21752](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=161-21752) |
| RF-11 · Auditoria · referência sem título | 1440 × 960 | [161:21816](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=161-21816) |
| B12 · Sem autoria.ler | 1440 × 900 | [161:21880](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=161-21880) |
| RF-11 · Auditoria · detalhe da versão publicada | 1440 × 1100 | [167:10975](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=167-10975) |
| RF-11 · Auditoria · detalhe da versão publicada · referência sem título | 1440 × 1100 | [167:11078](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=167-11078) |

**Mobile · Light · 390 px**

| Tela / estado | Dimensão | Link e `node-id` |
|---|---|---|
| A1 · Cursos da escola | 390 × 844 | [161:21938](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=161-21938) |
| A2 · Novo curso | 390 × 844 | [161:22014](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=161-22014) |
| A3 · Alterações não publicadas | 390 × 844 | [161:22124](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=161-22124) |
| A6 · Escolher vídeo pronto | 390 × 844 | [161:22272](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=161-22272) |
| A7 · Primeira publicação | 390 × 844 | [161:22442](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=161-22442) |
| A7 · Pendências | 390 × 844 | [161:22600](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=161-22600) |
| A9 · Histórico | 390 × 844 | [161:22765](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=161-22765) |
| A3 · Curso recém-criado | 390 × 844 | [168:10704](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=168-10704) |
| A3 · Rascunho estruturado | 390 × 844 | [168:10757](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=168-10757) |
| A3 · Currículo completo | 390 × 844 | [168:10876](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=168-10876) |
| A3 · Publicado · v1 | 390 × 844 | [168:11002](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=168-11002) |
| A8 · Confirmar descarte | 390 × 844 | [168:11134](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=168-11134) |
| A10 · Versão vigente | 390 × 844 | [168:11304](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=168-11304) |

**Desktop · Dark**

| Tela / estado | Dimensão | Link e `node-id` |
|---|---|---|
| A1 · Cursos da escola | 1440 × 900 | [161:22817](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=161-22817) |
| A3 · Alterações não publicadas | 1440 × 900 | [161:22932](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=161-22932) |
| A6 · Escolher vídeo pronto | 1440 × 900 | [161:23145](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=161-23145) |
| A7 · Publicar nova versão | 1440 × 900 | [161:23372](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=161-23372) |

### 5.3 Composições propostas e reuso

As cinco composições são propostas visuais de Autoria; sua criação como `block` em código
continua sujeita à regra de três usos de `Components.md`. Os frames usam instâncias dos
componentes existentes e bindings das coleções **Theme**, **Spacing** e **Radius**.

| Composição | Variações | Link e `node-id` |
|---|---|---|
| CourseStatusBadge | Rascunho / Publicado / Alterações não publicadas | [157:37](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=157-37) |
| ModuleAccordionHeader | Desktop / mobile × edição / leitura | [157:89](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=157-89) |
| LessonItem | Desktop / mobile × sem vídeo / vídeo pronto × edição / leitura | [157:178](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=157-178) |
| PublicationPendencyList | Pendências de módulo e aula com links individuais | [157:179](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=157-179) |
| VideoOption | Selecionado / não selecionado; largura responsiva | [157:209](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=157-209) |

A [seção de composições](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=157-2) também inclui os ícones vetoriais
[book-open](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=157-3), [grip-vertical](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=157-7)
e [chevron-up](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=157-15), a partir dos SVGs de Lucide.
O AppShell mantém Sidebar 264 e Topbar 68; no mobile a largura é 390 e a navegação compacta usa
o padrão existente. Button, Badge, Tab, FormField, Input, EmptyState, Alert, Skeleton, Toast,
Dialog Header, Radio Option e Area Card são instâncias do acervo aprovado. Textarea reaproveita
a apresentação do campo multilinha, com ajuda e contador próprios de descrição ou nota de versão.
O reflexo de Auditoria reutiliza a lista e o detalhe aprovados, com tipo **Versão publicada**,
origem **Conteúdo e Currículo**, alvo **Curso** e estado **Conforme**.

### 5.4 Verificação e aprovação

- Inventário conferido no Figma: **106 frames de telas/estados**, incluindo **13
  mobile**, **4 Dark**, os dois fluxos, o índice e as cinco composições.
- Navegação conferida pela leitura das reações do Figma: **1586 destinos de navegação**
  registrados e válidos na mesma página; A1–A12 são alcançáveis a partir do Início do professor.
- Estrutura editável: texto, instâncias, frames e vetores; **nenhum preenchimento de imagem**
  ou captura rasterizada da UI. Fontes conferidas: Plus Jakarta Sans, Inter e JetBrains Mono.
- Revisão visual por screenshots dos layouts representativos, incluindo formulário, editor,
  seletor, confirmação mobile, publicação Dark, Auditoria e fluxos; ajustes de reflow, campos
  multilinha, largura mobile e bounds dos painéis realizados.
- Aprovação visual explícita recebida do responsável pelo produto nesta conversa em
  **2026-09-29**: **“Está aprovado”**. O gate estático de aprovação retornou **exit 0**
  após o registro; ele verifica o registro e não substitui a manifestação humana.

A task_01 foi concluída após a aprovação. As tasks 2.0–7.0 não têm mais o bloqueio visual;
suas demais dependências e verificações de implementação permanecem obrigatórias.


---

## 6. Decisões de desenho aprovadas em 2026-09-29

1. **G1/G2:** Autoria e Vídeos no grupo Conteúdo; lista com filtro por estado, sem busca por título.
2. **G4/G5:** editor em `Accordion`, edição por Dialog e salvamento por item, sem “Salvar curso”.
3. **G6:** alça de arrastar com comandos equivalentes no menu, incluindo mover aula de módulo.
4. **G7:** seletor de vídeos prontos em `Sheet`, sem player, com vínculo após confirmação.
5. **G8:** conferência de publicação em Dialog; pendências focáveis e validação final do servidor.
6. **G9:** Histórico como aba no editor e versão imutável em rota própria.
7. **G10:** exclusão só do rascunho nunca publicado; remoção de módulo/aula sempre confirmada.
8. **G13:** card Autoria no Início do professor, sem contagem, conforme a permissão de leitura.
