# Wireframes ASCII — Autoria de curso (CAP-005)

> **Status:** ASCII aprovado pelo responsável em 2026-09-29; desenho no Figma pendente.
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

## 5. Handoff para o Figma (ASCII aprovado)

1. **Fluxo — Autoria:** os dois diagramas da seção 2, com A1–A12 como nós e as transições de erro.
2. **Screens — Autoria:** desktop 1440 para A1–A12 e seus estados; mobile 390 para A1, A2, A3,
   A6, A7 e A9. Light em todos; Dark em A1, A3, A6 e A7.
3. **Components (proposta):** `CourseStatusBadge`, `ModuleAccordionHeader`, `LessonItem`,
   `PublicationPendencyList` e `VideoOption`, reaproveitando os tokens e componentes aprovados.

O desenho visual e sua aprovação são etapas posteriores; este documento registra apenas o fluxo e
os wireframes ASCII.

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
