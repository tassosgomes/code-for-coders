# Revisão full do PRD — Progresso do aluno (CAP-017)

Run: run.DNJg0XpO
Modo: full · Tentativa: 2/3

- base_ref: 3e0dc57894d280301799af616c4b8cb3690317f3
- validated_commit: ad7f1b8b0e7b7eb0b4301fe22626b833ff409946
- validated_tree: 9cf141d9439b5511f29ffc4a3dec007245e59adb
- Branch: `feature/progresso-aluno`. HEAD e árvore não mudaram durante a revisão. `git status --porcelain` igual antes e depois do sensor (`routing.default.json` preexistente e fora do PRD, `flow-state.json` e `AGENTS.md`, alterações de estado/usuário fora da implementação). Worktree do sensor removido.

## Resultado: FULL VALIDATION APROVADA (0 bloqueantes, 4 recomendações)

## Bloqueios da tentativa 1 (run.th6TLndb)

- **B1** (suíte de `learning`, DLX da `learning.playback-progress`): **resolvido** (838f080). `CatalogInitialLoadTestFixture` e `LearningInfrastructureTests` dão fila própria a `PlaybackProgressQueue` (e `VideoFactsQueue`). `dotnet test` da solução inteira: exit 0, 245/245.
- **B2** (V-01, retenção em `media/appsettings.json:36`): **resolvido**. `PlaybackProgressTests.DeliveredConfigurationPublishesProgressLiveInsteadOfRetainingIt` usa o `appsettings.json` entregue, exige que `RetainedRoutingKeys` não contenha a rota e que a linha do outbox nasça pendente. Mutação repetida (rota de volta em `RetainedRoutingKeys`): **morta** (`PlaybackProgressTests` 16 testes, 1 falha, exit 2).
- **B3** (V-03, filtro de aluno em `StudentCourseAccessQueries.cs:11`): **resolvido** (ad7f1b8). `GrantOfAnotherStudentInSameSchoolAndCourseNeverAppearsInList` cobre um segundo aluno na mesma escola e curso. Mutação repetida (`.Where(grant => true)`): **morta** (`StudentCourseAccessListTests` 13 testes, 1 falha, exit 2).

Diff desde a full 1/3 (`f2cfbef..ad7f1b8`): só 5 arquivos de teste em `src/`; nenhum código de produção mudou, então as 19 mutações mortas na tentativa 1 continuam válidas (mesmo código de produção).

## Gate e checks por componente

Fonte do CI: `.github/workflows/{learning,commerce,media,bff-student,student-spa}.yml` → `ci-dotnet.yml@v1` / `ci-react-ts.yml@v1` (restore, `dotnet format --verify-no-changes`, `dotnet test` Debug com cobertura ≥ 70%, publish Release, imagem/Trivy/SBOM/SAST em modo observe).

| Componente | Passo | Resultado |
|---|---|---|
| learning | format; publish Release | exit 0; exit 0 |
| learning | `dotnet test --solution` Debug + cobertura | exit 0, 245/245; cobertura (união de linhas) 96,08% |
| media | format; publish Release | exit 0; exit 0 |
| media | `dotnet test` | exit 0, 233/233; cobertura 91,69% |
| bff-student | format; publish Release | exit 0; exit 0 |
| bff-student | `dotnet test` | exit 0, 233/233; cobertura 86,72% |
| commerce | format; publish Release | exit 0; exit 0 |
| commerce | `dotnet test` (unit, arquitetura, e2e, integração) | exit 0, 391/391 (duas execuções, ambas exit 0); cobertura 96,12% (agora medida no conjunto inteiro) |
| student-spa | `npm run lint`, `typecheck` | exit 0, exit 0 |
| student-spa | `npm run test` (vitest + cobertura) | exit 0, 157/157; linhas 89,14% |
| student-spa | `vite build --base=/student/` | exit 0 |
| Kibana | `import_observabilidade.py --verify-only` | exit 0 |
| Compose | não repetido: nenhum arquivo de compose mudou desde a full 1/3 (que os validou) | — |

Nota de execução: uma primeira tentativa minha rodou dois laços `dotnet test` concorrentes sobre os mesmos containers e logs; `media` e `bff-student` falharam nessa execução concorrente (contenção de Testcontainers, não do código). Descartei esses resultados e refiz `learning`, `media` e `bff-student` sozinhos, em sequência, com logs próprios: tudo exit 0 (tabela acima). `commerce` passou nas duas execuções.

Não reproduzíveis aqui (CI hospedado): build/push de imagem, Trivy, SBOM, SAST, segredos e dependências (`security-mode: observe`).

## Sensor de discriminação

Na tentativa 1: 21 mutações, 19 mortas, 2 sobreviventes (B2, B3). Nesta tentativa, em `git worktree` isolado em `HEAD`, repeti os dois sobreviventes (M1 e C1) contra o código corrigido: ambos **mortos** (acima). As outras 19 não foram repetidas porque o código de produção é idêntico. Worktree removido; HEAD, árvore e `git status --porcelain` conferidos contra a linha de base.

## Rastreabilidade, contratos e segurança

Sem mudança desde a tentativa 1, cujas conclusões se mantêm: RF-01 a RF-06 e RN-P01 a P10 têm teste de integração ou de SPA; formatos de `learning` e BFF batem com os recortes do PRD; `learning` usa `sub` como aluno, decisão de acesso antes dos dados, escopo `course-access:read` só para `learning`, e agora a fronteira entre alunos de `commerce` tem teste que discrimina; ordem de implantação coerente com a TechSpec.

## Recomendações (não bloqueiam)

1. Executar os cenários no navegador de 4.0–6.0 (retomada em 4:12, Continuar, migração entre seções, indisponibilidade) com `scripts/apps.sh start --remote` antes de implantar; esta revisão só verificou por testes automatizados.
2. Tolerar `endedAt`/`endedOn` ausentes no cliente da lista quando a revogação (CAP-009) existir.
3. Helper de validação de resposta OpenAPI em `src/contract-testing` para as rotas novas, após a promoção dos recortes a `contracts/`; a promoção para `contracts/` fica para a conclusão do PRD.
4. Existe um worktree antigo `/tmp/run.6nWwePRy-media-sensor` (3b8339a, de uma revalidação anterior), fora do escopo desta revisão; vale removê-lo com `git worktree remove`.

## Limitações declaradas

- Imagens, Trivy, SBOM, SAST, segredos e dependências do CI não foram reproduzidos.
- Provas no navegador não executadas.
- Os timeouts de autenticação intermitentes do Postgres remoto citados no contexto não afetaram esta revisão (testes de integração usam Testcontainers).
