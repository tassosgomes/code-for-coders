---
status: pending
task_kind: enabling
blocked_by: ["1.0"]
gate: 'rg -q "^> \*\*Status:\*\* ASCII e Figma aprovados pelo responsável em [0-9]{4}-[0-9]{2}-[0-9]{2}" docs/design/wireframes-cortesias.md'
gate_expect: "exit 0: o cabeçalho do documento registra a aprovação do ASCII e do Figma pelo responsável"
---

# 2.0 Figma da área Cortesias aprovado

**Fatia:** EN-01 (Figma) · **Cobre:** Experiência do Usuário do PRD; RF-01, RF-02, RF-03, RF-05, RF-08 (desenho) · **Spec:** `techspec.md#habilitadores-inevitáveis` · **ADR:** —

## Comportamento

As telas do ASCII aprovado em 1.0 são desenhadas no Figma sobre o design system do backoffice, com todos os
estados e a variante mobile. O documento de wireframes passa a referenciar os frames (link do índice de revisão e
os node-ids por tela), e o cabeçalho troca a linha de status por
`> **Status:** ASCII e Figma aprovados pelo responsável em <AAAA-MM-DD>` — escrita só depois da aprovação explícita
do responsável sobre o desenho real. Pedido de ajuste volta ao Figma antes do registro.

## Fora do escopo desta task

Código de tela. Mudança de decisão já fechada no ASCII: se o Figma exigir, volta a 1.0.

## Decisões fechadas

- O Figma parte do ASCII aprovado em 1.0 e não reabre o fluxo nem os textos aprovados.
- Componentes do design system existente têm precedência sobre componente novo (`docs/design/Components.md`).

## Modificar / Referenciar

- **modificar:** `docs/design/wireframes-cortesias.md` (links e node-ids dos frames; linha de status)
- **ref:** `DESIGN.md`; `docs/design/Components.md`; `docs/design/wireframes-catalogo-vitrine.md` (seção de frames e registro de aprovação); Figma do design system

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `docs/design` | — | Sem check automatizado; a aprovação é do responsável | Fluxo de design do backoffice (CAP-002) |

## Pronto quando

- [ ] Gate passa (exit 0).
- [ ] Toda tela do ASCII tem frame no Figma, em desktop e mobile, e o documento aponta para eles.
- [ ] O responsável aprovou o Figma explicitamente.
