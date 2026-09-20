---
tsg_artifact: domain
product: code-4-coders
version: 1.0
status: draft
updated: 2026-09-20
sources: vision.md@1.1, context/domain-map.md@1.1, backlog/capabilities.md@1.2
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
mensagem; Notificação é dona da entrega e do consentimento.** O *mecanismo* da junta (se Notificação
assina o fato `identidade.conta-criada` ou se recebe um pedido explícito de envio) é decisão de
contrato e está em QA-01.

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
| RN-13 | Toda Conta ativa carrega ao menos o papel `aluno`. Papéis internos são **adicionais** e nunca substituem o papel base | Política de produto |
| RN-14 | **Não há auto-cadastro de ator interno.** Um ator interno só passa a existir por Convite de Acesso Interno emitido por quem tem a permissão de conceder acesso interno | `CAP-002` |
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
| RF-06 | **Restrição de sessão simultânea entrar por engano em um PRD** — a proteção de conteúdo é tema recorrente e a trava parece barata | Média | Alto | RN-09 e G24; e QA-02, que aponta uma contradição ainda aberta no próprio baseline |

---

## 9. Questões em Aberto (Open Questions)

- [ ] **QA-01 — Mecanismo da junta com Notificação.** O Domain Map declara a divisão de
      responsabilidade (origem é dona do conteúdo, Notificação é dona da entrega e do consentimento),
      mas não o mecanismo: Notificação **assina** `identidade.conta-criada` e monta a mensagem, ou
      recebe de `identity` um **pedido explícito de envio** com o conteúdo pronto? A diferença decide
      quem conhece o texto da mensagem e quem conhece o token de confirmação. **Precisa estar fechada
      antes do PRD de `CAP-026`**, que é quem materializa o contrato.
- [ ] **QA-02 — Contradição dentro do baseline sobre sessões simultâneas.** BA15 descreve a pilha de
      proteção como "URL assinada + HLS AES-128 + marca d'água + **limite de sessões simultâneas**",
      enquanto BA16 rejeita explicitamente sessão única e lease de reprodução, e G24 proíbe qualquer
      restrição de concorrência sem decisão com dado. As duas não podem valer juntas, e o mecanismo
      moraria neste domínio. **A decisão pertence ao `architecture-baseline.md`**, não a este
      documento; RN-09 segue BA16/G24 até que o baseline se resolva. Dono: time.
- [ ] **QA-03 — Conta única para quem acumula papéis.** Um professor que compra um curso usa a mesma
      Conta (RN-13, papel base `aluno` + papel interno) ou duas contas separadas? RN-13 assume a
      primeira; a alternativa muda o cadastro e a marca d'água. Dono: negócio.
- [ ] **QA-04 — Quem pode convidar ator interno.** RN-14 exige a permissão de conceder acesso
      interno, mas não diz se ela é exclusiva do administrador ou delegável a outro papel. Dono:
      negócio. Não bloqueia o PRD de `CAP-001`.
- [ ] **QA-05 — Primeiro administrador.** RN-14 elimina auto-cadastro de ator interno, o que torna o
      primeiro administrador um problema de origem: ele nasce por provisionamento da Fase 0 ou por um
      caminho de exceção auditado? Dono: time. Bloqueia o PRD de `CAP-002`, não o de `CAP-001`.

---

*Domain Doc gerado com a skill `tsg-flow-domain-creator`. Para criar o PRD de uma capacidade que
toca este domínio, use `tsg-flow-prd-creator` fornecendo o `vision.md`, este arquivo, os demais
domain docs que a capacidade atravessa e o ID da capacidade.*
