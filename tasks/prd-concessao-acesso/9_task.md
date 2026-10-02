---
status: pending
task_kind: vertical
blocked_by: ["5.0", "2.0"]
gate: 'dotnet test --project src/identity/tests/CodeForCoders.Identity.IntegrationTests/CodeForCoders.Identity.IntegrationTests.csproj -- --filter-class CodeForCoders.Identity.IntegrationTests.AuditStudentReferenceTests --minimum-expected-tests 4 && dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.IntegrationTests/CodeForCoders.BffAdmin.IntegrationTests.csproj -- --filter-class CodeForCoders.BffAdmin.IntegrationTests.CourtesyAuditReferenceTests --minimum-expected-tests 5 && npm --prefix src/admin-spa run test -- audit-trail-courtesy'
gate_expect: "Pelo menos 13 testes passam: 4 de identity, 5 do BFF, 4 do SPA"
---

# 9.0 O administrador vê a cortesia na trilha, com aluno, curso, vigência e motivo legíveis

**Fatia:** V-07 · **Cobre:** RF-08 (rótulos e filtro), US-04, RN-A08, RN-21, RN-23, C-07 · **Spec:** `techspec.md#v-07-o-administrador-vê-a-cortesia-na-trilha-com-o-aluno-o-curso-a-vigência-e-o-motivo-legíveis` · **ADR:** —

## Comportamento

- **`identity`:** `resolveAuditIdentityReferencesInternal` passa a aceitar a referência `conta-aluno` (Identity
  1.0.0 → 1.1.0 do recorte de CAP-030) e devolve o **nome** da conta como rótulo, **nunca** o e-mail; a conta
  desativada ainda é identificada (RN-23); referência de outro tenant e inexistente são indistintas e voltam sem
  rótulo; só o administrador consulta.
- **`bff-admin`:** o tipo de referência `conta-aluno` entra na lista de tipos resolvidos por Identity; para o
  ato `cortesia-concedida` o BFF acrescenta ao detalhe o atributo **derivado** `cursoTitulo`, resolvido por
  `learning` pela operação que já existe para referências de curso (papel administrador); o schema não muda
  (`attributes` é mapa de texto). Falha de resolução devolve só o identificador, sem nome inventado.
- **`admin-spa`:** a trilha mostra o rótulo *Cortesia concedida*, oferece o tipo no filtro e, no detalhe,
  mostra o autor, o aluno **pelo nome**, o curso **pelo título** (`cursoTitulo`), a vigência formatada
  ("6 meses" ou "Vitalícia") e o motivo; o e-mail do aluno não aparece; referência não resolvida mostra só a
  identificação. Conforme o Figma aprovado em 2.0.
- **Caso negativo principal:** sessão sem papel administrador → 403; conta de outro tenant ou inexistente → sem
  rótulo, sem dizer qual.

## Fora do escopo desta task

Resolução do rótulo por `commerce` (a conta de aluno é de Identity); novos tipos de atos de outros domínios.

## Decisões fechadas

- O rótulo da conta de aluno é o nome, nunca o e-mail (RN-21, G23); o e-mail aparece só nas telas de cortesia.
- `cursoTitulo` é derivado na leitura, não gravado na Auditoria (`techspec.md#decisões-técnicas`, D-09).
- A consulta da trilha continua restrita ao administrador (RN-A09).

## Modificar / Referenciar

- **modificar:** `src/identity/src/CodeForCoders.Identity.Application/UseCases/Accounts/ResolveAuditIdentityReferences/` e a consulta de rótulos em `Infra.Data`; `src/identity/src/CodeForCoders.Identity.Api/Endpoints/AuditIdentityReferenceEndpoints.cs` (tipo aceito)
- **modificar:** `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Endpoints/AuditRecordEndpoints.cs` (tipos de referência e atributo derivado)
- **modificar:** `src/admin-spa/src/features/audit-trail/components/audit-trail-screen.tsx`, `audit-record-detail-screen.tsx`, `utils/format-offer-audit-attribute.ts`, `src/admin-spa/src/testing/handlers.ts`
- **ref:** `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Clients/CourseAuditReferenceEnricher.cs` (resolução do título pelo `learning`); `tasks/prd-consulta-trilha-auditoria/internal-api-contract-identity.yaml` 1.1.0; `docs/design/wireframes-cortesias.md` e Figma aprovado; skills `dotnet` e `react`

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
| `src/admin-spa` | `npm --prefix src/admin-spa run test -- audit-trail` | exit 0 (sem regressão) | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run build -- --base=/admin/` | exit 0 | `ci-react-ts.yml` |

Cobertura agregada ≥ 70%, `dotnet publish` e imagens ficam para a validação full.

## Pronto quando

- [ ] Gate passa (exit 0) com pelo menos 13 testes.
- [ ] Composição: `identity` e `bff-admin` iniciam no ambiente de teste com os registros reais, e o cliente HTTP real do BFF para Identity e para `learning` é exercitado contra a fronteira controlada do teste.
- [ ] O detalhe de um ato `cortesia-concedida` mostra "Cortesia concedida", o autor, o aluno **pelo nome**, o curso **pelo título**, a vigência e o motivo; o e-mail do aluno não aparece; o filtro por tipo oferece *Cortesia concedida*.
- [ ] Conta de outro tenant ou inexistente → sem rótulo, sem dizer qual; sem sessão de administrador → 403.
- [ ] Smoke no Compose (o de 5.0): conceder uma cortesia e abrir `http://localhost:8081/admin/auditoria` como administrador → o registro conforme aparece com os rótulos legíveis.
