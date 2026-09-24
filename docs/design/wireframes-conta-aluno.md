# Wireframes ASCII — Conta do aluno (CAP-001) · proposta para aprovação

> **Status:** aprovado (ASCII e Figma) em 2026-09-24 · refatoração ainda não iniciada
> **Objetivo:** reconstruir no Figma, sobre o Design System Code4Coders, as telas já entregues,
> antes de refatorar o `student-spa`. Aprovado este ASCII → desenho no Figma → aprovação → refatoração.
> **Fontes:** `tasks/prd-conta-aluno/prd.md` (RF-01…RF-06), `tasks/prd-notificacao-transacional/prd.md`,
> `tasks/prd-trilha-auditoria/prd.md`, `src/student-spa`, `src/admin-spa`, `DESIGN.md`, `docs/design/Components.md`.

---

## 1. Inventário

### 1.1 O que os PRDs entregam em tela

| PRD | Capacidade | Tem tela? | Observação |
|---|---|---|---|
| Conta e autenticação do aluno | CAP-001 | **Sim** | Todo o `student-spa` atual |
| Notificação transacional | CAP-026 | **E-mail** | 2 modelos: confirmação de conta e recuperação de senha. PRD pede layout neutro "até a identidade visual existir" (OD3) — agora existe |
| Trilha de auditoria | CAP-030 | **Não** | PRD declara "nesta fatia não há tela" |

### 1.2 Telas construídas (`student-spa`)

| # | Tela | Rota | RF | Estados implementados hoje |
|---|---|---|---|---|
| T1 | Criar conta | `/cadastro` | RF-01 | formulário · enviando · erro (conta existe / política / genérico) · sucesso "Cadastro iniciado" |
| T2 | Confirmar conta | `/confirm-account?token=` | RF-02 | confirmando · confirmada · link indisponível + reenvio · reenvio solicitado |
| T3 | Entrar | `/entrar` | RF-03 | formulário · entrando · credenciais inválidas · e-mail não confirmado |
| T4 | Recuperar senha | `/recuperar-senha` | RF-05 | formulário · enviando · resposta neutra |
| T5 | Redefinir senha | `/redefinir-senha?token=` | RF-05 | formulário · salvando · senha redefinida · link indisponível · erro genérico |
| T6 | Início (logado) | `/` | RF-03/04 | placeholder técnico em inglês ("Learning overview", health-check do serviço) + painel de sessão com "Sair" |
| T7 | Trocar senha | `/trocar-senha` | RF-06 | formulário · salvando · rejeitada · sucesso |
| T8 | Erro de rota | qualquer | — | "Something needs attention" (inglês) |
| — | Sessão expirada | evento global | RF-03 | apenas redireciona para `/entrar`, **sem mensagem** |

`admin-spa`: apenas um placeholder técnico ("Admin Workspace" + health-check). Nenhum PRD aprovado entrega tela de admin ainda (convite e papéis são CAP-002). **Proposta: fora desta rodada.**

### 1.3 Lacunas encontradas (PRD × implementação × Design System)

| # | Lacuna | Origem | Proposta no wireframe |
|---|---|---|---|
| L1 | Sessão expirada volta ao login sem explicar o que houve | PRD, Experiência do Usuário: "retorna à entrada sem perder a compreensão" | `Alert` informativo no topo de T3 |
| L2 | Cadastro com e-mail existente só mostra texto, sem caminhos | RF-01: "orienta a entrar, recuperar a senha ou pedir nova confirmação" | `Alert` com 3 links de ação |
| L3 | "Conta confirmada" não oferece botão para entrar | RF-02 | CTA "Entrar na plataforma" |
| L4 | Login "não confirmado" leva a `/confirm-account` sem token, que abre com título "Link indisponível" | RF-03 | Estado próprio "Reenviar confirmação", sem tom de erro |
| L5 | Troca de senha não avisa que as outras sessões foram encerradas | RF-06 | Texto do sucesso menciona isso |
| L6 | Requisitos de senha só em texto fixo; erro diz "não atende à política" | DESIGN.md §11: erro diz o que fazer | Checklist ao vivo dos 4 requisitos + tamanho |
| L7 | Textos em inglês (shell, início, erro), marca "student-spa" | DESIGN.md §11 (pt-BR) | Tudo em pt-BR, marca Code4Coders |
| L8 | Sem shell de app logado (Sidebar + Topbar do DS) nem menu de conta | DESIGN.md §5 | App shell do DS com menu de usuário (Trocar senha · Sair) |
| L9 | Sem "mostrar senha" | Acessibilidade/usabilidade | Botão ghost de ícone no campo de senha |

> Observação de stack: `DESIGN.md` cita Next.js; o projeto usa React + Vite. shadcn/ui e os tokens
> funcionam igual; só a parte de fontes/tema do DESIGN.md muda na refatoração (sem impacto no Figma).

---

## 2. Fluxo do usuário

```
                              ┌──────────────────┐
                              │    Visitante     │
                              └────────┬─────────┘
                ┌──────────────────────┼─────────────────────────┐
                ▼                      ▼                         ▼
        ┌──────────────┐       ┌──────────────┐          ┌───────────────┐
        │ T1 Criar     │◀─────▶│ T3 Entrar    │─────────▶│ T4 Recuperar  │
        │    conta     │ links │              │"Esqueceu │    senha      │
        └──────┬───────┘       └──┬───┬───┬───┘ a senha?"└───────┬───────┘
     cadastro  │        credencial│   │   │conta não             │ sempre resposta
     válido    ▼        inválida  │   │   │confirmada            ▼ neutra
        ┌──────────────┐   (fica  │   │   ▼                ┌───────────────┐
        │ T1·sucesso   │   em T3) │   │ ┌──────────────┐   │ T4·enviado    │
        │ "Verifique   │          │   │ │ T3·não       │   │ "Verifique    │
        │  seu e-mail" │          │   │ │ confirmado   │   │  seu e-mail"  │
        └──────┬───────┘          │   │ └──────┬───────┘   └───────┬───────┘
               │                  │   │        │ "Reenviar link"   │
               ▼                  │   │        ▼                   ▼
        ╔══════════════╗          │   │ ┌──────────────┐    ╔═══════════════╗
        ║ ✉ E-mail     ║          │   │ │ T2·reenviar  │    ║ ✉ E-mail      ║
        ║ confirmação  ║◀─────────┼───┼─│ confirmação  │    ║ recuperação   ║
        ╚══════╤═══════╝          │   │ └──────────────┘    ╚═══════╤═══════╝
               │ clica no link    │   │                             │ clica no link
               ▼                  │   │                             ▼
        ┌──────────────┐          │   │                     ┌───────────────┐
        │ T2 Confirmar │          │   │                     │ T5 Redefinir  │
        │ (validando)  │          │   │                     │    senha      │
        └──┬────────┬──┘          │   │                     └──┬─────────┬──┘
     válido│        │expirado/    │   │               válido+  │         │ link
           ▼        ▼usado        │   │               senha ok ▼         ▼ inválido
   ┌───────────┐ ┌─────────────┐  │   │             ┌──────────────┐ ┌──────────────┐
   │T2·confir- │ │T2·link indis│  │   │             │T5·redefinida │ │T5·link indis-│
   │mada       │ │ponível +    │──┘   │             │              │ │ponível       │──▶ T4
   └─────┬─────┘ │reenvio      │      │             └──────┬───────┘ └──────────────┘
         │       └─────────────┘      │                    │
         └──────────────▶ T3 ◀────────┼────────────────────┘
                                      │ credencial válida + conta confirmada
                                      ▼
   ┌──────────────────────────────────────────────────────────────────────┐
   │                        ÁREA LOGADA (App Shell)                       │
   │   ┌──────────────┐   menu de conta   ┌──────────────┐                │
   │   │ T6 Início    │──────────────────▶│ T7 Trocar    │──▶ T7·sucesso  │
   │   │              │◀──────────────────│    senha     │    (outras     │
   │   └──────┬───────┘   "Voltar"        └──────────────┘    sessões     │
   │          │ menu de conta → "Sair"                        encerradas) │
   └──────────┼───────────────────────────────────────────────────────────┘
              ▼                                    ⏱ inatividade / revogação
        T3 Entrar  ◀────────────────────────────── T3 + Alert "Sua sessão expirou"
                                                   (em qualquer tela logada)

   Qualquer rota inexistente ou falha de renderização ──▶ T8 Erro
```

---

## 3. Layouts base

### 3.1 AuthLayout — telas públicas (T1–T5)

Desktop 1440. Metade esquerda: formulário em `Card` (máx. 420px). Metade direita: painel `bg-inverse`
com um `CodeWindow` ilustrativo (princípio "código é a estrela"). No mobile (390), o painel some e
o formulário ocupa a largura toda.

```
Desktop 1440 ────────────────────────────────────────────────────────────────────────────────
┌─────────────────────────────────────────────┬─────────────────────────────────────────────┐
│ [</>] Code4Coders                           │░░░░░░░░░░░░░░ bg-inverse (dark) ░░░░░░░░░░░░│
│                                             │░░                                         ░░│
│                                             │░░   ┌─ CodeWindow ─────────────── ● ● ● ┐ ░░│
│         ┌─ Card ───────────────────┐        │░░   │ // aluno.ts                       │ ░░│
│         │                          │        │░░   │ const voce = new Dev({            │ ░░│
│         │   << conteúdo da tela >> │        │░░   │   curiosidade: Infinity,          │ ░░│
│         │                          │        │░░   │ });                               │ ░░│
│         │                          │        │░░   │ await voce.aprender();  ✓ passou  │ ░░│
│         └──────────────────────────┘        │░░   └───────────────────────────────────┘ ░░│
│                                             │░░                                         ░░│
│                                             │░░   Aprenda programando de verdade.       ░░│
│                                             │░░   Cursos práticos, de dev para dev.     ░░│
│ © Code4Coders · Termos · Privacidade        │░░                                         ░░│
└─────────────────────────────────────────────┴─────────────────────────────────────────────┘

Mobile 390 ─────────────────────────
┌──────────────────────────────────┐
│ [</>] Code4Coders                │
│                                  │
│  << conteúdo da tela, sem Card, │
│     largura total, px-4 >>       │
│                                  │
│ © Code4Coders                    │
└──────────────────────────────────┘
```

### 3.2 AppShell — área logada (T6, T7)

`Sidebar` shadcn (264px) + `Topbar` (68px) + conteúdo em `bg-muted` com `p-8`.
Nesta fase a sidebar só tem "Início" (os outros itens chegam com os próximos CAPs).
No mobile, a sidebar vira `Sheet` aberto pelo botão ☰.

```
┌──────────────────┬─────────────────────────────────────────────────────────────────────────┐
│ [</>] Code4Coders│                                          [◐ tema]  ( AS ) Ana Souza ▾   │ ← Topbar 68
│                  ├─────────────────────────────────────────────────────────────────────────┤
│ ▣ Início  ◀ ativo│ bg-muted                                             ┌─ DropdownMenu ─┐ │
│                  │                                                      │ Ana Souza      │ │
│                  │   << conteúdo da página >>                           │ ana@exemplo.com│ │
│                  │                                                      │ ────────────── │ │
│                  │                                                      │ 🔑 Trocar senha│ │
│                  │                                                      │ ↪ Sair         │ │
│                  │                                                      └────────────────┘ │
│ Sidebar 264      │                                                                         │
└──────────────────┴─────────────────────────────────────────────────────────────────────────┘
```

### 3.3 Componentes do DS usados

`Card` · `Form`/`FormField`/`Label`/`Input` · `Button` (default, outline, ghost icon, link) ·
`Alert` (default, destructive) · `Sidebar` · `DropdownMenu` · `Avatar` · `Skeleton` · `CodeWindow` ·
`Sonner` (toast de "Sessão encerrada"). **Componente novo proposto:** `PasswordField` =
`Input` + botão mostrar/ocultar + checklist de requisitos (entra também no Figma e no `Components.md`).

---

## 4. Wireframes (conteúdo do Card)

Legenda: `[ Botão primário ]` · `[ Botão outline ]` · `link sublinhado`→ `_texto_` ·
`( ! )` Alert · `[____]` Input · `👁` mostrar senha · `✓`/`○` requisito atendido/pendente.

### T1 · Criar conta — `/cadastro`

```
T1.a  Formulário (com validação ao vivo)          T1.b  Erro: e-mail já cadastrado (L2)
┌──────────────────────────────────────┐          ┌──────────────────────────────────────┐
│ CONTA DO ALUNO            (overline) │          │ CONTA DO ALUNO                       │
│ Criar conta                     (H2) │          │ Criar conta                          │
│ Enviaremos um link para confirmar    │          │                                      │
│ seu e-mail antes de liberar o acesso.│          │ ( ! ) Já existe uma conta com esse   │
│                                      │          │       e-mail. O que você quer fazer? │
│ Nome                                 │          │       _Entrar_ · _Recuperar senha_ · │
│ [ Ana Souza                        ] │          │       _Reenviar confirmação_         │
│ E-mail                               │          │                                      │
│ [ ana@exemplo.com                  ] │          │ Nome     [ Ana Souza               ] │
│ Senha                                │          │ E-mail   [ ana@exemplo.com         ] │
│ [ ••••••••••                   👁 ] │          │ Senha    [ ••••••••••          👁 ] │
│  ✓ 8+ caracteres    ✓ letra maiúscula│          │                                      │
│  ✓ letra minúscula  ○ número         │          │ [          Criar conta             ] │
│  ○ símbolo                           │          │                                      │
│                                      │          │ Já tem conta? _Entrar_               │
│ [          Criar conta             ] │          └──────────────────────────────────────┘
│                                      │
│ Já tem conta? _Entrar_               │          Outros erros → Alert destructive no mesmo
└──────────────────────────────────────┘          lugar: "Não conseguimos criar sua conta
Enviando: botão desabilitado "Criando conta…"     agora. Tente de novo em instantes."

T1.c  Sucesso
┌──────────────────────────────────────┐
│            ( ✉ )  tile ícone         │
│ Confira seu e-mail              (H2) │
│ Enviamos um link de confirmação para │
│ **ana@exemplo.com**. Ele vale por    │
│ tempo limitado.                      │
│                                      │
│ Sua conta ainda não dá acesso a      │
│ cursos — isso vem com a matrícula.   │
│                                      │
│ Não chegou? Olhe o spam ou           │
│ _reenvie o link de confirmação_      │
│                                      │
│ [        Ir para a entrada         ] │ (outline)
└──────────────────────────────────────┘
```

### T2 · Confirmar conta — `/confirm-account`

```
T2.a  Validando link                              T2.b  Conta confirmada (L3)
┌──────────────────────────────────────┐          ┌──────────────────────────────────────┐
│ ▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒  (Skeleton título)  │          │        ( ✓ )  tile success-soft      │
│ ▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒         │          │ E-mail confirmado               (H2) │
│                                      │          │ Tudo certo. Sua conta está pronta    │
│ Confirmando seu e-mail…              │          │ para entrar.                         │
│ (aria-live, sem spinner em tela cheia)│         │                                      │
└──────────────────────────────────────┘          │ [       Entrar na plataforma       ] │
                                                  └──────────────────────────────────────┘

T2.c  Link indisponível (expirado/usado)          T2.d  Reenviar confirmação (vindo do login, L4)
┌──────────────────────────────────────┐          ┌──────────────────────────────────────┐
│        ( ! )  tile warning-soft      │          │ CONTA DO ALUNO                       │
│ Este link não vale mais         (H2) │          │ Reenviar confirmação            (H2) │
│ Ele expirou ou já foi usado. Peça um │          │ Informe o e-mail do cadastro. Se     │
│ novo — sua conta continua salva.     │          │ houver conta pendente, enviamos um   │
│                                      │          │ novo link.                           │
│ E-mail                               │          │                                      │
│ [                                  ] │          │ E-mail                               │
│                                      │          │ [                                  ] │
│ [       Receber novo link          ] │          │ [       Receber novo link          ] │
│                                      │          │                                      │
│ _Voltar para a entrada_              │          │ _Voltar para a entrada_              │
└──────────────────────────────────────┘          └──────────────────────────────────────┘

T2.e  Novo link solicitado (resposta neutra)
┌──────────────────────────────────────┐
│            ( ✉ )                     │
│ Confira seu e-mail              (H2) │
│ Se houver uma conta pendente para    │
│ esse e-mail, o novo link já está a   │
│ caminho.                             │
│ [        Ir para a entrada         ] │
└──────────────────────────────────────┘
```

### T3 · Entrar — `/entrar`

```
T3.a  Formulário                                  T3.b  Credenciais inválidas (mensagem genérica)
┌──────────────────────────────────────┐          ┌──────────────────────────────────────┐
│ CONTA DO ALUNO                       │          │ Entrar                               │
│ Entrar                          (H2) │          │                                      │
│ Bora continuar de onde parou.        │          │ ( ! ) E-mail ou senha incorretos.    │
│                                      │          │       Confira e tente de novo.       │
│ E-mail                               │          │       (Alert destructive)            │
│ [                                  ] │          │                                      │
│ Senha            _Esqueceu a senha?_ │          │ E-mail [ ana@exemplo.com          ]  │
│ [                              👁 ] │          │ Senha  [                       👁 ]  │
│                                      │          │                                      │
│ [             Entrar               ] │          │ [             Entrar               ] │
│                                      │          └──────────────────────────────────────┘
│ Novo por aqui? _Criar conta_         │
└──────────────────────────────────────┘

T3.c  Conta não confirmada                        T3.d  Sessão expirada / encerrada (L1)
┌──────────────────────────────────────┐          ┌──────────────────────────────────────┐
│ Entrar                               │          │ Entrar                               │
│                                      │          │                                      │
│ ( i ) Falta confirmar seu e-mail.    │          │ ( i ) Sua sessão expirou por         │
│       Use o link que enviamos no     │          │       inatividade. Entre de novo     │
│       cadastro ou peça outro.        │          │       para continuar.                │
│       [ Reenviar confirmação ]       │          │       (Alert default, some ao enviar)│
│       (Alert warning-soft, botão     │          │                                      │
│        outline → T2.d)               │          │ E-mail [                          ]  │
│                                      │          │ Senha  [                       👁 ]  │
│ E-mail [ ana@exemplo.com          ]  │          │ [             Entrar               ] │
│ Senha  [                       👁 ]  │          └──────────────────────────────────────┘
│ [             Entrar               ] │          Após "Sair": sem Alert, toast Sonner
└──────────────────────────────────────┘          "Você saiu da sua conta."
```

### T4 · Recuperar senha — `/recuperar-senha`

```
T4.a  Formulário                                  T4.b  Resposta neutra (sempre a mesma)
┌──────────────────────────────────────┐          ┌──────────────────────────────────────┐
│ CONTA DO ALUNO                       │          │            ( ✉ )                     │
│ Recuperar senha                 (H2) │          │ Confira seu e-mail              (H2) │
│ Informe o e-mail da conta. Se ela    │          │ Se houver uma conta de aluno com     │
│ existir, enviamos um link para criar │          │ esse e-mail, enviamos um link para   │
│ uma nova senha.                      │          │ criar uma nova senha. Ele vale por   │
│                                      │          │ tempo limitado.                      │
│ E-mail                               │          │                                      │
│ [                                  ] │          │ [        Voltar para a entrada     ] │
│                                      │          └──────────────────────────────────────┘
│ [   Receber link de recuperação    ] │
│                                      │
│ _← Voltar para a entrada_            │
└──────────────────────────────────────┘
```

### T5 · Redefinir senha — `/redefinir-senha`

```
T5.a  Formulário                                  T5.b  Senha redefinida
┌──────────────────────────────────────┐          ┌──────────────────────────────────────┐
│ CONTA DO ALUNO                       │          │        ( ✓ )  tile success-soft      │
│ Crie uma nova senha             (H2) │          │ Senha redefinida                (H2) │
│ Escolha uma senha que você ainda não │          │ Pronto. Por segurança, encerramos    │
│ usa nesta conta.                     │          │ as outras sessões da sua conta.      │
│                                      │          │                                      │
│ Nova senha                           │          │ [      Entrar com a nova senha     ] │
│ [ ••••••••                     👁 ] │          └──────────────────────────────────────┘
│  ✓ 8+ caracteres    ✓ letra maiúscula│
│  ✓ letra minúscula  ✓ número         │          T5.c  Link indisponível
│  ○ símbolo                           │          ┌──────────────────────────────────────┐
│                                      │          │        ( ! )  tile warning-soft      │
│ [         Redefinir senha          ] │          │ Este link não vale mais         (H2) │
└──────────────────────────────────────┘          │ Ele expirou ou já foi usado. Peça um │
Erro de rede → Alert destructive acima do         │ novo link de recuperação.            │
botão; formulário mantém o valor.                 │ [       Pedir novo link            ] │
                                                  └──────────────────────────────────────┘
```

### T6 · Início (logado) — `/`

Hoje é um placeholder técnico. Nenhum PRD entregue define conteúdo (cursos chegam com CAP-008), então
a proposta é uma tela honesta: boas-vindas + estado vazio no padrão do DS + atalhos da conta.
**O health-check do serviço sai da UI do aluno.**

```
┌──────────────────┬─────────────────────────────────────────────────────────────────────────┐
│ [</>] Code4Coders│                                                   ( AS ) Ana Souza ▾    │
│                  ├─────────────────────────────────────────────────────────────────────────┤
│ ▣ Início         │  INÍCIO                                                    (overline)   │
│                  │  Olá, Ana 👋                                                     (H2)   │
│                  │  Sua conta está pronta.                                                 │
│                  │                                                                         │
│                  │  ┌─ Card ─────────────────────────────────────┐ ┌─ Card (380) ────────┐ │
│                  │  │ ┌─ CodeWindow (pequeno) ─────────── ● ● ●┐ │ │ Sua conta       (H5)│ │
│                  │  │ │ // nenhum curso por aqui ainda         │ │ │ ( AS ) Ana Souza    │ │
│                  │  │ │ cursos.length === 0  // true           │ │ │ ana@exemplo.com     │ │
│                  │  │ └────────────────────────────────────────┘ │ │ ✓ E-mail confirmado │ │
│                  │  │ Seus cursos aparecem aqui              (H4)│ │                     │ │
│                  │  │ Quando você se matricular em um curso, é   │ │ [ Trocar senha    ] │ │
│                  │  │ por aqui que retoma as aulas.              │ │ _Sair_              │ │
│                  │  └────────────────────────────────────────────┘ └─────────────────────┘ │
│                  │                                                                         │
└──────────────────┴─────────────────────────────────────────────────────────────────────────┘
Carregando sessão: Skeleton no formato dos dois cards.
Mobile: cards empilhados; sidebar em Sheet (☰ na topbar).
```

> Estado vazio sem CTA de catálogo, porque o catálogo ainda não existe. Quando existir, entra
> `[ Explorar cursos ]`.

### T7 · Trocar senha — `/trocar-senha` (dentro do AppShell)

```
┌──────────────────┬─────────────────────────────────────────────────────────────────────────┐
│ [</>] Code4Coders│                                                   ( AS ) Ana Souza ▾    │
│ ▣ Início         ├─────────────────────────────────────────────────────────────────────────┤
│                  │  _← Início_                                                             │
│                  │  CONTA                                                                  │
│                  │  Trocar senha                                                   (H2)    │
│                  │                                                                         │
│                  │  ┌─ Card (máx. 560) ───────────────────────────┐                        │
│                  │  │ Senha atual                                 │                        │
│                  │  │ [ ••••••••                              👁 ]│                        │
│                  │  │ Nova senha                                  │                        │
│                  │  │ [ ••••••••••                            👁 ]│                        │
│                  │  │  ✓ 8+ caracteres     ✓ letra maiúscula      │                        │
│                  │  │  ✓ letra minúscula   ✓ número   ○ símbolo   │                        │
│                  │  │                                             │                        │
│                  │  │ ( i ) Ao trocar, as outras sessões da sua   │                        │
│                  │  │       conta serão encerradas. Esta continua.│                        │
│                  │  │                                             │                        │
│                  │  │            [ Cancelar ]  [ Trocar senha ]   │                        │
│                  │  └─────────────────────────────────────────────┘                        │
└──────────────────┴─────────────────────────────────────────────────────────────────────────┘

T7.b  Rejeitada → Alert destructive no Card: "A senha atual não confere ou a nova senha não
      cumpre os requisitos. Confira e tente de novo."
T7.c  Sucesso → volta para T6 com toast Sonner: "Senha trocada. Encerramos as outras sessões."
      (L5; substitui a página de sucesso atual)
```

### T8 · Erro — qualquer rota

```
┌──────────────────────────────────────┐   (AuthLayout se deslogado, AppShell se logado)
│ ┌─ CodeWindow ────────────── ● ● ● ┐ │
│ │ $ GET /pagina-que-nao-existe     │ │
│ │ 404 Not Found                    │ │
│ └──────────────────────────────────┘ │
│ Essa página não existe          (H2) │
│ O endereço pode ter mudado ou estar  │
│ errado.                              │
│ [        Voltar para o início      ] │
└──────────────────────────────────────┘
Variante de falha inesperada: "Algo deu errado aqui" + "Tentar de novo" (outline) + "Voltar".
```

---

## 5. E-mails transacionais (CAP-026)

Restrições do PRD: legível sem imagens, link visível como texto (não só botão), validade real do
link renderizada, sem dado pessoal além do nome. Largura 600, tokens light fixos (clientes de
e-mail não aplicam tema de forma confiável).

```
E1  Confirmação de conta                          E2  Recuperação de senha
┌──────────────────────────────────────┐          ┌──────────────────────────────────────┐
│ </> Code4Coders                      │          │ </> Code4Coders                      │
│──────────────────────────────────────│          │──────────────────────────────────────│
│ Oi, Ana!                             │          │ Oi, Ana!                             │
│                                      │          │                                      │
│ Falta um passo: confirme seu e-mail  │          │ Recebemos um pedido para criar uma   │
│ para entrar na Code4Coders.          │          │ nova senha para sua conta.           │
│                                      │          │                                      │
│ [       Confirmar meu e-mail       ] │          │ [        Criar nova senha          ] │
│                                      │          │                                      │
│ Ou copie este link no navegador:     │          │ Ou copie este link no navegador:     │
│ https://code4coders…/confirm-account │          │ https://code4coders…/redefinir-senha │
│ ?token=…                             │          │ ?token=…                             │
│                                      │          │                                      │
│ ⏱ O link vale por {validade} e só    │          │ ⏱ O link vale por {validade} e só    │
│   funciona uma vez.                  │          │   funciona uma vez.                  │
│                                      │          │                                      │
│ Não criou esta conta? Ignore este    │          │ Não pediu? Ignore este e-mail — sua  │
│ e-mail.                              │          │ senha continua a mesma.              │
│──────────────────────────────────────│          │──────────────────────────────────────│
│ Code4Coders · e-mail automático      │          │ Code4Coders · e-mail automático      │
└──────────────────────────────────────┘          └──────────────────────────────────────┘
```

---

## 6. Plano para o Figma (após aprovação deste ASCII)

Páginas novas no arquivo `Code4Coders — Design System`, sem mexer nas existentes:

1. **🧭 Fluxo — Conta do aluno**: o fluxo da seção 2, com os frames das telas como nós.
2. **📱 Telas — Conta do aluno**: T1–T8, todos os estados, **desktop 1440 + mobile 390**, tema Light.
   Dark só nas telas principais (T1.a, T3.a, T6) para validar os tokens.
3. **✉ E-mails**: E1 e E2 a 600px.
4. **Components**: `PasswordField` novo (variants: vazio, preenchido, erro, requisitos parciais/completos).

Tudo usando as variáveis, text styles e componentes do DS (nenhum valor hardcoded).

---

## 7. Decisões para você aprovar

1. **Admin fora desta rodada** (só existe placeholder técnico; CAP-002 ainda sem PRD).
2. **E-mails E1/E2 entram** na rodada, com a identidade visual nova.
3. **AuthLayout dividido** com `CodeWindow` à direita (no mobile, só o formulário).
4. **Início (T6)** vira boas-vindas + estado vazio + card da conta; health-check sai da UI.
5. **Trocar senha**: sucesso vira toast + retorno ao Início (em vez de página própria).
6. **Correções de lacuna L1–L9** entram no desenho (algumas mudam comportamento, não só visual:
   L1 sessão expirada, L2 caminhos no cadastro duplicado, L4 estado "Reenviar confirmação").
7. **`PasswordField`** como componente novo do DS.

---

## 8. Figma (aprovado em 2026-09-24)

Base: `https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System`

| Entrega | Página | node-id |
|---|---|---|
| Fluxo do usuário | Fluxo — Conta do aluno | `56-3` |
| Componentes novos (proposta) | Screens — Conta do aluno | `33-3` |
| T1 · Criar conta | Screens — Conta do aluno | `35-97` |
| T2 · Confirmar conta | Screens — Conta do aluno | `46-368` |
| T3 · Entrar | Screens — Conta do aluno | `51-1168` |
| T4 · Recuperar senha | Screens — Conta do aluno | `51-1355` |
| T5 · Redefinir senha | Screens — Conta do aluno | `51-1757` |
| T6 · Início (logado) | Screens — Conta do aluno | `53-1617` |
| T7 · Trocar senha | Screens — Conta do aluno | `53-2134` |
| T8 · Erro | Screens — Conta do aluno | `54-1808` |
| E1/E2 · E-mails | Screens — Conta do aluno | `54-1856` |
| Dark mode — validação | Screens — Conta do aluno | `54-1957` |

Componentes propostos (migram para a página Components após aprovação): Icon (16 lucide), Brand Logo,
Status Tile, Password Requirement, Password Field, Form Field, Alert, Auth Brand Panel, Skeleton,
Toast, Menu Item, Account Menu.
