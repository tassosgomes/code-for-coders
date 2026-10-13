---
status: pending
task_kind: enabling
blocked_by: ["1.0"]
gate: 'rg -q "^> \*\*Adendo NFS-e:\*\* ASCII e Figma aprovados pelo responsável em [0-9]{4}-[0-9]{2}-[0-9]{2}" docs/design/wireframes-compra.md'
gate_expect: "exit 0: o cabeçalho de wireframes-compra.md registra ASCII e Figma aprovados para o adendo da NFS-e"
---

# 2.0 Figma do adendo da nota e do prestador aprovado

**Fatia:** EN-01 (Figma) · **Cobre:** RF-02, RF-03, RF-05, RF-06, RF-07 (desenho) · **Spec:** `techspec.md#habilitadores-inevitáveis` · **ADR:** —

## Comportamento

O adendo ASCII aprovado em 1.0 vira frames no arquivo de design que `docs/design/wireframes-compra.md` já aponta (Code4Coders — Design System). Os frames cobrem as mesmas superfícies e estados do ASCII, em desktop e celular, com o texto travado em 1.0. Light e dark seguem o tema que o arquivo já tem.

O adendo ganha, no fim, a seção de handoff: link de cada frame. A linha do cabeçalho passa a:

`> **Adendo NFS-e:** ASCII e Figma aprovados pelo responsável em AAAA-MM-DD`

A linha `> **Status:**` de CAP-011 não muda. Pedido de ajuste volta ao ASCII (1.0) antes de qualquer registro novo.

Código de tela só começa depois desta aprovação.

## Fora do escopo desta task

- Código de `student-spa` e `admin-spa` → 4.0 a 8.0.
- Redesenhar resumo, pagamento, comprovante ou filtros de pedido já aprovados.

## Decisões fechadas

- O texto é o do ASCII aprovado; o Figma não reabre frase.
- Três superfícies: bloco no pedido do aluno, bloco no pedido do financeiro, `/financeiro/prestador` (`techspec.md` § Bloco Frontend).

## Modificar / Referenciar

- **modificar:** `docs/design/wireframes-compra.md` (seção de handoff do adendo e a linha `Adendo NFS-e` no cabeçalho)
- **ref:** o adendo ASCII produzido em 1.0; `DESIGN.md` § 12; `docs/design/wireframes-compra.md` § 8 (handoff já usado em CAP-011, como formato, sem reabrir aqueles frames)

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| Documentação | — | Sem check automatizado para documento de design; o gate estático é a evidência | Não há lint de Markdown no CI (`.github/workflows`) |

## Pronto quando

- [ ] Gate passa (exit 0).
- [ ] Cada estado desenhado em 1.0 tem frame, e o handoff do adendo aponta para ele.
- [ ] O responsável aprovou o Figma, e a linha do cabeçalho registra ASCII e Figma com a data.
