---
status: done
task_kind: vertical
blocked_by: ["1.0"]
gate: "dotnet test --project src/audit/tests/CodeForCoders.Audit.UnitTests/CodeForCoders.Audit.UnitTests.csproj -- --filter-class CodeForCoders.Audit.UnitTests.AdministrativeActConformityTests --minimum-expected-tests 9 && dotnet test --project src/audit/tests/CodeForCoders.Audit.IntegrationTests/CodeForCoders.Audit.IntegrationTests.csproj -- --filter-class CodeForCoders.Audit.IntegrationTests.NonConformingActRecordingTests --minimum-expected-tests 4"
gate_expect: "Pelo menos 13 testes passam: 9 unitários da política de conformidade e 4 de integração"
---

# 3.0 Registrar e alertar ato não conforme

**Fatia:** V-03 · **Cobre:** RF-03, US-03, RN-A05, RN-A06, RN-19, DP-03, DP-04 · **Spec:** `techspec.md#v-03` · **ADR:** —

## Comportamento

Uma mensagem **legível** (JSON com `fatoId`, `origem` e `tenantId` válidos) com faltas **não é
recusada**: vira registro `non_conforming` com todas as razões, é confirmada ao broker e gera **um**
alerta por ato. As razões são os códigos estáveis da tabela em `techspec.md#v-03`:
`tipo-desconhecido`, `autor-ausente`, `alvo-ausente`, `motivo-ausente`, `momento-ausente`,
`complemento-invalido`, `motivo-excede-limite`.

- O que foi recebido é preservado nos campos correspondentes; o que faltou fica nulo. Tipo
  desconhecido preserva o texto recebido. `momento-ausente` grava `praticado_em` nulo.
  `complemento-invalido` não grava o complemento. `motivo-excede-limite` grava o motivo inteiro.
- Alerta = um incremento de `audit.acts.nonconforming` (atributos `origin`, `type`, sem razões) e um
  log de aviso com `fatoId`, `origem`, `tipo` e razões — nunca motivo nem complemento.
- Reentrega do mesmo ato não conforme segue a idempotência de 1.0 (unicidade): um registro, nenhum
  alerta repetido.

Casos de integração mínimos: `papel-revogado` sem motivo; ato sem autor e sem alvo (duas razões, um
alerta); tipo `papel-alterado` (desconhecido nesta versão); reentrega de não conforme sem alerta novo.
Os unitários cobrem cada código, a combinação de várias razões e `convite-interno-aceito` sem motivo
como conforme.

## Fora do escopo desta task

Mensagem sem `fatoId`/`origem`/`tenantId` válido é ilegível e pertence a 4.0. Reentrega divergente é 2.0.

## Decisões fechadas

- Registrar e marcar em vez de recusar: OD20 / DP-03; tipo desconhecido é não conforme: DP-04.
- Motivo exigido por tipo: tabela de RF-01 no [PRD](prd.md) e RN-19 de Identidade.
- Regra de conformidade no Domain, numa política única de tipos: techspec.md, Bloco Backend.

## Modificar / Referenciar

- **modificar:** `src/audit/src/CodeForCoders.Audit.Domain/Entities/AuditRecord.cs` (conformidade e razões na criação)
- **modificar:** `src/audit/src/CodeForCoders.Audit.Application/UseCases/Audit/RecordConsumedAuditEvent/*` (mapeamento tolerante do ato legível)
- **modificar:** `src/audit/src/CodeForCoders.Audit.Application/Common/AuditTelemetry.cs` (`audit.acts.nonconforming`)
- **modificar:** `src/audit/tests/CodeForCoders.Audit.UnitTests/*`, `src/audit/tests/CodeForCoders.Audit.IntegrationTests/*`
- **ref:** `techspec.md#v-03`, `contracts.md` cenários 2–4, skill `dotnet`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/audit` | `dotnet format src/audit/CodeForCoders.Audit.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` passo Lint |
| `src/audit` | `dotnet build src/audit/CodeForCoders.Audit.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` (restore/build implícito no test) |
| `src/audit` | `dotnet test --project src/audit/tests/CodeForCoders.Audit.ArchitectureTests/CodeForCoders.Audit.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` passo Testes (suíte do componente) |
| `src/audit` | `dotnet test --project src/audit/tests/CodeForCoders.Audit.UnitTests/CodeForCoders.Audit.UnitTests.csproj` | exit 0 | `ci-dotnet.yml` passo Testes |

Cobertura agregada ≥ 70% e `dotnet publish` ficam para a validação full.

## Pronto quando

- [ ] Gate passa (exit 0) com pelo menos 13 testes selecionados.
- [ ] Cada código de razão aparece no registro quando, e só quando, sua condição ocorre.
- [ ] Ato sem autor e sem alvo gera um registro com duas razões e exatamente um alerta; sua reentrega não gera outro.
- [ ] Nenhum ato legível incompleto é descartado nem vai para a DLQ.
