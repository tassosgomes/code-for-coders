---
status: pending
task_kind: vertical
blocked_by: ["2.0", "3.0"]
gate: 'dotnet test --project src/identity/tests/CodeForCoders.Identity.IntegrationTests/CodeForCoders.Identity.IntegrationTests.csproj -- --filter-class CodeForCoders.Identity.IntegrationTests.TaxDocumentTests --minimum-expected-tests 7 && dotnet test --project src/billing/tests/CodeForCoders.Billing.IntegrationTests/CodeForCoders.Billing.IntegrationTests.csproj -- --filter-class CodeForCoders.Billing.IntegrationTests.FiscalHolderInformedTests --minimum-expected-tests 3 && dotnet test --project src/bff-student/tests/CodeForCoders.BffStudent.IntegrationTests/CodeForCoders.BffStudent.IntegrationTests.csproj -- --filter-class CodeForCoders.BffStudent.IntegrationTests.MyTaxDocumentProxyTests --minimum-expected-tests 3 && npm --prefix src/student-spa run test -- student-fiscal-holder'
gate_expect: "Pelo menos 13 testes .NET passam (7 de identity, 3 de billing e 3 do bff-student) e os testes do student-spa com o fragmento student-fiscal-holder passam"
---

# 4.0 O aluno informa o tomador e a nota que só esperava isso entra na fila

**Fatia:** V-02 · **Cobre:** RF-03; RN-F07, RN-F08, RN-F09; US-02 · **Spec:** `techspec.md#v-02` · **ADR:** 0021

## Comportamento

No pedido pago em *Aguardando tomador*, a página mostra o campo e o texto aprovados em 2.0. O aluno grava CPF, CNPJ ou NIF na conta, na própria página. O curso continua acessível pelo caminho que já existe. A compra não passa a exigir o documento para concluir.

`putMyTaxDocument` segue para `putStudentTaxDocumentInternal`, com a sessão do aluno, CSRF e `Idempotency-Key`. O BFF não valida dígito.

- CPF `52998224725` e CNPJ `19131243000197` gravam. O valor na conta é só dígito.
- CPF ou CNPJ com dígito trocado → 422 `TAX_DOCUMENT_INVALID` e a conta não muda. A nota continua *Aguardando tomador*. A página mostra o texto de erro aprovado em 2.0.
- NIF vazio ou só espaços → 422 e nada grava. NIF aparado, não vazio, até 40 caracteres, grava.
- Gravar o mesmo valor não publica fato. Valor novo publica `identidade.identificacao-fiscal-informada.v1` sem CPF, CNPJ, NIF nem e-mail no payload, no outbox e na routing key.
- `getMyTaxDocument` devolve o documento da conta, ou ausência, com `Cache-Control: no-store`. Conta sem documento não é 404. O financeiro não edita esse documento.

Fiscal consome o fato na fila própria ligada a `identity.events`, relê `getStudentTaxDocumentInternal` pelo adaptador real de 3.0 e:

- documento que estava `awaiting-holder`, tomador agora válido, prestador ainda não utilizável → `awaiting-provider`, sem entrar na fila;
- o mesmo caso, com prestador já utilizável → enfileira e fica `queued`.

Nota já *Emitida* não existe ainda como transição desta task. A página, com a consulta da nota respondendo `issued`, mantém o campo para a próxima compra e o texto de que o PDF já disponível não muda. Trocar o documento nessa resposta não pede outra emissão.

A rota de contato do aluno continua exigindo o emissor `notification`. `billing` não a reutiliza.

## Fora do escopo desta task

- Ligar o prestador e soltar em lote → 5.0.
- Congelar tomador, PDF e aviso na autorização → 6.0. Aqui a página só mostra o campo quando a consulta já devolve `issued`.
- Apagar o documento um ano depois da gravação → 8.0.

## Decisões fechadas

- O documento mora na conta e serve à próxima compra (DP-02, RN-F08). O financeiro não o edita.
- O fato não leva o documento (C-05, ADR-0021).
- Sem identificação válida o documento fica *Aguardando tomador*, com prioridade sobre a falta de prestador (RN-F07).

## Modificar / Referenciar

- **modificar:** `src/identity/src/CodeForCoders.Identity.Domain/Entities/Account.cs` (documento do tomador); `src/identity/src/CodeForCoders.Identity.Api/Endpoints/StudentContactEndpoints.cs` (a rota de contato não passa a aceitar `billing` nem a devolver o documento); `src/student-spa/src/features/student-purchase/components/student-order-screen.tsx` (campo do tomador no pedido pago, no texto de 2.0)
- **ref:** `src/identity/src/CodeForCoders.Identity.Api/Extensions/EndpointExtensions.cs`; `tasks/prd-emissao-nfse/api-contract-conta.yaml`; `tasks/prd-emissao-nfse/internal-api-contract-identity.yaml`; `tasks/prd-emissao-nfse/internal-api-contract-identity-student.yaml`; `tasks/prd-emissao-nfse/asyncapi-contract-identity.yaml`; `docs/adr/0021-billing-le-tomador-em-identity.md`; `docs/design/wireframes-compra.md` (adendo aprovado em 2.0); `.claude/skills/dotnet/SKILL.md`; `.claude/skills/react/SKILL.md`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/identity` | `dotnet format src/identity/CodeForCoders.Identity.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/identity` | `dotnet test --project src/identity/tests/CodeForCoders.Identity.ArchitectureTests/CodeForCoders.Identity.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/identity` | `dotnet ef migrations has-pending-model-changes --project src/identity/src/CodeForCoders.Identity.Infra.Data --startup-project src/identity/src/CodeForCoders.Identity.Api` | exit 0, sem mudança pendente | AGENTS.md |
| `src/identity` | `dotnet test --project src/identity/tests/CodeForCoders.Identity.IntegrationTests/CodeForCoders.Identity.IntegrationTests.csproj -- --filter-class CodeForCoders.Identity.IntegrationTests.StudentContactTests --minimum-expected-tests 1` | exit 0 (o contato continua do emissor `notification`) | `ci-dotnet.yml` Testes |
| `src/billing` | `dotnet format src/billing/CodeForCoders.Billing.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/bff-student` | `dotnet format src/bff-student/CodeForCoders.BffStudent.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/bff-student` | `dotnet test --project src/bff-student/tests/CodeForCoders.BffStudent.ArchitectureTests/CodeForCoders.BffStudent.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/student-spa` | `npm --prefix src/student-spa run lint` | exit 0 | `ci-react-ts.yml` Lint |
| `src/student-spa` | `npm --prefix src/student-spa run typecheck` | exit 0 | `ci-react-ts.yml` Typecheck |
| `src/student-spa` | `npm --prefix src/student-spa run test -- order-return` | exit 0 (o pedido pago continua abrindo) | `ci-react-ts.yml` Testes |

## Pronto quando

- [ ] Gate focalizado passa (exit 0).
- [ ] Dígito trocado e NIF vazio respondem 422, a conta não muda e a nota continua *Aguardando tomador*.
- [ ] CPF válido com prestador ausente passa a *Aguardando prestador* e não entra na fila; com prestador utilizável, fica `queued`.
- [ ] O fato publicado não contém documento nem e-mail, e repetir o mesmo valor não publica de novo.
- [ ] O curso continua acessível, e a rota de contato não atende `billing`.
- [ ] No pedido pago, o campo e o erro de dígito são os do adendo aprovado.
