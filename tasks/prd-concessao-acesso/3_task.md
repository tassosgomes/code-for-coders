---
status: done
task_kind: vertical
blocked_by: ["2.0"]
gate: 'dotnet test --project src/identity/tests/CodeForCoders.Identity.UnitTests/CodeForCoders.Identity.UnitTests.csproj -- --filter-class CodeForCoders.Identity.UnitTests.StaffRoleCatalogCourtesyPermissionTests --minimum-expected-tests 4 && dotnet test --project src/identity/tests/CodeForCoders.Identity.IntegrationTests/CodeForCoders.Identity.IntegrationTests.csproj -- --filter-class CodeForCoders.Identity.IntegrationTests.StudentAccountLookupTests --minimum-expected-tests 8 && dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.IntegrationTests/CodeForCoders.BffAdmin.IntegrationTests.csproj -- --filter-class CodeForCoders.BffAdmin.IntegrationTests.CourtesyLookupProxyTests --minimum-expected-tests 5 && npm --prefix src/admin-spa run test -- courtesy-student-lookup'
gate_expect: "Pelo menos 21 testes passam: 4 unitários e 8 de integração de identity, 5 do BFF, 4 do SPA"
---

# 3.0 O financeiro abre a área Cortesias e localiza o aluno pelo e-mail

**Fatia:** V-01 · **Cobre:** RF-01, RF-02 (conta), US-01 (início), RN-D09, RN-D11, RN-D17, RN-01, RN-12, RN-13, RN-16, RN-17, RN-18, RN-21, RN-23, DP-01, DP-02, C-02, C-08 · **Spec:** `techspec.md#v-01-o-financeiro-abre-a-área-de-cortesias-e-localiza-o-aluno-pelo-e-mail` · **ADR:** [ADR-0005](../../docs/adr/0005-sessao-e-servico-do-backoffice.md), [ADR-0010](../../docs/adr/0010-autenticacao-de-servico-commerce-em-identity.md)

## Comportamento

- **`identity` — permissão:** o papel financeiro passa a carregar `cortesia.conceder` (além de `financeiro.ler` e
  `oferta.editar`); professor, suporte e administrador não a recebem, e o administrador **não herda** a área
  (DP-03 de CAP-002). A permissão chega à sessão e ao JWT do ator pelo catálogo de papéis, sem outro ponto de
  alteração. O enum `Permission` dos contratos de Identity já a lista (C-08).
- **`identity` — `lookupStudentAccountInternal`:** `POST /internal/v1/student-account-lookups` valida a asserção
  do `bff-admin` com o escopo novo `student-account:lookup`, a sessão interna vigente (`X-Staff-Session`) e a
  permissão `cortesia.conceder` **na sessão**; normaliza o e-mail como o cadastro (`Trim` e minúsculas, RN-01);
  busca por `NormalizedEmail` no tenant da asserção, **só conta de aluno**; devolve e-mail, nome,
  `emailConfirmed` e `status` (`active` ou `disabled`, a partir de `DeactivatedOn`). Inexistente, conta de ator
  interno e outro tenant devolvem o **mesmo** 404 `STUDENT_ACCOUNT_NOT_FOUND`. Sem asserção válida → 401
  `SERVICE_UNAUTHORIZED`; sem sessão → 401 `SESSION_REQUIRED`; sem escopo ou sem a permissão → 403
  `PERMISSION_DENIED`; corpo inválido → 400 `VALIDATION_ERROR`. Nada além de e-mail e nome é devolvido (RN-16).
- **`bff-admin` — `lookupStudentAccount`:** `POST /api/v1/student-account-lookups` com cookie de sessão e CSRF
  (G18); valida a sessão, exige `cortesia.conceder` e repassa ao Identity com a asserção do BFF e o
  `X-Staff-Session`; a lista de escopos permitidos do assinador ganha `student-account:lookup`. Respostas:
  200, 404 indistinto, 401, 403, 502 `IDENTITY_UNAVAILABLE`, 504 `UPSTREAM_TIMEOUT`. A resposta sai com
  `Cache-Control: no-store`.
- **`admin-spa`:** item de menu **Cortesias**, visível só com `cortesia.conceder`; rota `/cortesias` com o passo
  *Aluno*: campo de e-mail, a localização como **mutação** (nunca uma consulta com o e-mail na chave do cache), o
  resultado (e-mail e nome), o aviso "e-mail ainda não confirmado" (que não impede), a conta desativada (que
  impede seguir) e o "Não há conta de aluno com este e-mail". O estado do formulário vive no cliente e é
  descartado ao sair da área. Sem permissão, o item não aparece e a rota direta mostra o acesso negado.
  Conforme o Figma aprovado em 2.0.
- **E-mail fora dos lugares errados (RN-21, G23):** o e-mail não aparece em URL, chave de cache, log, span ou
  métrica, em nenhum dos três serviços; nenhum valor dele é guardado em armazenamento do navegador.
- **Configuração:** o emissor `bff-admin` de Identity ganha o escopo novo nos três Compose e no script de
  ambiente local.
- **Caso negativo principal:** e-mail de ator interno → o mesmo 404 de inexistente; professor, suporte,
  administrador e aluno → 403 no BFF **e** em Identity chamado direto; papel revogado → a próxima ação da sessão
  aberta é recusada (RN-17).

## Fora do escopo desta task

Cursos (4.0), concessão (5.0), lista de concessões do aluno (6.0). A reconfirmação da conta por `commerce` e o escopo `student-account:confirm` ficam em 5.0. Resolução da referência `conta-aluno` na trilha fica em 9.0.

## Decisões fechadas

- Localização pelo e-mail exato, sem lista nem busca aproximada (DP-02); devolução mínima (RN-16).
- A localização é de Identity, nunca de `commerce`: o e-mail não vai a `commerce` ([ADR-0010](../../docs/adr/0010-autenticacao-de-servico-commerce-em-identity.md), decisão 5).
- Autorização em duas camadas: BFF e serviço dono, com a sessão e o JWT (ADR-0005); `bff-admin` e `admin-spa` implantados juntos.
- Nome da área e da rota: **Cortesias**, `/cortesias` (`techspec.md#decisões-técnicas`, D-01).

## Modificar / Referenciar

- **modificar:** `src/identity/src/CodeForCoders.Identity.Domain/Entities/StaffRoleCatalog.cs` (permissão no papel financeiro)
- **modificar:** `src/identity/src/CodeForCoders.Identity.Api/Security/ServiceAssertionScopes.cs` (escopo `student-account:lookup` do emissor staff); endpoints de Identity e o registro de endpoints (operação nova)
- **modificar:** `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Security/ServiceAssertionTokenFactory.cs` (escopo permitido); `Extensions/EndpointExtensions.cs` e o registro do cliente de Identity (grupo `/api/v1/student-account-lookups`)
- **modificar:** `src/admin-spa/src/features/staff-session/utils/get-staff-areas.ts`, `src/admin-spa/src/config/paths.ts`, `src/admin-spa/src/app/app-routes.tsx`, `src/admin-spa/src/components/app-shell.tsx` (item, rota, agrupamento e ícone)
- **modificar:** `docker-compose.yml`, `docker-compose.coolify.yml`, `docker-compose.remote.yml` (escopo do emissor `bff-admin`)
- **ref:** `src/identity/src/CodeForCoders.Identity.Api/Endpoints/AuditIdentityReferenceEndpoints.cs` (padrão de operação interna com asserção, sessão e erros); `src/identity/src/CodeForCoders.Identity.Application/UseCases/Accounts/RegisterStudentAccount/RegisterStudentAccount.cs` e `AuthenticateStaffSession.cs` (normalização do e-mail); `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Clients/AuditIdentityReferenceClient.cs` (cliente com asserção e `X-Staff-Session`); `src/admin-spa/src/features/catalog-courses/**` (padrão de feature); `internal-api-contract-identity.yaml` e `api-contract.yaml` (`lookupStudentAccount`); `docs/design/wireframes-cortesias.md` e Figma aprovado; skills `dotnet` e `react`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/identity` | `dotnet format src/identity/CodeForCoders.Identity.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/identity` | `dotnet build src/identity/CodeForCoders.Identity.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/identity` | `dotnet test --project src/identity/tests/CodeForCoders.Identity.IntegrationTests/CodeForCoders.Identity.IntegrationTests.csproj` | exit 0 (sem regressão) | `ci-dotnet.yml` Testes |
| `src/bff-admin` | `dotnet format src/bff-admin/CodeForCoders.BffAdmin.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/bff-admin` | `dotnet build src/bff-admin/CodeForCoders.BffAdmin.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/bff-admin` | `dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.IntegrationTests/CodeForCoders.BffAdmin.IntegrationTests.csproj` | exit 0 (sem regressão) | `ci-dotnet.yml` Testes |
| `src/admin-spa` | `npm --prefix src/admin-spa run lint` | exit 0 | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run typecheck` | exit 0 | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run test -- staff-session` | exit 0 (sem regressão) | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run build -- --base=/admin/` | exit 0 | `ci-react-ts.yml` |

Cobertura agregada ≥ 70%, `dotnet publish` e imagens ficam para a validação full.

## Pronto quando

- [x] Gate passa (exit 0) com pelo menos 21 testes.
- [x] Composição: `identity` e `bff-admin` iniciam no ambiente de teste com os registros reais (escopo, emissor, cliente) e o cliente HTTP real do BFF é exercitado contra a fronteira controlada do teste, sem dublê do adaptador.
- [x] `lookupStudentAccountInternal` direto: e-mail com maiúsculas e espaços nas bordas encontra a conta cadastrada pelo fluxo real; inexistente e conta de ator interno → o mesmo 404; desativada → `status: disabled`; e-mail não confirmado → `emailConfirmed: false`; outro tenant → 404; sem escopo ou sem a permissão → 403.
- [x] A inspeção da saída de log e dos spans de uma localização não contém o e-mail nem o nome.
- [x] Smoke no Compose (Identity, `bff-admin`, `admin-spa`, PostgreSQL, Valkey; `./scripts/generate-local-env.sh`, `./scripts/apps.sh start`, quatro papéis de equipe, uma conta de aluno ativa, uma não confirmada, uma desativada e uma de ator interno): `http://localhost:8081/admin/cortesias` como financeiro → passo *Aluno* e a localização; como professor, suporte e administrador, o menu não tem o item e a rota direta nega; revogar o papel e repetir a ação → recusada.
