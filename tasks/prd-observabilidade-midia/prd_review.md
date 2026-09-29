# Revisão full — Observabilidade operacional da ingestão de mídia

Run: run.OFJqDMrc

**FULL VALIDATION APROVADA** — `outcome=approved`, `gate=passed`; 0 bloqueantes.

Branch: `feature/observabilidade-midia`. `base_ref=b1578d33e9c7823ec78d68bfad49b1de23635928` (= `target_ref`; branch 17 à frente, 0 atrás — G3 resolvido pelo rebase do integrator, run.OeEr7T9P).
Commit validado: `92330a4fbdcc8e5837198be1cb3c470d8218c317`; árvore: `7bfaa833c207615626ce2d3c96f51f6142c76c90`. HEAD e árvore inalterados durante a revisão; sensor executado em worktree isolado (removido após); `publish/` e `TestResults/` gerados pelos checks foram apagados. Sujeira restante no worktree é só operacional (`routing.default.json`, `runs.jsonl`, `flow-state.json`, `delegate-logs/`, `__pycache__`), fora do escopo.

## Matriz de evidência

| Componente / fonte | Comando | Resultado e limite |
|---|---|---|
| `media` — `.github/workflows/media.yml` → `template-pipeline/ci-dotnet.yml@v1` (Debug, cobertura ≥ 70%) | `dotnet restore` | exit 0 |
| | `dotnet format --verify-no-changes --no-restore` | exit 0 |
| | `dotnet test --configuration Debug --coverage` | exit 0 — **125/125** (1ª execução: 124/125 por flake do Testcontainers em `VideoTitleSearchTests`; classe verde isolada 10/10 e suíte verde na repetição — causa ambiental, sem relação com o diff) |
| Cobertura (união de linhas, mesmo método do template) | script do CI reproduzido | **90,09%** (5882/6529) ≥ 70% |
| | `dotnet publish -c Release` | exit 0 |
| Imagem | `docker build -f src/media/Dockerfile .` | exit 0. Push GHCR não executado |
| Segurança | `gitleaks detect --source src/media --no-git` | exit 0, 0 achados (supressão F1/F4 de `MediaOutboxMetricsTests.cs:23` válida). 15 achados do modo git-histórico são commits pré-base em outros componentes, fora do diff e fora do escopo do scan do `media` |
| Semgrep/Trivy/SBOM | — | indisponíveis localmente; modo `observe` no CI (não bloqueantes). Scans observacionais não reproduzidos |
| Kibana | `python3 scripts/kibana/import_observabilidade.py --verify-only` | exit 0 — 7 saved objects; A1–A5 com IDs estáveis, thresholds da TechSpec, sem conectores; painéis de counters com baseline anterior à janela + incremento por produtor (G1); `SUM()` sobre counter cumulativo reprovado pelo verificador |
| Compose | `docker compose config` + cláusulas default/override V-07 | exit 0 — default `http://otel-collector:4317` em 10 ocorrências, override `http://192.168.0.5:4317` em 10 ocorrências (G2) |

## Rastreabilidade e contratos

RF-01→1.0/2.0, RF-02→2.0, RF-03→1.0/3.0, RF-04→4.0, RF-05→5.0, RF-06→6.0, RF-07→4.0 (`video.id` em span + `VideoId` em log confirmados no diff). DP-01/DP-02 (só Kibana, sem canal externo — `actions == []` verificado), DP-03 (dimensões só em conjuntos fechados: `status/reason/stage/event/queue`; `RecordOutboxEvent` ignora routing keys desconhecidas, sem vazamento de cardinalidade), DP-04 (gauges só no snapshot do worker; counters aditivos por produtor). Mudanças de suporte (`UploadedAt`/`PreparationAttempts` lidos, `CompleteAsync → (Video?, NewlyCompleted)`, `videoId` nos spans) não alteram comportamento do pipeline (capacidade/retry/limites intactos). DLQ degrada em warning, sem derrubar o worker. Bloqueantes F1–F5 (full-1) e G1–G3 (full-2) verificados como corrigidos no diff e nos gates das revalidações 2.0/5.0/6.0/7.0.

## Sensor de discriminação

6 mutações de comportamento, uma por fatia, em worktree descartado — todas detectadas, nenhuma sobreviveu:

| # | Fatia / critério | Mutação | Suíte focalizada | Resultado |
|---|---|---|---|---|
| M1 | V-02 / sessão criada conta no funil | `UploadsCreated.Add(1)` removido | `MediaUploadFunnelMetricsTests` | 2/5 falham ✓ |
| M2 | V-03 / idade do aguardante mais antigo | `oldestWaitingSeconds` forçado a 0 | `MediaQueueMetricsTests` | 1/4 falha ✓ |
| M3 | V-04 / falha classificada por motivo | reason fixo em `attempts-exhausted` (1ª tentativa quebrou sintaxe e foi refeita — compilação quebrada não mede) | `MediaPreparationMetricsTests` | 2/5 falham ✓ |
| M4 | V-05 / publicação soma `published` | `RecordOutboxPublished` removido | `MediaOutboxMetricsTests` | 1/5 falha ✓ |
| M5 | V-06 / A2 dispara acima de 10% | threshold A2 `0.10 → 0.50` no NDJSON | `verify-only` | exit 1 ✓ |
| M6 | V-07 / default OTLP preservado | default `otel-collector:4317` → host inválido | cláusula de default do gate | 0 ocorrências, gate reprovaria ✓ |

## Bloqueantes

Nenhum.

## Recomendações / pendências operacionais (herdadas, não bloqueantes)

1. **2.0-R1:** smoke visual da seção Envio no Kibana dev com tráfego real.
2. **4.0-R1:** flake de cold-start do fixture Postgres (~60s) — observar no CI.
3. **6.0-P1** (decisão do usuário 2026-09-28): smokes A2 (lote ≥ 4 corrompidos + resolução) e A4 (parar/retomar `media-worker`) no rollout; owner equipe/plataforma.
4. **7.0-P1:** confirmação visual de que a stack local alimenta o dashboard (≤ 2 min); owner usuário/equipe.

## Integridade da revisão

Nenhum código, task, estado do fluxo ou commit alterado pelo validator. Checks em primeiro plano; exit codes registrados acima. Revisão independente sobre o HEAD final — aprovação do implementer não reutilizada como revisão semântica.
