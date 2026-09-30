---
status: pending
task_kind: enabling
blocked_by: ["1.0"]
gate: 'rg -q "^> \*\*Adendo nível e pré-requisito:\*\* ASCII e Figma aprovados pelo responsável em [0-9]{4}-[0-9]{2}-[0-9]{2}" docs/design/wireframes-autoria-curso.md'
gate_expect: "exit 0: a linha do adendo registra a aprovação do Figma pelo responsável, além do ASCII"
---

# 2.0 Adendo de nível e pré-requisito no Figma aprovado

**Fatia:** EN-01 (Figma) · **Cobre:** Experiência do Usuário do PRD; RF-01, RF-02, RF-04, RF-05 (desenho) · **Spec:** `techspec.md#bloco-frontend` · **ADR:** —

## Comportamento

No arquivo Figma `kKNfxTqSFT5IfQHcoTh5gn` (Code4Coders — Design System), as páginas de Autoria ganham
os frames do adendo aprovado em 1.0, sobre o design system do backoffice e reusando os componentes de
Autoria já aprovados: seção "Para quem é este curso" (padrão, preenchida, erro, somente leitura),
seletor de recomendados (busca, vazio, limite de 5, reordenação), aviso nas duas variantes, janela de
publicação com aviso, lista com "Sem nível", página da versão com nível e pré-requisito (inclusive
versão anterior à entrega), mobile 390 px e validação em Dark.

O responsável revisa pelo link do desenho real e aprova ou pede ajustes. Ajuste que mude comportamento
em relação ao ASCII atualiza e reaprova o ASCII antes do registro. Aprovado o Figma:

- a linha do cabeçalho passa a
  `> **Adendo nível e pré-requisito:** ASCII e Figma aprovados pelo responsável em <AAAA-MM-DD>`;
- a seção de handoff do Figma do documento lista os node-ids dos frames do adendo.

## Fora do escopo desta task

Código no `admin-spa`. Code Connect. Promover componentes novos à página Components.

## Decisões fechadas

- O ASCII aprovado em 1.0 é a fonte; o Figma não reabre as decisões do adendo.
- Aviso de sem nível em texto, anunciado a leitor de tela, nunca só cor.
- Reordenação dos recomendados operável por teclado, sem depender de arrastar.

## Modificar / Referenciar

- **modificar:** `docs/design/wireframes-autoria-curso.md` (linha de aprovação e node-ids do adendo)
- **ref:** skills `figma:figma-use` e `figma:figma-generate-design`; índice de Autoria no Figma (node `176:11040`) e frames A1, A3, A7 e A10 do 1º PRD

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `docs/design` | — | Sem check automatizado; a aprovação é do responsável | Fluxo de design do backoffice (CAP-002) |

## Pronto quando

- [ ] Gate passa (exit 0).
- [ ] Os node-ids registrados abrem os frames do adendo.
- [ ] O responsável aprovou o Figma explicitamente.
