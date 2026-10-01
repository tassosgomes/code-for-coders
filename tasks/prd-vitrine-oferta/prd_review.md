# Revisão full do PRD — Vitrine e oferta de curso (CAP-003, 1º PRD)

Run: run.u0ZkoRq9
Modo: full · Tentativa: 3/3 · Validator independente (worker fresco)

- base_ref: `5566d027a6c74924f85a84bfa318711e56b15104`
- validated_commit: `5a34aa9fe579c877308cd1e1fa1629ef60a7bb10`
- validated_tree: `87696c31331738f8a1827331615c2ea4d414e0ac`
- HEAD e `git status --porcelain` idênticos antes e depois da revisão (só `tasks/prd-vitrine-oferta/flow-state.json` modificado, já na entrada).

**Resultado: FULL VALIDATION APROVADA** — 0 bloqueantes, 4 recomendações (2 herdadas, 2 novas).

## Escopo do diff desde a rodada 2 (run.mWneQ7Ip, aprovou `0dedaec`)

`git diff 0dedaec..5a34aa9`: 5 arquivos — **um** de código, `src/admin-spa/src/testing/audit-trail-offer-handlers.ts`
(helper de teste; fix SonarCloud S7737: `attributes` deixou de ter objeto literal como default de parâmetro e passou a
`attributes ?? {…}`; mesma semântica para os chamadores, único chamador `audit-trail-offer.test.tsx:14`), mais
relatórios/estado/`tasks.md`. Nenhum código de produção mudou; confirmado no arquivo que o padrão S7737 não existe mais
(`src/testing/audit-trail-offer-handlers.ts:9-20`).

## Matriz de verificações (CI: `.github/workflows/*.yml` + reusáveis `ci-dotnet.yml@v1` / `ci-react-ts.yml@v1`)

| Componente | Origem | lint/format | typecheck | testes | cobertura | build |
|---|---|---|---|---|---|---|
| **admin-spa** | **reexecutado por inteiro** (`npm ci`, `npm run lint`, `npm run typecheck`, `npm test`, `npm run build -- --base=/admin/`) | 0 (1 aviso `react-hooks/incompatible-library`, 0 erros; pré-existente) | 0 | 0 · **205/205** (38 arquivos) | **90,33%** linhas (mín. 70%) | 0 |
| identity | reaproveitado (rodada 2; código inalterado) | 0 | — | 132/132 | 79,65% | publish 0 · imagem 0 |
| commerce | reaproveitado | 0 | — | 232/232 | 95,19% | 0 |
| audit | reaproveitado | 0 | — | 127/127 | 81,28% | 0 |
| bff-admin | reaproveitado | 0 | — | 193/193 | 83,49% | 0 |
| bff-student | reaproveitado | 0 | — | 167/167 | 84,55% | 0 |
| student-spa | reaproveitado | 0 | — | 63/63 | 85,90% | 0 |

- Reaproveitamento justificado: `git diff 0dedaec..5a34aa9 -- src` toca só `src/admin-spa/src/testing/...`; os demais seis componentes têm árvore de código idêntica à validada na rodada 2.
- Não reproduzíveis localmente (observacionais, `security-mode: observe`): push GHCR, login, cache GHA, SBOM, Trivy, SAST, secret scan, scan de dependências, docker build do admin-spa. DAST desligado. Não é espelho completo do CI.

## Sensor de discriminação (fatia V-10/12.0, `audit-trail-offer.test.tsx`, 7 testes)

Worktree temporário `--detach` (sem `git stash`), `node_modules` por link simbólico; controle sem mutação: 7/7, exit 0.

| # | Mutação | Critério (12.0) | Resultado |
|---|---|---|---|
| M1 | `cents / 100` → `cents / 10` (`format-offer-audit-attribute.ts`) | preço em reais | **morto** (exit 1, 1 falha) |
| M2 | `'Vitalícia'` → valor cru | vigência "Vitalícia" | **morto** (exit 1) |
| M3 | plural `mês`/`meses` invertido | vigência "N meses" | **morto** (exit 1) |
| M4 | ordenação dos atributos removida | atributos em ordem anterior→novo | **morto** (exit 1) |
| M5 | rótulo `Oferta alterada` → `oferta-alterada` (`audit-record-detail-screen.tsx`) | rótulo legível do tipo | **morto** (exit 1, 3 falhas) |
| M6 | guarda `/^\d+$/.test(value)` → `true` | valor malformado não vira formatado enganoso | **sobreviveu** — ver recomendação 3 |

- M1–M5 reexecutados nesta rodada; as mutações das demais fatias (M01–M17 da rodada 1, M12/M12b da rodada 2) ficam reaproveitadas: o código de produção é idêntico.
- Descarte: worktree removido e podado; HEAD e `git status --porcelain` voltaram à linha de base.
- Limitação herdada: não mutados o fluxo de Conteúdo (`CatalogCourseView.Apply`/consumidor) e a asserção de serviço BFF→commerce, cobertos pelas revisões focused 3.0 e 9.0.

## Rastreabilidade e integração

Sem mudança de produção desde a rodada 1; a revisão semântica anterior permanece válida (rotas OpenAPI x implementadas, migrations geradas pelo EF, fato/ato no outbox na mesma transação, área pública anônima com `no-store`/404 uniforme/rate limit, sem segredo versionado, ADR 0009).

## Recomendações (não bloqueiam)

1. **Par vazio em `oferta-alterada`** (`src/audit/.../AdministrativeActPolicy.cs:101-113`): contrato 1.3.0 define pares opcionais e o único produtor só emite com mudança. Gatilho: um segundo produtor.
2. **Accordion vs frame P2.a** (`src/student-spa/src/components/ui/accordion.tsx:25`, `student-course-page.tsx:115`): divergência de apresentação sem quebra de jornada.
3. **M6 sobrevive** (`src/admin-spa/src/features/audit-trail/utils/format-offer-audit-attribute.ts:4`): o teste "malformed historical values" usa `'inválido'`, que `Number.isSafeInteger` já rejeita; a guarda `^\d+$` não é discriminada para entradas como `'1e3'` ou `''`. Não vem de critério de aceite da 12.0 nem do contrato (centavos em texto numérico), por isso não bloqueia. Sugestão: acrescentar `precoNovo: '1e3'` ao teste.
4. **3 MAJOR `autoFocus` do SonarCloud**: sem correção autorizada (comportamento aprovado); manter como recomendação/aceite de risco.

## Veredito

**FULL VALIDATION APROVADA.** admin-spa reexecutado por inteiro passa (lint, typecheck, 205 testes, cobertura 90,33%, build); demais componentes com evidência comprovadamente inalterada; fix S7737 confirmado; sensor sem mutante sobrevivente derivado de critério de aceite.
