# Revisão full do PRD — Progresso do aluno (CAP-017)

Run: run.th6TLndb
Modo: full · Tentativa: 1/3

- base_ref: 3e0dc57894d280301799af616c4b8cb3690317f3
- validated_commit: f2cfbef3d0a4fd1dc9954a7119a824324361972a
- validated_tree: 5775d88dae7216c8f30d1630e82cd7052fb618c4
- Branch: `feature/progresso-aluno`. `git status --porcelain` e HEAD idênticos antes e depois da revisão (só `routing.default.json`, preexistente e fora do PRD, e `flow-state.json`). O sensor rodou em `git worktree` temporário, já removido.

## Resultado: FULL VALIDATION REPROVADA (3 bloqueantes, 5 recomendações)

## Bloqueantes

### B1. A suíte de integração de `learning` falha (exit 2, 244/245)

- `LearningInfrastructureTests.HeartbeatFlowsThroughOutboxRabbitMqAndConsumer` falha na suíte completa e passa isolada (`--filter-class`, exit 0). Reproduzido duas vezes (suíte inteira: 245 testes, 1 falha; só a integração: 214, 1 falha).
- Erro: `PRECONDITION_FAILED - inequivalent arg 'x-dead-letter-exchange' for queue 'learning.playback-progress': received 'learning.integration.events.dlx' but current is 'catalog-test.<id>.dlx'`.
- Causa: o inicializador de topologia agora declara sempre `learning.playback-progress` (`RabbitMqTopologyInitializer.cs:71,76`), com o DLX da configuração. As fixtures compartilham um RabbitMQ; `CatalogInitialLoadTestFixture.cs:47` isola `VideoFactsQueue` por instância mas não `PlaybackProgressQueue`. Quem sobe primeiro fixa o DLX da fila, e o outro host leva 406.
- O gate focalizado da 3.0 não pega isso porque roda só as classes novas. O job obrigatório `learning` do CI roda a suíte inteira e reprova.
- Correção esperada: dar a cada fixture/host de teste uma `RabbitMq:PlaybackProgressQueue` própria (como já se faz com `VideoFactsQueue`), sem enfraquecer o teste.

### B2. Mutante sobrevivente (V-01, `media`): a retenção desligada na configuração real não é testada

- Arquivo/linha: `src/media/src/CodeForCoders.Media.Api/appsettings.json:36` (`"RetainedRoutingKeys": []`).
- Mutação: voltar para `["midia.reproducao-avancou.v1"]`. Resultado: `ProgressFactPublicationTests`, `PlaybackProgressTests` e `RetainedOutboxFactTests` passam (24/24).
- Critério: RF-02 / task 3.0 "`media` deixa de reter o avanço: o fato passa a ser publicado ao vivo". `ProgressFactPublicationTests.cs:60` monta `OutboxOptions` à mão e `PlaybackProgressTests.cs:405` força a retenção com `UseSetting`; nenhum teste lê o `appsettings.json` entregue.
- Teste que deveria falhar: um que carregue a configuração real de `media` (ou suba a API real) e prove que o avanço é publicado ao vivo / que `Outbox:RetainedRoutingKeys` não contém a rota.

### B3. Mutante sobrevivente (V-03, `commerce`): a lista não é testada contra outro aluno da mesma escola

- Arquivo/linha: `src/commerce/src/CodeForCoders.Commerce.Infra.Data/Entitlement/StudentCourseAccessQueries.cs:11` (`.Where(grant => grant.StudentId == input.StudentId)`).
- Mutação: trocar por `.Where(grant => true)`. Resultado: `StudentCourseAccessListTests` passa (12/12).
- Critério: TechSpec, "`studentId` sempre o que veio na consulta" (fronteira de dado entre alunos; a rota entrega a lista que o aluno vê em "meus cursos"). Os testes só têm um aluno por escola e o caso "outra escola".
- Teste que deveria falhar: concessão de um segundo aluno na mesma escola/curso não pode aparecer na lista do primeiro.

## Gate e checks por componente

Fonte do CI: `.github/workflows/{learning,commerce,media,bff-student,student-spa}.yml` → `tassosgomes/template-pipeline` `ci-dotnet.yml@v1` / `ci-react-ts.yml@v1` (workflow reutilizável lido via `gh api`). Passos do `ci-dotnet`: restore, `dotnet format --verify-no-changes`, `dotnet test --configuration Debug` com cobertura (união de linhas, limite 70%), `dotnet publish -c Release`, imagem + Trivy, SBOM, SAST/segredos/dependências (observe).

| Componente | Passo | Resultado |
|---|---|---|
| learning | restore, format | exit 0, exit 0 |
| learning | `dotnet test` (Debug, cobertura) | **exit 2**: 245 testes, 244 ok, 1 falha (B1). Cobertura agregada 95,57% (≥ 70) |
| learning | publish Release | exit 0 |
| commerce | restore, format, publish | exit 0 |
| commerce | `dotnet test` | unit/arquitetura/e2e ok; integração 194 testes ok, sem falha, em duas execuções (a 1ª parou no meu `timeout` de 1200 s com 110 ok; as classes restantes rodaram na 2ª, 166 ok). Cada teste leva ~10–15 s neste ambiente. **Cobertura não medida** (não rodou o conjunto inteiro com `--coverage`) |
| media | restore, format, publish | exit 0 |
| media | `dotnet test` (cobertura) | exit 0, 232 ok; cobertura 91,69% |
| bff-student | restore, format, publish | exit 0 |
| bff-student | `dotnet test` (cobertura) | exit 0, 233 ok; cobertura 86,72% |
| student-spa | `npm run lint`, `npm run typecheck` | exit 0, exit 0 |
| student-spa | `npm run test` (vitest + cobertura) | exit 0, 24 arquivos / 157 testes; linhas 89,14% (≥ 70) |
| student-spa | `vite build --base=/student/` | exit 0 |
| Kibana | `import_observabilidade.py --verify-only` | exit 0 |
| Compose | `docker compose config -q` | `docker-compose.yml` exit 0; `docker-compose.remote.yml` é overlay (com o base: exit 0); `docker-compose.coolify.yml` exige variáveis do Coolify (`AWS_MEDIA_BUCKET` etc.), sem relação com o diff (só o escopo `course-access:read` foi acrescentado) |

Não reproduzíveis aqui (CI hospedado): build/push de imagem, Trivy, SBOM, SAST, segredos e dependências (`security-mode: observe`, não reprovam o job).

## Sensor de discriminação

21 mutações de comportamento em worktree isolado; 19 mortas, 2 sobreviventes (B2, B3). Tudo restaurado: HEAD, árvore e `git status` iguais à linha de base.

| Fatia | Mutação | Resultado |
|---|---|---|
| V-01 | L1 inverter o par (`occurred_at`,`sequence`) da última posição | morta (4 testes) |
| V-01 | L2 limite de conclusão 90% → 95% | morta (2) |
| V-01 | L3 remover a reavaliação de conclusão ao chegar a duração | morta (2) |
| V-01 | L11 gravar progresso mesmo com `event_id` repetido | morta (1) |
| V-01 | T1 fila ligada à routing key errada | morta (16) |
| V-01 | **M1 retenção de volta no `appsettings` de `media`** | **sobreviveu (B2)** |
| V-02 | L4 percentual arredondado para cima | morta (3) |
| V-02 | L5 janela de reinício 10 s → 1 s | morta (1) |
| V-02 | L6 `ended` deixa de zerar a retomada | morta (2) |
| V-02 | L10 pular a decisão de acesso | morta (2) |
| V-02 | B1 BFF sem audiência `learning` em `my-courses` | morta (1) |
| V-02 | B2 erro de Identity nas rotas novas sem 502/504 | morta (6) |
| V-02 | B3 503 de Identity vazando nas rotas novas | morta (2) |
| V-02 | S3 retomada usa `lastPositionSeconds` | morta |
| V-02 | S4 teto de espera de 2 s → 10 min | morta |
| V-03 | L7 ordem dos vigentes invertida | morta (1) |
| V-03 | S2 botão sempre "Continuar" | morta |
| V-03 | **C1 `commerce` sem filtro de aluno** | **sobreviveu (B3)** |
| V-04 | L8 503 de Matrícula vira lista vazia | morta (3) |
| V-04 | L9 `progressAvailable` sempre verdadeiro | morta (3) |
| V-04 | S1 aviso de progresso indisponível removido | morta |

A primeira tentativa de M1 rodou o projeto inteiro de `media` e teve 15 falhas de infraestrutura (conflito de nome do container Ryuk do Testcontainers). Foi descartada e repetida só com as três classes relevantes (resultado acima).

## Rastreabilidade e integração

- RF-01/02, RN-P01 a P06 (3.0), RF-03/05/06, RN-P07/P09 (4.0), RF-04, RN-P08/P10 (5.0/6.0) têm teste de integração ou de SPA correspondente; a matriz da TechSpec foi conferida contra os nomes dos testes.
- Contratos: os formatos de `learning` (`CourseProgress`, `MyCourses`) e as validações do BFF batem com os recortes do PRD. Não há helper de validação de resposta HTTP contra OpenAPI no repositório (só AsyncAPI, em `src/contract-testing`); a conferência das respostas contra os recortes é por testes de campos. A promoção para `contracts/` fica para a conclusão do PRD.
- Segurança: `learning` usa `sub` como aluno; decisão de acesso antes dos dados (L10 morta); escopo `course-access:read` só para `learning` (testes de `commerce` morrem se `media` ou o escopo de decisão for usado); logs do consumidor sem payload. A exceção é B3.
- Ordem de implantação (`commerce` → `learning` → `media` sem retenção → reenvio → BFF → SPA) coerente com a TechSpec; o reenvio único não tem código novo.

## Pendências do orquestrador (avaliadas)

- 3.0 rec1 (`VideoAvailabilityFact.Parse` sem validar `durationSeconds`): **não procede**. O `Parse` valida presença e tipo com `TryGetProperty`/`TryGetInt32` e lança `JsonException` (já tratada, vai à fila de erro) antes de chegar ao `GetProperty` final. Sem ação.
- 5.0 rec2 (`StudentCourseAccessClient.IsValid` derruba a lista se um `ended` vier sem `endedAt`): **recomendação, não bloqueante**. Nesta entrega `commerce` sempre grava `expires_at` e só existe o estado `active`, então um `ended` sem `endedAt` não é produzível. Vale tolerar quando a revogação (CAP-009) chegar.
- Ambiente (`scripts/apps.sh start --remote` com `BrokerUnreachableException`; provas no navegador de 4.0–6.0 não feitas): **limitação registrada**, não verificável por esta revisão. As provas de comportamento observável (retomada em 4:12, Continuar, migração entre seções, indisponibilidade) só têm testes automatizados; executar o cenário no navegador antes de integrar/implantar.

## Recomendações (não bloqueiam)

1. Executar os cenários no navegador de 4.0–6.0 assim que o `--remote` estiver alcançável (limitação acima).
2. Tolerar `endedAt`/`endedOn` ausentes no cliente da lista quando a revogação existir (5.0 rec2).
3. Medir a cobertura de `commerce` no conjunto completo; a integração leva ~35 min neste ambiente (testes de 10–15 s cada, preexistente, sem relação com o diff).
4. Acrescentar um helper de validação de resposta OpenAPI em `src/contract-testing` para as rotas novas assim que os recortes forem promovidos a `contracts/`.
5. Design: manter `PlaybackProgressStore`/`VideoDurationStore` como estão (duas escritas com regras diferentes, custo baixo); gatilho para revisitar: um terceiro consumidor de progresso na mesma tabela.

## Limitações declaradas

- Imagens, Trivy, SBOM, SAST e scans de segredos/dependências do CI não foram reproduzidos.
- Cobertura de `commerce` não medida; as demais ficaram acima de 70%.
- Provas no navegador não executadas (ambiente).
