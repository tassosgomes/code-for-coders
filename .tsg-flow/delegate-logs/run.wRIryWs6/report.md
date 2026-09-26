# Implementer — task 8.0 (fix, tentativa 2/3)

Run: run.wRIryWs6

- Worktree: `/home/tsgomes/github-tassosgomes/code-for-coders-acesso-interno` (branch `feature/acesso-interno`), HEAD `3422698bf338fc8b1379b7bbb8045ba6f6e1479e`
- Bloqueantes tratados: B2 (cobertura commerce) e B3 (cobertura learning/media), de `prd_review.md` (run.QeuEMpDO)

## Resultado

**IMPLEMENTATION COMPLETE**. O gate e todos os checks saíram com exit 0. Os três serviços ficaram com cobertura ≥ 70%.

## Arquivos alterados

Todos são novos e seguem o precedente de `src/bff-student/tests/` (decisão do usuário, commit `13f2d8b`):

- `src/commerce/tests/Directory.Build.props`, `src/commerce/tests/testconfig.json`
- `src/learning/tests/Directory.Build.props`, `src/learning/tests/testconfig.json`
- `src/media/tests/Directory.Build.props`, `src/media/tests/testconfig.json`

O `testconfig.json` é idêntico byte a byte ao precedente. O `Directory.Build.props` também é idêntico, exceto
pelo nome do serviço no comentário, o mesmo ajuste que a task 3.0 fez em identity, bff-admin e notification.
Nenhum dos três serviços tinha `tests/Directory.Build.props`, então não houve conteúdo a integrar.

Não houve mudança em código de produção, testes, contratos, techspec ou `flow-state.json`.

## Cobertura (medida como o CI)

Comando, rodado em `src/<svc>`: `dotnet restore && dotnet test --no-restore --configuration Debug --coverage --coverage-output-format cobertura`.
A agregação usa a união de linhas de todos os relatórios Cobertura, com o script Python extraído de
`tassosgomes/template-pipeline/.github/workflows/ci-dotnet.yml@v1` (baixado com `gh api`). Antes de cada medição,
removi os `TestResults` antigos.

| Serviço | Antes (full run.QeuEMpDO) | Depois | Relatórios | Testes |
|---|---|---|---|---|
| commerce | 59,87% | **78,41%** | 4 | 20/20, exit 0 |
| learning | 55,69% | **76,09%** | 4 | 13/13, exit 0 |
| media | 56,98% | **77,03%** | 4 | 13/13, exit 0 |

Nenhum dos relatórios gerados contém mais `obj/` ou `*.generated.cs`: `grep -c` deu 0 nos 12 arquivos.
Todos os serviços passaram de 70%, então não precisei acrescentar testes: o contexto só pedia testes onde a
cobertura continuasse abaixo desse limite. Os `TestResults` criados pela medição foram removidos.

## Gate declarado (via `rtk proxy`, sem alteração)

Exit **0**, com 15 testes: Commerce `FinanceAreaAuthorizationTests` 7/7, Identity `UserTokenSigningKeysTests` 3/3,
BFF `FinanceAreaTests` 3/3 e SPA `-t FinanceArea` com 2 passando. O `gate_expect` pede ≥ 12 (5/3/3/1) e foi atendido.

## Verificações (cada uma rodou em comando separado e saiu com exit 0, sem warnings)

- commerce, learning, media, identity, bff-admin: `dotnet format <slnx> --verify-no-changes`, `dotnet build <slnx>` e ArchitectureTests.
- Identity UnitTests e BffAdmin UnitTests.
- admin-spa: `lint`, `typecheck` e `build`.
- Suítes completas de commerce, learning e media (todos os projetos de teste): rodaram na medição de cobertura acima, com exit 0.

## Limitações

- Não rodei o smoke com Docker Compose, por decisão do contexto.
- Não rodei de novo as suítes completas de identity, bff-admin e admin-spa. Esta correção não toca esses
  componentes: os arquivos novos ficam em `src/<commerce|learning|media>/tests/`.
- O valor "antes" vem da revisão full, não de uma nova medição.
