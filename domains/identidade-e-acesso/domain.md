---
tsg_artifact: domain
product: code-4-coders
version: 1.1
status: approved
updated: 2026-09-21
sources: vision.md@1.2, context/domain-map.md@1.2, backlog/capabilities.md@1.4
---

# Domain Document — Identidade e Acesso

> Detalha o bounded context de **um** domínio do Domain Map. Não decide prioridade, ordem nem
> escopo de entrega — isso é do backlog de capacidades e do PRD. Forneça este arquivo junto com o
> `vision.md` ao iniciar um PRD de capacidade que toque este domínio.

**Domínio:** Identidade e Acesso
**Capacidades atendidas:** `CAP-001`, `CAP-002`
**Restrições arquiteturais pertinentes:** BA05, BA06, BA07, BA16 · G04, G07, G10, G18, G23, G24, G26

---

## 1. Propósito do Domínio (Domain Purpose)

### Responsabilidade Principal

Estabelecer **quem é o ator** e **o que ele está autorizado a fazer** na plataforma, para alunos e
para pessoal interno.

### Problema que Resolve

Sem este domínio não existe ator, e sem ator nenhum outro ciclo de valor existe: não há comprador,
aluno, progresso, marca d'água nem ato administrativo atribuível. Ele resolve duas dores que mudam
por razões diferentes e por isso vivem juntas aqui e em lugar nenhum mais:

1. **Provar identidade** — uma pessoa cria conta, confirma que o e-mail é dela, entra, sai e
   recupera o acesso sozinha quando esquece a senha, sem depender de atendimento humano.
2. **Delegar operação com menor privilégio** — a escola concede, restringe e revoga acesso do
   pessoal interno ao backoffice, de modo que o professor não enxergue dado financeiro e o suporte
   não enxergue dado de autoria.

A justificativa do Domain Map para a fronteira: autenticação e papel interno mudam por razão de
**segurança e organização**, enquanto direito de acesso a conteúdo muda por razão **comercial**.
Misturados, uma mudança de política de suporte tocaria a regra de acesso do aluno pagante.

### Fora do Escopo deste Domínio (Out of Scope)

Herdado do campo "O que não faz" do Domain Map, com o destino de cada item:

- **Decidir se um aluno pode assistir a um curso** → Matrícula e Direito de Acesso. Ser aluno não é
  ter acesso; o direito depende de compra e vigência, não de identidade. Esta é a fronteira que a
  visão e o Domain Map registraram como estrutural — ver risco RF-01.
- **Registrar o histórico de atos administrativos** → Auditoria e Conformidade. Este domínio
  *pratica* atos administrativos (conceder e revogar papel) e publica o fato; quem registra de forma
  imutável é `audit`, deliberadamente fora do alcance de quem pratica o ato (BA03 · DE13).
- **Guardar preferência ou consentimento de comunicação** → Notificação. Este domínio pede o envio
  de confirmação de conta e de recuperação de senha; **não decide se pode enviar**.
- **Liberação progressiva dentro de um curso** → Aprendizagem e Progresso. É regra pedagógica, não
  autorização. Confundir as duas é recusa de PRD por G20.
- **Perfil público, bio, avatar e reputação do aluno** → Comunidade e Engajamento, quando existir.
  Aqui mora só o que identifica e autoriza.
- **Emissão de documento fiscal ou qualquer dado de cobrança do titular** → Fiscal e Cobrança.
- **Execução de exclusão ou anonimização de dado pessoal** → coordenada por `CAP-031` (LGPD).
  Este domínio executa a parte que lhe cabe quando solicitado, mas não decide sozinho apagar dado
  que sustenta obrigação fiscal.

---

## 2. Usuários do Domínio (Domain Users)

| Perfil (Role) | O que faz neste domínio | Frequência de uso |
|---|---|---|
| Visitante | Cria conta e confirma o e-mail | Uma vez por conta |
| Aluno | Autentica, encerra sessão, recupera e troca a senha | Diária (autenticação) · rara (recuperação) |
| Professor | Autentica no backoffice e exerce as permissões do papel | Diária |
| Suporte | Autentica no backoffice; diagnostica acesso sem alterar papel alheio | Diária |
| Financeiro | Autentica no backoffice e exerce as permissões do papel | Diária |
| Administrador | Convida ator interno, concede, altera e revoga papel | Rara, mas de alto impacto |

A visão define autenticação **própria da plataforma, sem SSO corporativo**, com RBAC para os papéis
internos (suporte, financeiro, professor, admin).

---

## 3. Entidades Principais (Core Entities)

> Vocabulário de negócio, não schema. As cinco primeiras vêm do Domain Map; **Credencial** e
> **Token de Verificação** refinam Conta e não criam fronteira nova.

| Entidade | Descrição | Atributos Principais | Relacionamentos |
|---|---|---|---|
| Conta | A pessoa reconhecida pela plataforma, aluno ou ator interno | e-mail, nome, situação (não confirmada / ativa / desativada), data de criação | possui: Credencial, Sessão, Papel |
| Credencial | O segredo que prova a posse da conta | segredo verificável (nunca legível), data da última troca | pertence a: Conta |
| Sessão | Uma permanência autenticada, encerrável a qualquer momento | início, última atividade, expiração, origem, situação | pertence a: Conta |
| Papel | Função exercida na plataforma que agrupa permissões | nome (aluno, professor, suporte, financeiro, administrador), escopo | agrupa: Permissão · atribuído a: Conta |
| Permissão | Autorização para executar uma ação específica no backoffice | ação, recurso | pertence a: Papel |
| Convite de Acesso Interno | O único caminho pelo qual um ator interno passa a existir | e-mail convidado, papel ofertado, quem convidou, validade, situação | origina: Conta (ator interno) · concede: Papel |
| Token de Verificação | Prova de uso único e vida curta para confirmar e-mail ou redefinir senha | finalidade, validade, situação (emitido / usado / expirado) | pertence a: Conta |

---

## 4. Capacidades Atendidas (Capabilities Served)

> Só referência. Prioridade, fase, dependência entre capacidades e ordem de implementação vivem em
> `backlog/capabilities.md`.

| Capacidade | O que este domínio entrega a ela |
|---|---|
| `CAP-001` | O ciclo completo da conta do aluno: cadastro, confirmação de e-mail, autenticação, sessão, encerramento e recuperação de acesso |
| `CAP-002` | O convite, a ativação, a concessão de papel, o exercício de permissão com menor privilégio e a revogação de acesso do pessoal interno |

Ambas derivam de `C01` na visão.

---

## 5. Juntas com Outros Domínios (Domain Joints)

> Herdadas da tabela de interações do Domain Map.

### Depende de (Upstream)

| Domínio | O que consome | Tipo | Dono do dado | Criticidade |
|---|---|---|---|---|
| Notificação | Entrega da confirmação de conta e da recuperação de senha ao e-mail do aluno | Mensagem assíncrona | Identidade e Acesso (conteúdo) / Notificação (entrega e consentimento) | **Alta** — sem ela `CAP-001` não fecha o ciclo |

Esta é a única dependência upstream do domínio, e é a razão pela qual `CAP-026` foi antecipada ao
MVP (R5 · Q1 · OD1). O Domain Map declara a divisão: **o domínio de origem é dono do conteúdo da
mensagem; Notificação é dona da entrega e do consentimento.**

**Mecanismo decidido (QA-01, 2026-09-20).** Notificação é **carteiro, não redatora**:

- Este domínio publica `notificacao.envio-solicitado` pelo outbox (G06), com destinatário,
  **identificador do Modelo de Mensagem**, os **dados que o preenchem** (nome, link, validade) e a
  **finalidade declarada**. A mensagem é **endereçada** a Notificação, não difundida.
- Notificação verifica consentimento, entrega pelo canal, repete em falha e registra o resultado.
  **Não conhece o conceito de "confirmação de conta" nem de "recuperação de senha".**
- `identidade.conta-criada` segue existindo como fato de domínio para Matrícula e Inteligência de
  Negócio, mas **não é ele que dispara o e-mail**. Fosse ele, o Token de Verificação viajaria dentro
  de um fato difundido, legível por todo assinante — inclusive `analytics`.
- **O texto mora em Notificação, não aqui.** O Modelo de Mensagem é entidade de Notificação no
  Domain Map, e o backlog herda a restrição "nenhum domínio monta mensagem de canal" — consequência
  de DE12. Este domínio dita **o quê** e **quando**; Notificação dita **como aquilo se parece** e
  cuida de remetente, rodapé, descadastro e reentrega. O que `CAP-027` acrescenta depois é o negócio
  **editar** o modelo, não a propriedade dele mudar de dono.
- O ganho desta divisão não é teórico: fosse a origem a compor assunto e corpo, marca, rodapé legal
  e descadastro estariam espalhados por seis serviços, que é exatamente o que DE12 existe para
  impedir.

### Fornece para (Downstream)

| Domínio | O que fornece | Tipo | Dono do dado | Criticidade |
|---|---|---|---|---|
| Todos os domínios | Quem é o ator corrente e quais permissões ele carrega | Síncrono (no caminho quente) | Este domínio | **Alta** — é a junta mais usada do sistema |
| Entrega de Mídia e Proteção | O aluno corrente, para compor a marca d'água dinâmica | Síncrono | Este domínio | Alta |
| Matrícula e Direito de Acesso | O fato de que uma conta passou a existir | Evento | Este domínio | Média |
| Notificação | O fato de que uma conta passou a existir | Evento | Este domínio | Média |
| Inteligência de Negócio | Eventos de conta para consolidar indicadores | Evento | Este domínio | Baixa |
| Auditoria e Conformidade | O fato de um ato administrativo de acesso ter sido praticado | Evento | Auditoria (registro) / este domínio (fato) | **Alta** — `CAP-030` depende disto |

**Nota sobre a junta transversal.** `identity` é o único emissor do JWT interno; os demais serviços
validam localmente via JWKS, **sem round-trip** (BA06). Isso mantém a junta "todos → identidade"
dentro do limite de um salto síncrono (BA08) sem transformar `identity` em gargalo do caminho
quente. A **autorização fina é decidida pelo serviço dono do recurso** a partir das claims; o BFF
não é fronteira de confiança (BA05).

### Integrações Externas (External Integrations)

| Sistema Externo | Finalidade | Direção |
|---|---|---|
| Provedor de e-mail transacional | Entregar confirmação de conta e recuperação de senha — **acessado por Notificação, nunca por este domínio** | Saída (indireta) |

Não há SSO, não há provedor de identidade externo e não há federação nesta versão (decisão da visão).

---

## 6. Regras de Negócio (Business Rules)

| ID | Regra | Origem |
|---|---|---|
| RN-01 | O e-mail identifica a Conta unicamente, normalizado em minúsculas e sem espaços nas bordas. Não existem duas contas não desativadas com o mesmo e-mail | Política de produto |
| RN-02 | Conta recém-criada nasce **não confirmada** e não autentica antes da confirmação do e-mail | `CAP-001` |
| RN-03 | Token de Verificação é de **uso único** e tem validade curta. Expirado, é reemitido sem criar nova conta e sem invalidar a conta existente | Segurança |
| RN-04 | A recuperação de senha **nunca revela se o e-mail existe**: a resposta ao solicitante é idêntica para e-mail cadastrado e não cadastrado | Segurança (enumeração de contas) |
| RN-05 | A autenticação com credencial inválida **não distingue** e-mail inexistente de senha errada na resposta | Segurança (enumeração de contas) |
| RN-06 | O segredo da Credencial é guardado apenas em forma não reversível, com algoritmo de derivação lenta. Nunca é exibido, registrado, transportado em claro nem recuperável — só redefinível | Segurança |
| RN-07 | Redefinir ou trocar a senha **encerra todas as demais sessões ativas** da conta e invalida todo Token de Verificação de recuperação ainda pendente | Segurança |
| RN-08 | A Sessão tem expiração por inatividade deslizante e é encerrada por logout explícito, por expiração ou por revogação. **A revogação tem efeito imediato** | BA06 |
| RN-09 | **Nenhum limite de sessões simultâneas, de dispositivos ou de concorrência é aplicado.** A plataforma observa e não age; qualquer restrição exige decisão explícita com dado que a justifique | BA16 · G24 |
| RN-10 | O endereço IP não é usado como sinal de autorização, de sessão nem de detecção de compartilhamento | G26 |
| RN-11 | Toda escrita autenticada por cookie de sessão exige proteção CSRF. Cookie sem CSRF é vulnerabilidade, não simplificação | G18 |
| RN-12 | Permissão só é concedida **através de um Papel**. Nenhuma permissão é atribuída diretamente a uma Conta | `CAP-002` |
| RN-13 | **Conta de aluno e conta de ator interno são contas distintas.** Uma conta não acumula os dois lados: ou ela é de aluno, ou é de ator interno. Papéis internos acumulam **entre si** (o mesmo ator pode ser professor e suporte) | QA-03 |
| RN-13a | Uma pessoa que é aluno **e** ator interno mantém **duas contas, com endereços de e-mail distintos** — consequência direta de RN-01, que faz o e-mail identificar a conta unicamente. Na prática: endereço institucional para a conta interna, pessoal para a de aluno | QA-03 · RN-01 |
| RN-13b | Quando essa pessoa compra e assiste a um curso, ela o faz **pela conta de aluno**: o direito de acesso, o progresso e a marca d'água seguem essa conta, nunca a interna | QA-03 · BA15 |
| RN-14 | **Não há auto-cadastro de ator interno.** Um ator interno só passa a existir por Convite de Acesso Interno, e no MVP **apenas o administrador** pode emiti-lo. Delegar a permissão a outro papel é mudança posterior deliberada: delegar depois é fácil, recolher é difícil | `CAP-002` · QA-04 |
| RN-15 | O Convite é de uso único, vinculado ao e-mail convidado e tem validade. Expirado, exige novo convite — não é reativável | `CAP-002` |
| RN-16 | **Menor privilégio por papel:** o professor não acessa dado financeiro e o suporte não acessa dado de autoria. Um papel interno só enxerga o que o seu escopo declara | `CAP-002` · visão |
| RN-17 | A revogação de papel tem efeito na **próxima decisão de autorização** e encerra as sessões do ator interno afetado | BA07 |
| RN-18 | Papel e permissão trafegam como **claim**, nunca como string solta comparada no consumidor | Baseline |
| RN-19 | Emitir convite, aceitar convite, conceder papel, alterar papel e revogar papel são **atos administrativos**: este domínio publica o fato com autor e motivo; o registro imutável é de Auditoria e Conformidade | `CAP-030` · DE13 |
| RN-20 | Um ator não concede, altera nem revoga o próprio papel | Segregação de função |
| RN-21 | O e-mail do aluno não aparece em log, span, métrica, URL, nome de objeto, chave de cache nem routing key. Ele **é** entregue ao cliente do player para compor a marca d'água — a exposição é a exceção declarada, não a regra | G10 · G23 |
| RN-22 | Este domínio responde "quem é o ator e o que ele pode fazer". **Nunca** responde "este aluno pode assistir a este curso agora" | Domain Map · G20 |
| RN-23 | Desativar uma conta preserva a identidade para fins de auditoria e de obrigação fiscal. Exclusão e anonimização são coordenadas por `CAP-031`, nunca executadas unilateralmente aqui | LGPD · `CAP-031` |
| RN-24 | Toda entidade deste domínio carrega `tenant_id` desde a Fase 1, mesmo em operação mono-tenant | BA10 · G07 |
| RN-25 | O **primeiro administrador** nasce por provisionamento da Fase 0 — seed único, datado e executado pelo time, fora do fluxo de convite. Todo administrador seguinte nasce por Convite de Acesso Interno (RN-14). O seed é anterior a `CAP-030` e, por isso, **não aparece na trilha de auditoria**: é lacuna conhecida e aceita, não omissão | QA-05 · `CAP-002` |
| RN-26 | O e-mail do aluno aparece no payload do pedido de envio a Notificação — é o destinatário, e não há entrega sem ele. Esta é a **segunda e última exceção declarada** a RN-21, junto com a marca d'água. Fora dessas duas, mascaramento vale em todo lugar | G23 · QA-01 |
| RN-27 | Este domínio **não pede envio sem finalidade declarada**. Toda solicitação a Notificação carrega a finalidade, que é o que permite a ela aplicar consentimento sem conhecer o conteúdo | DE12 · QA-01 |
| RN-28 | Este domínio **não monta mensagem de canal**. Pede o envio indicando o Modelo de Mensagem e fornecendo os dados que o preenchem; assunto, corpo, remetente e rodapé são de Notificação | Domain Map (Modelo de Mensagem) · DE12 |

---

## 7. Eventos do Domínio (Domain Events)

> O contrato real materializa no pacote de contratos e na TechSpec; aqui fica o fato de negócio.
> Publicação sempre por outbox, nunca direto do caso de uso (G06).

### Produz (Publishes)

- `identidade.conta-criada` — uma pessoa passou a existir na plataforma (ainda não confirmada)
- `identidade.conta-confirmada` — o e-mail foi comprovado e a conta passou a autenticar
- `identidade.senha-redefinida` — o segredo da conta foi trocado por recuperação ou por escolha
- `identidade.conta-desativada` — a conta deixou de autenticar, sem deixar de existir
- `identidade.convite-interno-emitido` — ato administrativo: alguém foi convidado à operação
- `identidade.convite-interno-aceito` — o convidado ativou a conta e assumiu o papel
- `identidade.papel-concedido` — ato administrativo, com autor e motivo
- `identidade.papel-revogado` — ato administrativo, com autor e motivo
- `notificacao.envio-solicitado` — pedido de entrega endereçado a Notificação. **O contrato é de
  Notificação, não deste domínio** (por isso o prefixo): quem define o que um pedido de envio aceita
  é quem entrega. Este domínio é apenas um dos remetentes, e informa modelo e dados, nunca texto

Os quatro últimos são o que `CAP-030` consome para compor a trilha de auditoria; os três primeiros
interessam a Notificação, Matrícula e Inteligência de Negócio.

### Consome (Subscribes)

Nenhum evento é consumido por este domínio no horizonte de `CAP-001` e `CAP-002`. É consequência da
fronteira, não omissão: identidade é um domínio **transversal a montante** — todos perguntam a ele,
ele não depende do fato consumado de ninguém.

---

## 8. Riscos de Fronteira (Boundary Risks)

| ID | Risco | Probabilidade | Impacto | Mitigação |
|---|---|---|---|---|
| RF-01 | **"Autenticado" ser confundido com "tem direito de acesso"** e a decisão de acesso ao conteúdo vazar para cá — o mesmo risco que a visão e o baseline registraram em três lugares | Alta | Alto | RN-22 + Fora do Escopo explícito; a decisão de acesso é consulta síncrona ao dono com cache de até 30s e falha fechada (BA07), e só `Entitlement` expõe o caso de uso de decisão (G11) |
| RF-02 | **Papel interno virar catálogo de telas em vez de agrupamento de permissões** — o backoffice cresce e cada tela nova inventa um papel | Média | Médio | RN-12 e RN-18: permissão é o átomo, papel é agrupamento, claim é o transporte |
| RF-03 | **`identity` virar gargalo do caminho quente** se cada serviço perguntar quem é o ator a cada requisição | Média | Alto | BA06: JWT interno de vida curta validado localmente via JWKS, sem round-trip; a junta transversal não consome salto síncrono |
| RF-04 | **O e-mail do aluno vazar por um caminho legítimo** — ele é dado pessoal que, por decisão de proteção de conteúdo, precisa chegar ao cliente do player | Média | Alto | RN-21 delimita a única exposição permitida; todo o resto é mascarado (G10 · G23) |
| RF-05 | **Consentimento de comunicação migrar para cá** porque a confirmação de conta e a recuperação de senha nascem aqui | Média | Médio | Fora do Escopo explícito: Notificação é dona única do consentimento (DE12); este domínio fornece conteúdo, não decide envio |
| RF-06 | **Restrição de sessão simultânea entrar por engano em um PRD** — a proteção de conteúdo é tema recorrente e a trava parece barata | Média | Alto | RN-09 e G24, confirmados pelo time em 2026-09-20: a medição é observatória e a restrição só se justifica com dado, para não cobrar do aluno pagante o erro. BA15 foi alinhada a essa decisão no baseline v1.2; OD8 permanece como registro histórico |

---

## 9. Questões em Aberto (Open Questions)

- [x] **QA-01 — Mecanismo da junta com Notificação. Fechada em 2026-09-20.** Notificação assina a
      própria fila de pedidos de envio, não o fato de domínio, e entrega sem conhecer o conteúdo.
      Detalhe em §5; regras em RN-26, RN-27 e RN-28. Descartado: Notificação assinar `identidade.conta-criada`
      e montar a mensagem — faria o Token de Verificação viajar num fato difundido e obrigaria
      Notificação a conhecer as regras de identidade.
- [x] **QA-02 — Sessões simultâneas. Confirmada em 2026-09-20.** A medição é observatória, para
      indicar desvio e decidir depois com dado; a linha foi escolhida para não prejudicar a
      experiência do aluno pagante. RN-09 e RF-06 já refletem isso. O baseline v1.2 corrigiu BA15
      para ficar alinhado a BA16/G24; **OD8** permanece no `flow-state.json` como decisão registrada,
      não como pendência deste documento.
- [x] **QA-03 — Conta única para quem acumula papéis. Fechada em 2026-09-20: contas separadas.**
      Formalizada em RN-13, RN-13a e RN-13b. A consequência que decorre de RN-01 e precisa ser dita:
      como o e-mail identifica a conta unicamente, **as duas contas exigem endereços distintos**.
      Não é preciosismo de modelagem — com o mesmo endereço em duas contas, "recuperar a senha deste
      e-mail" deixaria de ter resposta única, e RF-03 do PRD de `CAP-026` perderia sentido.
      Descartado: conta única com papel base `aluno` + papel interno.
- [x] **QA-04 — Quem pode convidar ator interno. Fechada em 2026-09-20.** Exclusiva do administrador
      no MVP; delegação a outro papel é mudança posterior deliberada. Formalizada em RN-14.
- [x] **QA-05 — Primeiro administrador. Fechada em 2026-09-20.** Seed único e datado, executado pelo
      time na Fase 0, fora do fluxo de convite. Formalizada em RN-25, com a lacuna de auditoria
      declarada.

---

## Histórico de Revisões

| Versão | Data | Autor | Alterações |
|---|---|---|---|
| 1.1 | 2026-09-21 | Tasso Gomes | Revalidado contra as origens v1.2/v1.4 e contra o baseline v1.2; nenhuma fronteira ou regra do domínio foi alterada |

*Domain Doc gerado com a skill `tsg-flow-domain-creator`. Para criar o PRD de uma capacidade que
toca este domínio, use `tsg-flow-prd-creator` fornecendo o `vision.md`, este arquivo, os demais
domain docs que a capacidade atravessa e o ID da capacidade.*
