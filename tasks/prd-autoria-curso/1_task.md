---
status: done
task_kind: enabling
blocked_by: []
gate: 'rg -q "^> \\*\\*Status:\\*\\* ASCII e Figma aprovados pelo responsável em [0-9]{4}-[0-9]{2}-[0-9]{2}" docs/design/wireframes-autoria-curso.md'
gate_expect: "exit 0 somente depois de o responsável aprovar explicitamente o Figma e o documento registrar a aprovação"
---

# 1.0 Desenhar Autoria no Figma e obter aprovação explícita do responsável

**Fatia:** EN-01 · **Cobre:** Experiência do Usuário do PRD; gate visual de todas as fatias ·
**Spec:** [TechSpec, Bloco Frontend](techspec.md#bloco-frontend) · **ADR:** —

## Comportamento

O [ASCII aprovado](../../docs/design/wireframes-autoria-curso.md) é desenhado no arquivo Figma
*Code4Coders — Design System*: fluxo de criação/publicação e de republicação/descarte, A1–A12,
estados de erro, permissão e vazio, desktop 1440, mobile 390 e variações Light/Dark listadas no
handoff. Reusar AppShell, componentes e tokens já aprovados em Acesso interno, Vídeos e DS. O
desenho da trilha mostra o rótulo “Versão publicada” e o alvo curso no padrão de Auditoria.

Ao terminar os frames, registrar no wireframe o link do arquivo e `node-id` de cada tela, estado
e componente proposto. Entregar o link ao responsável para revisão. **Esta task continua
`validating` e as tasks 2.0–7.0 continuam bloqueadas até a aprovação explícita do responsável
sobre o Figma real.** Pedido de ajuste volta ao desenho e à revisão. Só depois da aprovação,
registrar a data no documento com a linha de status que o gate exige, marcar esta task `done` e
liberar a implementação. A aprovação anterior do ASCII não satisfaz este gate.

## Fora do escopo desta task

Código do `admin-spa`, API, migrations, Figma Code Connect ou aprovação presumida.

## Decisões fechadas

- Seções 1–6 do ASCII e as decisões G1–G13 estão aprovadas. Mudança de comportamento descoberta
  no Figma exige atualizar e reapresentar o ASCII antes de pedir aprovação visual.
- A aprovação deve ser dada pelo responsável após receber o link do desenho; o comando estático
  verifica apenas o registro, não substitui a manifestação humana.

## Modificar / Referenciar

- **modificar:** `docs/design/wireframes-autoria-curso.md` (seção Figma com URLs e `node-id`; estado
  “ASCII e Figma aprovados” apenas após aprovação explícita).
- **ref:** `DESIGN.md`, `docs/design/Components.md`,
  `docs/design/wireframes-acesso-interno.md`, `docs/design/wireframes-videos.md`,
  `docs/design/wireframes-auditoria.md`, [PRD](prd.md) e [TechSpec](techspec.md).
- **ref:** [arquivo Figma Code4Coders — Design System](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System).

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `docs/design` | `rg -q "^> \\*\\*Status:\\*\\* ASCII e Figma aprovados pelo responsável em [0-9]{4}-[0-9]{2}-[0-9]{2}" docs/design/wireframes-autoria-curso.md` | exit 0 só após aprovação explícita registrada | PRD, Experiência do Usuário; handoff do ASCII |
| Figma | — | Revisão visual humana de todos os frames e estados; não há CI automatizado de Figma neste projeto | fluxo de design aprovado em CAP-002/CAP-006 |

## Pronto quando

- [x] A1–A12, estados, fluxos, mobile e Dark previstos no ASCII têm frames navegáveis e `node-id`
  registrados no documento.
- [x] O responsável recebe o link e aprova explicitamente o desenho no Figma; eventuais ajustes
  pedidos são reapresentados antes de registrar a aprovação.
- [x] O cabeçalho do wireframe registra data e responsável **após** essa aprovação; o gate retorna
  exit 0. Antes disso, 2.0–7.0 permanecem bloqueadas.

## Evidências do desenho — aprovado em 2026-09-29

- [Índice de revisão no Figma](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn/Code4Coders-%E2%80%94-Design-System?node-id=176-11040).
- O inventário de cada tela, estado, fluxo e composição está na seção 5 dos
  [wireframes de Autoria](../../docs/design/wireframes-autoria-curso.md#5-handoff-do-figma--aprovado).
- 106 frames de telas/estados, incluindo 13 mobile e 4 Dark; dois fluxos e cinco composições.
  Camadas editáveis, fontes do DS e destinos de navegação conferidos pela API do Figma;
  layouts representativos revisados visualmente por screenshots.
- O responsável pelo produto (usuário desta conversa) aprovou explicitamente o desenho em
  2026-09-29: **“Está aprovado”**, após receber o link do Figma real. Data e responsável
  registrados no cabeçalho do wireframe após essa manifestação.
- Gate de aprovação executado com **exit 0**. Esta task está `done`; o bloqueio visual das
  tasks 2.0–7.0 foi satisfeito, sem alterar suas demais dependências nem iniciar implementação.
