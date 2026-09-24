---
status: done
task_kind: vertical
blocked_by: ["1.0"]
gate: "dotnet test --project src/audit/tests/CodeForCoders.Audit.IntegrationTests/CodeForCoders.Audit.IntegrationTests.csproj -- --filter-class CodeForCoders.Audit.IntegrationTests.AuditImmutabilityTests --minimum-expected-tests 7 && dotnet test --project src/audit/tests/CodeForCoders.Audit.UnitTests/CodeForCoders.Audit.UnitTests.csproj -- --filter-class CodeForCoders.Audit.UnitTests.AuditAppendOnlyGuardrailTests --minimum-expected-tests 2"
gate_expect: "Pelo menos 9 testes passam: 7 de integração de imutabilidade e 2 unitários da guarda de mutação"
---

# 5.0 Demonstrar imutabilidade com a credencial de execução

**Fatia:** V-05 · **Cobre:** RF-05, US-02, RN-A01, RN-A02, RN-A10 · **Spec:** `techspec.md#v-05` · **ADR:** [0002](../../docs/adr/0002-plataforma-de-runtime-coolify.md)

## Comportamento

O `audit` passa a rodar com uma **credencial de execução** própria: login membro de
`code_for_coders_audit_writer`, com só `USAGE` no schema `audit_access` e `SELECT, INSERT` em
`audit_records` (e na sequência, se houver). A credencial dona (`code_for_coders_audit`) fica restrita ao
passo de migration. `ConnectionStrings:DefaultConnection` é a de execução; a da migration é separada.

- Com a credencial de execução, `UPDATE`, `DELETE` e `TRUNCATE` diretos em `audit_records` falham
  por **privilégio**; o registro permanece idêntico.
- Com a credencial dona, `UPDATE` e `DELETE` falham pelo **trigger**.
- Reiniciar o host e republicar todas as mensagens já processadas não muda contagem nem conteúdo.
- Os papéis dos outros serviços não têm `CONNECT` no banco `code_for_coders_audit`.
- O `DbContext` recusa estados `Modified` e `Deleted` do Registro (teste unitário existente
  estendido à entidade nova).

Os grants do papel `writer` e o `REVOKE ... FROM PUBLIC` vão na migration que cria a tabela ou numa
nova, para que ambiente novo nasça correto; `scripts/init-local-databases.sql` cria o login local e o
`docker-compose.yml` aponta o `audit` para ele. O fixture de integração aplica as migrations com a
credencial dona e sobe o host com a de execução.

## Fora do escopo desta task

Provisionar o login nos ambientes da plataforma (QT-01 da TechSpec). Remover privilégio do dono sobre o
próprio banco: o trigger é a barreira nessa camada.

## Decisões fechadas

- Duas credenciais do mesmo serviço, conforme ADR-0002 e G13: techspec.md, seção ADR (conformidade explícita).
- Migration como passo de deploy, nunca no boot: ADR-0002.

## Modificar / Referenciar

- **modificar:** `src/audit/src/CodeForCoders.Audit.Infra.Data/DependencyInjection.cs` e `src/audit/src/CodeForCoders.Audit.Infra.Data/AuditDbContextFactory.cs` (conexão de execução ≠ de migration)
- **modificar:** `src/audit/src/CodeForCoders.Audit.Infra.Data/Configuration/AuditDatabasePermissions.sql` e `src/audit/src/CodeForCoders.Audit.Infra.Data/Configuration/AuditAppendOnlyGuardrail.md` (grants e as duas camadas)
- **modificar:** `scripts/init-local-databases.sql` e `docker-compose.yml` (login de execução local)
- **modificar:** `src/audit/tests/CodeForCoders.Audit.IntegrationTests/AuditIntegrationFixture.cs`, `src/audit/tests/CodeForCoders.Audit.UnitTests/AuditAppendOnlyGuardrailTests.cs`
- **ref:** `techspec.md#v-05`, `docs/adr/0002-plataforma-de-runtime-coolify.md`, skill `dotnet`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/audit` | `dotnet format src/audit/CodeForCoders.Audit.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` passo Lint |
| `src/audit` | `dotnet build src/audit/CodeForCoders.Audit.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` (restore/build implícito no test) |
| `src/audit` | `dotnet test --project src/audit/tests/CodeForCoders.Audit.ArchitectureTests/CodeForCoders.Audit.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` passo Testes (suíte do componente) |
| `src/audit` | `dotnet test --project src/audit/tests/CodeForCoders.Audit.UnitTests/CodeForCoders.Audit.UnitTests.csproj` | exit 0 | `ci-dotnet.yml` passo Testes |

Cobertura agregada ≥ 70% e `dotnet publish` ficam para a validação full.

## Pronto quando

- [ ] Gate passa (exit 0) com pelo menos 9 testes selecionados.
- [ ] As cinco tentativas de mutação (três com a de execução, duas com a dona) falham e o registro fica idêntico.
- [ ] O serviço sobe e registra atos com a credencial de execução no fixture e no Compose local (após recriar o volume ou reexecutar o init).
- [ ] Reinício + republicação mantêm contagem e conteúdo.
