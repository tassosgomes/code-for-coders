# Revisão full do PRD — Vitrine e oferta de curso (CAP-003, 1º PRD)

Run: run.mWneQ7Ip
Modo: full · Tentativa: 2/3 · Validator independente (worker fresco)

- base_ref: `5566d027a6c74924f85a84bfa318711e56b15104`
- validated_commit: `0dedaecaa7084e62ba2a709b1d2f05875aec0a8f`
- validated_tree: `1147cbb032b0d2c37c38a97e4b9bcdbe18021f60`
- HEAD e `git status --porcelain` idênticos antes e depois da revisão (só `tasks/prd-vitrine-oferta/flow-state.json` modificado, já na entrada).

**Resultado: FULL VALIDATION APROVADA** — 0 bloqueantes, 2 recomendações (herdadas).

## Rodada anterior (run.bkD2FY6p, tentativa 1/3)

Reprovou por B1/M12: filtro `level=intermediate|advanced` do BFF do aluno não era discriminado por teste (RF-07).
Correção em `0dedaec`: Theory `EveryContractLevelIsAcceptedAndForwardedToCommerce` (`intermediate`, `advanced`) em
`src/bff-student/tests/.../ShowcaseAnonymousRouteTests.cs:138-150`, que afirma 200 e o repasse da query ao `commerce`.
**B1 resolvido** (ver sensor).

## Escopo do diff desde a rodada 1

`git diff 96c716c..0dedaec -- src` contém um único arquivo: o teste acima (+14 linhas, nenhum código de produção).
Por isso os componentes cujo diff não mudou têm evidência reaproveitada da rodada 1; bff-student foi reexecutado por inteiro.

## Matriz de verificações (CI: `.github/workflows/*.yml` + reusável `ci-dotnet.yml@v1` / `ci-react-ts.yml@v1` de `tassosgomes/template-pipeline`)

Cadeia .NET: `dotnet restore` → `dotnet format --verify-no-changes --no-restore` → `dotnet test --no-restore -c Debug --coverage --coverage-output-format cobertura` → cobertura por união de linhas (script do CI, mín. 70%) → `dotnet publish -c Release --no-restore` → `docker build` (sem push).

| Componente | Origem | restore | format/lint | testes | cobertura | publish/build | imagem |
|---|---|---|---|---|---|---|---|
| **bff-student** | **reexecutado nesta rodada** | 0 | 0 | 0 · **167/167** (165 + 2 casos da Theory) | **84,55%** (4 relatórios) | 0 | 0 |
| identity | reaproveitado (rodada 1; código inalterado) | 0 | 0 | 0 · 132/132 | 79,65% | 0 | 0 |
| commerce | reaproveitado | 0 | 0 | 0 · 232/232 | 95,19% | 0 | 0 |
| audit | reaproveitado | 0 | 0 | 0 · 127/127 | 81,28% | 0 | 0 |
| bff-admin | reaproveitado | 0 | 0 | 0 · 193/193 | 83,49% | 0 | 0 |
| admin-spa | reaproveitado | 0 | 0 (1 aviso, 0 erros) | 0 · 205/205 | 90,28% | 0 | 0 |
| student-spa | reaproveitado | 0 | 0 | 0 · 63/63 | 85,90% | 0 | 0 |

- bff-student: suítes unit, integration (Testcontainers reais), architecture e end-to-end passaram; nenhum teste pulado.
- Não reproduzíveis localmente (observacionais, `security-mode: observe`): push GHCR, login, cache GHA, SBOM, Trivy da imagem, SAST, secret scan, scan de dependências. DAST desligado.

## Sensor de discriminação

Worktree temporário (`git worktree add --detach`, sem `git stash`), suíte focalizada `ShowcaseAnonymousRouteTests` (19 testes).

| # | Mutação | Critério | Resultado |
|---|---|---|---|
| M12 | `Levels` sem `"advanced"` (`ShowcaseEndpoints.cs:14`) | RF-07 | **morto** — falha `EveryContractLevelIsAcceptedAndForwardedToCommerce(level: "advanced")` por asserção (exit 2; 1 falha, 18 passam) |
| M12b | `Levels` sem `"intermediate"` | RF-07 | **morto** — falha `...(level: "intermediate")` (exit 2; 1 falha, 18 passam) |
| controle | sem mutação | — | 19/19 passam (exit 0) |

- Reexecutados: M12 e M12b (fatia V-07 do bff-student). M01–M11 e M13–M17 da rodada 1 (todos mortos) ficam reaproveitados: o código de produção é idêntico entre `96c716c` e `0dedaec`.
- Incidentes de ambiente do validator (não do código): uma primeira rodada de M12b falhou 19/19 por falha de subida do Ryuk do Testcontainers, e dois scripts em segundo plano concorrentes contaminaram as primeiras tentativas. Os resultados acima são só das execuções limpas e sequenciais. Contêineres órfãos de teste foram removidos.
- Descarte: worktree removido e podado; HEAD e `git status --porcelain` voltaram exatamente à linha de base.
- Limitação herdada: não mutados o fluxo de Conteúdo (`CatalogCourseView.Apply`/consumidor) e a asserção de serviço BFF→commerce, cobertos pelas revisões focused 3.0 e 9.0 e pelo gate agregado.

## Rastreabilidade e integração

Sem mudança de produção desde a rodada 1; a revisão semântica da rodada 1 permanece válida: rotas OpenAPI x implementadas, migrations geradas pelo EF, fato/ato no outbox na mesma transação, área pública anônima (`no-store`, 404 uniforme, rate limit, idempotência por hash, contagem sem identificador de pessoa), composição sem segredo versionado (ADR 0009).

## Recomendações (não bloqueiam; julgadas de novo, permanecem recomendações)

1. **Par vazio em `oferta-alterada`** (`src/audit/.../AdministrativeActPolicy.cs:101-113`): o contrato 1.3.0 define os pares como opcionais e o único produtor só emite ato quando preço ou vigência mudam. Sem defeito nem descumprimento. Gatilho: um segundo produtor de `oferta-alterada`.
2. **Accordion da página do curso vs frame P2.a** (`src/student-spa/src/components/ui/accordion.tsx:25`, `student-course-page.tsx:115`): divergência de apresentação, sem quebra de jornada nem requisito de PRD.

## Veredito

**FULL VALIDATION APROVADA.** CI reproduzível passa nos 7 componentes (bff-student reexecutado, demais com evidência comprovadamente inalterada), cobertura acima de 70%, e o sensor de discriminação não tem mutante sobrevivente. B1 resolvido.
