# ADR-0004: Autenticação de serviço entre BFF do aluno e Identity

## Status

Accepted

## Identidade e escopo

- Caminho: `docs/adr/0004-autenticacao-de-servico-bff-identity.md`
- Domínios/componentes afetados: borda BFF do aluno, serviço Identity, chamadas pré-login e emissão de JWT interno
- Origem histórica: `tasks/prd-conta-aluno`, CAP-001
- Substitui: Nenhuma

## Data

2026-09-22

## Contexto

O SPA acessa apenas o BFF. Cadastro, confirmação, pedido de recuperação e login ocorrem antes de existir sessão de aluno; ainda assim, Identity precisa distinguir chamadas do BFF de tráfego arbitrário. Depois do login, Identity é o único emissor do JWT interno e o BFF não pode criar identidade de aluno por conta própria. O baseline define JWT de usuário curto, validado localmente via JWKS, mas não define autenticação da chamada BFF → Identity antes do login. A fundação registra secret manager e serviços isolados; não foi encontrado provisionamento de certificados de cliente para mTLS.

## Decisão

O BFF do aluno autentica cada chamada interna a Identity com uma **asserção de serviço assimétrica e curta**, assinada por chave privada exclusiva do BFF e entregue pelo secret manager. Identity valida assinatura, `kid`, emissor, audiência, escopo, validade e identificador único da asserção. A credencial de serviço prova somente qual serviço chamou, nunca a identidade do aluno. Chamadas com asserção ausente, inválida ou fora de escopo são rejeitadas antes do caso de uso. Chaves públicas ficam configuradas em Identity com período de sobreposição para rotação. O transporte interno usa TLS.

Após validar credencial de aluno e situação da conta, somente Identity emite JWT interno de usuário, curto e com audiência do destino. BFF guarda esse JWT apenas no lado servidor e nunca o inclui no corpo/cookie do SPA. A autorização do serviço chamador e a autenticação do aluno são verificações distintas. O OpenAPI interno BFF → Identity deve descrever a asserção, seus escopos, a emissão/renovação do JWT, erros, idempotência e versionamento antes da implementação. Esta ADR não decide a política de acesso do BFF de backoffice nem de outros clientes futuros.

## Alternativas consideradas

### Segredo estático compartilhado

- **Prós:** configuração e verificação simples.
- **Contras:** servidor e cliente conhecem o mesmo segredo, rotação exige coordenação rígida, e vazamento de qualquer lado permite impersonar o BFF.
- **Por que rejeitada:** aumenta o raio de comprometimento numa interface que emite identidade de aluno.

### mTLS com certificado de workload

- **Prós:** forte autenticação de transporte, sem token de serviço em header.
- **Contras:** exige emissão, distribuição, renovação e verificação de certificados de workload; esse mecanismo não está documentado na fundação atual.
- **Por que rejeitada nesta etapa:** cria dependência operacional não encontrada no projeto. Pode ser adotado depois como camada adicional, com decisão própria.

## Consequências

### Positivas

- Identity autentica chamadas pré-login sem fingir que existe sessão de aluno.
- A chave privada permanece apenas no BFF; Identity recebe somente material público para verificar.
- A emissão do JWT de usuário permanece exclusiva de Identity.

### Negativas

- Exige distribuição e rotação de chaves, escopos e relógios sincronizados.
- O contrato interno e seus testes passam a ser pré-requisito de deploy coordenado das duas pontas.

### Riscos

- **Reuso de asserção interceptada:** TLS interno, validade curta, `jti` e proteção contra replay na janela da asserção; nenhuma asserção em log/trace.
- **Rotação falha:** manter período de sobreposição de chaves públicas, testar `kid` antigo e novo e remover o antigo só após confirmar a transição.
- **Confusão serviço/aluno:** testes devem rejeitar asserção de serviço usada como JWT de usuário e JWT de usuário usado como autenticação do BFF.

## Notas de implementação

- Configuração de chave e audiência deve ser validada no início e vir do secret manager no ambiente de deploy.
- A interface interna é responsabilidade conjunta de BFF e Identity; o OpenAPI público da SPA não a substitui.
- Nenhum endpoint de emissão de JWT interno deve ser acessível diretamente do navegador.

## Referências

- [Baseline arquitetural](../../context/architecture-baseline.md) — BA05/BA06, autenticação e JWKS.
- [ADR-0002](0002-plataforma-de-runtime-coolify.md) — gestão de segredos no runtime.
- [ADR-0003](0003-verificacao-de-sessao-do-aluno.md) — verificação de sessão entre BFF e Identity.
- [TechSpec de origem](../../tasks/prd-conta-aluno/techspec.md) — contrato e fatias de CAP-001.
