---
status: pending
task_kind: vertical
blocked_by: ["1.0"]
gate: "dotnet test --project src/learning/tests/CodeForCoders.Learning.IntegrationTests/CodeForCoders.Learning.IntegrationTests.csproj -- --filter-class CodeForCoders.Learning.IntegrationTests.CourseAccessTests --minimum-expected-tests 6"
gate_expect: "6 testes de acesso e criação de curso passam; menos de 6 reprova"
---

# 2.0 Professor cria e encontra cursos da escola com autorização

**Fatia:** V-01 · **Cobre:** RF-01, RF-02, RF-05; US-01, US-08; RN-C02/03/10/14,
RN-12/16/17/18 · **Spec:** [V-01](techspec.md#v-01-professor-cria-e-encontra-cursos-da-escola)
· **ADR:** [ADR-0005](../../docs/adr/0005-sessao-e-servico-do-backoffice.md)

## Comportamento

Com o Figma aprovado, o professor encontra **Autoria** no grupo Conteúdo e no Início, abre
`/admin/autoria`, cria curso com título obrigatório e descrição pedagógica, e é levado ao editor
`/admin/autoria/{courseId}`. A lista contém todos os cursos da escola, de qualquer autor,
ordenados pela última edição e paginados, com Rascunho/Publicado, versão vigente quando houver,
último editor e momento. Criar devolve 201, `Location` e curso com `draftRevision`; repetição da
mesma intenção na janela de idempotência devolve a mesma criação. A página e o editor sobrevivem
a refresh nas URLs públicas.

Identity concede `autoria.editar` somente ao professor e emite audiência `learning`; o BFF
revalida sessão, confere `autoria.ler`/`autoria.editar`, CSRF e encaminha JWT curto de ator;
Learning valida localmente JWT, tenant e permissão. Ator só com `autoria.ler` vê lista/editor
sem controles de escrita e recebe 403 se escrever diretamente. Suporte, financeiro e
administrador sem professor não veem a área; link direto recusa. Papel revogado na sessão aberta
resulta em 401 na próxima ação. Curso de outra escola não aparece e `getCourse`/chamada interna
por ID responde 404 indistinto. Nome do criador/último editor é retrato de exibição, não
identidade ou conteúdo de log/fato.

Esta fatia estabelece o schema `content`, as migrations geradas por EF, o cliente HTTP real
do BFF e os fixtures de teste necessários à jornada. Testar o adaptador real do BFF contra
fronteira Learning controlada e a inicialização do host com registros reais; mock da interface
interna sozinho não prova a integração. Smoke no Compose com professor A/B, leitor e outro tenant.

## Fora do escopo desta task

Módulos e aulas (3.0), vídeo (4.0), publicação (5.0), histórico (6.0) e exclusão (7.0).

## Decisões fechadas

- `autoria.ler` abre a área; `autoria.editar` é a única permissão de escrita, concedida ao
  professor (DP-02). BFF e Learning autorizam independentemente (ADR-0005).
- O curso pertence ao tenant, não ao professor criador; título 1–200, descrição até 5 000
  caracteres (C-07). ID de outro tenant é 404, sem vazamento.
- Migrations são geradas com tooling EF; a ordem inicial instala Identity antes de expor o BFF.

## Modificar / Referenciar

- **modificar:** `src/identity/src/CodeForCoders.Identity.Domain/Entities/StaffRoleCatalog.cs`,
  `docker-compose.yml`, `docker-compose.coolify.yml` (permissão/audiência e endereços internos).
- **modificar:** `src/learning/src/CodeForCoders.Learning.Infra.Data/LearningDbContext.cs`,
  `src/learning/src/CodeForCoders.Learning.Api/Extensions/EndpointExtensions.cs`,
  `src/learning/src/CodeForCoders.Learning.Api/Extensions/ServiceConfigurationExtensions.cs`.
- **modificar:** `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Extensions/EndpointExtensions.cs`,
  `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Extensions/ServiceConfigurationExtensions.cs`.
- **modificar:** `src/admin-spa/src/config/paths.ts`, `src/admin-spa/src/app/router.tsx`,
  `src/admin-spa/src/features/staff-session/utils/get-staff-areas.ts`.
- **ref:** [PRD](prd.md), [contrato HTTP público](api-contract.yaml),
  [contrato interno](internal-api-contract-learning.yaml),
  [Figma aprovado em 1.0](../../docs/design/wireframes-autoria-curso.md),
  `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Security/BffSecurityMiddleware.cs`,
  `src/identity/src/CodeForCoders.Identity.Api/Security/StaffSessionTokenIssuer.cs`,
  `.agents/skills/dotnet/SKILL.md`, `.agents/skills/react/SKILL.md`.

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| Identity | `dotnet restore src/identity/CodeForCoders.Identity.slnx` | exit 0 | `.github/workflows/identity.yml` → `ci-dotnet.yml@v1` |
| Identity | `dotnet format src/identity/CodeForCoders.Identity.slnx --verify-no-changes` | exit 0 | mesmo CI, lint |
| Identity | `dotnet test --project src/identity/tests/CodeForCoders.Identity.IntegrationTests/CodeForCoders.Identity.IntegrationTests.csproj -- --filter-class CodeForCoders.Identity.IntegrationTests.CourseAuthoringPermissionTests --minimum-expected-tests 2` | exit 0, ≥ 2 testes | mesmo CI, testes |
| Identity | `dotnet publish src/identity/CodeForCoders.Identity.slnx -c Release` | exit 0 | mesmo CI, publish |
| Learning | `dotnet restore src/learning/CodeForCoders.Learning.slnx` | exit 0 | `.github/workflows/learning.yml` → `ci-dotnet.yml@v1` |
| Learning | `dotnet format src/learning/CodeForCoders.Learning.slnx --verify-no-changes` | exit 0 | mesmo CI, lint |
| Learning | `dotnet test --project src/learning/tests/CodeForCoders.Learning.IntegrationTests/CodeForCoders.Learning.IntegrationTests.csproj -- --filter-class CodeForCoders.Learning.IntegrationTests.CourseAccessTests --minimum-expected-tests 6` | exit 0, ≥ 6 testes | mesmo CI, testes; gate focalizado |
| Learning | `dotnet publish src/learning/CodeForCoders.Learning.slnx -c Release` | exit 0 | mesmo CI, publish |
| BFF | `dotnet restore src/bff-admin/CodeForCoders.BffAdmin.slnx` | exit 0 | `.github/workflows/bff-admin.yml` → `ci-dotnet.yml@v1` |
| BFF | `dotnet format src/bff-admin/CodeForCoders.BffAdmin.slnx --verify-no-changes` | exit 0 | mesmo CI, lint |
| BFF | `dotnet test --project src/bff-admin/tests/CodeForCoders.BffAdmin.IntegrationTests/CodeForCoders.BffAdmin.IntegrationTests.csproj -- --filter-class CodeForCoders.BffAdmin.IntegrationTests.CourseAuthoringAccessTests --minimum-expected-tests 4` | exit 0, ≥ 4 testes; cliente HTTP e host reais | mesmo CI, testes |
| BFF | `dotnet publish src/bff-admin/CodeForCoders.BffAdmin.slnx -c Release` | exit 0 | mesmo CI, publish |
| SPA | `npm --prefix src/admin-spa run lint` | exit 0 | `.github/workflows/admin-spa.yml` → `ci-react-ts.yml@v1` |
| SPA | `npm --prefix src/admin-spa run typecheck` | exit 0 | mesmo CI, Typecheck |
| SPA | `npm --prefix src/admin-spa test -- authoring-courses` | exit 0; arquivo selecionado cobre lista/criação/acesso | mesmo CI, Vitest |
| SPA | `npm --prefix src/admin-spa run build -- --base=/admin/` | exit 0 | mesmo CI, Build |

Cobertura agregada ≥ 70% por componente e imagens de container são aferidas na validação full/CI.

## Pronto quando

- [ ] Gate focalizado passa (exit 0) com ao menos 6 testes de Learning.
- [ ] Professor A cria e professor B do mesmo tenant encontra e abre o curso; título em branco é
  recusado com indicação do campo, e retry idempotente não duplica curso.
- [ ] Leitor vê dados sem ações de escrita; chamada direta sem permissão recebe 403; revogação
  recebe 401; curso de outro tenant recebe 404 sem título ou autor expostos.
- [ ] BFF exerce o adaptador HTTP real com Learning controlado e inicia com DI real; menu, lista,
  criação e editor abrem após refresh em `http://localhost:8081/admin/autoria` e no ID criado.
