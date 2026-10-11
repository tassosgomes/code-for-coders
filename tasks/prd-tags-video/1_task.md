---
status: done
task_kind: enabling
blocked_by: []
gate: 'rg -q "^> \*\*Adendo tags de vídeo:\*\* (ASCII aprovado|ASCII e Figma aprovados) pelo responsável em [0-9]{4}-[0-9]{2}-[0-9]{2}" docs/design/wireframes-videos.md && rg -q "^> \*\*Adendo tags de vídeo:\*\* (ASCII aprovado|ASCII e Figma aprovados) pelo responsável em [0-9]{4}-[0-9]{2}-[0-9]{2}" docs/design/wireframes-autoria-curso.md'
gate_expect: "exit 0: o cabeçalho dos dois documentos de wireframe registra a aprovação do adendo ASCII de tags pelo responsável"
---

# 1.0 Adendo ASCII de tags de vídeo (envio, tela Vídeos e seletor da aula) aprovado

**Fatia:** EN-01 (ASCII) · **Cobre:** RF-01 a RF-06 (desenho) · **Spec:** `techspec.md` § Habilitadores inevitáveis, § Bloco Frontend · **ADR:** —

## Comportamento

Os dois documentos de wireframe aprovados ganham uma seção de **adendo**, sem reabrir o que já foi
aprovado neles. O formato é o do adendo de nível e pré-requisito em
`docs/design/wireframes-autoria-curso.md` (seção 7): uma linha própria no cabeçalho e a seção
numerada no fim do documento.

**`docs/design/wireframes-videos.md`**, em desktop e celular:

- **Formulário de envio:** o campo opcional de tags abaixo do título, com etiquetas removíveis e a
  lista de sugestões aberta. Estados de erro com o texto exato: tag vazia, mais de 30 caracteres,
  11ª tag. A tag equivalente não duplica.
- **Lista da tela Vídeos:** as tags de cada linha (vídeo sem tags, com 1 tag e com 10 tags,
  incluindo como a linha se comporta quando as etiquetas não cabem). Onde fica a ação de editar as
  tags.
- **Edição de tags:** a superfície de edição (diálogo ou equivalente), com sugestão, remoção, Salvar
  e Cancelar, erro de servidor (422) e o retorno após salvar. Remover todas as tags.
- **Filtro por tags:** junto dos filtros de estado e título; tags escolhidas como etiquetas; a
  escolha **E / OU**, visível só com 2 ou mais tags e com **E** indicado como padrão. A mensagem de
  lista vazia com filtro de tag e a ação de limpar os filtros. O filtro mantido enquanto a lista se
  atualiza sozinha.

**`docs/design/wireframes-autoria-curso.md`** (seletor de vídeo da aula):

- O filtro por tags ao lado da busca por título, com a mesma escolha E/OU.
- As tags exibidas em cada vídeo do seletor.
- A mensagem "nenhum vídeo pronto atende aos filtros" com a ação de limpar.

**Notas de acessibilidade do PRD:** campo e sugestões operáveis só por teclado (setas, Enter ou
vírgula para confirmar, Esc para fechar); rótulo acessível de remoção ("Remover tag docker"); E/OU
com estado anunciado.

Cada documento traz no cabeçalho a linha
`> **Adendo tags de vídeo:** ASCII aprovado pelo responsável em AAAA-MM-DD (…)`, no formato que o
gate confere, depois da aprovação explícita do responsável.

## Fora do escopo desta task

- Figma → 2.0. Código de tela → 3.0 a 7.0.
- Gestão das tags da escola (renomear, mesclar, excluir em massa): fora do PRD (DP-04).

## Decisões fechadas

- Tag livre com sugestão; tag nova nasce no uso (DP-01).
- Tags opcionais no envio e editáveis na tela Vídeos por quem tem `midia.enviar` (DP-02, RN-T04).
- Filtro nas duas telas, E/OU escolhível, **E** padrão (DP-03).
- Limites: até 10 tags por vídeo, de 1 a 30 caracteres (DP-05, RN-T03).
- O seletor só oferece vídeos prontos (RN-C04).

## Modificar / Referenciar

- **modificar:** `docs/design/wireframes-videos.md` (seção de adendo e linha no cabeçalho); `docs/design/wireframes-autoria-curso.md` (seção de adendo e linha no cabeçalho)
- **ref:** `docs/design/wireframes-autoria-curso.md` § 7 (formato de adendo e registro de aprovação); `DESIGN.md`; `docs/design/Components.md`; `prd.md` (Experiência do Usuário, RF-01 a RF-06); `techspec.md` (Bloco Frontend); `api-contract.md` (códigos `TAG_INVALID` e `TAGS_LIMIT_EXCEEDED`)

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| Documentação | — | Sem check automatizado para documento de design; o gate estático é a evidência | AGENTS.md (não há lint de Markdown no CI) |

## Pronto quando

- [ ] Gate passa (exit 0).
- [ ] Todas as telas e estados listados estão desenhados em desktop e celular, com o texto exato.
- [ ] O responsável aprovou o ASCII, e a aprovação está registrada no cabeçalho dos dois documentos com a data.
