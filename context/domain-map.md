---
tsg_artifact: domain-map
product: code-4-coders
version: 1.1
status: approved
updated: 2026-09-20
sources: vision.md@1.1
---

# Domain Map

> **Nível 1 da hierarquia de documentação.** Deriva de `vision.md` (v1.1, aprovado). Este documento define fronteiras conceituais — não arquitetura, não serviços, não contratos. A decisão de como isso vira sistema físico é do Architecture Baseline.

**Versão:** 1.1 · **Data:** 2026-09-20 · **Origem:** `vision.md` v1.1 · **Produto:** code-4-coders

---

## Visão Geral da Decomposição

A visão descreve três fluxos de valor que atravessam o produto inteiro:

1. **Descobrir → comprar → acessar** — o visitante escolhe uma oferta, paga e recebe o direito de consumir um conteúdo por uma vigência definida no contrato da compra.
2. **Assistir → progredir → ser avaliado → comprovar** — o aluno consome aulas, acumula progresso, é avaliado e obtém um certificado verificável por terceiros.
3. **Cobrar → manter ou bloquear** — a relação financeira continua depois da venda e decide, ao longo do tempo, se o acesso permanece.

A decomposição abaixo segue a **pergunta que cada domínio responde**, não a tela onde a informação aparece. Três fronteiras concentram o risco do produto e foram desenhadas primeiro:

- **"O que está à venda?"** (Catálogo e Oferta) é diferente de **"o que se aprende?"** (Conteúdo e Currículo).
- **"Este aluno pode acessar isto agora?"** (Matrícula e Direito de Acesso) é diferente de **"ele pagou?"** (Vendas, Cobrança) e de **"ele já pode ver a próxima aula?"** (Aprendizagem e Progresso).
- **"Quem é você?"** (Identidade e Acesso) é diferente de **"o que você comprou?"** (Matrícula e Direito de Acesso).

São **16 domínios conceituais**. Isso não significa 16 serviços, 16 bancos ou 16 times — significa 16 responsabilidades que não devem se misturar. A Fase 1 da visão toca apenas 7 deles.

**Limites desta decomposição:** não há decisão de tecnologia, persistência, protocolo ou granularidade de deploy. Turmas/coortes, afiliados e gamificação aparecem alojados em domínios maiores, com a justificativa registrada em Pontos de Atenção.

---

## Lista de Domínios

### 1. Identidade e Acesso

- **Responsabilidade:** estabelecer quem é o ator e o que ele está autorizado a fazer na plataforma, para alunos e para pessoal interno.
- **O que não faz:** não decide se um aluno pode assistir a um curso — isso é direito de acesso, e depende de compra, não de identidade. Não registra o histórico de ações administrativas (Auditoria e Conformidade). Não guarda preferência de comunicação (Notificação).
- **Entidades conceituais:** Conta, Sessão, Papel, Permissão, Convite de Acesso Interno.
- **Linguagem ubíqua:**
  - *Papel* — função exercida na plataforma (aluno, professor, suporte, financeiro, administrador) que agrupa permissões.
  - *Permissão* — autorização para executar uma ação específica no backoffice.
  - *Ator interno* — pessoa da operação, distinta do aluno, sujeita a RBAC estrito.
- **Interações:** todos os domínios perguntam a este quem é o ator corrente. Publica a criação de conta, que interessa a Matrícula, Notificação e Inteligência de Negócio.
- **Justificativa:** autenticação e autorização de papel mudam por razões de segurança e organização interna, enquanto direito de acesso a conteúdo muda por razões comerciais. Misturar os dois faz com que uma mudança de política de suporte toque a regra de acesso do aluno pagante.

### 2. Catálogo e Oferta

- **Responsabilidade:** definir o que está à venda, por qual preço, sob qual condição comercial e com qual direito de acesso prometido.
- **O que não faz:** não cria nem estrutura o conteúdo pedagógico (Conteúdo e Currículo). Não processa pagamento (Vendas e Checkout). Não concede acesso (Matrícula e Direito de Acesso). Não impede compra por pré-requisito — pré-requisito aqui é texto informativo.
- **Entidades conceituais:** Oferta, Preço, Campanha de Compra, Cupom, Vitrine, Nível, Pré-requisito Declarado.
- **Linguagem ubíqua:**
  - *Oferta* — combinação de conteúdo vendável, preço, condição comercial e direito de acesso prometido.
  - *Campanha de compra* — condição temporária que altera preço e/ou direito de acesso de uma oferta.
  - *Nível* — iniciante, intermediário ou avançado; orienta a escolha, não restringe.
  - *Pré-requisito declarado* — recomendação pedagógica exibida ao aluno, sem efeito sobre compra ou acesso.
- **Interações:** entrega a oferta vigente a Vendas e Checkout no momento da compra. Referencia o currículo publicado por Conteúdo e Currículo. Informa a Matrícula e Direito de Acesso qual vigência a oferta prometeu.
- **Justificativa:** o mesmo curso pode ser vendido de várias formas ao longo do tempo (avulso vitalício, avulso por 12 meses, incluso em assinatura, turma com data) sem que uma vírgula do conteúdo mude. Preço e currículo têm ciclos de vida e donos diferentes — negócio e professor.

### 3. Conteúdo e Currículo

- **Responsabilidade:** estruturar o que se aprende — cursos, módulos, aulas, materiais complementares e a ordem pedagógica entre eles.
- **O que não faz:** não define preço nem condição de venda (Catálogo e Oferta). Não armazena nem entrega o vídeo em si (Entrega de Mídia e Proteção). Não registra o que cada aluno já assistiu (Aprendizagem e Progresso). Não corrige avaliações (Avaliação).
- **Entidades conceituais:** Curso, Módulo, Aula, Material Complementar, Versão de Publicação.
- **Linguagem ubíqua:**
  - *Currículo* — estrutura ordenada de módulos e aulas que compõe um curso.
  - *Publicação* — ato de tornar uma versão do currículo disponível para consumo.
  - *Aula* — menor unidade de conteúdo consumível, que referencia uma mídia.
- **Interações:** publica a estrutura consumida por Aprendizagem e Progresso e referenciada por Catálogo e Oferta. Solicita a Entrega de Mídia e Proteção a preparação de um vídeo. Define onde as avaliações se encaixam no currículo, sem possuí-las.
- **Justificativa:** o conteúdo é o ativo central do negócio e evolui por decisão pedagógica — aula regravada, módulo reordenado, material atualizado. Essa evolução não pode arrastar consigo preço, progresso de alunos ou histórico de compras.

### 4. Entrega de Mídia e Proteção

- **Responsabilidade:** armazenar, preparar e entregar o vídeo e os materiais pesados ao aluno autorizado, com proteção contra cópia.
- **O que não faz:** não decide quem pode assistir — pergunta a Matrícula e Direito de Acesso. Não conhece curso, módulo, preço ou progresso. Não é dono do significado pedagógico do arquivo.
- **Entidades conceituais:** Mídia, Versão de Reprodução, Sessão de Reprodução, Marca d'Água, Ativo Protegido.
- **Linguagem ubíqua:**
  - *Sessão de reprodução* — autorização temporária e individual para reproduzir uma mídia.
  - *Marca d'água dinâmica* — identificação do aluno sobreposta ao vídeo durante a reprodução.
  - *Proteção* — conjunto de medidas que dissuade a cópia; dissuasão, não garantia.
- **Interações:** consulta Matrícula e Direito de Acesso antes de liberar reprodução. Recebe de Conteúdo e Currículo a mídia a preparar. Informa Aprendizagem e Progresso sobre o avanço da reprodução.
- **Justificativa:** este é o domínio que concentra o custo de infraestrutura (armazenamento e CDN) e a estratégia de proteção — que, sem DRM contratado (vision.md v1.1), é própria e tende a mudar com o tempo. Isolá-lo permite trocar de provedor, mudar a estratégia de proteção ou renegociar custo sem tocar em regra pedagógica ou comercial. É também o único domínio cujo comportamento é ditado por restrições técnicas externas.

### 5. Matrícula e Direito de Acesso

- **Responsabilidade:** responder, a qualquer momento, se um aluno tem direito de acessar um conteúdo — e até quando.
- **O que não faz:** não cobra nem verifica pagamento (Cobrança e Assinatura). Não decide preço nem prazo prometido (Catálogo e Oferta). Não controla a liberação da próxima aula dentro de um curso (Aprendizagem e Progresso) — essa é regra pedagógica, não comercial.
- **Entidades conceituais:** Matrícula, Concessão de Acesso, Vigência, Turma (Coorte), Suspensão, Revogação.
- **Linguagem ubíqua:**
  - *Concessão de acesso* — direito de um aluno sobre um conteúdo, com origem (compra, assinatura, turma, cortesia) e vigência.
  - *Vigência* — período de validade da concessão; por período determinado ou vitalícia, conforme o contrato da compra.
  - *Suspensão* — interrupção temporária do acesso por causa financeira, reversível ao regularizar.
  - *Revogação* — encerramento definitivo, por reembolso ou decisão administrativa.
  - *Turma (coorte)* — agrupamento de matrículas com data de início e acompanhamento comum.
- **Interações:** recebe de Vendas e Checkout a ordem de conceder acesso após compra concluída; recebe de Cobrança e Assinatura os eventos de inadimplência e reembolso que suspendem ou revogam. Responde a Entrega de Mídia e Proteção, Aprendizagem e Progresso, Avaliação e Comunidade a pergunta "este aluno pode?".
- **Justificativa:** a visão marcou o direito de acesso como decisão estrutural e registrou o risco de ele virar flag de checkout. Ele tem quatro origens diferentes (avulso, assinatura, turma, cortesia) e três causas de mudança (expiração, inadimplência, reembolso) que nascem em domínios distintos. Se essa regra morar dentro de Vendas ou de Cobrança, toda mudança comercial vira risco de o aluno certo perder acesso — ou o errado manter.

### 6. Vendas e Checkout

- **Responsabilidade:** conduzir a intenção de compra até um pedido concluído, aplicando condição comercial e atribuindo a indicação que a originou.
- **O que não faz:** não executa a cobrança no gateway nem gerencia recorrência (Cobrança e Assinatura). Não concede acesso (Matrícula e Direito de Acesso). Não emite documento fiscal (Fiscal). Não define a oferta (Catálogo e Oferta).
- **Entidades conceituais:** Pedido, Item do Pedido, Aplicação de Cupom, Indicação de Afiliado, Afiliado.
- **Linguagem ubíqua:**
  - *Pedido* — registro da intenção de compra de uma oferta por um comprador, com o preço e a condição congelados no momento da compra.
  - *Afiliado* — parceiro remunerado pelas vendas que indica.
  - *Atribuição* — vínculo entre uma venda e a indicação que a originou.
- **Interações:** consulta Catálogo e Oferta para obter a oferta vigente; aciona Cobrança e Assinatura para receber o pagamento; ao ser informado de pagamento confirmado, ordena a concessão de acesso a Matrícula e Direito de Acesso e notifica Fiscal do fato gerador.
- **Justificativa:** o pedido é o contrato do que foi comprado e por qual condição — e precisa permanecer imutável mesmo quando o preço da oferta mudar depois. Separá-lo da cobrança permite que uma venda exista com pagamento pendente (boleto, PIX não pago) sem que isso contamine o conceito de venda realizada.

### 7. Cobrança e Assinatura

- **Responsabilidade:** obter e manter o dinheiro — executar cobranças, renovar assinaturas, tratar falhas e conduzir a inadimplência até a decisão de bloquear.
- **O que não faz:** não bloqueia o acesso diretamente; comunica o fato financeiro e quem bloqueia é Matrícula e Direito de Acesso. Não emite nota fiscal (Fiscal). Não decide o preço (Catálogo e Oferta). Não envia as mensagens da régua (Notificação).
- **Entidades conceituais:** Assinatura, Ciclo de Cobrança, Tentativa de Cobrança, Pagamento, Inadimplência, Régua de Cobrança, Reembolso.
- **Linguagem ubíqua:**
  - *Assinatura* — compromisso de pagamento recorrente que sustenta um acesso continuado.
  - *Inadimplência* — estado do aluno cuja cobrança falhou ou venceu sem pagamento.
  - *Régua de cobrança* — sequência programada de avisos antes do bloqueio.
  - *Reembolso* — devolução de valor que encerra o direito adquirido.
- **Interações:** recebe de Vendas e Checkout a ordem de cobrar; integra-se ao gateway externo; publica pagamento confirmado, pagamento falho, inadimplência e reembolso, consumidos por Matrícula e Direito de Acesso, Fiscal, Notificação e Inteligência de Negócio.
- **Justificativa:** é o domínio de maior complexidade de estado no tempo (ciclos, tentativas, prazos, estornos) e o mais acoplado a um provedor externo. A visão já o isolou como risco. Mantê-lo separado de Vendas evita que a complexidade da recorrência invada o ato simples de comprar um curso avulso — que é justamente o escopo da Fase 1.

### 8. Fiscal

- **Responsabilidade:** transformar um fato gerador em documento fiscal válido perante a prefeitura e manter esse documento acessível ao aluno e ao financeiro.
- **O que não faz:** não decide se houve venda (Vendas e Checkout), não recebe dinheiro (Cobrança e Assinatura), não faz contabilidade e não substitui ERP.
- **Entidades conceituais:** Documento Fiscal (NFS-e), Fato Gerador, Tomador do Serviço, Cancelamento Fiscal.
- **Linguagem ubíqua:**
  - *Fato gerador* — evento comercial que obriga a emissão do documento fiscal.
  - *NFS-e* — Nota Fiscal de Serviço Eletrônica, emitida segundo a regra do município do prestador.
- **Interações:** consome eventos de pagamento confirmado e reembolso; solicita ao provedor municipal a emissão ou o cancelamento; disponibiliza o documento ao aluno via Notificação.
- **Justificativa:** a regra fiscal é ditada por legislação municipal externa, muda sem aviso e por razão alheia ao produto. Mais importante: **uma falha de emissão não pode impedir uma venda nem o acesso do aluno**. Essa assimetria de criticidade só se sustenta se a emissão viver fora do caminho crítico da compra.

### 9. Aprendizagem e Progresso

- **Responsabilidade:** acompanhar o percurso do aluno dentro de um curso — o que já consumiu, onde parou, o que já pode ver e o que anotou.
- **O que não faz:** não decide se o aluno tem direito ao curso (Matrícula e Direito de Acesso). Não estrutura o currículo (Conteúdo e Currículo). Não corrige avaliações (Avaliação), embora dependa do resultado delas. Não emite certificado (Certificação).
- **Entidades conceituais:** Progresso, Conclusão de Aula, Percurso na Trilha, Liberação de Aula, Anotação Pessoal.
- **Linguagem ubíqua:**
  - *Progresso* — registro do avanço de um aluno em um curso.
  - *Liberação progressiva (drip content)* — regra que só abre a próxima aula após a conclusão da anterior ou a aprovação em avaliação. Aplica-se **dentro** de um curso, nunca entre cursos.
  - *Anotação pessoal* — nota privada do aluno ancorada no minuto exato da aula.
- **Interações:** lê o currículo de Conteúdo e Currículo; verifica direito de acesso com Matrícula e Direito de Acesso; recebe de Entrega de Mídia e Proteção o avanço da reprodução; consulta Avaliação para liberar etapa condicionada; publica conclusão de curso, consumida por Certificação, Comunidade e Engajamento e Inteligência de Negócio.
- **Justificativa:** progresso é o dado mais volumoso e mais escrito do sistema, e sua regra de liberação é pedagógica — muda quando o professor muda de método, não quando o negócio muda de preço. A visão separou explicitamente liberação *dentro* do curso de acesso *ao* curso; esta fronteira materializa essa separação.

### 10. Avaliação

- **Responsabilidade:** medir o aprendizado — aplicar quizzes e provas, receber tentativas, corrigir e declarar aprovação.
- **O que não faz:** não decide o que a aprovação libera (Aprendizagem e Progresso) nem o que ela comprova (Certificação). Não estrutura o currículo, apenas se encaixa nele.
- **Entidades conceituais:** Avaliação, Questão, Tentativa, Resposta, Resultado, Critério de Aprovação.
- **Linguagem ubíqua:**
  - *Tentativa* — submissão de um aluno a uma avaliação, sujeita a limite e a registro próprio.
  - *Correção automática* — apuração do resultado sem intervenção humana, possível em questões objetivas.
  - *Aprovação* — declaração de que o aluno atingiu o critério definido para a avaliação.
- **Interações:** verifica direito de acesso antes de aplicar; publica resultado e aprovação, consumidos por Aprendizagem e Progresso, Certificação e Comunidade e Engajamento; questões dissertativas demandam correção por um ator com papel de professor.
- **Justificativa:** avaliação tem ciclo próprio (banco de questões, versões, tentativas, contestação) e um ator que não aparece em nenhum outro lugar do percurso: o professor corrigindo. Embutir isso em Aprendizagem faria um domínio já pesado acumular um segundo motivo completamente distinto para mudar.

### 11. Certificação

- **Responsabilidade:** emitir a prova de conclusão e permitir que qualquer terceiro verifique sua autenticidade.
- **O que não faz:** não decide se o aluno concluiu (Aprendizagem e Progresso) nem se foi aprovado (Avaliação). Não valida identidade do verificador — a validação é pública.
- **Entidades conceituais:** Certificado, Código de Validação, Registro de Conclusão, Consulta Pública de Validação.
- **Linguagem ubíqua:**
  - *Certificado validável* — comprovante com identificador único verificável publicamente.
  - *Código de validação* — identificador (QR code ou hash) impresso no certificado que permite a consulta por terceiro.
  - *Verificador* — terceiro não autenticado, tipicamente um empregador, que confere a autenticidade.
- **Interações:** consome conclusão de curso e aprovação; expõe consulta pública de validação; aciona Notificação na emissão.
- **Justificativa:** é o único domínio com um consumidor externo e **não autenticado** — um empregador que nunca terá conta na plataforma. Essa superfície pública tem exigências próprias de disponibilidade, imutabilidade e privacidade (o que o certificado revela sobre o aluno), incompatíveis com o restante do sistema, que pressupõe sessão de aluno.

### 12. Comunidade e Engajamento

- **Responsabilidade:** sustentar a interação entre alunos e com o professor, e estimular a permanência e a conclusão por mecânicas de reconhecimento.
- **O que não faz:** não trata problema de acesso, cobrança ou falha da plataforma (Atendimento e Suporte). Não é dono do progresso — reage a ele. Não define o conteúdo discutido.
- **Entidades conceituais:** Tópico, Resposta, Sala de Discussão da Aula, Dúvida Pedagógica, Moderação, Ponto, Badge, Ranking.
- **Linguagem ubíqua:**
  - *Dúvida pedagógica* — pergunta sobre o conteúdo, respondida por professor ou pela comunidade; distinta de chamado de suporte.
  - *Badge* — insígnia concedida por marco de progresso ou de participação.
  - *Moderação* — ação sobre conteúdo gerado por aluno, sujeita a registro de auditoria.
- **Interações:** verifica direito de acesso para liberar a sala de uma aula; consome conclusão de aula, conclusão de curso e aprovação para conceder pontos e badges; aciona Notificação quando uma dúvida é respondida.
- **Justificativa:** ambas as responsabilidades existem pelo mesmo motivo — fazer o aluno voltar — e compartilham os mesmos eventos de entrada e a mesma superfície na experiência. Separá-las agora criaria um domínio de gamificação pequeno demais, sem dado próprio, que só reage a eventos alheios. A divisão fica registrada como candidata futura, caso a gamificação ganhe regras próprias de campanha.

### 13. Atendimento e Suporte

- **Responsabilidade:** resolver o problema do aluno com a plataforma — acesso, cobrança, falha técnica — em um fluxo rastreável.
- **O que não faz:** não responde dúvida sobre o conteúdo do curso (Comunidade e Engajamento). Não executa reembolso nem altera acesso por conta própria — solicita aos domínios donos, que registram a ação.
- **Entidades conceituais:** Chamado, Interação de Atendimento, Artigo de Ajuda, Categoria de Problema.
- **Linguagem ubíqua:**
  - *Chamado de suporte* — solicitação sobre acesso, cobrança ou falha, com ciclo de vida e responsável.
  - *Central de ajuda* — base de artigos de autoatendimento que antecede o chamado.
- **Interações:** consulta Matrícula e Direito de Acesso e Cobrança e Assinatura para diagnosticar; solicita ação a esses domínios, sempre com registro em Auditoria e Conformidade; usa Notificação para responder ao aluno.
- **Justificativa:** a visão exige explicitamente que dúvida pedagógica e problema operacional corram separados — são atendidos por pessoas diferentes, com permissões diferentes e prazos diferentes. Unificá-los faria o suporte ver conteúdo de curso e o professor ver dado financeiro.

### 14. Notificação

- **Responsabilidade:** entregar uma mensagem ao destinatário certo pelo canal adequado, respeitando consentimento e preferência.
- **O que não faz:** não decide *o que* comunicar nem *quando* — os domínios de negócio pedem. Não faz campanha de marketing nem automação de funil. Não é dono da regra de negócio que originou a mensagem.
- **Entidades conceituais:** Notificação, Canal, Modelo de Mensagem, Preferência de Contato, Consentimento, Registro de Entrega.
- **Linguagem ubíqua:**
  - *Canal* — meio de entrega: e-mail, push no navegador ou WhatsApp.
  - *Consentimento* — autorização do aluno para ser contatado, com base legal registrada (LGPD).
  - *Modelo de mensagem* — texto parametrizável, sujeito a aprovação externa no caso do WhatsApp.
- **Interações:** consome pedidos de envio de praticamente todos os domínios; publica falha de entrega; é o guardião do consentimento perante a LGPD.
- **Justificativa:** é o único ponto do sistema que fala com o aluno fora da plataforma, e concentra uma obrigação legal (consentimento e descadastro) que precisa de um dono único. Se cada domínio enviasse suas próprias mensagens, o consentimento seria verificado em dez lugares — e violado em pelo menos um.

### 15. Inteligência de Negócio

- **Responsabilidade:** consolidar o que aconteceu no produto em indicadores que sustentem decisão — receita, churn, engajamento, desempenho de cursos.
- **O que não faz:** **não é dono de nenhum dado de negócio** e não é fonte de verdade para nenhuma decisão operacional. Não altera estado em domínio algum. Não substitui ferramenta de BI de mercado.
- **Entidades conceituais:** Indicador, Série Histórica, Recorte de Análise, Painel.
- **Linguagem ubíqua:**
  - *Churn* — taxa de cancelamento de assinaturas em um período.
  - *Indicador* — medida derivada de eventos de negócio, com definição acordada e estável.
  - *Aluno ativo* — definição de atividade a ser acordada com o negócio (ponto aberto A1 da visão).
- **Interações:** consome eventos de todos os domínios; não publica nada que outro domínio consuma.
- **Justificativa:** é um domínio puramente derivado, e essa assimetria é a fronteira. Declará-lo explicitamente como consumidor sem posse impede o antipadrão mais comum em sistemas assim: o painel virar fonte de verdade e, com o tempo, começar a escrever de volta.

### 16. Auditoria e Conformidade

- **Responsabilidade:** registrar de forma imutável quem fez o quê no backoffice, e sustentar os direitos do titular de dados previstos na LGPD.
- **O que não faz:** não concede nem nega permissão (Identidade e Acesso). Não mede desempenho de negócio (Inteligência de Negócio). Não guarda log técnico de aplicação — o registro aqui é de ato administrativo com consequência.
- **Entidades conceituais:** Registro de Auditoria, Ato Administrativo, Solicitação do Titular, Base Legal de Tratamento.
- **Linguagem ubíqua:**
  - *Ato administrativo* — ação de ator interno com efeito sobre aluno ou dinheiro: reembolso, alteração de nota, concessão de acesso, banimento.
  - *Registro de auditoria* — evidência imutável de um ato, com autor, momento, alvo e motivo.
  - *Solicitação do titular* — pedido de acesso, correção ou exclusão de dados pessoais.
- **Interações:** consome atos administrativos de Cobrança e Assinatura, Matrícula e Direito de Acesso, Avaliação, Comunidade e Engajamento e Atendimento e Suporte; coordena com os domínios donos a execução de exclusão ou anonimização.
- **Justificativa:** a visão exige rastrear quem deu reembolso, quem alterou nota e quem baniu usuário. Um registro de auditoria só vale se for imutável e estiver fora do alcance de quem praticou o ato — o que é impossível se cada domínio guardar o próprio log. A LGPD acrescenta a esse mesmo domínio a necessidade de um ponto único de resposta ao titular.

---

## Dependências Entre Domínios

| Origem | Destino | Interação de negócio | Responsabilidade dos dados |
|---|---|---|---|
| Vendas e Checkout | Catálogo e Oferta | Obter a oferta vigente e sua condição no momento da compra | Catálogo e Oferta |
| Vendas e Checkout | Cobrança e Assinatura | Solicitar o recebimento do valor do pedido | Vendas (pedido) / Cobrança (pagamento) |
| Vendas e Checkout | Matrícula e Direito de Acesso | Ordenar a concessão de acesso após compra concluída | Matrícula e Direito de Acesso |
| Cobrança e Assinatura | Matrícula e Direito de Acesso | Informar inadimplência e reembolso, que suspendem ou revogam | Matrícula e Direito de Acesso |
| Cobrança e Assinatura | Fiscal | Informar o fato gerador do documento fiscal | Fiscal |
| Cobrança e Assinatura | Notificação | Pedir o envio da régua de cobrança | Cobrança (regra) / Notificação (entrega) |
| Matrícula e Direito de Acesso | Catálogo e Oferta | Conhecer a vigência prometida pela oferta | Catálogo e Oferta |
| Entrega de Mídia e Proteção | Matrícula e Direito de Acesso | Perguntar se o aluno pode reproduzir a mídia agora | Matrícula e Direito de Acesso |
| Entrega de Mídia e Proteção | Identidade e Acesso | Obter o aluno corrente para a marca d'água dinâmica | Identidade e Acesso |
| Conteúdo e Currículo | Entrega de Mídia e Proteção | Solicitar a preparação de uma mídia para consumo | Entrega de Mídia e Proteção |
| Aprendizagem e Progresso | Conteúdo e Currículo | Ler a estrutura do currículo a percorrer | Conteúdo e Currículo |
| Aprendizagem e Progresso | Matrícula e Direito de Acesso | Verificar direito de acesso ao curso | Matrícula e Direito de Acesso |
| Aprendizagem e Progresso | Avaliação | Consultar aprovação para liberar etapa condicionada | Avaliação |
| Entrega de Mídia e Proteção | Aprendizagem e Progresso | Informar o avanço da reprodução da aula | Aprendizagem e Progresso |
| Certificação | Aprendizagem e Progresso | Consumir a conclusão que autoriza a emissão | Aprendizagem e Progresso |
| Certificação | Avaliação | Consumir a aprovação exigida pelo curso | Avaliação |
| Comunidade e Engajamento | Aprendizagem e Progresso | Reagir a conclusões para conceder pontos e badges | Aprendizagem e Progresso |
| Comunidade e Engajamento | Matrícula e Direito de Acesso | Liberar a sala de discussão de uma aula | Matrícula e Direito de Acesso |
| Atendimento e Suporte | Matrícula e Direito de Acesso | Diagnosticar e solicitar correção de acesso | Matrícula e Direito de Acesso |
| Atendimento e Suporte | Cobrança e Assinatura | Diagnosticar cobrança e solicitar reembolso | Cobrança e Assinatura |
| Todos os domínios | Identidade e Acesso | Identificar o ator e verificar sua permissão | Identidade e Acesso |
| Todos os domínios | Notificação | Pedir o envio de uma mensagem ao aluno | Domínio de origem (conteúdo) / Notificação (entrega e consentimento) |
| Inteligência de Negócio | Todos os domínios | Consumir eventos para consolidar indicadores | Domínio de origem |
| Auditoria e Conformidade | Domínios com ato administrativo | Registrar de forma imutável quem fez o quê | Auditoria e Conformidade |

**Leitura do grafo:** Matrícula e Direito de Acesso é o nó mais consultado do sistema (5 domínios perguntam a ele) e Identidade e Acesso e Notificação são transversais por natureza. Nenhum ciclo de dependência forte foi criado: os únicos pares bidirecionais são Aprendizagem ↔ Entrega de Mídia e Aprendizagem ↔ Avaliação, e em ambos os sentidos são assimétricos — um consulta, o outro informa fato consumado.

---

## Pontos de Atenção

**Sobreposições resolvidas**

- *Acesso ao curso × liberação da aula.* Dois conceitos que a linguagem do dia a dia chama de "liberar". Ficaram em domínios distintos: a pergunta comercial ("tem direito?") é de Matrícula e Direito de Acesso; a pergunta pedagógica ("já pode ver a próxima?") é de Aprendizagem e Progresso. Qualquer PRD que misture os dois deve ser recusado.
- *Dúvida × chamado.* A visão exige a separação; ela virou fronteira entre Comunidade e Engajamento e Atendimento e Suporte, com atores e permissões diferentes.
- *Oferta × currículo.* "Curso" significa coisas diferentes para o negócio e para o professor. Catálogo e Oferta é dono do vendável; Conteúdo e Currículo é dono do aprendível.
- *Identidade × direito.* Ser aluno não é ter acesso. Separados em Identidade e Acesso e Matrícula e Direito de Acesso.

**Candidatos a fusão** (manter separados por ora, revisar se o custo aparecer)

- *Fiscal* dentro de *Cobrança e Assinatura* — Fiscal é pequeno. Foi mantido separado pela assimetria de criticidade: falha fiscal não pode travar venda. Se a operação se provar simples e única, a fusão é aceitável.
- *Avaliação* dentro de *Aprendizagem e Progresso* — defensável enquanto houver só quiz objetivo. Deixa de ser no momento em que entrar correção dissertativa por professor.
- *Certificação* dentro de *Aprendizagem e Progresso* — rejeitada pela superfície pública não autenticada, que é uma exigência de outra natureza.

**Candidatos a divisão futura** (não dividir agora)

- *Comunidade e Engajamento* → gamificação vira domínio próprio se ganhar regras de campanha, temporada ou recompensa com valor econômico.
- *Vendas e Checkout* → afiliados vira domínio próprio se houver comissionamento com regra de apuração, repasse e extrato para o parceiro.
- *Matrícula e Direito de Acesso* → turmas (coortes) viram domínio próprio se ganharem cronograma, encontro ao vivo e acompanhamento de professor por turma. Hoje são agrupamento de matrículas.

**Lacunas materiais**

- A definição de *aluno ativo* não existe (ponto A1 da visão, dono: negócio) e é insumo de Inteligência de Negócio e de Comunidade e Engajamento. Não bloqueia a Fase 1.
- A política de retenção e anonimização de dados de aluno (LGPD) não foi definida e é insumo de Auditoria e Conformidade. Deve ser resolvida antes da fase que trata dados em escala, não na Fase 1.

**Alerta de escopo**

16 domínios contra 2 engenheiros. A Fase 1 da visão toca **7**: Identidade e Acesso, Catálogo e Oferta, Conteúdo e Currículo, Entrega de Mídia e Proteção, Matrícula e Direito de Acesso, Vendas e Checkout e Aprendizagem e Progresso — com Auditoria e Conformidade em versão mínima. Os outros 8 existem no mapa para que as fronteiras dos 7 primeiros sejam desenhadas sabendo o que virá, não para serem construídos agora.

---

## Decisões Estruturais Tomadas

| # | Decisão | Origem |
|---|---|---|
| DE01 | Direito de acesso é domínio próprio, não atributo de pedido nem de assinatura | Risco registrado na visão: "direito de acesso variável por oferta vazar regra pelo sistema" |
| DE02 | Catálogo e Oferta separado de Conteúdo e Currículo | Monetização múltipla (avulso, assinatura, turma) sobre o mesmo conteúdo |
| DE03 | Entrega de Mídia é domínio próprio, cego a curso e a preço | Concentração de custo (armazenamento, CDN) e estratégia de proteção própria e volátil |
| DE04 | Pré-requisito não gera dependência entre domínios; é atributo informativo do Catálogo | Decisão do negócio: recomendação pedagógica sem gate |
| DE05 | Liberação progressiva pertence a Aprendizagem, não a Direito de Acesso | Separação explícita na visão entre regra pedagógica e regra comercial |
| DE06 | Fiscal fora do caminho crítico da compra | Falha de emissão não pode impedir venda nem acesso |
| DE07 | Gamificação alojada em Comunidade e Engajamento | Evitar domínio sem dado próprio; mesma finalidade e mesmos eventos de entrada |
| DE08 | Turmas (coortes) são agrupamento de matrículas | Coorte é forma de conceder e acompanhar acesso, não domínio autônomo nesta fase |
| DE09 | Afiliados alojado em Vendas e Checkout | Atribuição de indicação nasce no pedido; comissionamento ainda não tem regra definida |
| DE10 | Nenhum domínio assume tenant único; isolamento por tenant é requisito de fronteira | Restrição da visão: mono-tenant sem decisão irreversível |
| DE11 | Inteligência de Negócio não possui dado e nunca escreve | Impedir que o painel vire fonte de verdade operacional |
| DE12 | Notificação é dona única do consentimento LGPD | Obrigação legal precisa de ponto único de verificação e descadastro |
| DE13 | Auditoria é domínio próprio e imutável, fora do alcance de quem pratica o ato | Exigência da visão de rastrear reembolso, nota e banimento |

---

*Domain Map gerado com o agente `tsg-flow-domain-decomposer`. Próximo passo sugerido: `tsg-flow-architecture-baseline`, que traduz estas fronteiras conceituais em regras estruturais de implementação.*

---

## Histórico de Revisões

| Versão | Data | Autor | Alterações |
|---|---|---|---|
| 1.0 | 2026-09-19 | Tasso Gomes | Decomposição inicial em 16 domínios conceituais a partir de `vision.md` v1.0 |
| 1.1 | 2026-09-20 | Tasso Gomes | Incorpora `vision.md` v1.1: a estratégia de proteção do domínio Entrega de Mídia e Proteção passa a ser própria, sem DRM contratado |
