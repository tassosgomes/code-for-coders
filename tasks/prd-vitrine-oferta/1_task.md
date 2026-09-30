---
status: pending
task_kind: enabling
blocked_by: []
gate: 'rg -q "^> \*\*Status:\*\* (ASCII aprovado|ASCII e Figma aprovados) pelo responsável em [0-9]{4}-[0-9]{2}-[0-9]{2}" docs/design/wireframes-catalogo-vitrine.md'
gate_expect: "exit 0: o cabeçalho do documento de wireframes do Catálogo e da vitrine registra a aprovação do ASCII pelo responsável"
---

# 1.0 Wireframes ASCII do Catálogo e da vitrine aprovados

**Fatia:** EN-01 (ASCII) · **Cobre:** Experiência do Usuário do PRD; RF-02, RF-03, RF-04, RF-05, RF-07, RF-08, RF-09, RF-10 (desenho) · **Spec:** `techspec.md#bloco-frontend` · **ADR:** —

## Comportamento

Um documento de wireframes em `docs/design/wireframes-catalogo-vitrine.md` desenha, com todos os estados
(carregando, vazio, erro, sem permissão, mobile 390 px), as telas que a TechSpec prevê:

- **Backoffice (`admin-spa`, design system do backoffice):** lista do Catálogo (título, nível ou "Sem
  nível", na vitrine, contagem de ofertas por estado, paginação); ficha do curso (título, nível e
  pré-requisito em leitura com o link para a Autoria condicionado a `autoria.ler`, aviso permanente de
  nível nulo com a criação de rascunho ainda habilitada, chamada comercial com contador de 160 e a
  orientação de RN-O06, lista de ofertas com rascunhos primeiro, estado, preço, vigência e "N cliques em
  Comprar"); formulário de oferta (preço digitado em reais com duas casas, vigência "Por período" de 1 a
  60 meses ou "Vitalícia", erro no campo nomeado em `FIELD_INVALID`); confirmações de publicar (o cartão
  exato que o visitante verá), de alterar preço ou vigência de oferta publicada (antes/depois e a frase
  "vale para compras futuras e não altera quem já comprou"), de despublicar e de excluir rascunho; rótulos,
  opções de filtro, formatação dos atributos e "Não se aplica a este tipo" do motivo dos três atos na
  consulta da trilha.
- **Área pública (`student-spa`, `DESIGN.md` e Figma do design system, OD3):** vitrine (filtro de nível como
  grupo de opções com rótulo e estado anunciado, cartões, "a partir de R$ X" quando houver mais de uma
  oferta, estado vazio que oferece "Todos", paginação de 12); página do curso (nível, descrição,
  "Recomendamos saber antes" com link só para recomendado na vitrine, estrutura sem vídeo, uma opção de
  compra por oferta com vigência e preço em texto); aviso "compra em breve", focável e anunciado;
  tela "este curso não está disponível" com link para a vitrine; layout público com marca e links para
  entrar e cadastrar.
- Nenhum texto de tela promete exclusividade de conteúdo ou proteção contra cópia (RN-O06).

O cabeçalho do documento registra `> **Status:** ASCII aprovado pelo responsável em <AAAA-MM-DD>` — escrito
só depois da aprovação explícita do responsável. Pedido de ajuste volta ao ASCII antes do registro.

## Fora do escopo desta task

Figma (2.0) e qualquer código. Fluxos de checkout, pedido e pagamento (`CAP-011`).

## Decisões fechadas

- Fluxo de design: wireframe ASCII → Figma → aprovação → código de tela (PRD, Experiência do Usuário).
- Rotas e endereços públicos seguem `techspec.md#arquitetura-da-solução`: `/student/cursos`,
  `/student/cursos/{courseId}` (filtro `?nivel=`), `/admin/catalogo`, `/admin/catalogo/{courseId}`.
- Vigência sempre com o ponto de partida ("contados a partir da liberação"); preço em reais com centavos,
  sem parcelamento; o aviso de compra aparece independentemente do resultado da contagem.

## Modificar / Referenciar

- **ref:** `prd.md` (Experiência do Usuário, RF-02 a RF-10); `techspec.md#bloco-frontend`; `api-contract.md`
  e `api-contract-student.md` (campos e erros); `docs/design/wireframes-autoria-curso.md` e
  `docs/design/wireframes-auditoria.md` (formato do documento e do registro de aprovação);
  `docs/design/Components.md`; `DESIGN.md`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `docs/design` | — | Sem check automatizado; a aprovação é do responsável | Fluxo de design do backoffice (CAP-002) |

## Pronto quando

- [ ] Gate passa (exit 0).
- [ ] O documento cobre todas as telas e estados acima, em desktop e mobile 390 px.
- [ ] O responsável aprovou o ASCII explicitamente.
