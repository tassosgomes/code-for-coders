---
status: pending
task_kind: vertical
blocked_by: ["1.0"]
gate: 'docker compose -f docker-compose.yml config --quiet && OTEL_EXPORTER_OTLP_ENDPOINT=http://192.168.0.5:4317 docker compose -f docker-compose.yml config | grep -q "192.168.0.5:4317"'
gate_expect: "exit 0: compose válido com o default http://otel-collector:4317 preservado, e o override da variável aparece na configuração interpolada dos serviços"
---

# 7.0 Paridade local: endpoint OTLP interpolável no Compose

**Fatia:** V-07 · **Cobre:** suporte à evidência de V-01..V-06 fora do servidor dev · **Spec:**
`techspec.md#v-07` · **ADR:** —

## Comportamento

O `docker-compose.yml` passa a interpolar o endpoint OTLP dos serviços que hoje o fixam em
`http://otel-collector:4317`: `OTEL_EXPORTER_OTLP_ENDPOINT:
${OTEL_EXPORTER_OTLP_ENDPOINT:-http://otel-collector:4317}`. Sem variável, nada muda (o collector
local continua `debug`); com
`OTEL_EXPORTER_OTLP_ENDPOINT=http://192.168.0.5:4317` no `.env`, a telemetria da stack local vai
direto ao Elastic do servidor de desenvolvimento, e o dashboard "Pipeline de Mídia" reflete o que
acontece localmente — mesma régua, mesmo painel.

`docs/infra-servidor-desenv.md` documenta o override (quando usar, o que esperar, e que a rede
precisa alcançar `192.168.0.5:4317` e o Kibana).

## Fora do escopo desta task

Subir Elastic/Kibana no compose local (não faz parte — a paridade é por apontamento ao servidor
dev), mudar o collector local (continua `debug`), `docker-compose.coolify.yml` (produção já aponta
ao servidor dev).

## Decisões fechadas

- Paridade por override de endpoint, não por stack local nova (TechSpec V-07).
- Default preservado: ausência da variável não muda comportamento.

## Modificar / Referenciar

- **modificar:** `docker-compose.yml` (interpolação em todos os serviços que fixam o endpoint);
  `docs/infra-servidor-desenv.md` (override documentado)
- **ref:** `otel-collector-config.yaml` (collector local permanece debug — inalterado);
  `docker-compose.coolify.yml` (produção já aponta a `192.168.0.5:4317` — referência do padrão)

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `docker-compose.yml` | `docker compose -f docker-compose.yml config --quiet` | exit 0, default preservado | Gate desta task (sem CI de compose — limitação registrada) |
| `docker-compose.yml` | `OTEL_EXPORTER_OTLP_ENDPOINT=http://192.168.0.5:4317 docker compose -f docker-compose.yml config \| grep -c "192.168.0.5:4317"` | ≥ 1 (exit 0) | Idem |

## Pronto quando

- [ ] Gate passa (exit 0): compose válido, default intacto, override aplicado.
- [ ] Smoke: com a variável apontada e a stack local no ar, um vídeo enviado localmente aparece no
      dashboard "Pipeline de Mídia" em ≤ 2 min.
- [ ] Sem a variável, `docker compose config` mostra `http://otel-collector:4317` como antes.
