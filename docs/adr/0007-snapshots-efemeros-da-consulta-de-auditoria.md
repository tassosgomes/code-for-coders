# ADR-0007: Snapshots efêmeros da consulta de Auditoria

## Status

Accepted

## Identidade e escopo

- Caminho: `docs/adr/0007-snapshots-efemeros-da-consulta-de-auditoria.md`
- Domínios/componentes afetados: Auditoria e Conformidade, serviço `audit`, infraestrutura Valkey
- Origem histórica: `tasks/archive/prd-consulta-trilha-auditoria`, CAP-030
- Substitui: Nenhuma

## Data

2026-09-27

Decisão aprovada pelo responsável pela feature em 2026-09-27.

## Contexto

A consulta administrativa precisa percorrer todos os registros elegíveis sem repetições ou perdas,
inclusive quando novos atos são recebidos com `praticado_em` anterior aos registros já vistos. O
contrato HTTP fixa os IDs elegíveis na primeira página e devolve um identificador opaco de snapshot.
Os registros de Auditoria são evidência imutável: o banco de `audit` só admite `SELECT` e `INSERT`
do processo, e sua escrita de evidência ocorre apenas pelo consumidor de fatos. Uma tabela de
paginação atualizada pela consulta não pertence a essa fronteira.

O baseline e a ADR-0002 já entregam Valkey como infraestrutura para dados temporários dos BFFs;
esta decisão amplia seus consumidores ao `audit` sem torná-lo fonte de verdade. A lista de IDs de uma
investigação não autoriza nenhuma decisão de negócio, pode ser descartada e reconstruída por nova
consulta, e não contém motivo, explicação ou rótulo de Identidade.

## Decisão

O serviço `audit` usa Valkey para guardar, com expiração absoluta de 30 minutos, a lista ordenada
dos IDs de registros originais selecionados pela primeira consulta. A seleção ocorre numa única
visão consistente do PostgreSQL; a resposta só é entregue depois de a lista completa e seus
metadados estarem disponíveis no Valkey. O token público é aleatório e opaco. O registro temporário
inclui tenant, sessão administrativa, filtros normalizados, tamanho de página e total. Cada página
revalida JWT, papel, tenant, sessão, filtros e tamanho antes de recortar os IDs armazenados. O
conteúdo dos registros continua vindo do PostgreSQL, nunca do Valkey.

O namespace e a credencial de `audit` no Valkey são separados dos BFFs. A expiração não é
prorrogada por leitura. Snapshot expirado, perdido ou incompatível retorna o erro de filtro do
contrato; a interface inicia uma nova busca e informa que a visão anterior expirou. Falha ao criar
o snapshot não devolve uma página que prometa continuidade. O volume e a idade das visões são
observados para dimensionar a capacidade; pressão de memória é falha explícita, não paginação
silenciosamente instável.

## Alternativas Consideradas

### Alternativa 1: Cursor com limite de momento ou UUID

- **Descrição:** fixar um valor máximo de ordenação no primeiro pedido e consultar páginas abaixo dele.
- **Prós:** custo de memória constante e nenhuma dependência nova de Valkey no `audit`.
- **Contras:** um ato recebido depois com momento do ato retroativo pode cair dentro do intervalo
  congelado e mudar os IDs elegíveis; UUID e commit concorrente também não garantem a mesma visão.
- **Por que rejeitada:** não cumpre o acordo de IDs fixos sob inserções concorrentes.

### Alternativa 2: Tabela de snapshots no banco de Auditoria

- **Descrição:** materializar IDs e metadados numa tabela própria para cada busca.
- **Prós:** continuidade durante reinícios do cache e transação próxima à fonte.
- **Contras:** a consulta escreveria e apagaria estado no banco de evidência, exigindo privilégios
  incompatíveis com a restrição append-only e com a escrita somente por consumo de fatos.
- **Por que rejeitada:** enfraquece a fronteira de custódia para sustentar um estado descartável.

## Consequências

### Positivas

- Inserções posteriores, mesmo retroativas, não mudam a sequência de IDs navegada.
- A evidência continua somente no PostgreSQL e a consulta não ganha permissão de mutá-la.

### Negativas

- `audit` passa a depender de Valkey para criar e continuar páginas estáveis.
- Uma busca ampla consome memória proporcional ao número de IDs até expirar; perda do cache exige reiniciar a busca.

### Riscos

- A lista de IDs permite correlacionar registros. Restringir namespace/credencial, incluir tenant e
  sessão no valor, usar token aleatório, expirar em 30 minutos e não guardar nomes ou texto livre.
- Uma busca ampla pode pressionar Valkey. Medir tamanho e quantidade de snapshots, alertar antes da
  saturação e falhar sem devolver página incompleta.

## Notas de Implementação

O cache contém IDs e metadados de paginação, nunca cópia do Registro de Auditoria. A ordem é
`coalesce(praticado_em, recebido_em)` decrescente, depois ID decrescente. O intervalo informado
filtra somente `praticado_em`, inclusive as duas bordas; `null` fica fora de intervalo informado.
O snapshot fixa IDs, não a indicação `hasComplements`, que pode mudar quando um complemento novo
chega. A liberação automática usa TTL absoluto, conforme [EXPIRE no Valkey](https://valkey.io/commands/expire/).

## Referências

- [Baseline arquitetural](../../context/architecture-baseline.md) — BA03, G07, G13 e cache descartável.
- [ADR-0002](0002-plataforma-de-runtime-coolify.md) — Valkey na plataforma, sem ser fonte de verdade.
- [Domínio Auditoria](../../domains/auditoria-e-conformidade/domain.md) — RN-A01, RN-A04, RN-A09 e RN-A13.
- [ADR-0005](0005-sessao-e-servico-do-backoffice.md) — revalidação de sessão e JWT de `audit`.
- [Contrato de consulta](../../tasks/archive/prd-consulta-trilha-auditoria/contracts.md) — C-01, origem histórica.
