# Contratos consolidados — referência vigente

> Os contratos de planejamento em `tasks/prd-*/` **permanecem intocados** e são a origem
> histórica. Este diretório é a **referência vigente**: cópia fiel dos YAMLs mais novos de
> cada fronteira no momento da consolidação, organizada pelo **service dono**.
> Regra de mudança: qualquer alteração entra por PR neste diretório com o dono como
> codeowner; `tasks/` nunca mais é editado para fins de contrato.

Excluído propositalmente: `tasks/prd-vitrine-oferta/` (CAP-003, em construção).
Quando ele for mergeado, promove-se aqui: `commerce` 1.1.0, `audit` 1.3.0 e o que mais ele evoluir.

## Dono × arquivo × origem

| Dono (`src/`) | Arquivo canônico | Origem (cópia fiel) | Versão efetiva |
|---|---|---|---|
| `identity` | `identity/openapi-public.yaml` (+`.md`) | `tasks/prd-acesso-interno/api-contract.yaml` | 1.3.0 (já inclui `oferta.editar` da vitrine, C-10 aplicada) |
| `identity` | `identity/openapi-internal.yaml` (+`.md`) | `tasks/prd-acesso-interno/internal-api-contract.yaml` | 1.3.0 |
| `identity` | `identity/openapi-student-public.yaml` (+`.md`) | `tasks/prd-conta-aluno/api-contract.yaml` | 1.0.0 |
| `identity` | `identity/openapi-student-internal.yaml` (+`.md`) | `tasks/prd-conta-aluno/internal-api-contract.yaml` | 1.0.0 |
| `identity` | `identity/asyncapi-1.1.0.yaml` | `tasks/prd-acesso-interno/asyncapi-contract.yaml` | **efetivo p/ staff** (recorte CAP-002; não repete CAP-001) |
| `identity` | `identity/asyncapi-1.0.0.yaml` | `tasks/prd-conta-aluno/asyncapi-contract.yaml` | **efetivo p/ conta-aluno** (recorte CAP-001, complementar ao 1.1.0) |
| `notification` | `notification/asyncapi-1.1.0.yaml` | `tasks/prd-acesso-interno/asyncapi-contract-notification.yaml` | **efetivo** (contrato completo: 1.0.0 + `convite-interno`) |
| `notification` | `notification/asyncapi-1.0.0.yaml` | `tasks/prd-notificacao-transacional/asyncapi-contract.yaml` | base histórica |
| `audit` | `audit/asyncapi-1.2.0.yaml` | `tasks/prd-autoria-curso/asyncapi-contract-audit.yaml` | **efetivo** (aceita `conteudo/versao-publicada`; complemento 1.1.0 inalterado) |
| `audit` | `audit/asyncapi-1.1.0.yaml` | `tasks/prd-consulta-trilha-auditoria/asyncapi-contract.yaml` | canal de complemento |
| `audit` | `audit/asyncapi-1.0.1.yaml` | `tasks/prd-trilha-auditoria/asyncapi-contract.yaml` | base de ingestão |
| `audit` | `audit/openapi-public.yaml` (+`.md`) | `tasks/prd-consulta-trilha-auditoria/api-contract.yaml` | 1.2.0 |
| `audit` | `audit/openapi-internal.yaml` (+`.md`) | `tasks/prd-consulta-trilha-auditoria/internal-api-contract-audit.yaml` | 1.2.0 |
| `audit` | `audit/openapi-identity-lookup.yaml` (+`.md`) | `tasks/prd-consulta-trilha-auditoria/internal-api-contract-identity.yaml` | 1.0.0 |
| `media` | `media/openapi-public.yaml` (+`.md`) | `tasks/prd-ingestao-midia/api-contract.yaml` | 1.1.1 |
| `media` | `media/openapi-internal.yaml` | `tasks/prd-ingestao-midia/internal-api-contract.yaml` | 1.0.1 |
| `media` | `media/asyncapi-1.1.0.yaml` | `tasks/prd-autoria-curso/asyncapi-contract-media.yaml` | **efetivo** (1.0.0 + consome `conteudo.versao-publicada`) |
| `media` | `media/asyncapi-1.0.0.yaml` | `tasks/prd-ingestao-midia/asyncapi-contract.yaml` | base produtora |
| `learning` | `learning/openapi-public.yaml` (+`.md`) | `tasks/prd-nivel-prerequisito-curso/api-contract.yaml` | **efetivo** 1.1.0 (1.0.0 de autoria evoluído) |
| `learning` | `learning/openapi-internal.yaml` (+`.md`) | `tasks/prd-nivel-prerequisito-curso/internal-api-contract-learning.yaml` | **efetivo** 1.1.0 |
| `learning` | `learning/asyncapi-1.1.0.yaml` | `tasks/prd-nivel-prerequisito-curso/asyncapi-contract.yaml` | **efetivo** (fato com `description/level/prerequisite`) |
| `learning` | `learning/asyncapi-1.0.0.yaml` | `tasks/prd-autoria-curso/asyncapi-contract.yaml` | base |
| `commerce` | `commerce/openapi-internal-finance-1.0.0.yaml` | `tasks/prd-acesso-interno/internal-api-contract-commerce.yaml` | 1.0.0 (`finance-area`) |

Sem contrato por decisão de desenho: `prd-observabilidade-midia` (só Kibana/alertas, nenhum HTTP/mensageria novo).

## Leitura dos efetivos por fronteira

- **Borda `admin-spa → bff-admin`**: composição de `identity/openapi-public` (staff) + `media/openapi-public` + `learning/openapi-public` + `audit/openapi-public`. O BFF não tem contrato próprio; agrega por tags.
- **Borda `student-spa → bff-student`**: `identity/openapi-student-public` (vitrine adicionará o dela aqui depois).
- **Internos**: cada `openapi-internal`/`openapi-*-lookup` é o acordo `bff → serviço`.
- **Mensageria**: para cada app AsyncAPI, o arquivo de maior versão é o efetivo; os menores são base histórica preservada porque os recortes são complementares, não repetidos.

## Verificação

Todos os 23 YAMLs passam em `yaml.safe_load` (2026-09-30). Próximo passo sugerido:
teste de conformance no CI que quebre o build se endpoint/DTO/evento do código divergir
destes arquivos (mesmo padrão do `--verify-only` da observabilidade).
