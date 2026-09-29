# Revisão full — Observabilidade operacional da ingestão de mídia

Run: run.dehQCWsM

**FULL VALIDATION REPROVADA** — `outcome=rejected`, `gate=failed`; há bloqueios de teste, comportamento e integração.

Base revisada: `695f3f857254def4c844c9f90255369c6f8ffc41`. Commit: `4b0b38ff39c3c8834439d3c5f2c4b5d0b38f3ec2`; árvore: `8f19fd483295a5ad1377d958c341df266ce3eb8b`. O HEAD e a árvore permaneceram estáveis; `git diff --quiet HEAD` nos caminhos revisados saiu `0`.

## Matriz de evidência

| Componente / fonte | Checks, resultado e limite |
|---|---|
| `media` — `.github/workflows/media.yml` → workflow reutilizável | `dotnet restore` 0; `dotnet format --verify-no-changes --no-restore` 0; suíte MTP Debug com cobertura: exit 2, 123/125 testes; cobertura calculada dos relatórios: 90,09% (o step de cobertura do workflow não roda após falha nos testes); `dotnet publish -c Release` 0, com aviso `NETSDK1194`. |
| Imagem — workflow reutilizável, `build-container=true` | `docker build -f src/media/Dockerfile ...` local: 0. Push GHCR e scan Trivy não foram executados localmente. |
| Kibana — sem CI dedicado | `python3 scripts/kibana/import_observabilidade.py --verify-only`: 0. É verificação offline estrutural; import e smoke visual no Kibana compartilhado não executados. |
| Compose — sem CI dedicado | Gate local com default e override OTLP: 0; `docker compose -f docker-compose.coolify.yml config --quiet` com 15 placeholders não secretos: 0. |
| Segurança — workflow reutilizável | Gitleaks 8.30.1 em `src/media`: 1 achado `generic-api-key`, SARIF `warning`, em `MediaOutboxMetricsTests.cs:23`; é falso positivo de uma routing key. O workflow mapeia `warning` para `medium` e aplica `low` em `enforce`, portanto o gate falha. Semgrep, Trivy e SBOM não foram reproduzidos localmente (ferramentas/actions ausentes); `security-mode=observe` vale para os scans observacionais, não para segredos. |

## Bloqueantes

1. **Suíte agregada reprovada.** `MediaVolumeMetricsTests.cs:77` espera três instrumentos e recebe seis após os novos gauges; `MediaQueueMetricsTests.cs:34` recebe quatro medições de `oldest_waiting` em vez de uma quando workers/listeners compartilham o `Meter` estático. O gate agregado terminou com exit 2; isso reprova a full.
2. **Contadores exibidos incorretamente.** Os painéis de funil, claims, resultados e publicações somam amostras `Counter` com `SUM` (`scripts/kibana/import_observabilidade.py:654,682,710,737,930,937,951`; saved objects em `observabilidade-midia.ndjson:6`). O próprio manifesto trata counters como cumulativos para A2; somar snapshots repete eventos e distorce as contagens nas janelas de RF-02/RF-04/RF-05. O `verify-only` reforça essa agregação em vez de detectá-la.
3. **A2 perde a primeira amostra sem baseline.** A regra usa `COALESCE(f_base, f_first, 0)` como início (`import_observabilidade.py:153-159`). Para uma nova série de motivo sem ponto anterior, quatro falhas na janela podem ser contadas como três (ou zero se chegam antes da primeira coleta), ficando abaixo do mínimo de quatro da TechSpec e deixando de acionar o alerta.
4. **Gate de segredos falha por falso positivo.** O achado em `MediaOutboxMetricsTests.cs:23` é um identificador de routing key, não uma credencial; o SARIF local reproduz o achado no escopo `src/media`. O workflow aplica o gate `low/enforce`, então é necessária uma supressão específica ou ajuste revisado da regra antes da integração.
5. **Base de integração avançou.** O estado do fluxo registra `target_ref=b1578d33e9c7823ec78d68bfad49b1de23635928`, enquanto esta chamada valida `base_ref=695f3f857254def4c844c9f90255369c6f8ffc41`; `main` contém dois commits posteriores ao base. Reprepare a branch sobre o target registrado e rode full novamente antes de integrar.

## Limites e recomendações

- Sensor de discriminação não executado porque o gate agregado falhou; o procedimento full exige que os checks obrigatórios passem antes do sensor.
- Import real no Kibana, comportamento visual e resolução de A2/A4, reachability remota da Management API e aplicação da ILM de 30 dias permanecem sem evidência neste ambiente. Não foi feita alteração em ambiente compartilhado.
- Após corrigir os bloqueantes e repreparar a base, repetir o full; a evidência atual não autoriza integração.
