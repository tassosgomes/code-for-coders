---
status: pending
task_kind: enabling
blocked_by: ["1.0"]
gate: 'rg -q "^> \*\*Status:\*\* ASCII e Figma aprovados pelo responsável em [0-9]{4}-[0-9]{2}-[0-9]{2}" docs/design/wireframes-compra.md'
gate_expect: "exit 0: o cabeçalho registra ASCII e Figma aprovados pelo responsável, com a data"
---

# 2.0 Figma da compra, do pedido e dos pedidos no backoffice aprovado

**Fatia:** EN-02 (Figma) · **Cobre:** RF-01, RF-02, RF-05, RF-06, RF-10, RF-11 (desenho) · **Spec:** `techspec.md` § Habilitadores inevitáveis · **ADR:** —

## Comportamento

As telas aprovadas em ASCII (1.0) existem no arquivo Code4Coders — Design System do Figma, com os componentes e tokens do design system vigente, em desktop e celular, para todos os estados de 1.0. `docs/design/wireframes-compra.md` ganha a seção de handoff do Figma com o link de cada frame (no formato da seção 8 de `wireframes-cortesias.md`) e a linha `> **Figma:**` no cabeçalho. Ajuste pedido pelo responsável volta ao ASCII antes de qualquer registro. Com a aprovação, a linha `> **Status:**` passa a registrar "ASCII e Figma aprovados pelo responsável em AAAA-MM-DD" e libera o código de tela a partir de 4.0.

## Fora do escopo desta task

- Código de tela → 4.0 em diante.
- Identidade visual definitiva (A2 da visão): as telas usam o design system atual.

## Decisões fechadas

- As do ASCII aprovado em 1.0; o Figma não reabre conteúdo nem estados.

## Modificar / Referenciar

- **modificar:** `docs/design/wireframes-compra.md` (seção de handoff do Figma e registro de aprovação)
- **ref:** `docs/design/wireframes-cortesias.md` (seção 8, formato do handoff); `DESIGN.md`; `docs/design/Components.md`; skill `figma:figma-generate-design`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| Documentação | — | Sem check automatizado para documento de design; o gate estático é a evidência | AGENTS.md |

## Pronto quando

- [ ] Gate passa (exit 0).
- [ ] Cada tela e estado de 1.0 tem frame no Figma com link no documento.
- [ ] O responsável aprovou o Figma, e a aprovação está registrada no cabeçalho com a data.
