---
tsg_artifact: contract
product: code-4-coders
capability: CAP-003
version: 1.0
status: approved
updated: 2026-09-30
sources: tasks/prd-vitrine-oferta/prd.md@1.0, domains/catalogo-e-oferta/domain.md@1.1, domains/matricula-e-direito-de-acesso/domain.md@1.0, domains/auditoria-e-conformidade/domain.md@1.2, context/architecture-baseline.md@1.2
---

# Contratos de integração — vitrine e oferta de curso

> - PRD: [prd.md](prd.md), v1.0, aprovado em 2026-09-30
> - Data da revisão: 2026-09-30
> - Estado do conjunto: **Aprovado para implementação (1.0)** em 2026-09-30 — todas as decisões aprovadas pelo responsável; todos os documentos validados sem erros.

Este conjunto registra o acordo para o primeiro PRD de `CAP-003`. O **provedor** dos dados do curso é o segundo PRD de `CAP-005` ([contracts.md](../prd-nivel-prerequisito-curso/contracts.md)), escrito na mesma passada. Contratos de PRDs anteriores são preservados nas pastas de origem; as únicas mudanças neles são aditivas: C-09 (Auditoria, recorte novo aqui) e C-10 (catálogo de permissões, aplicado em `tasks/prd-acesso-interno/`).

## Seleção e escopo

| Integração identificada | Modalidade | Justificativa |
|---|---|---|
| `admin-spa` → `bff-admin` → `commerce` | OpenAPI | Área Catálogo: ficha, ofertas, publicação (RF-01 a RF-06, RF-09). |
| `student-spa` → `bff-student` → `commerce` | OpenAPI | Vitrine, página e clique, públicos (RF-07 a RF-09). |
| `bff-admin` → `commerce` para a consulta da trilha | OpenAPI | Rótulo da oferta alvo dos atos (RF-10, C-11). |
| `learning` → `commerce` | AsyncAPI | O Catálogo mantém a visão do curso publicado pelo fato de versão (C-01). |
| `commerce` → Auditoria e consumidores futuros | AsyncAPI | Atos de oferta (RN-O15) e fatos `catalogo.*` (domain doc §7). |
| Identity | OpenAPI (enum) | `oferta.editar` no catálogo de permissões (C-10). |
| Catálogo como produto de dados | Não aplicável | Banco interno de `commerce`; nenhum consumidor recebe dataset com compromisso próprio. Nenhum ODCS. |

| Documento | Padrão e versão | Versão do contrato | Fronteira | Estado |
|---|---|---|---|---|
| [api-contract.yaml](api-contract.yaml) e [.md](api-contract.md) | OpenAPI 3.1.0 | `bff-admin` Catálogo 1.0.0 | Recorte: 8 operações novas; as de CAP-002, CAP-005, CAP-006 e CAP-030 não mudam | **Aprovado para implementação** em 2026-09-30; lint sem erros nem avisos |
| [api-contract-student.yaml](api-contract-student.yaml) e [.md](api-contract-student.md) | OpenAPI 3.1.0 | `bff-student` vitrine 1.0.0 | Recorte: 3 operações públicas novas; as de CAP-001 não mudam | **Aprovado para implementação** em 2026-09-30; lint sem erros nem avisos |
| [internal-api-contract-commerce.yaml](internal-api-contract-commerce.yaml) e [.md](internal-api-contract-commerce.md) | OpenAPI 3.1.0 | `commerce` 1.0.0 → 1.1.0 | 8 operações de backoffice, 3 públicas e `resolveOfferReferencesInternal`; área financeira 1.0.0 sem mudança | **Aprovado para implementação** em 2026-09-30; lint sem erros nem avisos |
| [asyncapi-contract.yaml](asyncapi-contract.yaml) | AsyncAPI 3.0.0 | `commerce` 1.0.0 | Receptor de `conteudo.versao-publicada.v1` 1.1.0; produtor de `catalogo.oferta-*.v1` e dos atos | **Aprovado para implementação** em 2026-09-30; parser sem erros |
| [asyncapi-contract-audit.yaml](asyncapi-contract-audit.yaml) | AsyncAPI 3.0.0 | `audit` 1.2.0 → 1.3.0 | Recorte aditivo do canal `auditoria.ato-praticado.v1` | **Aprovado para implementação** em 2026-09-30; parser sem erros |
| `tasks/prd-acesso-interno/api-contract.yaml` e `internal-api-contract.yaml` | OpenAPI 3.1.0 | Identity 1.2.0 → 1.3.0 | Só o enum `Permission` ganha `oferta.editar` (C-10) | Aplicado; lint sem erros |

## Participantes e interfaces

| Identificador | Provedor/produtor | Consumidores | Comportamento | PRD |
|---|---|---|---|---|
| `listCatalogCourses`, `getCatalogCourse`, `updateCatalogCourse` | `bff-admin`, decisão de `commerce` | `admin-spa` | Cursos publicados, ficha com dados da versão vigente, chamada comercial | RF-02 |
| `createOffer`, `updateOffer`, `deleteOffer` | idem | `admin-spa` | Oferta com nome, preço e vigência; só rascunho é excluído | RF-03, RF-05 |
| `publishOffer`, `unpublishOffer` | idem | `admin-spa` | Publicar exige nível no curso; fato + ato na mesma transação | RF-04, RF-06 |
| `*Internal` de backoffice (8) | `commerce` | `bff-admin` | Mesmas regras, JWT de ator `audience: commerce`, `oferta.editar` | RF-01 a RF-06 |
| `listShowcaseCourses`, `getShowcaseCourse` | `bff-student`, dados de `commerce` | `student-spa` | Vitrine pública com filtro por nível; página do curso | RF-07, RF-08 |
| `registerPurchaseIntent` | idem | `student-spa` | Clique anônimo contado; resposta `coming-soon` | RF-09 |
| `*Internal` públicas (3) | `commerce` | `bff-student` | Asserção de serviço, escopos `showcase:read` e `purchase-intent:write` | RF-07 a RF-09 |
| `resolveOfferReferencesInternal` | `commerce` | `bff-admin` (consulta da trilha) | Rótulo "curso — opção", papel administrador | RF-10 |
| `receberVersaoPublicada` | `commerce` | — | Visão do curso: título, descrição, nível, pré-requisito, estrutura | RF-02, RF-07, RF-08 |
| `publicarOferta{Publicada,Alterada,Despublicada}` · `catalogo.oferta-*.v1` | `commerce`, por outbox | nenhum nesta entrega | Estado da oferta após o fato; `offerRevision` ordena | RF-04 a RF-06 |
| `publicarAtoDeOferta` · `auditoria.ato-praticado.v1` | `commerce`, por outbox | `audit` 1.3.0 | Atos `oferta-*`, alvo `oferta`, sem motivo | RF-04 a RF-06, RF-10 |

| Ação | Operação HTTP | Mensagens (mesma transação) |
|---|---|---|
| Publicar | `publishOffer` | `catalogo.oferta-publicada.v1` + ato `oferta-publicada` (`fatoId` = `eventId`) |
| Mudar preço/vigência de oferta publicada | `updateOffer` | `catalogo.oferta-alterada.v1` + ato `oferta-alterada` |
| Despublicar | `unpublishOffer` | `catalogo.oferta-despublicada.v1` + ato `oferta-despublicada` |
| Criar, excluir, mudar só nome, mudar rascunho, chamada comercial | demais escritas | nenhuma (DP-05) |
| Clique em Comprar | `registerPurchaseIntent` | nenhuma |

`courseId` é o mesmo de `learning` em todas as interfaces, no fato de versão e no complemento do ato; `offerId` é o mesmo nas APIs, nos fatos e no alvo do ato.

## Origem e decisões

| Entrada consultada | Versão e data | Decisão herdada |
|---|---|---|
| [PRD de CAP-003](prd.md) | v1.0, 2026-09-30 | RF-01 a RF-10, DP-01 a DP-05; OD47 a OD51, OD53, OD54 |
| [Catálogo e Oferta](../../domains/catalogo-e-oferta/domain.md) | v1.1, 2026-09-30 | RN-O01 a RN-O15, RN-O18; eventos §7 |
| [Matrícula e Direito de Acesso](../../domains/matricula-e-direito-de-acesso/domain.md) | v1.0, 2026-09-30 | RN-D03, RN-D04: a vigência precisa ser congelável pelo pedido — daí a forma única `accessPeriod` |
| [Auditoria e Conformidade](../../domains/auditoria-e-conformidade/domain.md) | v1.2, 2026-09-27 | RN-A05, RN-A08, RN-A14 |
| [Baseline](../../context/architecture-baseline.md) | v1.2, 2026-09-21 | BFF por audiência, G06, G07, G08, G09, G10, G15, G18, G26 |
| [ADR-0004](../../docs/adr/0004-autenticacao-de-servico-bff-identity.md) e [ADR-0005](../../docs/adr/0005-sessao-e-servico-do-backoffice.md) | aceitas | Asserção de serviço do `bff-student` (0004, só para Identity); JWT de ator com audiência do serviço dono (0005) |
| [Contratos de autoria (CAP-005)](../prd-autoria-curso/contracts.md) | 1.0, 2026-09-28 | C-01 (tipo novo na Auditoria como minor), C-08 (rótulo resolvido pelo dono), C-09 (consumidor mantém a visão pelo fato), C-10 (permissão no enum de Identity) |
| [Contratos de nível e pré-requisito (CAP-005)](../prd-nivel-prerequisito-curso/contracts.md) | em revisão, 2026-09-30 | Fato 1.1.0, reenvio (C-01), título de publicação do recomendado (C-02), descrição no fato (C-03) |
| [Área financeira de `commerce`](../prd-acesso-interno/internal-api-contract-commerce.yaml) | 1.0.0 | JWT de audiência `commerce`, `TOKEN_INVALID`/`PERMISSION_DENIED` |

Decisões deste contrato:

| ID | Decisão | Alternativa descartada | Estado |
|---|---|---|---|
| C-01 | **O Catálogo mantém a própria visão do curso publicado a partir de `conteudo.versao-publicada.v1`** (C-09 de CAP-005), com a carga inicial pelo reenvio de `learning` (C-01 do conjunto provedor). Nenhuma chamada a `learning` no caminho da vitrine nem na regra de nível (RN-O03) | Leitura síncrona `commerce` → `learning` | **Aprovada em 2026-09-30** |
| C-02 | **Recomendado fora do Catálogo mostra o título da publicação**; conhecido, o título atual (C-02 do provedor) | — | Herdada |
| C-03 | **A página pública sai inteira da visão do Catálogo**, inclusive a descrição (C-03 do provedor) | `bff-student` ler de `learning` | **Aprovada em 2026-09-30** |
| C-04 | **`bff-student` se autentica em `commerce` por asserção de serviço**, no mecanismo da ADR-0004, com audiência `commerce` e escopos `showcase:read` e `purchase-intent:write`. A ADR-0004 cobre só Identity: a TechSpec registra uma ADR própria para a extensão | Sem autenticação, confiando na rede interna: qualquer processo inflaria cliques ou leria catálogo de outra escola | **Aprovada em 2026-09-30** |
| C-05 | **Preço em centavos de real, inteiro (`priceCents`), moeda implícita BRL nas interfaces HTTP e explícita (`currency: BRL`) nos fatos.** Vigência numa forma única, `accessPeriod` `{type: months, months}` ou `{type: lifetime}`, igual em HTTP e fatos, para o pedido de `CAP-011` congelar sem conversão | Decimal em texto; vigência em dois campos soltos | **Aprovada em 2026-09-30** |
| C-06 | **Ficha sem "criar":** todo curso que o Catálogo conhece tem ficha. Ofertas de um curso vêm todas na ficha, até 50, sem paginação (teto declarado em `maxItems`) | Criar ficha explicitamente: passo sem valor para o financeiro | **Aprovada em 2026-09-30** |
| C-07 | **O endereço público do curso é identificado por `courseId`** | Slug do título: muda quando o professor renomeia, e o link compartilhado quebra | **Aprovada em 2026-09-30** |
| C-08 | **Clique em Comprar sem sessão e sem CSRF, com `Idempotency-Key` por clique**, 404 sem contar para oferta não publicada, 429 do limite de borda. G18 não se aplica: a operação não é autenticada por cookie | CSRF para anônimo: exigiria emitir prova a visitante sem sessão, para proteger um contador sem valor financeiro | **Aprovada em 2026-09-30** |
| C-09 | **Auditoria 1.2.0 → 1.3.0, aditiva:** origem `catalogo`, tipos `oferta-publicada`, `oferta-alterada`, `oferta-despublicada`, alvo `oferta`, complemento `curso` e, em `oferta-alterada`, `precoAnterior`/`precoNovo` (centavos) e/ou `vigenciaAnterior`/`vigenciaNova` (`12m` ou `vitalicia`). Sem motivo (OD50) | Deixar o ato chegar como tipo desconhecido: poluiria a trilha e o alerta de operação | **Aprovada em 2026-09-30** |
| C-10 | **`oferta.editar` entra no enum `Permission`** de `tasks/prd-acesso-interno/` (Identity 1.2.0 → 1.3.0), concedida só ao financeiro; a audiência `commerce` passa a carregá-la | — | **Aplicada** (decorre de OD47; mesmo procedimento de C-10 de CAP-005) |
| C-11 | **Rótulo da oferta na consulta da trilha resolvido por `commerce`** (`resolveOfferReferencesInternal`, papel administrador), mesmo padrão de C-08 de CAP-005. Muda só o comportamento do BFF da consulta de CAP-030 e os rótulos do `admin-spa`; nenhum schema de CAP-030 muda (`type` e `label` já são texto livre e opcional) | Mostrar só "Oferta" e o id | **Aprovada em 2026-09-30** |
| C-12 | **Os fatos `catalogo.oferta-*` existem já nesta entrega**, sem consumidor, porque o domain doc os declara e o outbox os produz junto com o ato | Produzir só o ato: o primeiro consumidor exigiria carga retroativa | **Aprovada em 2026-09-30** |

## Evolução e compatibilidade

- **`bff-admin` Catálogo 1.0.0, `bff-student` vitrine 1.0.0:** novos, sem versão anterior. Compatibilidade com produção não verificada.
- **`commerce` 1.0.0 → 1.1.0:** acrescenta operações; a área financeira não muda. O esquema `StudentBffServiceAssertion` é novo em `commerce`.
- **Auditoria 1.2.0 → 1.3.0 (C-09):** mudança de **receptor**, aditiva; produtores atuais continuam válidos. `complemento` já aceitava chaves texto adicionais (`additionalProperties: {type: string, maxLength: 100}`); as novas são declaradas. **Ordem de implantação obrigatória:** `audit` 1.3.0 antes de `commerce` publicar o primeiro ato.
- **Identity 1.2.0 → 1.3.0 (C-10):** aditiva; clientes ignoram permissão desconhecida.
- **Consulta da trilha (CAP-030):** só comportamento do BFF e rótulos do SPA (C-11).
- **`conteudo.versao-publicada.v1` 1.1.0:** consumido como publicado pelo provedor; mensagem 1.0.0 é aplicada como sem nível.
- **Ordem de implantação do conjunto:** Identity 1.3.0 → `audit` 1.3.0 → `commerce` com o consumidor do fato → `learning` 1.1.0 e reenvio → `commerce` expondo Catálogo e vitrine → `bff-admin` e `bff-student`.

## Validação e verificação

| Documento | Comando e ferramenta | Padrão/ruleset | Resultado |
|---|---|---|---|
| [api-contract.yaml](api-contract.yaml) | `npx --yes @stoplight/spectral-cli@6.15.0 lint tasks/prd-vitrine-oferta/api-contract.yaml --ruleset .claude/skills/tsg-flow-contract-creator/rulesets/openapi.yaml -F hint` | OpenAPI 3.1.0, ruleset local | Sem erros nem avisos |
| [api-contract-student.yaml](api-contract-student.yaml) | mesmo comando | OpenAPI 3.1.0, ruleset local | Sem erros nem avisos |
| [internal-api-contract-commerce.yaml](internal-api-contract-commerce.yaml) | mesmo comando | OpenAPI 3.1.0, ruleset local | Sem erros nem avisos |
| `tasks/prd-acesso-interno/api-contract.yaml` e `internal-api-contract.yaml` 1.3.0 | mesmo comando, `--fail-severity=error` | OpenAPI 3.1.0, ruleset local | Sem erros |
| [asyncapi-contract.yaml](asyncapi-contract.yaml), [-audit](asyncapi-contract-audit.yaml) | `npx --yes @asyncapi/cli@6.1.0 validate <arquivo>` | AsyncAPI 3.0.0 | Válidos, referências externas resolvidas; uma informação recomenda 3.1.0, mantida a 3.0.0 da skill |
| Exemplos de mensagem | `jsonschema` 4.26.0 (Draft 7, com formato) | Payloads de `commerce` e `audit` 1.3.0 | Os três fatos de oferta e os quatro exemplos da Auditoria conformes; 61 meses, preço zero e vigência `doze` recusados |

O contrato interno de `commerce` é derivado dos dois públicos por transformação mecânica (mesmos schemas; segurança e erros trocados), para que não divirjam.

A implementação deve verificar:

- `publishOffer` com curso sem nível na versão vigente → 422 `COURSE_LEVEL_REQUIRED`, nada publicado; oferta já publicada → `OFFER_STATE_CONFLICT`; duplo clique → uma publicação, um fato, um ato;
- oferta, fato e ato nascem juntos ou não nascem (falha forçada entre eles);
- `updateOffer` de preço em oferta publicada → ato com `precoAnterior`/`precoNovo`; só nome → nenhum ato; em rascunho → nenhum ato;
- `deleteOffer` em publicada ou despublicada → `OFFER_STATE_CONFLICT`;
- nova versão do curso sem nível → curso sai da vitrine, ofertas continuam `published` (DP-04); republicação com nível → volta;
- vitrine e página nunca mostram curso ou oferta de outra escola; asserção de outra escola → dados só daquela escola;
- ator sem `oferta.editar` → 403 no BFF **e** em `commerce` chamado direto; professor e administrador sem o papel financeiro → 403;
- asserção do `bff-student` sem escopo → 403 `SCOPE_DENIED`; JWT de aluno no lugar da asserção → 401; asserção repetida (`jti`) → 401;
- clique repetido com a mesma `Idempotency-Key` conta uma vez; oferta despublicada → 404, sem contar; o registro do clique não tem sessão, cookie, IP nem identificador de navegador;
- atos de oferta chegam à trilha **conformes** e aparecem na consulta com o rótulo "curso — opção";
- reenvio de `learning`: o Catálogo passa a conhecer todos os cursos publicados; fato com `versionNumber` menor é ignorado;
- nenhum preço, texto comercial ou dado de visitante em log, span ou métrica além do necessário à operação (G10).

## Pendências e handoff

1. **ADR na TechSpec:** extensão da asserção de serviço do `bff-student` a `commerce` (C-04).
2. **Ordem de implantação** acima, incluindo o reenvio de `learning` depois do consumidor do Catálogo.
3. **Para a TechSpec:** modelo da visão do curso em `commerce`, contador de cliques (agregação sem identificador), limite de borda do clique, cache de leitura da vitrine, tempos-limite dos BFFs para `commerce`.

Para `tsg-flow-techspec-creator`: usar este índice, [api-contract.yaml](api-contract.yaml), [api-contract-student.yaml](api-contract-student.yaml), [internal-api-contract-commerce.yaml](internal-api-contract-commerce.yaml), [asyncapi-contract.yaml](asyncapi-contract.yaml) e [asyncapi-contract-audit.yaml](asyncapi-contract-audit.yaml), sem duplicar schemas.
