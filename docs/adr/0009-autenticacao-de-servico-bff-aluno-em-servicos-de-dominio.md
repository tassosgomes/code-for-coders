# ADR-0009: Autenticação de serviço do BFF do aluno em serviços de domínio, para leituras e escritas públicas

## Status

Accepted

## Identidade e escopo

- Caminho: `docs/adr/0009-autenticacao-de-servico-bff-aluno-em-servicos-de-dominio.md`
- Domínios/componentes afetados: `bff-student` e serviços de domínio que ele chama sem sessão de aluno (primeiro: `commerce`, módulo Catálogo), Valkey do serviço de domínio
- Origem histórica: `tasks/archive/prd-vitrine-oferta`, CAP-003 (decisão C-04 do conjunto de contratos)
- Substitui: Nenhuma. Estende a [ADR-0004](0004-autenticacao-de-servico-bff-identity.md), que decidiu só a chamada BFF → Identity e declarou não decidir "outros clientes futuros"

## Data

2026-09-30

## Contexto

A ADR-0004 fixou como o `bff-student` se autentica em Identity: asserção de serviço assimétrica e curta, com `kid`, emissor, audiência, escopo, `jti` e chave privada exclusiva do BFF. A ADR-0005 estendeu o mesmo mecanismo ao `bff-admin` e adotou JWT de ator para chamadas do backoffice a serviços de domínio.

A vitrine pública de cursos (primeira superfície anônima de leitura de catálogo) faz o `bff-student` chamar `commerce` **sem sessão de aluno**: não há JWT de usuário para validar, e o visitante não é identificado. Duas operações são públicas (ler a vitrine e a página do curso) e uma é escrita anônima (contar um clique em *Comprar*). O baseline manda que o SPA fale só com o BFF (G15), que toda consulta carregue o tenant (G07) e que a borda filtre mas não proteja sozinha. Sem autenticação do chamador, qualquer processo com acesso à rede interna poderia ler o catálogo de qualquer escola informando um `tenantId` ou inflar a contagem de cliques. Nenhum serviço de domínio autentica hoje o `bff-student`: a verificação de asserção existe só em Identity, com audiência única e um conjunto fixo de escopos.

## Decisão

1. O `bff-student` autentica **cada chamada pública a um serviço de domínio** com uma asserção de serviço, no mesmo mecanismo da ADR-0004: JWS RS256, `iss` e `sub` = `bff-student`, `aud` = identificador do **serviço destino** (`commerce`), `scope` com os escopos da operação, `tenantId` da escola do BFF, `iat`/`nbf`/`exp` com vida de até 60 segundos e `jti` único por chamada. A asserção prova apenas qual serviço chamou; nunca representa visitante, aluno ou sessão.
2. **Chave, `kid` e escopos por destino.** O `bff-student` usa um par de chaves próprio para cada audiência de serviço de domínio, distinto do par usado para Identity. O destino guarda só as chaves públicas do emissor, com período de sobreposição para rotação, e a lista de `tenantId` que o emissor pode representar.
3. **Verificação no serviço destino**, antes do caso de uso: assinatura, `kid`, emissor, audiência, janela de validade, escopo exigido pela operação (declarado no emissor e no token), `tenantId` permitido e **`jti` inédito**, consumido em Valkey do próprio destino com TTL até `exp`. Qualquer falha é recusada sem efeito: asserção inválida → 401, escopo insuficiente → 403. O tenant do contexto do destino vem da asserção verificada, não de parâmetro da requisição.
4. **Separação dos esquemas.** A asserção do `bff-student` não autoriza rota de ator interno, e o JWT de ator (ADR-0005) não autoriza rota pública do BFF do aluno. Cada rota declara um único esquema de autenticação.
5. **Rotas anônimas no BFF.** O BFF do aluno trata as rotas públicas como anônimas: ignora o cookie de sessão se presente, não o valida nem o renova, e não exige CSRF (não há sessão a proteger). A proteção do BFF para escrita anônima é idempotência por clique e limite de taxa **particionado pelo recurso**, nunca por IP ou identificador de navegador (G26, G10).
6. A decisão vale para o `bff-student` chamando serviços de domínio em rotas anônimas. Chamadas com sessão de aluno continuam como decididas nas ADR-0003 e ADR-0004 (JWT de usuário com audiência do destino).

## Alternativas Consideradas

### Alternativa 1: Confiar na rede interna, sem autenticar o chamador

- **Descrição:** `commerce` aceita as rotas públicas de quem alcançar a porta interna, lendo o tenant de um cabeçalho.
- **Prós:** nenhuma chave nova, nenhum verificador novo.
- **Contras:** qualquer processo interno lê catálogo de outra escola ou infla contagens; o tenant passa a ser informado pelo chamador.
- **Por que rejeitada:** viola G07 (tenant decidido pelo dono) e repete o raciocínio da ADR-0004: autenticar o serviço chamador é barato e fecha o raio de comprometimento.

### Alternativa 2: Reutilizar o par de chaves e a asserção usados para Identity

- **Descrição:** uma única chave do `bff-student` para todos os destinos, distinguidos só pela audiência.
- **Prós:** um segredo a menos por ambiente.
- **Contras:** vazamento da chave compromete todos os destinos; escopos por destino ficam acoplados.
- **Por que rejeitada:** mesma lógica da ADR-0005 (alternativa 3): superfícies com risco diferente não compartilham credencial. O custo é um segredo a mais por destino.

### Alternativa 3: mTLS com certificado de workload

- **Descrição:** autenticação de transporte entre BFF e serviço.
- **Prós:** autenticação forte sem token em cabeçalho.
- **Contras:** a fundação não documenta emissão e renovação de certificados de workload (mesma constatação da ADR-0004).
- **Por que rejeitada nesta etapa:** cria dependência operacional não encontrada no projeto. Pode entrar depois como camada adicional.

### Alternativa 4: JWT de aluno

- **Descrição:** exigir conta para ver a vitrine.
- **Por que rejeitada:** a persona Visitante avalia antes de ter conta (decisão de produto do PRD de CAP-003, RN-O13); não é alternativa técnica para um consumidor anônimo.

## Consequências

### Positivas

- Serviços de domínio autenticam o `bff-student` e decidem o tenant a partir de credencial verificada.
- O mecanismo é o já conhecido pela equipe (ADR-0004); o verificador é cópia do padrão de Identity, sem biblioteca compartilhada nova (serviços têm deploy independente, ADR-0001).
- Comprometer a chave de um destino não abre os demais.

### Negativas

- Um par de chaves e uma configuração de emissor por serviço destino: mais segredos a distribuir e rotacionar.
- Cada leitura pública soma uma escrita em Valkey no destino (consumo do `jti`) e uma assinatura RSA no BFF.
- Verificadores duplicados por serviço (um por destino) divergem se não houver teste de conformidade.

### Riscos

- **Indisponibilidade do Valkey do destino derruba leituras públicas.** Falha fechada: o destino responde erro e o BFF devolve 502 do recorte; a verificação de saúde (`readiness`) do destino já depende do Valkey.
- **Reuso de asserção interceptada:** TLS interno, vida de até 60 segundos, `jti` consumido uma vez, nenhuma asserção em log, span ou métrica.
- **Rotação falha:** sobreposição de chaves públicas, teste com `kid` antigo e novo, remoção do antigo só após a transição.
- **Confusão entre esquemas:** testes recusam asserção em rota de ator e JWT de ator em rota pública.

## Notas de Implementação

- Configuração do emissor no destino (chaves públicas por `kid`, escopos permitidos, `tenantId` permitidos) e chave privada do BFF vêm do secret manager do ambiente (ADR-0002) e são validadas na partida.
- O verificador do destino usa o mesmo conjunto de verificações de `ServiceAssertionVerifier` de Identity (`src/identity/src/CodeForCoders.Identity.Api/Security/ServiceAssertionVerifier.cs`) como referência de comportamento.
- O OpenAPI interno do destino descreve a asserção, os escopos e os erros; a operação pública do BFF não os repete.

## Referências

- [ADR-0001](0001-monorepo-de-codigo.md), [ADR-0019](0019-runtime-e-deploy-atual.md), [ADR-0003](0003-verificacao-de-sessao-do-aluno.md), [ADR-0004](0004-autenticacao-de-servico-bff-identity.md), [ADR-0005](0005-sessao-e-servico-do-backoffice.md).
- [Baseline arquitetural](../../context/architecture-baseline.md) — G07, G10, G15, G26; BA05.
- Origem histórica: `tasks/archive/prd-vitrine-oferta/techspec.md` e `tasks/archive/prd-vitrine-oferta/contracts.md` (C-04).
