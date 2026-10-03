---
status: done
task_kind: enabling
blocked_by: ["1.0"]
gate: 'grep -qE "^> \*\*Status:\*\* aprovado \(ASCII e Figma\)" docs/design/wireframes-auditoria.md'
gate_expect: "exit 0: o documento de wireframes registra a aprovação do Figma pelo usuário, além do ASCII"
---

# 2.0 Telas da área Auditoria no Figma aprovadas

**Fatia:** EN-02 (Figma) · **Cobre:** Experiência do Usuário do PRD · **Spec:** `techspec.md#frontend` · **ADR:** —

## Comportamento

O Figma `kKNfxTqSFT5IfQHcoTh5gn` (Code4Coders — Design System) já tem, desde 2026-09-27, as páginas
"Fluxo — Auditoria" e "Screens — Auditoria" com A1–A5, estados, mobile, validação Dark e a proposta
de componentes novos; os node-ids estão na §7 de `docs/design/wireframes-auditoria.md`. B12, B13 e
B1 reusam os frames de CAP-002.

O usuário revisa o desenho e aprova ou pede ajustes. Ajuste que mude comportamento em relação ao
ASCII atualiza e reaprova o ASCII antes do registro. Aprovado o Figma:

- o cabeçalho passa a `> **Status:** aprovado (ASCII e Figma) em <data> · implementação não
  iniciada`, e o título da §7 deixa de dizer "aguardando aprovação";
- a nota da §7 que pede ajuste na TechSpec passa a apontar a revisão 1.1 da TechSpec, que já
  incorporou os nomes na lista (decisão 2).

## Fora do escopo desta task

Código no `admin-spa`. Code Connect. Migrar os componentes propostos para a página Components.

## Decisões fechadas

- O ASCII aprovado em 1.0 é a fonte; o Figma não reabre as decisões da seção 6.
- Situação com cor, ícone e texto, nunca só cor (G7). Nenhum UUID ou texto livre em URL (G25).
- Complemento no próprio detalhe, sem AlertDialog extra (decisão 6); nunca "falhou" após `202` (decisão 7).

## Modificar / Referenciar

- **modificar:** `docs/design/wireframes-auditoria.md` (status e nota da §7)
- **ref:** skills `figma:figma-use` e `figma:figma-generate-design`; páginas "Screens — Acesso interno" e "Screens — Vídeos" do mesmo arquivo

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `docs/design` | — | Sem check automatizado; a aprovação é do usuário | Fluxo de design do backoffice (CAP-002) |

## Pronto quando

- [x] Gate passa (exit 0).
- [x] Os node-ids da §7 continuam válidos após os ajustes pedidos na revisão.
- [x] O usuário aprovou o Figma (2026-09-27).
