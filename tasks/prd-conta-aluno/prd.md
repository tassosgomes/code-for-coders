---
tsg_artifact: prd
product: code-4-coders
capability: CAP-001
version: 1.1
status: approved
updated: 2026-09-22
sources: backlog/capabilities.md@1.4, vision.md@1.2, context/domain-map.md@1.2, context/architecture-baseline.md@1.2, domains/identidade-e-acesso/domain.md@1.1, tasks/prd-notificacao-transacional/prd.md@1.1
---

# Conta e autenticação do aluno — ciclo completo do MVP

## Visão Geral

Uma pessoa precisa criar sua conta, provar que controla o e-mail informado, entrar na plataforma,
encerrar a sessão e recuperar o acesso sem atendimento humano. Esse ciclo estabelece o ator que as
capacidades seguintes reconhecerão como comprador e aluno. Uma conta ainda não confirmada não pode
autenticar. O e-mail necessário para confirmação e recuperação é entregue por `CAP-026`.

Esta entrega cobre o **ciclo completo da conta do aluno no MVP**, conforme recorte confirmado em
2026-09-22: cadastro, confirmação de e-mail, login, sessão, logout e recuperação de senha. A troca
de senha por aluno autenticado faz parte da gestão segura dessa mesma credencial. O acesso interno
por papéis pertence a `CAP-002`.

## Rastreabilidade

### Capacidade e fronteiras

- **Capacidade:** `CAP-001` — Conta e autenticação do aluno.
- **Escopo desta entrega:** ciclo completo da conta de aluno no MVP, incluindo reenvio de confirmação
  quando o link expira, troca e redefinição de senha, expiração e revogação das sessões afetadas.
- **Fora desta entrega:** convite e autenticação de ator interno (`CAP-002`); autorização para
  assistir a um curso (`CAP-008`); perfil social (`CAP-023`); atendimento por chamado (`CAP-025`);
  exclusão ou anonimização coordenada de dados pessoais (`CAP-031`). Não há parte restante de
  `CAP-001` planejada para fase posterior.
- **Domínios atravessados:** Identidade e Acesso,
  [domain.md](../../domains/identidade-e-acesso/domain.md); Notificação, cujo recorte transacional
  está no [PRD aprovado de CAP-026](../prd-notificacao-transacional/prd.md). Notificação não tem
  domain doc nesta rodada porque `CAP-027` só entra na Fase 4.
- **Junta:** Identidade e Acesso é dona da Conta, da Credencial, da Sessão, do Token de Verificação
  e da decisão de pedir envio. Ela fornece destinatário, modelo, dados e finalidade no pedido
  `notificacao.envio-solicitado`. Notificação é dona do modelo, do texto, do consentimento, da
  entrega e do Registro de Entrega. O pedido segue o
  [contrato AsyncAPI aprovado](../prd-notificacao-transacional/asyncapi-contract.yaml).
- **Dependência:** `CAP-026`, na fatia que entrega confirmação de conta e recuperação de senha.
  O [PRD de CAP-026](../prd-notificacao-transacional/prd.md) e seu contrato estão aprovados; a
  implementação foi integrada no PR #5 em 2026-09-22.
- **Restrições do baseline que limitam o produto:** sessão opaca no BFF do aluno, sem token no
  navegador (BA05/BA06); revogação imediata (RN-08); proteção CSRF em escrita autenticada (G18);
  nenhuma limitação de dispositivos ou sessões simultâneas (BA16/G24); `tenant_id` desde o MVP
  (BA10/G07); nenhum dado pessoal em telemetria ou URLs (G10/G23).

### Visão e domínio

- **Origem na visão:** `C01`; contribui para a Fase 1, na qual o aluno encontra, compra e assiste
  a um curso. A autenticação é própria da plataforma, sem SSO corporativo. A entrega é web
  responsiva e em português; não há migração de contas externas.
- **Entidades de Identidade e Acesso:** Conta, Credencial, Sessão e Token de Verificação. Papel
  aparece apenas como identidade de aluno; convite e permissões internas pertencem a `CAP-002`.
- **Regras herdadas:** RN-01 a RN-11, RN-13/13a/13b, RN-21 a RN-24 e RN-26 a RN-28 do
  [domain doc](../../domains/identidade-e-acesso/domain.md). RN-22 impede confundir conta
  autenticada com direito de acesso a curso.
- **Eventos produzidos:** `identidade.conta-criada`, `identidade.conta-confirmada`,
  `identidade.senha-redefinida` e os pedidos endereçados `notificacao.envio-solicitado` para os
  modelos `confirmacao-de-conta` e `recuperacao-de-senha`. O fato `identidade.conta-criada` não
  transporta token nem dispara o e-mail. Nenhum evento é consumido por esta capacidade.

## Objetivos

1. Permitir que uma pessoa conclua cadastro e confirmação de e-mail sem intervenção da operação.
2. Permitir entrada e saída com sessão revogável e recuperação autônoma quando a senha é esquecida.
3. Impedir autenticação de conta não confirmada e evitar que login ou recuperação revelem a
   existência de um e-mail cadastrado.
4. Fornecer identidade de aluno estável às próximas capacidades sem presumir direito de acesso
   a conteúdo.

## Histórias de Usuário

- Como visitante, quero criar uma conta e confirmar meu e-mail para entrar na plataforma.
- Como pessoa que não recebeu ou deixou expirar a confirmação, quero pedir novo link sem refazer
  o cadastro.
- Como aluno, quero entrar, manter uma sessão durante o uso e encerrá-la quando terminar.
- Como aluno que esqueceu a senha, quero redefini-la sem depender de atendimento.
- Como aluno autenticado, quero trocar minha senha e encerrar as outras sessões da conta.

## Funcionalidades Principais

### RF-01: Cadastrar conta de aluno

**Descrição:** receber nome, e-mail e senha, criar uma Conta de aluno não confirmada e solicitar
o e-mail de confirmação. O e-mail é normalizado conforme RN-01. O cadastro não cria acesso a
curso.

**Critérios de aceitação:**

- **Dado** um e-mail ainda sem conta não desativada, **quando** a pessoa conclui um cadastro
  válido, **então** existe uma Conta não confirmada e um pedido de confirmação endereçado a
  Notificação; a pessoa recebe orientação para verificar o e-mail.
- **Dado** um e-mail já ligado a uma conta não desativada, **quando** alguém tenta cadastrá-lo
  novamente, **então** não nasce outra conta nem uma nova credencial; a interface orienta a entrar,
  recuperar a senha ou pedir nova confirmação conforme a situação, sem expor credenciais.
- **Dado** o mesmo e-mail com maiúsculas ou espaços nas bordas, **quando** o cadastro é tratado,
  **então** ele corresponde à mesma identidade normalizada.
- **Dado** uma conta de aluno recém-criada, **quando** alguém tenta autenticar antes da
  confirmação, **então** a entrada é negada e a pessoa é orientada a confirmar o e-mail.
- **Dado** uma senha com menos de oito caracteres ou sem pelo menos uma letra maiúscula, uma
  minúscula, um dígito e um símbolo, **quando** alguém tenta cadastrar a conta, **então** o
  cadastro é recusado com orientação de correção e nenhuma Conta é criada.

**Prioridade:** Must Have. **Rastreabilidade:** RN-01, RN-02, RN-06, RN-13, RN-24, RN-26 a RN-28.

### RF-02: Confirmar e-mail e reenviar confirmação

**Descrição:** validar um link de uso único dentro do prazo informado no e-mail. Uma pessoa com
Conta não confirmada pode solicitar novo link sem criar outra conta. A validade é um parâmetro
de operação conforme `CAP-026`; a regra funcional é uso único e expiração curta.

**Critérios de aceitação:**

- **Dado** um link válido para uma Conta não confirmada, **quando** a pessoa o utiliza, **então**
  o e-mail fica confirmado, a Conta pode autenticar, o link deixa de funcionar e
  `identidade.conta-confirmada` é publicado.
- **Dado** um link usado, expirado, alterado ou de outra finalidade, **quando** alguém tenta
  confirmar a conta, **então** nenhuma conta é confirmada e a interface oferece o caminho para
  pedir novo link.
- **Dado** uma Conta não confirmada cujo link expirou, **quando** a pessoa pede reenvio, **então**
  ela recebe novo pedido de confirmação no mesmo endereço e não precisa refazer o cadastro.
**Prioridade:** Must Have. **Rastreabilidade:** RN-02, RN-03, RN-26 a RN-28; `CAP-026` RF-02.

### RF-03: Entrar e manter sessão de aluno

**Descrição:** autenticar somente Conta de aluno ativa e confirmada, criar sessão e permitir o
uso até logout, expiração por inatividade ou revogação. O produto não impõe sessão única nem
limite de dispositivos.

**Critérios de aceitação:**

- **Dado** e-mail e senha corretos de Conta de aluno ativa e confirmada, **quando** o aluno
  entra, **então** passa a ter sessão autenticada e é reconhecido como o mesmo ator nas próximas
  ações.
- **Dado** e-mail inexistente ou senha incorreta, **quando** a entrada é tentada, **então** a
  resposta visível é a mesma e nenhuma sessão é criada.
- **Dado** credenciais corretas de uma Conta não confirmada, **quando** a entrada é tentada,
  **então** não há sessão autenticada e a pessoa recebe orientação para confirmar o e-mail.
  Credenciais incorretas continuam recebendo a resposta genérica do critério anterior.
- **Dado** uma Conta desativada, **quando** a entrada é tentada, **então** não há sessão
  autenticada.
- **Dado** uma sessão ativa, **quando** há atividade válida, **então** o período de inatividade é
  renovado; **quando** o prazo configurado transcorre sem atividade, **então** a sessão expira e
  a próxima ação protegida exige nova autenticação.
- **Dado** o mesmo aluno em sessões simultâneas, **quando** inicia outra sessão, **então** a
  sessão anterior continua válida até seu próprio encerramento, expiração ou revogação.
- **Dado** um ator interno, **quando** tenta usar o fluxo de aluno, **então** a conta interna não
  vira Conta de aluno nem lhe concede acesso a curso.

**Prioridade:** Must Have. **Rastreabilidade:** RN-02, RN-05, RN-08 a RN-10, RN-13/13a/13b,
RN-22, RN-24; BA06.

### RF-04: Encerrar sessão

**Descrição:** permitir logout da sessão corrente e revogação imediata. O encerramento não
altera outras sessões ativas do mesmo aluno.

**Critérios de aceitação:**

- **Dado** uma sessão ativa, **quando** o aluno escolhe sair, **então** aquela sessão é
  encerrada e nenhuma ação protegida posterior a aceita.
- **Dado** outra sessão do mesmo aluno, **quando** ele sai da sessão corrente, **então** a outra
  sessão permanece ativa.
- **Dado** sessão já encerrada ou expirada, **quando** ocorre nova tentativa de logout,
  **então** nenhuma sessão é reativada e a pessoa é encaminhada à entrada.

**Prioridade:** Must Have. **Rastreabilidade:** RN-08, RN-09.

### RF-05: Solicitar e concluir recuperação de senha

**Descrição:** receber pedido de recuperação, solicitar a `CAP-026` o envio de link ao endereço
de Conta elegível e permitir redefinição mediante prova válida. O solicitante recebe resposta
indistinguível para e-mail cadastrado e não cadastrado.

**Critérios de aceitação:**

- **Dado** um e-mail de Conta de aluno não desativada, **quando** a recuperação é solicitada, **então**
  a resposta visível é neutra e um pedido com finalidade `recuperacao-de-senha` é enviado a
  Notificação com link e prazo de validade.
- **Dado** um e-mail sem Conta elegível, **quando** a recuperação é solicitada, **então** a
  resposta visível é idêntica e nenhum pedido de envio é publicado.
- **Dado** um link válido, **quando** o aluno define nova senha válida, **então** a Credencial
  muda, o link e todos os demais links de recuperação pendentes deixam de funcionar, as outras
  sessões da Conta são encerradas e `identidade.senha-redefinida` é publicado.
- **Dado** uma Conta ainda não confirmada, **quando** sua senha é redefinida, **então** ela
  permanece não confirmada e precisa confirmar o e-mail antes do primeiro login.
- **Dado** link usado, expirado, alterado ou de confirmação de conta, **quando** alguém tenta
  redefinir a senha, **então** a senha não muda e a pessoa pode solicitar nova recuperação.
- **Dado** uma senha redefinida, **quando** a senha antiga é usada para entrar, **então** a
  autenticação falha.
- **Dado** uma nova senha que não atende à política aprovada, **quando** alguém tenta concluir a
  recuperação, **então** a Credencial não muda e o token continua utilizável até seu vencimento.

**Prioridade:** Must Have. **Rastreabilidade:** RN-03 a RN-07, RN-21, RN-26 a RN-28;
`CAP-026` RF-03.

### RF-06: Trocar senha com sessão ativa

**Descrição:** aluno autenticado pode trocar a própria senha mediante confirmação da senha atual.
Essa mudança tem as mesmas consequências de revogação da redefinição por recuperação.

**Critérios de aceitação:**

- **Dado** aluno autenticado e senha atual correta, **quando** ele define nova senha válida,
  **então** a Credencial muda, as demais sessões são encerradas, os links de recuperação
  pendentes são invalidados e `identidade.senha-redefinida` é publicado.
- **Dado** senha atual incorreta, **quando** a troca é solicitada, **então** a Credencial e as
  sessões não mudam.
- **Dado** outra sessão da mesma Conta, **quando** a troca é concluída, **então** sua próxima
  ação protegida exige nova autenticação.
- **Dado** uma nova senha que não atende à política aprovada, **quando** o aluno tenta trocá-la,
  **então** a Credencial e as sessões não mudam.

**Prioridade:** Must Have. **Rastreabilidade:** RN-06, RN-07, RN-08, RN-11.

## Experiência do Usuário

O visitante encontra o cadastro e, ao concluí-lo, recebe uma instrução para verificar o e-mail.
O link confirma a conta uma única vez. Se expirou ou a mensagem não chegou, a interface oferece
reenvio sem exigir novo cadastro. O e-mail transacional informa seu prazo de validade.

O aluno entra com e-mail e senha, usa a plataforma enquanto a sessão está válida e encontra uma
ação clara de sair. Ao expirar a sessão, retorna à entrada sem perder a compreensão do que
aconteceu. Quem esqueceu a senha solicita recuperação e recebe uma resposta neutra; o link
recebido permite definir uma nova senha. Mensagens de erro em login e recuperação não expõem
se um e-mail pertence a alguém. Fluxos de formulário e mensagens devem ser utilizáveis por
teclado e tecnologias assistivas; o detalhamento visual cabe ao time de design.

## Decisões de Produto

| ID | Decisão confirmada | Alternativa descartada e motivo | Efeito |
|---|---|---|---|
| DP-01 | O primeiro PRD de `CAP-001` entrega o ciclo completo da conta do aluno no MVP; recorte confirmado pelo usuário em 2026-09-22 | Adiar confirmação ou recuperação deixaria o cadastro sem prova de e-mail ou o aluno sem saída ao esquecer a senha | RF-01 a RF-06 |
| DP-02 | Contas de aluno e de ator interno são separadas, inclusive por e-mails distintos (QA-03 do domain doc) | Conta única com papel aluno e interno criaria recuperação ambígua e cruzaria fronteiras de autorização | RF-01, RF-03 |
| DP-03 | Sessões simultâneas são permitidas (BA16, RN-09) | Sessão única e limite por IP prejudicariam uso legítimo sem evidência de abuso | RF-03, RF-04 |
| DP-04 | Confirmação e recuperação usam os dois modelos transacionais de `CAP-026` (DP-01 a DP-03 daquele PRD) | Enviar diretamente do domínio de Identidade duplicaria o dono da entrega e do texto | RF-01, RF-02, RF-05 |
| DP-05 | Senha do aluno tem no mínimo oito caracteres e inclui letra maiúscula, minúscula, dígito e símbolo; espaços não contam como símbolo. Regra confirmada pelo usuário em 2026-09-22 | Deixar a regra em aberto impediria validar cadastro e recuperação de forma consistente | RF-01, RF-05, RF-06 |
| DP-06 | smtp4dev captura e-mails no ambiente local para testar o fluxo, sem necessidade de provedor ou DNS reais. Escolha confirmada pelo usuário em 2026-09-22 | Exigir provedor de produção antes do teste local atrasaria o aceite funcional | RF-01, RF-02, RF-05 |

## Restrições Técnicas de Alto Nível

- O canal de e-mail depende da implantação operacional do provedor e do domínio de envio de
  `CAP-026`; a validade dos links é parâmetro configurado e exibido nas mensagens. Não se fixa
  prazo numérico neste PRD.
- No ambiente local, Notificação envia os dois modelos transacionais ao smtp4dev e o time lê os
  links na interface dele. Esse teste valida geração, pedido, consumo e renderização do e-mail;
  o envio a destinatários externos continua dependendo do provedor e DNS de produção.
- Credenciais e tokens de verificação não são expostos em resposta, log, métrica ou evento
  difundido. O link secreto segue somente no pedido endereçado a Notificação e na mensagem ao
  titular. O e-mail pode constar no pedido de envio, conforme exceção RN-26, e não em telemetria.
- Sessões são opacas para o navegador e revogáveis imediatamente; escrita autenticada exige
  proteção CSRF. O prazo de inatividade é configurável e será definido na especificação técnica
  antes da implementação.
- A identidade do aluno não concede automaticamente direito comercial de acesso a um curso.

## Não-Objetivos (Fora de Escopo)

- Login social, SSO corporativo, aplicativo mobile nativo e migração de contas externas.
- Autocadastro interno, convites, papéis e permissões de backoffice (`CAP-002`).
- Concessão, consulta ou revogação de direito de acesso a curso (`CAP-008`).
- Restrição por IP, dispositivo, quantidade de sessões ou reprodução simultânea.
- Perfil público, avatar, comunidade, campanhas ou mensagens promocionais.
- Desativação administrativa, exclusão e anonimização coordenada da conta (`CAP-031`).

## Plano de Rollout Faseado

**MVP / Fase 1:** RF-01 a RF-06 devem funcionar em conjunto com os dois modelos aprovados de
`CAP-026`. O gate para avançar é demonstrar cadastro, confirmação, login, logout, reenvio,
recuperação e troca de senha de ponta a ponta, inclusive expiração, uso único e revogação.

Fases posteriores acrescentam capacidades vizinhas (`CAP-002`, `CAP-008`, `CAP-031`), sem
reabrir o ciclo de conta desta entrega.

## Métricas de Sucesso

| Medida | Definição | Meta e momento |
|---|---|---|
| Conclusão do ciclo de cadastro | Cadastro → pedido de confirmação → confirmação → primeiro login, sem intervenção da operação | Fluxo completo demonstrado no aceite do MVP; taxa real medida após lançamento, sem meta percentual inventada antes de haver base |
| Recuperação autônoma | Pedido por Conta elegível → mensagem → senha redefinida → novo login | Fluxo completo demonstrado no aceite do MVP; taxa real medida após lançamento |
| Quebra de regras de segurança observáveis | Conta não confirmada autenticada; link reutilizado; sessão revogada aceita; resposta de recuperação que distingue existência de conta | Zero nos cenários de aceite antes do lançamento e durante a operação |
| Falha de entrega que impede o ciclo | Pedidos de confirmação ou recuperação sem desfecho útil para o aluno | Mensurável por finalidade e motivo a partir do Registro de Entrega de `CAP-026` desde o lançamento; linha de base operacional antes de fixar meta de redução |

## Riscos e Mitigações

- **E-mail não entregue:** cadastro e recuperação ficam interrompidos. `CAP-026` registra o
  desfecho; o aluno pode pedir novo link e a operação consegue diagnosticar a falha.
- **Confusão entre conta e acesso a curso:** login pode ser interpretado como matrícula. A
  comunicação e os requisitos separam identidade (`CAP-001`) de direito de acesso (`CAP-008`).
- **Fricção na confirmação:** se o link expira ou se perde, a pessoa abandona o cadastro. O
  reenvio reaproveita a Conta não confirmada e comunica o prazo do link.

## Questões em Aberto

- **QT-01 — Prazos de segurança.** Donos: time de segurança e produto; declarar valores
  antes de operar com alunos reais. Definir o prazo de validade de cada finalidade de link e o
  prazo de inatividade da sessão. A política de senha está fechada em DP-05; uso único, expiração
  e revogação já estão fechados. A TechSpec trata os prazos como configuração explícita.
- **QT-02 — Disponibilidade operacional do e-mail.** Dono: time de plataforma; resolver antes de
  tráfego real. Confirmar provedor, domínio remetente e SPF/DKIM/DMARC da entrega de `CAP-026`.
  A falta disso impede confirmação e recuperação com destinatários externos, embora o smtp4dev
  permita testar o fluxo completo no ambiente local.
