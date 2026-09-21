# Fundação (Fase 0) do code-4-coders

> Checklist vivo da Fase 0. Marque cada caixa conforme construir.
> A Fase 0 corre **fora do fluxo TSG**, por decisão registrada em `flow-state.json` (`foundation.nota`).
> Critério de fechamento: hello-world de cada serviço da Fase 1 passando pela esteira.

**Status:** Etapa 3 concluída · **Criado em:** 2026-09-20

**Escopo desta execução:** somente a Etapa 3. A execução técnica dos itens da Etapa 0 acontece no
`template-pipeline`; este arquivo registra a ordem, os gates e as evidências necessárias. As Etapas 1 e
2 já estão registradas como entregues; esta execução fecha a documentação e a transição do fluxo.

## Context

`context/architecture-baseline.md` v1.2 aceitou microsserviços, mas registrou uma condição estrutural:
**sem esteira de deploy independente por serviço, microsserviços vira monolito distribuído.** O baseline
declara 7 dependências de plataforma (esteira por serviço com rollback, feed NuGet para `Contracts`,
RabbitMQ+DLQ, Postgres com banco/credencial por serviço, Valkey, coletor OTLP, secret manager) e as
deixa fora do próprio escopo (BA14). A Etapa 3 fechou **AB05** no baseline e **OD4** no
`flow-state.json`, registrando a fundação como pronta para o fluxo.

Hoje `flow-state.json` tem `foundation.status: "done"`, `platform_ready: true` e os 10 serviços da
Fase 1 com `path` preenchido. O repo já contém o golden path e as réplicas da fundação. O fluxo TSG
foi retomado e está em NA5 → NA6, depois da aprovação/produção dos artefatos que originalmente ocupavam
NA1 → NA3 e da aprovação de CAP-026.

`template-pipeline` é a esteira: uma IDP sobre GitHub Actions, com **Fase 1 (CI + segurança) pronta e
madura** — `ci-dotnet.yml` e `ci-react-ts.yml` cobrem exatamente as duas stacks do produto, com contrato
único verificado por script (ADR 0002), actions pinadas por SHA e self-test que testa o próprio gate.
**Fases 2 (IssueOps), 3 (CD) e 4 (catálogo) não começaram.** Zero IaC, zero publicação de pacote, zero
deploy. Ou seja: a plataforma cobre hoje a metade de CI da dependência #1 e nenhuma das outras seis.

**Resultado registrado:** `identity` saiu do zero e os outros 9 apps foram replicados a partir dele;
`foundation.platform_ready` virou `true` e AB05/OD4 foram fechadas. Tudo que a esteira precisar e não
tiver continua rastreado como issue em `template-pipeline`, com a operação descrita nos ADRs e em
`docs/foundation.md`.

**Decisões tomadas nesta sessão:** monorepo de código neste repo · golden path `identity` primeiro,
réplica depois · Coolify em VPS para compute e dados, AWS S3+CloudFront só para mídia.

---

## Etapa 0 — Destravar a esteira (`template-pipeline`)

Abrir **14 issues** — abertura concluída em 2026-09-20. Três são bloqueantes e devem ser implementadas antes da Etapa 1; depois cortar
`v1.2.0` movendo a tag `v1` (fluxo do `_release.yml`, que espera o `_selftest.yml` verde no SHA exato).

**Issues abertas em 2026-09-20:** [F0-01](https://github.com/tassosgomes/template-pipeline/issues/3),
[F0-02](https://github.com/tassosgomes/template-pipeline/issues/4),
[F0-03](https://github.com/tassosgomes/template-pipeline/issues/5),
[F0-04](https://github.com/tassosgomes/template-pipeline/issues/6),
[F0-05](https://github.com/tassosgomes/template-pipeline/issues/7),
[F0-06](https://github.com/tassosgomes/template-pipeline/issues/8),
[F0-07](https://github.com/tassosgomes/template-pipeline/issues/9),
[F0-08](https://github.com/tassosgomes/template-pipeline/issues/10),
[F0-09](https://github.com/tassosgomes/template-pipeline/issues/11),
[F0-10](https://github.com/tassosgomes/template-pipeline/issues/12),
[F0-11](https://github.com/tassosgomes/template-pipeline/issues/13),
[F0-12](https://github.com/tassosgomes/template-pipeline/issues/14),
[F0-13](https://github.com/tassosgomes/template-pipeline/issues/15) e
[F0-14](https://github.com/tassosgomes/template-pipeline/issues/16).

### Sequenciamento e gates

Os números abaixo são os números pretendidos das issues no `template-pipeline`. Ao abrir cada issue,
registrar aqui o link permanente e manter o checklist sincronizado com o estado real do issue.

| Gate | Escopo | Evidência exigida | Libera |
|---|---|---|---|
| 0.0 — registro | Abrir #1–#14 com os labels indicados | 14 issues abertas, cada uma com link registrado e descrição autocontida | Execução da Etapa 0 |
| 0.1 — CI | Implementar #1–#3 | `_selftest.yml` verde no SHA da mudança, `v1.2.0` publicada e `v1` apontando para esse SHA | Etapa 1 |
| 0.2 — plataforma | Implementar #4–#7 | Deploy de um serviço com ambientes, segredos, migration e rollback por digest comprovados | CD da Etapa 1 |
| 0.3 — contratos | Implementar #8–#10 | Pacote `Contracts` publicado e gates de lint/compatibilidade verdes | Consumo de contratos pelos serviços |
| 0.4 — monorepo e defaults | Implementar #11–#14 | Self-test cobrindo o schema/versionamento e as três correções menores, sem regressão nos defaults | Fechamento da Etapa 0 |

O gate 0.1 é a única saída antecipada permitida para a Etapa 1. Os gates 0.2–0.4 continuam sendo
parte da Etapa 0 e devem permanecer rastreáveis até o fechamento formal. A Etapa 0 só fecha quando:

- as 14 issues estiverem fechadas com links para o código, workflow ou configuração entregue;
- cada evidência da tabela acima estiver anexada ao issue correspondente;
- o contrato único (`scripts/check-contract.sh`) e o `_selftest.yml` estiverem verdes no SHA liberado;
- a tag `v1` apontar para a mesma revisão validada de `v1.2.0`.

### 0.A Bloqueantes (impedem `identity` de passar na CI)

- [ ] **F0-01 ([issue #3](https://github.com/tassosgomes/template-pipeline/issues/3)) — `ci-*.yml`: separar o contexto do build de container do `working-directory`**
      `ci-dotnet.yml:280` usa `context: ${{ inputs.working-directory }}`. Num monorepo, o Dockerfile de
      `src/identity` precisa de `Directory.Build.props`, `Directory.Packages.props`, `global.json` e
      `BannedSymbols.txt` da raiz — `dotnet restore` dentro da imagem falha sem eles. Adicionar inputs
      `docker-context` (default: o `working-directory`, preservando o comportamento atual) e `dockerfile`.
      Por ADR 0002, os dois entram nos **seis** `ci-*.yml`, e `scripts/check-contract.sh` valida.
      · label `enhancement`
- [ ] **F0-02 ([issue #4](https://github.com/tassosgomes/template-pipeline/issues/4)) — `ci-dotnet.yml`: suportar Microsoft.Testing.Platform (SDK 10)**
      O passo de testes usa `--collect:"XPlat Code Coverage" --results-directory` — sintaxe VSTest. O
      `global.json` do padrão do time fixa `"test": { "runner": "Microsoft.Testing.Platform" }`,
      obrigatório no SDK 10 (`.agents/skills/dotnet/references/testing.md:17-19`). Sob MTP a cobertura
      sai por `--coverage --coverage-output-format cobertura` e o arquivo não se chama
      `coverage.cobertura.xml`, então o passo `coverage` também não acha nada. Detectar o runner pelo
      `global.json` e ramificar. · label `bug`
- [ ] **F0-03 ([issue #5](https://github.com/tassosgomes/template-pipeline/issues/5)) — `ci-dotnet.yml`: `--configuration Release` fixo nos testes**
      `ArchitectureTests` precisa rodar em **Debug** — o ArchUnitNET lê o IL e o Release otimiza
      dependências (`.agents/skills/dotnet/references/testing.md:86-87`). O flag está hardcoded e
      `test-args` só concatena, então passar `--configuration Debug` duplica o flag e quebra. Adicionar
      input `test-configuration` (default `Release`) aos **seis** `ci-*.yml` por causa do ADR 0002;
      só o workflow .NET precisa usá-lo efetivamente. · label `bug`
- [ ] Implementar F0-01, F0-02 e F0-03, `_selftest.yml` verde, cortar `v1.2.0` e mover a tag `v1`

### 0.B Plataforma — CD (fecha a dependência #1 do baseline)

- [ ] **F0-04 ([issue #6](https://github.com/tassosgomes/template-pipeline/issues/6)) — Fase 3: `cd-coolify.yml` reusável.** Deploy por serviço a partir de `image-digest`,
      ambientes `dev`/`staging`/`prod` com reviewers, **rollback por redeploy do digest anterior**.
      Consome os outputs `image-digest`/`version` que os `ci-*.yml` já expõem esperando exatamente isso.
      Épico. · label `enhancement`
- [ ] **F0-05 ([issue #7](https://github.com/tassosgomes/template-pipeline/issues/7)) — Migration como step de deploy.** Regra 9 de Propriedade dos Dados: *"Migration é step de
      deploy, nunca no boot, e cada tabela tem exatamente um serviço que a migra."* Job `migrate` no CD
      antes do rollout (EF bundle), e action de CI envolvendo
      `.agents/skills/dotnet/assets/ci/check-migrations-immutable.sh`. · label `enhancement`
- [ ] **F0-06 ([issue #8](https://github.com/tassosgomes/template-pipeline/issues/8)) — Ambientes e segredos (dependência #7).** GitHub Environments com reviewers + integração com
      o secret manager do Coolify; nenhuma credencial em repositório ou imagem. · label `enhancement`
- [ ] **F0-07 ([issue #9](https://github.com/tassosgomes/template-pipeline/issues/9)) — Provisionamento das dependências de runtime no Coolify (#3, #4, #5, #6 do baseline).**
      Postgres com **banco e credencial por serviço** (mecanismo de G04; a permissão sem `UPDATE`/`DELETE`
      é o de G13), RabbitMQ com quorum queues + DLX/DLQ e retenção, Valkey, coletor OTLP com backend de
      traces, métricas e logs. · label `enhancement`

### 0.C Contratos (dependência #2 e guardrail G14)

- [ ] **F0-08 ([issue #10](https://github.com/tassosgomes/template-pipeline/issues/10)) — Publicar o pacote NuGet interno `Contracts`.** Workflow `publish-nuget.yml` para GitHub
      Packages (BA13: *"nunca em projeto referenciado entre solutions"*) e consumo autenticado nos
      serviços. · label `enhancement`
- [ ] **F0-09 ([issue #11](https://github.com/tassosgomes/template-pipeline/issues/11)) — G14: gate de compatibilidade de contrato.** *"Mudança de contrato é aditiva; incompatível
      exige nova versão. Mecanismo: diff de OpenAPI e do pacote `Contracts` na CI."* Action com `oasdiff`
      para o OpenAPI e comparação de API pública para o pacote. · label `enhancement`
- [ ] **F0-10 ([issue #12](https://github.com/tassosgomes/template-pipeline/issues/12)) — Lint de contrato OpenAPI (Spectral).** O ruleset já existe em
      `.agents/skills/tsg-flow-contract-creator/rulesets/openapi.yaml`; falta a action. · label `enhancement`

### 0.D Monorepo

- [ ] **F0-11 ([issue #13](https://github.com/tassosgomes/template-pipeline/issues/13)) — `platform.yml` / `platform.schema.json` para monorepo.** O schema assume 1 arquivo = 1
      serviço (`nome` escalar). Um repo com 10 apps precisa de lista. Insumo da Fase 2/4. · label `enhancement`
- [ ] **F0-12 ([issue #14](https://github.com/tassosgomes/template-pipeline/issues/14)) — Versionamento por serviço em monorepo.** `version` sai de `${GITHUB_REF#refs/tags/}`; uma
      tag `identity/v1.2.3` vira tag Docker inválida (barra). Não bloqueia a Etapa 1 (em branch o valor é
      o SHA curto e o CD é por digest), mas quebra o dia do primeiro release. · label `bug`

### 0.E Dívida menor e defaults

- [ ] **F0-13 ([issue #15](https://github.com/tassosgomes/template-pipeline/issues/15)) — Três correções pequenas.** (a) `actions/ephemeral-app/action.yml` cita
      `actions/ephemeral-app/stop`, que não existe; (b) `scripts/check-pins.sh` tem `docker://*) ;;` sem
      `continue`, então reprovaria uma referência `docker://`; (c) README/ADR 0001 afirmam que o CodeQL do
      próprio repo roda, mas não há workflow que o acione. · labels `bug`, `good first issue`
- [ ] **F0-14 ([issue #16](https://github.com/tassosgomes/template-pipeline/issues/16)) — `setup-toolchain`: default .NET 8.0.x → 10.0.x e respeitar `global.json`.**
      `actions/setup-toolchain/action.yml:37`. Não bloqueia (passamos `version: '10.0.x'` explícito), mas
      o default da plataforma está uma major atrás do padrão do time. · label `enhancement`

---

## Etapa 1 — Golden path: `identity` ponta a ponta (`code-for-coders`)

### 1.1 Raiz do monorepo

- [x] Copiar de `.agents/skills/dotnet/assets/` para a raiz: `global.json`, `Directory.Build.props`,
      `Directory.Build.targets`, `Directory.Packages.props`, `.editorconfig`, `BannedSymbols.txt`
      (`Directory.Build.props` referencia `$(MSBuildThisFileDirectory)BannedSymbols.txt`, então os dois
      ficam juntos na raiz) + `.dockerignore`
- [x] `docker-compose.yml` com Postgres, RabbitMQ (com DLX), Valkey e coletor OTLP — **com as mesmas tags
      usadas pelos Testcontainers**, conforme `.agents/skills/dotnet/references/operations.md`
- [x] `otel-collector-config.yaml`

### 1.2 `src/identity/` — solution .NET 10 em Clean Architecture

- [x] Camadas `Domain`, `Application`, `Infra.Data.EF`, `Infra.Messaging`, `Api` + `Contracts` como
      projeto próprio (futuro pacote da F0-08); `ProjectName` → `CodeForCoders.Identity`
- [x] Módulo único `IdentityAccess` com schema próprio
- [x] `tenant_id` em toda entidade + global query filter (BA10, G07)
- [x] Outbox; caso de uso nunca publica no broker (G06)
- [x] `Program.cs`: OTLP com `service.name`, `/health/live` só `self`, `/health/ready` com Postgres,
      RabbitMQ e Valkey, `ProblemDetails` com `traceId`, `AddStandardResilienceHandler`
- [x] **Hello-world que exercita mecanismo, não domínio** (o baseline proíbe decidir feature aqui):
      migration inicial criando só a tabela de outbox (infra, não domínio) + endpoint técnico que publica
      `identity.platform.heartbeat.v1` pelo outbox e o consome de volta. Prova migration + outbox +
      RabbitMQ + DLQ + OTLP ponta a ponta. **Marcar como descartável** — sai quando o PRD de CAP-001 chegar

### 1.3 Testes

- [x] `ArchitectureTests` a partir de `.agents/skills/dotnet/assets/ArchitectureTests/`
      (`LayerDependencyTest.cs`, `ConventionTest.cs`, `ProjectArchitecture.cs`) — mecanismo de G01, G02,
      G06, G07 e G11
- [x] `IntegrationTests` com Testcontainers (Postgres + RabbitMQ)
- [x] `EndToEndTests` com `WebApplicationFactory`

### 1.4 Container

- [x] `src/identity/Dockerfile` multi-stage `mcr.microsoft.com/dotnet/sdk:10.0` → `aspnet:10.0`, conforme
      `.agents/skills/dotnet/references/operations.md`. **Contexto de build é a raiz do repo** (F0-01)

### 1.5 `.github/workflows/identity.yml`

- [x] Workflow chamador com filtro de `paths:`

```yaml
on:
  push:
    branches: [main]
    paths: ['src/identity/**', 'Directory.*', 'global.json', 'BannedSymbols.txt', '.github/workflows/identity.yml']
  pull_request:
    paths: [ ...os mesmos... ]

permissions:               # workflow reusável não declara permissões de propósito;
  contents: read           # pedir mais do que o chamador concede faz o run falhar na largada
  security-events: write   # publish-findings
  packages: write          # build-container → GHCR

jobs:
  ci:
    uses: tassosgomes/template-pipeline/.github/workflows/ci-dotnet.yml@v1
    with:
      working-directory: src/identity
      docker-context: .                        # F0-01
      dockerfile: src/identity/Dockerfile      # F0-01
      version: '10.0.x'
      test-configuration: Debug                # F0-03 (ArchitectureTests lêem IL)
      coverage-threshold: 70
      build-container: true
      image-name: code-4-coders-identity
      security-mode: observe                   # calibrar antes de enforce
```

### 1.6 CD

- [ ] Deploy via `cd-coolify.yml` (F0-04): `dev` automático, `staging` e `prod` com reviewer
- [ ] **Rollback provado**, não presumido: redeploy do digest anterior e `curl` confirmando

### 1.7 Fechar a etapa

- [x] `flow-state.json` → `foundation.services[0].path = "src/identity"`

---

## Etapa 2 — Réplica (9 apps restantes)

Padrão já provado; execução mecânica, adequada a agente. Cada app ganha estrutura equivalente, workflow
chamador com `paths:` próprio, `image-name` próprio e entrada em `foundation.services[].path`.

- [x] **`commerce`** (.NET) — 3 módulos, **3 schemas** (Catalog · Sales · Entitlement); `Entitlement` é o
      1º candidato a extração (BA04)
- [x] **`learning`** (.NET) — 2 módulos, 2 schemas (Content · Progress)
- [x] **`media`** (.NET) — camada anticorrupção de CDN/storage; AWS S3 + CloudFront
- [x] **`audit`** (.NET) — append-only, só consome evento; **permissão de banco sem `UPDATE`/`DELETE`** (G13)
- [x] **`notification`** (.NET) — fatia mínima: e-mail transacional, um canal (OD1)
- [x] **`bff-student`** (.NET + YARP) — sessão **opaca** em Valkey, token nunca no browser (BA06); CSRF (G18)
- [x] **`bff-admin`** (.NET + YARP) — idem
- [x] **`student-spa`** (React/Vite/TS, `ci-react-ts.yml`) — assets da skill `react`: `Dockerfile`,
      `nginx.conf.template`, `docker/40-runtime-env.sh`; `window.RUNTIME_ENV` (imagem única); OTel Web com
      `traceparent`. `ingress.yaml` é de Kubernetes — descartar no Coolify
- [x] **`admin-spa`** (React/Vite/TS) — idem

---

## Etapa 3 — Registrar e fechar o fluxo

- [x] **[`docs/adr/0001-monorepo-de-codigo.md`](adr/0001-monorepo-de-codigo.md)** — por que um repo e não dez, e por que isso **não** viola
      BA01: serviço continua sendo unidade de deploy, escala e falha; cada um tem imagem, deploy e
      rollback próprios
- [x] **[`docs/adr/0002-plataforma-de-runtime-coolify.md`](adr/0002-plataforma-de-runtime-coolify.md)** — registra uma divergência que precisa ser
      explícita: `vision.md` §Restrições Técnicas diz *"Infraestrutura: nuvem AWS, com S3 + CloudFront"*.
      Coolify em VPS para compute e dados diverge disso
- [x] **`vision.md` → v1.2** restringindo a exigência de AWS a armazenamento e distribuição de mídia. O
      ADR sozinho não basta: o validador de fluxo compara versão e origem entre níveis
- [x] **`docs/foundation.md`** — o que a Fase 0 entregou, como subir o ambiente local, como um serviço
      novo nasce
- [x] **`flow-state.json`** — `foundation.status: "done"`, `platform_ready: true`, todos os `path`
      preenchidos; fechar `OD4` movendo-a para `decisions`; registrar as decisões novas (monorepo, Coolify)
- [x] **`context/architecture-baseline.md` → v1.2** fechando **AB05**
- [x] Retomar o fluxo TSG em NA1 → NA2 → NA3; o estado atual já avançou para NA5 → NA6

---

## Pontos a confirmar durante a execução

- [ ] **`code-for-coders` é público.** Hoje é um repo de documentação; a partir da Etapa 1 passa a
      hospedar o código do produto, incluindo o esquema de proteção de vídeo sem DRM. Vale uma decisão
      consciente antes do primeiro commit de `src/`. (Público é o que dá environments com reviewers e
      SARIF de graça no plano Free — é o argumento do ADR 0001 do `template-pipeline`.)
- [ ] **`security-mode`** entra em `observe` e migra para `enforce` depois de calibrar — recomendação do
      próprio guia de segurança da plataforma. Segredo vazado já é bloqueante sempre, por dentro

---

## Verificação

**Local**
```bash
docker compose up -d
dotnet test                                   # unit + architecture + integration (Testcontainers)
docker build -f src/identity/Dockerfile .     # contexto na raiz
```

**CI** — push numa branch tocando `src/identity/**`:
```bash
gh run watch                                  # jobs build / security / (dast)
gh run view --json jobs                        # cobertura ≥ 70, findings, image-digest
```
Esperado: `build` e `security` verdes, cobertura apurada (não "não apurada" — é o sintoma da F0-02),
imagem no GHCR com digest.

**Imagem**
```bash
docker pull ghcr.io/tassosgomes/code-4-coders-identity@<digest>
docker run --rm -p 8080:8080 ...              # contra o compose local
curl -sf localhost:8080/health/live
curl -s  localhost:8080/health/ready | jq     # Postgres, RabbitMQ, Valkey
```

**Ponta a ponta** — chamar o endpoint de heartbeat e confirmar, no backend OTLP, **um único trace**
cobrindo requisição HTTP → outbox → publicação no RabbitMQ → consumo. É isso que prova a dependência #6.

**Deploy e rollback** — deploy do digest N em `dev`, `curl` confirmando; redeploy do digest N-1, `curl`
confirmando a versão anterior. Sem isso, a dependência #1 não está atendida.

**Fluxo**
```bash
python3 .tsg/validate_state.py                # verde após editar flow-state.json
```
