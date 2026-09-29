# Revisão full — Observabilidade operacional da ingestão de mídia

Run: run.KxoQ3hg1

**FULL VALIDATION REPROVADA** — `outcome=rejected`, `gate=failed`; 3 bloqueantes.

Branch: `feature/observabilidade-midia`. `base_ref=695f3f857254def4c844c9f90255369c6f8ffc41`; `target_ref=b1578d33e9c7823ec78d68bfad49b1de23635928`.
Commit validado: `37ca9d1f40f6dbb2050de8f03a74d728ce316355`; árvore: `b8ab809a5eba269159e1183dc470e1b33ba99e64`. HEAD e status da árvore principal ficaram estáveis; sensor isolado removido.

## Matriz de evidência

| Componente / fonte | Resultado e limite |
|---|---|
| `media` — [workflow](../../.github/workflows/media.yml), [template CI](https://raw.githubusercontent.com/tassosgomes/template-pipeline/v1/.github/workflows/ci-dotnet.yml) | restore 0; format 0; Debug MTP 125/125; cobertura 90,34% (mín. 70%); publish Release 0 (aviso `NETSDK1194`). |
| Imagem | `docker build -f src/media/Dockerfile`: 0. Push GHCR não executado. |
| Segurança | Gitleaks 8.30.1: 0, sem achados; Semgrep, Trivy e SBOM indisponíveis localmente. Scans observacionais do CI não reproduzidos. |
| Kibana | `python3 scripts/kibana/import_observabilidade.py --verify-only`: 0, incluindo cenários A2; validação offline. Import/smoke no Kibana não executados. |
| Compose | Config local, config Coolify com placeholders e gate V-07 atual: 0. |

## Bloqueantes

1. **Contadores dos painéis subcontam eventos na janela.** `scripts/kibana/import_observabilidade.py:134-147` calcula `LAST − FIRST` somente dentro do filtro temporal: um único snapshot após eventos retorna zero e eventos entre o início da janela e o primeiro snapshot somem. Isso afeta os painéis RF-02/RF-04/RF-05. O painel de outbox também agrega os produtores `api` e `worker` só por `event`, sem calcular incrementos por produtor. Os counters OTLP são cumulativos ([docs OpenTelemetry .NET](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/docs/metrics/README.md)); o verificador estrutural aceita essa forma.
2. **Mutante V-07 sobreviveu ao sensor.** Trocar o default `http://otel-collector:4317` por um host inválido ainda deixa o gate sair `0`: ele valida o Compose e o override explícito, mas não o default exigido por `7_task.md:5-6`. O full exige corrigir a discriminação desta fatia.
3. **Base de integração desatualizada.** `HEAD...main` = `13/2`; a branch está dois commits atrás da `target_ref`. O full foi chamado com `base_ref` anterior ao target registrado; reprepare sobre `b1578d33e9c7823ec78d68bfad49b1de23635928` e rode full novamente.

Sensor: mutações V-01 a V-06 foram detectadas pelos gates focados; V-07 sobreviveu. Suite agregada, build, formato e verificações estáticas passaram; a aprovação é impedida pelos bloqueantes acima.
