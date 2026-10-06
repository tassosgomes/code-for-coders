# Wireframes ASCII — Compra avulsa, pedido, Meus pedidos e Pedidos no backoffice (CAP-011)

> **Status:** ASCII e Figma aprovados pelo responsável em 2026-10-05 (ASCII aprovado em 2026-10-05 com "Wireframe aprovado", propostas P1 a P10 da seção 1.4 aceitas como desenhadas; Figma aprovado em 2026-10-05 com "Aprovado!", incluindo o texto do comprovante (R1) e a Sidebar com Início, Meus pedidos e Trocar senha; frames na [seção 8](#8-handoff-do-figma--aprovado)). Código de tela liberado a partir da task 4.0.
> **Figma:** [Índice de revisão](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=295-88) · frames por tela na [seção 8](#8-handoff-do-figma--aprovado) · arquivo Code4Coders — Design System.
> **Handoff:** aprovado o ASCII → desenho no Figma (task 2.0) → aprovação → código de tela (tasks 4.0 a 7.0, 9.0 e 10.0). Pedido de ajuste volta a este documento antes de qualquer registro.
> **Objetivo:** definir fluxos, conteúdo e estados da compra (botão *Comprar*, resumo, pedido), de *Meus pedidos* (`student-spa`) e da área financeira com lista e detalhe de pedidos (`admin-spa`) antes do desenho no Figma.
> **Fontes:** [PRD](../../tasks/prd-compra-avulsa/prd.md) v1.0 (RF-01, RF-02, RF-05, RF-06, RF-10, RF-11, Experiência do Usuário, RN-V01…RN-V12, RN-CB01…RN-CB06, DP-01…DP-09), [TechSpec](../../tasks/prd-compra-avulsa/techspec.md) (§ Bloco Frontend, § URLs públicas, § Habilitadores inevitáveis), contratos do recorte [do aluno](../../tasks/prd-compra-avulsa/api-contract.md) e [do backoffice](../../tasks/prd-compra-avulsa/api-contract-admin.md), [wireframes do Catálogo e vitrine](wireframes-catalogo-vitrine.md) (página do curso, P2/P3), [wireframes da Conta do aluno](wireframes-conta-aluno.md) (AppShell do aluno), [wireframes de Progresso](wireframes-progresso.md) (Início "meus cursos"), [wireframes de Acesso interno](wireframes-acesso-interno.md) (B11 Financeiro, B12), [wireframes de Cortesias](wireframes-cortesias.md) (formato, busca de aluno por e-mail), [componentes](Components.md) e [Design System](../../DESIGN.md).

---

## 1. Inventário

### 1.1 O que o PRD entrega em tela

| Entrega | RF | Tela ou efeito visível |
|---|---|---|
| *Comprar* deixa de abrir o aviso "em breve" e leva ao resumo | RF-01 | P2 (mudança), C0 |
| Visitante entra ou cria conta e volta à compra | RF-01 | C0 (fluxo), login/cadastro existentes |
| Resumo: curso, opção, preço, vigência, ida ao ambiente seguro | RF-01 | C1 |
| Aviso de acesso existente | RF-02 | C1.b |
| Pedido pendente existente leva ao pedido | RF-03 | C1.c |
| Opção indisponível, pagamento indisponível | RF-01, RF-04 | C1.d, C1.e |
| Página de retorno em todos os estados | RF-05 | O1…O12 |
| Retomar, continuar ou desistir do pendente | RF-06 | O5…O8 |
| Expirado e Cancelado com nova compra | RF-06, RF-08 | O9, O10 |
| Meus pedidos | RF-10 | M1…M5 |
| Pedidos no backoffice: lista, filtros, detalhe | RF-11 | F1…F8 |

### 1.2 Telas

**`student-spa`** (base `/student/`; rotas do SPA sem o prefixo):

| # | Tela | Rota | Quem | Estados principais |
|---|---|---|---|---|
| P2 | Página do curso (mudança no botão) | `/cursos/:courseId` | visitante ou aluno | botão leva ao resumo |
| C1 | Resumo da compra | `/comprar/:offerId` | aluno | normal · com aviso de acesso existente · pedido pendente existente · opção indisponível · pagamento indisponível · carregando · erro |
| O | Pedido (retorno, retomada, *Ir para o curso*) | `/pedidos/:orderId` (`?resultado=concluido` ou `?resultado=saiu` na volta do gateway) | aluno dono do pedido | Confirmando · demora · Compra confirmada · Liberando seu acesso · aguardando boleto · aguardando PIX · aguardando sem meio · desistir · Expirado · Cancelado · não encontrado · carregando · erro |
| M | Meus pedidos | `/pedidos` | aluno | com pedidos · pendente em destaque · vazio · carregando · erro |
| N | Navegação da conta | Sidebar e menu da conta | aluno | item *Meus pedidos* |

**`admin-spa`** (base `/admin/`; rotas do SPA sem o prefixo):

| # | Tela | Rota | Permissão | Estados principais |
|---|---|---|---|---|
| F1 | Pedidos (lista) | `/financeiro` | `financeiro.ler` | com pedidos · filtrada · vazia · carregando · erro |
| F2 | Filtros | dentro de F1 | `financeiro.ler` | por situação, curso, período, e-mail do aluno · aluno não encontrado |
| F3 | Detalhe do pedido | `/financeiro/pedidos/:orderId` | `financeiro.ler` | pago · pendente · expirado · cancelado · não encontrado · carregando · erro |
| B12 | Sem permissão | rotas protegidas | — | reusa o frame de Acesso interno |

### 1.3 Decisões de desenho propostas (para aprovação)

| # | Ponto | Proposta e fundamento |
|---|---|---|
| G1 | *Comprar* | O botão de cada opção vira link para `/comprar/:offerId` (`Button asChild`). O `Dialog`/`Sheet` P3 "A compra estará disponível em breve" deixa de existir. Visitante e aluno veem o mesmo botão; a diferença acontece no resumo (401 → entrar) |
| G2 | Visitante | O resumo devolve 401; a SPA guarda a compra pendente (`localStorage`, 24 h) e leva ao login com `returnTo=/comprar/:offerId`. No login e no cadastro entra um `Alert info`: "Entre ou crie sua conta para continuar sua compra." (texto novo, sem nome do curso porque a compra pendente guarda só `offerId` e `courseId`) |
| G3 | Resumo | Uma tela só, em `Card` único, sem Stepper: curso, opção, preço, vigência, aviso de acesso existente (quando há), aviso de ida ao ambiente seguro e **um** botão primário *Ir para o pagamento*. Vigência sempre "a partir da liberação" (RN-D04) |
| G4 | Texto da vigência | Meses: "Acesso por N meses, contados a partir da liberação do acesso" · vitalícia: "Acesso vitalício". A linha é gerada pela plataforma, nunca vem do nome da opção |
| G5 | Aviso de acesso existente | `Alert info` permanente acima do botão, nunca bloqueia (DP-08). "Você já tem acesso a este curso até DD/MM/AAAA (cortesia)." · vitalício: "Você já tem acesso vitalício a este curso (cortesia)." · a origem em linguagem do aluno: "cortesia" ou "compra". Sem o aviso quando a decisão de acesso está indisponível ou a concessão venceu |
| G6 | Ida ao pagamento | Aviso fixo acima do botão: "Você será levado ao ambiente seguro de pagamento. Cartão, PIX ou boleto são escolhidos lá." O clique cria o pedido e abre o pagamento; botão desabilitado com spinner "Abrindo o pagamento…" até a resposta (G09, sem pedido duplicado) |
| G7 | O pedido é a única página de situação | Retorno do gateway, retomada, *Meus pedidos* e comprovante por e-mail levam todos a `/pedidos/:orderId`. A tela mostra a situação **real** (RN-CB04): o retorno do aluno nunca confirma nada sozinho |
| G8 | Estado "Confirmando" vs. "aguardando sem meio" | Os dois são `awaiting-payment` sem meio escolhido. Quem volta com `?resultado=concluido` vê **Confirmando pagamento**; sem o parâmetro ou com `?resultado=saiu` vê **Aguardando sem meio** (Continuar pagamento / Desistir). Ver ponto em aberto P3 |
| G9 | Atualização sem recarregar | "Confirmando" e "Liberando seu acesso" consultam o pedido de novo a cada 3 s por até 2 min; depois entra o aviso de demora e a consulta passa a 15 s. A troca de estado é anunciada: `aria-live="polite"` na região do estado. O foco só se move na troca para **Compra confirmada** (vai a *Ir para o curso*, O3); nas demais trocas e no aviso de demora o foco fica onde está |
| G10 | Situação nunca só por cor | Toda situação é `Badge` com **ícone + texto**: Aguardando pagamento (`warning`, relógio), Pago (`success`, check), Expirado (`destructive`, relógio riscado), Cancelado (`secondary`, X). O mesmo vale na lista do backoffice |
| G11 | Desistir | Confirmação em `AlertDialog` (desktop) / `Sheet` inferior (mobile): "Desistir deste pedido?" Cancelar não tem volta; depois o aluno compra de novo pelas condições vigentes (RN-V07, DP-04). Fica visível só em pedido aguardando pagamento |
| G12 | Ver boleto / Ver código PIX | O botão chama `startOrderPayment` e leva o aluno ao endereço das instruções no gateway (o contrato devolve o endereço, não o código). A plataforma não mostra QR Code nem linha digitável próprios e não guarda o endereço (RN-CB01) |
| G13 | Meus pedidos | Entra na Sidebar do aluno (item **Meus pedidos**, ícone `receipt`, entre Início e Trocar senha) e no menu da conta (`DropdownMenu`), como atalho. Lista do mais recente ao mais antigo, 10 por página. Pendente em destaque com `Card` de borda de acento e o prazo |
| G14 | Backoffice | A tela "Área reservada" (B11) sai: `/financeiro` passa a ser a lista de pedidos. O item **Financeiro** do menu continua onde está, sob `financeiro.ler`. Título da página: **Pedidos** (sobretítulo FINANCEIRO) |
| G15 | Filtros do backoffice | Situação, curso, período (de/até, datas inclusivas, DD/MM/AAAA) e e-mail do aluno. O e-mail usa a mesma busca de aluno de Cortesias: nunca vai para a URL; o filtro guarda a conta localizada. Filtros aplicam em **Filtrar**; **Limpar filtros** volta ao padrão |
| G16 | Detalhe só leitura | Nenhum botão de ação sobre pedido, pagamento ou acesso (DP-09). A referência do gateway aparece com botão de copiar, só isso |
| G17 | Valores e datas | Valor em reais com vírgula ("R$ 497,00"); datas DD/MM/AAAA; momentos "DD/MM/AAAA às HH:MM" no fuso da escola. Número do pedido com `#` e 6 dígitos ("#000123") |
| G18 | Vocabulário do gateway | Nenhum termo do gateway em tela (RN-CB05): meios são "Cartão de crédito", "PIX" e "Boleto"; motivos e códigos nunca aparecem. A única exceção é a **referência do pagamento** no detalhe do backoffice, que existe para localizar o pagamento no painel do gateway |

### 1.4 Pontos para conferência do responsável

São lacunas ou escolhas que o PRD e a TechSpec deixam abertas; a proposta de cada uma está desenhada abaixo, e o responsável confirma ou pede ajuste.

| # | Ponto | Proposta desenhada | Alternativa |
|---|---|---|---|
| P1 | **Pedido pendente na mesma opção (RF-03).** O PRD diz que o aluno "é levado ao pedido pendente, com o preço e a vigência dele". O resumo mostra a condição **vigente** da oferta, que pode ser diferente da do pedido | C1.c: o resumo aparece com `Alert info` "Você já tem um pedido aguardando pagamento desta opção" e o botão vira *Ver pedido pendente*; nenhum botão de pagar | Redirecionar direto para `/pedidos/:orderId`, sem passar pelo resumo (evita mostrar preço que não vale para o pedido) |
| P2 | **Depois de desistir (RF-06).** O rótulo *Desistir e pagar de outra forma* sugere seguir direto para uma nova compra | O pedido vira Cancelado e fica na própria página com *Comprar de novo* (que vai ao resumo e mostra as condições vigentes, que podem ter mudado) | Navegar sozinho ao resumo da mesma opção depois de cancelar |
| P3 | **"Confirmando" depende do parâmetro `?resultado=concluido`.** Sem ele, um pedido sem meio escolhido é "aguardando sem meio". Se o aluno paga com cartão e recarrega a página já sem o parâmetro antes de a confirmação chegar, vê *Continuar pagamento / Desistir* por instantes | Mantido como no contrato (O1 e O5). *Continuar pagamento* nesse caso leva à página do gateway, que informa que a sessão já foi paga | Pedir ao contrato um indicador de "pagamento enviado, aguardando confirmação" no pedido |
| P4 | **Expirado/Cancelado: *Comprar de novo* com oferta despublicada.** O pedido guarda a oferta; se ela saiu de venda, o resumo mostra C1.d | *Comprar de novo* sempre leva a `/comprar/:offerId`; se indisponível, o resumo explica | Esconder *Comprar de novo* quando a oferta não está mais à venda (exigiria o dado no pedido) |
| P5 | **Resumo, opção indisponível (404).** O erro não traz o curso, então não há como voltar à página do curso por link | C1.d: *Ver todos os cursos* e *Voltar* (histórico) | Contrato devolver o `courseId` no 404 |
| P6 | **Pedido pago em *Meus pedidos* "leva ao curso" (RF-10).** | Ação da linha *Ir para o curso*, resolvida como no pedido (`listMyCourses` → `continueLessonId`); se o curso ainda não aparece, abre o pedido em "Liberando seu acesso" | A linha só leva ao pedido, e o botão fica lá |
| P7 | **Texto de acesso vitalício no aviso (RF-02).** O PRD dá o texto só para data | "Você já tem acesso vitalício a este curso (cortesia)." | — |
| P8 | **Filtro de curso no backoffice.** O contrato filtra por `courseId`; a fonte da lista de cursos para o filtro não é definida no recorte | Combobox com busca por título, "Todos os cursos" por padrão; a origem dos cursos fica para a task do backoffice (10.0) | Campo de texto de título |
| P9 | **Contagem "N cliques em Comprar" do backoffice do Catálogo (DP-06).** A contagem para de crescer e o total histórico passa a ser rotulado "cliques antes da venda abrir". Essa mudança é de tela do Catálogo, não está no escopo desta task | Não desenhada aqui | Incluir um adendo ao `wireframes-catalogo-vitrine.md` |
| P10 | **Cabeçalho de PIX/boleto: "prazo".** O contrato traz `pendingPayment.expiresAt` (boleto: 3 dias; PIX: 24 h) | "Pague até DD/MM/AAAA às HH:MM" para os dois | — |

---

## 2. Fluxos do usuário

### 2.1 Aluno: compra por cartão

```text
  /cursos/:courseId ── [ Comprar ] na opção ──▶ /comprar/:offerId (C1)
        │ visitante: 401 ──▶ guarda {offerId, courseId, 24h} ──▶ login com returnTo ──▶ volta a C1
        │                    (cadastro: depois de confirmar o e-mail, o login sem returnTo leva a C1)
        │ opção fora de venda ──▶ C1.d (nada criado)
        │ pedido pendente na opção ──▶ C1.c
        ▼
  C1 Resumo: curso · opção · preço · vigência · aviso de acesso existente (quando há)
        │ [ Ir para o pagamento ]  (cria o pedido; mesmo clique repetido = um pedido)
        │ gateway indisponível ──▶ C1.e (Alert + Tentar de novo, mesmo pedido)
        ▼
  Página de pagamento do gateway (fora da plataforma: escolhe cartão, PIX ou boleto)
        │ paga ──▶ /pedidos/:orderId?resultado=concluido
        │ sai  ──▶ /pedidos/:orderId?resultado=saiu
        ▼
  O1 Confirmando pagamento ──(a cada 3 s)──▶ O3 Compra confirmada ──▶ [ Ir para o curso ] ──▶ /aulas/:lessonId
        │ > 2 min ──▶ O2 aviso de demora (continua consultando, a cada 15 s)
        └ pago, concessão ainda não lida ──▶ O4 Liberando seu acesso ──▶ O3
```

### 2.2 Aluno: PIX ou boleto, retomar, desistir

```text
  Gateway: gera PIX ou boleto ──▶ /pedidos/:orderId?resultado=concluido
        ▼
  O5 Aguardando pagamento do boleto / O6 do PIX (prazo, [ Ver boleto ] / [ Ver código PIX ])
        │ compensa ──▶ pedido Pago ──▶ O3 (e-mail de comprovante: fora de tela)
        │ [ Desistir e pagar de outra forma ] ──▶ O8 confirma ──▶ O10 Cancelado ──▶ [ Comprar de novo ]
        │ prazo vence ──▶ O9 Expirado ──▶ [ Comprar de novo ]
        ▼
  Em qualquer momento: Meus pedidos (M1) ──▶ pendente em destaque ──▶ /pedidos/:orderId

  Saiu do gateway sem escolher meio ──▶ O7 Aguardando sem meio: [ Continuar pagamento ] · [ Desistir ]
        │ página vencida ──▶ 422 ──▶ O9 Expirado
```

### 2.3 Financeiro: consultar pedidos

```text
  Menu Financeiro (financeiro.ler) ──▶ F1 Pedidos (mais recentes primeiro)
        │ filtros: situação · curso · período · e-mail (busca de aluno) ──▶ [ Filtrar ]
        │ nenhum resultado ──▶ F1.d · aluno sem conta ──▶ F2.b
        ▼ linha
  F3 Detalhe: situação · aluno · curso · opção · valor · vigência · meio · referência do gateway
              · momentos · concessão (existe ou não)
  Sem financeiro.ler (professor, suporte, administrador sem o papel) ──▶ B12 · sessão revogada ──▶ B1
```

---

## 3. Layout base e componentes

### 3.1 AppShell do aluno (O, M, C1)

Reusa o AppShell aprovado em CAP-001 e mantido em CAP-017: `Sidebar` 264, `Topbar` 68 com tema e menu da conta, conteúdo `bg-muted` com `p-8`. Em mobile a sidebar vira `Sheet` pelo botão ☰. A única mudança é o item novo **Meus pedidos** (seção 4, N1).

```text
┌──────────────────┬──────────────────────────────────────────────────────────────────────┐
│ [</>] Code4Coders│                                         [◐ tema]  ( AS ) Ana Souza ▾  │
│                  ├──────────────────────────────────────────────────────────────────────┤
│ ▣ Início         │ bg-muted                                                             │
│ 🧾 Meus pedidos  │   << C1 · O · M >>                                                   │
│ 🔑 Trocar senha  │                                                                      │
│ Sidebar 264      │                                                                      │
└──────────────────┴──────────────────────────────────────────────────────────────────────┘
```

O resumo e o pedido ficam dentro do AppShell do aluno logado. A página do curso (P2) segue no layout público aprovado em CAP-003.

### 3.2 AppShell do backoffice (F)

Reusa o AppShell de CAP-002 (`Sidebar` 264, `Topbar` 68, conteúdo `bg-muted` com `p-8`), com o grupo FINANCEIRO (Cortesias, Financeiro). Em mobile a sidebar vira `Sheet`.

### 3.3 Componentes

Reuso: `Sidebar`, `Breadcrumb`, `Card`, `Badge`, `Button`, `Alert`, `AlertDialog`, `Sheet`, `Skeleton`, `Table`, `Pagination`, `Select`, `Input`, `Label`, `Popover`/`Calendar` (período), `Combobox`, `Tooltip`, `Sonner`, `EmptyState`, `CodeWindow`, `DropdownMenu`, `StatusTile` (confirmação). Ícones `lucide-react` com nome acessível.

**Composições propostas para o Figma:**
- `PurchaseSummaryCard` = curso, opção, preço, vigência, avisos e botão.
- `OrderStatusBadge` = `Badge` com ícone e texto por situação (G10), usado no pedido, em Meus pedidos e no backoffice.
- `OrderStatePanel` = título + descrição + região `aria-live` + ações, uma variante por estado do pedido (O1…O12).
- `OrderRow` = linha de Meus pedidos (tabela no desktop, card no mobile); variante `pendente` destacada.
- `FinanceOrderRow` = linha da lista do backoffice.
- `FinanceFilterBar` = situação, curso, período, e-mail do aluno e botões.
- `OrderTimeline` = momentos do pedido (criado, pago, expirado, cancelado) no detalhe.

Legenda dos desenhos: `[ Ação ]` botão · `_link_` navegação · `( Estado )` badge · `[____]` campo · `( • )` rádio marcado · `[✓]` checkbox · `( ! )` aviso · `( i )` informação · `▼` select · `⧉` copiar.

---

## 4. Wireframes — Aluno

### P2 · Página do curso — o que muda no botão *Comprar* (`/cursos/:courseId`)

A estrutura, o texto das opções, a ordem (menor para maior preço) e os estados P2.c a P2.f ficam como aprovados em [CAP-003](wireframes-catalogo-vitrine.md). Muda só o comportamento do botão.

```text
P2.a  Desktop 1440 — bloco lateral "Opções de acesso" (recorte)
┌─ Opções de acesso (lateral) ───┐
│ Acesso por 12 meses            │
│ Acesso por 12 meses, contados  │
│ a partir da liberação          │
│ R$ 397,00                      │
│ [ Comprar ]  ──▶ /comprar/{offerId da opção}
│ ────────────────────────────── │
│ Acesso vitalício               │
│ Acesso vitalício               │
│ R$ 897,00                      │
│ [ Comprar ]  ──▶ /comprar/{offerId da opção}
└────────────────────────────────┘

P2.b  Mobile 390 — o mesmo botão em cada card de opção; o botão fixo do rodapé continua levando
      até "Opções de acesso".
```

- O clique **não abre mais** o `Dialog` P3 ("A compra estará disponível em breve") nem soma na contagem anônima de cliques (DP-06). O foco segue para a página do resumo.
- É um link: Ctrl+clique abre em nova aba; teclado e leitor de tela o anunciam como link com o nome "Comprar acesso por 12 meses" (`aria-label` com o nome da opção).
- Visitante e aluno veem o mesmo botão. O visitante é levado ao login pelo resumo (fluxo 2.1).

```text
P2.c  Login e cadastro quando vêm de uma compra (G2) — recorte acima do formulário existente
┌──────────────────────────────────────────────┐
│ ( i ) Entre ou crie sua conta para continuar │
│       sua compra.                  (Alert info)│
│ << formulário de login ou de cadastro existente >> │
└──────────────────────────────────────────────┘
O Alert só aparece quando existe compra pendente guardada. Depois de entrar (ou de confirmar o e-mail
e entrar), o aluno chega direto a C1, da opção que escolheu.
```

### C1 · Resumo da compra — `/comprar/:offerId`

```text
C1.a  Desktop 1440 — resumo normal
┌──────────────────┬───────────────────────────────────────────────────────────────────────┐
│ ▣ Início         │ COMPRA                                                    (overline)  │
│ 🧾 Meus pedidos  │ Resumo da compra                                               (H2)   │
│                  │                                                                       │
│                  │ ┌─ PurchaseSummaryCard (máx. 640) ──────────────────────────────────┐ │
│                  │ │ Curso                                                             │ │
│                  │ │ .NET do zero à API                                          (H3)  │ │
│                  │ │ ───────────────────────────────────────────────────────────────── │ │
│                  │ │ Opção de acesso      Acesso por 12 meses                          │ │
│                  │ │ Vigência             Acesso por 12 meses, contados a partir da    │ │
│                  │ │                      liberação do acesso                          │ │
│                  │ │ Preço                R$ 497,00                                    │ │
│                  │ │ ───────────────────────────────────────────────────────────────── │ │
│                  │ │ ( i ) Você será levado ao ambiente seguro de pagamento. Cartão,   │ │
│                  │ │       PIX ou boleto são escolhidos lá.                            │ │
│                  │ │                                                                   │ │
│                  │ │                                 [ Ir para o pagamento ]           │ │
│                  │ │ _← Voltar ao curso_                                               │ │
│                  │ └───────────────────────────────────────────────────────────────────┘ │
└──────────────────┴───────────────────────────────────────────────────────────────────────┘
Opção vitalícia: a linha Vigência diz "Acesso vitalício".
"Voltar ao curso" leva a /cursos/:courseId. O botão confirma o preço e a vigência que o aluno vê;
o pedido congela exatamente isso (RN-V03).
Enquanto cria o pedido e abre o pagamento: botão desabilitado com spinner "Abrindo o pagamento…".
```

```text
C1.b  Com aviso de acesso existente (RF-02) — informa, não bloqueia
│ ┌─ PurchaseSummaryCard ─────────────────────────────────────────────────────────────────┐ │
│ │ .NET do zero à API                                                              (H3)  │ │
│ │ Opção de acesso      Acesso vitalício                                                 │ │
│ │ Vigência             Acesso vitalício                                                 │ │
│ │ Preço                R$ 897,00                                                        │ │
│ │ ( i ) Você já tem acesso a este curso até 30/11/2026 (cortesia).        (Alert info)  │ │
│ │       Você pode comprar mesmo assim: a nova compra não altera o acesso que você tem.  │ │
│ │ ( i ) Você será levado ao ambiente seguro de pagamento. Cartão, PIX ou boleto são     │ │
│ │       escolhidos lá.                                                                  │ │
│ │                                                  [ Ir para o pagamento ]              │ │
│ └───────────────────────────────────────────────────────────────────────────────────────┘ │
Variantes do texto do aviso:
  vitalício ........ "Você já tem acesso vitalício a este curso (cortesia)."
  origem compra .... "Você já tem acesso a este curso até DD/MM/AAAA (compra)."
Concessão vencida, ou decisão de acesso indisponível: o aviso não aparece e a compra segue (C1.a).
O botão mantém o mesmo peso: o aviso informa, não desestimula nem bloqueia.
```

```text
C1.c  Pedido pendente na mesma opção (RF-03, RN-V06) — ver ponto P1
│ ┌─ PurchaseSummaryCard ─────────────────────────────────────────────────────────────────┐ │
│ │ .NET do zero à API                                                              (H3)  │ │
│ │ ( i ) Você já tem um pedido aguardando pagamento desta opção.           (Alert info)  │ │
│ │       O pedido mantém o preço e a vigência de quando foi criado.                      │ │
│ │                                                                                       │ │
│ │                                                    [ Ver pedido pendente ]            │ │
│ │ _← Voltar ao curso_                                                                   │ │
│ └───────────────────────────────────────────────────────────────────────────────────────┘ │
Neste estado o card não mostra preço nem vigência da oferta atual (poderiam diferir do pedido).
[ Ver pedido pendente ] leva a /pedidos/{pendingOrderId}. Nenhum pedido novo é criado.
Outra opção do mesmo curso, sem pedido pendente, mostra o resumo normal (RN-V06 é por opção).
```

```text
C1.d  Opção indisponível (404 OFFER_NOT_AVAILABLE — despublicada, inexistente ou de outra escola)
│ ┌─ EmptyState ────────────────────────────────────────────────────────────────────┐ │
│ │ Esta opção não está mais disponível                                       (H3)  │ │
│ │ A escola retirou esta opção de compra. Nenhum pedido foi criado e você não      │ │
│ │ foi cobrado.                                                                    │ │
│ │ [ Ver todos os cursos ]  _← Voltar_                                             │ │
│ └─────────────────────────────────────────────────────────────────────────────────┘ │
Mesma tela para a opção que sai de venda entre o clique em Comprar e o clique em Ir para o
pagamento: nenhum pedido nasce.

C1.e  Pagamento indisponível (503 ao abrir a página de pagamento) — o pedido já existe
│ ( ! ) Não foi possível abrir o pagamento agora.                    (Alert destructive)  │
│       Seu pedido foi criado e continua aguardando pagamento. Você pode tentar de novo   │
│       ou voltar mais tarde em Meus pedidos.                                             │
│       [ Tentar de novo ]   _Ver meu pedido_                                             │
O resumo permanece acima do Alert. [ Tentar de novo ] usa o mesmo pedido (não cria outro) e o
botão principal fica no lugar com o foco no Alert.

C1.f  Carregando: Skeleton do título do curso, de três linhas de dados e do botão.
C1.g  Erro (502/504 ao carregar): ( ! ) Não foi possível carregar o resumo agora.  [ Tentar de novo ]
                                                                           (Alert destructive)
```

```text
C1.h  Mobile 390
┌──────────────────────────────────┐
│ [</>] Code4Coders             ☰  │
│ COMPRA                           │
│ Resumo da compra                 │
│ ┌──────────────────────────────┐ │
│ │ .NET do zero à API           │ │
│ │ Opção     Acesso por 12 meses│ │
│ │ Vigência  Acesso por 12      │ │
│ │ meses, contados a partir da  │ │
│ │ liberação do acesso          │ │
│ │ Preço     R$ 497,00          │ │
│ │ ( i ) Você já tem acesso a   │ │
│ │ este curso até 30/11/2026    │ │
│ │ (cortesia).                  │ │
│ │ ( i ) Você será levado ao    │ │
│ │ ambiente seguro de pagamento.│ │
│ │ Cartão, PIX ou boleto são    │ │
│ │ escolhidos lá.               │ │
│ └──────────────────────────────┘ │
│ [ Ir para o pagamento ]          │
│ _← Voltar ao curso_              │
└──────────────────────────────────┘
Botão de largura total. Os Alerts empilham acima do botão.
```

### O · Pedido — `/pedidos/:orderId`

Uma página só, com o número do pedido e o resumo congelado sempre visíveis, e um painel de estado que muda. O resumo congelado é o que o aluno viu ao clicar em *Comprar*, não a oferta atual.

```text
O.0  Estrutura (desktop 1440) — o painel de estado (O1…O12) ocupa a área "<< estado >>"
┌──────────────────┬───────────────────────────────────────────────────────────────────────┐
│ ▣ Início         │ PEDIDO #000123                                            (overline)  │
│ 🧾 Meus pedidos ◀│ .NET do zero à API                                              (H2)  │
│                  │ Acesso por 12 meses · R$ 497,00 · criado em 05/10/2026               │
│                  │                                                                       │
│                  │ ┌─ OrderStatePanel (região aria-live="polite") ─────────────────────┐ │
│                  │ │ << estado: título, texto, ações >>                                │ │
│                  │ └───────────────────────────────────────────────────────────────────┘ │
│                  │ ┌─ Resumo do pedido ────────────────────────────────────────────────┐ │
│                  │ │ Curso       .NET do zero à API                                    │ │
│                  │ │ Opção       Acesso por 12 meses                                   │ │
│                  │ │ Vigência    Acesso por 12 meses, contados a partir da liberação   │ │
│                  │ │ Valor       R$ 497,00                                             │ │
│                  │ │ Meio        Cartão de crédito  (ou "Ainda não escolhido")         │ │
│                  │ │ Situação    ( Aguardando pagamento )                              │ │
│                  │ └───────────────────────────────────────────────────────────────────┘ │
│                  │ _← Meus pedidos_                                                      │
└──────────────────┴───────────────────────────────────────────────────────────────────────┘
Vigência vitalícia: "Acesso vitalício". Situação sempre com ícone + texto (G10).
```

```text
O1  Confirmando pagamento  (awaiting-payment, sem meio, volta com ?resultado=concluido)
│ ┌─ OrderStatePanel ─────────────────────────────────────────────────────────────────┐ │
│ │ (spinner)  Confirmando pagamento                                            (H3)  │ │
│ │ Recebemos a sua volta do pagamento e estamos aguardando a confirmação. Esta       │ │
│ │ página atualiza sozinha: você não precisa recarregar.                             │ │
│ └───────────────────────────────────────────────────────────────────────────────────┘ │
Consulta o pedido a cada 3 s. O spinner tem texto alternativo; o título é o anúncio.
O pagamento só é considerado feito quando a confirmação chega: voltar do gateway não confirma
nada (RN-CB04).

O2  Confirmação demorando (mais de 2 min em O1 ou O4; continua consultando, a cada 15 s)
│ ┌─ OrderStatePanel ─────────────────────────────────────────────────────────────────┐ │
│ │ (spinner)  Confirmando pagamento                                            (H3)  │ │
│ │ ( i ) A confirmação pode demorar. Seu acesso será liberado sozinho assim que o    │ │
│ │       pagamento for confirmado e você receberá o comprovante por e-mail.          │ │
│ │       Você pode sair desta página: acompanhe em _Meus pedidos_.                   │ │
│ └───────────────────────────────────────────────────────────────────────────────────┘ │
O aviso é anunciado uma vez (`aria-live="polite"`) quando aparece. Em "Liberando seu acesso"
(O4) o título é esse e o aviso é o mesmo.

O3  Compra confirmada  (paid com accessGrantedAt; o curso aparece em "meus cursos")
│ ┌─ OrderStatePanel (StatusTile de sucesso) ─────────────────────────────────────────┐ │
│ │ (✓)  Compra confirmada                                                      (H3)  │ │
│ │ Seu acesso a .NET do zero à API está liberado. Acesso por 12 meses, contados a    │ │
│ │ partir da liberação do acesso.                                                    │ │
│ │ Você receberá o comprovante por e-mail.                                           │ │
│ │                                              [ Ir para o curso ]                  │ │
│ └───────────────────────────────────────────────────────────────────────────────────┘ │
Mudança de "Confirmando pagamento" para "Compra confirmada": anunciada a leitor de tela
(`aria-live="polite"`, "Compra confirmada"); o foco vai para [ Ir para o curso ].
Vitalícia: "Seu acesso a … está liberado. Acesso vitalício."
[ Ir para o curso ] leva a /aulas/{continueLessonId} do curso (listMyCourses, de CAP-017).
A linha "Você receberá o comprovante por e-mail." aparece só quando o pedido passou por aqui
na mesma visita; ao abrir o pedido pago depois, o painel mostra só o texto de acesso e o botão.

O4  Liberando seu acesso  (paid sem accessGrantedAt, ou concessão ainda não visível em "meus cursos")
│ ┌─ OrderStatePanel ─────────────────────────────────────────────────────────────────┐ │
│ │ (spinner)  Liberando seu acesso                                             (H3)  │ │
│ │ Seu pagamento foi confirmado. Estamos liberando o curso: isso leva alguns         │ │
│ │ segundos. Esta página atualiza sozinha.                                           │ │
│ └───────────────────────────────────────────────────────────────────────────────────┘ │
Consulta a cada 3 s por 2 min; depois, O2 (aviso de demora) e consulta a cada 15 s. Quando a
concessão aparece, vira O3. [ Ir para o curso ] não aparece antes disso.
```

```text
O5  Aguardando pagamento do boleto  (awaiting-payment com pendingPayment.method = boleto)
│ ┌─ OrderStatePanel ─────────────────────────────────────────────────────────────────┐ │
│ │ ( ⏱ )  Aguardando pagamento do boleto                                       (H3)  │ │
│ │ Pague o boleto até 08/10/2026 às 23:59. O acesso a .NET do zero à API será        │ │
│ │ liberado sozinho quando o pagamento for compensado, o que pode levar até 3 dias   │ │
│ │ úteis. O curso ainda não está liberado.                                           │ │
│ │ [ Ver boleto ]            [ Desistir e pagar de outra forma ]                     │ │
│ └───────────────────────────────────────────────────────────────────────────────────┘ │
O6  Aguardando pagamento do PIX  (pendingPayment.method = pix)
│ ┌─ OrderStatePanel ─────────────────────────────────────────────────────────────────┐ │
│ │ ( ⏱ )  Aguardando pagamento do PIX                                          (H3)  │ │
│ │ Pague o PIX até 06/10/2026 às 14:20. O acesso será liberado sozinho assim que o   │ │
│ │ pagamento for confirmado. O curso ainda não está liberado.                        │ │
│ │ [ Ver código PIX ]        [ Desistir e pagar de outra forma ]                     │ │
│ └───────────────────────────────────────────────────────────────────────────────────┘ │
[ Ver boleto ] e [ Ver código PIX ] levam às instruções no ambiente seguro de pagamento (G12);
o botão mostra spinner e fica desabilitado enquanto o endereço é obtido.
Erro ao obter (503): ( ! ) Não foi possível abrir o pagamento agora.  [ Tentar de novo ]  (Alert destructive)
O texto "até 3 dias úteis" vem da compensação do boleto; se o responsável preferir não prometer
prazo de compensação, a frase sai e fica só "assim que o pagamento for compensado".

O7  Aguardando pagamento sem meio escolhido  (awaiting-payment, sem meio; sem ?resultado=concluido)
│ ┌─ OrderStatePanel ─────────────────────────────────────────────────────────────────┐ │
│ │ ( ⏱ )  Aguardando pagamento                                                 (H3)  │ │
│ │ Você saiu do pagamento antes de escolher como pagar. Seu pedido continua          │ │
│ │ aguardando e vale até 06/10/2026 às 14:20.                                        │ │
│ │ [ Continuar pagamento ]            [ Desistir ]                                   │ │
│ └───────────────────────────────────────────────────────────────────────────────────┘ │
[ Continuar pagamento ] abre a página de pagamento do gateway (mesmo pedido; spinner "Abrindo o
pagamento…"). Gateway indisponível (503): Alert destructive "Não foi possível abrir o pagamento
agora. Seu pedido continua aguardando." + [ Tentar de novo ].
Prazo vencido (422): vai a O9, com o texto "O prazo para pagar venceu."

O8  Confirmar desistência  (AlertDialog; mobile 390: Sheet inferior)
┌─ AlertDialog (máx. 440) ──────────────────────────────────────────────── ✕ ┐
│ Desistir deste pedido?                                                (H4) │
│ O pedido #000124 será cancelado e o pagamento pendente, também. Se você    │
│ quiser comprar de novo, a compra seguirá as condições da opção naquele     │
│ momento, que podem ser diferentes das deste pedido.                        │
│                                                                            │
│                      [ Manter pedido ]        [ Desistir do pedido ]       │
└────────────────────────────────────────────────────────────────────────────┘
O foco abre em [ Manter pedido ] (a ação segura). Esc ou ✕ fecha e devolve o foco ao botão que
abriu. Confirmar mostra spinner "Cancelando…" e, na resposta, toast "Pedido cancelado." e O10.
Erro (502/504): Alert destructive no AlertDialog "Nada mudou. Tente de novo." Pedido que já não
está aguardando (422): o diálogo fecha e a tela mostra a situação atual.
```

```text
O9  Expirado  (expired)
│ ┌─ OrderStatePanel ─────────────────────────────────────────────────────────────────┐ │
│ │ ( ⏱̸ )  Pedido expirado                                                      (H3)  │ │
│ │ O prazo para pagar este pedido venceu em 09/10/2026 e o acesso não foi liberado.  │ │
│ │ Se você ainda quiser o curso, faça uma nova compra.                               │ │
│ │ ( i ) Se você pagou este pedido e o pagamento ainda não foi compensado, o acesso  │ │
│ │       é liberado sozinho quando ele chegar.                                       │ │
│ │                                                  [ Comprar de novo ]              │ │
│ └───────────────────────────────────────────────────────────────────────────────────┘ │
O10  Cancelado  (cancelled)
│ ┌─ OrderStatePanel ─────────────────────────────────────────────────────────────────┐ │
│ │ ( ✕ )  Pedido cancelado                                                     (H3)  │ │
│ │ Você desistiu deste pedido em 06/10/2026. Nenhum acesso foi liberado.             │ │
│ │ Se quiser comprar de novo, a nova compra segue as condições vigentes da opção.    │ │
│ │ ( i ) Se você pagou este pedido e o pagamento ainda não foi compensado, o acesso  │ │
│ │       é liberado sozinho quando ele chegar.                                       │ │
│ │                                                  [ Comprar de novo ]              │ │
│ └───────────────────────────────────────────────────────────────────────────────────┘ │
[ Comprar de novo ] leva a /comprar/{offerId}: mostra o resumo com as condições de agora, ou C1.d
se a opção saiu de venda (pontos P2 e P4). O Alert "se você pagou" cobre o pagamento confirmado
depois (RN-V08); se esse pagamento chegar, o pedido passa a Pago e a página abre em O3 ao recarregar.

O11  Pedido não encontrado  (404, inclusive pedido de outro aluno ou de outra escola)
│ ┌─ EmptyState ────────────────────────────────────────────────────────────────────┐ │
│ │ Pedido não encontrado                                                     (H3)  │ │
│ │ Este pedido não existe ou não é seu. Confira o endereço ou veja a lista dos     │ │
│ │ seus pedidos.                                                                   │ │
│ │ [ Ver meus pedidos ]                                                            │ │
│ └─────────────────────────────────────────────────────────────────────────────────┘ │
A tela é idêntica para pedido inexistente e pedido de outro aluno: a plataforma responde como se
não existisse (RF-05, RF-10).

O12  Carregando e erro
│ Carregando: Skeleton do título, do resumo e do painel de estado.
│ Erro (502/504): ( ! ) Não foi possível carregar o pedido agora.  [ Tentar de novo ] (Alert destructive)
│ Consulta de acompanhamento falhou (rede): o painel atual fica na tela e entra, discreto,
│ "( i ) Não conseguimos atualizar agora. Tentaremos de novo." (Alert info); não troca de estado.
│ Sessão encerrada (401): vai ao login e volta ao pedido (returnTo).
```

```text
O13  Mobile 390 — Compra confirmada (O3), as outras variantes mantêm a mesma estrutura
┌──────────────────────────────────┐
│ [</>] Code4Coders             ☰  │
│ PEDIDO #000123                   │
│ .NET do zero à API               │
│ Acesso por 12 meses · R$ 497,00  │
│ ┌──────────────────────────────┐ │
│ │ (✓) Compra confirmada        │ │
│ │ Seu acesso a .NET do zero à  │ │
│ │ API está liberado. Acesso por│ │
│ │ 12 meses, contados a partir  │ │
│ │ da liberação do acesso.      │ │
│ │ Você receberá o comprovante  │ │
│ │ por e-mail.                  │ │
│ │ [ Ir para o curso ]          │ │
│ └──────────────────────────────┘ │
│ ┌─ Resumo do pedido ───────────┐ │
│ │ Curso      .NET do zero à API│ │
│ │ Opção      Acesso por 12 meses│ │
│ │ Vigência   Acesso por 12     │ │
│ │ meses, contados a partir da  │ │
│ │ liberação do acesso          │ │
│ │ Valor      R$ 497,00         │ │
│ │ Meio       Cartão de crédito │ │
│ │ Situação   ( ✓ Pago )        │ │
│ └──────────────────────────────┘ │
│ _← Meus pedidos_                 │
└──────────────────────────────────┘
Ações de O5…O10 ficam em largura total, empilhadas, com a ação principal primeiro e [ Desistir… ]
como `Button` outline. O8 vira Sheet inferior. O Resumo do pedido repete todas as linhas do desktop
(Curso, Opção, Vigência, Valor, Meio, Situação) em todas as variantes mobile, inclusive as pendentes
(O5, O7) e as de Expirado e Cancelado.
```

### M · Meus pedidos — `/pedidos`

```text
M1  Desktop 1440 — com pedidos (pago, expirado e um boleto aguardando)
┌──────────────────┬───────────────────────────────────────────────────────────────────────┐
│ ▣ Início         │ MINHA CONTA                                               (overline)  │
│ 🧾 Meus pedidos ◀│ Meus pedidos                                                    (H2)  │
│                  │                                                                       │
│                  │ ┌─ OrderRow (pendente, em destaque) ────────────────────────────────┐ │
│                  │ │ #000124 · React na prática · Acesso por 6 meses                   │ │
│                  │ │ R$ 297,00 · 06/10/2026 · Boleto            ( ⏱ Aguardando pagamento) │
│                  │ │ Pague até 09/10/2026 às 23:59                                     │ │
│                  │ │                                        [ Retomar pagamento ]      │ │
│                  │ └───────────────────────────────────────────────────────────────────┘ │
│                  │ ┌─ Table ───────────────────────────────────────────────────────────┐ │
│                  │ │ Pedido  Curso · opção              Valor     Data       Meio      Situação        │
│                  │ │ #000123 .NET do zero à API         R$ 497,00 05/10/2026 Cartão de ( ✓ Pago )      │
│                  │ │         Acesso por 12 meses                              crédito   [ Ir para o curso ] │
│                  │ │ #000118 Testes na prática          R$ 197,00 20/09/2026 —         ( ⏱̸ Expirado )   │
│                  │ │         Acesso vitalício                                 Ainda não   _Ver pedido_  │
│                  │ │                                                          escolhido           │
│                  │ │ #000101 Fundamentos de C#          R$ 147,00 02/09/2026 PIX        ( ✕ Cancelado ) │
│                  │ │         Acesso por 3 meses                                         _Ver pedido_  │
│                  │ └───────────────────────────────────────────────────────────────────┘ │
│                  │                                                      ‹ 1 2 ›          │
└──────────────────┴───────────────────────────────────────────────────────────────────────┘
Do mais recente ao mais antigo, 10 por página. O número do pedido é link para /pedidos/:orderId em
todas as linhas.
O pedido pendente aparece em destaque no topo da página (`Card` com borda de acento), só ali:
a tabela traz os demais. Vários pendentes (opções diferentes): um Card de destaque por pedido,
do mais recente ao mais antigo. Na segunda página, só a tabela.
Pago: [ Ir para o curso ] (ponto P6). Pedido pago mostra o curso, a opção, o valor e a vigência
congelados, mesmo que a oferta tenha sido despublicada depois (RF-10).
Meio "—" quando ainda não foi escolhido, com o texto "Ainda não escolhido".
Situação sempre com ícone + texto (G10).

M2  Vazio
│ ┌─ EmptyState ────────────────────────────────────────────────────────────────────┐ │
│ │ Você ainda não tem compras                                                (H3)  │ │
│ │ Quando você comprar um curso, o pedido aparece aqui, com a situação e o         │ │
│ │ comprovante de pagamento por e-mail.                                            │ │
│ │ [ Ver cursos ]                                                                  │ │
│ └─────────────────────────────────────────────────────────────────────────────────┘ │
[ Ver cursos ] leva à vitrine (/cursos).

M3  Carregando: Skeleton do título, de um card e de três linhas.
M4  Erro (502/504): ( ! ) Não foi possível carregar seus pedidos agora.  [ Tentar de novo ]
                                                                         (Alert destructive)
```

```text
M5  Mobile 390 — cada pedido vira um card (sem tabela)
┌──────────────────────────────────┐
│ [</>] Code4Coders             ☰  │
│ MINHA CONTA                      │
│ Meus pedidos                     │
│ ┌─ pendente (destaque) ────────┐ │
│ │ #000124 · 06/10/2026         │ │
│ │ React na prática             │ │
│ │ Acesso por 6 meses           │ │
│ │ R$ 297,00 · Boleto           │ │
│ │ ( ⏱ Aguardando pagamento )   │ │
│ │ Pague até 09/10/2026, 23:59  │ │
│ │ [ Retomar pagamento ]        │ │
│ └──────────────────────────────┘ │
│ ┌──────────────────────────────┐ │
│ │ #000123 · 05/10/2026         │ │
│ │ .NET do zero à API           │ │
│ │ Acesso por 12 meses          │ │
│ │ R$ 497,00 · Cartão de crédito│ │
│ │ ( ✓ Pago )                   │ │
│ │ [ Ir para o curso ]          │ │
│ └──────────────────────────────┘ │
│ ┌──────────────────────────────┐ │
│ │ #000118 · 20/09/2026         │ │
│ │ Testes na prática            │ │
│ │ Acesso vitalício             │ │
│ │ R$ 197,00 · Ainda não escolhido│
│ │ ( ⏱̸ Expirado )  _Ver pedido_ │ │
│ └──────────────────────────────┘ │
│                ‹ 1 2 ›          │
└──────────────────────────────────┘
```

### N · Navegação da conta — onde *Meus pedidos* aparece

```text
N1  Sidebar (desktop) e Sheet (mobile)               N2  Menu da conta (DropdownMenu)
┌──────────────────┐                                  ┌─ DropdownMenu ──────────────┐
│ [</>] Code4Coders│                                  │ Ana Souza                   │
│                  │                                  │ ─────────────────────────── │
│ ▣ Início         │ ◀ ativo em /                     │ 🧾 Meus pedidos             │
│ 🧾 Meus pedidos  │ ◀ ativo em /pedidos e            │ 🔑 Trocar senha             │
│ 🔑 Trocar senha  │   /pedidos/:orderId              │ ↪ Sair                      │
└──────────────────┘                                  └─────────────────────────────┘
```

- **Meus pedidos** é o segundo item da Sidebar, entre Início e Trocar senha, e fica ativo também no detalhe do pedido (`aria-current="page"` na lista; no detalhe, na Sidebar o item fica ativo e o caminho "← Meus pedidos" volta à lista).
- O atalho no menu da conta repete o destino, sem badge nem contador.
- A Sidebar de hoje já tem **Início** e **Trocar senha** (`app-shell.tsx`); **Trocar senha** não muda de lugar nem de destino e continua também no menu da conta. O item novo entra entre os dois (mudança sobre G10 de CAP-017, que dizia "nenhum item novo"; aqui ele nasce porque a tela nova precisa de um ponto de entrada fixo).

---

## 5. Wireframes — Backoffice (`admin-spa`)

### F1 · Pedidos — `/admin/financeiro` (rota do SPA: `/financeiro`)

Substitui a tela "Área reservada" (B11 de [Acesso interno](wireframes-acesso-interno.md)): o `EmptyState` com `CodeWindow` `GET /finance-area` e o texto "Seu acesso está liberado. Vendas, pedidos e repasses aparecem aqui quando o módulo financeiro chegar." deixam de existir. O item **Financeiro** do menu e a permissão `financeiro.ler` não mudam.

```text
F1.a  Desktop 1440 — lista com filtro aplicado (Aguardando pagamento · React na prática)
┌──────────────────────┬──────────────────────────────────────────────────────────────────────┐
│ [</>] Code4Coders    │                                     [◐ tema] ( PA ) Paulo Alves ▾   │
│       Backoffice     ├──────────────────────────────────────────────────────────────────────┤
│ ▣ Início             │ FINANCEIRO                                                (overline) │
│ FINANCEIRO           │ Pedidos                                                        (H2)  │
│ 🎁 Cortesias         │ Compras feitas pelos alunos, com a situação do pagamento.            │
│ $ Financeiro ◀ ativo │                                                                      │
│                      │ ┌─ FinanceFilterBar ────────────────────────────────────────────────┐ │
│                      │ │ Situação ▼ [Aguardando pagamento]   Curso ▼ [React na prática]    │ │
│                      │ │ De [__/__/____]  Até [__/__/____]                                 │ │
│                      │ │ E-mail do aluno [______________________] [ Localizar ]            │ │
│                      │ │                              _Limpar filtros_   [ Filtrar ]       │ │
│                      │ └───────────────────────────────────────────────────────────────────┘ │
│                      │ 1 pedido                                                              │
│                      │ ┌─ Table ───────────────────────────────────────────────────────────┐ │
│                      │ │ Nº      Data       Aluno                 Curso · opção     Valor   Meio    Situação              │
│                      │ │ #000124 06/10/2026 Joana Ribeiro         React na prática  R$ 297,00 Boleto ( ⏱ Aguardando pagamento ) │
│                      │ │                    joana.ribeiro@example.com Acesso por 6 meses                                │
│                      │ └───────────────────────────────────────────────────────────────────┘ │
│                      │                                                  ‹ 1 ›  20/página     │
└──────────────────────┴──────────────────────────────────────────────────────────────────────┘
Do mais recente ao mais antigo. A linha inteira abre o detalhe (F3); o número é o link para
teclado e leitor de tela. Aluno: nome em cima, e-mail embaixo. Meio "—" quando não escolhido.
Situação: ícone + texto (G10), em todas as linhas.
Sem filtros: todas as situações, todos os cursos, sem período, sem aluno.
Datas do período inclusivas, no fuso da escola. "Até" anterior a "De": erro no campo
"A data final não pode ser anterior à inicial." e a busca não roda.
Os filtros aplicam em [ Filtrar ]; a lista não muda enquanto o financeiro digita.
```

```text
F1.b  Lista sem filtros (recorte da tabela; mesmas colunas)
│ #000125 06/10/2026 Marcos Lima          .NET do zero à API Acesso vitalício  R$ 897,00 PIX      ( ✓ Pago )              │
│ #000124 06/10/2026 Joana Ribeiro        React na prática   Acesso por 6 meses R$ 297,00 Boleto  ( ⏱ Aguardando pagamento ) │
│ #000123 05/10/2026 Ana Souza            .NET do zero à API Acesso por 12 meses R$ 497,00 Cartão de crédito ( ✓ Pago )  │
│ #000118 20/09/2026 Rafael Prado         Testes na prática  Acesso vitalício  R$ 197,00 —        ( ⏱̸ Expirado )          │
│ #000101 02/09/2026 Júlia Campos         Fundamentos de C#  Acesso por 3 meses R$ 147,00 PIX     ( ✕ Cancelado )         │

F1.c  Carregando: Skeleton do filtro e de 5 linhas.
F1.d  Sem resultado (com filtro): "Nenhum pedido com estes filtros."  [ Limpar filtros ]
F1.e  Sem pedidos (sem filtro): "Ainda não há pedidos. Quando um aluno comprar um curso, o pedido
      aparece aqui."
F1.f  Erro (502/504): ( ! ) Não foi possível carregar os pedidos agora.  [ Tentar de novo ]
                                                                      (Alert destructive)
      Se a identificação dos alunos não responde, a lista inteira não sai (não aparece lista
      sem nome e e-mail).
F1.g  Sem financeiro.ler (professor, suporte, administrador sem o papel), link direto: B12
      "Esta área não é do seu papel" (EmptyState de Acesso interno). O item do menu nem aparece.
F1.h  Sessão encerrada (401): B1 de Acesso interno.
```

```text
F1.i  Mobile 390 — cada pedido vira um card; filtros recolhidos em um botão
┌──────────────────────────────────┐
│ ☰  Pedidos                        │
│ [ Filtros (2) ]                   │  ◀ abre um Sheet inferior com os campos de F2
│ ┌──────────────────────────────┐  │
│ │ #000124 · 06/10/2026         │  │
│ │ Joana Ribeiro                │  │
│ │ joana.ribeiro@example.com    │  │
│ │ React na prática             │  │
│ │ Acesso por 6 meses           │  │
│ │ R$ 297,00 · Boleto           │  │
│ │ ( ⏱ Aguardando pagamento )   │  │
│ └──────────────────────────────┘  │
│                ‹ 1 2 ›           │
└──────────────────────────────────┘
O número "(2)" do botão Filtros é o total de filtros aplicados, em texto (não só cor).
```

### F2 · Filtros

```text
F2.a  Campos (desktop; no mobile ficam em um Sheet inferior, na mesma ordem)
┌─ FinanceFilterBar ──────────────────────────────────────────────────────────────────┐
│ Situação                    ▼  Todas · Aguardando pagamento · Pago · Expirado · Cancelado │
│ Curso                       ▼  Todos os cursos · busca por título (ponto P8)          │
│ De  [DD/MM/AAAA]  Até [DD/MM/AAAA]   (calendário em Popover; datas inclusivas)         │
│ E-mail do aluno  [ joana.ribeiro@example.com ]  [ Localizar ]                          │
│                                          _Limpar filtros_   [ Filtrar ]               │
└─────────────────────────────────────────────────────────────────────────────────────────┘

F2.b  E-mail do aluno — retorno da busca (mesma busca de Cortesias; o e-mail nunca vai para a URL)
  Localizado:   [ Joana Ribeiro · joana.ribeiro@example.com   ✕ ]   (chip; ✕ remove o filtro)
  Não achado:   Nenhuma conta de aluno com este e-mail.   (texto de ajuda sob o campo; o filtro
                não é aplicado e a lista não muda)
  Indisponível: ( ! ) Não foi possível localizar agora.  [ Tentar de novo ]  (Alert destructive)
  O texto de "não achado" é o mesmo para e-mail inexistente, de ator interno ou de outra escola.
  E-mail com maiúsculas e espaços nas bordas é normalizado.
```

### F3 · Detalhe do pedido — `/admin/financeiro/pedidos/:orderId`

```text
F3.a  Desktop 1440 — pedido pago
┌──────────────────────┬──────────────────────────────────────────────────────────────────────┐
│ $ Financeiro ◀ ativo │ _Financeiro_ / _Pedidos_ / #000123                        (Breadcrumb)│
│                      │ Pedido #000123                                    ( ✓ Pago )    (H2) │
│                      │                                                                      │
│                      │ ┌─ Card: Pedido ────────────────────────────────────────────────────┐ │
│                      │ │ Aluno           Ana Souza · ana.souza@example.com                 │ │
│                      │ │ Curso           .NET do zero à API                                │ │
│                      │ │ Opção           Acesso por 12 meses                               │ │
│                      │ │ Valor           R$ 497,00                                         │ │
│                      │ │ Vigência        Acesso por 12 meses, contados a partir da         │ │
│                      │ │ prometida       liberação do acesso                               │ │
│                      │ └───────────────────────────────────────────────────────────────────┘ │
│                      │ ┌─ Card: Pagamento ─────────────────────────────────────────────────┐ │
│                      │ │ Meio                  Cartão de crédito                           │ │
│                      │ │ Valor recebido        R$ 497,00                                   │ │
│                      │ │ Referência do         pi_3Q2w3E4r5T6y7U8i0   [ ⧉ Copiar ]          │ │
│                      │ │ pagamento no gateway  (para localizar o pagamento no painel do    │ │
│                      │ │                       gateway)                                    │ │
│                      │ └───────────────────────────────────────────────────────────────────┘ │
│                      │ ┌─ Card: Momentos (OrderTimeline) ──────────────────────────────────┐ │
│                      │ │ Criado em              05/10/2026 às 14:19                        │ │
│                      │ │ Página de pagamento    válida até 06/10/2026 às 14:19             │ │
│                      │ │ Pago em                05/10/2026 às 14:21                        │ │
│                      │ └───────────────────────────────────────────────────────────────────┘ │
│                      │ ┌─ Card: Acesso ────────────────────────────────────────────────────┐ │
│                      │ │ Concessão              ( ✓ Existe )                               │ │
│                      │ │ Acesso liberado em     05/10/2026 às 14:21                        │ │
│                      │ └───────────────────────────────────────────────────────────────────┘ │
│                      │ _← Voltar para Pedidos_                                              │
└──────────────────────┴──────────────────────────────────────────────────────────────────────┘
Só leitura (DP-09): nenhum botão de cancelar, estornar, reenviar comprovante nem conceder.
"Voltar para Pedidos" mantém os filtros e a página da lista de onde o financeiro veio.
Os momentos aparecem só quando existem; os que não existem não viram linha vazia.
```

```text
F3.b  Variantes por situação (cada uma mostra só o que existe)
  Aguardando pagamento ... Meio (ou "Ainda não escolhido"); "Pague até" = prazo do boleto/PIX ou
                           da página; Pago/Expirado/Cancelado em: não aparecem;
                           Concessão: ( — Sem concessão: pedido não pago )
  Pago, acesso em liberação  Pago em preenchido; Concessão: ( ⏱ Ainda não existe ) e
                           "O acesso é liberado sozinho; atualize em alguns instantes."
  Expirado ............... Momentos: Criado em, Expirado em; Concessão: ( — Sem concessão )
  Cancelado .............. Momentos: Criado em, Cancelado em; Concessão: ( — Sem concessão )
  Pago depois de expirar ou cancelar (RN-V08): Momentos: Criado em, Expirado em (ou Cancelado em),
                           Pago em; Concessão existe; situação ( ✓ Pago )
  Vitalícia: Vigência prometida "Acesso vitalício".

F3.c  Pedido não encontrado (404: inexistente ou de outra escola)
│ Pedido não encontrado                                                         (H3)  │
│ Este pedido não existe ou não pertence a esta escola.   [ Voltar para Pedidos ]     │
F3.d  Carregando: Skeleton do título e de quatro Cards.
F3.e  Erro (502/504): ( ! ) Não foi possível carregar o pedido agora.  [ Tentar de novo ]
F3.f  Sem financeiro.ler, link direto: B12.
```

```text
F3.g  Mobile 390 — os Cards empilham; cada linha "rótulo: valor" vira rótulo em cima, valor embaixo
┌──────────────────────────────────┐
│ ☰  Pedido #000123                │
│ ( ✓ Pago )                       │
│ PEDIDO                           │
│ Aluno                            │
│ Ana Souza                        │
│ ana.souza@example.com            │
│ Curso                            │
│ .NET do zero à API               │
│ Opção · Valor                    │
│ Acesso por 12 meses · R$ 497,00  │
│ Vigência prometida               │
│ Acesso por 12 meses, contados a  │
│ partir da liberação do acesso    │
│ PAGAMENTO                        │
│ Meio: Cartão de crédito          │
│ Referência no gateway            │
│ pi_3Q2w3E4r5T6y7U8i0  [ ⧉ ]     │
│ MOMENTOS                         │
│ Criado 05/10/2026 às 14:19       │
│ Pago 05/10/2026 às 14:21         │
│ ACESSO                           │
│ Concessão: ( ✓ Existe )          │
│ Liberado 05/10/2026 às 14:21     │
└──────────────────────────────────┘
```

---

## 6. Acessibilidade e conteúdo

| Ponto | Regra |
|---|---|
| Mudança de estado do pedido | A região do painel de estado tem `aria-live="polite"`. Troca de "Confirmando pagamento"/"Liberando seu acesso" para "Compra confirmada" é anunciada ("Compra confirmada") e o foco vai para *Ir para o curso*. O aviso de demora é anunciado uma vez |
| Situação nunca só por cor | `Badge` com ícone + texto em todas as telas (pedido, Meus pedidos, backoffice). Ícones decorativos com `aria-hidden`; o texto da situação é o nome acessível |
| Tabelas | `Table` com `caption` visível ou `aria-label` ("Meus pedidos", "Pedidos da escola"); cabeçalhos `scope="col"`; número do pedido é o link da linha. No mobile os cards mantêm a ordem de leitura rótulo → valor |
| Diálogo de desistência | `AlertDialog` com foco inicial na ação segura (*Manter pedido*), Esc fecha, foco volta ao botão de origem |
| Botão com carregamento | Spinner com texto ("Abrindo o pagamento…", "Cancelando…") e `aria-busy`; botão desabilitado contra duplo clique |
| Formulário de filtros | Cada campo com `Label`; erro no campo com `aria-describedby`; *Filtrar* é botão de envio; o resultado é anunciado ("N pedidos") |
| Datas e valores | Texto, nunca só ícone; "R$ 497,00" e "DD/MM/AAAA" sempre com o formato completo |
| Troca de contexto | O resumo avisa antes do clique que o pagamento é em ambiente seguro externo (RN-CB01, Experiência do Usuário) |
| Dados sensíveis | Nenhum dado de cartão em tela; a referência do gateway só aparece no backoffice (G18) |
| Tema | Claro e escuro pelos tokens existentes; as cores de situação usam `success`, `warning`, `destructive` e `secondary` do `Badge` |

---

## 7. Fora deste documento

- **Figma:** na [seção 8](#8-handoff-do-figma--aprovado) (task 2.0), aprovado pelo responsável em 2026-10-05.
- **Comprovante por e-mail (RF-09):** o texto é do modelo de Notificação (task 8.0), não de tela.
- **Página de pagamento:** é do gateway (DP-01); este documento registra só o que o aluno vê antes de ir e ao voltar.
- **Backoffice do Catálogo:** a contagem de cliques rotulada "cliques antes da venda abrir" (DP-06) não é desenhada aqui (ponto P9).
- **Ação do financeiro sobre pedido:** fora da entrega (DP-09).

---

## 8. Handoff do Figma — aprovado

O desenho foi materializado no arquivo **Code4Coders — Design System** em 2026-10-05, nas páginas `Fluxo — Compra` e `Screens — Compra`, sobre o ASCII aprovado (seções 1 a 7), sem reabrir fluxo nem estados. A revisão começa pelo [índice de revisão](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=295-88).

> **Aprovação:** Figma aprovado pelo responsável em 2026-10-05 ("Aprovado!"). Código de tela liberado a partir da task 4.0.

### 8.1 Fluxos e navegação

| Artefato | Link e `node-id` |
|---|---|
| Índice de revisão | [295:88](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=295-88) |
| Fluxo 1 · Aluno — comprar um curso | [295:170](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=295-170) |
| Fluxo 2 · Aluno — PIX/boleto, retomar, desistir, Meus pedidos | [295:273](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=295-273) |
| Fluxo 3 · Financeiro — consultar pedidos | [295:376](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=295-376) |
| Página Fluxo — Compra | [284:13062](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=284-13062) |
| Página Screens — Compra | [284:13063](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=284-13063) |

Cada passo dos fluxos tem o link **Revisar tela →**, que abre o frame correspondente pelo link do arquivo. B12 (sem permissão, [90:2842](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=90-2842)) e B1 (sessão encerrada, [70:199](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=70-199)) reusam os frames de Acesso interno. Os dados são ilustrativos e o desenho não executa pagamento, consulta nem autorização; foco, teclado, polling, idempotência e regras do servidor seguem as seções 1 a 6.

### 8.2 Inventário de telas e estados

Desktop em 1440 de largura (altura 900, ou a do conteúdo quando ele passa disso); mobile em 390. Light em todas as telas; Dark em quatro (O3, M1, F1.b e C1.b). Reuso do AppShell do aluno e do backoffice, `Button`, `Badge`, `Avatar`, `Empty State`, `OfferOption` e `Sidebar Nav Item` do design system, com variáveis e estilos de texto do arquivo (sem valor fixo de cor).

**Ajustes de desenho sem mudar decisão:** estados a mais que o ASCII listava: O3.b (vitalícia, aberta depois), O5.b e O7.b (pagamento indisponível ao obter instruções), O8.b (erro na desistência), F2.c (período inválido), F3.b1 a F3.b5 (variantes do detalhe), F2.b (estados da busca de e-mail), e as versões mobile de M2, M3 e M4. O ícone da situação é um vetor `lucide` (relógio, check, relógio riscado, X), nunca só cor; o Skeleton usa `secondary`. Quatro ícones lucide novos foram criados como componentes: `Icon/receipt`, `Icon/timer-off`, `Icon/loader-circle` e `Icon/copy`.

**Aplicação das recomendações da revisão da task 1.0:** R2 (foco): uma regra só, no G9; R3 (campos completos): vigência no F3.g, data de criação no pendente do M5 e Resumo completo nas variantes mobile do pedido (O13, O5.m, O7.m, O9.m, O10.m); R4 (navegação): a Sidebar do desenho mostra **Início**, **Meus pedidos** e **Trocar senha**, nessa ordem, como no `AppShell` de hoje.

**Texto proposto para aprovação (R1):** no estado Compra confirmada (O3 e O13), o Figma usa **"Você receberá o comprovante por e-mail."** no lugar de "Enviamos o comprovante para o seu e-mail." do ASCII, porque a concessão pode acontecer antes de o envio estar confirmado (RF-09). Aprovado pelo responsável em 2026-10-05; o ASCII de O3 e O13 foi alinhado no mesmo registro.


#### 🧩 Composições propostas — Compra — [grupo 287:37138](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=287-37138)

| Tela / estado | Dimensão | Link e `node-id` |
|---|---|---|
| OrderStatusBadge | 562 × 48 | [287:37139](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=287-37139) |
| PurchaseSummaryCard | 640 × 404 | [294:39040](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=294-39040) |
| OrderStatePanel | 760 × 246 | [294:39057](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=294-39057) |
| OrderRow | 1112 × 116 | [294:39069](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=294-39069) |
| FinanceOrderRow | 1110 × 66 | [294:39096](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=294-39096) |
| FinanceFilterBar | 1112 × 328 | [294:39130](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=294-39130) |
| OrderTimeline | 760 × 174 | [294:39150](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=294-39150) |

#### P2 · Página do curso — botão Comprar (CAP-003) — [grupo 294:2003](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=294-2003)

| Tela / estado | Dimensão | Link e `node-id` |
|---|---|---|
| P2.a · Opções de acesso (lateral) 1440 | 1440 × 659 | [294:2027](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=294-2027) |
| P2.b · Opções de acesso | 390 × 671 | [294:2029](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=294-2029) |
| P2.c · Login/cadastro vindo de uma compra — recorte | 520 × 252 | [294:2049](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=294-2049) |

#### C1 · Resumo da compra — /comprar/:offerId — [grupo 288:84](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=288-84)

| Tela / estado | Dimensão | Link e `node-id` |
|---|---|---|
| C1.a · Resumo | 1440 × 900 | [288:85](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=288-85) |
| C1.b · Com aviso de acesso existente | 1440 × 900 | [288:151](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=288-151) |
| C1.c · Pedido pendente na mesma opção | 1440 × 900 | [288:225](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=288-225) |
| C1.d · Opção indisponível | 1440 × 900 | [288:280](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=288-280) |
| C1.e · Pagamento indisponível | 1440 × 900 | [288:346](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=288-346) |
| C1.f · Carregando | 1440 × 900 | [288:427](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=288-427) |
| C1.g · Erro ao carregar | 1440 × 900 | [288:472](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=288-472) |
| C1.h · Resumo | 390 × 844 | [288:522](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=288-522) |

#### O · Pedido — /pedidos/:orderId — [grupo 289:286](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=289-286)

| Tela / estado | Dimensão | Link e `node-id` |
|---|---|---|
| O1 · Confirmando pagamento | 1440 × 900 | [289:287](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=289-287) |
| O2 · Confirmação demorando | 1440 × 900 | [289:363](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=289-363) |
| O3 · Compra confirmada | 1440 × 900 | [289:448](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=289-448) |
| O3.b · Compra confirmada, vitalícia, aberta depois | 1440 × 900 | [289:528](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=289-528) |
| O4 · Liberando seu acesso | 1440 × 900 | [289:604](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=289-604) |
| O5 · Aguardando pagamento do boleto | 1440 × 900 | [289:685](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=289-685) |
| O5.b · Boleto — erro ao obter instruções | 1440 × 982 | [289:769](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=289-769) |
| O6 · Aguardando pagamento do PIX | 1440 × 900 | [289:865](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=289-865) |
| O7 · Aguardando pagamento sem meio escolhido | 1440 × 900 | [289:949](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=289-949) |
| O7.b · Pagamento indisponível ao continuar | 1440 × 958 | [289:1033](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=289-1033) |
| O8 · Confirmar desistência (AlertDialog) | 1440 × 900 | [289:1129](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=289-1129) |
| O8.b · Desistência com erro | 1440 × 900 | [289:1228](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=289-1228) |
| O9 · Expirado | 1440 × 918 | [289:1331](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=289-1331) |
| O10 · Cancelado | 1440 × 918 | [289:1425](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=289-1425) |
| O11 · Pedido não encontrado | 1440 × 900 | [289:1510](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=289-1510) |
| O12.a · Carregando o pedido | 1440 × 900 | [289:1558](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=289-1558) |
| O12.b · Erro ao carregar o pedido | 1440 × 900 | [289:1607](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=289-1607) |
| O12.c · Consulta de acompanhamento falhou | 1440 × 900 | [289:1654](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=289-1654) |

#### O · Pedido — Mobile 390 — [grupo 290:796](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=290-796)

| Tela / estado | Dimensão | Link e `node-id` |
|---|---|---|
| O1.m · Confirmando pagamento | 390 × 844 | [290:797](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=290-797) |
| O13 · Compra confirmada | 390 × 898 | [290:852](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=290-852) |
| O5.m · Aguardando pagamento do boleto | 390 × 974 | [290:911](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=290-911) |
| O7.m · Aguardando pagamento sem meio escolhido | 390 × 934 | [290:971](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=290-971) |
| O8.m · Confirmar desistência (Sheet inferior) | 390 × 974 | [290:1031](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=290-1031) |
| O9.m · Expirado | 390 × 976 | [290:1102](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=290-1102) |
| O10.m · Cancelado | 390 × 1040 | [290:1172](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=290-1172) |

#### M · Meus pedidos — /pedidos — [grupo 291:960](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=291-960)

| Tela / estado | Dimensão | Link e `node-id` |
|---|---|---|
| M1 · Com pedidos | 1440 × 900 | [291:961](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=291-961) |
| M2 · Vazio | 1440 × 900 | [291:1115](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=291-1115) |
| M3 · Carregando | 1440 × 900 | [291:1167](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=291-1167) |
| M4 · Erro ao carregar | 1440 × 900 | [291:1214](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=291-1214) |
| M5 · Meus pedidos | 390 × 1130 | [291:1264](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=291-1264) |
| M2.m · Vazio | 390 × 844 | [291:1351](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=291-1351) |
| M3.m · Carregando | 390 × 844 | [291:1379](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=291-1379) |
| M4.m · Erro ao carregar | 390 × 844 | [291:1401](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=291-1401) |

#### N · Navegação da conta — onde Meus pedidos aparece — [grupo 291:1427](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=291-1427)

| Tela / estado | Dimensão | Link e `node-id` |
|---|---|---|
| N1.a · Sidebar (desktop) — ativo em / | 264 × 360 | [291:1428](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=291-1428) |
| N1.b · Sidebar (desktop) — ativo em /pedidos e /pedidos/:orderId | 264 × 360 | [291:1453](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=291-1453) |
| N1.c · Sheet (mobile) — menu ☰ | 390 × 844 | [291:1479](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=291-1479) |
| N2 · Menu da conta (DropdownMenu) | 232 × 195 | [291:1507](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=291-1507) |

#### F · Pedidos — lista e filtros — /financeiro — [grupo 292:1223](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=292-1223)

| Tela / estado | Dimensão | Link e `node-id` |
|---|---|---|
| F1.a · Lista com filtro aplicado | 1440 × 900 | [292:1277](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=292-1277) |
| F1.b · Lista sem filtros | 1440 × 1065 | [292:1492](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=292-1492) |
| F1.c · Carregando | 1440 × 900 | [292:1559](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=292-1559) |
| F1.d · Sem resultado com filtros | 1440 × 900 | [292:1631](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=292-1631) |
| F1.e · Sem pedidos | 1440 × 900 | [292:1701](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=292-1701) |
| F1.f · Erro ao carregar | 1440 × 900 | [292:1772](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=292-1772) |
| F2.c · Período inválido | 1440 × 1087 | [292:1986](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=292-1986) |

#### F · Pedido — detalhe só leitura — /financeiro/pedidos/:orderId — [grupo 292:2048](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=292-2048)

| Tela / estado | Dimensão | Link e `node-id` |
|---|---|---|
| F3.a · Pedido pago | 1440 × 1136 | [292:2050](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=292-2050) |
| F3.b1 · Aguardando pagamento | 1440 × 996 | [292:2139](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=292-2139) |
| F3.b2 · Pago, acesso em liberação | 1440 × 1100 | [292:2214](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=292-2214) |
| F3.b3 · Expirado | 1440 × 960 | [292:2298](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=292-2298) |
| F3.b4 · Cancelado | 1440 × 960 | [292:2373](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=292-2373) |
| F3.b5 · Pago depois de expirar (RN-V08) | 1440 × 1136 | [292:2445](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=292-2445) |
| F3.c · Pedido não encontrado | 1440 × 900 | [292:2534](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=292-2534) |
| F3.d · Carregando | 1440 × 900 | [292:2564](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=292-2564) |
| F3.e · Erro ao carregar | 1440 × 900 | [292:2595](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=292-2595) |

#### F · Mobile 390 — lista, filtros e detalhe — [grupo 292:2049](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=292-2049)

| Tela / estado | Dimensão | Link e `node-id` |
|---|---|---|
| F1.i · Lista | 390 × 926 | [292:2624](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=292-2624) |
| F2.a · Filtros (Sheet inferior) | 390 × 844 | [292:2710](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=292-2710) |
| F3.g · Pedido pago | 390 × 1268 | [292:2806](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=292-2806) |
| F2.b · Busca de e-mail — estados (Light) | 560 × 414 | [292:2904](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=292-2904) |

#### Dark mode — validação de tokens — [grupo 294:39229](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=294-39229)

| Tela / estado | Dimensão | Link e `node-id` |
|---|---|---|
| O3 · Compra confirmada (Dark) | 1440 × 900 | [294:39230](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=294-39230) |
| M1 · Com pedidos (Dark) | 1440 × 900 | [294:39282](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=294-39282) |
| F1.b · Lista sem filtros (Dark) | 1440 × 1065 | [294:39382](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=294-39382) |
| C1.b · Com aviso de acesso existente (Dark) | 1440 × 900 | [294:39562](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=294-39562) |
