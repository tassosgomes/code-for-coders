# ADR-0012: Autenticação de serviço de `media` e `learning` em `commerce`, ambos como chamadores desde `CAP-007` (revisa a ADR-0011)

## Status

Accepted

## Identidade e escopo

- Caminho: `docs/adr/0012-autenticacao-de-servico-media-learning-em-commerce-revisada.md`
- Domínios/componentes afetados: `commerce` (módulo Matrícula e Direito de Acesso, como destino), `media` e `learning` (como chamadores), Valkey de `commerce`, provisionamento local de chaves
- Origem histórica: `tasks/prd-reproducao-protegida`, `CAP-007` (decisão C-02 do conjunto de contratos)
- Substitui: [ADR-0011](0011-autenticacao-de-servico-media-learning-em-commerce.md). A ADR-0011 passa a `Superseded by ADR-0012` quando esta for aceita. Os itens 1, 2, 3 e 5 são mantidos como estavam; o item 4 muda.

## Data

2026-10-03

## Contexto

A ADR-0011 decidiu que `media` e `learning` são emissores de asserção de serviço em `commerce` para consultar a decisão "este aluno pode acessar este curso agora?" (`decideAccessInternal`), e que **as credenciais só nascem quando existe chamador**: `media` em `CAP-007`, `learning` em `CAP-017`. A premissa era que `learning` só consultaria a decisão quando entregasse o progresso do aluno.

A tela da aula (`CAP-007`) muda isso. Ela mostra a lista de aulas do curso, que revela título de curso, módulo e aula. Quem não tem direito ao curso não pode ver nada disso, nem o título (RN-R07 do PRD). Quem serve a estrutura é `learning`, dono de Conteúdo e Currículo. Para não servir título a quem não tem direito, `learning` precisa **perguntar ao dono do direito antes de devolver a estrutura**, e a pergunta é a mesma decisão da ADR-0011.

As alternativas a isso põem a regra no lugar errado: o BFF decidir a ordem das chamadas (a regra de acesso passaria a depender de quem orquestra, e `learning` serviria estrutura a qualquer aluno autenticado que o chamasse), ou entregar a estrutura sem decisão. O baseline manda quem usa a decisão perguntar ao dono, sem intermediário (BA07, G11).

## Decisão

1. **`media` e `learning` são emissores de asserção de serviço em `commerce`**, no mecanismo da ADR-0004 e da ADR-0009: JWS RS256, `iss` e `sub` = identificador do serviço chamador, `aud` = `commerce`, `scope`, `tenantId`, `iat`/`nbf`/`exp` com vida de até 60 segundos, `jti` único por chamada e consumido uma vez em Valkey de `commerce`. **Cada chamador tem um par de chaves próprio**, distinto de qualquer outro usado para Identity ou para BFFs. *(Sem mudança.)*
2. **Escopo único: `access-decision:read`.** A rota da decisão aceita **apenas** asserção de serviço com esse escopo. O emissor `bff-student` não o recebe, e o JWT de ator (ADR-0005) não autoriza a rota. Cada rota declara um único esquema de autenticação (ADR-0009, item 4). *(Sem mudança.)*
3. **A asserção prova só quem é o chamador.** O `studentId` vai na consulta e **deve ter sido obtido pelo chamador de um JWT de aluno validado** (ADR-0003 e ADR-0013); Matrícula não valida o aluno, e o chamador não aceita identificador vindo do navegador. O `tenantId` do contexto de `commerce` vem da asserção verificada. *(Sem mudança.)*
4. **As credenciais de `media` e de `learning` nascem em `CAP-007`, juntas.** `media` consulta a decisão na abertura e na renovação de cada Sessão de Reprodução; `learning` consulta antes de devolver a aula e a estrutura do curso. Cada serviço recebe seu par de chaves e sua entrada de emissor em `commerce` nesta entrega. Nenhum outro serviço recebe credencial por antecipação. *(Muda: era `learning` em `CAP-017`.)*
5. **Resposta e cache.** O consumidor guarda a resposta por **até 30 segundos**, nunca além do término informado, com chave versionada que inclui a escola; sem resposta, **não libera** (BA07). **Decisão indisponível nunca é guardada** e nunca se confunde com decisão negada. A decisão não depende de Identity: `commerce` decide só com as concessões que ele mesmo guarda. *(Acrescenta a regra sobre indisponibilidade.)*
6. **Sem cache da asserção de serviço.** Cada consulta assina uma asserção nova (`jti` único). O custo (uma assinatura RSA no chamador e uma escrita de `jti` em Valkey no destino) fica restrito às consultas que passam pelo cache de decisão de até 30 s. Medir (latência da decisão e taxa de `jti` rejeitado) antes de qualquer otimização; reuso de asserção exige nova decisão.

## Alternativas Consideradas

### Alternativa 1: Manter a ADR-0011 e o BFF decidir a ordem

- **Descrição:** o BFF chamaria `media` para abrir a sessão e, só em caso de sucesso, `learning` para a estrutura, sem `learning` consultar o direito.
- **Prós:** `learning` não vira chamador de `commerce`; uma credencial a menos agora.
- **Contras:** a regra de acesso passa a viver na orquestração do BFF; `learning` serve título de aula a qualquer JWT de aluno que o alcance; `media` e `learning` ficam com garantias diferentes sobre o mesmo dado.
- **Por que rejeitada:** quem usa a decisão deve perguntar ao dono (BA07, G11), e a estrutura é conteúdo do curso.

### Alternativa 2: Servir a estrutura com a decisão dentro do próprio `media`

- **Descrição:** `media` devolveria também a lista de aulas.
- **Contras:** `media` passaria a conhecer título de curso, módulo e aula, contra RN-M02 (domínio cego a curso).
- **Por que rejeitada:** fronteira de domínio.

### Alternativa 3: Direito dentro do JWT do aluno

- **Descrição:** Identity incluiria no JWT os cursos a que o aluno tem direito.
- **Contras:** réplica do direito; suspensão e revogação deixariam de valer na próxima decisão; viola G11.
- **Por que rejeitada:** a fonte única é o dono.

## Consequências

### Positivas

- A decisão de acesso continua com um único dono, um único esquema de autenticação e escopo próprio.
- `learning` e `media` oferecem a mesma garantia: nada do curso sai sem decisão positiva.
- `CAP-017` encontra `learning` já provisionado.

### Negativas

- Um segundo par de chaves e um segundo emissor para rotacionar, antes do previsto.
- A abertura de uma aula soma duas decisões (a de `learning` na tela e a de `media` na sessão), cada uma com cache de até 30 s por chamador.

### Riscos

- **Chamador usa `studentId` sem JWT validado (confused deputy).** Mitigação: `media` e `learning` só aceitam `studentId` da claim `sub` do JWT de aluno que validaram, nunca de parâmetro; teste de arquitetura e de integração recusam outro caminho.
- **Indisponibilidade de Valkey ou de `commerce` bloqueia a tela e a reprodução.** Falha fechada por decisão do baseline; a mensagem ao aluno é a de indisponibilidade, não a de "sem acesso".
- **Reuso de asserção interceptada:** TLS interno, vida de até 60 segundos, `jti` consumido uma vez, nenhuma asserção em log, span ou métrica.
- **Confusão entre esquemas:** testes recusam asserção sem o escopo, asserção de `bff-student` e JWT de ator nesta rota.

## Notas de Implementação

- Escopos e emissores ficam em `ServiceAssertionOptions.Issuers` de `commerce`; `media` e `learning` entram ao lado de `bff-student`. O escopo `access-decision:read` já existe em `ServiceAssertionScopes`.
- Pares de chaves locais seguem o provisionamento do `bff-student`: `scripts/generate-local-env.sh` e `docker-compose.yml`.
- O OpenAPI interno de `commerce` não muda: `tasks/prd-concessao-acesso/internal-api-contract-commerce.yaml`, `decideAccessInternal`.

## Referências

- [ADR-0011](0011-autenticacao-de-servico-media-learning-em-commerce.md) — decisão que esta revisa.
- [ADR-0003](0003-verificacao-de-sessao-do-aluno.md), [ADR-0004](0004-autenticacao-de-servico-bff-identity.md), [ADR-0005](0005-sessao-e-servico-do-backoffice.md), [ADR-0009](0009-autenticacao-de-servico-bff-aluno-em-servicos-de-dominio.md), [ADR-0010](0010-autenticacao-de-servico-commerce-em-identity.md).
- [Baseline arquitetural](../../context/architecture-baseline.md) — BA07, G07, G10, G11.
- Origem histórica: `tasks/prd-reproducao-protegida/contracts.md` (C-02, C-12) e `tasks/prd-concessao-acesso/contracts.md` (C-04).
