# Relatório — Task 1.0: Credencial de serviço do bff-admin

Run: run.ZehMLHWz

- Task: 1.0 (EN-01) · Mode: implement · Attempt: 1/3
- Worktree: `/home/tsgomes/github-tassosgomes/code-for-coders-acesso-interno` (branch `feature/acesso-interno`)
- Outcome: implementation_complete · Gate: passed

## O que foi implementado

Identity verifica asserções de serviço de mais de um emissor, com chaves, escopos e tenants por emissor:

- `src/identity/src/CodeForCoders.Identity.Api/Security/ServiceAssertionOptions.cs` — nova seção
  `ServiceAssertions:Issuers` (`PublicKeys`, `AllowedScopes`, `AllowedTenantIds` por emissor) com
  `GetEffectiveIssuers()`. Campos planos legados mantidos como fallback que sintetiza o emissor
  `bff-student` com os escopos de CAP-001, para `.env`/config antiga continuar subindo.
- `src/identity/src/CodeForCoders.Identity.Api/Security/ServiceAssertionScopes.cs` (novo) — conjuntos
  canônicos: 9 escopos `student-*` (CAP-001) e 8 escopos `staff-*` (internal-api-contract.yaml).
- `src/identity/src/CodeForCoders.Identity.Api/Security/ServiceAssertionVerifier.cs` — resolve o emissor
  pelo claim `iss` (desconhecido → nulo = 401 `SERVICE_UNAUTHORIZED`); `kid` procurado só nas chaves
  daquele emissor; exige o escopo requerido no token **e** na allowlist do emissor; tenant validado
  contra os tenants do emissor. Cruzamento `bff-student`×backoffice e `bff-admin`×aluno recusado antes
  de qualquer caso de uso.
- `src/identity/src/CodeForCoders.Identity.Api/Extensions/ServiceAssertionExtensions.cs` — validação de
  partida via `ServiceAssertionConfigurationValidator` público/testável: audience obrigatória, ≥1 emissor,
  e por emissor nome, ≥1 chave pública RSA ≥ 2048 válida, ≥1 escopo, ≥1 tenant válido. Falha impede a
  partida (`ValidateOnStart`).
- `src/identity/src/CodeForCoders.Identity.Api/appsettings.json` — emissores `bff-student` (9 escopos
  `student-*`) e `bff-admin` (8 escopos `staff-*`).
- `docker-compose.yml` — Identity recebe `ServiceAssertions:Issuers:bff-student:*` (chave existente) e
  `Issuers:bff-admin:*` (nova `BFF_ADMIN_IDENTITY_PUBLIC_KEY_B64`); `bff-admin` recebe a privada
  (`StaffIdentity__*`, gancho para a fábrica da task 2.0).
- `scripts/generate-local-env.sh` — gera o segundo par RSA do `bff-admin`
  (`BFF_ADMIN_IDENTITY_PUBLIC_KEY_B64` / `BFF_ADMIN_IDENTITY_PRIVATE_KEY_B64`).
- `docs/student-registration-local-development.md` — seção CAP-002 com instrução de regenerar o `.env`
  (o script não sobrescreve o existente; sem as novas chaves o Identity recusa a partida).

## Testes

- Novos `ServiceAssertionVerifierTests` (6) e `ServiceAssertionConfigurationValidatorTests` (7):
  `bff-admin` aceito em escopo de backoffice; `bff-student` aceito em escopo de aluno; cruzamentos
  recusados; emissor desconhecido recusado; chave de outro emissor recusada; emissor sem chave,
  RSA < 2048, sem escopo, tenant inválido, sem audience e fallback legado cobertos.
- `IdentityApiFactory` (E2E) migrada para `Issuers` com os dois emissores + `CreateAdminServiceAssertion`;
  novo Fact E2E prova 401 `SERVICE_UNAUTHORIZED` para asserção `bff-admin` e para `bff-student` em
  escopo de backoffice no endpoint de aluno.

## Evidência (gate + verificações do projeto)

- Gate `dotnet build src/identity/CodeForCoders.Identity.slnx`: exit 0, 12 projetos, 0 erros, 0 warnings.
- `dotnet format ... --verify-no-changes`: exit 0.
- ArchitectureTests: 8/8 passou (execução direta do runner).
- UnitTests: 15/15 passou (2 pré-existentes + 13 novos; execução direta do runner).
- Regressão CAP-001 `StudentSessionTests` (integration, `--minimum-expected-tests 4`): 4/4 passou.
- Identity EndToEndTests: 6/6 passou (inclui o novo teste inter-emissor).
- bff-student EndToEndTests (regressão CAP-001): 18/18 passou.
- `bash -n scripts/generate-local-env.sh` OK; `docker-compose.yml` parseado como YAML OK.
- Nota operacional: `dotnet test --project` devolve "Zero tests ran" (exit 5) neste ambiente para todos
  os projetos, inclusive os intocados — quirk do runner MTP v2 aqui; os mesmos assemblies executados
  diretamente passam. Sem relação com esta mudança.

## Fora de escopo (conforme a task)

Nenhum endpoint de backoffice; fábrica de asserção do `bff-admin` fica para a task 2.0 (o Compose já
entrega a chave privada no container como gancho `StaffIdentity__*`).

## Arquivos tocados

M: `docker-compose.yml`, `docs/student-registration-local-development.md`,
`scripts/generate-local-env.sh`,
`src/identity/src/CodeForCoders.Identity.Api/{Security/ServiceAssertionVerifier.cs,Security/ServiceAssertionOptions.cs,Extensions/ServiceAssertionExtensions.cs,appsettings.json}`,
`src/identity/tests/CodeForCoders.Identity.{EndToEndTests/IdentityApiFactory.cs,EndToEndTests/StudentPasswordChangeEndpointTests.cs,UnitTests/CodeForCoders.Identity.UnitTests.csproj}`.
Novos: `Security/ServiceAssertionScopes.cs`, `UnitTests/ServiceAssertionVerifierTests.cs`,
`UnitTests/ServiceAssertionConfigurationValidatorTests.cs`.
Não tocados: checkout principal, `main`, `tasks/prd-acesso-interno/flow-state.json` (orquestrador).
