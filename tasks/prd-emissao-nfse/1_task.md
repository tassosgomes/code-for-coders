---
status: pending
task_kind: enabling
blocked_by: []
gate: 'rg -q "^> \*\*Adendo NFS-e:\*\* (ASCII aprovado|ASCII e Figma aprovados) pelo responsável em [0-9]{4}-[0-9]{2}-[0-9]{2}" docs/design/wireframes-compra.md'
gate_expect: "exit 0: o cabeçalho de wireframes-compra.md registra a aprovação do adendo ASCII da NFS-e pelo responsável"
---

# 1.0 Adendo ASCII da nota no pedido do aluno, no pedido do financeiro e na tela do prestador

**Fatia:** EN-01 (ASCII) · **Cobre:** RF-02, RF-03, RF-05, RF-06, RF-07 (desenho) · **Spec:** `techspec.md#v-02`, `techspec.md#habilitadores-inevitáveis` · **ADR:** —

## Comportamento

`docs/design/wireframes-compra.md` ganha um adendo, sem reabrir o que CAP-011 já aprovou. O formato é o do adendo de tags em `docs/design/wireframes-videos.md`: uma linha própria no cabeçalho e uma seção no fim. A linha de `> **Status:**` que já existe não muda.

O adendo desenha, em desktop e celular, só o que esta entrega acrescenta. Situação nunca é só cor: ícone e texto.

**Pedido do aluno** (`/pedidos/:orderId`), quando o pedido está pago. A chamada do pedido continua a que já existe. A nota é uma segunda consulta; se ela falha, o pedido permanece e a seção diz que a nota não carregou.

- Pedido não pago: nenhuma menção a nota.
- *Aguardando tomador:* o texto "Informe um CPF, CNPJ ou NIF para emitirmos a nota deste pedido. O acesso ao curso não depende disso." O campo fica na própria página, com rótulo, e o curso continua pelo caminho que já existe. Dígito inválido ou NIF vazio: "Informe um CPF, CNPJ ou NIF válido. Nada foi salvo." O erro é texto, não só borda.
- *Nota em emissão* (`issuing`, que cobre prestador ainda não utilizável e nota na fila): "Nota em emissão". Sem prazo e sem pedir ação sobre o prestador.
- *Emitida* com arquivos: baixar PDF e baixar XML. Se `studyDocument`, a marca "sem valor fiscal" aparece junto dos botões, em texto, não só por cor.
- *Emitida* sem arquivos: "A nota foi emitida. O arquivo fica guardado por um ano e não está mais disponível." Sem botões de download e sem tratar isso como falha.
- *Falha:* "A nota não pôde ser emitida. O acesso ao curso não depende disso." Sem botão de tentar de novo e sem o motivo do emissor.
- Nota já emitida: o campo do documento continua, com "Este documento vale para a próxima compra. A nota já disponível não muda."

**Meus pedidos** (`/pedidos`): a lista continua a que já existe. Para os pagos da página, a situação da nota é outra consulta. Falha dessa consulta não esconde a lista. Pedido não pago não menciona nota.

**Pedido do financeiro** (`/financeiro/pedidos/:orderId`): o detalhe atual mais uma seção. 404 da nota: "Não há nota fiscal neste pedido." Outra falha: o pedido continua e a seção diz "A nota não carregou. O pedido continua disponível." Situações em texto: Aguardando tomador, Aguardando prestador, Na fila, Emitida, Falha. Motivo visível em Aguardando tomador e em Falha, vindo da resposta, sem código de prefeitura. Download de PDF e XML quando a nota está emitida e o arquivo existe. Sem arquivos: a mesma frase de um ano, sem download e sem nova tentativa. Nova tentativa só em Falha. Não há cancelar nota nem marcar emitida à mão. A seção não mostra CPF, CNPJ, NIF nem o e-mail como dado do tomador.

**Lista do financeiro** (`/financeiro`): a lista atual mais a situação vinda da consulta em lote. Pedido sem nota não ganha situação. Falha da consulta não esconde a lista.

**Prestador** (`/financeiro/prestador`): chave do emissor, código de serviço e alíquota, sem valor de fábrica. A chave digitada não volta depois do envio e não permanece preenchida. Prestador incompleto não aparece como utilizável. Sem `fiscal.gerir`, salvar responde 403 com texto. Professor e suporte continuam sem a área.

Cada frase acima é o texto a desenhar. A aprovação do responsável trava essas frases; pedido de ajuste volta a este adendo antes do Figma.

No cabeçalho, depois da aprovação explícita:

`> **Adendo NFS-e:** ASCII aprovado pelo responsável em AAAA-MM-DD`

## Fora do escopo desta task

- Figma → 2.0. Código de tela → 4.0 a 8.0.
- Cancelar nota, código de serviço por curso, nota de assinatura.

## Decisões fechadas

- A nota mora no pedido que o aluno já consulta; não há área nova (`prd.md`, Experiência do Usuário).
- *Nota em emissão* cobre prestador ausente e nota na fila, sem prazo (`techspec.md` § Bloco Frontend).
- Download autenticado; o e-mail não é desenhado aqui (DP-04).
- Sem valor de fábrica para código de serviço e alíquota (QA-02).
- A marca "sem valor fiscal" é texto junto dos botões (RN-F14).

## Modificar / Referenciar

- **modificar:** `docs/design/wireframes-compra.md` (seção de adendo e linha `Adendo NFS-e` no cabeçalho; a linha `Status` de CAP-011 permanece)
- **ref:** `docs/design/wireframes-videos.md` (formato de adendo e registro de aprovação); `DESIGN.md`; `docs/design/Components.md`; `prd.md` (Experiência do Usuário, RF-02, RF-03, RF-05, RF-06, RF-07); `techspec.md` § Bloco Frontend e § URLs públicas; `api-contract-student.md`; `api-contract-admin.md`; `api-contract-conta.md`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| Documentação | — | Sem check automatizado para documento de design; o gate estático é a evidência | Não há lint de Markdown no CI (`.github/workflows`) |

## Pronto quando

- [ ] Gate passa (exit 0).
- [ ] As três superfícies e os estados listados estão desenhados em desktop e celular, com o texto desta task.
- [ ] O pedido e a lista continuam visíveis quando a consulta da nota falha.
- [ ] O responsável aprovou o ASCII, e a aprovação está na linha `Adendo NFS-e` do cabeçalho, com a data.
