# Retrospectiva — execução TSG Flow do PRD `prd-conta-aluno`

> Data: 2026-09-23 · Branch: `feature/conta-aluno` (worktree `code-for-coders-conta-aluno`) · Transporte: Herdr
> Resultado: **PRD COMPLETE** em `f587d4d` (entrega em branch, sem push/PR). Validação full aprovada na rodada 2,
> tentativa 2/3, espelhando o CI. Rodada 1 reprovada 3/3 (lint/teste SPA, link sem base path, format e cobertura).

## 1. Números

Fonte: `.tsg-flow/delegate-logs/runs.jsonl` da worktree (66 delegações).

| Kind | Chamadas | OK | Falha de transporte | Tempo total |
|---|---|---|---|---|
| codex (`gpt-6-luna`, effort max) | 40 | 32 | 8 | ~413 min |
| claude (`claude-opus-5-5`, medium/high) | 23 | 20 | 3 | ~86 min |
| opencode (`muse-spark-1.3`) | 3 | 0 | 3 | — |

Falhas de transporte: 17 de 66 chamadas (26%) — `agent_start_failed`/`agent_pane_busy` 6, `timeout_or_blocked`
(`agent_prompt_stalled`) 4, `result_missing` 2, `report_stale` 1, `result_invalid` 1, mais 3 do opencode antes da sessão.

Implementer (primeira passagem de cada task e correções):

| Task | Tentativa 1 (codex max) | Correções |
|---|---|---|
| 1.0 | OK em 48 min; revisão reprovou (`correlationId`) | claude medium OK |
| 2.0 | gate reprovado em 27 min (teste SPA sem `cleanup`) | claude medium OK |
| 3.0 | gate reprovado em 36 min (IDE0161 na migration) | claude medium falhou 2× (parou no diagnóstico); nova rodada claude high OK |
| 4.0 | gate reprovado em 54 min (erro de compilação) | claude high OK na 3/3; revisão reprovou (token em claro, DI ausente) |
| 5.0 | gate reprovado em 20 min (`StatusCodes` inexistente) | claude high OK; revisão pediu teste de contrato interno |

Validação full: 1/3 reprovada (lint + teste SPA), 2/3 aprovada **sem espelhar o CI** (invalidada), 3/3 reprovada
(`dotnet format` em 2 migrations e cobertura bff-student 51,5% < 70%).

## 2. O que funcionou

- **Revisão independente com anti-afinidade.** O validator codex pegou defeitos reais que o gate não via:
  `correlationId` ausente no contrato AsyncAPI, token de recuperação em claro no outbox, escopos de sessão sobrescritos
  no Compose, fronteira de imports da SPA e ausência de teste de contrato interno.
- **Smoke real (quando executado) pegou o que os testes com stub escondiam:** `bff-student` não subia por DI ausente
  de `ServiceAssertionTokenFactory` desde a 3.0, e os links de e-mail sem base path `/student/` davam 404.
- **Claude high como fixer:** correções focadas em 1–7 min, iterando até o gate passar e já limpando lint/fronteira
  dos arquivos tocados.
- **Estado persistido (`flow-state.json`) + reconciliação com Git** permitiu retomar após falhas de transporte e
  aceitar um checkpoint cujo envelope veio inválido (`commit: null`) sem duplicar commit.

## 3. Erros e lições

### 3.1 Detalhamento das tasks e da TechSpec

| Problema observado | Consequência | Melhoria |
|---|---|---|
| Gate de cada task roda só testes selecionados; lint, `dotnet format` e cobertura só aparecem na full. | 6 erros `react-hooks/refs`, 1 teste quebrado desde a 1.0 e 169 erros de format só descobertos no fim; 3 reaberturas. | Incluir no `gate` de toda task `vertical` o lint/format **dos serviços tocados** (`npm run lint`, `dotnet format --verify-no-changes`). São baratos e o CI os exige. |
| Testes E2E do BFF substituem os clientes do Identity por stubs. | DI ausente, escopos do Compose e clientes sem cobertura passaram por 4 tasks aprovadas. | A task deve exigir (a) teste do cliente HTTP real com `HttpMessageHandler` fake e (b) um teste de partida do host sem substituir dependências (ou `ValidateOnBuild`). |
| TechSpec contraditória: "nunca guardar token em claro" vs. link com token no pedido gravado no outbox no mesmo commit. | Bloqueio de segurança só na 4.0, exigindo decisão humana e retrabalho retroativo em 1.0/2.0. | A revisão da TechSpec deve cruzar requisitos de segurança com o fluxo de persistência (outbox, logs, backups). |
| URL pública dos links sem base path da SPA não especificada. | Links de confirmação/redefinição com 404; revisores focused não viram. | TechSpec/tasks devem declarar a URL completa derivada do base path e um critério "o link abre a tela". |
| Smoke no smtp4dev em "Pronto quando", mas sem ambiente previsto (chaves, migrations não aplicadas automaticamente, stack de outra checkout ocupando portas). | Smoke adiado de 1.0 até a 4.0; exigiu autorização para derrubar outro stack. | Task habilitadora ou pré-requisito explícito: script de smoke local com chaves efêmeras, `database update` e nome de projeto Compose isolado. |
| Threshold de cobertura do CI (70%) não considerado no plano; bff-student já estava em 36% na base. | Bloqueio na última full, parcialmente pré-existente. | O task-creator deve ler `.github/workflows/*` e registrar gates de CI herdados (cobertura, format) como critério da primeira task que toca o serviço. |

### 3.2 Skill `tsg-flow-implementer`

- **Regra de passagem única custou tentativas.** O implementer corrige, roda o gate uma vez e encerra mesmo quando o
  próximo erro é trivial. Como o gate encadeia com `&&`, cada tentativa revelou só a camada seguinte (3.0: migration →
  cookie → teste SPA). Proposta: permitir até N iterações de correção+gate dentro da mesma chamada, limitadas a erros de
  build/teste da própria task, e contar tentativa apenas quando o limite interno se esgota.
- **Implementação inicial sem verificação local barata.** Quatro das cinco primeiras passagens do codex terminaram em
  erro de compilação/analyzer após 20–54 min. Proposta: exigir `dotnet build` e lint dos arquivos tocados antes do gate,
  e rodar `dotnet format` logo após `dotnet ef migrations add` (adicionar à skill `dotnet`).
- **Relatório com nome/caminho inconsistente** (`report.md`, `implementer-report.md`, `report: null`). Padronizar.
- **Wrapper `rtk`** alterou argumentos de `dotnet test`/ESLint em várias execuções; os workers precisaram de
  `rtk proxy`. Documentar na skill que o gate roda via `rtk proxy` ou sem wrapper.

### 3.3 Skill `tsg-flow-validator`

- **Focused/revalidation não rodam lint/format do serviço tocado.** Revisores aprovaram arquivos com erros
  `react-hooks/refs` e migrations fora do format; a full reprovou depois. Incluir lint/format do serviço no focused.
- **Full precisa de checklist explícito espelhando `.github/workflows`.** A full 2/3 aprovou sem `dotnet format` nem
  cobertura .NET; só a 3/3, com instrução explícita, espelhou o CI. Tornar obrigatório: para cada serviço alterado,
  executar exatamente os passos do workflow reusável (restore, format, test+coverage, gate, publish/build).
- **Comandos em background.** O validator claude deixou mutantes e cobertura em background e ficou ocioso; o Herdr
  encerrou sem resultado (`result_missing`). Proibir background na skill ou esperar explicitamente.
- **Recomendações que deveriam ser bloqueantes.** Base path dos links e ausência de testes dos clientes reais
  apareceram como recomendação/pendência em revisões focused. Critério: se quebra uma jornada do PRD ou um gate de CI,
  é bloqueante.

### 3.4 Orquestrador (condução nesta sessão)

- **Sem canal para contexto extra.** `tsg-delegate.sh` não aceita nota; decisões do responsável (cifrar outbox,
  autorização de smoke, exigência de espelhar CI) foram anexadas aos arquivos de task e ao `prd_review.md`. Isso
  funciona, mas mistura decisão operacional com o plano. Proposta: `--context-file=<path>` no script, gravado no
  diretório do run e citado no prompt.
- **Promessa não cumprida.** Anunciei que pediria ao validator para avaliar achados extras antes de verificar que o
  script não tinha esse canal. Verificar a capacidade antes de prometer.
- **Pendências adiadas demais.** O teste do dashboard quebrado e os erros de lint foram registrados como
  `open_findings` desde a 2.0, mas só tratados após a full. Quando um achado já viola um gate de CI, tratá-lo na
  próxima correção disponível da task dona, não na full.
- **Aprovação full aceita sem conferir cobertura do CI.** Detectei a divergência com a execução interrompida e pedi
  decisão; a regra deve ser: aprovação full só é aceita se o relatório listar cada passo de CI dos serviços alterados.

### 3.5 Escolha de modelo e roteamento

| Papel | Observação | Recomendação |
|---|---|---|
| Implementer inicial (codex `gpt-6-luna`, max) | Entrega volume grande e coerente, mas lenta (20–54 min) e 4/5 vezes com erro de build no fim. | Manter para a primeira passagem **se** a skill exigir build/lint antes do gate; alternativamente testar claude high como primeira passagem e comparar pelo ledger. |
| Escalonamento (claude, medium) | Parou no diagnóstico em 2 de 3 tentativas da 3.0. | Esforço padrão `high` na rota de escalonamento do implementer (`routing.default.json`). |
| Fixer (claude, high) | 13 correções OK, 1–7 min cada. | Manter como padrão de `fix`. |
| Validator focused/revalidation (codex, max) | Achados relevantes; 5–20 min. | Manter; anti-afinidade funcionou. |
| Validator full (claude opus, medium) | Aprovou sem espelhar CI; deixou comandos em background. | Subir para `high` e reforçar checklist de CI na skill. |
| Integrator (opencode) | Nunca recebeu o prompt (`agent_prompt_stalled`). | Trocar rota padrão para codex (hoje só em override local `.tsg-flow/routing.json`). |

### 3.6 Transporte Herdr

- `agent_pane_busy` (pane ainda não é shell disponível) ocorreu 6 vezes e `agent_prompt_stalled` 4. O script já espera
  1 s após `agent start`; falta esperar o pane ficar disponível **antes** do `agent start` e repetir o prompt uma vez
  quando o status continua `idle` após 5 s.
- `result_invalid` com `commit: null` num checkpoint bem-sucedido: validar no worker (schema) antes de encerrar, ou o
  script completar `commit` a partir de `git rev-parse HEAD` quando o outcome for `checkpoint_ok`.

## 3.7 Desfecho da rodada full 2 (adendo)

- 3.0 reaberta (format das migrations + testes dos clientes HTTP reais e exclusão de `obj/**` da cobertura):
  bff-student foi de 51,5% para 83,56%. Aprovada na primeira revalidação.
- Full 1/3 da rodada 2: o validator claude ficou ocioso **de novo** com CI em background/`Monitor` (`result_missing`);
  a repetição com `--kind=codex` concluiu, mas reportou "Zero tests ran" nos três serviços .NET — **falso positivo de
  ambiente**, desmentido pelo job real do GitHub Actions em `main` (12 testes executados). Achou um bloqueio real:
  token de link no access log do Nginx e possivelmente em `url.full` da telemetria da SPA.
- 4.0 reaberta: `log_format` sem query/referer e span processor de redação antes do exporter; aprovada na primeira
  revalidação.
- Full 2/3 (codex, ~63 min): **aprovada** com espelho completo do CI (format, testes, cobertura ≥ 70% nos quatro
  serviços, Spectral/AsyncAPI, smoke Compose e sensor 5/5). `complete-prd` em `f587d4d` com entrega em branch.

Lições adicionais:
- Validator full em claude precisa proibir background explicitamente; até lá, rotear full para codex.
- Um achado de gate contraditório com evidência anterior deve ser conferido contra o CI real antes de virar retrabalho.
- Segurança de segredos em URL precisa cobrir **toda a cadeia**: persistência (outbox), proxy (access log), telemetria
  do cliente e Referer — a TechSpec citava só "logs e telemetria" do backend.

## 4. Ações priorizadas

1. **Gate das tasks inclui lint/format do serviço** (task-creator) e **focused roda lint/format** (validator).
2. **Implementer itera internamente** até o gate passar, com limite; build/lint antes do gate; `dotnet format` após migrations (skills `tsg-flow-implementer` e `dotnet`).
3. **Full espelha `.github/workflows` com checklist obrigatório** e sem comandos em background (validator).
4. **Tasks exigem teste do cliente HTTP real e partida do host sem stubs**; ler thresholds de CI no planejamento (task-creator).
5. **Roteamento:** integrator → codex; escalonamento do implementer em `high`; validator full em `high`.
6. **`tsg-delegate.sh`:** `--context-file`, espera de pane disponível e retry de prompt parado.
7. **TechSpec:** checar contradições de segurança × persistência e declarar URLs públicas completas com base path.
8. **Smoke local reprodutível** (script com chaves efêmeras, migrations e projeto Compose isolado) como pré-requisito do PRD.
