# Wireframes ASCII — Catálogo do backoffice e vitrine pública (CAP-003, 1º PRD)

> **Status:** ASCII aprovado pelo responsável em 2026-09-30 (aprovação explícita "O wireframe tá aprovado", com as seis propostas da seção 7 mantidas como desenhadas). Figma desenhado em 2026-09-30 e aguardando a aprovação do responsável (task 2.0, seção 8): o cabeçalho passa a `ASCII e Figma aprovados` só depois dela.
> **Figma:** [Índice de revisão](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=220-5226) · frames por tela na [seção 8](#8-handoff-do-figma--aguardando-aprovação) · arquivo Code4Coders — Design System.
> **Handoff:** aprovado o ASCII → desenho no Figma (task 2.0) → aprovação → código de tela (tasks 3.0 em diante). Pedido de ajuste volta a este documento antes de qualquer registro.
> **Objetivo:** definir fluxos, conteúdo e estados das telas do Catálogo (`admin-spa`) e da área pública de cursos (`student-spa`) antes do desenho no Figma.
> **Fontes:** [PRD](../../tasks/prd-vitrine-oferta/prd.md) v1.0 (RF-02…RF-10, Experiência do Usuário, DP-01…DP-05), [TechSpec](../../tasks/prd-vitrine-oferta/techspec.md) v1.0 (Bloco Frontend, V-01…V-10), [contrato do backoffice](../../tasks/prd-vitrine-oferta/api-contract.md) 1.0.0, [contrato da vitrine](../../tasks/prd-vitrine-oferta/api-contract-student.md) 1.0.0, [wireframes de Autoria](wireframes-autoria-curso.md), [wireframes de Auditoria](wireframes-auditoria.md), [wireframes de Acesso interno](wireframes-acesso-interno.md) (AppShell, B12, B13), [wireframes da conta do aluno](wireframes-conta-aluno.md) (layout público), [componentes](Components.md) e [Design System](../../DESIGN.md).

---

## 1. Inventário

### 1.1 O que o PRD entrega em tela

| Entrega | RF | Tela ou efeito visível |
|---|---|---|
| Permissão `oferta.editar` | RF-01 | Item **Catálogo** no menu só para quem a tem; B12 por link direto |
| Ficha sobre curso publicado | RF-02 | C1 lista; C2 ficha (nível e pré-requisito só leitura, chamada comercial) |
| Criar e editar oferta em rascunho | RF-03 | C3 formulário; exclusão de rascunho em C7 |
| Publicar | RF-04 | C4 confirmação com o cartão exato que o visitante verá |
| Alterar oferta publicada | RF-05 | C3 (edição) + C5 confirmação antes/depois |
| Despublicar | RF-06 | C6 confirmação; estado *Despublicada* na ficha |
| Vitrine com filtro por nível | RF-07 | P1 |
| Página pública do curso | RF-08 | P2; P4 para curso indisponível |
| Botão *Comprar*, aviso e contagem | RF-09 | P3 aviso; "N cliques em Comprar" em C2 |
| Rótulos dos atos na trilha | RF-10 | C8 (lista, filtro e detalhe da consulta existente) |

### 1.2 Telas e sobreposições

**Backoffice** (`admin-spa`, base `/admin/`; origem `http://localhost:8081` no Compose e `https://c4c-admin.lab.tasso.dev.br` no Coolify de desenvolvimento)

| # | Tela | Rota | Permissão | Estados principais |
|---|---|---|---|---|
| C1 | Catálogo (lista de cursos) | `/admin/catalogo` | `oferta.editar` | com cursos · vazio · carregando · erro · (B12 sem permissão) |
| C2 | Ficha do curso | `/admin/catalogo/{courseId}` | `oferta.editar` | sem ofertas · com ofertas · sem nível (aviso) · na vitrine · fora da vitrine · carregando · não encontrada · erro |
| C3 | Oferta (criar/editar) | Dialog em C2 | `oferta.editar` | criando · editando rascunho · editando publicada · campo inválido · salvando · erro |
| C4 | Confirmar publicação | Dialog em C2 | `oferta.editar` | confirmar · publicando · recusada por falta de nível · erro |
| C5 | Confirmar alteração de preço/vigência | Dialog em C2 | `oferta.editar` | antes/depois · salvando · erro |
| C6 | Confirmar despublicação | AlertDialog em C2 | `oferta.editar` | confirmar · despublicando · erro |
| C7 | Confirmar exclusão de rascunho | AlertDialog em C2 | `oferta.editar` | confirmar · excluindo · erro |
| C8 | Trilha de auditoria — atos de oferta | telas existentes `/admin/auditoria` e `/admin/auditoria/{recordId}` | papel `administrador` | rótulo resolvido · rótulo não disponível · `Oferta alterada` com valores |
| B12/B13/B1 | Sem permissão · erro geral · sessão encerrada | rotas protegidas | — | reusam os frames de Acesso interno |

**Área pública** (`student-spa`, base `/student/`; origem `http://localhost:8082` no Compose e `https://c4c-student.lab.tasso.dev.br` no Coolify de desenvolvimento). Sem sessão, sem carregador de login.

| # | Tela | Rota | Estados principais |
|---|---|---|---|
| P1 | Vitrine | `/student/cursos` e `/student/cursos?nivel=iniciante\|intermediario\|avancado` | com cursos · filtrada · filtro sem resultado · vitrine sem nenhum curso · carregando · erro |
| P2 | Página do curso | `/student/cursos/{courseId}` | uma oferta · várias ofertas · recomendado na vitrine · recomendado fora da vitrine · sem pré-requisito · carregando · erro |
| P3 | Aviso "compra em breve" | Dialog sobre P2 | aberto · fechado (foco volta ao botão) |
| P4 | Curso não disponível | `/student/cursos/{courseId}` quando o servidor devolve 404 | um estado para fora da vitrine, inexistente e de outra escola |

### 1.3 Decisões de desenho propostas (para aprovação)

| # | Ponto | Proposta e fundamento |
|---|---|---|
| G1 | Lugar no menu do backoffice | Item **Catálogo** num grupo novo **Comercial**, acima de **Financeiro** (área reservada de CAP-002, que continua onde está). O grupo só aparece com área autorizada; **Catálogo** exige `oferta.editar`. O financeiro tem `financeiro.ler` e `oferta.editar`, então vê os dois |
| G2 | Lista do Catálogo | Tabela ordenada por título (contrato), 10 por página. Nível em texto (ou **"Sem nível"** com ícone de aviso, nunca só cor); coluna **Na vitrine** (*Sim* com ✓ / *Não* com –); ofertas por estado em texto ("2 publicadas · 1 rascunho") |
| G3 | Ficha | Cabeçalho com título, nível e pré-requisito **somente leitura**, e a frase "O professor altera nível e pré-requisito no curso." com link para a Autoria **só se** o usuário tiver `autoria.ler` (papéis se acumulam). Chamada comercial num `Card` próprio, acima das ofertas |
| G4 | Aviso de curso sem nível | `Alert warning` **permanente** no topo da ficha enquanto o nível for nulo: "Nenhuma oferta pode ser publicada até o professor declarar o nível do curso." *Nova oferta* e a edição de rascunho continuam habilitadas (RF-02). Curso com ofertas publicadas e sem nível (DP-04) acrescenta: "O curso saiu da vitrine; as ofertas continuam publicadas." |
| G5 | Ações por estado da oferta | *Rascunho*: Editar · Publicar · Excluir. *Publicada*: Editar · Despublicar. *Despublicada*: Editar · Publicar de novo. **Nunca** há Excluir em publicada ou despublicada (RN-O11). Ações num `DropdownMenu` (⋯) com o botão principal visível do estado |
| G6 | Estado da oferta | `Badge` com ícone e texto: **Rascunho** (secondary), **Publicada** (success, ✓), **Despublicada** (outline, –). Cor nunca é o único sinal |
| G7 | Publicar sem nível | O botão fica **habilitado**; a recusa do servidor (`COURSE_LEVEL_REQUIRED`) aparece como `Alert destructive` dentro do diálogo, com o motivo, e a oferta continua em rascunho. O aviso permanente de G4 já antecipa a causa |
| G8 | Confirmação de publicar | O diálogo mostra o **cartão exato** da página pública (nome da opção, preço, vigência em linguagem clara) e o texto "Ao publicar, esta opção aparece na página pública do curso." O cartão é o mesmo bloco `OfferOption` da página P2 |
| G9 | Confirmação de alterar preço ou vigência | Só abre se preço ou vigência mudaram. Mostra **Antes** e **Depois** lado a lado e a frase fixa "Vale para compras futuras e não altera quem já comprou." Mudança só de nome salva direto, com toast, sem esse diálogo |
| G10 | Preço | Digitado em reais com vírgula ("497,00"), prefixo `R$` fora do campo; aceita até duas casas; erro diz o que fazer: "Informe um valor entre R$ 0,01 e R$ 99.999,99." |
| G11 | Vigência | `RadioGroup` com duas opções — **Por período** e **Vitalícia**. "Por período" revela o campo **Meses** (inteiro de 1 a 60). Erro: "Informe de 1 a 60 meses inteiros." |
| G12 | Contagem de cliques | Em cada oferta, texto "14 cliques em Comprar" (0 → "Nenhum clique em Comprar ainda"). É total desde a publicação, sem detalhe por dia |
| G13 | Orientação de RN-O06 | Abaixo da chamada comercial: "Descreva o que a pessoa vai aprender. Não prometa exclusividade de conteúdo nem proteção contra cópia." |
| G14 | Vitrine pública | Grade de **cartões** (`CourseCard` adaptado ao que o servidor manda: sem foto, instrutor, tecnologia nem nota). Miniatura de fallback `bg-inverse` com glifo, `Badge` de nível, título, resumo, preço. 12 por página |
| G15 | Filtro por nível | `RadioGroup` horizontal (Todos · Iniciante · Intermediário · Avançado) com `legend` "Nível" e estado anunciado; mudar de opção troca o `?nivel=` no endereço e mantém o foco na opção |
| G16 | Preço na vitrine | Um cartão com uma oferta: "R$ 297,00". Com mais de uma: "a partir de R$ 397,00" e "2 opções de acesso" |
| G17 | Página do curso | Uma coluna de leitura + bloco **Opções de acesso** no fim e, no desktop, um painel lateral fixo com as mesmas opções. Títulos de seção navegáveis: H1 título, H2 *Sobre o curso*, *Recomendamos saber antes*, *O que você vai ver*, *Opções de acesso* |
| G18 | Pré-requisito | Sempre como recomendação, sob o título "Recomendamos saber antes" (nunca "requisito"). Recomendado na vitrine: link para a página dele. Fora da vitrine: só o título, sem link e sem explicação |
| G19 | Vigência em texto | "Acesso por 12 meses, contados a partir da liberação" · "Acesso por 1 mês, contado a partir da liberação" · "Acesso vitalício". Sempre em texto, junto ao preço; nunca só em destaque visual |
| G20 | Comprar | Botão principal por opção de acesso. O clique **sempre** abre P3, mesmo que a contagem falhe; P3 não pede dado nenhum |
| G21 | Curso indisponível | Um estado só (P4) para curso fora da vitrine, inexistente ou de outra escola: "Este curso não está disponível." com `[ Ver todos os cursos ]` |
| G22 | Layout público | Cabeçalho com marca (link para a vitrine), **Cursos**, **Entrar** e **Criar conta**; rodapé mínimo. Não lê a sessão: aluno autenticado vê a mesma vitrine e o mesmo cabeçalho |
| G23 | Início do backoffice | Sem card novo no Início nesta entrega; a entrada é o menu |
| G24 | Trilha (C8) | Os três tipos entram no filtro e nos rótulos; alvo aparece como "curso — opção"; `Oferta alterada` mostra valores anteriores e novos formatados; motivo mostra "Não se aplica a este tipo" |

---

## 2. Fluxos do usuário

### 2.1 Financeiro: montar, publicar e manter uma oferta

```text
  Menu Comercial → Catálogo
          │ oferta.editar
          ▼
  ┌────────────────────────┐  abrir curso  ┌─────────────────────────────────────┐
  │ C1 Cursos do catálogo  │──────────────▶│ C2 Ficha do curso                   │
  │ título · nível ·       │               │ leitura: título · nível · pré-req.  │
  │ na vitrine · ofertas   │               │ chamada comercial (≤160) · ofertas  │
  └────────────────────────┘               └──┬────────┬───────────┬─────────────┘
                                              │        │           │
                                [ Nova oferta ]        │           │ ⋯ por estado
                                              ▼        │           ▼
                                   ┌───────────────┐   │    Rascunho:  Editar (C3) · Publicar (C4) · Excluir (C7)
                                   │ C3 Oferta     │   │    Publicada: Editar (C3→C5) · Despublicar (C6)
                                   │ nome·preço·   │   │    Despub.:   Editar (C3) · Publicar de novo (C4)
                                   │ vigência      │   │
                                   └──────┬────────┘   └── chamada comercial: salva na própria ficha
                                          │ salvo → C2 (oferta em Rascunho)

  Publicar (C4) ─ confirma com o cartão exato ─▶ Publicada        ─ sem nível: Alert de recusa, continua Rascunho
  Alterar oferta publicada ─ preço ou vigência mudou? ─ sim ▶ C5 antes/depois ▶ salvo
                                                     └ só nome ▶ salvo direto (toast)
  Despublicar (C6) ─▶ Despublicada · o curso sai da vitrine se era a última publicada

  Sem oferta.editar por link direto → B12 · sessão revogada → B1 · erro 502/504 → Alert com [ Tentar de novo ]
```

### 2.2 Administrador: atos de oferta na trilha

```text
  /admin/auditoria ─ filtro Tipo ─ Oferta publicada / Oferta alterada / Oferta despublicada
        │ linha (Alvo: "curso — opção")
        ▼
  /admin/auditoria/{recordId} ─ Autor · Alvo · (Oferta alterada) preço/vigência anterior → novo · Motivo: não se aplica
```

### 2.3 Visitante: descobrir, avaliar e manifestar interesse

```text
  P1 Vitrine (/student/cursos[?nivel=])
   │  filtro por nível (muda o endereço)            │ nenhum curso no filtro → P1.d "Ver todos"
   │  clique no cartão                               ▼
   ▼                                          (oferece voltar para Todos)
  P2 Página do curso (/student/cursos/{courseId})
   │  recomendado na vitrine → link → outra P2     curso fora da vitrine / inexistente → P4
   │  [ Comprar ] numa opção de acesso
   ▼
  P3 Aviso "compra em breve" (Dialog; nada é pedido; a contagem é silenciosa)
   └─ [ Entendi ] ou Esc → foco volta ao botão Comprar que abriu o aviso
```

---

## 3. Layout base e componentes

### 3.1 AppShell do backoffice

Reusa o AppShell aprovado em CAP-002 (`Sidebar` 264, `Topbar` 68, conteúdo `bg-muted` com `p-8`). Em mobile a sidebar vira `Sheet` aberto pelo botão ☰.

```text
┌──────────────────────┬──────────────────────────────────────────────────────────────────────┐
│ [</>] Code4Coders    │                                     [◐ tema] ( LM ) Laura Mendes ▾  │
│       Backoffice     ├──────────────────────────────────────────────────────────────────────┤
│ ▣ Início             │                                                                      │
│ COMERCIAL            │   << C1 ou C2 >>                                                     │
│ 🏷 Catálogo  ◀ ativo │                                                                      │
│ ▤ Financeiro         │                                                                      │
└──────────────────────┴──────────────────────────────────────────────────────────────────────┘
```

### 3.2 Layout público (`student-spa`)

```text
┌──────────────────────────────────────────────────────────────────────────────────────────┐
│ [</>] Code4Coders        Cursos                       [◐ tema]  _Entrar_  [ Criar conta ] │
├──────────────────────────────────────────────────────────────────────────────────────────┤
│                                                                                          │
│   << P1, P2 ou P4 >>                                          (largura de leitura 1120)  │
│                                                                                          │
├──────────────────────────────────────────────────────────────────────────────────────────┤
│ © Code4Coders · cursos de programação                                                    │
└──────────────────────────────────────────────────────────────────────────────────────────┘
Mobile 390: marca + ☰ (Sheet com Cursos, Entrar, Criar conta). Sem sidebar.
```

### 3.3 Componentes

Reuso: `Sidebar`, `Breadcrumb`, `Card`, `Badge`, `Table`, `Pagination`, `Dialog`, `AlertDialog`, `Sheet`, `Form`/`FormField`/`Label`/`Input`/`Textarea`, `RadioGroup`, `Button`, `Alert`, `Skeleton`, `DropdownMenu`, `EmptyState`, `CodeWindow`, `Sonner`, `Tooltip`. Ícones `lucide-react` com nome acessível nos botões.

**Composições propostas para o Figma:**
- `OfferStatusBadge` = `Badge` + ícone + texto (Rascunho · Publicada · Despublicada).
- `OfferRow` = linha da oferta na ficha: nome, vigência, preço, estado, cliques, ações.
- `OfferOption` = opção de acesso exibida ao visitante (nome, vigência em texto, preço, [ Comprar ]); reaproveitada, sem o botão, no cartão de confirmação de C4.
- `LevelBadge` = `Badge secondary` com o nível em texto (Iniciante · Intermediário · Avançado).
- `ShowcaseCard` = `CourseCard` adaptado (sem instrutor, tecnologia e nota) com preço "a partir de".
- `BeforeAfter` = duas colunas **Antes**/**Depois** com preço e vigência, para C5.

**Legenda dos desenhos:** `[ Ação ]` botão · `_link_` navegação · `( Estado )` badge · `⋯` menu com rótulo acessível · `(!)` aviso · `✓` confirmação · `( • )` opção de rádio marcada · `( )` opção não marcada · `[____]` campo. Textos e valores são ilustrativos; identificadores técnicos nunca aparecem como navegação normal.

---

## 4. Wireframes — backoffice

### C1 · Catálogo — `/admin/catalogo`

```text
C1.a  Lista (desktop 1440)
┌──────────────────────┬──────────────────────────────────────────────────────────────────────┐
│ ▣ Início             │  CATÁLOGO                                                            │
│ COMERCIAL            │  Cursos publicados da escola                                         │
│ 🏷 Catálogo ◀        │  Escolha um curso para montar as ofertas de venda.                   │
│ ▤ Financeiro         │                                                                      │
│                      │  ┌────────────────────────────────────────────────────────────────┐  │
│                      │  │ Curso                Nível         Na vitrine   Ofertas         │  │
│                      │  ├────────────────────────────────────────────────────────────────┤  │
│                      │  │ .NET do zero à API   Avançado      ✓ Sim        2 publicadas ·  │  │
│                      │  │                                                 1 despublicada  │  │
│                      │  │                                                 [ Abrir → ]     │  │
│                      │  │ Fundamentos de C#    (!) Sem nível – Não        1 rascunho      │  │
│                      │  │                                                 [ Abrir → ]     │  │
│                      │  │ Testes na prática    Intermediário – Não        Sem ofertas     │  │
│                      │  │                                                 [ Abrir → ]     │  │
│                      │  └────────────────────────────────────────────────────────────────┘  │
│                      │                                             ‹ 1 2 › · 10 por página    │
└──────────────────────┴──────────────────────────────────────────────────────────────────────┘
Ordem: por título. Linha inteira abre C2 (clique ou Enter). Curso nunca publicado não aparece.
"(!) Sem nível" é texto + ícone; nenhuma oferta desse curso pode ser publicada até o nível existir.
```

```text
C1.b  Escola sem curso publicado                  C1.c  Carregando e erro
┌─ EmptyState ─────────────────────────────┐    ┌──────────────────────────────────────────┐
│ ┌─ CodeWindow ──────────── ● ● ● ─────┐  │    │ Carregando: Skeleton de 5 linhas.        │
│ │ // nenhum curso publicado ainda     │  │    │                                          │
│ └──────────────────────────────────────┘  │    │ ( ! ) Não foi possível carregar o        │
│ Nenhum curso publicado ainda.             │    │       catálogo agora.  [ Tentar de novo ]│
│ Só cursos publicados pelo professor       │    │                       (Alert destructive)│
│ aparecem aqui para receber ofertas.       │    └──────────────────────────────────────────┘
└───────────────────────────────────────────┘
C1.d  Sem oferta.editar (link direto): B12 "Você não tem acesso a esta área." — o item do menu nem aparece.
```

```text
C1.e  Mobile 390
┌──────────────────────────────────┐
│ ☰  Catálogo                      │
│ ┌──────────────────────────────┐ │
│ │ .NET do zero à API           │ │
│ │ Avançado · ✓ Na vitrine      │ │
│ │ 2 publicadas · 1 despublicada│ │
│ │                            › │ │
│ └──────────────────────────────┘ │
│ ┌──────────────────────────────┐ │
│ │ Fundamentos de C#            │ │
│ │ (!) Sem nível · – Fora da    │ │
│ │ vitrine · 1 rascunho       › │ │
│ └──────────────────────────────┘ │
│            ‹ 1 2 ›               │
└──────────────────────────────────┘
```

### C2 · Ficha do curso — `/admin/catalogo/{courseId}`

```text
C2.a  Com ofertas, curso na vitrine (desktop 1440)
┌──────────────────────┬──────────────────────────────────────────────────────────────────────┐
│ ▣ Início             │  _← Catálogo_                                                        │
│ COMERCIAL            │  FICHA DO CURSO                                                      │
│ 🏷 Catálogo ◀        │  .NET do zero à API                      (✓ Na vitrine)        (H2)  │
│ ▤ Financeiro         │                                                                      │
│                      │  ┌─ Card · vem do curso (somente leitura) ──────────────────────┐    │
│                      │  │ Nível             (Avançado)                                   │    │
│                      │  │ Pré-requisito     Git e C# básico.                             │    │
│                      │  │ Recomendados      Fundamentos de C#                            │    │
│                      │  │ O professor altera nível e pré-requisito no curso.  _Abrir na  │    │
│                      │  │ Autoria_  ← só aparece com autoria.ler                          │    │
│                      │  └──────────────────────────────────────────────────────────────┘    │
│                      │                                                                      │
│                      │  ┌─ Card · Chamada comercial ───────────────────────────────────┐    │
│                      │  │ [ Da primeira linha de C# a uma API no ar.                ]   │    │
│                      │  │ Descreva o que a pessoa vai aprender. Não prometa exclusividade│    │
│                      │  │ de conteúdo nem proteção contra cópia.            46/160      │    │
│                      │  │                                        [ Salvar chamada ]      │    │
│                      │  └──────────────────────────────────────────────────────────────┘    │
│                      │                                                                      │
│                      │  OFERTAS                                        [ + Nova oferta ]    │
│                      │  ┌──────────────────────────────────────────────────────────────┐    │
│                      │  │ Acesso por 12 meses            R$ 397,00      (✓ Publicada)   │    │
│                      │  │ 12 meses · 14 cliques em Comprar        [ Despublicar ]  ⋯    │    │
│                      │  │──────────────────────────────────────────────────────────────│    │
│                      │  │ Acesso vitalício               R$ 897,00      (✓ Publicada)   │    │
│                      │  │ Vitalícia · 5 cliques em Comprar        [ Despublicar ]  ⋯    │    │
│                      │  │──────────────────────────────────────────────────────────────│    │
│                      │  │ Acesso por 6 meses             R$ 247,00      ( – Despublicada)│    │
│                      │  │ 6 meses · 3 cliques em Comprar           [ Publicar de novo ] ⋯│    │
│                      │  └──────────────────────────────────────────────────────────────┘    │
└──────────────────────┴──────────────────────────────────────────────────────────────────────┘
Ordem das ofertas: rascunhos primeiro, depois publicadas e despublicadas (contrato). ⋯ = Editar.
Publicada: ⋯ Editar. Despublicada: ⋯ Editar. Rascunho: [ Publicar ] + ⋯ Editar · Excluir.
Sem Excluir em publicada ou despublicada.
```

```text
C2.b  Curso sem nível, só rascunho (aviso permanente, criação habilitada)
│  .NET do zero à API                                          ( – Fora da vitrine )       │
│  ( ! ) Nenhuma oferta pode ser publicada até o professor declarar o nível do curso.      │
│        O nível é declarado no curso e vale depois que o professor publicar.  (Alert warn) │
│  ┌─ Card · vem do curso ────────────────────────────────────────────────────────────┐   │
│  │ Nível            (!) Sem nível                                                   │   │
│  │ Pré-requisito    Não informado                                                   │   │
│  └──────────────────────────────────────────────────────────────────────────────────┘   │
│  OFERTAS                                                          [ + Nova oferta ]      │
│  │ Acesso por 12 meses          R$ 497,00    (Rascunho)   [ Publicar ]  ⋯              │
│  │ 12 meses · Nenhum clique em Comprar ainda                                             │

C2.c  Curso que perdeu o nível com ofertas publicadas (DP-04)
│  ( ! ) O curso saiu da vitrine porque a versão atual não declara nível. As ofertas       │
│        publicadas continuam publicadas e voltam à vitrine quando o professor declarar    │
│        o nível e publicar o curso.                                        (Alert warning) │
│  ( – Fora da vitrine )                                                                   │

C2.d  Sem nenhuma oferta
│  OFERTAS                                                          [ + Nova oferta ]      │
│  Nenhuma oferta ainda. Crie a primeira para colocar o curso à venda.                     │

C2.e  Carregando: Skeleton dos três cards. Erro: Alert destructive + [ Tentar de novo ].
C2.f  Curso inexistente, de outra escola ou nunca publicado: "Curso não encontrado no catálogo."  _← Catálogo_
C2.g  Chamada com mais de 160 caracteres:  contador "171/160" em destructive e
      "Use até 160 caracteres: tire 11." — [ Salvar chamada ] desabilitado.
C2.h  Salvar chamada: toast "Chamada salva."; campo vazio salva como "sem chamada".
```

```text
C2.i  Mobile 390
┌──────────────────────────────────┐
│ ☰  Ficha do curso                │
│ .NET do zero à API               │
│ ✓ Na vitrine                     │
│ ┌─ Do curso (leitura) ─────────┐ │
│ │ Avançado · Git e C# básico.  │ │
│ └──────────────────────────────┘ │
│ ┌─ Chamada comercial ──────────┐ │
│ │ [ Da primeira linha…       ] │ │
│ │ …                   46/160   │ │
│ │ [ Salvar chamada ]           │ │
│ └──────────────────────────────┘ │
│ OFERTAS        [ + Nova oferta ] │
│ ┌──────────────────────────────┐ │
│ │ Acesso por 12 meses          │ │
│ │ R$ 397,00 · 12 meses         │ │
│ │ (✓ Publicada)                │ │
│ │ 14 cliques em Comprar        │ │
│ │ [ Despublicar ]           ⋯  │ │
│ └──────────────────────────────┘ │
└──────────────────────────────────┘
```

### C3 · Oferta — Dialog sobre C2 (criar e editar)

```text
C3.a  Nova oferta
┌─ Dialog (máx. 520) ───────────────────────────────────────── ✕ ┐
│ Nova oferta                                                (H4) │
│ A oferta nasce como rascunho e só aparece ao público depois de  │
│ publicada.                                                      │
│                                                                 │
│ Nome da opção            (até 60 caracteres)              0/60  │
│ [ Acesso por 12 meses                                        ]  │
│                                                                 │
│ Preço                                                           │
│ R$ [ 497,00          ]                                          │
│                                                                 │
│ Vigência do acesso                                              │
│ ( • ) Por período      ( ) Vitalícia                (RadioGroup)│
│ Meses  [ 12 ]   de 1 a 60 meses inteiros                        │
│                                                                 │
│                              [ Cancelar ]  [ Salvar rascunho ]  │
└─────────────────────────────────────────────────────────────────┘

C3.b  Vitalícia: o campo Meses some.
│ ( ) Por período      ( • ) Vitalícia                                                    │
│ Acesso vitalício, sem data de término.                                                  │

C3.c  Campos inválidos (mensagem no campo nomeado pelo servidor em FIELD_INVALID)
│ Preço                                                                                   │
│ R$ [ 0,00            ]                                                                  │
│ (!) Informe um valor entre R$ 0,01 e R$ 99.999,99.                                      │
│ Meses  [ 61 ]                                                                           │
│ (!) Informe de 1 a 60 meses inteiros.                                                   │
│ Preço com mais de duas casas ("497,005") → "Use até duas casas decimais."               │
│ Curso com 50 ofertas → Alert "O curso já tem 50 ofertas. Exclua um rascunho primeiro."  │

C3.d  Editando oferta publicada (avisa antes; salvar abre C5 se preço/vigência mudaram)
│ Editar oferta publicada                                                                 │
│ ( i ) Esta oferta está à venda. Mudar preço ou vigência vale só para compras futuras.   │
│ …mesmos campos…                                   [ Cancelar ]  [ Revisar alteração ]   │
│ Mudou só o nome:                                   [ Cancelar ]  [ Salvar ]             │

C3.e  Salvando: botão com spinner e texto "Salvando…", campos desabilitados.
C3.f  Erro de rede/servidor: Alert destructive "Não foi possível salvar agora." + [ Tentar de novo ]
      (repete com a mesma chave de idempotência; campos mantidos).
```

```text
C3.g  Mobile 390: o Dialog vira Sheet inferior com os mesmos campos; botões empilhados
      ([ Salvar rascunho ] em cima, [ Cancelar ] embaixo).
```

### C4 · Confirmar publicação — Dialog sobre C2

```text
C4.a  Conferência (o cartão é o que o visitante verá)
┌─ Dialog (máx. 520) ───────────────────────────────────────── ✕ ┐
│ Publicar oferta                                            (H4) │
│ Confira o que o visitante vai ver na página do curso.           │
│                                                                 │
│ ┌─ OfferOption (sem botão Comprar) ───────────────────────────┐ │
│ │ Acesso por 12 meses                                         │ │
│ │ Acesso por 12 meses, contados a partir da liberação         │ │
│ │ R$ 397,00                                                   │ │
│ └─────────────────────────────────────────────────────────────┘ │
│ Ao publicar, esta opção aparece na página pública do curso.     │
│                                                                 │
│                                 [ Cancelar ]  [ Publicar ]      │
└─────────────────────────────────────────────────────────────────┘

C4.b  Publicando: botão "Publicando…" com spinner.
C4.c  Recusada por falta de nível (COURSE_LEVEL_REQUIRED) — oferta continua em Rascunho
│ ( ! ) O curso não declara nível. O professor precisa declarar o nível e publicar o curso.  │
│                                                              (Alert destructive) [ Fechar ]│
C4.d  Falha de rede: Alert "Não foi possível publicar agora." + [ Tentar de novo ] (mesma chave).
C4.e  Toast de sucesso: "Oferta publicada." O estado na ficha vira (✓ Publicada).
```

### C5 · Confirmar alteração de preço ou vigência — Dialog sobre C2

```text
C5.a  Antes e depois
┌─ Dialog (máx. 560) ───────────────────────────────────────── ✕ ┐
│ Confirmar alteração                                        (H4) │
│ Acesso por 12 meses                                             │
│                                                                 │
│ ┌─ Antes ─────────────────┐  ┌─ Depois ────────────────┐        │
│ │ R$ 497,00               │  │ R$ 397,00               │        │
│ │ 12 meses                │  │ 12 meses                │        │
│ └─────────────────────────┘  └─────────────────────────┘        │
│                                                                 │
│ ( i ) Vale para compras futuras e não altera quem já comprou.   │
│       A mudança fica registrada na trilha com o seu nome.       │
│                                                                 │
│                          [ Voltar e editar ]  [ Confirmar ]     │
└─────────────────────────────────────────────────────────────────┘

C5.b  Vigência mudou (12 meses → vitalícia): só a coluna de vigência difere; o preço repete igual.
│ Antes: R$ 397,00 · 12 meses        Depois: R$ 397,00 · Vitalícia                        │
C5.c  Preço e vigência mudaram juntos: as duas linhas mudam; é um único registro na trilha.
C5.d  Mobile 390: Sheet inferior; Antes e Depois empilhados.
```

### C6 e C7 · Despublicar e excluir rascunho — AlertDialog sobre C2

```text
C6  Despublicar
┌─ AlertDialog ─────────────────────────────────────────────── ┐
│ Despublicar "Acesso por 12 meses"?                      (H4)  │
│ A opção sai da página pública e não aceita compra nova.       │
│ Quem já comprou não é afetado. Você pode publicá-la de novo.  │
│ ( i ) Este é o último curso publicado?  → quando for a única  │
│       oferta publicada: "O curso também sai da vitrine."      │
│                        [ Cancelar ]  [ Despublicar ]          │
└───────────────────────────────────────────────────────────────┘

C7  Excluir rascunho
┌─ AlertDialog ─────────────────────────────────────────────── ┐
│ Excluir o rascunho "Acesso por 6 meses"?                (H4)  │
│ O rascunho será apagado e não poderá ser recuperado.          │
│                        [ Cancelar ]  [ Excluir ]  (destructive)│
└───────────────────────────────────────────────────────────────┘
C6/C7 em erro: Alert destructive dentro do diálogo + [ Tentar de novo ]. Excluir não existe em publicada nem despublicada.
```

### C8 · Trilha de auditoria — atos de oferta (telas existentes de CAP-030)

```text
C8.a  Filtro de tipo ganha três opções
│ Tipo  [▾ Todos                     ]                                                     │
│       Todos · Versão publicada · Papel concedido · Papel revogado · Convite emitido ·    │
│       Convite aceito · Oferta publicada · Oferta alterada · Oferta despublicada          │

C8.b  Linha da lista
│ Momento      Tipo               Autor          Alvo                              Situação │
│ 08/10 11:30  Oferta alterada    Laura Mendes   .NET do zero à API — Acesso…      (✓ Conforme)│
│ 07/10 10:05  Oferta publicada   Laura Mendes   .NET do zero à API — Acesso…      (✓ Conforme)│
│ Alvo sem rótulo (oferta excluída ou desconhecida):  Oferta · 7c8d…9e0f  ⧉                 │

C8.c  Detalhe de "Oferta alterada"
│  Oferta alterada                                                        (✓ Conforme)     │
│  ┌─ Card ORIGINAL · NÃO EDITÁVEL ──────────────────────────────────────────────────────┐ │
│  │ Momento do ato           08/10/2026 11:30:00                                        │ │
│  │ Recebido pela Auditoria  08/10/2026 11:30:03                                        │ │
│  │ Origem                   Catálogo e Oferta                                          │ │
│  │ Tipo                     Oferta alterada                                            │ │
│  │ Autor                    Laura Mendes                                               │ │
│  │ Alvo                     .NET do zero à API — Acesso por 12 meses                   │ │
│  │ Curso                    6f1e…5d6e                                                  │ │
│  │ Preço anterior           R$ 497,00                                                  │ │
│  │ Preço novo               R$ 397,00                                                  │ │
│  │ Motivo                   Não se aplica a este tipo                                  │ │
│  └─────────────────────────────────────────────────────────────────────────────────────┘ │
Vigência mudou: linhas "Vigência anterior  12 meses" e "Vigência nova  Vitalícia".
Preço e vigência juntos: as quatro linhas. "Oferta publicada" e "Oferta despublicada": sem linhas de
valor; só Curso; Motivo "Não se aplica a este tipo". Nenhum tipo de oferta aparece com "Motivo ausente".
```

---

## 5. Wireframes — área pública

### P1 · Vitrine — `/student/cursos`

```text
P1.a  Com cursos (desktop 1440)
┌──────────────────────────────────────────────────────────────────────────────────────────┐
│ [</>] Code4Coders        Cursos                       [◐ tema]  _Entrar_  [ Criar conta ] │
├──────────────────────────────────────────────────────────────────────────────────────────┤
│  CURSOS                                                                                  │
│  Escolha por onde começar                                                          (H1)  │
│  Aprenda programação com aulas em vídeo, do básico ao avançado.                          │
│                                                                                          │
│  Nível   ( • ) Todos   ( ) Iniciante   ( ) Intermediário   ( ) Avançado     (RadioGroup) │
│  3 cursos                                                        (aria-live="polite")     │
│                                                                                          │
│  ┌──────────────────────┐ ┌──────────────────────┐ ┌──────────────────────┐              │
│  │ ┌─ fallback ────────┐│ │ ┌─ fallback ────────┐│ │ ┌─ fallback ────────┐│              │
│  │ │      </>          ││ │ │      </>          ││ │ │      </>          ││              │
│  │ └───────────────────┘│ │ └───────────────────┘│ │ └───────────────────┘│              │
│  │ (Avançado)           │ │ (Iniciante)          │ │ (Intermediário)      │              │
│  │ .NET do zero à API   │ │ Fundamentos de C#    │ │ Testes na prática    │              │
│  │ Da primeira linha de │ │ Sintaxe, tipos e     │ │ Escreva testes que   │              │
│  │ C# a uma API no ar.  │ │ orientação a objetos │ │ protegem o seu       │              │
│  │                      │ │ em C#.               │ │ código.              │              │
│  │ a partir de R$ 397,00│ │ R$ 297,00            │ │ R$ 197,00            │              │
│  │ 2 opções de acesso   │ │                      │ │                      │              │
│  │            [ Ver → ] │ │            [ Ver → ] │ │            [ Ver → ] │              │
│  └──────────────────────┘ └──────────────────────┘ └──────────────────────┘              │
│                                                                ‹ 1 2 › · 12 por página   │
└──────────────────────────────────────────────────────────────────────────────────────────┘
Ordem: do curso colocado na vitrine há menos tempo para o mais antigo. O cartão inteiro é um link
para P2 (botão interno só visual). "a partir de" só quando há mais de uma oferta publicada.
```

```text
P1.b  Filtrada (endereço: /student/cursos?nivel=iniciante)
│  Nível   ( ) Todos   ( • ) Iniciante   ( ) Intermediário   ( ) Avançado                 │
│  1 curso                                                                                 │
│  ┌──────────────────────┐                                                                │
│  │ … cartão Fundamentos │   O endereço pode ser compartilhado; ao abrir, o filtro vem     │
│  └──────────────────────┘   marcado.                                                      │

P1.c  Filtro sem nenhum curso                         P1.d  Vitrine sem nenhum curso
┌────────────────────────────────────────────┐      ┌────────────────────────────────────────┐
│ Nível  ( ) Todos  ( ) Iniciante  ( • ) Avan.│      │ ┌─ CodeWindow ─────────── ● ● ● ──┐     │
│                                            │      │ │ // em breve, cursos por aqui    │     │
│ Nenhum curso neste nível por enquanto.     │      │ └────────────────────────────────┘     │
│ [ Ver todos os cursos ]  ← volta a Todos   │      │ Ainda não há cursos disponíveis.       │
└────────────────────────────────────────────┘      │ Volte em breve.                         │
                                                    └────────────────────────────────────────┘
P1.e  Carregando: Skeleton de 6 cartões com a geometria real; filtro visível.
P1.f  Erro: ( ! ) Não foi possível carregar os cursos agora.  [ Tentar de novo ]  (Alert destructive)
P1.g  Nível desconhecido no endereço (?nivel=xyz): trata como Todos, sem erro na tela.
```

```text
P1.h  Mobile 390
┌──────────────────────────────────┐
│ [</>]                         ☰  │
│ Escolha por onde começar         │
│ Nível                            │
│ ( • ) Todos     ( ) Iniciante    │
│ ( ) Intermediário ( ) Avançado   │
│ 3 cursos                         │
│ ┌──────────────────────────────┐ │
│ │ (Avançado)                   │ │
│ │ .NET do zero à API           │ │
│ │ Da primeira linha de C# a    │ │
│ │ uma API no ar.               │ │
│ │ a partir de R$ 397,00        │ │
│ │ 2 opções de acesso         › │ │
│ └──────────────────────────────┘ │
│            ‹ 1 2 ›               │
└──────────────────────────────────┘
Mobile: uma coluna; sem miniatura (ganho de espaço).
```

### P2 · Página do curso — `/student/cursos/{courseId}`

```text
P2.a  Desktop 1440 — curso com duas opções e um recomendado na vitrine
┌──────────────────────────────────────────────────────────────────────────────────────────┐
│ [</>] Code4Coders        Cursos                       [◐ tema]  _Entrar_  [ Criar conta ] │
├──────────────────────────────────────────────────────────────────────────────────────────┤
│  _← Todos os cursos_                                                                     │
│  (Avançado)                                                                              │
│  .NET do zero à API                                                                (H1)  │
│                                                                                          │
│  ┌─ conteúdo ─────────────────────────────────────┐  ┌─ Opções de acesso (lateral) ───┐  │
│  │ Sobre o curso                            (H2)  │  │ Acesso por 12 meses            │  │
│  │ Do primeiro programa a uma API publicada, com  │  │ Acesso por 12 meses, contados  │  │
│  │ testes e deploy.                               │  │ a partir da liberação          │  │
│  │                                                │  │ R$ 397,00                      │  │
│  │ Recomendamos saber antes                 (H2)  │  │ [ Comprar ]                    │  │
│  │ Git e C# básico.                               │  │ ──────────────────────────────│  │
│  │ Cursos recomendados:                           │  │ Acesso vitalício               │  │
│  │ • _Fundamentos de C#_   ← link (está na vitrine)│  │ Acesso vitalício               │  │
│  │ • Introdução a APIs     ← só o título          │  │ R$ 897,00                      │  │
│  │                                                │  │ [ Comprar ]                    │  │
│  │ O que você vai ver                       (H2)  │  └────────────────────────────────┘  │
│  │ ▸ Fundamentos da linguagem (2 aulas)           │                                      │
│  │     Tipos e variáveis                          │                                      │
│  │     Controle de fluxo                          │                                      │
│  │ ▸ Construindo a API (3 aulas)                  │                                      │
│  │     …                                          │                                      │
│  └────────────────────────────────────────────────┘                                      │
└──────────────────────────────────────────────────────────────────────────────────────────┘
Opções do menor para o maior preço. Estrutura com módulos recolhíveis (Accordion) e só títulos — sem
vídeo, sem duração. O pré-requisito é recomendação: nenhum botão é bloqueado por ele.
"Sobre o curso" vazio (descrição em branco): a seção some.
Sem pré-requisito e sem recomendados: a seção "Recomendamos saber antes" some.
```

```text
P2.b  Mobile 390 (as opções de acesso vêm depois da estrutura; botão fixo no rodapé leva até elas)
┌──────────────────────────────────┐
│ [</>]                         ☰  │
│ _← Todos os cursos_              │
│ (Avançado)                       │
│ .NET do zero à API               │
│ Sobre o curso                    │
│ Do primeiro programa a uma API…  │
│ Recomendamos saber antes         │
│ Git e C# básico.                 │
│ • _Fundamentos de C#_            │
│ • Introdução a APIs              │
│ O que você vai ver               │
│ ▸ Fundamentos da linguagem       │
│ ▸ Construindo a API              │
│ Opções de acesso                 │
│ ┌──────────────────────────────┐ │
│ │ Acesso por 12 meses          │ │
│ │ Acesso por 12 meses, contados│ │
│ │ a partir da liberação        │ │
│ │ R$ 397,00                    │ │
│ │ [ Comprar ]                  │ │
│ └──────────────────────────────┘ │
│ ┌──────────────────────────────┐ │
│ │ Acesso vitalício  R$ 897,00  │ │
│ │ [ Comprar ]                  │ │
│ └──────────────────────────────┘ │
└──────────────────────────────────┘
```

```text
P2.c  Uma oferta só: o bloco lateral mostra uma opção. O nome da opção é texto livre do financeiro;
      a linha de vigência é sempre gerada pela plataforma, mesmo que o nome cite o prazo.
P2.d  Carregando: Skeleton do título, de três blocos de texto e do painel lateral.
P2.e  Erro: ( ! ) Não foi possível carregar este curso agora.  [ Tentar de novo ]   (Alert destructive)
P2.f  Curso republicado pelo professor com aula nova: a estrutura mostra a nova versão ao recarregar,
      sem nenhuma ação do financeiro.
```

### P3 · Aviso "compra em breve" — Dialog sobre P2

```text
┌─ Dialog (máx. 440) ───────────────────────────────────────── ✕ ┐
│ A compra estará disponível em breve                        (H4) │
│ Ainda estamos preparando o pagamento deste curso. Volte em      │
│ breve para concluir a sua compra.                               │
│ Não pedimos nenhum dado seu agora.                              │
│                                                                 │
│                                          [ Entendi ]            │
└─────────────────────────────────────────────────────────────────┘
O Dialog abre a cada clique em [ Comprar ], com foco no botão [ Entendi ] e anunciado por leitor de
tela. Esc ou ✕ fecha; o foco volta ao [ Comprar ] que abriu o aviso. A contagem é silenciosa:
nenhum erro, atraso ou limite de requisições aparece para o visitante. Mobile 390: Sheet inferior.
```

### P4 · Curso não disponível

```text
┌──────────────────────────────────────────────────────────────────────────────────────────┐
│ [</>] Code4Coders        Cursos                       [◐ tema]  _Entrar_  [ Criar conta ] │
├──────────────────────────────────────────────────────────────────────────────────────────┤
│   ┌─ CodeWindow ──────────────────────────── ● ● ● ┐                                     │
│   │ // curso não encontrado                         │                                     │
│   └─────────────────────────────────────────────────┘                                     │
│   Este curso não está disponível.                                                  (H1)  │
│   Ele pode ter saído da vitrine ou o endereço pode estar incorreto.                      │
│   [ Ver todos os cursos ]                                                                │
└──────────────────────────────────────────────────────────────────────────────────────────┘
Mesma tela para curso fora da vitrine, inexistente e de outra escola. Mobile 390: mesma composição em uma coluna.
```

---

## 6. Conteúdo, acessibilidade e regras de texto

**Vocabulário fixo** (DESIGN.md §11: pt-BR direto, botões no infinitivo, erros que dizem o que fazer, números no formato BR):

| Item | Texto |
|---|---|
| Níveis | Iniciante · Intermediário · Avançado; ausente: "Sem nível" |
| Estados da oferta | Rascunho · Publicada · Despublicada |
| Vigência por período | "Acesso por N meses, contados a partir da liberação" (N = 1: "Acesso por 1 mês, contado a partir da liberação") |
| Vigência vitalícia | "Acesso vitalício" |
| Preço | `R$ 397,00`, sempre com centavos, sem parcelamento |
| Pré-requisito | "Recomendamos saber antes" |
| Botões | Nova oferta · Salvar rascunho · Salvar chamada · Publicar · Publicar de novo · Despublicar · Excluir · Revisar alteração · Confirmar · Comprar · Entendi · Ver todos os cursos |

**Textos que a plataforma nunca gera (RN-O06):** nenhuma tela, mensagem ou orientação afirma ou sugere exclusividade de conteúdo ("exclusivo", "só aqui") nem proteção contra cópia ("protegido", "à prova de cópia"). A chamada comercial e o nome da opção são texto livre do financeiro, exibidos como texto, nunca como HTML; a orientação G13 lembra a regra na hora de escrever.

**Acessibilidade:**
- Filtro de nível como `RadioGroup` com `legend` "Nível", estado da opção anunciado e resultado em região `aria-live="polite"` ("3 cursos").
- Preço e vigência em texto, nunca só em destaque visual; estado da oferta com ícone e texto.
- Página do curso navegável por títulos (H1 + quatro H2); estrutura em `Accordion` operável por teclado.
- P3 e os diálogos do backoffice: foco inicial no primeiro controle útil, `Esc` fecha, foco volta ao gatilho.
- `Tabela` do C1 com `caption` oculta; linha inteira é link para C2 (Enter).
- Contraste e alvos de toque seguem DESIGN.md §10 (mínimo AA, alvo de 40 px).

---

## 7. Pontos confirmados na aprovação (2026-09-30)

1. **G1** — grupo **Comercial** no menu, com **Catálogo** acima de **Financeiro** (alternativa: item solto abaixo de Autoria).
2. **G7** — *Publicar* habilitado em curso sem nível, com a recusa explicada dentro do diálogo (alternativa: botão desabilitado com tooltip).
3. **G14** — cartão da vitrine sem foto, instrutor, tecnologia e nota (o servidor não envia esses dados); miniatura de fallback apenas no desktop.
4. **G22** — layout público **sem** ler a sessão: mostra sempre *Entrar* e *Criar conta*, inclusive para aluno autenticado (alternativa: leitura opcional da sessão para mostrar "Minha conta", sem redirecionar).
5. **G23** — nenhum card novo no Início do backoffice.
6. Endereço do filtro em português (`?nivel=iniciante`), traduzido para o enum do contrato no cliente.

---

## 8. Handoff do Figma — aguardando aprovação

O desenho foi materializado no arquivo **Code4Coders — Design System** em 2026-09-30, nas páginas `Fluxo — Catálogo e vitrine` e `Screens — Catálogo e vitrine`, sobre o ASCII aprovado (seções 1 a 7), sem reabrir fluxo nem textos. A revisão começa pelo [índice de revisão](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=220-5226).

> **Aprovação:** o desenho aguarda a aprovação explícita do responsável (task 2.0). Só depois dela a linha `Status` do cabeçalho passa a `ASCII e Figma aprovados pelo responsável em <AAAA-MM-DD>`. Pedido de ajuste volta ao Figma antes do registro.

### 8.1 Fluxos e navegação

| Artefato | Link e `node-id` |
|---|---|
| Índice de revisão | [220:5226](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=220-5226) |
| Fluxo 1 · Financeiro — montar, publicar e manter uma oferta | [220:5271](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=220-5271) |
| Fluxo 2 · Visitante — descobrir, avaliar e manifestar interesse | [220:5468](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=220-5468) |
| Fluxo 3 · Administrador — atos de oferta na trilha | [220:5597](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=220-5597) |
| Página Fluxo — Catálogo e vitrine | [205:5897](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=205-5897) |
| Página Screens — Catálogo e vitrine | [205:5898](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=205-5898) |

Cada nó dos fluxos tem o botão **Revisar tela →**, que abre o frame correspondente pelo link do arquivo. Os dados são ilustrativos e o desenho não executa escritas, busca nem autorização; foco, teclado, idempotência e regras do servidor seguem as seções 1 a 6.

### 8.2 Inventário de telas e estados

Desktop em 1440 de largura (altura 900, ou a do conteúdo quando ele passa disso); mobile em 390. Light em todas as telas; Dark em C2.a, C4.a, P1.a e P2.a. Os diálogos do backoffice mostram a ficha C2 ao fundo e os da área pública, a página P2.

#### C1 · Catálogo — `/admin/catalogo` — [grupo 207:55](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=207-55)

| Tela / estado | Dimensão | Link e `node-id` |
|---|---|---|
| C1.a · Lista de cursos | 1440 × 900 | [209:214](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=209-214) |
| C1.b · Escola sem curso publicado | 1440 × 900 | [209:338](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=209-338) |
| C1.c · Carregando | 1440 × 900 | [209:401](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=209-401) |
| C1.d · Erro ao carregar | 1440 × 900 | [209:518](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=209-518) |

#### C2 · Ficha do curso — `/admin/catalogo/{courseId}` — [grupo 210:214](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=210-214)

| Tela / estado | Dimensão | Link e `node-id` |
|---|---|---|
| C2.a · Com ofertas, curso na vitrine | 1440 × 1052 | [210:215](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=210-215) |
| C2.b · Sem nível, só rascunho | 1440 × 972 | [210:373](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=210-373) |
| C2.c · Perdeu o nível com ofertas publicadas (DP-04) | 1440 × 1081 | [210:518](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=210-518) |
| C2.d · Sem nenhuma oferta | 1440 × 918 | [210:677](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=210-677) |
| C2.e · Carregando | 1440 × 900 | [210:776](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=210-776) |
| C2.f · Erro ao carregar | 1440 × 900 | [210:842](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=210-842) |
| C2.g · Curso não encontrado | 1440 × 900 | [210:925](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=210-925) |
| C2.h · Chamada acima de 160 caracteres | 1440 × 1072 | [210:986](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=210-986) |

#### C3 · Oferta — criar e editar (Dialog sobre C2) — [grupo 211:727](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=211-727)

| Tela / estado | Dimensão | Link e `node-id` |
|---|---|---|
| C3.a · Nova oferta | 1440 × 900 | [211:780](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=211-780) |
| C3.b · Nova oferta vitalícia | 1440 × 900 | [211:889](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=211-889) |
| C3.c · Campos inválidos | 1440 × 900 | [211:1015](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=211-1015) |
| C3.d · Limite de 50 ofertas | 1440 × 900 | [211:1152](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=211-1152) |
| C3.e · Editando oferta publicada | 1440 × 900 | [211:1294](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=211-1294) |
| C3.f · Editando só o nome | 1440 × 900 | [211:1427](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=211-1427) |
| C3.g · Salvando | 1440 × 900 | [211:1543](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=211-1543) |
| C3.h · Erro ao salvar — campos mantidos | 1440 × 900 | [211:1676](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=211-1676) |

#### C4 · Publicar oferta (Dialog sobre C2) — [grupo 212:1672](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=212-1672)

| Tela / estado | Dimensão | Link e `node-id` |
|---|---|---|
| C4.a · Conferir o cartão | 1440 × 900 | [212:1697](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=212-1697) |
| C4.b · Publicando | 1440 × 900 | [212:1901](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=212-1901) |
| C4.c · Recusada: curso sem nível | 1440 × 900 | [212:2123](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=212-2123) |
| C4.d · Falha de rede | 1440 × 900 | [212:2221](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=212-2221) |
| C4.e · Oferta publicada (toast) | 1440 × 900 | [212:2400](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=212-2400) |

#### C5 · Alterar preço ou vigência (Dialog sobre C2) — [grupo 212:2469](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=212-2469)

| Tela / estado | Dimensão | Link e `node-id` |
|---|---|---|
| C5.a · Preço mudou | 1440 × 900 | [212:2519](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=212-2519) |
| C5.b · Vigência mudou | 1440 × 900 | [212:2625](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=212-2625) |
| C5.c · Preço e vigência mudaram | 1440 × 900 | [212:2731](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=212-2731) |
| C5.d · Salvando | 1440 × 900 | [212:2838](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=212-2838) |
| C5.e · Erro ao salvar | 1440 × 900 | [212:2944](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=212-2944) |

#### C6 e C7 · Despublicar e excluir rascunho (AlertDialog sobre C2) — [grupo 212:3007](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=212-3007)

| Tela / estado | Dimensão | Link e `node-id` |
|---|---|---|
| C6.a · Despublicar | 1440 × 900 | [212:3019](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=212-3019) |
| C6.b · Despublicar a última oferta publicada | 1440 × 900 | [212:3111](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=212-3111) |
| C6.c · Despublicando | 1440 × 900 | [212:3186](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=212-3186) |
| C6.d · Erro ao despublicar | 1440 × 900 | [212:3278](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=212-3278) |
| C7.a · Excluir rascunho | 1440 × 900 | [212:3352](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=212-3352) |
| C7.b · Erro ao excluir | 1440 × 900 | [212:3560](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=212-3560) |

#### C8 · Trilha de auditoria — atos de oferta (telas existentes de CAP-030) — [grupo 213:3451](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=213-3451)

| Tela / estado | Dimensão | Link e `node-id` |
|---|---|---|
| C8.a · Filtro Tipo com os três atos de oferta | 1440 × 960 | [213:3452](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=213-3452) |
| C8.b · Lista com atos de oferta | 1440 × 960 | [213:3523](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=213-3523) |
| C8.c · Detalhe de Oferta alterada (preço) | 1440 × 1100 | [213:3813](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=213-3813) |
| C8.d · Detalhe de Oferta alterada (preço e vigência) | 1440 × 1100 | [213:3923](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=213-3923) |
| C8.e · Detalhe de Oferta publicada | 1440 × 1100 | [213:4041](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=213-4041) |
| C8.f · Detalhe de Oferta despublicada, alvo sem rótulo | 1440 × 1100 | [213:4143](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=213-4143) |

#### Mobile 390 — Backoffice — [grupo 214:4053](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=214-4053)

| Tela / estado | Dimensão | Link e `node-id` |
|---|---|---|
| C1.e · Lista de cursos | 390 × 844 | [214:4054](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=214-4054) |
| C2.i · Ficha com ofertas | 390 × 1420 | [214:4121](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=214-4121) |
| C3.i · Nova oferta (Sheet) | 390 × 844 | [214:4288](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=214-4288) |
| C4.f · Publicar oferta (Sheet) | 390 × 844 | [214:4381](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=214-4381) |
| C5.f · Antes e depois (Sheet) | 390 × 844 | [214:4498](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=214-4498) |
| C6.e · Despublicar (AlertDialog) | 390 × 844 | [214:4567](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=214-4567) |

#### P1 · Vitrine — `/student/cursos` — [grupo 216:4371](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=216-4371)

| Tela / estado | Dimensão | Link e `node-id` |
|---|---|---|
| P1.a · Vitrine com cursos | 1440 × 900 | [216:4372](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=216-4372) |
| P1.b · Filtrada por Iniciante (`?nivel=iniciante`) | 1440 × 900 | [216:4477](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=216-4477) |
| P1.c · Filtro sem nenhum curso | 1440 × 900 | [216:4549](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=216-4549) |
| P1.d · Vitrine sem nenhum curso | 1440 × 900 | [216:4608](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=216-4608) |
| P1.e · Carregando | 1440 × 1116 | [216:4647](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=216-4647) |
| P1.f · Erro ao carregar | 1440 × 900 | [216:4746](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=216-4746) |

#### P2 · Página do curso — `/student/cursos/{courseId}` — [grupo 217:4617](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=217-4617)

| Tela / estado | Dimensão | Link e `node-id` |
|---|---|---|
| P2.a · Duas opções, um recomendado na vitrine | 1440 × 905 | [217:4618](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=217-4618) |
| P2.b · Uma opção, sem pré-requisito | 1440 × 900 | [217:4700](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=217-4700) |
| P2.c · Carregando | 1440 × 900 | [217:4760](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=217-4760) |
| P2.d · Erro ao carregar | 1440 × 900 | [217:4801](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=217-4801) |

#### P3 e P4 · Aviso de compra em breve e curso indisponível — [grupo 217:4850](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=217-4850)

| Tela / estado | Dimensão | Link e `node-id` |
|---|---|---|
| P3 · Aviso "compra em breve" (Dialog sobre P2) | 1440 × 900 | [217:4851](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=217-4851) |
| P4 · Curso não disponível | 1440 × 900 | [217:4918](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=217-4918) |

#### Mobile 390 — Área pública — [grupo 219:4767](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=219-4767)

| Tela / estado | Dimensão | Link e `node-id` |
|---|---|---|
| P1.h · Vitrine | 390 × 952 | [219:4768](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=219-4768) |
| P2.e · Página do curso (mobile do P2.b do ASCII) | 390 × 1132 | [219:4853](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=219-4853) |
| P3.m · Aviso "compra em breve" (Sheet) | 390 × 844 | [219:4928](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=219-4928) |
| P4.m · Curso não disponível | 390 × 844 | [219:5016](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=219-5016) |
| P0.m · Menu (Sheet com Cursos, Entrar e Criar conta) | 390 × 844 | [219:5050](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=219-5050) |

#### Dark mode — validação de tokens — [grupo 219:29699](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=219-29699)

| Tela / estado | Dimensão | Link e `node-id` |
|---|---|---|
| C2.a · Ficha com ofertas · Dark | 1440 × 1052 | [219:29700](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=219-29700) |
| C4.a · Conferir o cartão · Dark | 1440 × 900 | [219:29762](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=219-29762) |
| P1.a · Vitrine com cursos · Dark | 1440 × 900 | [219:29833](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=219-29833) |
| P2.a · Página do curso · Dark | 1440 × 905 | [219:29861](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=219-29861) |

### 8.3 Composições novas e reuso

Componentes do design system existente têm precedência (`Button`, `Badge`, `Avatar`, `Alert`, `Toast`, `Skeleton`, `Code Window`, `Dialog Header`, `Radio Option`, `Sidebar Nav Item`, `Brand Logo`, `Select`, `Audit Row`, `Evidence Field`, `Identity Ref`). As composições abaixo são **propostas** da seção 3.3 e moram na seção `🧩 Composições propostas — Catálogo e vitrine` da página Screens; nenhuma delas substitui componente existente.

| Composição | Link e `node-id` |
|---|---|
| `OfferStatusBadge` (Rascunho · Publicada · Despublicada) | [206:104](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=206-104) |
| `LevelBadge` (Iniciante · Intermediário · Avançado · Sem nível) | [206:121](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=206-121) |
| `OfferOption` (opção de acesso; o botão *Comprar* é opcional) | [206:129](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=206-129) |
| `BeforeAfter` (Antes e Depois, C5) | [206:139](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=206-139) |
| `ShowcaseCard` (cartão da vitrine; miniatura e linha extra opcionais) | [206:155](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=206-155) |
| `OfferRow` (linha da oferta na ficha, por estado) | [206:213](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=206-213) |
| `Icon/tag` (item Catálogo do menu) | [205:5902](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=205-5902) |

### 8.4 Notas de revisão

- **C8** parte dos frames aprovados de Auditoria (lista e registro, CAP-030): mesmo layout, acrescidos os três tipos de oferta, o alvo "curso — opção" e as linhas de valor de *Oferta alterada*. O administrador vê o menu do administrador, sem o grupo **Comercial**.

- **B12 (sem permissão) e B1 (sessão encerrada)** reusam os frames de Acesso interno: [B12.a · Sem permissão](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=90-2842) e [B1.c · Sessão encerrada](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=70-199). Não foram redesenhados.

- **C2.a** mostra o link *Abrir na Autoria* (financeiro que também tem `autoria.ler`); nos demais frames da ficha ele está ausente, como no usuário só com `oferta.editar` (G3).

- **P2.b do ASCII (mobile)** corresponde ao frame **P2.e**; o **P2.b** do Figma é o estado desktop de uma única opção, sem pré-requisito (P2.c do ASCII). O botão fixo *Ver opções de acesso* do mobile fica no rodapé da primeira tela.

- A escala de tipografia da área pública segue `DESIGN.md` (H1 de 36/42 no desktop; H2 de 30/36 no mobile) e o estado do filtro de nível usa o componente `Radio Option` do design system.
