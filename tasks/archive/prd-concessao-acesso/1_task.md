---
status: done
task_kind: enabling
blocked_by: []
gate: 'rg -q "^> \*\*Status:\*\* (ASCII aprovado|ASCII e Figma aprovados) pelo responsável em [0-9]{4}-[0-9]{2}-[0-9]{2}" docs/design/wireframes-cortesias.md'
gate_expect: "exit 0: o cabeçalho do documento de wireframes das cortesias registra a aprovação do ASCII pelo responsável"
---

# 1.0 Wireframes ASCII da área Cortesias aprovados

**Fatia:** EN-01 (ASCII) · **Cobre:** Experiência do Usuário do PRD; RF-01, RF-02, RF-03, RF-05, RF-08 (desenho); DP-03, DP-06, DP-07 · **Spec:** `techspec.md#habilitadores-inevitáveis` · **ADR:** —

## Comportamento

Um documento de wireframes em `docs/design/wireframes-cortesias.md` desenha, para o backoffice (`admin-spa`,
design system do backoffice) e com todos os estados (carregando, vazio, erro, sem permissão, mobile 390 px),
as telas que a TechSpec prevê:

- **Item de menu e rota** da área **Cortesias** (`/admin/cortesias`), visível só com `cortesia.conceder`; estado de
  acesso negado na rota direta.
- **Formulário sequencial em seis passos**, com o estado de cada passo mantido até confirmar:
  1. *Aluno*: campo de e-mail, resultado (e-mail e nome da conta), aviso "e-mail ainda não confirmado" que
     não impede, conta desativada que impede, "Não há conta de aluno com este e-mail" (o mesmo texto para
     inexistente e conta de ator interno), erro de serviço indisponível; e a **lista das concessões do aluno**
     (curso, origem, vigência em data absoluta, situação ativa ou vencida; vazia quando não há).
  2. *Curso*: busca por título, lista paginada de cursos com versão vigente (com ou sem oferta), vazio.
  3. *Vigência*: "Por período" (meses de 1 a 60) ou "Vitalícia"; a **prévia da data do término** ("até DD/MM/AAAA",
     vinda do servidor) e o texto "Acesso vitalício".
  4. *Motivo*: texto obrigatório com contador de 500 e a orientação de não incluir dado pessoal de terceiros.
  5. *Revisão*: resumo em **uma frase** ("Conceder a [e-mail] acesso a [curso] até [data] — motivo: …"), o aviso
     "este aluno já tem acesso até DD/MM/AAAA" sem impedir, e a **confirmação reforçada da vitalícia** com a
     frase de que **não há como desfazer pela tela**.
  6. *Resultado*: a concessão criada (curso, vigência, término), e os erros por `code` (motivo, meses, curso e
     conta não elegíveis, serviço indisponível, chave reutilizada).
- **Trilha de auditoria (CAP-030)**: rótulo *Cortesia concedida*, opção no filtro por tipo e o detalhe com o
  aluno **pelo nome**, o curso **pelo título**, a vigência formatada ("6 meses" ou "Vitalícia") e o motivo; o
  e-mail do aluno **não** aparece.
- Acessibilidade: passos como formulário sequencial com rótulo, erro anunciado e foco no primeiro campo
  inválido; data do término em texto; confirmação reforçada focável e anunciada; resumo legível como uma frase.

O cabeçalho do documento registra `> **Status:** ASCII aprovado pelo responsável em <AAAA-MM-DD>` — escrito só
depois da aprovação explícita do responsável. Pedido de ajuste volta ao ASCII antes do registro.

## Fora do escopo desta task

Figma (2.0) e qualquer código. Concessão por compra, revogação, suspensão, turma e telas do aluno.

## Decisões fechadas

- Fluxo de design: wireframe ASCII → Figma → aprovação → código de tela (PRD, Experiência do Usuário).
- A área se chama **Cortesias** e a rota é `/cortesias`; "Acessos" é do administrador (`techspec.md#decisões-técnicas`, D-01).
- Vigência sempre com data absoluta; a tela **não** calcula o término: mostra o que o servidor devolve (D-03).
- A tela nunca fala em "pedido" nem "compra" para cortesia e nunca promete ao aluno nada além da concessão.

## Modificar / Referenciar

- **ref:** `prd.md` (Experiência do Usuário, RF-01 a RF-05, RF-08); `techspec.md#bloco-frontend`; `api-contract.md`
  (campos e erros); `docs/design/wireframes-catalogo-vitrine.md` e `docs/design/wireframes-auditoria.md` (formato do
  documento e do registro de aprovação); `docs/design/Components.md`; `DESIGN.md`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `docs/design` | — | Sem check automatizado; a aprovação é do responsável | Fluxo de design do backoffice (CAP-002) |

## Pronto quando

- [x] Gate passa (exit 0).
- [x] O documento cobre todas as telas e estados acima, em desktop e mobile 390 px.
- [x] O responsável aprovou o ASCII explicitamente.
