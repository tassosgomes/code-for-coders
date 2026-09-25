# Wireframes ASCII — Acesso interno / backoffice (CAP-002) · proposta para aprovação

> **Status:** aprovado (ASCII e Figma) em 2026-09-25 · implementação não iniciada
> **Objetivo:** desenhar no Figma, sobre o Design System Code4Coders, as telas do backoffice que o PRD
> de acesso interno entrega, antes da implementação no `admin-spa` (tasks 2.0–9.0).
> Aprovado este ASCII → desenho no Figma → aprovação → implementação.
> **Fontes:** `tasks/prd-acesso-interno/prd.md` (RF-01…RF-14), `techspec.md` (Bloco Frontend, V-01…V-08),
> `api-contract.yaml` 1.0.0, `docs/design/wireframes-conta-aluno.md` (padrões já aprovados),
> `DESIGN.md`, `docs/design/Components.md`.

---

## 1. Inventário

### 1.1 O que o PRD entrega em tela

| Entrega | RF | Tem tela? | Observação |
|---|---|---|---|
| Papéis e permissões | RF-01 | Indireta | Aparece como menu por permissão e badges de papel |
| Primeiro administrador (seed) | RF-02 | Reusa B3 | Comando do time; o administrador define a senha pela tela de redefinição |
| Emitir convite | RF-03 | **Sim** | Dialog em Acessos |
| E-mail de convite | RF-04 | **E-mail** | Modelo novo `convite-interno` em Notificação |
| Aceitar convite | RF-05 | **Sim** | Página pública do link |
| Conceder / revogar / trocar papel | RF-06, 07, 09 | **Sim** | Dialogs em Acessos |
| Conta interna sem papel | RF-08 | **Sim** | Estado próprio do Início |
| Entrar e sair | RF-10 | **Sim** | Entrada própria, distinta da do aluno |
| Recuperar senha | RF-11 | **Sim** | Mesmo comportamento do aluno |
| Gestão de acesso | RF-12 | **Sim** | Lista de pessoas e de convites pendentes |
| Área financeira reservada | RF-13 | **Sim** | Página vazia que prova o menor privilégio |
| Atos à Auditoria | RF-14 | Não | Sem tela nesta entrega (consulta da trilha é o 2º PRD de CAP-030) |

### 1.2 Telas (`admin-spa`, base `/admin/`)

| # | Tela | Rota | RF | Permissão | Estados |
|---|---|---|---|---|---|
| B1 | Entrar | `/admin/entrar` | RF-10 | pública | formulário · entrando · credenciais inválidas · sessão encerrada · saiu |
| B2 | Recuperar senha | `/admin/recuperar-senha` | RF-11 | pública | formulário · enviando · resposta neutra |
| B3 | Redefinir senha | `/admin/redefinir-senha?token=` | RF-11, RF-02 | pública | formulário · salvando · redefinida · link indisponível · erro genérico |
| B4 | Aceitar convite | `/admin/convite?token=` | RF-05 | pública | validando · formulário · aceitando · convite inválido · e-mail indisponível |
| B5 | Início | `/admin/` | RF-10, RF-08, RF-13 | sessão | com áreas · sem área ainda (professor/suporte) · sem papel · carregando |
| B6 | Acessos | `/admin/acessos` | RF-12 | `acesso.gerir` | pessoas · convites pendentes · vazio · carregando · erro |
| B7 | Convidar (Dialog) | sobre B6 | RF-03 | `acesso.gerir` | formulário · recusas · enviado |
| B8 | Conceder papel (Dialog) | sobre B6 | RF-06 | `acesso.gerir` | formulário · sem papel disponível |
| B9 | Revogar papel (AlertDialog) | sobre B6 | RF-07, RF-08 | `acesso.gerir` | formulário · último papel |
| B10 | Trocar papel (Dialog) | sobre B6 | RF-09 | `acesso.gerir` | formulário |
| B11 | Financeiro | `/admin/financeiro` | RF-13 | `financeiro.ler` | reservada |
| B12 | Sem permissão | qualquer área protegida | RF-12, RF-13 | — | 403 por link direto |
| B13 | Erro | qualquer | — | — | 404 · falha inesperada |

Nenhuma das telas de trabalho de professor, suporte e financeiro entra aqui (vêm com CAP-005, CAP-011 e
suporte); a consulta da trilha também não.

### 1.3 Lacunas e decisões de desenho (PRD × TechSpec × contrato × DS)

| # | Ponto | Origem | Proposta no wireframe |
|---|---|---|---|
| G1 | A entrada do backoffice tem que ser visivelmente distinta da do aluno | PRD, Experiência do Usuário | Mesmo AuthLayout, com marca "Code4Coders **Backoffice**", overline "BACKOFFICE", painel direito com outro `CodeWindow` e o endereço `/admin` à vista |
| G2 | Após revogação ou troca, o ator recebe 401 sem saber o motivo | Contrato: `SESSION_REQUIRED` genérico | Alert neutro em B1: "Sua sessão foi encerrada. Entre de novo." — cobre expiração e revogação sem revelar nada |
| G3 | A sessão (`StaffSession`) não traz e-mail | Contrato; mesma regra de `Components.md` | Menu de conta mostra nome + papéis, **sem e-mail** |
| G4 | Professor e suporte ainda não têm área (telas vêm depois) | PRD, fora de escopo | Início honesto: "As ferramentas do seu papel chegam em breve", sem item de menu fantasma |
| G5 | O motivo não pode citar dado pessoal de terceiros | PRD, RN-A08 | Texto de ajuda fixo sob o campo motivo, em todas as ações |
| G6 | Revogar e trocar desconectam a pessoa | PRD, Fluxo de gestão | Alert warning dentro do dialog: "A pessoa será desconectada agora" |
| G7 | Novo convite para o mesmo e-mail substitui o pendente (DP-04) | Contrato: `supersededInvitationId` | Confirmação do envio avisa que o convite anterior deixou de valer; linha pendente ganha a ação "Enviar novo convite" (pré-preenchida) |
| G8 | Troca para papel que a pessoa já tem vira revogação | RF-09 | O campo "Para" só lista papéis que a pessoa **não** tem; o caso não nasce pela UI |
| G9 | O seed recebe o e-mail do modelo `recuperacao-de-senha` ("Recebemos um pedido…"), estranho para quem nunca pediu nada | TechSpec V-01 (reusa Notificação 1.0.0) | **Fora do desenho** — registrar como observação; B3 usa um título que serve aos dois casos ("Defina sua senha") |
| G10 | Papéis e permissões têm nomes técnicos (`financeiro.ler`) | Contrato | UI só mostra papéis em pt-BR (Administrador, Professor, Suporte, Financeiro); permissões nunca aparecem |

---

## 2. Fluxo do usuário

```
  ┌───────────────┐                           ┌──────────────────────┐
  │ Time (seed)   │ provision-first-admin     │ Administrador        │
  └──────┬────────┘                           └──────────┬───────────┘
         ▼                                               │ B6 Acessos → [ Convidar ]
  ╔═══════════════╗                                      ▼
  ║ ✉ E-mail de   ║                              ┌──────────────┐
  ║ redefinição   ║                              │ B7 Convidar  │──▶ B7·enviado ─┐
  ╚══════╤════════╝                              └──────────────┘ (validade)    │
         │ link                                                                 ▼
         ▼                                                              ╔═══════════════╗
  ┌──────────────┐  inválido ┌──────────────┐                           ║ ✉ E-mail E3   ║
  │ B3 Redefinir │──────────▶│B3·link indis-│──▶ B2                     ║ convite       ║
  │    senha     │           │ponível       │                           ╚═══════╤═══════╝
  └──────┬───────┘           └──────────────┘                                   │ link
         │ ok                                                                   ▼
         ▼                                                              ┌───────────────┐
  ┌──────────────┐  "Esqueceu ┌──────────────┐  sempre    ┌──────────┐ │ B4 Aceitar    │
  │ B1 Entrar    │──a senha?"▶│ B2 Recuperar │──neutra──▶ │ B2·envia-│ │ (validando)   │
  │ /admin/entrar│◀───────────│    senha     │            │ do       │ └──┬─────────┬──┘
  └──┬───────┬───┘            └──────────────┘            └────┬─────┘    │válido   │ inválido /
     │       │ credencial inválida (fica em B1,                │ ✉ E2     ▼         ▼ e-mail indisp.
     │       │ mesma resposta p/ aluno e e-mail inexistente)   ▼ → B3  ┌────────┐ ┌──────────────┐
     │ ok    ▼                                                         │B4 form │ │B4·não vale   │
     │                                                                 │nome +  │ │mais → pedir  │
     │                                                                 │senha   │ │ao admin      │
     │                                                                 └───┬────┘ └──────────────┘
     │                                    aceite = sessão aberta           │
     ▼                                                                     ▼
  ┌──────────────────────────────────────────────────────────────────────────────────────────┐
  │                       BACKOFFICE (AppShell · menu por permissão)                         │
  │                                                                                          │
  │   ┌─────────────┐  acesso.gerir   ┌───────────────┐  ⋯ na linha  ┌─────────────────────┐ │
  │   │ B5 Início   │────────────────▶│ B6 Acessos    │─────────────▶│ B8 Conceder papel   │ │
  │   │ (varia por  │                 │ pessoas +     │              │ B9 Revogar papel    │ │
  │   │  papel)     │  financeiro.ler │ convites      │              │ B10 Trocar papel    │ │
  │   │             │────────────┐    └───────────────┘              └──────────┬──────────┘ │
  │   └──────┬──────┘            ▼                                    revogar/  │ toast      │
  │          │            ┌───────────────┐                           trocar    ▼            │
  │          │ sem papel  │ B11 Financeiro│                         desconecta o outro ator ─┼─┐
  │          ▼            │ (reservada)   │                                                  │ │
  │   B5·sem acesso       └───────────────┘      link direto sem permissão ──▶ B12           │ │
  │   (procure o admin)                                                                      │ │
  └──────────┬───────────────────────────────────────────────────────────────────────────────┘ │
             │ menu de conta → "Sair"                                                          │
             ▼                                                                                 │
        B1 + toast "Você saiu"          B1 + Alert "Sua sessão foi encerrada" ◀────────────────┘
                                        (revogação, troca, inatividade — próxima ação do ator)

   Qualquer rota inexistente ou falha de renderização ──▶ B13 Erro
```

---

## 3. Layouts base

### 3.1 AuthLayout do backoffice — telas públicas (B1–B4)

Reusa o AuthLayout aprovado da conta do aluno (Card de 420px à esquerda, painel `bg-inverse` à direita,
mobile só com o formulário). Para cumprir G1, três diferenças fixas:

- marca **"Code4Coders" + `Badge` "Backoffice"** (variant `outline`) no topo;
- overline **"BACKOFFICE"** em todas as telas (no aluno é "CONTA DO ALUNO");
- `CodeWindow` com outro conteúdo e o endereço `…/admin` na barra da janela.

```
Desktop 1440 ────────────────────────────────────────────────────────────────────────────────
┌─────────────────────────────────────────────┬─────────────────────────────────────────────┐
│ [</>] Code4Coders  (Backoffice)             │░░░░░░░░░░░░░░ bg-inverse (dark) ░░░░░░░░░░░░│
│                                             │░░                                         ░░│
│                                             │░░   ┌─ CodeWindow ── …/admin ──── ● ● ● ┐ ░░│
│         ┌─ Card ───────────────────┐        │░░   │ // acesso.ts                      │ ░░│
│         │                          │        │░░   │ if (!voce.pode("financeiro.ler")) │ ░░│
│         │   << conteúdo da tela >> │        │░░   │   return negar();  // 403         │ ░░│
│         │                          │        │░░   │ abrir(area);        ✓ permitido   │ ░░│
│         │                          │        │░░   └───────────────────────────────────┘ ░░│
│         └──────────────────────────┘        │░░                                         ░░│
│                                             │░░   Operação da escola.                   ░░│
│                                             │░░   Cada pessoa vê só o que é dela.       ░░│
│ © Code4Coders · Área restrita à equipe      │░░                                         ░░│
└─────────────────────────────────────────────┴─────────────────────────────────────────────┘

Mobile 390 ─────────────────────────
┌──────────────────────────────────┐
│ [</>] Code4Coders  (Backoffice)  │
│                                  │
│  << conteúdo da tela, sem Card,  │
│     largura total, px-4 >>       │
│                                  │
│ © Code4Coders · Área restrita    │
└──────────────────────────────────┘
```

### 3.2 AppShell do backoffice — área logada (B5, B6, B11, B12)

Mesma estrutura do AppShell do aluno: `Sidebar` 264 + `Topbar` 68 + conteúdo `bg-muted` com `p-8`.
A sidebar mostra **só** as áreas das permissões da sessão; nada desabilitado ou "trancado".
Mobile: sidebar em `Sheet` pelo botão ☰.

```
┌──────────────────────┬─────────────────────────────────────────────────────────────────────┐
│ [</>] Code4Coders    │                                    [◐ tema]  ( MC ) Marina Costa ▾  │ ← Topbar 68
│       Backoffice     ├─────────────────────────────────────────────────────────────────────┤
│                      │ bg-muted                                        ┌─ DropdownMenu ───┐│
│ ▣ Início    ◀ ativo  │                                                 │ Marina Costa     ││
│ ⚿ Acessos            │  ← só com acesso.gerir                          │ (Administrador)  ││
│ $ Financeiro         │  ← só com financeiro.ler                        │ ──────────────── ││
│                      │                                                 │ ↪ Sair           ││
│                      │   << conteúdo da página >>                      └──────────────────┘│
│                      │                                                                     │
│ Sidebar 264          │                                                                     │
└──────────────────────┴─────────────────────────────────────────────────────────────────────┘
Menu de conta: nome + badges de papel, sem e-mail (G3). Sem "Trocar senha" nesta entrega
(o PRD não traz troca de senha do ator interno; a saída é B2/B3).
```

### 3.3 Componentes do DS usados

`Card` · `Form`/`FormField`/`Label`/`Input`/`Textarea` · `RadioGroup` (papel: 4 opções) · `Button`
(default, outline, ghost icon, destructive, link) · `Alert` (default, warning, destructive) · `Badge`
(outline, secondary) · `Table` · `Tabs` · `DropdownMenu` · `Dialog` / `AlertDialog` · `Sidebar` ·
`Avatar` · `Skeleton` · `Pagination` · `Sonner` · `CodeWindow` · `StatusTile` · `PasswordField`
(já aprovado na conta do aluno).

**Componentes novos propostos:**

- `RoleBadge` = `Badge` com o nome do papel em pt-BR (uma cor neutra só — papel não é status).
- `ReasonField` = `Textarea` + contador + ajuda fixa "Não cite dados pessoais de outras pessoas" (G5).
- `EmptyState` = tile de ícone + título + texto + ação opcional (hoje repetido à mão no Início do aluno).

---

## 4. Wireframes

Legenda: `[ Botão primário ]` · `[ Botão outline ]` · `_link_` · `( ! )` Alert · `[____]` Input ·
`👁` mostrar senha · `✓`/`○` requisito · `(Papel)` RoleBadge · `(•)`/`( )` RadioGroup · `⋯` menu de ações.

### B1 · Entrar — `/admin/entrar`

```
B1.a  Formulário                                  B1.b  Credenciais inválidas (mesma resposta
┌──────────────────────────────────────┐                para aluno e e-mail inexistente)
│ BACKOFFICE                (overline) │          ┌──────────────────────────────────────┐
│ Entrar na operação              (H2) │          │ BACKOFFICE                           │
│ Acesso da equipe da escola. Alunos   │          │ Entrar na operação                   │
│ entram por _code4coders…/entrar_.    │          │                                      │
│                                      │          │ ( ! ) E-mail ou senha incorretos.    │
│ E-mail                               │          │       Confira e tente de novo.       │
│ [                                  ] │          │       (Alert destructive)            │
│ Senha            _Esqueceu a senha?_ │          │                                      │
│ [                              👁 ] │          │ E-mail [ marina@escola.com        ]  │
│                                      │          │ Senha  [                       👁 ]  │
│ [             Entrar               ] │          │ [             Entrar               ] │
│                                      │          └──────────────────────────────────────┘
│ Sem conta? O acesso é por convite    │
│ de um administrador.                 │          Entrando: botão desabilitado "Entrando…"
└──────────────────────────────────────┘

B1.c  Sessão encerrada (G2)                       B1.d  Após "Sair"
┌──────────────────────────────────────┐          ┌──────────────────────────────────────┐
│ BACKOFFICE                           │          │ B1.a sem Alert                       │
│ Entrar na operação                   │          │                                      │
│                                      │          │                  ┌──────────────────┐│
│ ( i ) Sua sessão foi encerrada.      │          │                  │ ✓ Você saiu do   ││
│       Entre de novo para continuar.  │          │                  │   backoffice.    ││
│       (Alert default, some ao enviar)│          │                  └──────── Sonner ──┘│
│                                      │          └──────────────────────────────────────┘
│ E-mail [                          ]  │
│ Senha  [                       👁 ]  │          O texto de B1.c é o mesmo para inatividade,
│ [             Entrar               ] │          revogação e troca de papel: a SPA só recebe
└──────────────────────────────────────┘          401 e não deve adivinhar o motivo.
```

### B2 · Recuperar senha — `/admin/recuperar-senha`

```
B2.a  Formulário                                  B2.b  Resposta neutra (sempre a mesma)
┌──────────────────────────────────────┐          ┌──────────────────────────────────────┐
│ BACKOFFICE                           │          │            ( ✉ )  StatusTile         │
│ Recuperar senha                 (H2) │          │ Confira seu e-mail              (H2) │
│ Informe o e-mail da sua conta da     │          │ Se esse e-mail for de uma conta da   │
│ equipe. Se ela existir, enviamos um  │          │ equipe, enviamos um link para criar  │
│ link para criar uma nova senha.      │          │ uma nova senha. Ele vale por tempo   │
│                                      │          │ limitado.                            │
│ E-mail                               │          │                                      │
│ [                                  ] │          │ [       Voltar para a entrada      ] │
│                                      │          └──────────────────────────────────────┘
│ [   Receber link de recuperação    ] │
│                                      │          E-mail de aluno recebe a mesma tela e
│ _← Voltar para a entrada_            │          nenhum e-mail (RF-11).
└──────────────────────────────────────┘
```

### B3 · Redefinir senha — `/admin/redefinir-senha?token=`

Serve à recuperação (RF-11) **e** ao primeiro administrador do seed (RF-02). Por isso o título é
"Defina sua senha", que faz sentido nos dois casos (G9). O token sai da barra de endereço ao abrir.

```
B3.a  Formulário                                  B3.b  Senha definida
┌──────────────────────────────────────┐          ┌──────────────────────────────────────┐
│ BACKOFFICE                           │          │        ( ✓ )  StatusTile success     │
│ Defina sua senha                (H2) │          │ Senha definida                  (H2) │
│ Escolha uma senha que você ainda não │          │ Pronto. Por segurança, encerramos    │
│ usa nesta conta.                     │          │ as outras sessões da sua conta.      │
│                                      │          │                                      │
│ Nova senha                           │          │ [      Entrar com a nova senha     ] │
│ [ ••••••••                     👁 ] │          └──────────────────────────────────────┘
│  ✓ 8+ caracteres    ✓ letra maiúscula│
│  ✓ letra minúscula  ✓ número         │          B3.c  Link indisponível
│  ○ símbolo                           │          ┌──────────────────────────────────────┐
│                                      │          │        ( ! )  StatusTile warning     │
│ [          Definir senha           ] │          │ Este link não vale mais         (H2) │
└──────────────────────────────────────┘          │ Ele expirou ou já foi usado. Peça um │
Erro de rede → Alert destructive acima do         │ novo link de recuperação.            │
botão; formulário mantém o valor.                 │ [         Pedir novo link          ] │
Política violada no servidor → erro no campo      └──────────────────────────────────────┘
com a regra não atendida.
```

### B4 · Aceitar convite — `/admin/convite?token=`

Abrir o link já comprova o e-mail (RF-05): não há confirmação separada. O e-mail do convidado **não**
aparece na tela (`InvitationPreview` só traz papel e validade).

```
B4.a  Validando convite                           B4.b  Formulário
┌──────────────────────────────────────┐          ┌──────────────────────────────────────┐
│ ▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒  (Skeleton título)  │          │ BACKOFFICE                           │
│ ▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒         │          │ Você foi convidado para a equipe (H2)│
│                                      │          │                                      │
│ Conferindo seu convite…              │          │ ┌─ Card muted ─────────────────────┐ │
│ (aria-live)                          │          │ │ Papel        (Professor)         │ │
└──────────────────────────────────────┘          │ │ Vale até     02/10/2026 às 14:30 │ │
                                                  │ └──────────────────────────────────┘ │
                                                  │                                      │
                                                  │ Seu nome                             │
                                                  │ [                                  ] │
                                                  │ Crie uma senha                       │
                                                  │ [                              👁 ] │
                                                  │  ○ 8+ caracteres    ○ letra maiúscula│
                                                  │  ○ letra minúscula  ○ número         │
                                                  │  ○ símbolo                           │
                                                  │                                      │
                                                  │ [      Aceitar e entrar            ] │
                                                  └──────────────────────────────────────┘
                                                  Aceitando: "Ativando sua conta…". Sucesso →
                                                  B5 com toast "Bem-vindo à equipe, Rafael."

B4.c  Convite inválido (expirado, usado,          B4.d  E-mail indisponível
      substituído ou inexistente)                       (ganhou conta entre emissão e aceite)
┌──────────────────────────────────────┐          ┌──────────────────────────────────────┐
│        ( ! )  StatusTile warning     │          │        ( ! )  StatusTile warning     │
│ Este convite não vale mais      (H2) │          │ Não foi possível ativar      (H2)    │
│ Ele expirou, já foi usado ou foi     │          │ Este convite não pode ser ativado.   │
│ substituído por um mais novo. Peça   │          │ Procure o administrador que te       │
│ um novo ao administrador que te      │          │ convidou.                            │
│ convidou.                            │          │                                      │
│                                      │          │ _Ir para a entrada_                  │
│ _Já tem conta? Entrar_               │          └──────────────────────────────────────┘
└──────────────────────────────────────┘
Nenhum dos dois revela dado da conta ou do convite.
```

### B5 · Início — `/admin/`

Varia pelas permissões da sessão. Cards só para áreas que existem **e** que a pessoa pode abrir.

```
B5.a  Administrador
┌──────────────────────┬─────────────────────────────────────────────────────────────────────┐
│ [</>] Code4Coders    │                                              ( MC ) Marina Costa ▾  │
│       Backoffice     ├─────────────────────────────────────────────────────────────────────┤
│ ▣ Início             │  INÍCIO                                                             │
│ ⚿ Acessos            │  Olá, Marina                                                  (H2)  │
│                      │  Seus papéis: (Administrador)                                       │
│                      │                                                                     │
│                      │  ┌─ Card ──────────────────────────┐                                │
│                      │  │ ⚿  Acessos                  (H5)│                                │
│                      │  │ Convide pessoas e ajuste os     │                                │
│                      │  │ papéis da equipe.               │                                │
│                      │  │ 2 convites pendentes            │ ← contagem só se já carregada  │
│                      │  │ [ Abrir acessos → ]             │                                │
│                      │  └─────────────────────────────────┘                                │
└──────────────────────┴─────────────────────────────────────────────────────────────────────┘

B5.b  Professor ou suporte (sem área ainda, G4)   B5.c  Conta sem papel (RF-08)
┌──────────────────────────────────────────────┐  ┌──────────────────────────────────────────┐
│ ▣ Início │ INÍCIO                            │  │ ▣ Início │ INÍCIO                        │
│          │ Olá, Rafael                  (H2) │  │          │                               │
│          │ Seus papéis: (Professor)          │  │          │ ┌─ EmptyState ──────────────┐ │
│          │                                   │  │          │ │      ( 🔒 ) StatusTile    │ │
│          │ ┌─ EmptyState ──────────────────┐ │  │          │ │ Você ainda não tem acesso │ │
│          │ │ ┌─ CodeWindow ───────── ● ● ●┐│ │  │          │ │ Sua conta existe, mas não │ │
│          │ │ │ // autoria.ler  ✓          ││ │  │          │ │ tem papel na equipe agora.│ │
│          │ │ │ telas.length === 0         ││ │  │          │ │ Procure um administrador. │ │
│          │ │ └────────────────────────────┘│ │  │          │ │                           │ │
│          │ │ Suas ferramentas chegam em    │ │  │          │ │ [ Sair ]  (outline)       │ │
│          │ │ breve                    (H4) │ │  │          │ └───────────────────────────┘ │
│          │ │ Seu acesso está pronto. As    │ │  │          │                               │
│          │ │ telas do seu papel aparecem   │ │  │          │ Sidebar só com "Início".      │
│          │ │ aqui quando forem liberadas.  │ │  └──────────────────────────────────────────┘
│          │ └───────────────────────────────┘ │
└──────────────────────────────────────────────┘
Financeiro: igual a B5.a, com o card "Financeiro" no lugar de "Acessos".
Vários papéis (ex.: professor + financeiro): card Financeiro + EmptyState menor para o que falta.
Carregando: Skeleton no formato dos cards.
```

### B6 · Acessos — `/admin/acessos` (só `acesso.gerir`)

Duas vistas do mesmo contexto → `Tabs`. Dados tabulares → `Table`. Ações por linha em `⋯`
(`DropdownMenu`). A própria linha não tem ações (RF-12, `isSelf`).

```
B6.a  Pessoas
┌──────────────────────┬─────────────────────────────────────────────────────────────────────┐
│ [</>] Code4Coders    │                                              ( MC ) Marina Costa ▾  │
│       Backoffice     ├─────────────────────────────────────────────────────────────────────┤
│ ▣ Início             │  ACESSOS                                                            │
│ ⚿ Acessos  ◀ ativo   │  Equipe e papéis                                  [ + Convidar ]    │
│                      │  Quem opera a escola e o que cada pessoa pode fazer.                │
│                      │                                                                     │
│                      │  [ Pessoas  6 ]  [ Convites pendentes  2 ]            (Tabs)        │
│                      │  ┌──────────────────────────────────────────────────────────────┐   │
│                      │  │ Nome              E-mail                 Papéis           │   │   │
│                      │  │──────────────────────────────────────────────────────────────│   │
│                      │  │ (MC) Marina Costa marina@escola.com      (Administrador)  —  │   │
│                      │  │      (você)                                                  │   │
│                      │  │ (RS) Rafael Silva rafael@escola.com      (Professor)      ⋯  │   │
│                      │  │ (JL) Júlia Lima   julia@escola.com       (Professor)      ⋯  │   │
│                      │  │                                          (Suporte)            │   │
│                      │  │ (PA) Paulo Alves  paulo@escola.com       (Financeiro)     ⋯  │   │
│                      │  │ (CN) Carla Nunes  carla@escola.com       sem papel        ⋯  │   │
│                      │  │                                          (Badge warning)      │   │
│                      │  └──────────────────────────────────────────────────────────────┘   │
│                      │                                      ‹ 1 2 › (Pagination, 50/pág.) │
└──────────────────────┴─────────────────────────────────────────────────────────────────────┘

Menu ⋯ da linha (DropdownMenu)
┌──────────────────────────────┐
│ ＋ Conceder papel            │ → B8  (some se a pessoa já tem os 4)
│ ⇄ Trocar papel               │ → B10 (some se a pessoa não tem papel)
│ ──────────────────────────── │
│ ⊖ Revogar Professor          │ → B9  (um item por papel, texto destructive)
│ ⊖ Revogar Suporte            │
└──────────────────────────────┘

B6.b  Convites pendentes
│  [ Pessoas  6 ]  [ Convites pendentes  2 ]                                               │
│  ┌──────────────────────────────────────────────────────────────────────────────────┐    │
│  │ E-mail                 Papel ofertado   Convidado em   Vale até               │  │    │
│  │──────────────────────────────────────────────────────────────────────────────────│    │
│  │ ana@escola.com         (Professor)      25/09 10:12    02/10 10:12            ⋯  │    │
│  │ bruno@escola.com       (Suporte)        24/09 16:40    01/10 16:40            ⋯  │    │
│  └──────────────────────────────────────────────────────────────────────────────────┘    │
│  ⋯ → "Enviar novo convite" → B7 com e-mail e papel preenchidos (G7).                     │

Estados:
- Vazio (pessoas): impossível (o administrador sempre está lá).
- Vazio (convites): EmptyState "Nenhum convite pendente" + [ Convidar ].
- Carregando: Skeleton de 5 linhas da Table.
- Erro: Alert destructive "Não conseguimos carregar a equipe agora." + [ Tentar de novo ].
- Mobile 390: Table vira lista de Cards (nome, e-mail, badges, ⋯); [ + Convidar ] vai para
  o topo em largura total.
```

### B7 · Convidar — Dialog sobre B6

Três campos → `Dialog` (Components.md §5). Papel com 4 opções → `RadioGroup`.

```
B7.a  Formulário                                  B7.b  Recusas (DP-05: o motivo é dito)
┌─ Dialog (máx. 520) ─────────────────────── ✕ ┐  ┌─ Dialog ──────────────────────────────── ✕ ┐
│ Convidar para a equipe                  (H4) │  │ Convidar para a equipe                    │
│ A pessoa recebe um link por e-mail e define  │  │                                           │
│ a própria senha. O convite vale 7 dias.      │  │ E-mail                                    │
│                                              │  │ [ rafael@escola.com                     ] │
│ E-mail                                       │  │ ⚠ Esse e-mail já tem conta na equipe.     │
│ [ ana@escola.com                           ] │  │   Conceda o papel pela lista de pessoas.  │
│                                              │  │   _Ver Rafael Silva_                      │
│ Papel                                        │  │                                           │
│ (•) Professor   — autoria de cursos          │  │ ─ ou ─                                    │
│ ( ) Suporte     — atendimento a alunos       │  │                                           │
│ ( ) Financeiro  — dados financeiros          │  │ ⚠ Esse e-mail é de uma conta de aluno.    │
│ ( ) Administrador — gestão de acessos        │  │   Use o endereço institucional da pessoa. │
│                                              │  │                                           │
│ Motivo                                       │  │ ─ ou ─ (motivo vazio / só espaços)        │
│ [ Novo professor do curso de React         ] │  │                                           │
│ [                                          ] │  │ Motivo                                    │
│ Fica registrado na auditoria. Não cite       │  │ [                                       ] │
│ dados pessoais de outras pessoas.   0/500    │  │ ⚠ Conte por que está convidando.          │
│                                              │  └───────────────────────────────────────────┘
│              [ Cancelar ]  [ Enviar convite ]│  Erros no campo (FormMessage), com foco nele
└──────────────────────────────────────────────┘  e anúncio para leitor de tela.
Enviando: "Enviando…"; o Dialog não fecha em erro.

B7.c  Enviado (G7)
┌─ Dialog ─────────────────────────────────── ✕ ┐
│        ( ✉ )  StatusTile success             │
│ Convite enviado                         (H4) │
│ ana@escola.com recebeu o convite como        │
│ (Professor). Ele vale até 02/10/2026 às      │
│ 14:30.                                       │
│                                              │
│ ( i ) Havia outro convite para esse e-mail;  │ ← só quando supersededInvitationId ≠ null
│       ele deixou de valer.                   │
│                                              │
│              [ Convidar outra ]  [ Concluir ]│
└──────────────────────────────────────────────┘
Concluir → B6.b com o convite na lista.
```

### B8 · Conceder papel — Dialog

```
┌─ Dialog (máx. 520) ─────────────────────── ✕ ┐
│ Conceder papel a Rafael Silva           (H4) │
│ Papéis atuais: (Professor)                   │
│                                              │
│ Novo papel                                   │
│ ( ) Suporte                                  │  ← só papéis que ele ainda não tem
│ ( ) Financeiro                               │
│ ( ) Administrador                            │
│                                              │
│ Motivo                                       │
│ [                                          ] │
│ Fica registrado na auditoria. Não cite       │
│ dados pessoais de outras pessoas.            │
│                                              │
│ ( i ) Vale na próxima ação dele, sem         │
│       desconectar.                           │
│                                              │
│              [ Cancelar ]  [ Conceder papel ]│
└──────────────────────────────────────────────┘
Sucesso → fecha, toast "Suporte concedido a Rafael Silva.", linha atualizada.
changed:false (concorrência) → toast neutro "Rafael Silva já tinha esse papel. Nada mudou."
```

### B9 · Revogar papel — AlertDialog (ação destrutiva)

```
B9.a  Revogar um de vários papéis                 B9.b  Revogar o último papel (RF-08)
┌─ AlertDialog ──────────────────────────────┐    ┌─ AlertDialog ──────────────────────────────┐
│ Revogar Suporte de Júlia Lima?        (H4) │    │ Revogar Financeiro de Paulo Alves?    (H4) │
│                                            │    │                                            │
│ ( ! ) Júlia será desconectada agora.       │    │ ( ! ) Paulo será desconectado agora e      │
│       Ela continua com (Professor).        │    │       ficará sem acesso a nenhuma área.    │
│       (Alert warning)                      │    │       Para devolver acesso, conceda um     │
│                                            │    │       papel depois.                        │
│ Motivo                                     │    │                                            │
│ [                                        ] │    │ Motivo                                     │
│ Fica registrado na auditoria. Não cite     │    │ [                                        ] │
│ dados pessoais de outras pessoas.          │    │ Fica registrado na auditoria…              │
│                                            │    │                                            │
│          [ Cancelar ]  [ Revogar e         │    │          [ Cancelar ]  [ Revogar e         │
│                          desconectar ]     │    │                          desconectar ]     │
│                          (destructive)     │    │                          (destructive)     │
└────────────────────────────────────────────┘    └────────────────────────────────────────────┘
Sucesso → toast "Suporte revogado. Júlia foi desconectada."; linha atualizada (B9.b vira "sem papel").
```

### B10 · Trocar papel — Dialog

Uma ação, um motivo, dois atos na trilha (RF-09). "Para" exclui papéis que a pessoa já tem (G8).

```
┌─ Dialog (máx. 520) ─────────────────────── ✕ ┐
│ Trocar papel de Júlia Lima              (H4) │
│                                              │
│ De                                           │
│ (•) Professor                                │  ← papéis atuais (radio só se houver mais de um;
│ ( ) Suporte                                  │     com um só, vira texto fixo)
│                                              │
│ Para                                         │
│ ( ) Financeiro                               │  ← só papéis que ela não tem
│ (•) Administrador                            │
│                                              │
│ Resultado: (Administrador) (Suporte)         │  ← prévia ao vivo
│                                              │
│ Motivo                                       │
│ [                                          ] │
│ Fica registrado na auditoria. Não cite       │
│ dados pessoais de outras pessoas.            │
│                                              │
│ ( ! ) Júlia será desconectada agora.         │
│                                              │
│              [ Cancelar ]  [ Trocar papel ]  │
└──────────────────────────────────────────────┘
Sucesso → toast "Papel trocado. Júlia foi desconectada."
Falha → Alert destructive no Dialog: "Nada mudou. Tente de novo." (a troca é atômica).
```

### B11 · Financeiro — `/admin/financeiro` (só `financeiro.ler`)

```
┌──────────────────────┬─────────────────────────────────────────────────────────────────────┐
│ [</>] Code4Coders    │                                               ( PA ) Paulo Alves ▾  │
│       Backoffice     ├─────────────────────────────────────────────────────────────────────┤
│ ▣ Início             │  FINANCEIRO                                                         │
│ $ Financeiro ◀ ativo │  Financeiro                                                   (H2)  │
│                      │                                                                     │
│                      │  ┌─ EmptyState ───────────────────────────────────────────────────┐ │
│                      │  │ ┌─ CodeWindow ─────────────────────────────── ● ● ● ┐          │ │
│                      │  │ │ GET /finance-area  →  200 { "status": "reserved" } │          │ │
│                      │  │ └────────────────────────────────────────────────────┘          │ │
│                      │  │ Área reservada                                             (H4) │ │
│                      │  │ Seu acesso está liberado. Vendas, pedidos e repasses aparecem   │ │
│                      │  │ aqui quando o módulo financeiro chegar.                         │ │
│                      │  └─────────────────────────────────────────────────────────────────┘ │
└──────────────────────┴─────────────────────────────────────────────────────────────────────┘
```

### B12 · Sem permissão — link direto a área protegida (403 `PERMISSION_DENIED`)

```
┌──────────────────────┬─────────────────────────────────────────────────────────────────────┐
│ ▣ Início             │  ┌─ EmptyState ───────────────────────────────────┐                 │
│                      │  │ ┌─ CodeWindow ──────────────────── ● ● ● ┐     │                 │
│                      │  │ │ $ GET /admin/financeiro                 │     │                 │
│                      │  │ │ 403 Forbidden                           │     │                 │
│                      │  │ └─────────────────────────────────────────┘     │                 │
│                      │  │ Esta área não é do seu papel              (H3)  │                 │
│                      │  │ Se você precisa dela, peça a um administrador.  │                 │
│                      │  │ [ Voltar para o início ]                        │                 │
│                      │  └─────────────────────────────────────────────────┘                 │
└──────────────────────┴─────────────────────────────────────────────────────────────────────┘
O item de menu não existe para essa pessoa; esta tela só aparece por link direto.
```

### B13 · Erro — qualquer rota

Mesmo desenho do T8 aprovado ("Essa página não existe" / "Algo deu errado aqui"), dentro do
AuthLayout do backoffice se deslogado ou do AppShell do backoffice se logado. Sem desenho novo.

---

## 5. E-mail de convite (RF-04)

Mesmas restrições de E1/E2: legível sem imagens, link visível como texto, validade real, 600px,
tokens light fixos. O e-mail de redefinição do backoffice reusa **E2** sem mudança (G9).

```
E3  Convite para a equipe
┌──────────────────────────────────────┐
│ </> Code4Coders · Backoffice         │
│──────────────────────────────────────│
│ Olá!                                 │  ← sem nome: o convidado ainda não tem conta
│                                      │
│ Você foi convidado para a equipe da  │
│ Code4Coders como **Professor**.      │
│                                      │
│ [        Aceitar convite           ] │
│                                      │
│ Ou copie este link no navegador:     │
│ https://code4coders…/admin/convite   │
│ ?token=…                             │
│                                      │
│ ⏱ O convite vale até 02/10/2026 às   │
│   14:30 e só funciona uma vez.       │
│                                      │
│ Use este mesmo endereço para entrar. │
│ Não esperava este convite? Ignore    │
│ este e-mail.                         │
│──────────────────────────────────────│
│ Code4Coders · e-mail automático      │
└──────────────────────────────────────┘
```

---

## 6. Plano para o Figma (após aprovação deste ASCII)

Páginas novas no arquivo `Code4Coders — Design System`, sem mexer nas existentes:

1. **🧭 Fluxo — Acesso interno**: o fluxo da seção 2, com os frames das telas como nós.
2. **📱 Screens — Acesso interno**: B1–B12, todos os estados; públicas (B1–B4) em **desktop 1440 +
   mobile 390**; logadas em desktop 1440, com mobile 390 só para B5.a e B6.a. Tema Light; Dark só em
   B1.a e B6.a.
3. **✉ E-mail E3** a 600px, na mesma seção de E1/E2.
4. **Components (proposta)**: `RoleBadge`, `ReasonField`, `EmptyState`, variante "Backoffice" do
   `Auth Brand Panel` e da marca; `Table` e `Dialog` do DS se ainda não existirem no arquivo.

Tudo reusando os componentes já aprovados na conta do aluno (Form Field, Password Field, Alert,
Status Tile, Toast, Account Menu, Skeleton) e as variáveis/text styles do DS, sem valor hardcoded.

---

## 7. Decisões para você aprovar

1. **Entrada distinta por marca, não por layout** (G1): mesmo AuthLayout do aluno, com badge
   "Backoffice", overline e `CodeWindow` próprios.
2. **Mensagem única de sessão encerrada** (G2), sem distinguir revogação de inatividade.
3. **Menu de conta sem e-mail e sem "Trocar senha"** (G3; o PRD não entrega troca de senha interna).
4. **Início por permissão** (B5), com estado honesto para professor/suporte e estado "sem acesso"
   para conta sem papel; sem itens de menu desabilitados.
5. **Acessos com `Tabs` + `Table` + ações em `⋯`**; revogação é um item por papel.
6. **"Enviar novo convite" na linha pendente** (G7), reaproveitando o DP-04 — não é o "reenviar o
   mesmo convite" que o PRD tira do escopo, é um convite novo pré-preenchido.
7. **Campo "Para" da troca sem papéis já presentes** (G8).
8. **E-mail do seed (G9) fica fora**: reusa E2 como a TechSpec define; registrar como observação para
   Notificação.
9. **Componentes novos:** `RoleBadge`, `ReasonField`, `EmptyState`.

---

## 8. Figma (aprovado em 2026-09-25)

Base: `https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System`

| Entrega | Página | node-id |
|---|---|---|
| Fluxo do usuário | Fluxo — Acesso interno | `99-3` |
| Componentes novos (proposta) | Screens — Acesso interno | `66-3` |
| B1 · Entrar | Screens — Acesso interno | `70-53` |
| B2 · Recuperar senha | Screens — Acesso interno | `71-264` |
| B3 · Defina sua senha | Screens — Acesso interno | `71-450` |
| B4 · Aceitar convite | Screens — Acesso interno | `71-1092` |
| B5 · Início (por papel) | Screens — Acesso interno | `75-1059` |
| B6 · Acessos | Screens — Acesso interno | `81-1246` |
| B7 · Convidar | Screens — Acesso interno | `85-1681` |
| B8 · Conceder papel | Screens — Acesso interno | `88-1997` |
| B9 · Revogar papel | Screens — Acesso interno | `88-2107` |
| B10 · Trocar papel | Screens — Acesso interno | `89-2473` |
| B11 · Financeiro | Screens — Acesso interno | `90-2757` |
| B12 · Sem permissão | Screens — Acesso interno | `90-2841` |
| E3 · E-mail de convite | Screens — Acesso interno | `90-5024` |
| Dark mode — validação | Screens — Acesso interno | `95-2935` |

B13 (Erro) não tem frame próprio: reusa T8 da Conta do aluno.

Componentes propostos (migram para a página Components após aprovação): Icon (12 lucide novos: users,
wallet, lock, ellipsis, plus, user-plus, arrow-left-right, circle-minus, x, send, shield-check, clock),
Brand Logo · Backoffice, Auth Brand Panel · Backoffice, Role Badge, Radio Option, Reason Field,
Empty State, Invitation Summary, Sidebar Nav Item, Account Menu · Backoffice, Area Card, Staff Row,
Invite Row, Staff Card (mobile), Dialog Header.
