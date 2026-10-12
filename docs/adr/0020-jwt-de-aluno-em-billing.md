# ADR-0020: JWT de aluno validado por `billing`, para o aluno ver e baixar a própria nota

## Status

Accepted

## Identidade e escopo

- Caminho: `docs/adr/0020-jwt-de-aluno-em-billing.md`
- Domínios/componentes afetados: `identity` (audiência e escopo novos do JWT de aluno), `billing` (módulo Fiscal, validação), `bff-student` (rota da nota → audiência)
- Origem histórica: `tasks/prd-emissao-nfse`, `CAP-016` (decisão C-04 do conjunto de contratos)
- Substitui: Nenhuma. Estende a [ADR-0013](0013-jwt-de-aluno-validado-por-servicos-de-dominio.md), no precedente da [ADR-0016](0016-jwt-de-aluno-em-commerce.md)

## Data

2026-10-12

## Contexto

A nota fiscal da compra avulsa mora em `billing`, módulo Fiscal (baseline BA11). O aluno baixa o PDF e o XML do próprio pedido. A regra é a mesma do pedido: documento de outro aluno é indistinguível de inexistente, e o serviço dono decide quem é o aluno.

`billing` hoje só autentica a asserção de serviço de `commerce` (ADR-0017) e a assinatura do webhook do Stripe. Não valida JWT de aluno nem de ator. O `bff-student` já pede o JWT com a audiência da rota (ADR-0013, item 6): `/api/v1/orders` vai para `commerce`. A nota fica sob esse prefixo (`/api/v1/orders/{orderId}/fiscal-document`), então o mapeamento por prefixo mais longo entregaria um JWT de audiência `commerce` a uma rota cujo dono é `billing`.

O ator interno também consulta a nota, com audiência `billing` e permissão no token. Isso já é a ADR-0005 e não se decide de novo aqui.

## Decisão

1. **Identity emite JWT de aluno com `audience: billing`** e `scope: fiscal-document:own` (`StudentSessionTokens:AudienceScopes`). **Sem a claim `email` e sem a claim `permissions`.** Vida, claims e chave são os da ADR-0013. A audiência `billing` não entra em `EmailAudiences`.
2. **`billing` valida o JWT de aluno com o JWKS de Identity** (emissor `identity`, audiência `billing`, RS256, sem tolerância de relógio). Não há esquema de autenticação novo.
3. **Separação aluno × ator por política, como na ADR-0013 (item 3) e na ADR-0016 (item 3).** As rotas de aluno exigem `scope` igual a `fiscal-document:own` **e ausência da claim `permissions`**. Token de ator nessas rotas responde 403. As rotas do financeiro exigem a permissão do claim (`financeiro.ler` na leitura, `fiscal.gerir` na escrita) e recusam token sem `permissions`.
4. **O aluno é a claim `sub`.** Nenhuma rota de aluno aceita identificador de aluno por parâmetro, corpo ou cabeçalho. Pedido cujo aluno não é `sub` responde 404, o mesmo código de documento inexistente.
5. **O `bff-student` pede audiência `billing` nas rotas da nota**, mesmo estando sob `/api/v1/orders`. O prefixo `/api/v1/orders` continua `commerce` para pedido, pagamento e lista. A exceção é do mesmo tipo da que já manda `playback-sessions` para `media`.

O JWT de ator com audiência `billing` continua a ADR-0005: o `bff-admin` pede essa audiência só nas rotas fiscais; pedido e lista do financeiro continuam em `commerce`. Identity precisa de `StaffSessionTokens:AudienceScopes:billing` para conseguir emitir. O valor de escopo desse token de ator é `fiscal-documents:use`. `billing` não autoriza o ator por esse escopo: autoriza pela claim `permissions`.

## Alternativas Consideradas

### Alternativa 1: O BFF informa `studentId` e `billing` confia

- **Descrição:** a nota usaria a asserção de serviço do BFF, com o aluno no corpo.
- **Prós:** nenhuma audiência nova.
- **Contras:** `billing` passaria a confiar no BFF quanto a quem baixa a nota.
- **Por que rejeitada:** é o desenho que a ADR-0016 recusou para o pedido.

### Alternativa 2: Reusar o JWT de audiência `commerce` dentro de `billing`

- **Descrição:** `billing` aceitaria o token que o aluno já usa para ver o pedido.
- **Prós:** o prefixo `/api/v1/orders` não precisaria de exceção.
- **Contras:** um token de pedido abriria arquivo fiscal; a audiência deixaria de dizer o serviço dono.
- **Por que rejeitada:** a ADR-0013 emite um token por audiência. A nota não é de `commerce`.

## Consequências

### Positivas

- O arquivo sai só para o aluno assinado por Identity, sem `billing` consultar Identity a cada download.
- O e-mail do aluno não entra no JWT de `billing`.

### Negativas

- Mais uma audiência de aluno e uma de ator para configurar em cada ambiente.
- A tabela de rotas do `bff-student` precisa da exceção sob `/api/v1/orders`, senão a nota recebe o token errado.

### Riscos

- **Token de `commerce` ou de ator aceito na rota de aluno.** Mitigação: testes dos dois sentidos em todas as rotas de aluno e do financeiro.
- **Audiência `billing` ausente na configuração.** Mitigação: o emissor recusa audiência desconhecida e o BFF falha fechado (502), no mesmo comportamento das audiências que já existem.

## Notas de Implementação

- Configuração de aluno: `StudentSessionTokens__AudienceScopes__billing: fiscal-document:own`. Não acrescentar `billing` a `EmailAudiences`.
- Configuração de ator: `StaffSessionTokens__AudienceScopes__billing: fiscal-documents:use`.
- A exceção de rota no `bff-student` vale para os caminhos que terminam em `/fiscal-document`, `/fiscal-document/pdf` e `/fiscal-document/xml`.

## Referências

- [ADR-0005](0005-sessao-e-servico-do-backoffice.md), [ADR-0013](0013-jwt-de-aluno-validado-por-servicos-de-dominio.md), [ADR-0016](0016-jwt-de-aluno-em-commerce.md), [ADR-0017](0017-autenticacao-de-servico-commerce-em-billing.md)
- Origem histórica: `tasks/prd-emissao-nfse/contracts.md` (C-04)
