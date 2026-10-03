---
status: done
task_kind: vertical
blocked_by: []
gate: "python3 scripts/kibana/import_observabilidade.py --verify-only"
gate_expect: "Verificação estrutural passa (exit 0): data view + dashboard 'Pipeline de Mídia' com ID estável, as 3 métricas referenciadas conferidas contra o manifesto (media.videos.count, media.videos.stuck, media.storage.used), painel de staleness presente"
---

# 1.0 Dashboard "Pipeline de Mídia" v1 provisionado como código sobre os sinais existentes

**Fatia:** V-01 · **Cobre:** RF-01 (parcial), RF-03 (parcial: preso), US-01, DP-01 · **Spec:**
`techspec.md#v-01` e § Infraestrutura de visualização · **ADR:** [ADR-0008](../../../docs/adr/0008-observabilidade-kibana-como-codigo.md)

## Comportamento

O Kibana do servidor de desenvolvimento ganha o dashboard **"Pipeline de Mídia"** — seção
**Estado** — consultando apenas o que o serviço já emite hoje: `media.videos.count{status}`
(recebido, em preparação, pronto, falhou, com o vocabulário do domínio, sem tradução),
`media.videos.stuck` e `media.storage.used`, sobre o data view `metrics-generic*`.

O requisito de staleness do RF-01 fica materializado: um painel mostra a idade do ponto mais
recentente do snapshot (60 s em operação normal); sem ponto novo, a série esvazia e a idade cresce
— número congelado não parece atual. Fila vazia é zero explícito em `media.videos.count{status}`
(não ausência de série).

O provisionamento é como código (ADR-0008): saved objects NDJSON versionados em `scripts/kibana/`
com **IDs fixos** (reimportação idempotente) e um script que importa no Kibana e, em
`--verify-only`, valida sem rede: estrutura NDJSON, IDs estáveis, e que toda métrica referenciada
nos painéis existe na tabela de instrumentos da TechSpec (manifesto interno do script). A
importação real usa as credenciais Elastic já no `.env` (`ELASTIC_USERNAME`/`ELASTIC_PASSWORD`,
Kibana `kibana.tasso.dev.br`). `docs/infra-servidor-desenv.md` passa a documentar o
provisionamento.

Segunda pessoa executando o import a partir do repositório obtém o mesmo dashboard — esta é a
evidência de que o Kibana não é fonte de verdade.

## Fora do escopo desta task

Instrumentos novos (2.0–5.0), seções Envio/Fila/Preparação/Outbox (2.0–5.0), alertas (6.0),
paridade local do endpoint OTLP (7.0). Sem código .NET.

## Decisões fechadas

- ADR-0008 (Accepted): Kibana nunca é fonte de verdade; import com overwrite e IDs fixos.
- Data view `metrics-generic*` existente; nenhum índice novo.
- Vocabulário do painel = termos canônicos do PRD (recebido, em preparação, pronto, falhou,
  vídeo preso).

## Modificar / Referenciar

- **modificar:** `docs/infra-servidor-desenv.md` (provisionamento dos saved objects)
- **ref:** `tasks/prd-observabilidade-midia/techspec.md` § Tabela de instrumentos (manifesto dos
  nomes); `docs/infra-servidor-desenv.md` atual (stack do servidor dev); `.env`
  (`ELASTIC_*` — sem logar valores)

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `scripts/kibana/` | `python3 scripts/kibana/import_observabilidade.py --verify-only` | exit 0 | Gate desta task (sem CI dedicado — limitação registrada no plano) |

Sem check automatizado de CI para saved objects: a validação estrutural do script é o veredito
mecânico; a importação real é o smoke.

## Pronto quando

- [x] Gate passa (exit 0).
- [x] Dashboard importado no Kibana do servidor dev mostra os três sinais com os nomes do domínio
      e valores coerentes com o banco (com produção/local enviando OTLP).
- [x] Painel de staleness visível: idade do último ponto ≈ 60 s em operação; sem ponto novo, a
      idade aparece crescendo.
- [x] Import reexecutado por segunda pessoa a partir do repo sem erro e sem duplicar objetos.
