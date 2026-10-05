---
status: done
task_kind: enabling
blocked_by: []
gate: 'rg -q "^> \*\*Status:\*\* (ASCII aprovado|ASCII e Figma aprovados) pelo responsável em [0-9]{4}-[0-9]{2}-[0-9]{2}" docs/design/wireframes-progresso.md'
gate_expect: "exit 0: o cabeçalho do documento de wireframes do progresso registra a aprovação do ASCII pelo responsável"
---

# 1.0 Wireframes ASCII de "meus cursos" e da retomada na aula aprovados

**Fatia:** EN-01 (ASCII) · **Cobre:** RF-04, RF-05, RF-06 (desenho) · **Spec:** `techspec.md#habilitadores-inevitáveis` · **ADR:** —

## Comportamento

Existe `docs/design/wireframes-progresso.md`, no formato de `docs/design/wireframes-aula.md`, com o desenho ASCII, em desktop e celular, de:

- **Início = "meus cursos"** (`/`), no lugar reservado hoje em `dashboard-screen.tsx`, mantendo o card de conta: seção "Seus cursos" (título, percentual com "N de M aulas", Continuar ou Começar), seção "Acesso encerrado" (secundária, sem ação, "Seu acesso terminou em dd/mm/aaaa") e os estados carregando, vazio ("Você ainda não tem cursos" com caminho para a vitrine `/cursos`), indisponível (*Tentar de novo*, nunca o vazio) e progresso indisponível (lista sem percentual, com aviso).
- **Tela da aula** (a aprovada em `CAP-007`): marca de aula concluída na lista (texto além do ícone), percentual do curso, aviso "Retomando de m:ss" com *Começar do início* perto do player, e o caminho de volta ao Início.
- Como a sidebar trata "Início" agora que ele é "meus cursos".

Cada estado traz o texto exato que o aluno lê (PRD, Experiência do Usuário) e as notas de acessibilidade do PRD. O cabeçalho tem a linha `> **Status:**` com o registro de aprovação do responsável, no formato que o gate confere.

## Fora do escopo desta task

- Figma → 2.0. Código de tela → 4.0 a 6.0.

## Decisões fechadas

- Nenhuma rota nova: "meus cursos" é o Início, destino do login sem página de origem (DP-08, `techspec.md`).
- Encerrado aparece em seção própria, sem ação (DP-02). Percentual sempre com a contagem.
- Conclusão e percentual nunca bloqueiam aula (RN-R06, G20): nada de cadeado ou aula "bloqueada" no desenho.

## Modificar / Referenciar

- **ref:** `docs/design/wireframes-aula.md` (formato, registro de aprovação, tela da aula aprovada); `docs/design/wireframes-conta-aluno.md` (T6 Início e sidebar); `DESIGN.md`; `docs/design/Components.md`; `prd.md` (Experiência do Usuário, RF-04 a RF-06)

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| Documentação | — | Sem check automatizado para documento de design; o gate estático é a evidência | AGENTS.md (não há lint de Markdown no CI) |

## Pronto quando

- [x] Gate passa (exit 0).
- [x] Todos os estados de RF-04 e as três mudanças da tela da aula estão desenhados em desktop e celular.
- [x] O responsável aprovou o ASCII, e a aprovação está registrada no cabeçalho com a data.
