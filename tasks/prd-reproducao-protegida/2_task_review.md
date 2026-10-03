# Revisão focused — Task 2.0 (Figma da tela da aula aprovado)

Run: run.H2U2spoF

- Modo: focused · Tentativa: 1/3 · HEAD revisado: 51ec37bab5bf1cc23b921c1a52acfbaf808c9193 · checkpoint anterior: 1dd3cb6
- Escopo: `git diff 1dd3cb6..HEAD -- docs/design/wireframes-aula.md` (commits 62073b2 e 51ec37b) + alterações não commitadas de estado (`2_task.md`, `flow-state.json`, só metadados do fluxo).

## Gate

| Comando | Resultado |
|---|---|
| `rtk proxy rg -q "^> \*\*Status:\*\* ASCII e Figma aprovados pelo responsável em [0-9]{4}-[0-9]{2}-[0-9]{2}" docs/design/wireframes-aula.md` | exit 0 |

`gate_expect` atendido: o cabeçalho registra "ASCII e Figma aprovados pelo responsável em 2026-10-03".
Observação: a primeira execução sem `rtk proxy` devolveu exit 1 porque o hook do rtk reescreve o comando e altera o `rg -q`; com `rtk proxy` (argumentos originais preservados) o exit é 0, e `rg -c` sem `-q` confirma 1 linha casada. Não é falha do código.

Verificações do projeto: `docs/design` não tem check automatizado (a aprovação é do responsável). Nenhum código tocado; sem suíte comportamental aplicável.

## Conferência do "Pronto quando"

- [x] Gate passa (exit 0).
- [x] Há frame para cada estado do ASCII em desktop e celular, registrados na seção 8.2: A1.a, A1.a2, A1.b, A1.c (desktop e celular na horizontal), A1.d, A1.e, A1.f–A1.n (desktop 1440 e mobile 390). A1.o (renovação invisível, G11) e A1.p/A1.q/A1.r têm tratamento explícito (sem frame por decisão, coberto por A1.f/A1.h mobile, reuso do frame de Entrar 51:537 e Fluxo 2). Cobre também o estado "reproduzindo com a marca d'água" nas 4 zonas (LessonPlayer 256:535).
- [x] Aprovação explícita do responsável registrada com citação literal ("Perfeito! Está aprovado"), no commit 51ec37b, posterior ao handoff 62073b2 (ordem exigida: registro só depois da aprovação).
- [x] Seções 1–7 (ASCII aprovado) intactas: o diff toca só o cabeçalho (Status/Figma) e a seção 8 — o Figma não alterou conteúdo, mensagem nem fluxo.
- [x] Nenhum código alterado (fora do escopo respeitado); formato do handoff segue `wireframes-cortesias.md`.
- [x] Componentes do design system documentados (grupo "Composições propostas" com WatermarkTag, PlayerControls, LessonPlayer, LessonState, etc.); nenhum estilo avulso apontado no documento.

## Bloqueantes

Nenhum.

## Recomendações (não bloqueantes) — 2

1. O validator não abriu o arquivo do Figma (sem acesso no escopo desta revisão); a existência e o conteúdo dos node-ids não foram verificados, só a coerência documental e a aprovação registrada pelo responsável. Conferir por amostragem o índice 260:2332 antes da task 3.0, se desejado.
2. Dark só em A1.a e A1.f e mobile da tela cheia só na horizontal: suficiente para a task, mas as tasks de tela devem usar os tokens do DS e não o desenho como medida de pixel para os demais estados.

## Resultado

VALIDAÇÃO APROVADA (0 bloqueantes, 2 recomendações).
