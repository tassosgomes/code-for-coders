---
tsg_artifact: capability-backlog
product: code-4-coders
version: 1.3
status: approved
updated: 2026-09-20
sources: vision.md@1.1, context/domain-map.md@1.1, context/architecture-baseline.md@1.2
---

# Backlog de Capacidades

> **Nível 3 da hierarquia de documentação.** Deriva de `vision.md` (v1.1), `context/domain-map.md` (v1.1) e
> `context/architecture-baseline.md` (v1.2). Traduz domínios e fronteiras em **capacidades de negócio** —
> unidades de valor que podem originar um PRD independente. Não decide feature, tela, endpoint ou tabela:
> isso é escopo do PRD e da TechSpec.

**Versão:** 1.3 (aprovado) · **Data:** 2026-09-20 · **Produto:** code-4-coders · **Total:** 31 capacidades em 16 domínios · **MVP:** 11

Cada capacidade tem **ID estável** (`CAP-001`…). Domain Docs e PRDs referenciam o ID, nunca o título.
IDs são preservados em atualizações deste documento; capacidade descartada fica registrada como
`descartada`, e seu ID não é reutilizado.

---

## Estratégia de Sequenciamento

### 1. Domínios críticos para o MVP

A visão define a Fase 1 como "vender e assistir fim a fim". Isso torna críticos **7 domínios** —
Identidade e Acesso, Catálogo e Oferta, Conteúdo e Currículo, Entrega de Mídia e Proteção, Matrícula e
Direito de Acesso, Vendas e Checkout, Aprendizagem e Progresso — mais **Auditoria e Conformidade** em
versão mínima, mais **Notificação** em versão transacional mínima — conforme o alerta de escopo do
Domain Map e o agrupamento inicial de 6 serviços do baseline (BA02).

Houve uma **divergência com a visão, resolvida em 2026-09-20**: a visão coloca Notificação (`C14`) na
Fase 2, mas `CAP-001` (conta e autenticação) não fecha ciclo de valor sem e-mail — confirmação de conta
e recuperação de senha são produto mínimo, não melhoria, e `CAP-011` não comprova compra sem ele.
`CAP-026` foi antecipada ao MVP **em versão transacional mínima** (um canal, sem preferência, sem
campanha) e `notification` passou ao grupo inicial do baseline (BA02). A alternativa de hospedá-la como
módulo dentro de `identity` foi descartada: `CAP-011` também precisa de e-mail no MVP e forçaria
`commerce → identity`, junta que o Domain Map não permite. Histórico em Riscos (R5).

### 2. Fluxos centrais de negócio

Os três fluxos de valor do Domain Map, mapeados em capacidades:

| Fluxo | Capacidades que o completam | Fase |
|---|---|---|
| Descobrir → comprar → acessar | `CAP-003` → `CAP-011` → `CAP-008` | MVP |
| Assistir → progredir → ser avaliado → comprovar | `CAP-007` → `CAP-017` → `CAP-020` → `CAP-022` | MVP + Fase 3 |
| Cobrar → manter ou bloquear | `CAP-013` → `CAP-014` → `CAP-009` | Fase 2 |

O MVP fecha o primeiro fluxo inteiro e a primeira metade do segundo. O terceiro fluxo não existe no MVP
porque a venda avulsa não cria relação financeira continuada — é exatamente o que a visão isolou como
risco de complexidade.

### 3. Dependências estruturais que determinam a ordem

Quatro dependências não são negociáveis e ditam o sequenciamento:

- **Identidade antes de tudo.** Nenhuma capacidade tem ator sem `CAP-001`/`CAP-002`.
- **Conteúdo antes de oferta.** Não se vende o que não existe: `CAP-005` precede `CAP-003`.
- **Direito de acesso antes de venda.** `CAP-008` é construída **antes** de `CAP-011`, não depois. Essa
  inversão da ordem intuitiva é deliberada: é a mitigação prática do risco DE01/BA04 — se a concessão
  nascer como consequência do checkout, ela vira flag de pedido e o guardrail G20 passa a ser violado
  por construção. Com `CAP-008` pronta primeiro, `CAP-011` só publica o fato "compra concluída" e a
  concessão já existe como conceito com dono, vigência e origem próprios.
- **Decisão de acesso antes de reprodução.** `CAP-007` não pode liberar mídia sem alguém a quem
  perguntar "este aluno pode?" (BA07, falha fechada).

### 4. Fases de evolução

Mantidas as cinco fases da visão, com o conteúdo de cada uma expresso em capacidades:

| Fase | Tema | Capacidades |
|---|---|---|
| MVP (Fase 1) | Vender e assistir | `CAP-001` `CAP-002` `CAP-003` `CAP-005` `CAP-006` `CAP-007` `CAP-008` `CAP-011` `CAP-017` `CAP-026` `CAP-030` |
| Fase 2 | Receita recorrente | `CAP-004` `CAP-009` `CAP-013` `CAP-014` `CAP-015` `CAP-016` `CAP-029` |
| Fase 3 | Aprendizagem comprovada | `CAP-018` `CAP-019` `CAP-020` `CAP-022` |
| Fase 4 | Retenção e comunidade | `CAP-023` `CAP-024` `CAP-025` `CAP-027` |
| Fase 5 | Escala e governança | `CAP-010` `CAP-012` `CAP-021` `CAP-028` `CAP-031` |

### 5. Restrições da referência arquitetural herdadas por todo PRD

O baseline não é negociável no nível de PRD. As restrições com efeito direto sobre escopo de capacidade:

- **G20** — PRD que confunde *direito de acesso* (comercial, `CAP-008`) com *liberação progressiva*
  (pedagógica, `CAP-018`) é recusado.
- **BA07/G11** — decisão de acesso é consulta síncrona ao dono com cache ≤30s e **falha fechada**.
  Nenhuma capacidade pode replicar direito de acesso localmente.
- **BA15/BA16/G24/G25/G26** — proteção de conteúdo é **dissuasão declarada, não garantia**. Nenhuma
  capacidade de venda ou comunicação pode prometer exclusividade; nenhuma capacidade pode introduzir
  restrição de sessão ou de reprodução sem decisão explícita com dado (AB04); IP não é sinal.
- **BA10/G07** — `tenant_id` em toda entidade desde o MVP, mesmo mono-tenant.
- **DE06** — falha fiscal (`CAP-016`) nunca trava venda nem acesso.
- **DE11/G12** — `CAP-028` consome e nunca é consultada por decisão operacional.
- **DE13/G13** — `CAP-030` é append-only e só escreve por consumo de evento.

---

### 6. Rastro de origem na visão

A visão numera 16 capacidades de negócio (`C01`…`C16`) como insumo do Domain Map. Cada `CAP-XXX`
declara de qual delas deriva, no próprio bloco da capacidade. O elo existe para que uma divergência
de fase entre os dois documentos seja **visível** — sem ele, a visão dizer Fase 2 e este backlog dizer
MVP só apareceria quando alguém tentasse implementar. Derivação de muitos para muitos é normal.

| Origem | Capacidade da visão | Fase na visão | Deriva em | Fase aqui |
|---|---|---|---|---|
| `C01` | Identidade e acesso | Fase 1 | `CAP-001` `CAP-002` | MVP |
| `C02` | Catálogo e oferta | Fase 1 (mínimo) · cupons Fase 2 | `CAP-003` · `CAP-004` | MVP · Fase 2 |
| `C03` | Autoria e conteúdo | Fase 1 (mínimo) | `CAP-005` | MVP |
| `C04` | Entrega de vídeo e proteção | Fase 1 | `CAP-006` `CAP-007` | MVP |
| `C05` | Aprendizagem e progresso | Fase 1 (progresso) · Fase 3 (liberação, anotações) | `CAP-017` · `CAP-018` `CAP-019` | MVP · Fase 3 |
| `C06` | Avaliação | Fase 3 | `CAP-020` · `CAP-021` | Fase 3 · **Fase 5** |
| `C07` | Certificação | Fase 3 | `CAP-022` | Fase 3 |
| `C08` | Vendas e checkout | Fase 1 (avulsa) · Fase 5 (afiliados, turmas) | `CAP-011` · `CAP-008` · `CAP-012` `CAP-010` | MVP · Fase 5 |
| `C09` | Cobrança e assinatura | Fase 2 | `CAP-013` `CAP-014` `CAP-015` `CAP-009` | Fase 2 |
| `C10` | Fiscal | Fase 2 | `CAP-016` | Fase 2 |
| `C11` | Comunidade | Fase 4 | `CAP-023` | Fase 4 |
| `C12` | Engajamento e gamificação | Fase 4 | `CAP-024` | Fase 4 |
| `C13` | Suporte | Fase 4 | `CAP-025` | Fase 4 |
| `C14` | Notificação | Fase 2 (e-mail) · Fase 4 (push, WhatsApp) | `CAP-026` · `CAP-027` | **MVP mínimo** · Fase 4 |
| `C15` | Analytics e BI | Fase 5 | `CAP-029` · `CAP-028` | **Fase 2** · Fase 5 |
| `C16` | Auditoria e governança | Fase 1 (mínimo) · Fase 5 (completo) | `CAP-030` · `CAP-031` | MVP mínimo · Fase 5 |

**Três divergências de fase, todas com risco e dono registrados** — nenhuma outra capacidade muda de
fase em relação à visão:

1. `CAP-026` (`C14`) **antecipada** da Fase 2 ao MVP em versão transacional mínima. R5 · Q1 · BA02.
   É a única divergência que muda o escopo do MVP.
2. `CAP-029` (`C15`) **antecipada** da Fase 5 à Fase 2, só na medição agregada. R9 · Q4 · contrapartida
   de BA16. Não muda o agrupamento de serviços: agrega evento que `CAP-007`/`CAP-017` já emitem.
3. `CAP-021` (`C06`) **adiada** da Fase 3 à Fase 5. R11. `C06` chega à Fase 3 pela correção automática
   de `CAP-020`, que é o que o certificado (`C07`) exige; a dissertativa depende de regra de prazo que
   o negócio ainda não definiu. Não muda o agrupamento: BA12 já declara Avaliação como extração futura.

A visão também nomeia **turmas/coortes** no roadmap da Fase 5 e nos modelos de monetização sem dar a
elas capacidade numerada; `CAP-010` deriva de `C08` (matrícula) e preserva essa fase.


## Definição do MVP

**Promessa do MVP:** um visitante encontra um curso na vitrine, entende o nível e o que está comprando,
paga, tem o acesso liberado automaticamente com a vigência correta, assiste a uma aula com o conteúdo
protegido e o progresso persistido — e a operação consegue publicar o curso, atender o caso e provar
quem fez o quê.

**Composição (11 capacidades):**

| # | Capacidade | Domínio | Por que é indispensável |
|---|---|---|---|
| `CAP-001` | Conta e autenticação do aluno | Identidade e Acesso | Sem ator, nenhum outro ciclo existe |
| `CAP-002` | Acesso interno por papel e permissão | Identidade e Acesso | Professor publica, operação atende, admin governa |
| `CAP-005` | Autoria e publicação de curso | Conteúdo e Currículo | Não se vende o que não existe |
| `CAP-006` | Ingestão e preparação de mídia protegida | Entrega de Mídia e Proteção | O ativo central do negócio precisa entrar na plataforma |
| `CAP-003` | Vitrine e oferta de curso | Catálogo e Oferta | É onde o direito de acesso é prometido e o nível é comunicado |
| `CAP-008` | Concessão e vigência de direito de acesso | Matrícula e Direito de Acesso | Decisão estrutural da visão; construída antes da venda |
| `CAP-011` | Compra avulsa de curso | Vendas e Checkout | Único modelo de monetização obrigatório no MVP |
| `CAP-007` | Reprodução protegida da aula | Entrega de Mídia e Proteção | É o produto sendo consumido |
| `CAP-017` | Percurso e progresso do aluno | Aprendizagem e Progresso | Sem progresso, assistir não vira aprendizado registrado |
| `CAP-026` | Notificação transacional por e-mail | Notificação | Divergência registrada (R5/Q1): `CAP-001` e `CAP-011` não fecham sem ela |
| `CAP-030` | Trilha de auditoria de atos administrativos | Auditoria e Conformidade | Versão mínima; precisa nascer junto do primeiro ato administrativo |

**Fora do MVP, por decisão explícita:** assinatura e qualquer recorrência, cupom e campanha, emissão
fiscal, avaliação, certificado, comunidade, gamificação, suporte por chamado, turma, afiliado, painel de
indicadores e qualquer restrição de reprodução simultânea.

**Critério de conclusão do MVP** (herdado da visão, expresso em capacidades): uma compra real percorre
`CAP-011` → `CAP-008` sem intervenção manual, com a vigência do contrato da compra respeitada; e um aluno
percorre `CAP-007` → `CAP-017` com marca d'água ativa, URL assinada e progresso persistido.

### Ordem de implementação sugerida

| Ordem | Capacidade | Nota de sequenciamento |
|---|---|---|
| 1 | `CAP-001` + `CAP-026` (mínimo) | Entregues juntas: recuperação de senha é parte do ciclo de conta |
| 2 | `CAP-002` + `CAP-030` (mínimo) | O primeiro ato administrativo já precisa de registro imutável |
| 3 | `CAP-006` | Mídia antes da aula que a referencia; aula sem mídia é casca |
| 4 | `CAP-005` | Depende do papel de professor de `CAP-002` e da mídia de `CAP-006` |
| 5 | `CAP-003` | Oferta sobre currículo publicado; é onde a vigência é prometida |
| 6 | `CAP-008` | **Antes** da venda, para não virar flag de checkout |
| 7 | `CAP-007` + `CAP-017` | Validáveis com concessão de **cortesia** de `CAP-008`, sem depender do gateway |
| 8 | `CAP-011` | Fecha o fluxo; é o único passo bloqueado por contratação externa |

A posição de `CAP-007`/`CAP-017` antes de `CAP-011` é uma escolha de risco: ela desacopla a validação da
experiência de aprendizagem da contratação do gateway de pagamento, que é a dependência externa de maior
risco do MVP. Se o gateway atrasar, a plataforma ainda pode ser demonstrada fim a fim com acesso de
cortesia.

---

## Ordem de Execução

Duas ordens distintas, que não devem ser confundidas: a de **detalhar domínios** (`tsg-flow-domain-creator`,
nível 4) e a de **atacar capacidades** (`tsg-flow-prd-creator` em diante, nível 5). O domínio é detalhado
**uma vez** e serve a várias capacidades; a capacidade é atacada uma vez e produz um PRD.

Regra de acoplamento entre as duas: **um domínio é detalhado imediatamente antes da primeira capacidade
que o usa como domínio dono** — nunca antes disso. Detalhar os 16 domínios de uma vez produziria 16
documentos que envelhecem antes de virar PRD, e a visão já registrou que documentação ambígua ou obsoleta
é a principal fonte de retrabalho num time que codifica por agentes.

### A. Ordem de detalhamento dos domínios

| Ordem | Domínio | Habilita | Detalhar antes do | Escopo do Domain Doc |
|---|---|---|---|---|
| D1 | Notificação | `CAP-026` | Passo 1 | **Parcial** — só o ciclo transacional e o consentimento; revisitar em D15 |
| D2 | Identidade e Acesso | `CAP-001` `CAP-002` | Passo 2 | Completo |
| D3 | Auditoria e Conformidade | `CAP-030` | Passo 3 | **Parcial** — só o registro append-only; revisitar em D18 |
| D4 | Entrega de Mídia e Proteção | `CAP-006` `CAP-007` | Passo 5 | Completo |
| D5 | Conteúdo e Currículo | `CAP-005` | Passo 6 | Completo |
| D6 | Catálogo e Oferta | `CAP-003` `CAP-004` | Passo 7 | Completo |
| D7 | Matrícula e Direito de Acesso | `CAP-008` `CAP-009` `CAP-010` | Passo 8 | Completo |
| D8 | Aprendizagem e Progresso | `CAP-017` `CAP-018` `CAP-019` | Passo 10 | Completo |
| D9 | Vendas e Checkout | `CAP-011` `CAP-012` | Passo 11 | Completo |
| D10 | Fiscal | `CAP-016` | Passo 12 | Completo |
| D11 | Cobrança e Assinatura | `CAP-013` `CAP-014` `CAP-015` | Passo 13 | Completo |
| D12 | Inteligência de Negócio | `CAP-029` `CAP-028` | Passo 18 | **Parcial** — só a medição agregada; a parte de indicadores depende de A1 |
| D13 | Avaliação | `CAP-020` `CAP-021` | Passo 19 | Completo |
| D14 | Certificação | `CAP-022` | Passo 21 | Completo |
| D15 | Notificação (revisão) | `CAP-027` | Passo 23 | Canais adicionais e preferência de contato |
| D16 | Atendimento e Suporte | `CAP-025` | Passo 24 | Completo |
| D17 | Comunidade e Engajamento | `CAP-023` `CAP-024` | Passo 25 | Completo |
| D18 | Auditoria e Conformidade (revisão) | `CAP-031` | Passo 28 | Solicitação do titular; depende de AB03 |

**Quatro observações sobre esta ordem:**

1. **D6 e D7 são detalhados como par, na mesma sessão de trabalho.** Catálogo *promete* a vigência do
   acesso e Matrícula a *concede*; são os dois lados do mesmo conceito, e é exatamente na costura entre
   eles que mora o risco DE01. Detalhar um sem o outro é como esse risco se materializa: a vigência vira
   atributo de oferta em um documento e flag de pedido no outro.
2. **D1 e D3 são detalhados em versão parcial e revisitados.** É a consequência aceita de antecipar
   `CAP-026` e `CAP-030` ao MVP em escopo mínimo. O Domain Doc parcial precisa declarar o que ficou de
   fora, senão a revisão vira reescrita.
3. **D10 (Fiscal) abre a Fase 2, não D11 (Cobrança).** Ver R13: a obrigação fiscal nasce com a primeira
   venda real do MVP, não com a primeira assinatura.
4. **D12 (Inteligência de Negócio) é puxado para a Fase 2** em versão parcial, só pela medição de
   `CAP-029`. O painel de indicadores continua na Fase 5, preso a A1.

### B. Ordem de ataque das capacidades

Sequência completa das 31 capacidades. A coluna **Lote** indica o que é entregue como bloco coerente —
capacidades do mesmo lote podem ser trabalhadas em paralelo por engenheiros diferentes; lotes distintos
são sequenciais. O racional de cada passo do MVP está em *Ordem de implementação sugerida*.

#### MVP (Fase 1)

| # | Lote | Capacidade | Só pode começar depois de | Prova que está pronta |
|---|---|---|---|---|
| 1 | L1 · Fundação | `CAP-026` (mínimo) | — | Um e-mail transacional é entregue e registrado |
| 2 | L1 · Fundação | `CAP-001` | `CAP-026` | Um aluno se cadastra, confirma, entra e recupera a senha sozinho |
| 3 | L1 · Fundação | `CAP-030` (mínimo) | `CAP-001` | Um ato administrativo aparece na trilha e não pode ser alterado |
| 4 | L1 · Fundação | `CAP-002` | `CAP-001`, `CAP-030` | Um professor entra no backoffice e não enxerga dado financeiro |
| 5 | L2 · O que se aprende | `CAP-006` | `CAP-002` | Um vídeo enviado fica pronto para consumo e não é acessível sem assinatura de URL |
| 6 | L2 · O que se aprende | `CAP-005` | `CAP-002` (estrutura), `CAP-006` (vínculo) | Um curso com módulos, aulas e mídia é publicado |
| 7 | L3 · O que se vende e o que se pode | `CAP-003` | `CAP-005` | Um visitante vê a oferta com nível, pré-requisito e vigência prometida |
| 8 | L3 · O que se vende e o que se pode | `CAP-008` | `CAP-003` | Uma concessão de cortesia dá acesso e expira sozinha no fim da vigência |
| 9 | L4 · Consumo | `CAP-007` | `CAP-006`, `CAP-008` | Um aluno com cortesia assiste com marca d'água; sem direito, não assiste |
| 10 | L4 · Consumo | `CAP-017` | `CAP-007`, `CAP-005` | O aluno fecha o navegador e volta exatamente onde parou |
| 11 | L5 · Receita | `CAP-011` | `CAP-003`, `CAP-008`, gateway contratado | Uma compra real libera o acesso sem intervenção manual |

**Ponto de corte do MVP:** ao fim do passo 11, o critério de conclusão da Fase 1 está satisfeito. Nenhuma
capacidade da Fase 2 começa antes disso.

#### Fase 2 — Receita recorrente

| # | Lote | Capacidade | Só pode começar depois de | Por que nesta posição |
|---|---|---|---|---|
| 12 | L6 · Legalidade | `CAP-016` | `CAP-011`, provedor de NFS-e contratado | A obrigação nasceu com a primeira venda do MVP (R13) |
| 13 | L7 · Recorrência | `CAP-013` | `CAP-011`, gateway com recorrência | Muda o modelo econômico; tudo em L8 reage a ela |
| 14 | L8 · Consequência financeira | `CAP-014` | `CAP-013` | Produz o fato "inadimplente" |
| 15 | L8 · Consequência financeira | `CAP-009` | `CAP-008`, `CAP-014` | Consome o fato e retira o acesso; par obrigatório com o 14 |
| 16 | L8 · Consequência financeira | `CAP-015` | `CAP-009`, `CAP-016` | Reembolso só fecha o ciclo se revogar acesso e cancelar nota |
| 17 | L9 · Alavanca comercial | `CAP-004` | `CAP-003`, `CAP-011` | Primeiro movimento de preço sem editar a oferta |
| 18 | L9 · Alavanca comercial | `CAP-029` | `CAP-017`, `CAP-007` | Agrega o sinal que o MVP já emite; insumo de AB04 |

#### Fase 3 — Aprendizagem comprovada

| # | Lote | Capacidade | Só pode começar depois de | Por que nesta posição |
|---|---|---|---|---|
| 19 | L10 · Medir | `CAP-020` | `CAP-017`, `CAP-005` | Pré-requisito de 20 e de 21; sem ela não há o que comprovar |
| 20 | L11 · Comprovar | `CAP-018` | `CAP-020` | A trilha passa a ser caminho, com a avaliação como porta |
| 21 | L11 · Comprovar | `CAP-022` | `CAP-017`, `CAP-020` | Fecha o segundo fluxo de valor da visão |
| 22 | L12 · Aprofundar | `CAP-019` | `CAP-017`, `CAP-007` | Independente das demais; pode ser adiada sem quebrar a fase |

#### Fase 4 — Retenção e comunidade

| # | Lote | Capacidade | Só pode começar depois de | Por que nesta posição |
|---|---|---|---|---|
| 23 | L13 · Alcance | `CAP-027` | `CAP-026`, WhatsApp aprovado | Aprovação de template tem prazo externo; iniciar cedo. Reforça a régua da Fase 2 retroativamente |
| 24 | L14 · Proteger a receita | `CAP-025` | `CAP-008`, `CAP-009`, `CAP-015` | Aluno sem acesso e sem canal pede chargeback |
| 25 | L15 · Pertencimento | `CAP-023` | `CAP-008`, `CAP-002` | Principal alavanca de conclusão em curso gravado |
| 26 | L15 · Pertencimento | `CAP-024` | `CAP-023`, `CAP-017`, `CAP-020` | Reage a eventos que só existem depois de 25 |

#### Fase 5 — Escala e governança

| # | Lote | Capacidade | Só pode começar depois de | Por que nesta posição |
|---|---|---|---|---|
| 27 | L16 · Decidir com dado | `CAP-028` | A1 resolvido | Nada além dela é decidível sem indicador |
| 28 | L17 · Conformidade | `CAP-031` | `CAP-030`, AB03 resolvido | Obrigação legal com sanção; precede o que é opcional |
| 29 | L18 · Novos formatos | `CAP-010` | `CAP-008`, `CAP-011`, regra de turma definida | Terceiro modelo de monetização |
| 30 | L18 · Novos formatos | `CAP-021` | `CAP-020`, `CAP-002` | Gatilho declarado para extrair Avaliação como serviço (BA12) |
| 31 | L18 · Novos formatos | `CAP-012` | `CAP-011`, `CAP-028`, regra de comissionamento definida | Depende de apuração, que só existe com 27 |

### C. Critério para avançar de fase

Nenhuma fase começa por calendário. Cada uma abre quando a anterior satisfaz o critério de conclusão da
visão, expresso aqui em capacidades:

| Passar para | Exige | Verificação |
|---|---|---|
| Fase 2 | Passos 1–11 concluídos | Compra real fim a fim + aula assistida com progresso persistido |
| Fase 3 | Passos 12–18 concluídos | Assinatura renova sozinha, falha dispara régua e suspende acesso, venda gera NFS-e |
| Fase 4 | Passos 19–22 concluídos | Trilha condicionada a avaliação + certificado validado por um terceiro |
| Fase 5 | Passos 23–26 concluídos | Dúvida pedagógica e chamado correm separados; engajamento é mensurável |

**Limite de trabalho em progresso:** no máximo **um lote aberto por vez**. A visão registrou a revisão
humana como o gargalo escasso da operação — dois lotes abertos produzem entregas acumuladas esperando
aprovação, que é o risco de "revisão humana virar gargalo" se materializando.

---

## Capacidades por Domínio

### Domínio: Identidade e Acesso

#### CAP-001 — Conta e autenticação do aluno

- **Objetivo:** permitir que uma pessoa crie conta, confirme identidade, entre, saia e recupere acesso à
  plataforma com sessão segura.
- **Valor de negócio:** é a porta de entrada de todo o resto. Sem ela não há comprador, aluno, progresso
  nem marca d'água — e o atrito aqui é perda direta de conversão na vitrine.
- **Resumo do fluxo:** visitante se cadastra → confirma e-mail → autentica → obtém sessão → encerra ou
  recupera acesso quando esquece a senha.
- **Dependências:** `CAP-026` (confirmação e recuperação por e-mail).
- **Origem na visão:** `C01`
- **Prioridade:** Alta · **Fase:** MVP
- **Restrições herdadas:** sessão opaca no BFF, token nunca no browser (BA06); CSRF em toda escrita (G18);
  nenhum limite de dispositivos ou sessões simultâneas (BA16/G24).

#### CAP-002 — Acesso interno por papel e permissão

- **Objetivo:** permitir que a escola conceda, restrinja e revogue acesso do pessoal interno ao backoffice,
  com menor privilégio por papel.
- **Valor de negócio:** é o que torna a operação delegável sem expor dado financeiro ao professor nem dado
  de conteúdo ao suporte. Também é pré-condição de qualquer capacidade de backoffice.
- **Resumo do fluxo:** administrador convida um ator interno → o convidado ativa a conta → recebe papel →
  exerce apenas as permissões do papel → tem acesso revogado quando sai.
- **Dependências:** `CAP-001` (mecanismo de conta), `CAP-030` (registro do ato).
- **Origem na visão:** `C01`
- **Prioridade:** Alta · **Fase:** MVP
- **Restrições herdadas:** RBAC por claim, nunca string solta; o serviço dono autoriza o fino, o BFF não é
  fronteira de confiança.

### Domínio: Catálogo e Oferta

#### CAP-003 — Vitrine e oferta de curso

- **Objetivo:** expor publicamente o que está à venda, com preço, nível, pré-requisito declarado e o
  direito de acesso prometido, e levar o visitante à intenção de compra.
- **Valor de negócio:** é a única superfície que converte visitante em comprador. Como a plataforma
  decidiu **não travar** compra por pré-requisito, a vitrine carrega sozinha a responsabilidade de orientar
  a escolha — orientar mal custa reembolso e churn, não só conversão.
- **Resumo do fluxo:** negócio cria a oferta sobre um curso publicado → define preço e vigência do acesso
  → publica na vitrine → visitante navega, filtra por nível, lê pré-requisito e escolhe.
- **Dependências:** `CAP-005` (currículo publicado a referenciar).
- **Origem na visão:** `C02` (vitrine e oferta, em versão mínima com nível)
- **Prioridade:** Alta · **Fase:** MVP
- **Restrições herdadas:** pré-requisito e nível são informativos e nunca condicionam compra (DE04);
  nenhuma peça de oferta pode prometer exclusividade de conteúdo (BA15).

#### CAP-004 — Campanha de compra e cupom

- **Objetivo:** alterar temporariamente preço e/ou direito de acesso de uma oferta e conceder desconto
  nominal ou percentual na compra.
- **Valor de negócio:** é o instrumento comercial de aquisição — promoção sazonal, parceria, recuperação de
  carrinho. Sem ele, todo movimento de preço é edição permanente da oferta.
- **Resumo do fluxo:** negócio cria campanha ou cupom com regra e validade → o aluno aplica no checkout →
  o pedido congela a condição efetivamente aplicada.
- **Dependências:** `CAP-003`, `CAP-011`.
- **Origem na visão:** `C02` (cupons) e `C08` (cupons no checkout)
- **Prioridade:** Média · **Fase:** Fase 2
- **Restrições herdadas:** campanha pode alterar a vigência prometida, e `CAP-008` precisa honrar isso sem
  caso especial (DE01).

### Domínio: Conteúdo e Currículo

#### CAP-005 — Autoria e publicação de curso

- **Objetivo:** permitir que o professor estruture um curso em módulos e aulas, anexe material
  complementar e publique uma versão consumível.
- **Valor de negócio:** o conteúdo é o ativo central do negócio. Esta capacidade é o que transforma vídeo
  gravado em produto vendável e consumível, e é pré-condição de vitrine, reprodução e progresso.
- **Resumo do fluxo:** professor cria curso → organiza módulos e aulas na ordem pedagógica → vincula mídia
  e materiais → publica uma versão → revisa e republica depois.
- **Dependências:** `CAP-002` (papel de professor), `CAP-006` (mídia a vincular).
- **Origem na visão:** `C03`
- **Prioridade:** Alta · **Fase:** MVP
- **Restrições herdadas:** não define preço nem condição comercial (DE02); versão de publicação não pode
  invalidar progresso já registrado de aluno.

### Domínio: Entrega de Mídia e Proteção

#### CAP-006 — Ingestão e preparação de mídia protegida

- **Objetivo:** receber o arquivo de vídeo, prepará-lo para entrega segmentada e cifrada, e deixá-lo pronto
  para ser vinculado a uma aula.
- **Valor de negócio:** é o que coloca o ativo do negócio dentro da plataforma sob controle próprio — o
  oposto direto da dependência de plataforma de terceiro que motivou o projeto.
- **Resumo do fluxo:** professor envia o vídeo → a plataforma prepara a versão de reprodução e a protege →
  o ativo fica disponível para vínculo em `CAP-005` → o status da preparação é visível a quem enviou.
- **Dependências:** `CAP-002`.
- **Origem na visão:** `C04`
- **Prioridade:** Alta · **Fase:** MVP
- **Restrições herdadas:** nenhum objeto de vídeo público (G21); nenhum artefato derivado por aluno (G22);
  o provedor de CDN não vaza para outro domínio.

#### CAP-007 — Reprodução protegida da aula

- **Objetivo:** entregar a aula ao aluno autorizado, com proteção e identificação, e informar o avanço da
  reprodução.
- **Valor de negócio:** é o produto sendo consumido. A qualidade percebida da escola está aqui, e é também
  onde mora toda a estratégia de proteção que substituiu o DRM não contratado.
- **Resumo do fluxo:** aluno abre a aula → a plataforma consulta se ele pode acessar agora → abre sessão de
  reprodução individual → entrega por URL assinada de vida curta com marca d'água do e-mail do aluno
  sobreposta → informa o avanço a `CAP-017`.
- **Dependências:** `CAP-006`, `CAP-008` (decisão de acesso), `CAP-001` (identidade para a marca d'água).
- **Origem na visão:** `C04`
- **Prioridade:** Alta · **Fase:** MVP
- **Restrições herdadas:** falha fechada quando a decisão de acesso não responde (BA07); marca d'água
  renderizada no cliente (G22); e-mail fora de log, URL, chave de cache e routing key (G23); nenhuma trava
  de concorrência (BA16/G24). A proteção é **dissuasão declarada**, e nenhum PRD pode apresentá-la como
  garantia de exclusividade.

### Domínio: Matrícula e Direito de Acesso

#### CAP-008 — Concessão e vigência de direito de acesso

- **Objetivo:** conceder, manter e expirar o direito de um aluno sobre um conteúdo, e responder a qualquer
  momento se ele pode acessá-lo agora.
- **Valor de negócio:** é o que a escola realmente vende — não o vídeo, mas o direito de assisti-lo por uma
  vigência. A visão marcou isso como decisão estrutural e registrou o risco de virar flag de checkout; esta
  capacidade existe para que isso não aconteça.
- **Resumo do fluxo:** um fato de origem (compra concluída, cortesia administrativa) gera uma concessão com
  origem e vigência → o direito é consultado a cada acesso → expira sozinho no fim da vigência, ou é
  vitalício conforme o contrato da compra.
- **Dependências:** `CAP-003` (vigência prometida pela oferta).
- **Origem na visão:** `C08` (matrícula); a visão registra que o direito de acesso **atravessa**
  `C02`, `C08` e `C09` e é decisão estrutural, não detalhe de checkout
- **Prioridade:** Alta · **Fase:** MVP
- **Restrições herdadas:** fonte de verdade única, sem réplica local (G11); cache ≤30s e falha fechada
  (BA07); concessão de cortesia gera ato auditável (`CAP-030`). Não decide liberação de próxima aula (G20).

#### CAP-009 — Suspensão e revogação de acesso

- **Objetivo:** interromper temporariamente o acesso por causa financeira e encerrá-lo definitivamente por
  reembolso ou decisão administrativa, com efeito imediato e reversível quando cabível.
- **Valor de negócio:** é o que dá consequência à inadimplência e ao reembolso. Sem ela, a recorrência é
  uma promessa sem poder de cobrança, e o reembolso devolve dinheiro sem retirar o produto.
- **Resumo do fluxo:** `CAP-014` ou `CAP-015` informa o fato financeiro → a concessão é suspensa ou
  revogada → o efeito vale em segundos na decisão de acesso → a regularização restaura o que foi suspenso.
- **Dependências:** `CAP-008`, `CAP-014`, `CAP-015`.
- **Origem na visão:** `C09` (bloqueio de acesso)
- **Prioridade:** Alta · **Fase:** Fase 2
- **Restrições herdadas:** quem bloqueia é este domínio, nunca Cobrança diretamente; todo ato
  administrativo de revogação é auditado com autor e motivo.

#### CAP-010 — Turma (coorte)

- **Objetivo:** agrupar matrículas em uma turma com data de início e acompanhamento comum.
- **Valor de negócio:** habilita um modelo de venda com maior ticket e maior conclusão, previsto na visão
  como terceiro modelo de monetização.
- **Resumo do fluxo:** negócio cria a turma sobre uma oferta → alunos matriculados recebem concessão
  vinculada à turma → a turma inicia na data → o acompanhamento é feito por turma.
- **Dependências:** `CAP-008`, `CAP-003`, `CAP-011`.
- **Origem na visão:** `C08` (matrícula/coorte). A visão nomeia turmas/coortes no roadmap da Fase 5
  e nos modelos de monetização, sem capacidade numerada própria
- **Prioridade:** Baixa · **Fase:** Fase 5
- **Restrições herdadas:** hoje é agrupamento de matrículas (DE08); só vira domínio próprio se ganhar
  cronograma e encontro ao vivo — que são **non-goal** da versão atual.

### Domínio: Vendas e Checkout

#### CAP-011 — Compra avulsa de curso

- **Objetivo:** levar a intenção de compra até um pedido pago, com preço e condição congelados, e
  disparar a liberação do acesso.
- **Valor de negócio:** é a primeira receita da escola e o único modelo obrigatório no MVP. É também o
  passo que prova, fim a fim, que a plataforma própria substitui o intermediário.
- **Resumo do fluxo:** aluno escolhe a oferta → identifica-se → paga por cartão, PIX ou boleto → o pedido
  congela o que foi comprado → a confirmação do pagamento libera o acesso automaticamente e notifica.
- **Dependências:** `CAP-003`, `CAP-001`, `CAP-008`, `CAP-026`; externa: gateway de pagamento contratado.
- **Origem na visão:** `C08` (venda avulsa)
- **Prioridade:** Alta · **Fase:** MVP
- **Restrições herdadas:** dado de cartão não trafega nem é persistido; escrita idempotente (G09); pedido
  pendente (boleto/PIX não pago) é estado legítimo e não concede acesso.

#### CAP-012 — Indicação de afiliado

- **Objetivo:** atribuir uma venda à indicação que a originou e sustentar a remuneração do parceiro.
- **Valor de negócio:** abre um canal de aquisição de custo variável, pago só por resultado.
- **Resumo do fluxo:** parceiro recebe link identificado → o visitante chega por ele → a compra registra a
  atribuição → o parceiro consulta o que gerou.
- **Dependências:** `CAP-011`, `CAP-028` (apuração).
- **Origem na visão:** `C08` (afiliados)
- **Prioridade:** Baixa · **Fase:** Fase 5
- **Restrições herdadas:** a regra de comissionamento, repasse e extrato não existe ainda; enquanto não
  existir, afiliado permanece alojado em Vendas (DE09).

### Domínio: Cobrança e Assinatura

#### CAP-013 — Assinatura recorrente

- **Objetivo:** cobrar periodicamente e renovar sozinho o compromisso que sustenta um acesso continuado.
- **Valor de negócio:** é a mudança do modelo econômico da escola — de receita por venda para receita
  previsível, que é o que sustenta a operação mês a mês.
- **Resumo do fluxo:** aluno assina → o ciclo é cobrado na data → o sucesso renova a vigência do acesso →
  o cancelamento encerra a renovação sem retirar o que já foi pago.
- **Dependências:** `CAP-011`, `CAP-008`, `CAP-026`.
- **Origem na visão:** `C09`
- **Prioridade:** Alta · **Fase:** Fase 2
- **Restrições herdadas:** camada anticorrupção obrigatória sobre o gateway; webhook validado por
  assinatura e idempotente.

#### CAP-014 — Inadimplência e régua de cobrança

- **Objetivo:** tratar a falha de pagamento com tentativas e avisos programados antes de decidir pelo
  bloqueio.
- **Valor de negócio:** é o que evita perder receita por falha técnica de cartão — a maior causa de churn
  involuntário em assinatura. Recuperar aqui é mais barato que vender de novo.
- **Resumo do fluxo:** a cobrança falha → novas tentativas são programadas → a régua avisa o aluno pelos
  canais consentidos → o prazo esgota → o fato é informado a `CAP-009`, que suspende.
- **Dependências:** `CAP-013`, `CAP-026`, `CAP-009`.
- **Origem na visão:** `C09` (inadimplência)
- **Prioridade:** Alta · **Fase:** Fase 2
- **Restrições herdadas:** Cobrança comunica o fato financeiro e **não** bloqueia acesso diretamente.

#### CAP-015 — Reembolso

- **Objetivo:** devolver valor a um aluno e encerrar o direito adquirido, com registro de quem decidiu e
  por quê.
- **Valor de negócio:** é obrigação legal e de reputação, e é a contrapartida de vender sem gate de
  pré-requisito — quem compra fora do nível certo precisa ter saída. Sem ele, todo caso vira atrito manual.
- **Resumo do fluxo:** operação ou financeiro decide o reembolso → o valor é estornado no gateway → o
  direito é revogado → o documento fiscal é cancelado → o aluno é notificado.
- **Dependências:** `CAP-009`, `CAP-016`, `CAP-026`, `CAP-030`.
- **Origem na visão:** `C09` (reembolso)
- **Prioridade:** Média · **Fase:** Fase 2
- **Restrições herdadas:** ato administrativo com efeito sobre dinheiro e acesso — auditoria obrigatória
  com autor, momento, alvo e motivo.

### Domínio: Fiscal

#### CAP-016 — Emissão de NFS-e

- **Objetivo:** transformar o fato gerador em documento fiscal válido no município e disponibilizá-lo ao
  aluno e ao financeiro.
- **Valor de negócio:** é condição de legalidade da operação brasileira. Não emitir não é atraso de
  feature, é impedimento de operar.
- **Resumo do fluxo:** o pagamento é confirmado → o fato gerador é registrado → a nota é solicitada ao
  provedor municipal → o documento fica disponível → o reembolso gera o cancelamento correspondente.
- **Dependências:** `CAP-013`/`CAP-011` (fato gerador), `CAP-026`; externa: provedor de NFS-e contratado.
- **Origem na visão:** `C10`
- **Prioridade:** Alta · **Fase:** Fase 2
- **Restrições herdadas:** **falha fiscal nunca impede venda nem acesso** (DE06); regra municipal não vaza
  para nenhum outro domínio (camada anticorrupção obrigatória).

### Domínio: Aprendizagem e Progresso

#### CAP-017 — Percurso e progresso do aluno

- **Objetivo:** registrar o que o aluno já consumiu e onde parou, e devolver isso como uma área do aluno
  que permite retomar o estudo.
- **Valor de negócio:** progresso é o que transforma vídeo assistido em sensação de avanço — o principal
  motor de conclusão e, portanto, de renovação e de recomendação. É também o insumo de certificação,
  gamificação e de todo indicador de engajamento.
- **Resumo do fluxo:** aluno entra na área do aluno → vê os cursos a que tem direito e o quanto avançou →
  abre a aula onde parou → o avanço da reprodução é registrado → a aula é concluída → o curso é concluído.
- **Dependências:** `CAP-005` (currículo), `CAP-008` (direito), `CAP-007` (avanço da reprodução).
- **Origem na visão:** `C05` (progresso)
- **Prioridade:** Alta · **Fase:** MVP
- **Restrições herdadas:** domínio write-heavy — gravação com granularidade controlada, nunca um registro
  por segundo assistido; não decide direito de acesso (G20).

#### CAP-018 — Liberação progressiva na trilha

- **Objetivo:** condicionar a abertura da próxima etapa à conclusão da anterior ou à aprovação em avaliação.
- **Valor de negócio:** é o que faz a trilha ser um caminho e não um catálogo de vídeos — aumenta conclusão
  e dá sentido pedagógico à avaliação.
- **Resumo do fluxo:** professor define a regra de liberação no currículo → o aluno conclui a etapa → a
  próxima abre → etapa condicionada a avaliação consulta o resultado antes de abrir.
- **Dependências:** `CAP-017`, `CAP-005`, `CAP-020`.
- **Origem na visão:** `C05` (liberação progressiva)
- **Prioridade:** Média · **Fase:** Fase 3
- **Restrições herdadas:** aplica-se **dentro** de um curso, nunca entre cursos (DE05); confundi-la com
  direito de acesso é motivo de recusa de PRD (G20).

#### CAP-019 — Anotação pessoal ancorada na aula

- **Objetivo:** permitir que o aluno registre notas privadas vinculadas ao minuto exato da aula e as
  reencontre depois.
- **Valor de negócio:** aumenta o valor percebido do acesso continuado — a plataforma passa a guardar o
  estudo do aluno, não só o vídeo. É também um motivo concreto para voltar.
- **Resumo do fluxo:** aluno anota durante a aula → a nota guarda o ponto do vídeo → ele revê suas notas do
  curso → clica e volta ao trecho.
- **Dependências:** `CAP-017`, `CAP-007`.
- **Origem na visão:** `C05` (anotações)
- **Prioridade:** Baixa · **Fase:** Fase 3
- **Restrições herdadas:** conteúdo pessoal do aluno — sujeito à solicitação do titular (`CAP-031`).

### Domínio: Avaliação

#### CAP-020 — Quiz com correção automática

- **Objetivo:** aplicar avaliações objetivas, apurar o resultado sem intervenção humana e declarar
  aprovação segundo um critério definido.
- **Valor de negócio:** é o que torna o aprendizado verificável e o que dá lastro ao certificado. Sem ela,
  concluir significa apenas ter assistido.
- **Resumo do fluxo:** professor monta a avaliação e o critério de aprovação → o aluno submete uma
  tentativa → o resultado é apurado na hora → a aprovação é publicada para liberação e certificação.
- **Dependências:** `CAP-005`, `CAP-008`, `CAP-017`.
- **Origem na visão:** `C06` (correção automática)
- **Prioridade:** Alta · **Fase:** Fase 3
- **Restrições herdadas:** vive como módulo de `learning` (BA12); limite de tentativas é regra de negócio,
  não detalhe de tela.

#### CAP-021 — Correção dissertativa por professor

- **Objetivo:** permitir avaliação aberta corrigida por um professor, com nota, devolutiva e prazo.
- **Valor de negócio:** viabiliza cursos avançados, onde o que se mede não cabe em múltipla escolha — e é
  o que diferencia a escola de um repositório de vídeos.
- **Resumo do fluxo:** aluno submete a resposta → entra na fila do professor → o professor corrige e dá
  devolutiva → o resultado publica a aprovação.
- **Dependências:** `CAP-020`, `CAP-002`, `CAP-026`.
- **Origem na visão:** `C06`. **Divergência de fase:** a visão põe `C06` inteiro na Fase 3; aqui só
  a correção automática (`CAP-020`) fica na Fase 3 e a dissertativa vai para a Fase 5, porque
  depende de fluxo de professor e de regra de prazo ainda não definida pelo negócio (R11) e não é
  pré-requisito do certificado
- **Prioridade:** Baixa · **Fase:** Fase 5
- **Restrições herdadas:** é o gatilho declarado para extrair Avaliação como serviço próprio (BA12);
  alteração de nota é ato administrativo auditável.

### Domínio: Certificação

#### CAP-022 — Certificado validável de conclusão

- **Objetivo:** emitir o comprovante de conclusão e permitir que qualquer terceiro verifique sua
  autenticidade sem ter conta na plataforma.
- **Valor de negócio:** é o que o aluno leva para o mercado — o argumento final de venda de um curso
  avançado. Um certificado que ninguém consegue verificar não tem valor para quem contrata.
- **Resumo do fluxo:** o aluno conclui o curso e é aprovado → o certificado é emitido com código único →
  o aluno recebe e compartilha → um empregador consulta o código publicamente e vê a validade.
- **Dependências:** `CAP-017`, `CAP-020`, `CAP-026`.
- **Origem na visão:** `C07`
- **Prioridade:** Alta · **Fase:** Fase 3
- **Restrições herdadas:** única superfície pública sem sessão do sistema — rota anônima com rate limit e
  resposta mínima (nome, curso, data, status); imutabilidade do certificado emitido.

### Domínio: Comunidade e Engajamento

#### CAP-023 — Dúvida pedagógica e fórum moderado

- **Objetivo:** permitir que o aluno pergunte sobre o conteúdo, seja respondido pelo professor ou por
  outros alunos, e que a operação modere o que é publicado.
- **Valor de negócio:** é a principal alavanca de conclusão em curso gravado — aluno travado sem resposta
  abandona. Também reduz custo por aluno, porque a resposta fica pública para os próximos.
- **Resumo do fluxo:** aluno abre a dúvida na sala da aula → a comunidade ou o professor responde → o
  autor da dúvida é notificado → conteúdo impróprio é moderado, com registro.
- **Dependências:** `CAP-008` (liberar a sala), `CAP-026`, `CAP-030`, `CAP-002`.
- **Origem na visão:** `C11`
- **Prioridade:** Alta · **Fase:** Fase 4
- **Restrições herdadas:** dúvida pedagógica é distinta de chamado de suporte (`CAP-025`) — exigência
  explícita da visão, com atores e permissões diferentes.

#### CAP-024 — Reconhecimento por pontos e badges

- **Objetivo:** reconhecer marcos de progresso e de participação com pontos, insígnias e posição relativa.
- **Valor de negócio:** aumenta permanência e conclusão a custo baixo, reagindo a eventos que já existem.
- **Resumo do fluxo:** o aluno conclui aula, conclui curso, é aprovado ou ajuda alguém → o marco concede
  ponto ou badge → o aluno vê seu reconhecimento e sua posição.
- **Dependências:** `CAP-017`, `CAP-020`, `CAP-023`.
- **Origem na visão:** `C12`
- **Prioridade:** Média · **Fase:** Fase 4
- **Restrições herdadas:** reage a eventos, não é dona do progresso (DE07); vira domínio próprio só se
  ganhar campanha, temporada ou recompensa com valor econômico.

### Domínio: Atendimento e Suporte

#### CAP-025 — Central de ajuda e chamado de suporte

- **Objetivo:** resolver o problema do aluno com a plataforma — acesso, cobrança ou falha — em um fluxo
  rastreável, com autoatendimento antes do chamado.
- **Valor de negócio:** protege a receita já conquistada: aluno sem acesso e sem canal pede reembolso ou
  faz chargeback. O autoatendimento é o que mantém o custo de suporte descolado do número de alunos.
- **Resumo do fluxo:** aluno busca no artigo de ajuda → não resolve → abre chamado → o suporte diagnostica
  consultando direito de acesso e cobrança → solicita a ação ao domínio dono → responde e encerra.
- **Dependências:** `CAP-008`, `CAP-009`, `CAP-015`, `CAP-026`, `CAP-030`, `CAP-002`.
- **Origem na visão:** `C13`
- **Prioridade:** Alta · **Fase:** Fase 4
- **Restrições herdadas:** o suporte **não** altera acesso nem executa reembolso por conta própria —
  solicita aos donos, que registram; menor privilégio (suporte não vê dado financeiro completo).

### Domínio: Notificação

#### CAP-026 — Notificação transacional por e-mail

- **Objetivo:** entregar ao aluno, por e-mail, as mensagens que o fluxo de negócio exige, respeitando
  consentimento e descadastro.
- **Valor de negócio:** sem ela, a conta não se confirma, a senha não se recupera e a compra não gera
  comprovação — três rupturas no ciclo mínimo. É também o único ponto do sistema que fala com o aluno fora
  da plataforma e o guardião do consentimento LGPD.
- **Resumo do fluxo:** um domínio pede o envio de uma mensagem → o consentimento e a preferência são
  verificados → a mensagem é entregue pelo canal → a entrega (ou a falha) é registrada.
- **Dependências:** externa: provedor de e-mail transacional.
- **Origem na visão:** `C14` (e-mail). **Divergência de fase:** a visão põe `C14` na Fase 2;
  antecipada ao MVP em versão transacional mínima — decisão registrada em R5/Q1 e refletida no
  agrupamento inicial do baseline (BA02)
- **Prioridade:** Alta · **Fase:** **MVP em versão mínima** (divergência registrada — a visão a coloca na
  Fase 2; ver R5 e a origem acima)
- **Escopo mínimo no MVP:** e-mail transacional apenas (confirmação de conta, recuperação de senha,
  confirmação de compra e liberação de acesso). **Fora:** push, WhatsApp, preferência por tipo de
  mensagem, modelo editável pelo negócio — tudo isso é `CAP-027`.
- **Restrições herdadas:** dona única do consentimento (DE12); nenhum domínio monta mensagem de canal.

#### CAP-027 — Canais adicionais e preferência de contato

- **Objetivo:** alcançar o aluno por push e WhatsApp, e deixá-lo escolher por onde quer ser avisado de
  cada tipo de assunto.
- **Valor de negócio:** WhatsApp é o canal de maior abertura no mercado brasileiro — é o que faz a régua de
  cobrança recuperar pagamento e o aviso de nova aula trazer o aluno de volta.
- **Resumo do fluxo:** o aluno define suas preferências e consente por canal → o pedido de envio escolhe o
  canal adequado → a mensagem é entregue → a falha em um canal permite fallback.
- **Dependências:** `CAP-026`; externa: provedor oficial de WhatsApp Business API.
- **Origem na visão:** `C14` (push e WhatsApp)
- **Prioridade:** Média · **Fase:** Fase 4
- **Restrições herdadas:** aprovação de template é detalhe interno de Notificação; a plataforma notifica,
  **não faz campanha de marketing** (non-goal da visão).

### Domínio: Inteligência de Negócio

#### CAP-028 — Painel de indicadores do negócio

- **Objetivo:** consolidar receita, churn, engajamento e desempenho de cursos em indicadores estáveis que
  sustentem decisão.
- **Valor de negócio:** é o que permite ao administrador decidir o que produzir, o que despriorizar e onde
  a escola está perdendo aluno — hoje isso mora na plataforma de terceiro.
- **Resumo do fluxo:** os eventos de negócio são consumidos → viram indicadores com definição acordada →
  o administrador consulta por recorte e por período.
- **Dependências:** praticamente todas as capacidades produtoras de evento; bloqueada por **A1** (definição
  das métricas de sucesso e de "aluno ativo").
- **Origem na visão:** `C15`
- **Prioridade:** Alta · **Fase:** Fase 5
- **Restrições herdadas:** consome e nunca é consultada por decisão operacional (DE11/G12); não é dona de
  nenhum dado; não substitui ferramenta de BI de mercado.

#### CAP-029 — Medição de indício de compartilhamento de conta

- **Objetivo:** medir, de forma agregada, a sobreposição sustentada de **aulas distintas** por aluno, para
  decidir com dado se alguma restrição se justifica.
- **Valor de negócio:** é a contrapartida da decisão de não restringir sessão nem reprodução (BA16). Sem
  essa medição, a decisão de restringir (ou de nunca restringir) seria tomada por suposição sobre uma base
  que hoje tem zero aluno — e uma restrição errada cobra o preço do aluno pagante, no meio da aula.
- **Resumo do fluxo:** o avanço de reprodução já coletado por `CAP-017` é consultado → a sobreposição de
  aulas distintas por aluno é agregada → o negócio observa a evolução → decide a ação (nenhuma, aviso,
  contato ou restrição) em **AB04**.
- **Dependências:** `CAP-017`, `CAP-007`.
- **Origem na visão:** `C15`. **Divergência de fase:** a visão põe `C15` na Fase 5; a medição
  agregada é antecipada à Fase 2 como contrapartida da decisão de não restringir sessão nem
  reprodução (BA16) — sem dado, restringir ou não restringir seria suposição (R9)
- **Prioridade:** Média · **Fase:** Fase 2
- **Restrições herdadas:** **agregado primeiro** — caso individual só com finalidade, base legal e retenção
  registradas (G25); IP não é sinal (G26); nenhuma restrição sem decisão explícita com dado (G24).
  **Nota de sequenciamento:** o sinal bruto deve ser emitido desde a Fase 1 por exigência do baseline —
  isso é restrição herdada por `CAP-007`/`CAP-017`, não escopo desta capacidade.

### Domínio: Auditoria e Conformidade

#### CAP-030 — Trilha de auditoria de atos administrativos

- **Objetivo:** registrar de forma imutável quem fez o quê no backoffice, com autor, momento, alvo e motivo,
  e permitir consulta por quem governa.
- **Valor de negócio:** é o que torna a delegação de poder operacional segura — reembolso, concessão de
  cortesia, alteração de nota e banimento passam a ter responsável identificável. Sem isso, ampliar a
  equipe de operação amplia risco sem controle.
- **Resumo do fluxo:** um ator interno pratica um ato com efeito sobre aluno ou dinheiro → o fato é
  registrado fora do alcance de quem o praticou → o administrador consulta o histórico.
- **Dependências:** `CAP-002`; consome eventos de todas as capacidades com ato administrativo.
- **Origem na visão:** `C16` (log de ato administrativo, versão mínima)
- **Prioridade:** Alta · **Fase:** **MVP em versão mínima**
- **Escopo mínimo no MVP:** registro append-only dos atos existentes no MVP (gestão de acesso interno,
  publicação de curso, concessão de cortesia) e leitura restrita ao backoffice.
- **Restrições herdadas:** append-only, sem `UPDATE` nem `DELETE`, escrita só por consumo de evento
  (DE13/G13); serviço próprio desde a Fase 1 (BA03) — auditoria dentro do domínio auditado não é auditoria.

#### CAP-031 — Atendimento a solicitação do titular (LGPD)

- **Objetivo:** receber e cumprir pedidos de acesso, correção e exclusão ou anonimização de dados pessoais,
  coordenando os domínios donos.
- **Valor de negócio:** é obrigação legal com sanção, e a resposta precisa ser rastreável e tempestiva.
  Concentrar o atendimento em um ponto é o que impede que um pedido seja cumprido em oito lugares e
  esquecido no nono.
- **Resumo do fluxo:** o titular solicita → a solicitação é registrada com base legal e prazo → cada
  domínio dono executa a parte que lhe cabe → o resultado é devolvido ao titular e registrado.
- **Dependências:** `CAP-030`, `CAP-026`; bloqueada por **AB03** (política de retenção e anonimização).
- **Origem na visão:** `C16` (governança, escopo completo)
- **Prioridade:** Média · **Fase:** Fase 5
- **Restrições herdadas:** nenhum serviço decide sozinho apagar dado que sustenta obrigação fiscal;
  exclusão é coordenada, não propagada.

---

## Dependências entre Capacidades

### Grafo de dependências

| Capacidade | Depende de | Dependência externa |
|---|---|---|
| `CAP-001` | `CAP-026` | Provedor de e-mail |
| `CAP-002` | `CAP-001`, `CAP-030` | — |
| `CAP-003` | `CAP-005` | Identidade visual (A2) |
| `CAP-004` | `CAP-003`, `CAP-011` | — |
| `CAP-005` | `CAP-002`, `CAP-006` | — |
| `CAP-006` | `CAP-002` | AWS S3 + CloudFront |
| `CAP-007` | `CAP-006`, `CAP-008`, `CAP-001` | AWS S3 + CloudFront |
| `CAP-008` | `CAP-003` | — |
| `CAP-009` | `CAP-008`, `CAP-014`, `CAP-015` | — |
| `CAP-010` | `CAP-008`, `CAP-003`, `CAP-011` | — |
| `CAP-011` | `CAP-003`, `CAP-001`, `CAP-008`, `CAP-026` | **Gateway de pagamento** |
| `CAP-012` | `CAP-011`, `CAP-028` | — |
| `CAP-013` | `CAP-011`, `CAP-008`, `CAP-026` | **Gateway (recorrência)** |
| `CAP-014` | `CAP-013`, `CAP-026`, `CAP-009` | Gateway |
| `CAP-015` | `CAP-009`, `CAP-016`, `CAP-026`, `CAP-030` | Gateway |
| `CAP-016` | `CAP-011`/`CAP-013`, `CAP-026` | **Provedor de NFS-e** |
| `CAP-017` | `CAP-005`, `CAP-008`, `CAP-007` | — |
| `CAP-018` | `CAP-017`, `CAP-005`, `CAP-020` | — |
| `CAP-019` | `CAP-017`, `CAP-007` | — |
| `CAP-020` | `CAP-005`, `CAP-008`, `CAP-017` | — |
| `CAP-021` | `CAP-020`, `CAP-002`, `CAP-026` | — |
| `CAP-022` | `CAP-017`, `CAP-020`, `CAP-026` | — |
| `CAP-023` | `CAP-008`, `CAP-026`, `CAP-030`, `CAP-002` | — |
| `CAP-024` | `CAP-017`, `CAP-020`, `CAP-023` | — |
| `CAP-025` | `CAP-008`, `CAP-009`, `CAP-015`, `CAP-026`, `CAP-030`, `CAP-002` | — |
| `CAP-026` | — | **Provedor de e-mail transacional** |
| `CAP-027` | `CAP-026` | **WhatsApp Business API** |
| `CAP-028` | Todas as produtoras de evento | Bloqueada por A1 |
| `CAP-029` | `CAP-017`, `CAP-007` | Bloqueada por AB04 (ação a tomar) |
| `CAP-030` | `CAP-001` (ator interno identificável) | — |
| `CAP-031` | `CAP-030`, `CAP-026` | Bloqueada por AB03 |

### Capacidades mais dependidas (nós críticos)

1. **`CAP-008` — Concessão e vigência de direito de acesso**: 7 capacidades dependem dela. Espelha a
   leitura do Domain Map (Matrícula é o nó mais consultado) e a ressalva BA04 do baseline. **É a capacidade
   de maior custo de erro do produto inteiro.**
2. **`CAP-026` — Notificação transacional**: 11 capacidades dependem dela. Justifica a antecipação ao MVP.
3. **`CAP-017` — Percurso e progresso**: 6 capacidades dependem dela (certificação, gamificação,
   liberação, anotação, medição de compartilhamento).
4. **`CAP-001`/`CAP-002` — Identidade**: transversais por natureza; toda capacidade tem ator.

### Ausência de ciclos

Não há ciclo forte. Os dois pares bidirecionais herdados do Domain Map permanecem assimétricos:
`CAP-007` ↔ `CAP-017` (um pergunta o direito, o outro recebe o fato do avanço) e `CAP-018` ↔ `CAP-020`
(um consulta a aprovação, o outro a declara). Ambos respeitam o princípio BA09: quem decide pergunta,
quem mudou publica.

Dois **ciclos aparentes** foram resolvidos por direção de dependência, não por ordem de entrega:

- `CAP-002` ↔ `CAP-030`: Acesso interno *publica* o ato; Auditoria apenas *consome*. `CAP-030` não depende
  de `CAP-002` para existir — só do conceito de ator interno identificável (`CAP-001`). Por isso ela é
  construída no passo 3, antes de `CAP-002`, e nunca o contrário (DE13/G13).
- `CAP-009` ↔ `CAP-015`: o reembolso *produz o fato*; a revogação *o consome*. `CAP-009` entra no passo 15
  reagindo ao fato de inadimplência de `CAP-014`, e `CAP-015` no passo 16 já a encontra pronta.

---

## Riscos Estratégicos

| # | Risco | Prob. | Impacto | Mitigação |
|---|---|---|---|---|
| R1 | **11 capacidades no MVP para 2 engenheiros.** Mesmo cada uma sendo fatia fina, o MVP é o maior bloco do roadmap e não admite corte adicional sem quebrar a promessa "vender e assistir" | Alta | Alto | Ordem de implementação com valor verificável a cada passo; nenhuma capacidade posterior entra antes do critério de conclusão do MVP; limitar trabalho em progresso a uma capacidade por vez |
| R2 | **`CAP-008` construída depois de `CAP-011`** e o direito de acesso vira flag de pedido — o risco DE01 que a visão e o baseline registraram em três lugares | Média | Alto | Sequenciamento invertido deliberadamente (passo 6 antes do 8); G20 como gate de recusa de PRD; `CAP-008` nasce com origem, vigência e casos de uso próprios, e `CAP-011` só publica fato |
| R3 | **Gateway de pagamento não contratado bloqueia `CAP-011`** e, com ele, o critério de conclusão do MVP | Média | Alto | Contratar antes do passo 8; `CAP-007` e `CAP-017` posicionados antes da venda, validáveis com concessão de cortesia — o produto é demonstrável fim a fim mesmo sem o gateway |
| R4 | **Identidade visual (A2) atrasa e bloqueia `CAP-003`** ir a público — a vitrine é a única capacidade do MVP com dependência de design | Média | Médio | Tratar a vitrine como capacidade funcional completa sobre layout neutro; a troca de identidade visual é entrega do time de design, não retrabalho de capacidade |
| R5 | **Notificação fora do MVP** (como a visão a posiciona) deixa `CAP-001` sem recuperação de senha e `CAP-011` sem comprovação de compra — um MVP que não fecha o próprio ciclo | Alta | Alto | Antecipar `CAP-026` em versão transacional mínima. **Divergência da visão; exige confirmação do negócio.** Alternativa (pior): operar recuperação de senha por atendimento manual no lançamento |
| R6 | **Métricas de sucesso indefinidas (A1)** — o MVP pode ser entregue sem que se possa afirmar se funcionou, e `CAP-028` fica sem definição de indicador | Alta | Médio | A1 tem dono (negócio) e prazo: resolver antes de encerrar o MVP, não antes de começá-lo |
| R7 | **Fase 2 concentra o maior bloco de risco do roadmap**: recorrência, inadimplência, reembolso e fiscal, com duas dependências externas novas e a maior complexidade de estado no tempo do sistema | Alta | Alto | `CAP-013`→`CAP-014`→`CAP-009` implementadas nessa ordem, cada uma com fato de negócio verificável; `CAP-016` isolada fora do caminho crítico (DE06) desde o primeiro commit |
| R8 | **Proteção de conteúdo apresentada como garantia** em peça de venda, PRD ou TechSpec, quando a decisão registrada é dissuasão sem DRM | Média | Alto | Restrição declarada em `CAP-003`, `CAP-006` e `CAP-007`; limite honesto registrado no baseline; nenhuma comunicação de venda pode prometer exclusividade |
| R9 | **`CAP-029` nunca ser priorizada**, deixando a decisão de restringir (ou não) sem dado — a contrapartida de BA16 fica sem cumprir | Média | Médio | O sinal bruto é exigência de observabilidade herdada por `CAP-007`/`CAP-017` desde a Fase 1; a capacidade apenas agrega o que já existirá. Antes de materializar caso individual, AB04 precisa definir a ação (G25) |
| R10 | **Custo de storage e CDN crescer mais que a receita**, tornando `CAP-006`/`CAP-007` economicamente inviáveis na escala | Média | Alto | Custo por aluno ativo é sinal obrigatório desde o MVP; `media` isolado permite trocar provedor ou estratégia de entrega sem tocar em regra de negócio |
| R11 | **Capacidades da Fase 5 (`CAP-010`, `CAP-012`, `CAP-021`) nunca terem regra de negócio definida** — turma sem cronograma, afiliado sem comissionamento, correção dissertativa sem prazo | Média | Baixo | Mantidas como Baixa prioridade e fora de qualquer caminho crítico; cada uma exige definição de regra pelo negócio antes de virar PRD |
| R13 | **O MVP processa venda real sem emitir NFS-e** — `CAP-016` está na Fase 2 por decisão da visão, mas a obrigação fiscal nasce com a primeira nota, não com a primeira assinatura | Alta | Alto | `CAP-016` é o **passo 12**, primeiro da Fase 2, antes da recorrência. Se o MVP for a produção com venda a público antes disso, a emissão precisa ser resolvida por processo manual assumido e datado — não por omissão. **Exige decisão do negócio (Q5)** |
| R12 | **Vender sem gate de pré-requisito gerar reembolso por escolha errada de nível** — consequência direta de uma decisão de produto deliberada | Média | Médio | `CAP-003` carrega a responsabilidade de orientar bem (nível e pré-requisito visíveis e explicados); `CAP-015` precisa existir na Fase 2 como saída legítima, não como exceção operacional |

### Decisões que este backlog pede ao negócio

| # | Pergunta | Bloqueia |
|---|---|---|
| Q1 | Confirmar a antecipação de `CAP-026` (e-mail transacional mínimo) ao MVP | Fechamento do escopo do MVP (R5) |
| Q2 | Confirmar que o sequenciamento das Fases 2 a 5 corresponde à prioridade do negócio (hipótese H2 da visão) | Planejamento além do MVP |
| Q3 | Definir as métricas de sucesso e "aluno ativo" (A1) | Encerramento do MVP e `CAP-028` |
| Q4 | Definir a ação a tomar diante de indício de compartilhamento (AB04) | Materialização de caso individual em `CAP-029` (G25) |
| Q5 | Decidir como a obrigação de NFS-e é cumprida entre a primeira venda real e o passo 12 (R13) | Ir a público com venda real no MVP |

---

*Backlog gerado com o agente `tsg-flow-capability-backlog`. Próximo passo sugerido:
`tsg-flow-domain-creator` para o primeiro domínio do MVP (Identidade e Acesso), ou
`tsg-flow-prd-creator` para a primeira capacidade da ordem de implementação (`CAP-001`).*

---

## Histórico de Revisões

| Versão | Data | Autor | Alterações |
|---|---|---|---|
| 1.0 | 2026-09-20 | Tasso Gomes | Backlog inicial: 31 capacidades em 16 domínios, 11 no MVP. Em revisão — não integrado |
| 1.3 | 2026-09-20 | Tasso Gomes | Revisitado contra `architecture-baseline.md` v1.2 (correção de BA15). Nenhuma capacidade, fase, dependência ou ordem foi alterada: a citação a BA15 neste backlog já descrevia a proteção sem trava de concorrência |
| 1.2 | 2026-09-20 | Tasso Gomes | **Aprovado e integrado.** Acrescentado o rastro de origem na visão (`CAP-XXX ← CNN`) nas 31 capacidades e a tabela de reconciliação `C01`…`C16` → `CAP-XXX` com as três divergências de fase (R5, R9, R11). Nenhuma capacidade, dependência, fase ou ordem foi alterada |
| 1.1 | 2026-09-20 | Tasso Gomes | Revalidado contra `architecture-baseline.md` v1.1: a divergência de Notificação deixa de ser proposta e passa a decisão registrada, e a citação a BA02 acompanha o agrupamento inicial de 6 serviços. Nenhuma capacidade, dependência ou fase foi alterada |
