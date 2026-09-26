---
tsg_artifact: contract
product: code-4-coders
capability: CAP-006
version: 1.0
status: approved
updated: 2026-09-25
sources: tasks/prd-ingestao-midia/prd.md@1.0, domains/entrega-de-midia-e-protecao/domain.md@1.0
---

# Contratos de integração — ingestão de mídia

> - PRD: [prd.md](prd.md), v1.0, aprovado em 2026-09-25
> - Data da revisão: 2026-09-25
> - Estado do conjunto: **Aprovado para implementação (1.0)** em 2026-09-25 — C-11 a C-15 aprovadas; os três documentos validados.

Este conjunto registra o acordo para o recorte de `CAP-006`. A aprovação significa acordo para implementar as interfaces descritas; não afirma implantação. Os contratos de `CAP-002` são preservados na pasta de origem; a única mudança neles é aditiva e está descrita em C-14.

## Seleção e escopo

O SPA do backoffice consome novas operações do BFF do backoffice, que repassa a Media com o JWT do ator (ADR-0005). Media passa a produzir dois fatos. Não há produto de dados com consumidores; ODCS não se aplica. A URL de parte assinada não é interface HTTP nossa: é escrita direta no armazenamento, descrita como comportamento em `createVideoUploadPartUrls`, e não ganha contrato próprio.

| Documento | Padrão e versão | Versão do contrato | Fronteira | Estado |
|---|---|---|---|---|
| [api-contract.yaml](api-contract.yaml) e [api-contract.md](api-contract.md) | OpenAPI 3.1.0 | `bff-admin` 1.1.0 | Recorte de CAP-006 na borda `admin-spa` → `bff-admin`: 8 operações novas; as de CAP-002 (1.0.0) não mudam e não são repetidas | **Aprovado para implementação** em 2026-09-25; lint sem erros |
| [internal-api-contract.yaml](internal-api-contract.yaml) | OpenAPI 3.1.0 | `media` 1.0.0 | `bff-admin` → Media: as mesmas 8 operações, com JWT de ator interno | **Aprovado para implementação** em 2026-09-25; lint sem erros |
| [asyncapi-contract.yaml](asyncapi-contract.yaml) | AsyncAPI 3.0.0 | `media` 1.0.0 | Media como **produtora** de `midia.ativo-pronto.v1` e `midia.preparacao-falhou.v1` | **Aprovado para implementação** em 2026-09-25; parser sem erros |

## Participantes e interfaces

| Identificador | Provedor/produtor | Consumidores | Comportamento | PRD |
|---|---|---|---|---|
| `createVideoUpload` | `bff-admin`, com decisão de Media | `admin-spa` | Recusa tamanho e formato antes do primeiro byte; mesma `fingerprint` do mesmo ator retoma (200) | RF-02, RF-03 |
| `listPendingVideoUploads`, `getVideoUpload` | `bff-admin`, com decisão de Media | `admin-spa` | Só envios do próprio ator; partes já recebidas, conferidas no armazenamento | RF-03 |
| `createVideoUploadPartUrls` | `bff-admin`, com decisão de Media | `admin-spa` | URLs opacas de escrita de uma parte, vida curta; cada emissão renova a validade do envio | RF-02, RF-03 |
| `completeVideoUpload` | `bff-admin`, com decisão de Media | `admin-spa` | Confere todas as partes; cria vídeo `received`; repetição devolve o mesmo vídeo | RF-02, RF-04 |
| `listVideos`, `getVideo` | `bff-admin`, com decisão de Media | `admin-spa` (lista e, em CAP-005, seletor da aula) | Todos os vídeos da escola, filtro por estado e título; sem endereço nem chave | RF-06, RF-08 |
| `updateVideoTitle` | `bff-admin`, com decisão de Media | `admin-spa` | Qualquer ator com a permissão, qualquer estado | RF-07 |
| `*Internal` (8 operações) | Media | `bff-admin` | Mesmas regras; ator = `sub`, escola = `tenantId` do JWT; `uploaderName` só para exibição | RF-01 a RF-08 |
| `publicarAtivoPronto` | Media pelo outbox | `learning` em CAP-005 (previsto) | Um fato por vídeo que chega a `ready`, com duração | RF-09 |
| `publicarPreparacaoFalhou` | Media pelo outbox | `learning` em CAP-005 (previsto) | Um fato por vídeo que chega a `failed`, com a categoria do motivo | RF-09 |

Relação entre modalidades:

| Transição | Origem | Mensagem (mesma transação) |
|---|---|---|
| envio → `received` | `completeVideoUpload` | nenhuma |
| `received` → `preparing` | preparação automática | nenhuma |
| `preparing` → `ready` | preparação automática | `publicarAtivoPronto` |
| `received`/`preparing` → `failed` | preparação, esgotadas as tentativas | `publicarPreparacaoFalhou` |
| título alterado | `updateVideoTitle` | nenhuma |

## Origem e decisões

| Entrada consultada | Versão e data | Decisão herdada |
|---|---|---|
| [PRD de CAP-006](prd.md) | v1.0, 2026-09-25 | RF-01 a RF-11, DP-01 a DP-06 |
| [Entrega de Mídia e Proteção](../../domains/entrega-de-midia-e-protecao/domain.md) | v1.0, 2026-09-25 | RN-M01 a RN-M07, RN-M17; OD30 (Referência de Uso, fora deste recorte) |
| [Baseline](../../context/architecture-baseline.md) | v1.2, 2026-09-21 | BA05, BA06, autorização em duas camadas, G06, G07, G09, G10, G21, camada anticorrupção |
| [ADR-0005](../../docs/adr/0005-sessao-e-servico-do-backoffice.md) | aceita em 2026-09-25 | Sessão validada em Identity a cada ação; JWT curto com `permissions`, validado por JWKS no serviço dono |
| [Contratos de CAP-002](../prd-acesso-interno/contracts.md) | 1.1, aprovados em 2026-09-25 | Cookie `staff_session`, `X-CSRF-Token`, `Idempotency-Key` 24 h, Problem com `code`/`traceId`, paginação `_page`/`_size` até 50, `StaffUserToken` e `TOKEN_INVALID` do documento de Commerce |
| [Contrato de Identity de CAP-001](../prd-conta-aluno/asyncapi-contract.yaml) | 1.0.0 | Forma dos fatos: canal `<dominio>.<fato>.v1`, `eventId`, `occurredAt`, `correlationId` no header, entrega ao menos uma vez com dedupe por `eventId` |

Decisões novas deste contrato, aprovadas em 2026-09-25:

| ID | Decisão | Alternativa descartada |
|---|---|---|
| C-11 | **Bytes direto ao armazenamento, em partes, por URL assinada de vida curta** emitida por Media via BFF. É a única exceção a "SPA só fala com o BFF" (BA05): a URL é opaca, só escreve uma parte e não serve para ler. Media confere as partes no armazenamento na consulta e na conclusão; o cliente não manipula comprovante do provedor. **Decidida pelo usuário em 2026-09-25** | Partes via `bff-admin` → Media: 5 GB atravessariam dois serviços no VPS, ocupando conexão e banda dos dois |
| C-12 | **RF-08 é atendido pelo BFF** (`listVideos?status=ready`, `getVideo`), e `learning` valida o vínculo em CAP-005 por uma visão local alimentada por `midia.ativo-pronto`/`midia.preparacao-falhou`. A forma exata fica para CAP-005. **Decidida pelo usuário em 2026-09-25** | Operação `learning` → Media já agora: exigiria decidir autenticação serviço→serviço (ADR nova) para um consumidor ainda sem PRD |
| C-13 | **Nome do autor como retrato no momento do envio:** o BFF repassa `SessionValidated.name` em `uploaderName`; a identidade do autor vem de `sub` do JWT, nunca do corpo | Claim de nome no JWT: mudaria o token de CAP-002. Resolver o nome em Identity a cada listagem: exigiria `acesso.gerir`, que o professor não tem, ou uma operação nova em Identity |
| C-14 | **`midia.enviar` entra no enum `Permission`** de `tasks/prd-acesso-interno/api-contract.yaml` e `internal-api-contract.yaml` (1.0.0 → 1.1.0, aditivo), e Identity passa a aceitar `audience: media` para o `bff-admin`. A mudança é aplicada depois de CAP-002 integrada | Permissão fora do catálogo de Identity: contraria RN-12/RN-18 e o dono do catálogo |
| C-15 | **`videoId` é o mesmo nas APIs e nos fatos**; estados e motivos em inglês kebab-case, iguais nas duas modalidades | Nomes diferentes por modalidade: dois vocabulários para o mesmo vídeo |

## Evolução e compatibilidade

- **`bff-admin` 1.0.0 → 1.1.0:** só acrescenta operações; nenhuma operação de CAP-002 muda. O `Permission` da sessão ganha `midia.enviar` (C-14). O contrato de CAP-002 já diz que clientes ignoram permissão desconhecida, então o `admin-spa` de CAP-002 continua correto; o menu de vídeos passa a aparecer para quem tem a permissão.
- **Identity interna 1.0.0 → 1.1.0:** o enum `Permission` na resposta de validação ganha um valor. O único consumidor é o `bff-admin`, que é atualizado junto. Não há mudança de schema de requisição. `audience: media` é configuração de emissores/audiências, não campo novo.
- **Media interna e fatos `midia.*`:** novos, sem versão anterior. Compatibilidade com produção não verificada. O consumidor previsto (`learning`, CAP-005) ainda não existe; os enums `reason` e `status` podem crescer, e o consumidor deve tolerar valor novo.
- **Auditoria e Notificação:** nenhuma mudança. Enviar vídeo não é ato administrativo, e não há e-mail (DP-04).

## Validação e verificação

| Documento | Comando e ferramenta | Padrão/ruleset | Resultado |
|---|---|---|---|
| [api-contract.yaml](api-contract.yaml) | `npx --yes @stoplight/spectral-cli@6.15.0 lint tasks/prd-ingestao-midia/api-contract.yaml --ruleset .claude/skills/tsg-flow-contract-creator/rulesets/openapi.yaml --fail-severity=error` | OpenAPI 3.1.0, ruleset local | Sem erros nem avisos (dois `example` de header corrigidos na primeira execução) |
| [internal-api-contract.yaml](internal-api-contract.yaml) | mesmo comando sobre `internal-api-contract.yaml` | OpenAPI 3.1.0, ruleset local | Sem erros nem avisos |
| [asyncapi-contract.yaml](asyncapi-contract.yaml) | `npx --yes @asyncapi/cli@6.1.0 validate tasks/prd-ingestao-midia/asyncapi-contract.yaml` | AsyncAPI 3.0.0 | Válido; uma informação recomenda 3.1.0, mantida a 3.0.0 da skill |
| Exemplos de mensagem | `jsonschema` 4.26.0 (Draft 7, com verificação de formato) contra os payloads | AsyncAPI 3.0.0 | Os dois exemplos conformes; campo extra (`title`) recusado nos dois |

O contrato interno é derivado do público por transformação mecânica (mesmos schemas, segurança e erros trocados), para que os dois não divirjam.

A validade estrutural não comprova escrita direta, retomada, idempotência nem autorização. A implementação deve verificar:

- arquivo acima de 5 GiB e formato não aceito recusados sem nenhuma URL de parte emitida;
- retomada com a mesma `fingerprint` devolvendo as partes já confirmadas, e `fingerprint` igual de outro ator criando envio novo;
- `completeVideoUpload` com parte faltando → `UPLOAD_INCOMPLETE`; repetido após sucesso → o mesmo `videoId`;
- envio expirado → `UPLOAD_NOT_FOUND` e partes descartadas do armazenamento;
- URL de parte recusada depois de `expiresAt` e incapaz de ler qualquer objeto;
- leitura direta de original, qualidade gerada e chave negada (RF-10);
- professor com papel revogado → 401 no BFF na próxima ação; ator sem `midia.enviar` → 403 no BFF **e** no Media, chamado direto com JWT sem a permissão;
- vídeo de outra escola → `VIDEO_NOT_FOUND`;
- um único `midia.ativo-pronto` ou `midia.preparacao-falhou` por vídeo, com o mesmo `eventId` em toda republicação;
- nenhum título, nome de autor, URL de parte ou chave em log, span, métrica, fato ou erro.

## Pendências e handoff

1. C-11 a C-15 aprovadas em 2026-09-25.
2. **CORS e política do bucket** (plataforma): aceitar `PUT` só da origem do backoffice nas URLs de parte; nenhuma leitura pública (G21). É configuração, não contrato.
3. **Ordem de implantação:** Identity com `midia.enviar` e `audience: media` antes do `bff-admin` expor a área de vídeos; C-14 só depois de CAP-002 integrada.
4. **Valores operacionais** para a TechSpec: tamanho da parte, validade da URL de parte, número de tentativas da preparação.

Para `tsg-flow-techspec-creator`: usar este índice, [api-contract.yaml](api-contract.yaml), [internal-api-contract.yaml](internal-api-contract.yaml) e [asyncapi-contract.yaml](asyncapi-contract.yaml), sem duplicar schemas.
