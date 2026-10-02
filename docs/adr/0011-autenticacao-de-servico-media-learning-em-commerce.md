# ADR-0011: Autenticação de serviço de `media` e `learning` em `commerce`, para consultar a decisão de acesso

## Status

Accepted

## Identidade e escopo

- Caminho: `docs/adr/0011-autenticacao-de-servico-media-learning-em-commerce.md`
- Domínios/componentes afetados: `commerce` (módulo Matrícula e Direito de Acesso, como destino), `media` e `learning` (como chamadores futuros), Valkey de `commerce`
- Origem histórica: `tasks/prd-concessao-acesso`, CAP-008 (decisão C-04 do conjunto de contratos)
- Substitui: Nenhuma. Estende a [ADR-0009](0009-autenticacao-de-servico-bff-aluno-em-servicos-de-dominio.md) (chamadores: `bff-student`) e, por ela, a [ADR-0004](0004-autenticacao-de-servico-bff-identity.md), para chamadores que são **serviços de domínio**

## Data

2026-10-01

## Contexto

Matrícula e Direito de Acesso é a fonte única da resposta "este aluno pode acessar este curso agora?" (RN-D01, DE01). O baseline (BA07, G11) manda consultar o dono de forma síncrona, com cache de até 30 segundos no consumidor e falha fechada, e proíbe réplica local do direito. Os consumidores previstos são Mídia, antes de cada sessão de reprodução (`CAP-007`), e Aprendizagem (`CAP-017`), ambos serviços de domínio. Nenhum deles existe como chamador de `commerce` hoje.

`commerce` já verifica asserções de serviço de **um** emissor, `bff-student`, nas rotas públicas do Catálogo (ADR-0009), com `ServiceAssertionOptions.Issuers` aceitando vários emissores, cada um com chaves públicas por `kid`, escopos permitidos e tenants permitidos. A decisão de acesso não é uma rota de ator interno (JWT da ADR-0005) nem uma rota pública anônima: quem chama é um serviço, em nome de um aluno **que o próprio chamador já autenticou**.

## Decisão

1. **`media` e `learning` são emissores de asserção de serviço em `commerce`**, no mecanismo da ADR-0004 e da ADR-0009: JWS RS256, `iss` e `sub` = identificador do serviço chamador, `aud` = `commerce`, `scope`, `tenantId`, `iat`/`nbf`/`exp` com vida de até 60 segundos, `jti` único por chamada e consumido uma vez em Valkey de `commerce`. **Cada chamador tem um par de chaves próprio**, distinto de qualquer outro usado para Identity ou para BFFs.
2. **Escopo único: `access-decision:read`.** A rota da decisão aceita **apenas** asserção de serviço com esse escopo. O emissor `bff-student` não o recebe, e o JWT de ator (ADR-0005) não autoriza a rota. Cada rota declara um único esquema de autenticação (ADR-0009, item 4).
3. **A asserção prova só quem é o chamador.** O `studentId` vai na consulta e **deve ter sido obtido pelo chamador de uma sessão de aluno validada** (ADR-0003); Matrícula não valida o aluno, e o chamador não pode aceitar identificador vindo do navegador sem sessão. O `tenantId` do contexto de `commerce` vem da asserção verificada.
4. **Credenciais só existem quando existe chamador.** Esta entrega implementa e testa a verificação em `commerce` com um emissor de teste, **sem** provisionar chave de `media` nem de `learning` em ambiente nenhum. Cada serviço recebe seu par de chaves e sua entrada de emissor na entrega que passa a consultar a decisão (`CAP-007` para `media`, `CAP-017` para `learning`), seguindo esta ADR.
5. **Resposta e cache.** O consumidor guarda a resposta por **até 30 segundos**, nunca além do término informado, com chave versionada que inclui a escola; sem resposta, **não libera** (BA07). A decisão não depende de Identity: `commerce` decide só com as concessões que ele mesmo guarda.

## Alternativas Consideradas

### Alternativa 1: Direito dentro do JWT do aluno

- **Descrição:** Identity incluiria no JWT do aluno os cursos a que ele tem direito.
- **Prós:** nenhuma chamada síncrona.
- **Contras:** réplica do direito, com a validade do token no lugar do TTL de 30 s; suspensão e revogação deixariam de valer na próxima decisão; viola G11.
- **Por que rejeitada:** a fonte única é o dono, e o efeito imediato da revogação (`CAP-009`) é requisito do baseline.

### Alternativa 2: A decisão passa pelo `bff-student`

- **Descrição:** o BFF consulta `commerce` com a asserção da ADR-0009 e repassa o resultado a `media`.
- **Prós:** reaproveita o emissor existente.
- **Contras:** `media` passaria a confiar em uma resposta intermediada, e o BFF ganharia escopo sobre a decisão do direito; o caminho de reprodução teria mais um salto.
- **Por que rejeitada:** quem usa a decisão deve perguntar ao dono, sem intermediário (BA07, G11).

### Alternativa 3: Reutilizar a chave de um serviço para os dois chamadores

- **Descrição:** um único par de chaves para `media` e `learning`.
- **Contras:** o vazamento de uma abre as duas; escopos não se separam por serviço.
- **Por que rejeitada:** mesma lógica da alternativa 3 da ADR-0005 e da alternativa 2 da ADR-0009.

### Alternativa 4: Provisionar já as chaves de `media` e `learning`

- **Descrição:** deixar os emissores configurados antes dos consumidores.
- **Contras:** segredos sem uso, com rotação a operar e superfície de ataque sem benefício.
- **Por que rejeitada:** credencial ociosa é risco sem retorno; as chaves nascem com o primeiro chamador.

## Consequências

### Positivas

- A decisão de acesso tem um único dono, um único esquema de autenticação e escopo próprio.
- O verificador de `commerce` já aceita vários emissores; o trabalho é um escopo, uma política de rota e configuração.
- Nenhum segredo novo é provisionado até haver consumidor.

### Negativas

- Cada consulta soma uma escrita em Valkey no destino (consumo do `jti`) e uma assinatura RSA no chamador. No caminho quente da reprodução, isso pesa; a medição e a decisão de cache de asserção, se necessária, são de `CAP-007`.
- Mais um par de chaves por serviço chamador, a rotacionar.

### Riscos

- **Chamador usa `studentId` sem sessão validada (confused deputy).** A rota não valida o aluno. Mitigação: o contrato e esta ADR exigem o identificador de sessão validada; os testes de `CAP-007` e `CAP-017` verificam que a consulta só ocorre depois da validação da sessão.
- **Indisponibilidade de Valkey ou de `commerce` bloqueia a reprodução.** Falha fechada por decisão do baseline; o player degrada e não libera acesso novo.
- **Reuso de asserção interceptada:** TLS interno, vida de até 60 segundos, `jti` consumido uma vez, nenhuma asserção em log, span ou métrica.
- **Confusão entre esquemas:** testes recusam asserção sem o escopo, asserção de `bff-student` e JWT de ator nesta rota.

## Notas de Implementação

- Escopos e emissores ficam em `ServiceAssertionOptions.Issuers` de `commerce`; o escopo novo entra ao lado de `showcase:read` e `purchase-intent:write` em `ServiceAssertionScopes`.
- O OpenAPI interno de `commerce` descreve a operação, o esquema, o cache e os erros (`tasks/prd-concessao-acesso/internal-api-contract-commerce.yaml`, `decideAccessInternal`).

## Referências

- [ADR-0001](0001-monorepo-de-codigo.md), [ADR-0002](0002-plataforma-de-runtime-coolify.md), [ADR-0003](0003-verificacao-de-sessao-do-aluno.md), [ADR-0004](0004-autenticacao-de-servico-bff-identity.md), [ADR-0005](0005-sessao-e-servico-do-backoffice.md), [ADR-0009](0009-autenticacao-de-servico-bff-aluno-em-servicos-de-dominio.md), [ADR-0010](0010-autenticacao-de-servico-commerce-em-identity.md).
- [Baseline arquitetural](../../context/architecture-baseline.md) — BA07, G07, G10, G11.
- Origem histórica: `tasks/prd-concessao-acesso/techspec.md` e `tasks/prd-concessao-acesso/contracts.md` (C-04).
