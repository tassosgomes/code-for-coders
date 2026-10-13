---
status: pending
task_kind: vertical
blocked_by: ["2.0", "3.0"]
gate: 'dotnet test --project src/billing/tests/CodeForCoders.Billing.IntegrationTests/CodeForCoders.Billing.IntegrationTests.csproj -- --filter-class CodeForCoders.Billing.IntegrationTests.ServiceProviderTests --minimum-expected-tests 7 && dotnet test --project src/identity/tests/CodeForCoders.Identity.IntegrationTests/CodeForCoders.Identity.IntegrationTests.csproj -- --filter-class CodeForCoders.Identity.IntegrationTests.FiscalManagePermissionTests --minimum-expected-tests 4 && dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.IntegrationTests/CodeForCoders.BffAdmin.IntegrationTests.csproj -- --filter-class CodeForCoders.BffAdmin.IntegrationTests.ServiceProviderProxyTests --minimum-expected-tests 3 && npm --prefix src/admin-spa run test -- admin-service-provider'
gate_expect: "Pelo menos 14 testes .NET passam (7 de billing, 4 de identity e 3 do bff-admin) e os testes do admin-spa com o fragmento admin-service-provider passam"
---

# 5.0 O financeiro conecta o prestador e solta quem já tem tomador

**Fatia:** V-03 · **Cobre:** RF-02; RN-F05, RN-F10, RN-16; US-04 · **Spec:** `techspec.md#v-03` · **ADR:** 0005

## Comportamento

O papel financeiro abre `/financeiro/prestador`, no desenho aprovado em 2.0, e informa a chave do emissor, o código de serviço e a alíquota. Não há valor de fábrica (QA-02). A venda segue enquanto o prestador não está utilizável.

`putServiceProvider` exige `fiscal.gerir`. `getServiceProvider` exige `financeiro.ler` e não devolve a chave. A chave não aparece em log, outbox nem na resposta, e não permanece no estado da tela depois do envio.

- Primeira gravação sem chave → 422 `PROVIDER_KEY_REQUIRED`.
- Gravação seguinte sem chave, quando já existe uma, mantém a anterior.
- Alíquota fora do intervalo do contrato → 422 `PROVIDER_INVALID`.
- Utilizável só com os três: chave, código de serviço e alíquota. Incompleto não é utilizável, e o documento que esperava o prestador continua `awaiting-provider`.
- Ao ficar utilizável, todo documento `awaiting-provider` da escola entra na fila, cada um com a própria submissão, sem ação pedido a pedido. Documento `awaiting-holder` não entra.
- A chave fica cifrada, AES-GCM, 256 bits, chave de configuração por tenant, na família já usada no outbox de Identity. A leitura de prestador nunca a devolve.

`fiscal.gerir` entra no papel financeiro ao lado de `financeiro.ler`. Professor e suporte não ganham nenhuma das duas: 403, sem dado fiscal, e o token de professor não chama `billing`. A navegação da área continua exigindo `financeiro.ler`.

O emissor de ator passa a emitir audiência `billing` com escopo `fiscal-documents:use` quando essa audiência está configurada. Audiência ausente continua recusada. O aluno não entra nesta task: a audiência `billing` / `fiscal-document:own` dele é configuração feita em 3.0.

A tela é a única origem do código e da alíquota. Salvar sem `fiscal.gerir` responde 403.

## Fora do escopo desta task

- O aluno informar o tomador → 4.0.
- Baixar a nota, aviso e marca de estudo → 6.0.
- Nova tentativa → 7.0.
- Conta de produção da Notaas e certificado A1 (QA-03).

## Decisões fechadas

- Um código de serviço e uma alíquota por escola (DP-03, RN-F05, QA-02).
- A chave não volta na resposta (C-07).
- Professor e suporte não veem dado fiscal (RN-16, ADR-0005).
- Ao ficar utilizável, quem esperava só o prestador entra na fila sozinho (RN-F10).

## Modificar / Referenciar

- **modificar:** `src/identity/src/CodeForCoders.Identity.Domain/Entities/StaffRoleCatalog.cs` (`fiscal.gerir` no papel financeiro); `src/identity/src/CodeForCoders.Identity.Api/Security/StaffSessionTokenIssuer.cs` (audiência `billing` configurada é emitida; ausente, continua recusada); `src/admin-spa/src/config/paths.ts` (rota `/financeiro/prestador`); `docker-compose.yml`, `docker-compose.remote.yml`, `docker-compose.komodo.apis.yml`, `docker-compose.stable.yml` e `scripts/generate-local-env.sh` (chave de cifra do prestador, no padrão das chaves que já existem)
- **ref:** `src/identity/src/CodeForCoders.Identity.Infra.Data/Outbox/OutboxPayloadProtector.cs` (família AES-GCM; não cifrar o outbox de `billing`); `src/identity/src/CodeForCoders.Identity.Api/Extensions/ServiceConfigurationExtensions.cs` (validação de escopo das audiências que já existem); `tasks/prd-emissao-nfse/api-contract-admin.yaml` (`getServiceProvider`, `putServiceProvider`); `docs/adr/0005-sessao-e-servico-do-backoffice.md`; `docs/design/wireframes-compra.md` (adendo aprovado em 2.0); `.claude/skills/dotnet/SKILL.md`; `.claude/skills/react/SKILL.md`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/billing` | `dotnet format src/billing/CodeForCoders.Billing.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/billing` | `dotnet test --project src/billing/tests/CodeForCoders.Billing.ArchitectureTests/CodeForCoders.Billing.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/billing` | `dotnet ef migrations has-pending-model-changes --project src/billing/src/CodeForCoders.Billing.Infra.Data --startup-project src/billing/src/CodeForCoders.Billing.Api` | exit 0, sem mudança pendente | AGENTS.md |
| `src/identity` | `dotnet format src/identity/CodeForCoders.Identity.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/identity` | `dotnet test --project src/identity/tests/CodeForCoders.Identity.ArchitectureTests/CodeForCoders.Identity.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/bff-admin` | `dotnet format src/bff-admin/CodeForCoders.BffAdmin.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/bff-admin` | `dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.ArchitectureTests/CodeForCoders.BffAdmin.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/admin-spa` | `npm --prefix src/admin-spa run lint` | exit 0 | `ci-react-ts.yml` Lint |
| `src/admin-spa` | `npm --prefix src/admin-spa run typecheck` | exit 0 | `ci-react-ts.yml` Typecheck |
| `src/admin-spa` | `npm --prefix src/admin-spa run test -- finance-orders` | exit 0 (a lista de pedidos continua) | `ci-react-ts.yml` Testes |

## Pronto quando

- [ ] Gate focalizado passa (exit 0).
- [ ] Prestador sem os três não é utilizável, e a venda segue.
- [ ] Ao ficar utilizável, só os documentos `awaiting-provider` da escola entram na fila, cada um com a própria submissão.
- [ ] A chave não volta na resposta nem no log, e a segunda gravação sem chave mantém a anterior.
- [ ] Professor e suporte recebem 403 e o token de professor não chama `billing`.
- [ ] Em `/financeiro/prestador`, a tela é a do adendo, sem código nem alíquota preenchidos de fábrica.
