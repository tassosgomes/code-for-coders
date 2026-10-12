---
status: done
task_kind: enabling
blocked_by: ["1.0"]
gate: 'rg -q "^> \*\*Status:\*\* aprovado \(ASCII e Figma\) em [0-9]{4}-[0-9]{2}-[0-9]{2}" docs/design/wireframes-transcricao.md'
gate_expect: "exit 0: o cabeçalho de docs/design/wireframes-transcricao.md registra ASCII e Figma aprovados pelo responsável"
---

# 2.0 Figma da transcrição (tela Vídeos e página da aula) aprovado

**Fatia:** EN-01 (Figma) · **Cobre:** RF-02, RF-03, RF-04, RF-05, RF-08 (desenho) · **Spec:** `techspec.md#habilitadores-inevitáveis` · **ADR:** —

## Comportamento

O ASCII aprovado em 1.0 está desenhado no Figma do Design System Code4Coders, sobre as telas já
aprovadas de Vídeos e de Aula. O desenho reaproveita componentes e tokens existentes e cobre todos
os estados de 1.0, em desktop e celular, claro e escuro. O documento
`docs/design/wireframes-transcricao.md` ganha a seção de handoff com os frames (no formato da
seção "Handoff do Figma" de `docs/design/wireframes-aula.md`), e o cabeçalho passa a registrar
`> **Status:** aprovado (ASCII e Figma) em AAAA-MM-DD`, depois da aprovação explícita do
responsável. Divergência entre o Figma e o ASCII aprovado é decidida pelo responsável e registrada
no documento. A partir desta aprovação o código de tela das tasks seguintes está liberado.

## Fora do escopo desta task

- Qualquer código de tela. O design não altera contrato, regra de negócio nem o texto dos motivos fixados em 1.0.

## Decisões fechadas

- O Figma parte do ASCII aprovado; não redesenha o que 1.0 decidiu.
- A aprovação do ASCII e a do Figma são decisões separadas do responsável.

## Modificar / Referenciar

- **modificar:** `docs/design/wireframes-transcricao.md` (seção de handoff do Figma e linha de status do cabeçalho)
- **ref:** `docs/design/wireframes-aula.md` (formato do handoff); `DESIGN.md`; `docs/design/Components.md`; skills `figma:figma-generate-design` e `figma:figma-use`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| Documentação | `—` | Sem check automatizado para documento de design; o gate estático é a evidência | AGENTS.md (não há lint de Markdown no CI) |

## Pronto quando

- [ ] Gate passa (exit 0).
- [ ] Todos os frames de 1.0 existem no Figma, em desktop e celular, e estão listados no documento.
- [ ] O responsável aprovou o Figma, e a aprovação está registrada no cabeçalho com a data.
