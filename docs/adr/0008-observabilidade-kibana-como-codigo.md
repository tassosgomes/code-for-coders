# ADR-0008: Observabilidade do Kibana provisionada como código

## Status

Accepted

## Identidade e escopo

- Caminho: `docs/adr/0008-observabilidade-kibana-como-codigo.md`
- Domínios/componentes afetados: infraestrutura de observabilidade (Elasticsearch/Kibana do
  servidor de desenvolvimento); todos os serviços que passarem a ter dashboard e alerta.
- Origem histórica: `tasks/prd-observabilidade-midia` (CAP-006, segunda fatia).
- Substitui: Nenhuma.

## Data

2026-09-28

## Contexto

A plataforma coleta traces, logs e métricas via OpenTelemetry e indexa no Elasticsearch do servidor
de desenvolvimento, com visualização no Kibana. O baseline arquitetural define a divisão: a
aplicação garante emissão em OTLP; coleta, dashboards e roteamento de alerta são do time de
plataforma. Até aqui, porém, nenhum dashboard ou alerta existia — a consulta era manual no Discover.

A primeira entrega que materializa painéis e alertas (pipeline de mídia) precisa decidir como esses
objetos nascem e sobrevivem no Kibana. Criar direto na interface gera estado que não vive no
repositório: não é revisável, não é recriável por outra pessoa, diverge silenciosamente do que a
aplicação emite e morre com o ambiente. A stack do servidor de desenvolvimento é uma máquina só,
sem HA — a reconstrução do ambiente é um evento esperado, não hipotético.

## Decisão

Dashboards, regras de alerta e data views que a plataforma mantém são **saved objects NDJSON
versionados no repositório** (em `scripts/kibana/`) e **importados no Kibana por script**. O
Kibana é fonte de execução, nunca fonte de verdade: alteração passa pelo repositório, é revisada e
reimportada. Exportar do Kibana de volta para NDJSON é o caminho legítimo de autoria, seguido de
commit.

Cada PRD que criar painel ou alerta entrega seus saved objects junto com a instrumentação que os
alimenta; nomes de métricas e dimensões são contrato entre aplicação e saved objects, e mudam
juntos.

## Alternativas Consideradas

### Alternativa 1: criação manual na interface, documentada em doc

- **Descrição:** um operador cria dashboard e alertas na UI seguindo um guia em `docs/`.
- **Prós:** sem curva de ferramenta; rápido no primeiro dia.
- **Contras:** irreproduzível sem conhecimento tribal; divergência silenciosa entre doc, repo e
  Kibana; reconstrução do servidor dev perde tudo que não está no repo.
- **Por que rejeitada:** o problema que motivou o PRD de observabilidade foi exatamente depender de
  consulta manual e conhecimento tribal; reproduzir isso na solução seria circular.

### Alternativa 2: Terraform provider Elastic + pipeline de infra

- **Descrição:** gerenciar Kibana como infraestrutura declarativa com Terraform.
- **Prós:** drift detectado; estado gerenciado.
- **Contras:** ferramenta nova para o time, custo desproporcional para meia dúzia de objetos;
  provider sensível à versão do Kibana, igual ao NDJSON, sem ganho adicional neste porte.
- **Por que rejeitada:** otimização prematura para o volume atual; NDJSON versionado cobre
  revisabilidade e recriação.

## Consequências

### Positivas

- Painel e alerta são revisáveis em PR junto com a instrumentação que os alimenta.
- Ambiente reconstruído a partir do repositório, sem conhecimento tribal.
- Segunda pessoa consegue auditar o que alerta e o que o painel mostra.

### Negativas

- NDJSON de Kibana é verboso e acoplado ao formato da versão 9.x (migração de versão do Kibana
  exige re-export).
- A autoria natural é na UI; o passo de exportar/commitar pode ser esquecido.

### Riscos

- Divergência repo ↔ Kibana se alguém editar direto na UI — mitigação: regra explícita de
  "repo é fonte de verdade" e reimportação idempotente no script.
- Objetos com ID duplicado em import repetido — mitigação: import por overwrite determinístico
  com IDs fixos nos NDJSON.

## Notas de Implementação

- Import via API de saved objects do Kibana (NDJSON, overwrite com IDs estáveis), executado pelo
  script de acesso ao servidor de desenvolvimento (ver `scripts/remote-infra.sh` como referência de
  acesso e `docs/infra-servidor-desenv.md` como documentação do roteamento).
- Alertas sem conector de notificação nesta fase (decisão DP-02 do PRD de observabilidade); quando
  houver canal externo, o conector vira outro saved object versionado.

## Referências

- `context/architecture-baseline.md` — Padrões de Observabilidade (emissão pela aplicação;
  dashboards e alerta do time de plataforma).
- `docs/infra-servidor-desenv.md` — stack Elasticsearch/Kibana/Collector do servidor dev.
- `tasks/prd-observabilidade-midia/` — PRD e TechSpec da primeira entrega a aplicar esta decisão.
