---
status: done
task_kind: vertical
blocked_by: ["2.0", "3.0", "4.0", "5.0"]
gate: "python3 scripts/kibana/import_observabilidade.py --verify-only"
gate_expect: "Verificação estrutural passa (exit 0): 5 regras A1–A5 presentes com IDs estáveis, cada uma referencia só instrumentos da tabela da TechSpec, thresholds iguais aos da spec, nenhuma com conector de notificação"
---

# 6.0 Alertas A1–A5 no Kibana, versionados e resolvendo sozinhos

**Fatia:** V-06 · **Cobre:** RF-06, US-04, DP-02 · **Spec:** `techspec.md#v-06` (tabela A1–A5) ·
**ADR:** [ADR-0008](../../../docs/adr/0008-observabilidade-kibana-como-codigo.md)

## Comportamento

Cinco regras de threshold vivem no Kibana como saved objects versionados em `scripts/kibana/`,
avaliadas a cada 1 min sobre `metrics-generic*`, janela 15 min, **sem conector de notificação**
(DP-02: o alcance desta fatia é a tela de Alertas do Kibana):

| # | Alerta | Condição |
|---|---|---|
| A1 | Vídeo preso | `media.videos.stuck > 0` |
| A2 | Taxa de falha de preparação | `failed/(completed+failed) > 0,10` **e** ≥ 4 preparações finalizadas na janela |
| A3 | Outbox esgotado/atrasado | `media.outbox.exhausted > 0` **ou** `media.outbox.oldest_pending > 600 s` |
| A4 | Fila parada | `media.videos.oldest_waiting > 1800 s` |
| A5 | DLQ não vazia | `media.messaging.dlq.messages > 0` |

Alerta ativo resolve sozinho quando a condição deixa de valer (sem intervenção). O
`--verify-only` do script passa a validar também as regras: IDs estáveis, instrumentos referenciados
existem na tabela da TechSpec, thresholds conferem com a spec, zero conectores.

## Fora do escopo desta task

Notificação externa por qualquer canal (non-goal do PRD), APM UI, SLO. Nenhum código .NET — as
regras consomem instrumentos já entregues em 2.0–5.0.

## Decisões fechadas

- Thresholds confirmados em revisão (2026-09-28); calibráveis pós-rollout sem mudança de código (a
  regra vive no Kibana).
- Mínimo de 4 preparações em A2 (risco "alarme falso com volume baixo" do PRD).

## Modificar / Referenciar

- **modificar:** `scripts/kibana/` (NDJSON das regras + manifesto do `--verify-only`); sem arquivos
  .NET
- **ref:** `techspec.md` § Infraestrutura de visualização (tabela A1–A5 — valores contratuais);
  `docs/infra-servidor-desenv.md` (import); `.env` (`ELASTIC_*` — sem logar valores)

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `scripts/kibana/` | `python3 scripts/kibana/import_observabilidade.py --verify-only` | exit 0 (regras A1–A5 válidas, sem conectores) | Gate desta task (sem CI dedicado — limitação registrada) |

## Pronto quando

- [x] Gate passa (exit 0).
- [ ] Smoke A2: lote de ≥ 4 vídeos corrompidos ativa o alerta em ≤ 15 min e ele resolve sozinho
      após a janela esvaziar.
- [ ] Smoke A4: `media-worker` parado com fila acumulando ativa o alerta; volta ao ar → resolve.
- [ ] A1/A3/A5 verificadas por revisão da definição importada + série sabida no painel.
- [ ] Nenhuma mensagem sai do Kibana (sem conector configurado).
