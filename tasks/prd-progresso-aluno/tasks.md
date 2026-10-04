---
tsg_artifact: tasks
product: code-4-coders
capability: CAP-017
version: 1.0
status: approved
updated: 2026-10-04
sources: tasks/prd-progresso-aluno/prd.md@1.0, tasks/prd-progresso-aluno/techspec.md@1.0
---

# Plano de Implementação — Progresso do aluno (CAP-017, 1º PRD)

> **TechSpec de origem:** [techspec.md](techspec.md) 1.0, aprovada em 2026-10-04
> **Escopo:** Full-stack (`learning` módulo Progress, `commerce` Matrícula, `media` só configuração, `bff-student`, `student-spa`, painel no Kibana)
> **ADRs pertinentes:** [0003](../../docs/adr/0003-verificacao-de-sessao-do-aluno.md), [0008](../../docs/adr/0008-observabilidade-kibana-como-codigo.md), [0013](../../docs/adr/0013-jwt-de-aluno-validado-por-servicos-de-dominio.md), [0015](../../docs/adr/0015-escopos-de-servico-media-learning-em-commerce-por-rota.md)
> **Contratos:** [contracts.md](contracts.md) 1.0 — OpenAPI do [BFF do aluno](api-contract.yaml) 1.1.0, interno de [`learning`](internal-api-contract-learning.yaml) 1.3.0 e de [`commerce`](internal-api-contract-commerce.yaml) 1.3.0; AsyncAPI de [`learning`](asyncapi-contract.yaml) 1.2.0 e de [`media`](asyncapi-contract-media.yaml) 1.3.0
> **Design:** novo documento `docs/design/wireframes-progresso.md` (a produzir em 1.0 e 2.0)
> **Status do plano:** Confirmado para implementação (aprovado pelo responsável em 2026-10-04)

## Visão Geral

Ao final, cada avanço que a reprodução emite (inclusive os retidos desde `CAP-007`) vira progresso por aula em `learning`. O aluno
abre a plataforma e cai em "meus cursos": cursos vigentes com percentual e Continuar, que o leva à aula certa na posição em que
parou, e cursos com acesso encerrado com a data. Na tela da aula, a lista marca as concluídas e mostra o percentual, e o vídeo
retoma de onde ele parou. Nada falha de forma enganosa: Matrícula fora do ar não vira "você não tem cursos", e progresso fora do ar
não impede o vídeo.

O design (ASCII → Figma → aprovação) vem antes de qualquer código de tela. O consumo do avanço (3.0) não tem tela e corre em paralelo
ao design.

## Fases

### Fase 0 — Design aprovado e progresso registrado

1.0 wireframes ASCII e 2.0 Figma; em paralelo, 3.0 o avanço vira progresso. Checkpoint: cabeçalho de `docs/design/wireframes-progresso.md`
com `ASCII e Figma aprovados`; em desenvolvimento, os avanços retidos de `CAP-007` registrados uma vez em `learning`.

### Fase 1 — Retomar a aula

4.0. Checkpoint: em `http://localhost:8082/student/aulas/{lessonId}`, assistir até 4:12, fechar o navegador e voltar → começa em 4:12.

### Fase 2 — Meus cursos

5.0 vigentes e Continuar; 6.0 encerrados e falhas. Checkpoint: em `http://localhost:8082/student/`, o aluno com cortesia vê os cursos,
continua de onde parou; o curso vencido aparece em "Acesso encerrado"; `commerce` parado mostra indisponibilidade, não o vazio.

## Mapa de Entrega

| Fatia | Task | Comportamento observável | Gate | Bloqueado por |
|---|---|---|---|---|
| EN-01 (ASCII) | 1.0 | Wireframes ASCII de "meus cursos" e da retomada aprovados | `rg` do status em `wireframes-progresso.md` | Nenhum |
| EN-01 (Figma) | 2.0 | Figma aprovado e registrado | `rg` de `ASCII e Figma aprovados` no status | 1.0 |
| V-01 | 3.0 | O avanço vira progresso por aula, sem perder o retido; painel de atraso | Learning `PlaybackProgressConsumerTests` + `VideoDurationProjectionTests` + Media `ProgressFactPublicationTests` + `import_observabilidade.py --verify-only` | Nenhum |
| V-02 | 4.0 | O aluno retoma a aula de onde parou e vê concluídas e percentual | Learning `CourseProgressReadTests` + BFF `CourseProgressProxyTests` + SPA `lesson-resume-progress` | 2.0, 3.0 |
| V-03 | 5.0 | "Meus cursos" com os vigentes e Continuar | Commerce `StudentCourseAccessListTests` + Learning `StudentCourseAccessClientTests` + `StudentCoursesListingTests` + BFF `MyCoursesProxyTests` + SPA `my-courses-active` | 4.0 |
| V-04 | 6.0 | Encerrados, indisponibilidade e progresso indisponível | Learning `StudentCoursesDegradationTests` + SPA `my-courses-degraded` | 5.0 |

EN-01 vira duas tasks (ASCII e Figma) porque cada aprovação é uma decisão separada do responsável e o Figma parte do ASCII aprovado.
As quatro fatias seguem a TechSpec; a fatia V-NN está na task (NN + 2).0.

### Habilitadores

| Enabler | Task | Por que não cabe numa fatia | Desbloqueia |
|---|---|---|---|
| EN-01 (ASCII) | 1.0 | O desenho é decidido pelo responsável antes do código e vale para todas as fatias com tela | V-02, V-03, V-04 (tela) |
| EN-01 (Figma) | 2.0 | A aprovação do Figma é decisão do responsável e vale para todos os estados de uma vez | V-02, V-03, V-04 (tela) |

Não há habilitador técnico: o schema `progress` nasce em 3.0, que é a primeira a usá-lo (migration pela ferramenta do EF); a rota e o
escopo de `commerce` e o escopo como parâmetro da asserção de `learning` nascem em 5.0; a audiência das rotas novas no BFF nasce com
cada rota (4.0 e 5.0).

## Tasks

- [x] 1.0 Wireframes ASCII de "meus cursos" e da retomada na aula aprovados
- [x] 2.0 Figma de "meus cursos" e da retomada na aula aprovado
- [x] 3.0 O avanço da reprodução vira progresso por aula, sem perder o que foi retido
- [x] 4.0 Ao abrir a aula, o aluno retoma de onde parou e vê as concluídas e o percentual
- [ ] 5.0 O aluno vê os cursos vigentes em "meus cursos" e continua de onde parou
- [x] 6.0 "Meus cursos" mostra o acesso encerrado e não mente quando algo falha

## Caminho crítico e lanes

`1.0 → 2.0 → 4.0 → 5.0 → 6.0`, com `3.0` em paralelo ao design (não toca tela nem os arquivos de 1.0/2.0) e pré-requisito de 4.0.
5.0 depende de 4.0 por compartilhar `EndpointExtensions.cs` de `learning` e do BFF, `BffSecurityOptions.cs` e o middleware do BFF, e
porque o seu checkpoint usa a retomada de 4.0. 6.0 altera a mesma tela de 5.0. Fora de 3.0, **não há lane paralela segura**.

**Estado intermediário conhecido:** depois de 3.0 e antes de 4.0, o progresso é registrado e ainda não aparece ao aluno; entre 5.0 e 6.0,
o Início mostra os vigentes e ainda não desenha os encerrados nem as falhas (o serviço já responde certo). O plano é integrado numa única
entrega. A ordem de implantação é a de `contracts.md` e da TechSpec: `commerce` → `learning` (fila ligada) → `media` sem retenção e
reenvio único → `bff-student` → `student-spa`. Sem dado legado (até o MVP).

## Integridade dos gates

- .NET: `dotnet test --project <csproj> -- --filter-class <classe> --minimum-expected-tests N` (Microsoft.Testing.Platform: exit 8 se
  nada rodar, 9 se rodar menos que N). As classes nomeadas nos gates são criadas na própria task; nenhuma coincide com classe
  existente (`CourseAccessTests`, `AccessDecisionClientTests`, `AccessDecisionIssuersTests` e `StudentAccessGrantsTests` são preexistentes e
  só aparecem em Verificações do projeto).
- SPA: `npm --prefix src/student-spa run test -- <fragmento de caminho>`. O filtro de caminho sai com 1 quando nenhum arquivo casa; não
  usar `-t`. Fragmentos: `lesson-resume-progress` (4.0), `my-courses-active` (5.0), `my-courses-degraded` (6.0); nenhum é prefixo de outro
  nem casa arquivo existente (`dashboard-screen.test.tsx` é atualizado em 5.0 e roda na suíte inteira).
- Kibana (3.0): `python3 scripts/kibana/import_observabilidade.py --verify-only`, sem rede. O NDJSON é gerado pelo script
  (`--generate-only`), nunca editado à mão.
- Design (1.0, 2.0): verificação estática da linha `> **Status:**` do cabeçalho de `docs/design/wireframes-progresso.md`.
- Testcontainers (PostgreSQL, RabbitMQ) rodam **em sequência**; os comandos do `gate` são encadeados e não paralelos.

## Artefatos compartilhados

| Artefato | Produzido em | Evolui em |
|---|---|---|
| `docs/design/wireframes-progresso.md` (ASCII + frames Figma) | 1.0, 2.0 | referência de 4.0 a 6.0 |
| Schema `progress` (avanço bruto, progresso por aula, duração) e a fila `learning.playback-progress` | 3.0 | lido em 4.0 a 6.0 |
| `Outbox:RetainedRoutingKeys` de `media` sem o avanço | 3.0 | — |
| Rota de progresso em `learning`, BFF e tela da aula | 4.0 | — |
| Audiência por rota no BFF (`/api/v1/courses`, `/api/v1/my-courses`) e o mapeamento 502/504 | 4.0 | 5.0 |
| Rota `course-access`, escopo `course-access:read` e o escopo como parâmetro da asserção de `learning` | 5.0 | — |
| Início "meus cursos" | 5.0 | 6.0 |

## Verificação herdada

| Componente | Fonte | Checks e limites | Estado da base | Resolução planejada |
|---|---|---|---|---|
| `src/learning` | `.github/workflows/learning.yml` → `tassosgomes/template-pipeline/.github/workflows/ci-dotnet.yml@v1` | restore; `dotnet format --verify-no-changes`; `dotnet test` (MTP) com cobertura ≥ 70%; `dotnet publish -c Release`; imagem | Não medido nesta sessão; `CAP-007` integrada em `main` (PR #152) com a full aprovada | Nenhuma falha herdada comprovada. Cobertura agregada medida na validação full |
| `src/commerce` | `.github/workflows/commerce.yml` → `ci-dotnet.yml@v1` | Idem, cobertura ≥ 70% | Não medido | Nenhuma |
| `src/media` | `.github/workflows/media.yml` → `ci-dotnet.yml@v1` | Idem, cobertura ≥ 70% | Não medido | Testes de avanço e retenção de `CAP-007` precisam configurar a retenção explicitamente (3.0) |
| `src/bff-student` | `.github/workflows/bff-student.yml` → `ci-dotnet.yml@v1` | Idem, cobertura ≥ 70% | Não medido | Nenhuma |
| `src/student-spa` | `.github/workflows/student-spa.yml` → `ci-react-ts.yml@v1` (`build-args: --base=/student/`, cobertura 70) | lint; typecheck; `vitest run --coverage` ≥ 70%; `vite build --base=/student/`; imagem | Não medido | Nenhuma |
| Compose | `docker-compose.yml`, `docker-compose.remote.yml`, `docker-compose.coolify.yml`; AGENTS.md | `docker compose config -q` nos três arranjos | Não medido | Só o escopo novo de `learning` (5.0); nenhum hostname novo |
| Kibana | ADR-0008; `scripts/kibana/import_observabilidade.py` | `--verify-only` (sem rede); `--import` no Kibana de desenvolvimento | Painel de mídia entregue em `CAP-006`/`CAP-007` | Nenhuma |

## Cobertura

| Requisito | Task(s) |
|---|---|
| RF-01 | 3.0 |
| RF-02 | 3.0 |
| RF-03 | 4.0 |
| RF-04 | 1.0, 2.0, 5.0, 6.0 |
| RF-05 | 1.0, 2.0, 4.0, 5.0 |
| RF-06 | 1.0, 2.0, 4.0 |
| RN-P01 | 3.0 |
| RN-P02 | 3.0 |
| RN-P03 | 3.0 |
| RN-P04 | 3.0 |
| RN-P05 | 3.0 |
| RN-P06 | 3.0 |
| RN-P07 | 4.0, 5.0 |
| RN-P08 | 5.0 |
| RN-P09 | 4.0 |
| RN-P10 | 5.0 |
| US-01 | 5.0 |
| US-02 | 4.0 |
| US-03 | 5.0 |
| US-04 | 4.0, 5.0 |
| US-05 | 6.0 |
| US-06 | 3.0, 4.0 |
| US-07 | 3.0 |

Mapeamento das histórias do PRD, na ordem em que aparecem em "Histórias de Usuário": US-01 ver os cursos num só lugar; US-02 a aula
volta do ponto em que parou; US-03 botão Continuar; US-04 ver concluídas e quanto falta; US-05 acesso terminou, ver como encerrado; US-06
dois aparelhos, retomar do último; US-07 escola: o progresso preserva sessão, aula e instante para `CAP-029`.

Sem task própria, por decisão: **observabilidade** está em 3.0 (consumo e atraso), na mesma task do comportamento que mede; **segurança**
(escopo novo, aluno = `sub`, sem dado pessoal) está nos testes de 3.0, 4.0 e 5.0; **migração de dados** não existe (sem dado legado até o
MVP; os retidos chegam pelo reenvio de 3.0).

> Verificado por `python3 <skill-dir>/scripts/validate_plan.py tasks/prd-progresso-aluno/` antes do handoff.
