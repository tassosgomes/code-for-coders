---
tsg_artifact: prd
product: code-4-coders
capability: CAP-006
version: 1.0
status: approved
updated: 2026-09-28
sources: backlog/capabilities.md@1.4, vision.md@1.2, context/domain-map.md@1.2, context/architecture-baseline.md@1.2, domains/entrega-de-midia-e-protecao/domain.md@1.0
---

# Observabilidade operacional da ingestão de mídia — a equipe vê o pipeline sem perguntar a ninguém

## Visão Geral

O pipeline de ingestão de mídia funciona, mas para a equipe ele é uma caixa preta: o professor vê o
estado do vídeo dele no backoffice, e a equipe só descobre problema quando alguém reclama ou quando
alguém abre o banco na mão. Não há como responder, em tempo real, perguntas operacionais básicas:
quantos vídeos estão em andamento, há quanto tempo a fila espera, se a preparação está falhando e por
quê, se os eventos de mídia estão saindo.

Esta entrega dá à equipe interna **um painel operacional do pipeline no Kibana** — funil de envio,
fila de preparação, resultado e saúde do outbox — mais **regras de alerta dentro do Kibana** que
acendem quando o pipeline para. Nada muda para o professor, e nenhuma tela nova é criada no
backoffice: a observação operacional vive onde a equipe já monitora a plataforma.

É o complemento operacional de `CAP-006`: o PRD anterior fez o estado do vídeo ser visível a quem
enviou; este faz a **saúde do pipeline** ser visível a quem opera.

---

## Rastreabilidade

### Capacidade e fronteiras

- **Capacidade:** `CAP-006` — Ingestão e preparação de mídia protegida.
- **Escopo desta entrega:** o pipeline de envio e preparação (sessão de envio → recebido → em
  preparação → pronto/falhou) e a publicação dos eventos dele ficam **observáveis pela equipe no
  Kibana**: estado do pipeline por etapa, funil e tamanho de envio, profundidade e idade da fila,
  duração e falhas da preparação por motivo, saúde do outbox e da DLQ; alertas de threshold apenas
  no Kibana, sem canal externo; investigação de um vídeo específico até os traces e logs existentes.
- **Fora desta entrega:**
  - notificação de alerta por e-mail, WhatsApp, pager ou qualquer canal fora do Kibana → fatia
    futura, quando houver plantão para ser acordado;
  - métricas de reprodução e entrega ao aluno (sessão de reprodução, CDN, custo por aluno ativo) →
    `CAP-007`, que ainda não existe; RN-M17 permanece parcial como em `CAP-006` v1;
  - qualquer tela nova no backoffice para operação → a operação é no Kibana;
  - SLO do caminho crítico e "tempo até pronto" com meta → o valor permanece observado, sem alvo,
    como decidido no PRD anterior;
  - mudanças no comportamento do pipeline (capacidade de preparação, retries, limites) → este PRD
    observa; não muda o que está entregue;
  - observabilidade de outros serviços além de `media` (api e worker) → o padrão criado aqui é
    reutilizável, mas cada pipeline ganha sua fatia.
- **Domínios atravessados:** Entrega de Mídia e Proteção —
  [domain.md](../../../domains/entrega-de-midia-e-protecao/domain.md). Nenhum outro domínio de negócio é
  tocado: a observação não produz evento nem muda junta.
- **Dependências entre capacidades:** `CAP-006` (PRD anterior, entregue — pipeline existente e sinais
  de volume V-07); infraestrutura de telemetria existente (collector OTLP + Elasticsearch/Kibana do
  servidor de desenvolvimento, que já indexa traces e logs).
- **Restrições do baseline:** telemetria é OpenTelemetry + OTLP, coleta/dashboards/alerta são do time
  de plataforma e a aplicação garante emissão; métrica nomeada `{servico}.{agregado}.{evento}` com
  `unit`; **dimensão nunca é Id**; sem dimensão por escola em telemetria (DE10, resolvido como D-06
  no techspec anterior); sinais obrigatórios desde a Fase 1 incluem outbox atrasado ou esgotado e
  DLQ não vazia — esta entrega materializa esses sinais para o serviço de mídia; log sem PII (G23).

### Vision Doc

- **Objetivos de negócio atendidos:** `C04` (entrega de mídia sob controle próprio — agora também
  operável, não só funcionando).
- **Restrições globais aplicáveis:** stack OTel padronizada; monitoramento existente em
  Elastic/Kibana; nenhuma dependência nova de fornecedor.
- **Non-Goals globais respeitados:** G21/G22 não são afetados (nenhum objeto exposto, nenhum
  derivado por aluno); a proteção continua dissuasão declarada (BA15) — nada aqui a apresenta como
  garantia.

### Domain Docs

- **Entidades envolvidas:** Mídia (vídeo) e seu estado (RN-M05), sessão de envio (`VideoUpload`),
  outbox de publicação — todas de Entrega de Mídia e Proteção.
- **Regras de negócio referenciadas:** RN-M05 (estado recebido → em preparação → pronto/falhou),
  RN-M17 (custo de armazenamento como sinal — a parte já entregue é observada; a parte por aluno
  ativo continua com `CAP-007`).
- **Regras nascidas neste PRD:** nenhuma — a observação não cria regra de negócio nova; os limites
  de "vídeo preso" e "outbox esgotado" já existem (PRD anterior e baseline) e apenas ganham sinal.
- **Eventos consumidos:** nenhum.
- **Eventos produzidos:** nenhum novo. `midia.ativo-pronto.v1` e `midia.preparacao-falhou.v1`
  continuam como estão; esta entrega observa a publicação deles, não a altera.

## Termos Canônicos

| Termo | Definição de negócio | Escopo/Fonte |
|---|---|---|
| Pipeline de mídia | A jornada do vídeo na ingestão: sessão de envio → recebido → em preparação → pronto/falhou, incluindo a publicação dos fatos pelo outbox. Termina no pronto; a reprodução é `CAP-007`. | Este PRD |
| Em andamento | Sessões de envio pendentes somadas a vídeos em *recebido* aguardando preparação e em *em preparação*. | Este PRD |
| Vídeo preso | Vídeo em *recebido* ou *em preparação* há mais de 4× a própria duração (ou 12h, o que vier antes). | PRD `CAP-006` v1 (herdado) |
| Tempo até pronto | Tempo entre *recebido* e *pronto*. | PRD `CAP-006` v1 (herdado) |
| Outbox esgotado | Mensagem do outbox que esgotou as tentativas de publicação sem sucesso — o fato aconteceu e o mundo não soube. | Baseline / Este PRD |
| Falha de preparação | Preparação terminada em *falhou*, classificada por motivo (arquivo ilegível, formato não suportado, duração excedida, tentativas esgotadas). | PRD `CAP-006` v1 (herdado) |

---

## Objetivos

- A equipe responde, em até um minuto e sem abrir o banco, as perguntas operacionais do pipeline:
  quantos envios e preparações estão em andamento, há quanto tempo, com que resultado.
- Falhas de preparação deixam de ser descobertas por reclamação: aparecem no painel com motivo, no
  dia em que acontecem.
- O pipeline parado (fila que não anda, outbox esgotado, worker fora do ar) é percebido por regra de
  alerta no Kibana, não por ausência de vídeo pronto.
- Os sinais obrigatórios do baseline para o serviço de mídia — outbox atrasado/esgotado, DLQ não
  vazia — existem de fato e são consultáveis.
- O vocabulário do painel é o mesmo do backoffice e do domínio (recebido, em preparação, pronto,
  falhou), para que operador e professor falem a mesma língua.

---

## Histórias de Usuário

- Como **operador da plataforma**, eu quero ver quantos vídeos estão em cada etapa do pipeline
  (sessões de envio pendentes, aguardando preparação, em preparação) para que eu saiba o que está em
  andamento sem consultar o banco.
- Como **operador da plataforma**, eu quero ver a profundidade e a idade da fila de preparação para
  que eu perceba congestionamento antes que professores percebam.
- Como **operador da plataforma**, eu quero ver as falhas de preparação por motivo, em janela de
  tempo, para que eu comunique o professor afetado com a causa certa e saiba se é caso pontual ou
  padrão.
- Como **operador da plataforma**, eu quero alertas no Kibana quando o pipeline parar ou enfileirar
  além do normal para que eu reaja sem ficar olhando painel.
- Como **desenvolvedor**, eu quero partir de um vídeo com problema até os traces e logs dele para
  que eu diagnostique a causa sem reproduzir o envio.
- Como **operador da plataforma**, eu quero ver a saúde do outbox e da DLQ para que eu saiba se os
  fatos de mídia estão de fato chegando aos consumidores.

---

## Funcionalidades Principais

### RF-01: Painel "Pipeline de Mídia" com o estado por etapa

**Descrição**: Um dashboard no Kibana mostra, em um só lugar, o retrato atual do pipeline: quantas
sessões de envio estão pendentes, quantos vídeos aguardam preparação (*recebido*), quantos estão em
*em preparação*, e o acumulado de *pronto* e *falhou*. Os números vêm de sinal emitido continuamente
pelo serviço de mídia, com a idade da última atualização visível.

**Critérios de Aceitação**:

- **Given** vídeos distribuídos entre as etapas do pipeline
  **When** o operador abre o dashboard "Pipeline de Mídia"
  **Then** ele vê a contagem por etapa usando o vocabulário do domínio (sessão de envio pendente,
  recebido, em preparação, pronto, falhou), consistente com o estado real verificado no banco dentro
  da janela de atualização do sinal.

- **Given** o papel `worker` do serviço de mídia fora do ar por mais de uma janela de atualização
  **When** o operador abre o dashboard
  **Then** a ausência de atualização é perceptível (idade da última atualização à vista), em vez de
  um número congelado que parece atual.

- **Given** nenhum vídeo em nenhuma etapa
  **When** o operador abre o dashboard
  **Then** o painel mostra zero em cada etapa, não erro nem ausência de painel.

**Prioridade**: Must Have

**Rastreabilidade**: Mídia RN-M05; baseline (sinais de estado).

---

### RF-02: Funil e tamanho dos envios

**Descrição**: O dashboard mostra o funil das sessões de envio — criadas, concluídas (viraram vídeo
*recebido*) e expiradas por abandono — e a distribuição de tamanho dos arquivos enviados, para a
equipe distinguir "professor desistiu" de "upload quebrado" e conhecer o perfil de volume que chega.

**Critérios de Aceitação**:

- **Given** um professor que inicia, envia partes e conclui uma sessão de envio
  **When** o operador consulta o dashboard na janela seguinte
  **Then** a sessão aparece como criada e concluída, e o tamanho do arquivo entra na distribuição de
  tamanhos.

- **Given** uma sessão de envio abandonada que expira (24h)
  **When** o operador consulta o dashboard
  **Then** a sessão aparece como expirada, contada separadamente de concluída e de falha de
  preparação — abandono de envio não é falha do pipeline.

- **Given** a janela de tempo escolhida pelo operador
  **When** o dashboard é consultado
  **Then** criadas, concluídas e expiradas são comparáveis dentro da mesma janela, permitindo calcular
  a conclusão relativa.

**Prioridade**: Must Have

**Rastreabilidade**: PRD `CAP-006` v1 (envio retomável e expiração); baseline (métrica com `unit`).

---

### RF-03: Profundidade e idade da fila de preparação

**Descrição**: O dashboard mostra quantos vídeos aguardam preparação e há quanto tempo o mais antigo
espera, além do sinal já existente de vídeo preso. É a resposta direta à pergunta "a fila está
andando?".

**Critérios de Aceitação**:

- **Given** vídeos em *recebido* aguardando preparação
  **When** o operador consulta o dashboard
  **Then** ele vê a quantidade aguardando e a idade do aguardante mais antigo, na mesma vista.

- **Given** um vídeo que ultrapassa o limite de vídeo preso (4× a própria duração ou 12h)
  **When** o operador consulta o dashboard
  **Then** o sinal de vídeo preso está visível e maior que zero, distinto da profundidade comum da
  fila.

- **Given** um vídeo em *em preparação* executando normalmente há menos do que o limite de preso
  **When** o operador consulta o dashboard
  **Then** o vídeo conta como em preparação e não como preso — preparação longa de arquivo grande não
  é falsamente sinalizada.

**Prioridade**: Must Have

**Rastreabilidade**: PRD `CAP-006` v1 (vídeo preso); Mídia RN-M05.

---

### RF-04: Resultado da preparação — duração, tentativas e falhas por motivo

**Descrição**: O dashboard mostra o tempo até pronto, a duração das etapas da preparação (download,
análise, transcodificação, publicação), as tentativas por vídeo e as falhas classificadas por motivo,
em janela de tempo. É o que transforma "falhou" em informação acionável.

**Critérios de Aceitação**:

- **Given** uma preparação concluída com sucesso
  **When** o operador consulta o dashboard
  **Then** o tempo até pronto e a duração de cada etapa estão disponíveis em percentis, e o vídeo
  conta como pronto — tentativas intermediárias de falha passageira não contam como falha.

- **Given** uma preparação que falhou após esgotar tentativas
  **When** o operador consulta o dashboard
  **Then** a falha aparece classificada por motivo (arquivo ilegível, formato não suportado, duração
  excedida, tentativas esgotadas), com o número de tentativas.

- **Given** um período com pico de falhas de um mesmo motivo
  **When** o operador compara janelas no dashboard
  **Then** ele consegue ver a taxa de falha por motivo no período, distinguindo caso pontual de
  padrão.

**Prioridade**: Must Have

**Rastreabilidade**: Mídia RN-M05; PRD `CAP-006` v1 (motivos de falha, tempo até pronto observado);
baseline (dimensão nunca é Id — motivo é conjunto fechado, não identificador).

---

### RF-05: Saúde do outbox e da DLQ

**Descrição**: O dashboard mostra o estado da publicação dos fatos de mídia: mensagens do outbox
pendentes, idade da mais antiga, esgotadas, e o sinal de DLQ não vazia. Materializa para o serviço de
mídia os sinais obrigatórios do baseline.

**Critérios de Aceitação**:

- **Given** o outbox saudável (publicando dentro da janela de polling)
  **When** o operador consulta o dashboard
  **Then** pendentes fica próximo de zero e a idade da mais antiga, abaixo da janela de polling.

- **Given** uma mensagem que esgota as tentativas de publicação
  **When** o operador consulta o dashboard
  **Then** o contador de esgotadas é maior que zero e visível — o fato aconteceu e não foi publicado.

- **Given** mensagens na DLQ de mídia
  **When** o operador consulta o dashboard
  **Then** o sinal de DLQ não vazia está acessível a partir do mesmo painel, sem consulta manual à
  ferramenta de mensageria.

**Prioridade**: Must Have

**Rastreabilidade**: baseline (outbox atrasado ou esgotado; DLQ não vazia — sinais obrigatórios).

---

### RF-06: Alertas de threshold no Kibana

**Descrição**: Regras de alerta criadas no Kibana acendem quando o pipeline para ou degrada: vídeo
preso, taxa de falha de preparação alta, outbox esgotado ou atrasado, espera em fila excessiva. Nesta
fatia os alertas existem **apenas dentro do Kibana** (Stack Alerting), sem notificação por canal
externo; resolvem sozinhos quando a condição normaliza.

**Critérios de Aceitação**:

- **Given** vídeo preso acima de zero por 15 minutos
  **When** a regra avalia
  **Then** o alerta "vídeo preso" fica ativo no Kibana.

- **Given** taxa de falha de preparação acima de 10% em 15 minutos (mínimo de amostras definido na
  TechSpec para evitar alarme com 1 vídeo)
  **When** a regra avalia
  **Then** o alerta "taxa de falha de preparação" fica ativo.

- **Given** outbox esgotado acima de zero, ou pendente mais antiga com mais de 10 minutos
  **When** a regra avalia
  **Then** o alerta "outbox" fica ativo.

- **Given** vídeo aguardando preparação com mais de 30 minutos de espera
  **When** a regra avalia
  **Then** o alerta "fila parada" fica ativo.

- **Given** a condição que ativou um alerta volta ao normal
  **When** a regra avalia novamente
  **Then** o alerta resolve sozinho no Kibana, sem intervenção.

- **Given** qualquer alerta ativo
  **When** ele é consultado
  **Then** nenhuma mensagem é enviada por e-mail ou outro canal — o alcance desta fatia é a tela de
  alertas do Kibana.

**Prioridade**: Must Have

**Rastreabilidade**: baseline (roteamento de alerta é do time de plataforma); RF-03, RF-04, RF-05
deste PRD.

---

### RF-07: Investigação de um vídeo até o trace

**Descrição**: A partir de um vídeo com problema (identificado na lista do backoffice ou numa
reclamação), a equipe encontra no Kibana os spans e logs da preparação dele, usando a correlação que
o pipeline já propaga. O painel responde "o que está acontecendo no agregado"; esta função responde
"o que aconteceu com este".

**Critérios de Aceitação**:

- **Given** um vídeo que falhou, com identificador conhecido pela equipe
  **When** o operador pesquisa o correlacionador dele no Kibana
  **Then** encontra os spans das etapas de preparação e os logs do vídeo, sem reproduzir o envio.

- **Given** um vídeo em preparação no momento da investigação
  **When** o operador segue o mesmo caminho
  **Then** vê a etapa corrente em execução e o tempo já decorrido.

**Prioridade**: Should Have

**Rastreabilidade**: baseline (traceparent propagado; `ProblemDetails` com `traceId`); PRD
`CAP-006` v1 (correlação do vídeo com o trace).

---

## Experiência do Usuário

**Persona.** Operador da plataforma — desenvolvedor da equipe, que hoje monitora a saúde pelos
dashboards que não existem e pelo Discover do Kibana, consulta que exige saber o que procurar. A
persona secundária é o próprio desenvolvedor em investigação pontual (RF-07).

**Jornada operacional.** Abre o Kibana → dashboard "Pipeline de Mídia" → lê o retrato (em andamento
por etapa, fila, falhas, outbox) em menos de um minuto → se algo estiver fora do normal, segue o
alerta ativo ou a investigação por vídeo. O painel usa o vocabulário do domínio e do backoffice
(*recebido*, *em preparação*, *pronto*, *falhou*, vídeo preso), sem jargão interno de tabela ou
código.

**Jornada de investigação.** Um vídeo falhou e o professor pergunta: o operador pega o vídeo no
backoffice → localiza a preparação dele no Kibana pela correlação → vê a etapa exata e o motivo →
responde ao professor ou abre correção. Sem reproduzir envio, sem acessar o banco.

**Sem mudança para o professor.** Nenhuma tela, texto ou comportamento do backoffice muda. O que o
professor vê continua sendo o que o PRD anterior entregou.

---

## Decisões de Produto

| ID | Decisão confirmada | Alternativas descartadas e motivo | Impacto no PRD | Registro |
|---|---|---|---|---|
| DP-01 | A observação operacional vive no **Kibana**, não em tela nova do backoffice: o público é a própria equipe, que já usa o Kibana, e a correlação com traces e logs já está lá. | Tela de operação no admin SPA: custo de UI e backend para público minúsculo, sem ganho sobre o que o Kibana já dá. | Todo o escopo (RF-01 a RF-07); non-goal explícito. | — |
| DP-02 | Alertas desta fatia existem **só no Kibana**, sem canal externo: não há plantão para ser acordado, e alerta que notifica sem dono vira ruído. | Notificação por e-mail: descartada para esta fatia; volta quando houver plantão e política de escalonamento. | RF-06; non-goal explícito. | — |
| DP-03 | Métricas do pipeline **sem dimensão por escola e sem identificador** (vídeo, autor, título): dimensões são conjuntos fechados (etapa, motivo). Herda DE10/D-06 e a regra do baseline "dimensão nunca é Id". | Permitir filtro por escola/vídeo: viola o baseline e o decidido no techspec anterior; investigação pontual usa a correlação de trace (RF-07), não métrica. | RF-01 a RF-06; sem efeito no fluxo. | Baseline + techspec `CAP-006` v1 (D-06) |
| DP-04 | O **estado do pipeline é sinal emitido pelo próprio serviço** de mídia, e não consulta ao banco por fora: o dashboard reflete o que o serviço sabe de si, e a régua é a mesma em qualquer ambiente. | ETL/consulta direta ao banco pelo painel: acopla o painel ao schema, contorna a régua OTel do baseline e não resolve alerta. | Base técnica dos RF-01 a RF-05 (detalhe na TechSpec). | Baseline (aplicação garante emissão) |

---

## Não-Objetivos (Fora de Escopo)

- Notificação de alerta por qualquer canal externo (e-mail, WhatsApp, pager).
- Tela, card ou indicador operacional novo no backoffice.
- Métricas de reprodução e entrega ao aluno — sessão, CDN, custo por aluno ativo (RN-M17 completo):
  `CAP-007`.
- Meta/SLO para tempo até pronto: o valor continua sendo observado, como decidido no PRD anterior.
- Qualquer mudança no comportamento do pipeline: capacidade, limites, retry, expiração continuam os
  mesmos — este PRD não corrige o que observa.
- Observabilidade de serviços outros que não `media` (o padrão é reutilizável, cada pipeline tem sua
  fatia).
- APM UI do Elastic e análise de dependências: evolução futura da mesma stack.
- Página de status pública ou visível ao professor/escola.

---

## Plano de Rollout Faseado

### Fase 1 — Retrato imediato com o sinal que já existe

- **Funcionalidades incluídas:** primeira versão do RF-01 (estado por etapa com os sinais de volume
  já emitidos: vídeos por estado, preso, storage) e parte do RF-03 (preso).
- **Critério para avançar:** o dashboard "Pipeline de Mídia" existe no Kibana e mostra vídeos por
  estado com idade de atualização visível; a equipe o abre no lugar do banco.

### Fase 2 — Funil completo, alertas e investigação

- **Funcionalidades adicionais:** RF-01 completo, RF-02, RF-03 completo, RF-04, RF-05, RF-06, RF-07.
- **Critério de sucesso:** as perguntas das histórias de usuário são respondidas pelo dashboard; as
  cinco regras de alerta estão criadas e resolvem sozinhas; uma falha de preparação real (ou
  simulada) é investigada até o trace sem acessar o banco.

---

## Métricas de Sucesso

| Métrica | Definição | Alvo | Prazo |
|---|---|---|---|
| Percepção do estado | Tempo entre um evento do pipeline (nova sessão, falha, preso) e o sinal refletir no dashboard | ≤ janela de atualização do sinal | Contínuo |
| Falha com motivo | Preparações terminadas em *falhou* cujo motivo está visível no dashboard | 100% | Desde o rollout |
| Falha descoberta pela equipe | Falhas de preparação percebidas pelo painel antes de reclamação de professor | 100% | Primeiros 90 dias |
| Pipeline parado percebido | Tempo entre pipeline parado (fila/outbox/worker) e alerta ativo | ≤ 15 min | Desde o rollout |
| Tempo até pronto | Tempo entre *recebido* e *pronto* | Observado, sem alvo (herdado) | Primeiros 90 dias |

---

## Riscos e Mitigações

- **Painel sem dono apodrece** (criado, nunca mais aberto) — Mitigação: os alertas do RF-06 dão razão
  para voltar; a retro periódica da equipe inclui o painel na checagem.
- **Vocabulário divergente entre painel, banco e backoffice** (o mesmo estado com três nomes) —
  Mitigação: termos canônicos deste PRD são os do domínio (RN-M05) e o dashboard os usa sem tradução.
- **Confusão entre abandono de envio e falha do pipeline** (professor desistiu ≠ upload quebrou) —
  Mitigação: RF-02 separa expirada de concluída e de falha de preparação.
- **Dependência de infraestrutura de uma máquina só** (Elastic/Kibana no servidor de desenvolvimento,
  sem HA) — Mitigação: aceita nesta fase; disponibilidade dessa infra é preocupação de plataforma,
  registrada como questão em aberto.
- **Alarme falso com volume baixo** (1 vídeo falho = 100% de taxa) — Mitigação: mínimo de amostras
  por janela definido na TechSpec (RF-06).

---

## Alternativas Consideradas

### Abordagem Escolhida: sinal OTel do próprio serviço + dashboards e alertas no Elastic/Kibana existente

- **Descrição:** o serviço de mídia emite métricas de pipeline seguindo o padrão OTel do baseline; o
  Kibana, que já recebe traces e logs da plataforma, ganha o dashboard e as regras de alerta.
- **Por que foi escolhida:** zero stack nova para operar, o dado fica junto de traces e logs (a
  investigação do RF-07 sai de graça), e cumpre a divisão do baseline — a aplicação emite, a
  plataforma visualiza e alerta.

### Alternativa Rejeitada 1: Prometheus + Grafana dedicados

- **Descrição:** exporters/prometheus como fonte de métricas, Grafana para painéis e alertas.
- **Trade-offs:** ecossistema maduro de métricas; em troca, mais uma stack para instalar, atualizar e
  dar acesso, e um segundo lugar para olhar além do Kibana que já concentra logs e traces.
- **Por que foi rejeitada:** duplica infraestrutura e acesso para um time pequeno, sem ganho que a
  stack existente não dê neste volume.

### Alternativa Rejeitada 2: tela de operação no backoffice

- **Descrição:** área administrativa no admin SPA consultando o estado do pipeline por API.
- **Trade-offs:** UX sob controle total; em troca, backend e UI novos para público restrito à equipe,
  sem correlação com traces e logs, que continuariam no Kibana.
- **Por que foi rejeitada:** custo desproporcional ao público; registrou-se como DP-01.

### Alternativa Rejeitada 3: continuar no Discover com consulta manual

- **Descrição:** o status quo melhorado: queries salvas no Discover em vez de dashboard e alertas.
- **Trade-offs:** nenhum custo de construir; em troca, exige saber o que procurar, não responde estado
  em um minuto e não percebe nada sozinha.
- **Por que foi rejeitada:** é exatamente a condição que motivou o pedido — a equipe no escuro.

---

## Questões em Aberto

- **Thresholds finais dos alertas** — os valores do RF-06 (15 min preso, 10% falha, 10 min outbox,
  30 min fila) são a proposta inicial; confirmar na revisão deste PRD com a equipe. Impacto se não
  resolvido: alertas criados com valores padrão e calibrados depois.
- **Retenção das métricas no Elastic** (política de ILM do índice `metrics-generic`) — time de
  plataforma, antes do rollout da Fase 2. Não bloqueia o draft; define janela histórica consultável.
- **Paridade do ambiente local** (collector local do docker-compose apontando ao Elastic do servidor
  de desenvolvimento) — decisão de infra a tratar na TechSpec; o PRD não assume.
