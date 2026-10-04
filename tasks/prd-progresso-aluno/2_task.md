---
status: done
task_kind: enabling
blocked_by: ["1.0"]
gate: 'rg -q "^> \*\*Status:\*\* ASCII e Figma aprovados pelo responsável em [0-9]{4}-[0-9]{2}-[0-9]{2}" docs/design/wireframes-progresso.md'
gate_expect: "exit 0: o cabeçalho registra ASCII e Figma aprovados pelo responsável, com a data"
---

# 2.0 Figma de "meus cursos" e da retomada na aula aprovado

**Fatia:** EN-01 (Figma) · **Cobre:** RF-04, RF-05, RF-06 (desenho) · **Spec:** `techspec.md#habilitadores-inevitáveis` · **ADR:** —

## Comportamento

Os estados aprovados em 1.0 existem como frames no arquivo Code4Coders — Design System, sobre os componentes e tokens do design system do aluno, em desktop e celular, Light e Dark. `docs/design/wireframes-progresso.md` ganha a seção de handoff do Figma com o link de cada frame, e a linha `> **Status:**` passa a registrar "ASCII e Figma aprovados pelo responsável em AAAA-MM-DD". Pedido de ajuste volta ao documento antes de qualquer registro.

## Fora do escopo desta task

- Código de tela → 4.0 a 6.0.

## Decisões fechadas

- O que 1.0 aprovou não muda aqui sem voltar ao ASCII.
- Componentes do design system existente; nada de componente novo sem registro em `docs/design/Components.md`.

## Modificar / Referenciar

- **modificar:** `docs/design/wireframes-progresso.md` (seção de handoff do Figma e status)
- **ref:** `docs/design/wireframes-aula.md` (seção 8, formato do handoff); skills `figma:figma-use` e `figma:figma-generate-design`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| Documentação | — | Sem check automatizado para documento de design; o gate estático é a evidência | AGENTS.md |

## Pronto quando

- [x] Gate passa (exit 0).
- [x] Cada estado de 1.0 tem frame no Figma, com link no documento.
- [x] O responsável aprovou o Figma, e a aprovação está registrada no cabeçalho com a data.
