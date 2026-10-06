---
status: done
task_kind: enabling
blocked_by: []
gate: 'rg -q "^> \*\*Status:\*\* (ASCII aprovado|ASCII e Figma aprovados) pelo responsável em [0-9]{4}-[0-9]{2}-[0-9]{2}" docs/design/wireframes-compra.md'
gate_expect: "exit 0: o cabeçalho do documento de wireframes da compra registra a aprovação do ASCII pelo responsável"
---

# 1.0 Wireframes ASCII da compra, do pedido e dos pedidos no backoffice aprovados

**Fatia:** EN-02 (ASCII) · **Cobre:** RF-01, RF-02, RF-05, RF-06, RF-10, RF-11 (desenho) · **Spec:** `techspec.md` § Habilitadores inevitáveis, § Bloco Frontend · **ADR:** —

## Comportamento

Existe `docs/design/wireframes-compra.md`, no formato de `docs/design/wireframes-cortesias.md` (cabeçalho com Status, Figma, Handoff, Objetivo e Fontes), com o desenho ASCII, em desktop e celular, de:

- **Página do curso** (`/cursos/:courseId`, aprovada em `CAP-003`): o que muda no botão *Comprar* de cada opção — deixa de abrir o aviso "em breve" e leva ao resumo.
- **Resumo da compra** (`/comprar/:offerId`): curso, opção, preço, vigência ("acesso por 12 meses a partir da liberação" / "acesso vitalício"), o aviso de acesso existente ("Você já tem acesso a este curso até DD/MM/AAAA (cortesia)"), o aviso de ida ao ambiente seguro de pagamento, *Ir para o pagamento*, e os estados: opção indisponível, pedido pendente existente (leva ao pedido), pagamento indisponível com *Tentar de novo*.
- **Pedido** (`/pedidos/:orderId`), em todos os estados do PRD (RF-05, RF-06): "Confirmando pagamento" (e o aviso de demora após 2 min), "Compra confirmada" com *Ir para o curso*, "Liberando seu acesso", "Aguardando pagamento do boleto"/"do PIX" com prazo e *Ver boleto*/*Ver código PIX*, aguardando sem meio com *Continuar pagamento* e *Desistir*, *Desistir e pagar de outra forma* com confirmação, "Expirado" e "Cancelado" com *Comprar de novo*, e pedido não encontrado.
- **Meus pedidos** (`/pedidos`): lista com curso, opção, valor, data, meio e situação, o pendente em destaque com prazo, o vazio com caminho para a vitrine, e onde o link aparece na navegação da conta.
- **Backoffice, área financeira** (`/financeiro` e `/financeiro/pedidos/:orderId`): lista com número, data, aluno (nome e e-mail), curso, opção, valor, meio e situação; filtros por situação, curso, período e e-mail do aluno (com o retorno "nenhuma conta de aluno com este e-mail"); paginação; detalhe com vigência, momentos, referência do gateway e concessão; estados vazio, sem permissão e indisponível. O que acontece com a tela "Área reservada" atual.

Cada estado traz o texto exato que o aluno ou o financeiro lê (PRD, Experiência do Usuário e critérios de RF-01 a RF-11) e as notas de acessibilidade do PRD: mudança para "Compra confirmada" anunciada a leitor de tela; situação nunca só por cor. O cabeçalho tem a linha `> **Status:**` com o registro de aprovação do responsável, no formato que o gate confere.

## Fora do escopo desta task

- Figma → 2.0. Código de tela → 4.0 a 7.0, 9.0, 10.0.
- O comprovante por e-mail: o texto é do modelo de Notificação (8.0), não de tela.
- A página de pagamento: é do Stripe (DP-01); o documento só registra o que o aluno vê antes de ir e ao voltar.

## Decisões fechadas

- Checkout hospedado do Stripe; a plataforma não desenha a página de pagamento (DP-01).
- O aviso de acesso existente informa e nunca bloqueia (DP-08, RN-V11).
- *Desistir* existe para pedido pendente e cria a nova compra pelas condições vigentes (DP-04).
- A vigência é sempre "a partir da liberação do acesso" (RN-D04).
- Backoffice só leitura: nenhuma ação sobre pedido (DP-09).

## Modificar / Referenciar

- **ref:** `docs/design/wireframes-cortesias.md` (formato e registro de aprovação); `docs/design/wireframes-catalogo-vitrine.md` (página do curso e opções de compra aprovadas); `docs/design/wireframes-conta-aluno.md` (navegação da conta); `docs/design/wireframes-progresso.md` (Início "meus cursos", destino de *Ir para o curso*); `DESIGN.md`; `docs/design/Components.md`; `prd.md` (Experiência do Usuário, RF-01 a RF-11); `techspec.md` (§ Bloco Frontend, § URLs públicas)

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| Documentação | — | Sem check automatizado para documento de design; o gate estático é a evidência | AGENTS.md (não há lint de Markdown no CI) |

## Pronto quando

- [ ] Gate passa (exit 0).
- [ ] Todas as telas e estados listados estão desenhados em desktop e celular, com o texto exato.
- [ ] O responsável aprovou o ASCII, e a aprovação está registrada no cabeçalho com a data.
