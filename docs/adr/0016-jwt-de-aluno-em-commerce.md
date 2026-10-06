# ADR-0016: JWT de aluno validado por `commerce`, para o aluno comprar e ver os próprios pedidos

## Status

Accepted

## Identidade e escopo

- Caminho: `docs/adr/0016-jwt-de-aluno-em-commerce.md`
- Domínios/componentes afetados: `identity` (audiência e escopo novos do JWT de aluno), `commerce` (módulo Vendas, validação), `bff-student` (rota → audiência)
- Origem histórica: `tasks/archive/prd-compra-avulsa`, `CAP-011` (decisão C-03 do conjunto de contratos)
- Substitui: Nenhuma. Estende a [ADR-0013](0013-jwt-de-aluno-validado-por-servicos-de-dominio.md), que fixou o JWT de aluno por audiência para `media` e `learning`

## Data

2026-10-05

## Contexto

Até aqui, o aluno só chegava a `commerce` de forma anônima: a vitrine e o clique em *Comprar* usam a asserção de serviço do `bff-student` (ADR-0009), que não representa ninguém. A compra muda isso. Criar pedido, ir ao pagamento, desistir e listar os próprios pedidos são ações **de um aluno**, e a regra "o pedido é do aluno que comprou; pedido de outro é indistinguível de inexistente" precisa valer em `commerce`, o dono do pedido, sem confiar no BFF para dizer quem é o comprador.

`commerce` já valida localmente, pelo JWKS de Identity, o JWT de **ator interno** com audiência `commerce` (ADR-0005), e o JWKS de Identity já publica a chave de aluno (ADR-0013). O que falta é decidir a audiência, o escopo e a separação entre aluno e ator nas rotas de `commerce`.

## Decisão

1. **Identity emite JWT de aluno com `audience: commerce`** e `scope: orders:use` (`StudentSessionTokens:AudienceScopes`). **Sem a claim `email`**: ela continua exclusiva da audiência `media` (ADR-0013, item 4). Vida, claims e chave são os da ADR-0013.
2. **`commerce` valida o JWT de aluno com o mesmo esquema que já usa para o ator** (emissor `identity`, audiência `commerce`, JWKS de Identity, RS256, sem tolerância de relógio). Não há esquema de autenticação novo.
3. **Separação aluno × ator por política, como na ADR-0013 (item 3).** As rotas de aluno de `commerce` exigem `scope` igual a `orders:use` **e ausência da claim `permissions`**. As rotas do backoffice continuam exigindo a permissão que exigem (`financeiro.ler`, `cortesia.conceder`, `oferta.editar`), que um token de aluno nunca carrega. Um token de ator numa rota de aluno é recusado com 403, o que também cumpre "ator interno não compra" (RN-V01).
4. **O comprador é a claim `sub`.** Nenhuma rota de aluno de `commerce` aceita identificador de aluno por parâmetro, corpo ou cabeçalho (ADR-0013, item 5).
5. **O `bff-student` pede o JWT com a audiência da rota**: `/api/v1/orders` e `/api/v1/offers` → `commerce`, na tabela de rotas que já existe (ADR-0013, item 6). As rotas da vitrine continuam anônimas, com a asserção de serviço da ADR-0009.

## Alternativas Consideradas

### Alternativa 1: Asserção do `bff-student` com o `studentId` no corpo

- **Descrição:** estender a asserção de serviço da ADR-0009 com um escopo de escrita de pedido e deixar o BFF informar o aluno.
- **Prós:** nenhuma audiência nova em Identity.
- **Contras:** `commerce` passaria a confiar no BFF quanto a quem compra; um defeito no BFF cria pedido em nome de outro aluno ou expõe os pedidos dele.
- **Por que rejeitada:** contraria a ADR-0005 e a ADR-0013, em que o serviço dono decide pela identidade assinada por Identity.

### Alternativa 2: Esquema de autenticação separado para o aluno em `commerce`

- **Descrição:** um segundo esquema JWT só para tokens de aluno.
- **Prós:** isolamento explícito por esquema.
- **Contras:** dois esquemas com o mesmo emissor, a mesma audiência e o mesmo JWKS; a separação real continua dependendo de `scope` e `permissions`.
- **Por que rejeitada:** complexidade sem ganho de segurança; `learning` e `media` já separam por política.

## Consequências

### Positivas

- O pedido nasce com o comprador assinado por Identity, sem salto síncrono a Identity por requisição em `commerce`.
- `commerce` nunca recebe o e-mail do aluno.

### Negativas

- Mais uma audiência a configurar em cada ambiente (compose local, override remoto e Coolify).
- A separação aluno × ator em `commerce` depende de política e precisa de teste nos dois sentidos.

### Riscos

- **Token de ator aceito em rota de aluno, ou o contrário.** Mitigação: testes de integração nos dois sentidos em todas as rotas de aluno e do backoffice de pedidos.
- **Audiência esquecida num ambiente.** Mitigação: o BFF já falha fechado sem token (502); o smoke de homologação cobre a criação de pedido.

## Notas de Implementação

- Configuração em Identity: `StudentSessionTokens__AudienceScopes__commerce: orders:use`, nos três arquivos de compose.
- Configuração no `bff-student`: `BffSecurity:RouteAudiences` ganha `/api/v1/orders` e `/api/v1/offers` → `commerce`.
- O contrato vigente está em `contracts/identity/openapi-internal-student.yaml` (Identity 1.2.0) e `contracts/commerce/openapi-internal.yaml` (`commerce` 1.4.0, `StudentUserToken`), promovidos em 2026-10-06 a partir dos recortes históricos `tasks/archive/prd-compra-avulsa/internal-api-contract-identity-student.yaml` e `internal-api-contract-commerce.yaml`.

## Referências

- [ADR-0003](0003-verificacao-de-sessao-do-aluno.md), [ADR-0005](0005-sessao-e-servico-do-backoffice.md), [ADR-0009](0009-autenticacao-de-servico-bff-aluno-em-servicos-de-dominio.md), [ADR-0013](0013-jwt-de-aluno-validado-por-servicos-de-dominio.md)
- Origem histórica: `tasks/archive/prd-compra-avulsa/contracts.md` (C-03)
