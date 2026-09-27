---
status: pending
task_kind: vertical
blocked_by: ["3.0"]
gate: 'dotnet test --project src/audit/tests/CodeForCoders.Audit.IntegrationTests/CodeForCoders.Audit.IntegrationTests.csproj -- --filter-class CodeForCoders.Audit.IntegrationTests.AuditRecordDetailTests --minimum-expected-tests 7 && dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.EndToEndTests/CodeForCoders.BffAdmin.EndToEndTests.csproj -- --filter-class CodeForCoders.BffAdmin.EndToEndTests.AuditRecordDetailTests --minimum-expected-tests 6 && npm --prefix src/admin-spa run test -- audit-record-detail'
gate_expect: "Pelo menos 19 testes passam: 7 Audit, 6 BFF, 6 SPA"
---

# 4.0 Administrador examina o fato recebido, suas faltas e referências, e volta à lista filtrada pela pessoa

**Fatia:** V-02 · **Cobre:** RF-01, RF-03, RF-05 (sequência vazia e ordenação na leitura), US-02, US-04, RN-A01, RN-A04 a RN-A06, RN-A08 a RN-A11, RN-A13, Identidade RN-19, RN-20, RN-23, RN-24 · **Spec:** `techspec.md#v-02-administrador-examina-o-fato-recebido-e-suas-referências` · **ADR:** [ADR-0005](../../docs/adr/0005-sessao-e-servico-do-backoffice.md)

## Comportamento

- **Audit — `getAuditRecordInternal`:** com a mesma validação local de JWT de 3.0, devolve o
  original do tenant do token com origem, tipo, `practicedAt` e `receivedAt` separados, autor, alvo,
  motivo e atributos **como recebidos** (ausente continua `null`, nunca deduzido), `compliant`,
  cada razão de não conformidade e a sequência de complementos ordenada por `confirmedAt` e ID
  (vazia nesta task, sem entrada fantasma). ID inexistente, de outro tenant ou de um complemento →
  404 `AUDIT_RECORD_NOT_FOUND` indistinto. A leitura não altera nenhuma linha.
- **BFF — `getAuditRecord`:** revalida sessão e papel a cada chamada (mesmas respostas 401/403 de
  3.0, sem chamar `audit` quando recusa), pede JWT `audit`, repassa o 404 neutro e resolve em
  Identity as referências distintas do original e dos complementos com a operação de 3.0. Conta
  desativada recebe rótulo; convite inexistente ou Identity em falha transitória ficam sem `label`
  com a referência intacta; 401/403 de Identity fecha a resposta. Nenhum rótulo é gravado.
- **SPA:** linha da lista abre `/admin/auditoria/{recordId}` (clique ou Enter); o reload direto no
  detalhe funciona sob o base path. Detalhe conforme A2 do Figma aprovado: Card "Recebido da origem"
  (ORIGINAL · NÃO EDITÁVEL) com "Momento do ato" e "Recebido pela Auditoria" com segundos, origem
  traduzida (G23), atributos (`papel` como `RoleBadge`), Alert com as faltas no não conforme
  (G9), "— ausente" para obrigatório ausente, "Não se aplica a este tipo" para motivo de convite
  aceito, sem aviso (G15), tipo desconhecido em `mono` (A2.f), "Nome não disponível" + referência
  curta com copiar (G16). A área "Complementos" não aparece sem complemento. 404 → A2.e "Registro não
  encontrado" com volta à trilha; carregando (A2.g); erro (A2.h); 403 → B12; 401 → B1.
  *Ver atos desta pessoa* abre a lista filtrada por autor ou alvo com `FilterChip` removível
  (A1.b), com o UUID só em estado de navegação em memória, nunca na URL; recarregar essa lista
  reinicia sem o filtro e mostra A1.f. Logout limpa o cache de detalhes e rótulos.

## Fora do escopo desta task

Formulário e exibição de complementos com conteúdo (5.0 e 6.0). A ordenação dos complementos é
provada aqui com registros semeados diretamente no banco de teste de `audit`.

## Decisões fechadas

- Falha transitória de rótulos degrada; falha de autorização fecha: TechSpec, Decisões Técnicas 3.
- 404 idêntico para inexistente, outro tenant e ID de complemento: `api-contract.yaml`.
- Mensagem única "Nome não disponível", pois o contrato não distingue a causa (decisão 8 do wireframe).

## Modificar / Referenciar

- **modificar:** `src/audit/src/CodeForCoders.Audit.Api/Extensions/EndpointExtensions.cs`, `src/audit/src/CodeForCoders.Audit.Domain/Entities/AuditRecord.cs`, `src/audit/src/CodeForCoders.Audit.Infra.Data/Configuration/AuditRecordConfiguration.cs`, `src/audit/src/CodeForCoders.Audit.Infra.Data/AuditDbContext.cs` (tipo de registro complementar com FK e tenant, se ainda não existir; migration via `dotnet ef migrations add`)
- **modificar:** `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Extensions/EndpointExtensions.cs`
- **modificar:** `src/admin-spa/src/config/paths.ts`, `src/admin-spa/src/app/router.tsx`, `src/admin-spa/src/testing/handlers.ts`
- **ref:** `api-contract.yaml` (`getAuditRecord`), `internal-api-contract-audit.yaml` (`getAuditRecordInternal`); `techspec.md#backend` (Rótulos); `tasks/prd-trilha-auditoria/asyncapi-contract.yaml` (tipos de ato e atributos); `src/audit/src/CodeForCoders.Audit.Infra.Data/Configuration/AuditDatabasePermissions.sql`; `docs/design/wireframes-auditoria.md` e Figma aprovado; skills `dotnet` e `react`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/audit` | `dotnet format src/audit/CodeForCoders.Audit.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/audit` | `dotnet build src/audit/CodeForCoders.Audit.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/audit` | `dotnet test --project src/audit/tests/CodeForCoders.Audit.ArchitectureTests/CodeForCoders.Audit.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/bff-admin` | `dotnet format src/bff-admin/CodeForCoders.BffAdmin.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/bff-admin` | `dotnet build src/bff-admin/CodeForCoders.BffAdmin.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/bff-admin` | `dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.ArchitectureTests/CodeForCoders.BffAdmin.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/admin-spa` | `npm --prefix src/admin-spa run lint` | exit 0 | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run typecheck` | exit 0 | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run build` | exit 0 | `ci-react-ts.yml` |

Cobertura agregada ≥ 70%, `dotnet publish` e imagens ficam para a validação full.

## Pronto quando

- [ ] Gate passa (exit 0) com pelo menos 19 testes.
- [ ] Registro conforme de `papel-concedido` mostra autor, conta alvo, papel, motivo e os dois momentos; não conforme sem autor/motivo mostra as faltas sem valor inventado; convite aceito sem motivo não gera aviso.
- [ ] ID de outro tenant e ID de complemento produzem o mesmo 404 de um ID inexistente; o original fica idêntico após a leitura.
- [ ] Smoke no Compose: da lista em `http://localhost:8081/admin/auditoria`, o administrador abre um detalhe, recarrega a página no detalhe, usa *Ver atos desta pessoa* e volta à lista filtrada; a URL nunca contém UUID de pessoa, motivo ou filtro.
