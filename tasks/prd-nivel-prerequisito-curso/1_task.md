---
status: pending
task_kind: enabling
blocked_by: []
gate: 'rg -q "^> \*\*Adendo nível e pré-requisito:\*\* (ASCII aprovado|ASCII e Figma aprovados) pelo responsável em [0-9]{4}-[0-9]{2}-[0-9]{2}" docs/design/wireframes-autoria-curso.md'
gate_expect: "exit 0: o cabeçalho do documento de wireframes registra a aprovação do adendo ASCII pelo responsável"
---

# 1.0 Adendo ASCII de nível e pré-requisito aprovado

**Fatia:** EN-01 (ASCII) · **Cobre:** Experiência do Usuário do PRD; RF-01, RF-02, RF-04, RF-05 (desenho) · **Spec:** `techspec.md#bloco-frontend` · **ADR:** —

## Comportamento

`docs/design/wireframes-autoria-curso.md` ganha um adendo do 2º PRD de `CAP-005`, sobre as telas já
aprovadas do 1º PRD, sem reabrir as decisões delas. O adendo desenha, com todos os estados:

- **A3 · Editor** — seção **Para quem é este curso**, ao lado de título e descrição: nível como grupo
  de opções com rótulo (Iniciante, Intermediário, Avançado, Sem nível); texto do pré-requisito com
  contador até 1 000; cursos recomendados em lista ordenada (até 5) com adicionar, reordenar por
  teclado sem arrastar e remover; erro no campo para `FIELD_INVALID` e `RECOMMENDED_COURSE_INVALID`;
  estado somente leitura para quem tem `autoria.ler` sem `autoria.editar`.
- **Seletor de cursos recomendados** — busca por título (a partir de 2 caracteres), só cursos
  publicados da escola, sem o próprio curso, sem os já escolhidos; vazio, carregando, erro; ação de
  adicionar indisponível com 5 escolhidos, com o motivo em texto.
- **Aviso de curso sem nível** — permanente no editor, em texto e anunciado a leitor de tela, com
  atalho para o campo de nível; variante "o nível só vale depois de publicar" quando o nível existe só
  no rascunho.
- **A7 · Publicar/republicar** — repete o aviso sem bloquear; botão de publicar habilitado.
- **A1 · Lista de cursos** — "Sem nível" em texto (não só cor) para curso publicado cuja versão
  vigente não tem nível.
- **A10 · Versão publicada** — nível e pré-requisito da versão, com os cursos recomendados pelo título
  da época; versões anteriores a esta entrega mostram "Sem nível" e "Sem pré-requisito".
- **Ajuda contextual (G12)** — passa a dizer que **preço e vigência** são da oferta; nível e
  pré-requisito são do professor.
- Mobile 390 px para a seção e o seletor.

O cabeçalho do documento ganha, abaixo do `> **Status:**` do 1º PRD, a linha
`> **Adendo nível e pré-requisito:** ASCII aprovado pelo responsável em <AAAA-MM-DD>` — escrita só
depois da aprovação explícita do responsável. Pedido de ajuste volta ao ASCII antes do registro.

## Fora do escopo desta task

Figma (2.0) e qualquer código. Vitrine, filtro por nível e links para recomendados (`CAP-003`). O
histórico em lista (A9) não muda, porque `listCourseVersions` não mudou no contrato.

## Decisões fechadas

- Nível e pré-requisito são **recomendação**: nenhum texto sugere que impedem compra ou acesso (DE04).
- Nível é opcional para publicar (DP-01); pré-requisito é texto e/ou até 5 cursos publicados da
  escola (DP-02); recomendação mútua é permitida.
- No rascunho, o recomendado aparece com o título atual do rascunho dele; na versão, com o título da
  versão vigente dele no momento da publicação (`techspec.md#decisões-técnicas`, C-02).
- Decisões G1–G12 e demais do 1º PRD continuam valendo, exceto o texto de G12, revisto aqui.

## Modificar / Referenciar

- **modificar:** `docs/design/wireframes-autoria-curso.md` (adendo e linha de aprovação no cabeçalho)
- **ref:** `prd.md` (RF-01 a RF-05, Experiência do Usuário); `techspec.md#bloco-frontend`; `api-contract.yaml` (campos e erros); `docs/design/wireframes-auditoria.md` (formato de registro de aprovação)

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `docs/design` | — | Sem check automatizado; a aprovação é do responsável | Fluxo de design do backoffice (CAP-002) |

## Pronto quando

- [ ] Gate passa (exit 0).
- [ ] O adendo cobre editor, seletor, aviso nas duas variantes, janela de publicação, lista, página da versão, leitor e mobile.
- [ ] O responsável aprovou o ASCII explicitamente.
