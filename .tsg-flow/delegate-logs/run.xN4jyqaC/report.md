# Implementer — task 3.0 (fix, tentativa 3/3)

Run: run.xN4jyqaC
Resultado: **IMPLEMENTATION COMPLETE** (gate passed)

## Bloqueante tratado
B1 (prd_review.md, run.QeuEMpDO): cobertura do CI `bff-admin` 67,95% < 70%, causada em parte por
`obj/.../OpenApiXmlCommentSupport.generated.cs` (377 linhas, 0%) contado na métrica.

## Correção (decisão do usuário: seguir o precedente de audit/bff-student)
Mecanismo replicado exatamente como em `src/bff-student/tests`: `tests/Directory.Build.props` importa o
Directory.Build.props do nível acima e, para `IsTestProject == true`, copia `tests/testconfig.json` como
`$(AssemblyName).testconfig.json` para a saída (Microsoft.Testing.Platform). O `testconfig.json` tem conteúdo
idêntico ao de audit/bff-student (`Sources.Exclude`: `obj/` e `*.g.cs`/`*.generated.cs`).

Arquivos criados (nenhum código de produção nem teste foi alterado):
- `src/bff-admin/tests/Directory.Build.props`, `src/bff-admin/tests/testconfig.json`
- `src/identity/tests/Directory.Build.props`, `src/identity/tests/testconfig.json`
- `src/notification/tests/Directory.Build.props`, `src/notification/tests/testconfig.json`

Confirmado: `CodeForCoders.BffAdmin.UnitTests.testconfig.json` aparece em `bin/Debug/net10.0`, e os relatórios
Cobertura do bff-admin não contêm mais nenhum arquivo `obj/` ou `*.generated.cs`.

## Cobertura (medida como o CI)
Comando em `src/<componente>`: `dotnet restore && dotnet test --no-restore --configuration Debug --coverage
--coverage-output-format cobertura`. Relatórios Cobertura antigos (`TestResults`) foram removidos antes da
execução. A agregação usa a união de linhas de todos os relatórios, com o script Python extraído de
`tassosgomes/template-pipeline/.github/workflows/ci-dotnet.yml@v1`.

| Componente | Antes (full run.QeuEMpDO) | Depois | Testes (suíte completa) |
|---|---|---|---|
| bff-admin | 67,95% ❌ | **78,01%** ✅ | exit 0 · 43/43 (inclui EndToEnd, Integration) |
| identity | 75,92% | **78,60%** | exit 0 · 109/109 |
| notification | 78,21% | **85,37%** | exit 0 · 51/51 |

O "antes" vem da medição do validator em 893b8be e não foi repetido aqui. Com o bff-admin já acima de 70%,
não foram necessários testes extras (passo 3 do contexto).

## Gate
`dotnet test ... Identity.IntegrationTests -- --filter-class ...StaffSessionTests --minimum-expected-tests 5 &&
dotnet test ... BffAdmin.EndToEndTests -- --filter-class ...StaffSessionTests --minimum-expected-tests 5 &&
npm --prefix src/admin-spa run test -- -t StaffSession` (executado sem alterações, via `rtk proxy`)
→ exit 0. Identity 5/5, BFF 8/8, SPA 3 passed. Total 16 ≥ 11 (gate_expect atendido).

## Verificações do projeto (cada uma em comando separado)
| Comando | Exit |
|---|---|
| dotnet format src/identity/CodeForCoders.Identity.slnx --verify-no-changes | 0 |
| dotnet build src/identity/CodeForCoders.Identity.slnx | 0 (0 warnings) |
| dotnet test --project src/identity/tests/...ArchitectureTests | 0 |
| dotnet test --project src/identity/tests/...UnitTests | 0 |
| dotnet format src/bff-admin/CodeForCoders.BffAdmin.slnx --verify-no-changes | 0 |
| dotnet build src/bff-admin/CodeForCoders.BffAdmin.slnx | 0 (0 warnings) |
| dotnet test --project src/bff-admin/tests/...ArchitectureTests | 0 |
| dotnet test --project src/bff-admin/tests/...UnitTests | 0 |
| npm --prefix src/admin-spa run lint | 0 |
| npm --prefix src/admin-spa run typecheck | 0 |
| npm --prefix src/admin-spa run build | 0 |
| npm --prefix src/admin-spa run test (suíte completa) | 0 |
| dotnet test --project src/bff-student/tests/...EndToEndTests | 0 |
| dotnet format src/notification/CodeForCoders.Notification.slnx --verify-no-changes | 0 |
| dotnet build src/notification/CodeForCoders.Notification.slnx | 0 (0 warnings) |

Suítes completas de Identity IntegrationTests, Notification IntegrationTests e BFF-admin EndToEndTests foram
executadas dentro da medição de cobertura acima, com exit 0.

## Limitações
- Smoke Docker Compose não executado (proibido pelo contexto desta chamada).
- `dotnet publish` e o build de imagem não foram executados; ficam para a validação full.
- B2–B5 estão fora do escopo desta task. commerce, learning e media não foram tocados.
- `flow-state.json` (modificação do orquestrador) preservado sem edição.
