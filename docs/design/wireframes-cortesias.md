# Wireframes ASCII — Cortesias do backoffice (CAP-008, 1º PRD)

> **Status:** ASCII aprovado pelo responsável em 2026-10-01 (aprovação explícita "Aprovar o ASCII") · implementação não iniciada
> **Handoff:** aprovado o ASCII → desenho no Figma (task 2.0) → aprovação → código de tela (tasks 3.0 em diante). Pedido de ajuste volta a este documento antes de qualquer registro.
> **Objetivo:** definir fluxos, conteúdo e estados das telas da área **Cortesias** (`admin-spa`) e do reflexo da cortesia na trilha de auditoria antes do desenho no Figma.
> **Fontes:** [PRD](../../tasks/prd-concessao-acesso/prd.md) v1.1 (RF-01…RF-05, RF-08, Experiência do Usuário, DP-01…DP-07), [TechSpec](../../tasks/prd-concessao-acesso/techspec.md) v1.0 (V-01…V-07, Habilitadores inevitáveis, D-01, D-03), [contrato do backoffice](../../tasks/prd-concessao-acesso/api-contract.md) 1.0.0 ([YAML](../../tasks/prd-concessao-acesso/api-contract.yaml)), [wireframes do Catálogo](wireframes-catalogo-vitrine.md) (AppShell, formato do documento e do registro de aprovação), [wireframes de Auditoria](wireframes-auditoria.md) (trilha, `ComplianceBadge`, `EmptyState`, `CodeWindow`), [componentes](Components.md) e [Design System](../../DESIGN.md).

---

## 1. Inventário

### 1.1 O que o PRD entrega em tela

| Entrega | RF | Tela ou efeito visível |
|---|---|---|
| Permissão `cortesia.conceder` | RF-01 | Item **Cortesias** no menu só para quem a tem; B12 por link direto |
| Localizar o aluno beneficiário | RF-02 | T1 passo *Aluno* (e-mail, nome, concessões do aluno) |
| Escolher o curso | RF-03 | T2 passo *Curso* (busca por título, lista paginada) |
| Vigência com prévia do término | RF-03, RF-05 | T3 passo *Vigência* (período 1–60 ou vitalícia, prévia do servidor) |
| Motivo obrigatório | RF-03 | T4 passo *Motivo* (texto, contador de 500) |
| Revisão em uma frase + avisos | RF-03 | T5 passo *Revisão* (resumo, aviso de duplicidade, reforço da vitalícia) |
| Resultado da concessão | RF-03 | T6 passo *Resultado* (concessão criada, erros por `code`) |
| Rótulos da cortesia na trilha | RF-08 | T7 lista, filtro e detalhe da consulta existente (CAP-030) |

### 1.2 Telas (`admin-spa`, base `/admin/`; origem `http://localhost:8081` no Compose)

| # | Tela | Rota | Permissão | Estados principais |
|---|---|---|---|---|
| T0 | Cortesias (entrada do formulário) | `/admin/cortesias` (rota do SPA: `/cortesias`) | `cortesia.conceder` | formulário sequencial · (B12 sem permissão) |
| T1 | Passo 1 — Aluno | dentro de T0 | `cortesia.conceder` | vazio · localizado · e-mail não confirmado · desativada · não encontrada · carregando · erro |
| T2 | Passo 2 — Curso | dentro de T0 | `cortesia.conceder` | com cursos · filtrado · vazio · carregando · erro |
| T3 | Passo 3 — Vigência | dentro de T0 | `cortesia.conceder` | por período · vitalícia · prévia · campo inválido · prévia indisponível |
| T4 | Passo 4 — Motivo | dentro de T0 | `cortesia.conceder` | escrevendo · limite · vazio · erro de confirmação |
| T5 | Passo 5 — Revisão | dentro de T0 | `cortesia.conceder` | resumo · aviso de acesso existente · reforço da vitalícia · confirmando · erro |
| T6 | Passo 6 — Resultado | dentro de T0 | `cortesia.conceder` | sucesso · erro por `code` · erro de serviço |
| T7 | Trilha de auditoria — cortesia | telas existentes `/admin/auditoria` e `/admin/auditoria/{recordId}` | papel `administrador` | rótulo resolvido · filtro · detalhe sem e-mail |
| B12/B13/B1 | Sem permissão · erro geral · sessão encerrada | rotas protegidas | — | reusam os frames de Acesso interno |

### 1.3 Decisões de desenho propostas (para aprovação)

| # | Ponto | Proposta e fundamento |
|---|---|---|
| G1 | Nome, lugar e rota | Item **Cortesias** no grupo **Financeiro** do menu, abaixo de Financeiro, ícone `gift` (lucide). Rota do SPA `/cortesias` (cheia `/admin/cortesias`). Visível só com `cortesia.conceder`. O administrador não herda (DP-03 de CAP-002). Sem item desabilitado para os demais papéis: quem não tem a permissão nem vê o item; link direto cai em B12 |
| G2 | Formulário sequencial em seis passos | `Stepper` horizontal em desktop (Aluno · Curso · Vigência · Motivo · Revisão · Resultado), vertical compacto em mobile. Passos concluídos ficam resumidos e editáveis (volta sem perder o restante); o estado de cada passo é mantido até confirmar. Barra de progresso com `aria-current="step"` |
| G3 | Passo Aluno: identificação | Campo único **E-mail do aluno** + `[ Localizar ]`. Resultado mostra **e-mail e nome da conta** e a lista de concessões (T1). Nada além de e-mail e nome é mostrado; e-mail nunca vai para a URL (RN-21) |
| G4 | Passo Aluno: mesmos textos do PRD | Conta inexistente, de ator interno ou de outra escola: um texto só — "Não há conta de aluno com este e-mail" — sem distinguir. Desativada impede: "Esta conta está desativada e não pode receber cortesia." Não confirmada avisa sem impedir: "E-mail ainda não confirmado. Você pode conceder; o aluno usa o acesso depois de confirmar e entrar." Serviço indisponível: `Alert destructive` + `[ Tentar de novo ]` |
| G5 | Lista de concessões do aluno | Tabela (curso, origem, vigência em data absoluta, situação **Ativa**/**Vencida**) dentro do passo Aluno, 10 por página. Vazia: "Nenhuma concessão para este aluno ainda." Situação é texto + `Badge`, nunca só cor |
| G6 | Passo Curso: escolha | Busca por título (`Search` com `legend` "Buscar curso pelo título") + lista paginada de cursos **com versão vigente, com ou sem oferta** (DP-05). Linha: título + "Com oferta" / "Sem oferta" + `[ Escolher ]`. Vazio: "Nenhum curso publicado com este título." |
| G7 | Passo Vigência: opções | `RadioGroup` com duas opções — **Por período** e **Vitalícia** (precedente G11 do Catálogo). "Por período" revela **Meses** (inteiro 1–60). Erro: "Informe de 1 a 60 meses inteiros." A tela **nunca calcula** o término (D-03): mostra a **prévia do servidor** (`previewCourtesyTerm`) como "até DD/MM/AAAA". Vitalícia: texto fixo "Acesso vitalício" |
| G8 | Passo Motivo | `Textarea` + contador `0/500` + ajuda fixa "Não inclua dado pessoal de terceiros." (RN-A08). Vazio ou só espaços: "Informe o motivo da cortesia." Acima de 500: "Use até 500 caracteres: tire N." Texto digitado é preservado no erro |
| G9 | Passo Revisão: resumo em uma frase | Texto único legível como uma frase: "Conceder a [e-mail] acesso a [curso] até [data] — motivo: …" (vitalícia: "…acesso vitalício — motivo: …"). Abaixo, o motivo por extenso. A frase é o `aria-label` do resumo |
| G10 | Aviso de acesso existente | Se o aluno já tem acesso ativo ao curso: `Alert warning` permanente — "Este aluno já tem acesso até DD/MM/AAAA." — **sem impedir** (DP-06). Duas concessões convivem; a nova não estende nem encerra a outra |
| G11 | Confirmação reforçada da vitalícia | Só na vitalícia: `Checkbox` obrigatória + frase fixa "Esta cortesia vitalícia **não há como desfazer pela tela**." (DP-07) + botão `[ Confirmar cortesia vitalícia ]`. Focável e anunciada (`role="alertdialog"` quando em dialog, ou região com `aria-live="assertive"`) |
| G12 | Resultado: sucesso | Card da concessão criada: curso, vigência ("6 meses · até DD/MM/AAAA" ou "Vitalícia · Acesso vitalício"), término, motivo + `[ Conceder outra cortesia ]` e `_Ver concessões do aluno_`. Toast "Cortesia concedida." |
| G13 | Resultado: erros por `code` | Texto por código do contrato, com o digitado preservado: motivo/curso/vigência inválidos (`FIELD_INVALID` → mensagem no campo de origem); conta/curso não elegíveis (`STUDENT_ACCOUNT_NOT_ELIGIBLE` / `COURSE_NOT_ELIGIBLE` → "Não foi possível conceder: verifique o aluno e o curso." + volta ao passo); chave reutilizada com corpo diferente (`IDEMPOTENCY_KEY_REUSED` → "Esta confirmação já foi usada de outro jeito. Revise e confirme de novo."); serviço indisponível / reconfirmação falhou (502/504, `STUDENT_ACCOUNT_CHECK_UNAVAILABLE` → `Alert destructive` "Não foi possível conceder agora." + `[ Tentar de novo ]` com a mesma chave) |
| G14 | Trilha (T7) | Tipo novo entra no filtro e nos rótulos: **Cortesia concedida**. Detalhe mostra aluno **pelo nome**, curso **pelo título**, vigência formatada ("6 meses" ou "Vitalícia" + "até DD/MM/AAAA") e motivo. **E-mail do aluno nunca aparece**, nem no detalhe |
| G15 | Início do backoffice | Sem card novo no Início nesta entrega; a entrada é o menu |
| G16 | Reenvio | Duplo clique e nova tentativa usam a mesma `Idempotency-Key`: geram **uma** concessão e **um** ato. Botão de confirmação desabilita com spinner ("Concedendo…") até a resposta |

---

## 2. Fluxos do usuário

### 2.1 Financeiro: conceder cortesia

```text
  Menu Financeiro → Cortesias (/admin/cortesias)
          │ cortesia.conceder
          ▼
  T1 Aluno: [e-mail] → [ Localizar ]
     │ encontrado ──▶ mostra e-mail + nome + concessões (vazia quando não há)
     │ não confirmado ──▶ aviso, pode seguir
     │ desativada ──▶ impede
     │ não há conta (inexistente / ator interno / outra escola) ──▶ mesmo texto, impede
     │ 502/504 ──▶ Alert + [ Tentar de novo ]
          ▼ [ Continuar ]
  T2 Curso: busca por título → lista paginada (com ou sem oferta) → [ Escolher ]
     │ vazio ──▶ "Nenhum curso publicado com este título."
          ▼ [ Continuar ]
  T3 Vigência: ( • ) Por período [ meses 1–60 ] + prévia "até DD/MM/AAAA" (servidor)
               ( ) Vitalícia → "Acesso vitalício"
          ▼ [ Continuar ]
  T4 Motivo: [ texto 0/500 ] + "Não inclua dado pessoal de terceiros."
          ▼ [ Revisar ]
  T5 Revisão: uma frase ("Conceder a [e-mail] acesso a [curso] até [data] — motivo: …")
              + aviso "este aluno já tem acesso até DD/MM/AAAA" (sem impedir)
              + vitalícia: confirmação reforçada ("não há como desfazer pela tela")
          ▼ [ Confirmar cortesia ] / [ Confirmar cortesia vitalícia ]
  T6 Resultado: concessão criada (curso, vigência, término) ─ ou ─ erro por code
     └─ [ Conceder outra cortesia ]  ·  _Ver concessões do aluno_ (volta a T1 com o aluno)

  Sem cortesia.conceder por link direto → B12 · sessão revogada → B1 · erro 502/504 → Alert com [ Tentar de novo ]
  Passos mantêm estado até confirmar; voltar a um passo não apaga os demais.
```

### 2.2 Administrador: cortesia na trilha

```text
  /admin/auditoria ─ filtro Tipo ─ Cortesia concedida
        │ linha (Aluno: nome · Curso: título · Vigência: "6 meses" / "Vitalícia")
        ▼
  /admin/auditoria/{recordId} ─ Autor · Aluno (nome) · Curso (título) · Vigência · Motivo
                                (sem e-mail do aluno em nenhum ponto)
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
│ COMERCIAL            │   << T0…T6 >>                                                        │
│ 🏷 Catálogo          │                                                                      │
│ FINANCEIRO           │                                                                      │
│ 🎁 Cortesias ◀ ativo │                                                                      │
│ ▤ Financeiro         │                                                                      │
└──────────────────────┴──────────────────────────────────────────────────────────────────────┘
```

### 3.2 Componentes

Reuso: `Sidebar`, `Breadcrumb`, `Card`, `Badge`, `Table`, `Pagination`, `Stepper`, `Form`/`FormField`/`Label`/`Input`/`Textarea`, `RadioGroup`, `Checkbox`, `Button`, `Alert`, `Skeleton`, `Dialog`, `AlertDialog`, `Sheet`, `EmptyState`, `CodeWindow`, `Sonner`, `Tooltip`. Ícones `lucide-react` com nome acessível nos botões.

**Composições propostas para o Figma:**
- `StudentAccountCard` = e-mail + nome da conta + situação (ativa / desativada / e-mail não confirmado).
- `AccessGrantRow` = linha da concessão do aluno: curso, origem, vigência absoluta, situação (Ativa/Vencida).
- `CoursePickRow` = linha do curso elegível: título + "Com oferta"/"Sem oferta" + `[ Escolher ]`.
- `TermPreview` = texto da prévia do servidor ("até DD/MM/AAAA") ou "Acesso vitalício".
- `ReviewSentence` = resumo de uma frase da revisão (com `aria-label` integral).
- `LifetimeConfirm` = checkbox + frase de reforço ("não há como desfazer pela tela").
- `CourtesyResultCard` = concessão criada (curso, vigência, término, motivo).

**Legenda dos desenhos:** `[ Ação ]` botão · `_link_` navegação · `( Estado )` badge · `[____]` campo · `( • )` rádio marcado · `( )` não marcado · `[✓]` checkbox marcado · `[ ]` desmarcado · `(!)` aviso · `( i )` informação.

---

## 4. Wireframes — Cortesias

### T0 · Entrada — `/admin/cortesias` (rota do SPA: `/cortesias`)

```text
T0.a  Formulário sequencial (desktop 1440)
┌──────────────────────┬──────────────────────────────────────────────────────────────────────┐
│ FINANCEIRO           │  CORTESIAS                                                           │
│ 🎁 Cortesias ◀       │  Conceda acesso a um curso sem compra, com motivo.                   │
│ ▤ Financeiro         │                                                                      │
│                      │  ●───○───○───○───○───○  Aluno · Curso · Vigência · Motivo · Revisão   │
│                      │  (Stepper; passo atual com aria-current="step")                       │
│                      │                                                                      │
│                      │  ┌─ Card do passo ───────────────────────────────────────────────┐   │
│                      │  │ << T1 … T6 >>                                                 │   │
│                      │  └───────────────────────────────────────────────────────────────┘   │
│                      │                                    [ Voltar ]  [ Continuar ]          │
└──────────────────────┴──────────────────────────────────────────────────────────────────────┘
Passos concluídos mostram resumo de uma linha + _Editar_. [ Continuar ] só habilita com o passo válido.
Resultado (T6) esconde o Stepper e mostra o desfecho.

T0.b  Sem cortesia.conceder (link direto): B12 "Você não tem acesso a esta área." — o item do menu nem aparece.
T0.c  Carregando a área: Skeleton do Card do passo. Erro: Alert destructive + [ Tentar de novo ].
```

```text
T0.d  Mobile 390
┌──────────────────────────────────┐
│ ☰  Cortesias                      │
│ Passo 1 de 6 · Aluno               │
│ ●───○───○───○───○───○              │
│ ┌──────────────────────────────┐   │
│ │ << passo atual >>            │   │
│ └──────────────────────────────┘   │
│ [ Continuar ]                      │
│ [ Voltar ]                         │
└──────────────────────────────────┘
```

### T1 · Passo 1 — Aluno

```text
T1.a  Localizar (desktop)
┌─ Card ──────────────────────────────────────────────────────────────┐
│ ALUNO                                                        1 de 6 │
│ Informe o e-mail da conta de aluno.                                  │
│                                                                      │
│ E-mail do aluno                                                      │
│ [ joana.ribeiro@example.com                                     ]    │
│                                        [ Localizar ]                 │
└──────────────────────────────────────────────────────────────────────┘
E-mail com maiúsculas/espaços nas bordas é normalizado (encontra a conta).
A busca é POST de leitura: o e-mail nunca aparece na URL, em log ou métrica.

T1.b  Localizado (conta ativa, e-mail confirmado)
│ ALUNO                                                        1 de 6 │
│ ┌─ StudentAccountCard ────────────────────────────────────────────┐ │
│ │ joana.ribeiro@example.com · Joana Ribeiro                       │ │
│ └─────────────────────────────────────────────────────────────────┘ │
│ CONCESSÕES DESTA CONTA                                    10/página │
│ │ Curso              Origem    Vigência              Situação      │ │
│ │ .NET do zero à API Cortesia  6 meses · até 15/09/2027 (Ativa)   │ │
│ │ Testes na prática  Cortesia  Vitalícia             (Ativa)      │ │
│ │ Fundamentos de C#  Cortesia  3 meses · até 02/02/2026 (Vencida) │ │
│ Vigência sempre em data absoluta.                        ‹ 1 2 ›    │
│                                              [ Voltar ]  [ Continuar ]│

T1.c  E-mail ainda não confirmado (avisa, não impede)
│ ┌─ StudentAccountCard ────────────────────────────────────────────┐ │
│ │ joana.ribeiro@example.com · Joana Ribeiro                       │ │
│ │ ( i ) E-mail ainda não confirmado. Você pode conceder; o aluno  │ │
│ │       usa o acesso depois de confirmar e entrar.   (Alert info)  │ │
│ └─────────────────────────────────────────────────────────────────┘ │

T1.d  Conta desativada (impede)
│ ( ! ) Esta conta está desativada e não pode receber cortesia.       │
│                                        (Alert warning; sem [ Continuar ])│

T1.e  Não encontrada (texto único para inexistente, ator interno e outra escola)
│ Não há conta de aluno com este e-mail.                              │
│ Confira o e-mail e tente de novo.                                   │
│                                        (sem [ Continuar ])          │

T1.f  Sem concessões
│ CONCESSÕES DESTA CONTA                                               │
│ Nenhuma concessão para este aluno ainda.                              │

T1.g  Carregando: Skeleton do StudentAccountCard + 3 linhas. Erro/serviço indisponível:
│ ( ! ) Não foi possível localizar agora.  [ Tentar de novo ]  (Alert destructive)│
```

```text
T1.h  Mobile 390
┌──────────────────────────────────┐
│ Passo 1 de 6 · Aluno               │
│ E-mail do aluno                    │
│ [ joana.ribeiro@…              ]   │
│ [ Localizar ]                      │
│ ┌──────────────────────────────┐   │
│ │ Joana Ribeiro                │   │
│ │ joana.ribeiro@example.com    │   │
│ │ ( i ) E-mail ainda não        │   │
│ │ confirmado.                  │   │
│ └──────────────────────────────┘   │
│ CONCESSÕES                         │
│ ┌──────────────────────────────┐   │
│ │ .NET do zero à API           │   │
│ │ Cortesia · 6 meses           │   │
│ │ até 15/09/2027 · (Ativa)     │   │
│ └──────────────────────────────┘   │
│ [ Continuar ]                      │
└──────────────────────────────────┘
```

### T2 · Passo 2 — Curso

```text
T2.a  Escolha (desktop)
┌─ Card ──────────────────────────────────────────────────────────────┐
│ CURSO                                                        2 de 6 │
│ Escolha um curso publicado da escola. Cortesia não exige oferta.     │
│                                                                      │
│ Buscar curso pelo título                                             │
│ [ testes__________________________________________ ]  [ Buscar ]      │
│                                                                      │
│ │ Curso              Oferta        │                                   │
│ │ .NET do zero à API Com oferta   │  [ Escolher ]                     │
│ │ Testes na prática  Sem oferta   │  [ Escolher ]                     │
│ │ Fundamentos de C#  Com oferta   │  [ Escolher ]                     │
│ Só cursos com versão vigente.                         ‹ 1 2 ›         │
│                                              [ Voltar ]  [ Continuar ]│
Escolhido: linha com destaque + "Escolhido ✓". [ Continuar ] exige escolha.

T2.b  Filtrado: mantém o termo; "3 cursos com este título".
T2.c  Vazio: "Nenhum curso publicado com este título." + [ Ver todos ]
T2.d  Carregando: Skeleton de 5 linhas. Erro: Alert destructive + [ Tentar de novo ].
```

```text
T2.e  Mobile 390
┌──────────────────────────────────┐
│ Passo 2 de 6 · Curso               │
│ [ testes___________ ] [ Buscar ]   │
│ ┌──────────────────────────────┐   │
│ │ .NET do zero à API           │   │
│ │ Com oferta                   │   │
│ │ [ Escolher ]                 │   │
│ └──────────────────────────────┘   │
│ ┌──────────────────────────────┐   │
│ │ Testes na prática            │   │
│ │ Sem oferta · Escolhido ✓     │   │
│ └──────────────────────────────┘   │
│ [ Voltar ]  [ Continuar ]          │
└──────────────────────────────────┘
```

### T3 · Passo 3 — Vigência

```text
T3.a  Por período (desktop)
┌─ Card ──────────────────────────────────────────────────────────────┐
│ VIGÊNCIA                                                     3 de 6 │
│                                                                      │
│ Vigência do acesso                                        (RadioGroup)│
│ ( • ) Por período      ( ) Vitalícia                                 │
│ Meses  [ 6 ]   de 1 a 60 meses inteiros                              │
│                                                                      │
│ ( i ) Termina em até 15/09/2027 — vindo do servidor.                  │
│       A prévia vale se a confirmação for hoje.           (TermPreview)│
│                                                                      │
│                                              [ Voltar ]  [ Continuar ]│

T3.b  Vitalícia
│ ( ) Por período      ( • ) Vitalícia                                 │
│ Acesso vitalício, sem data de término.                               │

T3.c  Meses inválidos ("0", "61", "abc")
│ Meses  [ 61 ]                                                        │
│ (!) Informe de 1 a 60 meses inteiros.                               │

T3.d  Prévia indisponível (leitura 502/504)
│ ( ! ) Não foi possível mostrar a prévia agora. Tente de novo antes   │
│       de continuar.  [ Tentar de novo ]  (sem [ Continuar ])         │
```

```text
T3.e  Mobile 390
┌──────────────────────────────────┐
│ Passo 3 de 6 · Vigência            │
│ ( • ) Por período                  │
│ ( ) Vitalícia                      │
│ Meses  [ 6 ]                       │
│ ( i ) Termina em até 15/09/2027.   │
│ [ Voltar ]  [ Continuar ]          │
└──────────────────────────────────┘
```

### T4 · Passo 4 — Motivo

```text
T4.a  Escrevendo (desktop)
┌─ Card ──────────────────────────────────────────────────────────────┐
│ MOTIVO                                                       4 de 6 │
│ Diga por que esta cortesia existe. O motivo vai para a trilha.       │
│                                                                      │
│ Motivo                                                               │
│ [ Bolsa integral do parceiro municipal — turma 2027/1.             ] │
│ [                                                                    ] │
│ Não inclua dado pessoal de terceiros.                       46/500   │
│                                                                      │
│                                        [ Voltar ]  [ Revisar ]        │
└──────────────────────────────────────────────────────────────────────┘

T4.b  Vazio ou só espaços: "Informe o motivo da cortesia." (sem [ Revisar ]).
T4.c  Acima de 500: contador "512/500" em destructive + "Use até 500 caracteres: tire 12."
      Texto digitado é preservado.
```

```text
T4.d  Mobile 390
┌──────────────────────────────────┐
│ Passo 4 de 6 · Motivo              │
│ [ Bolsa integral…                ] │
│ Não inclua dado pessoal de         │
│ terceiros.                46/500   │
│ [ Voltar ]  [ Revisar ]            │
└──────────────────────────────────┘
```

### T5 · Passo 5 — Revisão

```text
T5.a  Resumo em uma frase (desktop)
┌─ Card ──────────────────────────────────────────────────────────────┐
│ REVISÃO                                                      5 de 6 │
│                                                                      │
│ ┌─ ReviewSentence ────────────────────────────────────────────────┐ │
│ │ Conceder a joana.ribeiro@example.com acesso a Testes na prática  │ │
│ │ até 15/09/2027 — motivo: bolsa integral do parceiro municipal.  │ │
│ └─────────────────────────────────────────────────────────────────┘ │
│ (lido pelo leitor de tela como uma frase; aria-label integral)       │
│                                                                      │
│ Aluno    Joana Ribeiro (joana.ribeiro@example.com)                   │
│ Curso    Testes na prática                                           │
│ Vigência 6 meses · até 15/09/2027                                    │
│ Motivo   Bolsa integral do parceiro municipal — turma 2027/1.        │
│                                                                      │
│                                        [ Voltar ]  [ Confirmar cortesia ]│
Confirmando: "Concedendo…" com spinner; botões desabilitados (mesma Idempotency-Key).

T5.b  Vitalícia (resumo + reforço)
│ │ Conceder a joana.ribeiro@example.com acesso vitalício a Testes na  │ │
│ │ prática — motivo: bolsa integral do parceiro municipal.           │ │
│ Vigência Acesso vitalício                                            │
│ ┌─ LifetimeConfirm ───────────────────────────────────────────────┐ │
│ │ [✓] Entendo que esta cortesia vitalícia não há como desfazer     │ │
│ │     pela tela.                                    (Alert warning)│ │
│ └─────────────────────────────────────────────────────────────────┘ │
│                              [ Voltar ]  [ Confirmar cortesia vitalícia ]│
Sem o checkbox, a confirmação fica desabilitada. O reforço é focável e anunciado.

T5.c  Aviso de acesso existente (não impede)
│ ( ! ) Este aluno já tem acesso até 15/03/2028. A nova cortesia não   │
│       estende nem encerra a outra; as duas passam a existir.        │
│                                                        (Alert warning)│

T5.d  Erro ao confirmar (rede/servidor, mesma chave)
│ ( ! ) Não foi possível conceder agora.  [ Tentar de novo ]  (Alert destructive)│
│ Campos e resumo mantidos; reenvio não duplica.                       │
```

```text
T5.e  Mobile 390
┌──────────────────────────────────┐
│ Passo 5 de 6 · Revisão             │
│ "Conceder a joana.… até            │
│ 15/09/2027 — motivo: bolsa… "      │
│ ( ! ) Este aluno já tem acesso     │
│ até 15/03/2028.                    │
│ [✓] Entendo que … não há como      │
│ desfazer pela tela. (só vitalícia) │
│ [ Confirmar cortesia ]             │
│ [ Voltar ]                         │
└──────────────────────────────────┘
```

### T6 · Passo 6 — Resultado

```text
T6.a  Sucesso (desktop)
┌─ Card ──────────────────────────────────────────────────────────────┐
│ RESULTADO                                                    6 de 6 │
│ ✓ Cortesia concedida.                                   (toast)      │
│                                                                      │
│ ┌─ CourtesyResultCard ────────────────────────────────────────────┐ │
│ │ Testes na prática                                               │ │
│ │ 6 meses · até 15/09/2027                                        │ │
│ │ Motivo: bolsa integral do parceiro municipal.                   │ │
│ └─────────────────────────────────────────────────────────────────┘ │
│                                                                      │
│ [ Conceder outra cortesia ]   _Ver concessões do aluno_              │
└──────────────────────────────────────────────────────────────────────┘
Vitalícia: "Vitalícia · Acesso vitalício" no lugar da data.

T6.b  Erros por código (nada é concedido; digitado preservado)
│ FIELD_INVALID (motivo, meses, curso): volta ao passo do campo com a mensagem │ 
│   de T3.c/T4.b/T4.c.                                                        │
│ STUDENT_ACCOUNT_NOT_ELIGIBLE / COURSE_NOT_ELIGIBLE:                        │
│ ( ! ) Não foi possível conceder: verifique o aluno e o curso.               │
│       _Voltar ao Aluno_  _Voltar ao Curso_                                  │
│ IDEMPOTENCY_KEY_REUSED (mesma chave, corpo diferente):                      │
│ ( ! ) Esta confirmação já foi usada de outro jeito. Revise e confirme       │
│       de novo.                                                              │
│ 502/504 (STUDENT_ACCOUNT_CHECK_UNAVAILABLE ou serviço indisponível):       │
│ ( ! ) Não foi possível conceder agora.  [ Tentar de novo ] (mesma chave)    │
```

```text
T6.c  Mobile 390
┌──────────────────────────────────┐
│ Passo 6 de 6 · Resultado           │
│ ✓ Cortesia concedida.              │
│ ┌──────────────────────────────┐   │
│ │ Testes na prática            │   │
│ │ 6 meses · até 15/09/2027     │   │
│ └──────────────────────────────┘   │
│ [ Conceder outra cortesia ]        │
│ _Ver concessões do aluno_          │
└──────────────────────────────────┘
```

### T7 · Trilha de auditoria — cortesia (telas existentes de CAP-030)

```text
T7.a  Filtro de tipo ganha uma opção
│ Tipo  [▾ Todos                     ]                                                     │
│       Todos · … · Cortesia concedida                                                    │

T7.b  Linha da lista
│ Momento      Tipo               Autor          Alvo                              Situação │
│ 01/10 09:20  Cortesia concedida  Marina Costa   Joana Ribeiro — Testes…         (✓ Conforme)│
│ Alvo: aluno pelo nome + curso pelo título; vigência e motivo só no detalhe.      │
│ E-mail do aluno não aparece na lista.                                            │

T7.c  Detalhe de "Cortesia concedida"
│  Cortesia concedida                                            (✓ Conforme)     │
│  ┌─ Card ORIGINAL · NÃO EDITÁVEL ──────────────────────────────────────────┐ │
│  │ Momento do ato           01/10/2026 09:20:00                            │ │
│  │ Recebido pela Auditoria  01/10/2026 09:20:03                            │ │
│  │ Origem                   Matrícula e Direito de Acesso                   │ │
│  │ Tipo                     Cortesia concedida                             │ │
│  │ Autor                    Marina Costa                                  │ │
│  │ Aluno                    Joana Ribeiro                                 │ │
│  │ Curso                    Testes na prática                             │ │
│  │ Vigência                 6 meses · até 15/09/2027                       │ │
│  │ Motivo                   ┃ Bolsa integral do parceiro municipal —      │ │
│  │                          ┃ turma 2027/1.                               │ │
│  └─────────────────────────────────────────────────────────────────────────┘ │
Vigência vitalícia: "Vitalícia · Acesso vitalício". E-mail do aluno não aparece
no detalhe. Motivo nunca é copiado para fora do detalhe.

T7.d  Mobile 390: Card em largura total, rótulo acima do valor; linha da lista vira card.
```

---

## 5. Acessibilidade

- Passos como formulário sequencial com `label` em todo campo, erro anunciado (`aria-describedby` + `aria-invalid`) e **foco no primeiro campo inválido** ao tentar continuar.
- Data do término sempre em texto ("até DD/MM/AAAA" / "Acesso vitalício"), nunca só visual.
- Confirmação reforçada da vitalícia focável e anunciada; o resumo da revisão é lido como **uma frase** (`aria-label` integral).
- Stepper com `aria-current="step"`, "Passo N de 6" em texto; toasts com `aria-live="polite"`, erros com `role="alert"`.
- Contraste e foco visível pelos tokens do DS; `prefers-reduced-motion` respeitado nos spinners.

---

## 6. Plano para o Figma (após aprovação deste ASCII)

Páginas novas no arquivo `Code4Coders — Design System`, sem mexer nas existentes:

1. **🧭 Fluxo — Cortesias**: o fluxo da seção 2, com os frames das telas como nós.
2. **📱 Screens — Cortesias**: T0–T7, todos os estados, em desktop 1440; mobile 390 para T0.d, T1.h, T2.e, T3.e, T4.d, T5.e, T6.c e T7.d. Tema Light; Dark em T1.b e T5.a.
3. **Components (proposta)**, na página de Screens: `StudentAccountCard`, `AccessGrantRow`, `CoursePickRow`, `TermPreview`, `ReviewSentence`, `LifetimeConfirm`, `CourtesyResultCard`, item de sidebar "Cortesias" e ícone lucide `gift` se ainda não existir.

Tudo reusando o AppShell, `EmptyState`, `CodeWindow`, `Stepper`, `RadioGroup`, `Checkbox`, `Alert`, `Table`, `Pagination`, `Dialog`, `Sheet` e demais componentes do DS, sem valor fixo.

---

## 7. Decisões para você aprovar

1. **Área "Cortesias" no grupo Financeiro**, rota `/cortesias` (`/admin/cortesias` cheia), só com `cortesia.conceder`, sem card no Início (G1, G15, T0).
2. **Formulário sequencial em seis passos** com estado mantido até confirmar; passos concluídos resumidos e editáveis (G2).
3. **Passo Aluno** com e-mail exato, textos únicos de "não há conta" e de desativada, aviso de não confirmado que não impede, e concessões em data absoluta com situação (G3–G5, T1).
4. **Passo Curso** por busca de título, só cursos com versão vigente, com ou sem oferta (G6, T2).
5. **Passo Vigência** com prévia do servidor ("até DD/MM/AAAA") e "Acesso vitalício"; a tela nunca calcula (G7, T3).
6. **Passo Motivo** com contador de 500 e orientação sobre dado pessoal de terceiros (G8, T4).
7. **Revisão em uma frase**, aviso de acesso existente sem impedir e **reforço da vitalícia** com "não há como desfazer pela tela" (G9–G11, T5).
8. **Resultado** com a concessão criada e erros por `code`, com reenvio idempotente (G12–G13, G16, T6).
9. **Trilha**: rótulo *Cortesia concedida*, filtro, detalhe com aluno pelo nome, curso pelo título, vigência formatada e motivo, **sem e-mail** (G14, T7).
10. **Componentes novos:** `StudentAccountCard`, `AccessGrantRow`, `CoursePickRow`, `TermPreview`, `ReviewSentence`, `LifetimeConfirm`, `CourtesyResultCard`.
