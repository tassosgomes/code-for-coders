---
status: done
task_kind: enabling
blocked_by: []
gate: 'grep -qE "^> \*\*Status:\*\* .*(ASCII aprovado|aprovado \(ASCII)" docs/design/wireframes-auditoria.md'
gate_expect: "exit 0: o documento de wireframes registra a aprovação do ASCII pelo usuário"
---

# 1.0 Wireframe ASCII da área Auditoria aprovado

**Fatia:** EN-01 (ASCII) · **Cobre:** Experiência do Usuário do PRD · **Spec:** `techspec.md#frontend` · **ADR:** —

## Comportamento

`docs/design/wireframes-auditoria.md` descreve as telas A1–A5, B12 e B13 com todos os estados, o
fluxo do usuário e as decisões de desenho G1–G26, e registra no cabeçalho a aprovação do ASCII pelo
usuário. Aprovado em 2026-09-27, antes da geração deste plano; a task entra já concluída para
manter a sequência design → Figma → código.

A decisão 2 (nomes de autor e alvo também na lista) foi aprovada em 2026-09-27 e levou à revisão
1.1 da TechSpec.

## Fora do escopo desta task

Figma (2.0) e código.

## Decisões fechadas

- Decisões 1–9 da seção 6 do documento de wireframes, com a decisão 2 aprovada (nomes na lista).

## Modificar / Referenciar

- **modificar:** `docs/design/wireframes-auditoria.md` (já concluído)
- **ref:** `docs/design/wireframes-acesso-interno.md` (AppShell, B12, B13, `ReasonField`)

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `docs/design` | — | Sem check automatizado; a aprovação é do usuário | Fluxo de design do backoffice (CAP-002) |

## Pronto quando

- [x] Gate passa (exit 0).
- [x] O usuário aprovou o ASCII.
