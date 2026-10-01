# Revisão full do PRD — Vitrine e oferta de curso (CAP-003, 1º PRD)

Run: run.bkD2FY6p
Modo: full · Tentativa: 1/3 · Validator independente (sem reuso das aprovações focused)

- base_ref: `5566d027a6c74924f85a84bfa318711e56b15104` (main @ origem, sem rebase pendente)
- validated_commit: `96c716c4f904bdd26191bf90c628a7773b6dcb57`
- validated_tree: `69eda4abe0a9d57a82663ce95f1c0979ef9558a7`
- HEAD e árvore estáveis durante a revisão; `git status --porcelain` igual antes e depois (só `tasks/prd-vitrine-oferta/flow-state.json` modificado, já na entrada).

**Resultado: FULL VALIDATION REPROVADA** — 1 bloqueante (mutante sobrevivente no sensor de discriminação), 2 recomendações.

## Bloqueante

**B1 — Filtro por nível da vitrine no BFF do aluno não é discriminado por nenhum teste (RF-07).**
- Arquivo/linha: `src/bff-student/src/CodeForCoders.BffStudent.Api/Endpoints/ShowcaseEndpoints.cs:14` (lista `Levels`).
- Mutação: remover `"advanced"` de `["beginner", "intermediate", "advanced"]`. Com ela, `GET /api/v1/showcase/courses?level=advanced` passa a responder 400 `INVALID_REQUEST` em vez de repassar o filtro. O visitante perde o filtro *Avançado*, critério de aceite de RF-07.
- Resultado: **sobreviveu**. A suíte focalizada da fatia V-07 (`ShowcaseAnonymousRouteTests`, 17 testes) passou com exit 0. As suítes inteiras de integração, unitárias e E2E do `bff-student` também passaram com a mutação aplicada.
- Teste que deveria ter falhado: `ShowcaseAnonymousRouteTests` (`src/bff-student/tests/CodeForCoders.BffStudent.IntegrationTests/ShowcaseAnonymousRouteTests.cs:132` repassa só `level=beginner`; `:139-140` cobre só valores inválidos `xyz` e vazio). `intermediate` e `advanced` não passam pelo BFF em teste algum.
- Correção esperada: acrescentar casos que enviem `intermediate` e `advanced` e afirmem 200 e o repasse da query ao `commerce`. Não enfraquecer nem remover teste.

## Matriz de verificações (fonte: workflows `.github/workflows/*.yml` + reusáveis `ci-dotnet.yml@v1` e `ci-react-ts.yml@v1`, lidos via `gh api`)

Passos reproduzidos localmente na ordem do CI. .NET: `dotnet restore` → `dotnet format --verify-no-changes --no-restore` → `dotnet test --no-restore -c Debug --coverage --coverage-output-format cobertura` (runner Microsoft.Testing.Platform) → cobertura por união de linhas (script do CI) com limite 70% → `dotnet publish -c Release --no-restore`. SPA: `npm ci` → `npm run lint` → `tsc --noEmit` → `npm test` (vitest + cobertura v8) → `npm run build -- --base=<base>`.

| Componente | restore/ci | format/lint | typecheck | testes | cobertura (mín. 70) | publish/build | imagem Docker |
|---|---|---|---|---|---|---|---|
| identity | 0 | 0 | — | 0 · 132/132 | 79,65% | 0 | 0 |
| commerce | 0 | 0 | — | 0 · 232/232 | 95,19% | 0 | 0 |
| audit | 0 | 0 | — | 0 · 127/127 | 81,28% | 0 | 0 |
| bff-admin | 0 | 0 | — | 0 · 193/193 | 83,49% | 0 | 0 |
| bff-student | 0 | 0 | — | 0 · 165/165 | 84,55% | 0 | 0 |
| admin-spa | 0 | 0 (1 aviso `react-hooks/incompatible-library`, 0 erros) | 0 | 0 · 205/205 | 90,28% | 0 (`--base=/admin/`) | 0 |
| student-spa | 0 | 0 | 0 | 0 · 63/63 | 85,90% | 0 (`--base=/student/`) | 0 |

- Todos os testes de integração rodaram com Testcontainers reais (PostgreSQL e RabbitMQ). Nenhum foi pulado.
- Imagens: `docker build` local de cada Dockerfile com o contexto do CI (raiz para .NET, `src/<spa>` para SPAs), sem push. Imagens de teste removidas.
- Limites e passos não reproduzíveis: push para GHCR, login, cache GHA, SBOM, scan Trivy da imagem, SAST, secret scan e scan de dependências do job `security` não rodam localmente; ficam como observacionais (`security-mode: observe`, não reprovam). Nenhum job DAST (`run-dast` desligado).
- Primeira tentativa do commerce teve 136 falhas por execução concorrente de duas cadeias minhas sobre o mesmo `bin/obj` (`BadImageFormatException` na instrumentação de cobertura e uma asserção de relógio). Isso foi falha de ambiente do validator, não do código. Refeita com uma cadeia única e árvore limpa, passou 232/232. Só a segunda execução conta como evidência.

## Sensor de discriminação

Executado em `git worktree` temporário (sem `git stash`), 17 mutações de comportamento derivadas dos critérios de aceite, uma ou mais por fatia, cada uma contra a suíte focalizada da fatia.

| # | Mutação | Critério | Resultado |
|---|---|---|---|
| M01 | preço máximo R$ 99.999,99 → R$ 100.000,00 | RF-03/DP-03 | morto (`OfferRulesTests`) |
| M02 | vigência máxima 60 → 61 meses | RF-03/DP-03 | morto (`OfferRulesTests`) |
| M03 | chamada comercial 160 → 161 | RF-02 | morto (`CatalogTaglineTests`) |
| M04 | publicar sem nível permitido | RF-04/RN-O03 | morto (`OfferPublicationRulesTests`) |
| M05 | alterar oferta em rascunho emite fato | RF-05 | morto (`OfferChangeRulesTests`) |
| M06 | curso sem oferta publicada continua na vitrine | RF-06/RF-07 | morto (`OfferUnpublishRulesTests`) |
| M07 | oferta despublicada pode ser excluída | RF-06/RN-O11 | morto (`OfferUnpublishRulesTests`) |
| M08 | clique em oferta despublicada é contado | RF-09 | morto (`PurchaseIntentTests`) |
| M09 | audit aceita preço com zero à esquerda | RF-05/RF-10 | morto (`OfferChangedActPolicyTests`) |
| M10 | audit aceita par anterior sem novo | RF-05 | morto (`OfferChangedActPolicyTests`) |
| M11 | `oferta.editar` também para professor | RF-01 | morto (`StaffRoleCatalogOfferPermissionTests`) |
| **M12** | **BFF rejeita `level=advanced`** | **RF-07** | **SOBREVIVEU (B1)** |
| M13 | ato `oferta-alterada` sem `precoAnterior` | RF-05 | morto (`OfferChangeTests`) |
| M14 | publicar não recalcula a vitrine | RF-04/RF-07 | morto (`OfferPublicationTests`) |
| M15 | rótulo da oferta na trilha sem o nome da opção | RF-10 | morto (`OfferReferenceResolutionTests`) |
| M16 | texto "Acesso vitalício" alterado na SPA pública | RF-08 | morto (`student-showcase`) |
| M17 | confirmação sem o aviso "compras futuras" | RF-05 | morto (`catalog-offer-price-change`) |

16 de 17 mortos por falha de asserção (nenhum por erro de compilação: 0 ocorrências de `error CS` nos logs). Descarte: worktree removido e podado; HEAD, árvore e `git status --porcelain` idênticos à linha de base. Logs em `scratchpad/val/mutlogs`.

Não mutados: o fluxo de Conteúdo (`CatalogCourseView.Apply` e consumidor RabbitMQ) e a asserção de serviço entre BFF e `commerce`, cobertos pelas revisões focused 3.0 e 9.0 e pelo gate agregado. Declarado como limitação do sensor, não como execução.

## Rastreabilidade e integração (revisão semântica)

- Rotas do OpenAPI do backoffice, da vitrine e interno de `commerce` coincidem com as implementadas em `bff-admin`, `bff-student` e `commerce` (conferência por rota e verbo).
- Migrations geradas pelo EF (Designer + snapshot presentes); nenhuma manual.
- Fato e ato nascem no outbox do Catálogo na mesma transação da mudança (`UpdateOffer`, `PublishOffer`, `UnpublishOffer`). Nome só da opção não gera fato nem ato (DP-05). `audit` aceita os três tipos novos e as chaves de complemento do contrato 1.3.0.
- Área pública anônima: `Cache-Control: no-store`, `404` uniforme para identificador inválido ou curso fora da vitrine, rate limit no clique, chave de idempotência do clique guardada só como hash, contagem diária por oferta sem identificador de pessoa (RF-09, G10, G26). Nenhuma cópia de exclusividade ou proteção na SPA pública (RN-O06).
- Composição: `docker-compose.yml`/`docker-compose.coolify.yml` e `scripts/generate-local-env.sh` ganham o par de chaves do `bff-student` → `commerce` e as origens da asserção de serviço (ADR 0009), sem segredo versionado.
- Estado intermediário declarado (vitrine vazia entre 3.0 e 8.0; fatos 1.1.0 como fixtures até CAP-005) não afeta a entrega final.

## Recomendações (não bloqueiam)

1. **Par vazio em `oferta-alterada`** (`src/audit/src/CodeForCoders.Audit.Domain/Policies/AdministrativeActPolicy.cs:101-113`, herdada da 7.0): permanece recomendação. O contrato 1.3.0 define os pares como opcionais ("quando o preço mudou") e o único produtor (`UpdateOffer`) só emite ato quando preço ou vigência mudam. Sem descumprimento de requisito nem defeito. Gatilho para endurecer: um segundo produtor de `oferta-alterada`.
2. **Accordion da página do curso vs frame P2.a** (`src/student-spa/src/components/ui/accordion.tsx:25`, `student-course-page.tsx:115`, herdada da 10.0): permanece recomendação. Divergência de apresentação, sem quebra de jornada nem requisito de PRD.

## Veredito

**FULL VALIDATION REPROVADA.** O CI reproduzível passa em todos os 7 componentes (testes, cobertura acima de 70%, publish e imagens). O sensor de discriminação reprova a fatia V-07 por um mutante sobrevivente (B1). Cada sobrevivente vira task de correção; depois da correção, a full precisa ser refeita sobre o novo commit.
