# ADR-0015: Autenticação de serviço de `media` e `learning` em `commerce` com um escopo por rota, e a lista de cursos do aluno só para `learning` (revisa a ADR-0012)

## Status

Accepted

## Identidade e escopo

- Caminho: `docs/adr/0015-escopos-de-servico-media-learning-em-commerce-por-rota.md`
- Domínios/componentes afetados: `commerce` (módulo Matrícula e Direito de Acesso, como destino), `learning` (como chamador), configuração de emissores de `commerce` nos três ambientes de compose
- Origem histórica: `tasks/archive/prd-progresso-aluno`, `CAP-017` (decisões C-02 e C-03 do conjunto de contratos)
- Substitui: [ADR-0012](0012-autenticacao-de-servico-media-learning-em-commerce-revisada.md). A ADR-0012 passa a `Superseded by ADR-0015` quando esta for aceita. Os itens 1, 3, 4, 5 e 6 são mantidos; o item 2 muda.

## Data

2026-10-04

## Contexto

A ADR-0012 fez de `media` e `learning` emissores de asserção de serviço em `commerce`, com um **escopo único**, `access-decision:read`, para a pergunta "este aluno pode acessar este curso agora?" (`decideAccessInternal`).

`CAP-017` cria a tela "meus cursos": a lista dos cursos sobre os quais o aluno tem ou teve concessão, vigentes e encerrados. A lista é de Matrícula (RN-D01). Quem a monta é `learning`, que junta a ela o título da versão vigente e o progresso do aluno (contrato C-02 de `CAP-017`). Para isso `learning` chama uma rota nova de `commerce`, `listStudentCourseAccessInternal` (`GET /internal/v1/course-access`).

Essa rota responde outra pergunta e expõe mais do que a decisão: dado um aluno, **todos** os cursos dele e quando terminaram. Com escopo único, qualquer emissor que pode decidir acesso também poderia listar cursos, e `media` não tem motivo para isso.

## Decisão

1. **Emissores e mecanismo, como na ADR-0012.** `media` e `learning` assinam asserção RS256 com chave própria, `aud` = `commerce`, `jti` único consumido em Valkey. *(Sem mudança.)*
2. **Um escopo por rota, e cada emissor só recebe os escopos de que precisa.**
   - `access-decision:read` → `decideAccessInternal`. Concedido a `media` e a `learning`.
   - `course-access:read` → `listStudentCourseAccessInternal`. Concedido **só a `learning`**.
   - Uma asserção com um escopo não autoriza a rota do outro (403 `SCOPE_DENIED`). O emissor `bff-student` não recebe nenhum dos dois, e o JWT de ator (ADR-0005) não autoriza nenhuma das duas rotas. *(Muda: era escopo único.)*
3. **A asserção prova só quem é o chamador**, e o `studentId` vai na consulta, obtido de um JWT de aluno validado (ADR-0003, ADR-0013). Vale para as duas rotas. *(Sem mudança.)*
4. **Credenciais**: as de `media` e `learning` já existem desde `CAP-007`; esta decisão só acrescenta um escopo à entrada de `learning`. Nenhum par de chaves novo. *(Sem mudança de chaves.)*
5. **Cache.** A decisão segue com cache de até 30 s no consumidor e indisponível nunca guardado. **A lista de cursos não é guardada pelo consumidor** (`Cache-Control: private, no-store`): ela muda com o tempo (RN-D06) e não pode virar réplica do direito (G11). Abrir aula continua exigindo a decisão. *(Acrescenta a regra da lista.)*
6. **Sem cache da asserção.** Cada chamada assina uma asserção nova, nas duas rotas. *(Sem mudança.)*

## Alternativas Consideradas

### Alternativa 1: Reusar `access-decision:read` na rota da lista

- **Prós:** nenhuma mudança de configuração nos emissores.
- **Contras:** `media` ganharia, sem precisar, a capacidade de listar os cursos de qualquer aluno da escola.
- **Por que rejeitada:** contraria o menor privilégio que a separação de emissores existe para garantir.

### Alternativa 2: O BFF do aluno chamar `commerce` com um JWT de aluno de audiência `commerce`

- **Prós:** `commerce` veria o próprio aluno no token, sem `studentId` na consulta.
- **Contras:** Identity passaria a emitir para `commerce` (mudança na ADR-0013), e o BFF passaria a compor Matrícula com progresso e a ordenar a tela, regra que é de Aprendizagem.
- **Por que rejeitada:** move regra para o BFF e abre uma audiência nova só para uma leitura.

### Alternativa 3: `commerce` publicar fatos de concessão e `learning` manter réplica dos cursos do aluno

- **Prós:** "meus cursos" sem chamada síncrona a `commerce`.
- **Contras:** cria uma segunda fonte do direito (RN-D01, G11) e uma vigência calculada fora do dono.
- **Por que rejeitada:** o baseline proíbe réplica do direito.

## Consequências

### Positivas

- Cada emissor tem exatamente o que usa; `media` não lista cursos.
- A rota nova reaproveita o mecanismo, as chaves e o Valkey de `jti` existentes.

### Negativas

- Mais uma entrada de escopo por ambiente na configuração de `commerce` (compose local, remoto e Coolify).
- O chamador `learning` passa a assinar asserções com escopos diferentes conforme a rota.

### Riscos

- **Escopo errado na asserção de `learning`** (hoje o escopo é fixo no código que assina). Mitigação: o escopo vira parâmetro da emissão; testes de integração de `commerce` recusam `access-decision:read` na lista e `course-access:read` na decisão.
- **Configuração esquecida num ambiente.** Mitigação: o smoke de "meus cursos" no ambiente de desenvolvimento falha com 403 `SCOPE_DENIED`, visível como 503 na tela.

## Notas de Implementação

- `ServiceAssertionScopes` de `commerce` ganha `course-access:read`; a política da rota nova exige esse escopo.
- Configuração: `ServiceAssertions__Issuers__learning__AllowedScopes__1: course-access:read` nos três arquivos de compose.
- O contrato vigente está em `contracts/commerce/openapi-internal.yaml` (`commerce` 1.3.0), promovido em 2026-10-04 a partir do recorte histórico `tasks/archive/prd-progresso-aluno/internal-api-contract-commerce.yaml`.

## Referências

- [ADR-0012](0012-autenticacao-de-servico-media-learning-em-commerce-revisada.md), [ADR-0013](0013-jwt-de-aluno-validado-por-servicos-de-dominio.md), [ADR-0009](0009-autenticacao-de-servico-bff-aluno-em-servicos-de-dominio.md)
- `context/architecture-baseline.md`: BA07, G11
- `domains/matricula-e-direito-de-acesso/domain.md`: RN-D01, RN-D06, RN-D07
