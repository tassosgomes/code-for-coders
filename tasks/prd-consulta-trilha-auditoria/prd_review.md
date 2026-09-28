# Revisão full — Consulta e complemento da trilha de auditoria (CAP-030, 2º PRD)

Run: run.A5UGbt7y

- **Modo:** full · **Tentativa:** 3/3 · **Data:** 2026-09-27
- **base_ref:** `dead10bc6237cade3eb4c66699b1b89b150f5378`
- **target_ref:** `dead10bc6237cade3eb4c66699b1b89b150f5378` (flow-state; `main` local = merge-base)
- **validated_commit:** `450f45a1835a40c8758150de5c49c4c4f15b8c75`
- **validated_tree:** `4ac94bc6304e6143e31c1de2cf5779d681e59dc9`
- **Estabilidade:** HEAD e `git status --porcelain` iguais no início e no fim, com diff vazio
  contra a linha de base capturada. Checks e mutações rodaram em worktrees temporários destacados
  em `450f45a` (`wa`, `dk`, `other-path-b`, `ci`), criados no scratchpad desta sessão e removidos
  no fim (`git worktree remove --force` + `prune`).
- **Diff desde a full anterior (`413e5eb..450f45a`):**
  - task 3.0 (B3):
    - `AuditIdentityReferenceQueries` passa a usar `IgnoreQueryFilters()`, e o filtro explícito
      pelo tenant da asserção continua;
    - novo teste em Postgres, `AuditReferenceAccess_LabelsOnlyReferencesOfTheAssertedTenant`.
  - task 6.0 (B2′): `BffAdminIntegrationFixture.SettlePendingOutboxMessagesAsync` é chamado
    antes de cada teste que liga o worker de outbox.
  - O restante do código de produção é idêntico ao revisado em run.8EPhaVq4 e run.XnmIxFI0. A
    revisão semântica e a rastreabilidade daquelas rodadas continuam válidas e não foram
    rederivadas.

## Resultado

**FULL VALIDATION APROVADA** — 0 bloqueantes, 4 recomendações.

- **B2′ (run.XnmIxFI0) resolvido.** A suíte do `bff-admin` passou nos três caminhos de checkout
  testados e no caminho exato do runner do GitHub, 103/103, com exit 0. A integração isolada
  passou 18/18 em 4 de 4 execuções, em dois caminhos. Um deles se chama `dk`, o mesmo nome do
  caminho que reprovava na full anterior.
- **B3 (run.XnmIxFI0) resolvido.** A mutação M6 agora morre. As variantes M6b (filtro de
  convites) e M6c (sem `IgnoreQueryFilters`) também morrem.

### Revisão da mudança de produção (Identity)

`IgnoreQueryFilters()` desliga apenas o filtro global de tenant ambiente, que é o único filtro de
`Account` e `StaffInvitation` em `IdentityDbContext`. O isolamento continua explícito:

- contas: `TenantId == tenantId` e `Type == InternalActor`;
- convites: `TenantId == tenantId`.

O `tenantId` vem da asserção validada. O comportamento atende a TechSpec V-01/V-02: a conta
desativada é lida, sempre filtrada por tenant. O novo teste usa um tenant ambiente diferente do
tenant da asserção, então prova que o filtro que vale é o explícito.

## Matriz de CI por componente

Fonte: `.github/workflows/{admin-spa,audit,bff-admin,identity}.yml`. Esses arquivos chamam
`tassosgomes/template-pipeline/.github/workflows/ci-{react-ts,dotnet}.yml@v1`, lidos via
`git show v1:` no clone local (`v1` = `db5006a`). A cobertura foi agregada com o script Python
extraído do `ci-dotnet.yml@v1`.

Os passos .NET rodaram no container `mcr.microsoft.com/dotnet/sdk:10.0` (SDK 10.0.401), por causa
do aborto de cobertura do MTP no host (R3). A linha **CI** usou o worktree montado em
`/home/runner/work/code-for-coders/code-for-coders`, o caminho do runner do GitHub. Nenhum outro
workflow é acionado pelo diff: `docker-compose*` e `scripts/` não estão nos `paths` de nenhum
workflow.

| Componente | Passo | Comando | Exit / resultado |
|---|---|---|---|
| admin-spa | install / lint / tsc | `npm ci`; `npm run lint`; `npx --no-install tsc --noEmit` | 0 / 0 / 0 |
| admin-spa | testes + cobertura ≥ 70 | `npm test`; `coverage-summary.json` | 0, 76/76; 87,88% ✅ |
| admin-spa | build | `npm run build -- --base=/admin/` | 0 |
| audit (CI) | restore / format / testes / publish | `dotnet restore`; `dotnet format --verify-no-changes --no-restore`; `dotnet test --no-restore -c Debug --coverage --coverage-output-format cobertura`; `dotnet publish -c Release --no-restore` | 0 / 0 / **0** (91/91) / 0 |
| audit (CI) | cobertura ≥ 70 | agregado de 4 relatórios | 81,22% ✅ |
| audit (`wa`) | testes | idem | **2**: 1 falha, anterior ao PRD, que depende do caminho (ver R1) |
| audit (`dk`, `other-path-b`) | integração isolada | `dotnet test --project …IntegrationTests` | 0 ×2 em cada caminho (66/66) |
| identity (CI) | restore / format / testes / publish | idem | 0 / 0 / **0** (124/124) / 0 |
| identity (CI) | cobertura ≥ 70 | agregado de 4 relatórios | 79,73% ✅ |
| identity (`wa`) | testes | idem | 1ª execução: aborto de ambiente (container Postgres do Testcontainers saiu com código 1 no boot). 2ª execução: **0**, 79,73% |
| bff-admin (CI, `wa`, `other-path-b`, `dk`) | restore / format / testes / publish | idem | 0 / 0 / **0** (103/103) / 0 nos 4 caminhos |
| bff-admin | cobertura ≥ 70 | agregado de 4 relatórios | 82,06–82,10% ✅ |
| bff-admin (`wa`, `dk`) | integração isolada | `dotnet test --project …IntegrationTests` | 0, 18/18, em 4 de 4 execuções |
| compose | sintaxe | `docker compose config -q`, com as variáveis obrigatórias preenchidas por valores fictícios: `docker-compose.yml`, `docker-compose.coolify.yml` e `docker-compose.yml` + `docker-compose.remote.yml` | 0 / 0 / 0 |
| scripts | sintaxe | `bash -n scripts/{apps,generate-local-env,remote-infra}.sh` | 0 |

Os passos de imagem, Trivy, SBOM, SAST e secret scan não foram executados. Eles fazem build e
push para o GHCR e usam ações da plataforma, o que não é reproduzível aqui, e rodam com
`security-mode: observe` (não bloqueiam). Esta matriz não é um espelho completo do CI.

## Sensor de discriminação

As mutações foram aplicadas no worktree isolado `other-path-b` e revertidas por
`git checkout -- <arquivo>`. Depois de cada uma, `git status --porcelain` do worktree ficou vazio.
Controles sem alteração (M0, M4-ctl) confirmaram que a suíte focalizada passa sem mutante.

| # | Fatia / critério | Mutação | Suíte focalizada | Resultado |
|---|---|---|---|---|
| M0 | controle | nenhuma | `Identity…AuditReferenceAccessTests` | exit 0, 10/10 |
| M6 | V-01/V-02: rótulos filtrados por tenant | `AuditIdentityReferenceQueries.cs:30`: `account.TenantId == tenantId` passa a `!= Guid.Empty` | idem | exit 2, 1 falha, **morto** (sobrevivia em run.XnmIxFI0) |
| M6b | idem, convites | `:45`: `invitation.TenantId == tenantId` passa a `!= Guid.Empty` | idem | exit 2, 1 falha, **morto** |
| M6c | V-01/V-02: tenant da asserção, não o ambiente | `:29`: `IgnoreQueryFilters()` removido nas contas | idem | exit 2, 1 falha, **morto** |
| M4-ctl | controle | nenhuma | `BffAdmin.Integration…AuditComplementConfirmationTests` | exit 0, 11/11 |
| M4 | V-03: idempotência (conflito de texto) | `AuditComplementConfirmationStore.cs:125`: `FixedTimeEquals` passa a comparar só o tamanho | idem | exit 2, 3 falhas, **morto** |
| M8 | V-03: publicação marca a linha processada (os testes agora chamam `Settle…`) | `OutboxPublisherWorker.cs:97`: `MarkProcessedAsync` removido | `BffAdmin.Integration…AuditComplementPublicationTests` | exit 2, 4 de 6 falham, **morto** |

M1b, M2b, M3, M5 e M7 (run.XnmIxFI0) não foram repetidas. Elas cobrem código e testes de
`audit`, do E2E do `bff-admin` e do `admin-spa`, que não mudaram em `413e5eb..450f45a`, e todas
morreram naquela rodada. M8 é nova: confirma que a limpeza do outbox antes de cada teste não
esvaziou a prova de publicação.

## Recomendações (não bloqueiam)

1. **R1 — `audit`: um teste anterior ao PRD depende da ordem.**
   `AdministrativeActRecordingTests.DiscardsFieldsOutsideTheContractAndKeepsTelemetryPrivate`
   falha de forma determinística no caminho `wa`: `Assert.Equal(2, ...)`, valor real 12. Isso
   aconteceu 2 de 2 vezes em `450f45a` e **2 de 2 vezes na base `dead10b`**, no mesmo caminho.
   Nos caminhos `dk`, `other-path-b` e no do runner do GitHub, o teste passa. O CI de `audit` na
   `main` também passa.

   Causa provável: o `ActivityListener` é global, e `WaitForActActivitiesAsync` conta atividades
   `audit.acts.*` sem filtrar pelo `fatoId`. Assim, entram mensagens que outros testes deixaram na
   fila compartilhada `audit.integration.acts`. É a mesma classe de defeito do B2′.

   Não bloqueia por três motivos:
   - o defeito vem da base e não foi introduzido pela entrega;
   - o job obrigatório passa no caminho do CI;
   - não há quebra de jornada.

   Convém abrir uma correção fora deste PRD: filtrar as atividades pelo `fatoId` ou drenar e
   isolar a fila por teste.
2. **R2:** continuam abertas as recomendações das fulls anteriores:
   - a segunda checagem de papel em `SearchAsync` e `AuthorizeAsync` não tem teste que a
     distinga da primeira;
   - `RedisTimeoutException` vira 500;
   - a ordem escopo/replay em `ServiceAssertionVerifier`;
   - uma linha de outbox indecifrável ou com destino inválido bloqueia o lote em produção.
3. **R3:** a apuração local de cobertura de `bff-admin` e `identity` precisa rodar em container
   (aborto do MTP com `--coverage` no host WSL). O `dotnet test --project` no host também
   reclama de VSTest quando o `obj/` vem de um restore feito no container. Convém registrar isso
   na documentação do projeto.
4. **R4:** `SettlePendingOutboxMessagesAsync` marca como processadas **todas** as linhas
   pendentes da tabela compartilhada. Isso basta enquanto a coleção for sequencial. Se algum dia
   ela for paralelizada, será preciso isolar por teste (schema ou filtro por destino).

## Logs

Os logs ficaram no scratchpad desta sessão
(`/tmp/claude-1000/…/cd29a4f1-42fc-4a7f-afe2-171cb0caa6eb/scratchpad/logs`):

- `{wa,dk,other-path-b,ci,wa2}-<componente>-{restore,format,test,publish}.log`;
- `ctr-int-*.log` e `ctr-audit-int-*.log`: execuções repetidas das integrações;
- `base-audit-int-wa-*.log`: comparação na base;
- `spa-*.log`: admin-spa;
- `mut-*.log`: mutações.
