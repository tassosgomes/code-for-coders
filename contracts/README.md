# Contratos vigentes

Este diretório é a **fonte da verdade** dos contratos de integração do sistema: um documento
completo por fronteira, organizado pelo serviço que o **provê** (HTTP) ou pela **aplicação**
descrita (AsyncAPI). O código, os testes de contrato e os novos PRDs partem daqui.

Os contratos que cada PRD escreveu continuam em `tasks/archive/prd-*/` apenas como histórico do
acordo daquela entrega. Eles são recortes, podem estar superados e **não** descrevem o sistema
atual.

## Regra de mudança

1. Um PRD novo trabalha em `tasks/prd-[slug]/` e usa o arquivo daqui como contrato anterior.
   O recorte do PRD (`api-contract.yaml`, `asyncapi-contract*.yaml` etc.) registra só o que muda.
2. Ao concluir o PRD, o recorte aprovado é **promovido** para o arquivo da fronteira aqui:
   juntar operações, canais e componentes; subir `info.version`; acrescentar a linha de origem
   em `info.description`; validar (abaixo); atualizar a tabela deste README.
3. Só então a pasta do PRD vai para `tasks/archive/`. Nada em `tasks/archive/` é editado depois.
4. Mudança de contrato fora de um PRD entra por PR direto neste diretório.

Testes de contrato leem os arquivos daqui, nunca de `tasks/` (ver "Conformidade do código").

## Fronteiras

| Arquivo | Versão | Fronteira | Origem (`tasks/archive/`) |
|---|---|---|---|
| `bff-admin/openapi-acesso-interno.yaml` | 1.4.0 | `admin-spa` → `bff-admin`: sessão, convite, papéis, área financeira | `prd-acesso-interno/api-contract.yaml` 1.4.0 |
| `bff-admin/openapi-midia.yaml` | 1.1.1 | `admin-spa` → `bff-admin`: vídeos | `prd-ingestao-midia/api-contract.yaml` 1.1.1 |
| `bff-admin/openapi-auditoria.yaml` | 1.2.0 | `admin-spa` → `bff-admin`: trilha de auditoria | `prd-consulta-trilha-auditoria/api-contract.yaml` 1.2.0 |
| `bff-admin/openapi-autoria.yaml` | 1.1.0 | `admin-spa` → `bff-admin`: autoria, nível e pré-requisito | `prd-autoria-curso/api-contract.yaml` 1.0.0 + `prd-nivel-prerequisito-curso/api-contract.yaml` 1.1.0 |
| `bff-admin/openapi-catalogo.yaml` | 1.0.0 | `admin-spa` → `bff-admin`: fichas e ofertas | `prd-vitrine-oferta/api-contract.yaml` 1.0.0 |
| `bff-admin/openapi-cortesias.yaml` | 1.0.0 | `admin-spa` → `bff-admin`: cortesias | `prd-concessao-acesso/api-contract.yaml` 1.0.0 |
| `bff-student/openapi-conta.yaml` | 1.0.0 | `student-spa` → `bff-student`: conta e sessão | `prd-conta-aluno/api-contract.yaml` 1.0.0 |
| `bff-student/openapi-vitrine.yaml` | 1.0.0 | `student-spa` → `bff-student`: vitrine | `prd-vitrine-oferta/api-contract-student.yaml` 1.0.0 |
| `bff-student/openapi-aula.yaml` | 1.0.0 | `student-spa` → `bff-student`: tela da aula, reprodução protegida e progresso | `prd-reproducao-protegida/api-contract.yaml` 1.0.0 |
| `identity/openapi-internal.yaml` | 1.4.0 | `bff-admin`/`commerce` → `identity` | `prd-acesso-interno/internal-api-contract.yaml` 1.4.0 + `prd-consulta-trilha-auditoria/internal-api-contract-identity.yaml` 1.1.0 + `prd-concessao-acesso/internal-api-contract-identity.yaml` 1.4.0 |
| `identity/openapi-internal-student.yaml` | 1.1.0 | `bff-student` → `identity` | `prd-conta-aluno/internal-api-contract.yaml` 1.0.0 + `prd-reproducao-protegida/internal-api-contract-identity.yaml` 1.1.0 |
| `commerce/openapi-internal.yaml` | 1.2.0 | `bff-admin`/`bff-student`/serviços → `commerce` | `prd-acesso-interno/internal-api-contract-commerce.yaml` 1.0.0 + `prd-vitrine-oferta/internal-api-contract-commerce.yaml` 1.1.0 + `prd-concessao-acesso/internal-api-contract-commerce.yaml` 1.2.0 |
| `learning/openapi-internal.yaml` | 1.2.0 | `bff-admin`/`bff-student`/serviços → `learning` | `prd-autoria-curso/internal-api-contract-learning.yaml` 1.0.0 + `prd-nivel-prerequisito-curso/internal-api-contract-learning.yaml` 1.1.0 + `prd-reproducao-protegida/internal-api-contract-learning.yaml` 1.1.0 |
| `media/openapi-internal.yaml` | 1.1.0 | `bff-admin`/`bff-student` → `media` | `prd-ingestao-midia/internal-api-contract.yaml` 1.0.1 + `prd-reproducao-protegida/internal-api-contract-media.yaml` 1.1.0 |
| `audit/openapi-internal.yaml` | 1.2.0 | `bff-admin` → `audit` | `prd-consulta-trilha-auditoria/internal-api-contract-audit.yaml` 1.2.0 |
| `identity/asyncapi.yaml` | 1.1.0 | aplicação `identity` | `prd-conta-aluno/asyncapi-contract.yaml` 1.0.0 + `prd-acesso-interno/asyncapi-contract.yaml` 1.1.0 |
| `notification/asyncapi.yaml` | 1.1.0 | aplicação `notification` | `prd-notificacao-transacional/asyncapi-contract.yaml` 1.0.0 + `prd-acesso-interno/asyncapi-contract-notification.yaml` 1.1.0 |
| `audit/asyncapi.yaml` | 1.4.0 | aplicação `audit` | `prd-trilha-auditoria` 1.0.1 + `prd-consulta-trilha-auditoria` 1.1.0 + `prd-autoria-curso` (audit) 1.2.0 + `prd-vitrine-oferta` (audit) 1.3.0 + `prd-concessao-acesso` (audit) 1.4.0 |
| `media/asyncapi.yaml` | 1.2.0 | aplicação `media` | `prd-ingestao-midia/asyncapi-contract.yaml` 1.0.0 + `prd-autoria-curso/asyncapi-contract-media.yaml` 1.1.0 + `prd-reproducao-protegida/asyncapi-contract.yaml` 1.2.0 |
| `learning/asyncapi.yaml` | 1.1.0 | aplicação `learning` | `prd-autoria-curso/asyncapi-contract.yaml` 1.0.0 + `prd-nivel-prerequisito-curso/asyncapi-contract.yaml` 1.1.0 |
| `commerce/asyncapi.yaml` | 1.1.0 | aplicação `commerce` | `prd-vitrine-oferta/asyncapi-contract.yaml` 1.0.0 + `prd-concessao-acesso/asyncapi-contract.yaml` 1.1.0 |

Sem contrato por decisão de desenho: `prd-observabilidade-midia` (Kibana e alertas, nenhuma
interface nova).

Um AsyncAPI referencia a mensagem de outra aplicação pelo arquivo dela aqui (por exemplo,
`commerce/asyncapi.yaml` → `../learning/asyncapi.yaml#/components/messages/VersaoPublicada`).

## Consolidação de 2026-10-04 (CAP-007)

- Promovidos os contratos de `tasks/archive/prd-reproducao-protegida` (PR #152): novo `bff-student/openapi-aula.yaml` (1.0.0); consolidação aditiva de entrega ao aluno em `media/openapi-internal.yaml` (1.1.0); leitura de aula em `learning/openapi-internal.yaml` (1.2.0); audiências autorizadas e claim `email` em `identity/openapi-internal-student.yaml` (1.1.0); fato `midia.reproducao-avancou.v1` em `media/asyncapi.yaml` (1.2.0).
- Exceção Spectral C-08 formalizada em `contracts/.spectral-exception.yaml` (estendendo o ruleset padrão da skill) para permitir respostas de entrega HLS (`application/vnd.apple.mpegurl` e `application/octet-stream`).

## Consolidação de 2026-10-03

- Os recortes de cada fronteira foram juntados em ordem cronológica; onde dois recortes
  divergiam, prevaleceu o mais novo (todas as divergências estruturais eram evoluções
  aditivas: campos obrigatórios novos, valores de enum novos).
- O BFF do backoffice continua com um arquivo por área: cada área tem permissão própria nas
  respostas compartilhadas (`PermissionDenied`, `StaffSessionCookie`), e um arquivo único
  exigiria reescrever esses componentes.
- `identity/openapi-internal-student.yaml` fica separado porque `SessionValidated` e
  `SessionValidationInput` do aluno e da equipe têm formas diferentes.
- Exemplos herdados de autoria 1.0.0 em `bff-admin/openapi-autoria.yaml` e
  `learning/openapi-internal.yaml` ganharam `currentLevel`/`level: null` e
  `prerequisite: {text: null, recommendedCourses: []}`, o valor de ausência que 1.1.0 documenta,
  porque 1.1.0 tornou esses campos obrigatórios.
- `identity/asyncapi.yaml`: conta-aluno e acesso-interno usavam a mesma chave de mensagem no
  canal `notificacao.envio-solicitado.v1`. O canal declara as duas mensagens
  (`pedidoDeEnvioSolicitado`, do aluno, e `pedidoDeEnvio`, da equipe), e cada operação aponta a sua.
- Os `.md` derivados dos PRDs não foram trazidos: são documentação gerada a partir do recorte e
  não representam o arquivo consolidado. O YAML é a fonte.

## Conformidade do código

Toda mensagem que um serviço publica é validada, nos testes de integração, contra o AsyncAPI daqui:
o teste captura o payload real (linha do outbox ou mensagem no broker) e chama
`{Servico}Messages.AssertSends(routingKey, payload)`. O helper compartilhado
(`src/contract-testing/AsyncApiContract.cs`, importado por `ContractTesting.props`) acha a operação
`send` do canal com esse endereço, resolve os `$ref` (inclusive entre arquivos) e valida o payload
com JSON Schema.

| Aplicação | Mensagens cobertas |
|---|---|
| `identity` | `identidade.conta-criada`, `identidade.conta-confirmada`, `identidade.senha-redefinida`, `notificacao.envio-solicitado` (aluno e equipe), `auditoria.ato-praticado` |
| `notification` | `notificacao.mensagem-entregue`, `notificacao.entrega-falhou` |
| `media` | `midia.ativo-pronto`, `midia.preparacao-falhou`, `midia.reproducao-avancou` |
| `learning` | `conteudo.versao-publicada`, `auditoria.ato-praticado` |
| `commerce` | `catalogo.oferta-*`, `matricula.acesso-*`, `auditoria.ato-praticado` |

`audit` só consome. Mudança em `contracts/` dispara o CI desses serviços.

O HTTP não tem comparação automática com o OpenAPI gerado pelo código; alguns testes
validam respostas pontuais contra o schema daqui (`AccessDecisionContract`,
`StudentAccountLookupContract`, testes da `admin-spa`).

## Validação

```bash
npx --yes @stoplight/spectral-cli lint "contracts/**/openapi*.yaml" \
  --ruleset contracts/.spectral-exception.yaml --fail-severity=error
npx --yes @asyncapi/cli validate contracts/<app>/asyncapi.yaml
```

Resultado em 2026-10-04: Spectral 6.17.0 com `contracts/.spectral-exception.yaml`, 15 documentos OpenAPI sem erros nem avisos;
`@asyncapi/cli` 3.x/6.x, 6 documentos AsyncAPI sem erros.
