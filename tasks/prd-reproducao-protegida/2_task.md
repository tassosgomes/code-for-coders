---
status: pending
task_kind: enabling
blocked_by: ["1.0"]
gate: 'rg -q "^> \*\*Status:\*\* ASCII e Figma aprovados pelo responsável em [0-9]{4}-[0-9]{2}-[0-9]{2}" docs/design/wireframes-aula.md'
gate_expect: "exit 0: o cabeçalho do documento registra a aprovação do ASCII e do Figma pelo responsável"
---

# 2.0 Figma da tela da aula aprovado

**Fatia:** EN-01 (Figma) · **Cobre:** Experiência do Usuário do PRD (desenho visual) · **Spec:** `techspec.md#habilitadores-inevitaveis` · **ADR:** —

## Comportamento

O desenho da tela da aula é produzido no Figma a partir do ASCII aprovado em 1.0, no arquivo do design system do produto,
com **um frame por estado** do wireframe, em desktop e celular de 390 px: reproduzindo (com a marca d'água nas suas zonas),
tela cheia, pausado, e cada estado de mensagem (sem acesso, acesso encerrado, indisponibilidade, aula não disponível, vídeo
indisponível, não foi possível iniciar, navegador sem suporte, reprodução interrompida). O desenho usa os componentes e
tokens do design system do aluno e dispensa estilo avulso.

O documento `docs/design/wireframes-aula.md` ganha uma seção de **handoff do Figma** com o índice de revisão e o frame de
cada estado, e o cabeçalho passa a `> **Status:** ASCII e Figma aprovados pelo responsável em <AAAA-MM-DD>` — escrito
**só depois** da aprovação explícita do responsável sobre o Figma. Pedido de ajuste volta ao Figma (ou ao ASCII, se mudar
conteúdo) antes do registro.

## Fora do escopo desta task

Qualquer código. Novos estados além dos do ASCII aprovado (se surgirem, voltam ao ASCII).

## Decisões fechadas

- O Figma parte do ASCII aprovado e **não** altera conteúdo, mensagem nem fluxo sem voltar ao ASCII.
- Componentes e tokens do design system do aluno; nada de cor ou espaçamento solto.

## Modificar / Referenciar

Arquivos a criar não são listados: a estrutura vem das skills `dotnet` e `react`.

- **modificar:** `docs/design/wireframes-aula.md` (registro dos frames e do status)
- **ref:** `docs/design/wireframes-cortesias.md` (seção de handoff do Figma e aprovação); `DESIGN.md`; `docs/design/Components.md`; skills `figma:figma-generate-design` e `figma:figma-use`

## Verificações do projeto

| `docs/design` | — | Sem check automatizado; a aprovação é do responsável | Fluxo de design do SPA do aluno |

## Pronto quando

- [ ] Gate passa (exit 0).
- [ ] Há um frame para cada estado do ASCII, em desktop e celular, registrados no documento.
- [ ] O responsável aprovou o Figma explicitamente.
