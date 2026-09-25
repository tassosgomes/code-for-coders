---
status: pending
task_kind: enabling
blocked_by: []
gate: 'dotnet build src/identity/CodeForCoders.Identity.slnx'
gate_expect: 'build de Identity sem erros e sem warnings novos; regressão de CAP-001 e testes do verificador em Verificações do projeto'
---

# 1.0 Credencial de serviço do bff-admin aceita por Identity sem abrir escopo de aluno

**Fatia:** EN-01 · **Cobre:** RN-18 (fronteira de confiança) · **Spec:** `techspec.md#habilitadores-inevitáveis` · **ADR:** [ADR-0004](../../docs/adr/0004-autenticacao-de-servico-bff-identity.md), [ADR-0005](../../docs/adr/0005-sessao-e-servico-do-backoffice.md)

## Comportamento

Identity passa a verificar asserções de serviço de **mais de um emissor**. Cada emissor tem as
suas chaves públicas (por `kid`), a sua lista de **escopos permitidos** e os tenants autorizados:

- `bff-student` mantém exatamente os escopos de CAP-001 (`student-*`); todos os endpoints de aluno
  continuam aceitando a asserção dele como hoje.
- `bff-admin` recebe os escopos do [contrato interno](internal-api-contract.yaml)
  (`staff-sessions:*`, `staff-passwords:reset`, `staff-invitations:*`, `staff-members:*`).
- Asserção do `bff-student` com escopo de backoffice, ou do `bff-admin` com escopo de aluno, é
  recusada com 401 `SERVICE_UNAUTHORIZED` antes de qualquer caso de uso — mesmo que a assinatura
  seja válida.
- Configuração inválida (emissor sem chave, chave RSA < 2048, emissor sem escopo, tenant inválido)
  impede a partida.

No ambiente local, `scripts/generate-local-env.sh` gera o par de chaves do `bff-admin` e o Compose
injeta a chave pública em Identity (emissor `bff-admin`) e a privada no `bff-admin`. Como o script
não sobrescreve `.env` existente, a documentação local explica como regenerá-lo.

Por que é habilitador: não há operação de backoffice ainda para observar; sem esta credencial
nenhuma fatia consegue chamar Identity. Desbloqueia V-01.

## Fora do escopo desta task

Nenhum endpoint de backoffice em Identity nem no `bff-admin` (V-01 em diante). A fábrica de asserção
do `bff-admin` nasce em 2.0, que é a primeira que chama Identity.

## Decisões fechadas

- Emissores separados por borda, escopos por emissor: ADR-0005 (3).
- Mecanismo de asserção assimétrica, `jti` contra replay: ADR-0004.

## Modificar / Referenciar

- **modificar:** `src/identity/src/CodeForCoders.Identity.Api/Security/ServiceAssertionVerifier.cs` (vários emissores, escopo por emissor)
- **modificar:** `src/identity/src/CodeForCoders.Identity.Api/Security/ServiceAssertionOptions.cs` e `src/identity/src/CodeForCoders.Identity.Api/Extensions/ServiceAssertionExtensions.cs` (configuração e validação de partida)
- **modificar:** `src/identity/src/CodeForCoders.Identity.Api/appsettings.json`, `docker-compose.yml`, `scripts/generate-local-env.sh`, `docs/student-registration-local-development.md`
- **modificar:** testes de Identity do verificador (unitários) e os de integração que montam a configuração de asserção
- **ref:** `internal-api-contract.yaml` (escopos), `tasks/prd-conta-aluno/internal-api-contract.yaml` (escopos de aluno), skill `dotnet`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/identity` | `dotnet format src/identity/CodeForCoders.Identity.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` passo Lint |
| `src/identity` | `dotnet build src/identity/CodeForCoders.Identity.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` (build no test) |
| `src/identity` | `dotnet test --project src/identity/tests/CodeForCoders.Identity.ArchitectureTests/CodeForCoders.Identity.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` passo Testes |
| `src/identity` | `dotnet test --project src/identity/tests/CodeForCoders.Identity.UnitTests/CodeForCoders.Identity.UnitTests.csproj` | exit 0 | `ci-dotnet.yml` passo Testes |
| `src/bff-student` | `dotnet test --project src/bff-student/tests/CodeForCoders.BffStudent.EndToEndTests/CodeForCoders.BffStudent.EndToEndTests.csproj` | exit 0 (regressão de CAP-001) | `ci-dotnet.yml` passo Testes |

Cobertura agregada ≥ 70% por componente, `dotnet publish` e build de imagem ficam para a validação full.

Regressão obrigatória nesta task: `dotnet test --project src/identity/tests/CodeForCoders.Identity.IntegrationTests/CodeForCoders.Identity.IntegrationTests.csproj -- --filter-class CodeForCoders.Identity.IntegrationTests.StudentSessionTests --minimum-expected-tests 4` (exit 0).

## Pronto quando

- [ ] Gate passa (exit 0).
- [ ] Testes do verificador: asserção `bff-admin` aceita em escopo de backoffice; asserção `bff-student` recusada em escopo de backoffice; asserção `bff-admin` recusada em escopo de aluno; emissor desconhecido recusado.
- [ ] Configuração sem chave para um emissor impede a partida.
- [ ] Testes de sessão de aluno de CAP-001 continuam passando.
