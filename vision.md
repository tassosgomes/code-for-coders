# Vision Document — code-4-coders

> **Nível 0 da hierarquia de documentação.** Este documento é a âncora de contexto para todos os Domain Docs, PRDs, Tech Specs e Tasks do projeto. Sempre que iniciar uma nova sessão com a IA, forneça este arquivo como contexto.

**Status:** aprovado (v1.1) · **Fonte primária:** `docs/draft.md` · **Data:** 2026-09-20

---

## 1. Visão Geral do Sistema (System Overview)

### Problema de Negócio

Operar uma escola online de programação hoje exige apoiar-se em plataformas prontas de terceiros (ex.: Hotmart). Essa dependência foi declarada como o ponto de partida do projeto: o negócio quer uma escola própria, construída do zero, cobrindo a experiência de ponta a ponta — da captação e onboarding do aluno à gestão financeira, suporte pedagógico, segurança de conteúdo e engajamento.

Sem um sistema próprio, a operação fica limitada a três frentes que a plataforma de terceiros controla: a **experiência pedagógica** (regras de liberação de aula, avaliação, certificação), a **relação financeira com o aluno** (recorrência, inadimplência, fiscal) e a **propriedade do relacionamento e dos dados** (comunicação, comunidade, analytics).

> Os custos concretos dessa dependência (taxa por transação, lock-in de dados, limites de personalização) não foram quantificados e estão registrados como hipótese em 7.3.

### Solução Proposta

Uma plataforma própria de educação online em programação que cobre o ciclo completo do aluno:

1. **Descobrir e comprar** — catálogo de cursos organizado por nível, checkout com meios de pagamento brasileiros, cupons e parcerias de indicação.
2. **Aprender** — player de vídeo proprietário com proteção de conteúdo, trilhas com liberação progressiva, avaliações automatizadas e anotações pessoais sincronizadas com o vídeo.
3. **Permanecer** — comunidade e fórum, gamificação, notificações multicanal e suporte separado entre dúvida pedagógica e problema técnico/financeiro.
4. **Comprovar** — certificados com validação pública verificável por terceiros.
5. **Ser operada** — backoffice com permissões por papel, trilha de auditoria e painel de indicadores de receita, engajamento e churn.

A plataforma nasce servindo **uma única escola**, com fronteiras de domínio desenhadas desde já para suportar **multi-tenant** em evolução futura, sem reescrita.

### Público-Alvo (Target Audience)

O aluno da code-4-coders é quem quer aprender ou aprofundar programação. O catálogo é segmentado em **três níveis — iniciante, intermediário e avançado**, e cursos podem declarar pré-requisitos. Nível e pré-requisito são **informação pedagógica que orienta a escolha, não controle de acesso**: a plataforma recomenda, exibe e explica, mas nunca impede a compra ou a matrícula de quem escolhe começar fora da ordem sugerida. A consequência de projeto é que a vitrine carrega a responsabilidade de orientar bem, já que não há trava para corrigir uma escolha ruim.

| Perfil (Role) | Descrição | Necessidade Principal |
|---|---|---|
| Aluno iniciante | Sem base prévia; busca o primeiro contato com programação | Caminho guiado, sem pré-requisito oculto, com sensação de progresso rápido |
| Aluno intermediário | Já programa; quer consolidar ou mudar de stack | Escolher o curso certo sem repetir o que já sabe |
| Aluno avançado | Profissional buscando especialização | Profundidade e comprovação do que concluiu |
| Visitante / Lead | Ainda não é aluno; avalia a oferta | Entender o nível, o conteúdo e o preço, e comprar sem atrito |
| Professor / Autor de conteúdo | Cria e publica cursos, módulos e avaliações | Publicar conteúdo, definir nível e pré-requisitos, responder dúvidas pedagógicas |
| Operação / Suporte | Atende problemas de acesso, cobrança e uso da plataforma | Diagnosticar e resolver casos de aluno com permissão restrita ao necessário |
| Financeiro | Cuida de faturamento, inadimplência, reembolsos e fiscal | Acompanhar recebimentos, agir sobre inadimplência e emitir documentos fiscais |
| Administrador do negócio | Responde pelo resultado da escola | Visão de receita, churn, engajamento e controle de quem pode o quê |

### Contexto de Entrada

- [x] Ideia nova (greenfield)
- [ ] Discovery com cliente
- [ ] Modernização de sistema legado

Não há sistema legado a preservar, e **migração de base de alunos ou histórico de plataforma externa está fora do escopo** — a plataforma parte de estado vazio.

---

## 2. Domínios Identificados (Domain Map)

**Seção deliberadamente não preenchida nesta etapa.**

A identificação de bounded contexts é responsabilidade do passo seguinte do fluxo (`tsg-flow-domain-decomposer`), que produzirá `context/domain-map.md`. Antecipar domínios aqui congelaria fronteiras antes da análise que deve defini-las.

Como insumo para esse passo, as **capacidades de negócio** que a visão reconhece — sem afirmar que cada uma é um domínio — são:

| # | Capacidade de negócio | Responsabilidade | Origem |
|---|---|---|---|
| C01 | Identidade e acesso | Contas, autenticação, papéis e permissões (RBAC) | Draft §4 |
| C02 | Catálogo e oferta | Cursos publicáveis, nível, pré-requisitos informativos, preços, cupons, vitrine | Draft §2 |
| C03 | Autoria e conteúdo | Criação de cursos, módulos, aulas, materiais | Draft §1 |
| C04 | Entrega de vídeo e proteção | Hospedagem, streaming, URL assinada, marca d'água | Draft §1, §5 |
| C05 | Aprendizagem e progresso | Trilhas, liberação progressiva, progresso, anotações | Draft §1 |
| C06 | Avaliação | Quizzes, provas, correção automática | Draft §1 |
| C07 | Certificação | Emissão e validação pública de certificados | Draft §4 |
| C08 | Vendas e checkout | Compra avulsa, matrícula, cupons, afiliados | Draft §2 |
| C09 | Cobrança e assinatura | Recorrência, inadimplência, bloqueio de acesso, reembolso | Draft §2 |
| C10 | Fiscal | Emissão de NFS-e | Draft §2 |
| C11 | Comunidade | Fórum, salas de discussão por aula | Draft §3 |
| C12 | Engajamento e gamificação | Pontos, badges, rankings | Draft §3 |
| C13 | Suporte | Central de ajuda, FAQ, tickets | Draft §3 |
| C14 | Notificação | E-mail, push e WhatsApp | Draft §5 |
| C15 | Analytics e BI | Receita, churn, engajamento, cursos mais vendidos | Draft §4 |
| C16 | Auditoria e governança | Log de ações administrativas | Draft §4 |

**Direito de acesso** (por quanto tempo o aluno acessa o que comprou) atravessa C02, C08 e C09 e é decisão estrutural, não detalhe de checkout — ver seção 5.

---

## 3. Mapa de Interdependências (Dependency Map)

**Dependências entre domínios:** não preenchidas nesta etapa — dependem do Domain Map (ver seção 2).

**Dependências externas** (estas, sim, são decisões de visão porque condicionam viabilidade e prazo):

| Dependência externa | Para que serve | Risco |
|---|---|---|
| Gateway de pagamento com cartão recorrente, PIX e boleto | C08, C09 | Alto — define o que é possível em cobrança e inadimplência |
| Provedor de emissão de NFS-e | C10 | Médio — varia por município |
| AWS (S3 + CloudFront ou equivalente) | C04 | Médio — custo cresce com audiência; é também o que sustenta a proteção de vídeo depois da decisão de não contratar DRM |
| WhatsApp Business API (provedor oficial) | C14 | Médio — aprovação de templates e custo por conversa |
| Provedor de e-mail transacional | C14 | Baixo |
| Identidade visual (time de design) | Frontend de vitrine, player e certificado | Médio — define quando a vitrine pode ir a público |

---

## 4. Roadmap Macro (High-Level Roadmap)

> Cada fase entrega valor verificável de ponta a ponta. Não há prazo fixo: o sequenciamento é por valor, e as fases posteriores ao MVP são proposta a confirmar com o negócio.

### Fase 1 — Vender e Assistir (MVP / Foundation)
**Objetivo:** um aluno descobre um curso, paga, entra e assiste — com o conteúdo protegido, o progresso registrado e o direito de acesso respeitado conforme o contrato da compra.
**Capacidades incluídas:** C01, C02 (mínimo, com nível), C03 (mínimo), C04, C05 (progresso), C08 (venda avulsa), C16 (mínimo)
**Critério de conclusão:** uma compra real é processada fim a fim, o acesso é liberado automaticamente com a vigência correta (período ou vitalício), e o aluno assiste a uma aula com progresso persistido e conteúdo protegido.

### Fase 2 — Receita Recorrente
**Objetivo:** sustentar a operação com assinatura e reduzir perda por falha de pagamento.
**Capacidades incluídas:** C09, C10, C14 (e-mail), C02 (cupons)
**Critério de conclusão:** uma assinatura renova sozinha, uma falha de cobrança dispara régua e bloqueia acesso conforme regra, e a venda gera documento fiscal.

### Fase 3 — Aprendizagem Comprovada
**Objetivo:** transformar consumo de vídeo em aprendizado verificável e comprovável.
**Capacidades incluídas:** C05 (liberação progressiva), C06, C07, C05 (anotações)
**Critério de conclusão:** um aluno conclui uma trilha condicionada a avaliação e obtém certificado validável publicamente por um terceiro.

### Fase 4 — Retenção e Comunidade
**Objetivo:** aumentar conclusão e permanência por pertencimento e suporte.
**Capacidades incluídas:** C11, C12, C13, C14 (push e WhatsApp)
**Critério de conclusão:** dúvida pedagógica e chamado de suporte correm em fluxos separados, e o engajamento é mensurável.

### Fase 5 — Escala e Governança
**Objetivo:** operar com dados, controle e abertura para novos formatos e produtores.
**Capacidades incluídas:** C15, C16 (completo), turmas/coortes, afiliados, preparação para multi-tenant
**Critério de conclusão:** o negócio decide com base no painel e a plataforma comporta um segundo produtor sem reescrita das fronteiras.

---

## 5. Restrições Globais (Global Constraints)

### Restrições Técnicas (Technical Constraints)
- **Stack obrigatória:** backend .NET / ASP.NET Core (C#) e frontend React + Vite + TypeScript — padrão do time, já vigente neste repositório.
- **Infraestrutura:** nuvem AWS, com S3 + CloudFront para armazenamento e distribuição de vídeo e materiais.
- **Integrações obrigatórias:** gateway de pagamento com suporte a cartão recorrente, PIX e boleto; serviço de emissão de NFS-e; canal WhatsApp.
- **Autenticação:** autenticação própria da plataforma, com RBAC para papéis internos (suporte, financeiro, professor, admin). Sem SSO corporativo.
- **Proteção de vídeo sem DRM:** não haverá provedor de DRM — decisão de custo. A proteção é construída pela
  própria plataforma: URL assinada de vida curta por sessão de reprodução, HLS com AES-128, marca d'água
  dinâmica com o e-mail do aluno sobreposta no player. **Não há limite de dispositivos nem de reprodução
  simultânea**: a plataforma mede a sobreposição de aulas distintas por aluno — o que não corresponde a estudo de
  uma pessoa — e decide com dado se alguma restrição se justifica. Isso dissuade
  gravação de tela casual e repasse de arquivo, e **não** impede extração por quem sabe o que está fazendo.
  Nenhuma comunicação de venda pode prometer exclusividade de conteúdo.
- **Multi-tenancy:** o MVP é mono-tenant, mas nenhuma fronteira de domínio pode assumir tenant único de forma irreversível.

### Restrições de Negócio (Business Constraints)
- **Modelos de monetização suportados:** assinatura recorrente, venda avulsa por curso e turmas com matrícula/coorte. A venda avulsa é o único obrigatório no MVP.
- **Direito de acesso configurável por oferta:** a duração do acesso não é regra global da plataforma. É definida **por curso e/ou por campanha de compra** — o contrato da compra determina se o acesso é por período ou vitalício. O modelo de oferta e de concessão de acesso precisa suportar ambos desde a Fase 1.
- **Segmentação por nível:** o catálogo expressa iniciante, intermediário e avançado; nível e pré-requisito são atributos da oferta. **Pré-requisito é recomendação pedagógica, sem gate**: não condiciona compra, matrícula nem acesso ao conteúdo. A única regra de liberação que a plataforma aplica é a progressão *dentro* de um curso (liberação progressiva, Fase 3) — nunca *entre* cursos.
- **Regulatório:** LGPD (dados de alunos, consentimento de comunicação, direito de exclusão) e obrigações fiscais brasileiras de prestação de serviço (NFS-e).
- **Mercado:** operação brasileira, em português, com meios de pagamento locais.
- **Prazo:** não há prazo definido. Prioridade é fatia de valor completa, não data.
- **Equipe e modo de execução:** dois engenheiros, com **toda a codificação executada de forma autônoma por agentes**. Os engenheiros atuam como definidores de escopo, revisores e donos dos gates de qualidade. Isso impõe duas condições estruturais: (a) a documentação (Vision → Domain → PRD → TechSpec → Tasks) é a entrada de produção, não subproduto — ambiguidade vira retrabalho; (b) o trabalho precisa ser fatiado em tarefas verticais pequenas com critério de aceite executável e gate automatizado, porque a revisão humana é o gargalo escasso.
- **Identidade visual:** será construída pelo time de design; não é definida nesta visão e não deve ser inventada pelos agentes.

### Non-Goals do Sistema
- Não será um marketplace aberto a produtores externos nesta versão — o suporte a múltiplos produtores é preparação de fronteiras, não funcionalidade entregue.
- Não migrará alunos, compras ou histórico de nenhuma plataforma externa.
- Não terá aplicativo mobile nativo; a entrega é web responsiva.
- Não fará produção, edição ou transcodificação autoral de vídeo além do necessário para entrega.
- Não incluirá aula ao vivo / webinar nesta versão.
- Não substituirá ferramenta de e-mail marketing e automação de funil — a plataforma notifica, não faz campanha.
- Não será sistema contábil ou ERP; emite documento fiscal e expõe dados, não fecha contabilidade.
- Não bloqueará compra, matrícula ou acesso por pré-requisito não cumprido — o aluno escolhe, a plataforma apenas informa.
- Não definirá identidade visual, marca ou design system — insumo externo do time de design.

---

## 6. Glossário de Negócio (Business Glossary)

> A coluna "Área de uso" aponta capacidades da seção 2, não bounded contexts.

| Termo | Definição | Área de uso |
|---|---|---|
| Nível do curso | Classificação do conteúdo em iniciante, intermediário ou avançado, usada para orientar a escolha do aluno | C02, C05 |
| Pré-requisito | Conhecimento ou curso que se recomenda ter antes de iniciar outro. É informativo e não restringe compra nem acesso | C02 |
| Oferta | Combinação de curso, preço, condição comercial e direito de acesso, exposta na vitrine | C02, C08 |
| Direito de acesso | Vigência do acesso do aluno ao conteúdo comprado — por período ou vitalício, conforme o contrato da compra | C02, C08, C09 |
| Campanha de compra | Condição comercial temporária que altera preço e/ou direito de acesso de uma oferta | C02, C08 |
| Trilha de aprendizagem | Sequência ordenada de módulos e aulas que define o caminho do aluno em um curso | C05 |
| Liberação progressiva (drip content) | Regra que só libera a próxima aula após concluir a anterior ou passar em uma avaliação | C05, C06 |
| Marca d'água dinâmica | Identificação do aluno sobreposta ao vídeo durante a reprodução, para desencorajar pirataria | C04 |
| Inadimplência | Situação do aluno cuja cobrança recorrente falhou ou venceu sem pagamento | C09 |
| Régua de cobrança | Sequência programada de avisos ao aluno inadimplente antes do bloqueio de acesso | C09, C14 |
| Churn | Taxa de cancelamento de assinaturas em um período | C15 |
| Certificado validável | Comprovante de conclusão com identificador único verificável publicamente por um terceiro | C07 |
| Badge | Insígnia concedida ao aluno por marco de progresso ou engajamento | C12 |
| Afiliado | Parceiro que indica vendas e é remunerado por elas | C08 |
| Cupom | Desconto aplicável na compra, por percentual ou valor fixo | C02, C08 |
| Turma (coorte) | Grupo de alunos que percorre um curso com data de início e acompanhamento comum | C08, C05 |
| Tenant | Escola/produtor cujos dados e marca são isolados dos demais na plataforma | Transversal |
| NFS-e | Nota Fiscal de Serviço Eletrônica, emitida no município do prestador | C10 |
| Dúvida pedagógica | Pergunta sobre o conteúdo do curso, respondida por professor ou comunidade — distinta de chamado de suporte | C11, C13 |
| Chamado de suporte | Solicitação sobre acesso, cobrança ou falha da plataforma, tratada pela operação | C13 |

---

## 7. Premissas e Riscos Globais (Assumptions & Risks)

### 7.1 Premissas (Assumptions)
- Existe conteúdo (cursos gravados) disponível ou em produção para publicar no lançamento.
- Os dois engenheiros têm disponibilidade para revisar e aprovar entregas no ritmo em que os agentes produzem.
- Haverá um ponto focal de negócio disponível para validar regras de cobrança, acesso e certificação.
- A escola conseguirá contratar gateway de pagamento e provedor de NFS-e em nome de pessoa jurídica própria.
- O volume inicial de alunos e a banda de vídeo cabem em custo de nuvem compatível com a receita da Fase 1.

### 7.2 Riscos Globais (Global Risks)
| Risco | Probabilidade | Impacto | Mitigação |
|---|---|---|---|
| Escopo amplo (16 capacidades) desproporcional a um time de dois engenheiros | Alta | Alto | Fase 1 estreita e fim a fim; capacidade posterior só entra com critério de conclusão definido |
| Documentação ambígua gerar código autônomo errado em escala | Alta | Alto | Vision/Domain/PRD/TechSpec como contrato; tarefas verticais pequenas com gate executável antes da revisão humana |
| Revisão humana virar gargalo e acumular entregas não validadas | Média | Alto | Limitar trabalho em progresso; gate automatizado reprovar antes de chegar ao revisor |
| Pirataria de conteúdo, agora sem DRM | Alta | Médio | Marca d'água com o e-mail do aluno, URL assinada e HLS AES-128. Compartilhamento de conta passa a ser o vetor principal e é **medido, não bloqueado**: sobreposição recorrente de aulas distintas do mesmo aluno é a assinatura do desvio, e a restrição só entra se o dado mostrar volume que a justifique. Restringir antes disso cobraria o erro do aluno legítimo. Proteção é dissuasão declarada, não garantia |
| Custo de storage e CDN crescer mais rápido que a receita | Média | Alto | Monitorar custo por aluno ativo desde a Fase 1; rever provedor antes de escalar |
| Direito de acesso variável por oferta ser tratado como caso especial e vazar regra pelo sistema | Média | Alto | Modelar concessão de acesso como conceito próprio desde a Fase 1, não como flag no checkout |
| Complexidade de recorrência, inadimplência e fiscal ser subestimada | Alta | Alto | Isolar cobrança atrás de fronteira própria; MVP só com venda avulsa |
| Decisão de multi-tenant ser adiada e virar reescrita | Média | Alto | Domain Map tratar isolamento por tenant como requisito de fronteira desde o início |
| Dependência de terceiros (gateway, NFS-e, WhatsApp) bloquear entrega | Média | Médio | Contratar e validar integrações antes da fase que depende delas |
| Ausência de prazo reduzir senso de prioridade | Média | Médio | Critério de conclusão por fase como marco substituto da data |

### 7.3 Base de Evidências

**Fatos** (declarados pelo usuário ou verificáveis no repositório)
- F1 — O objetivo é construir a escola do zero, sem depender de plataformas prontas como Hotmart. (`docs/draft.md`)
- F2 — As 16 capacidades da seção 2 vêm da pesquisa em `docs/draft.md`.
- F3 — MVP mono-tenant, com fronteiras preparadas para multi-tenant.
- F4 — Monetização prevista: assinatura recorrente, venda avulsa e turmas/coorte.
- F5 — Primeiro recorte: vender e assistir fim a fim.
- F6 — Stack .NET + React é padrão do time e vigora neste repositório. (skills `dotnet` e `react`)
- F7 — Restrições confirmadas: AWS (S3 + CloudFront); PIX, boleto, NFS-e e LGPD.
- F8 — Catálogo segmentado em três níveis: iniciante, intermediário e avançado.
- F9 — Migração de alunos/histórico está fora do escopo.
- F10 — Não há prazo definido.
- F11 — Equipe de dois engenheiros; toda a codificação é autônoma.
- F12 — Nome do produto: code-4-coders. Identidade visual será feita pelo time de design.
- F13 — Direito de acesso é definido por curso e/ou campanha de compra, podendo ser por período ou vitalício.
- F14 — Pré-requisito entre cursos é recomendação pedagógica informativa; a plataforma não aplica gate de acesso por ele.
- F15 — Não haverá contratação de DRM; a proteção de vídeo é própria, com marca d'água exibindo o e-mail do aluno. (decisão do time, 2026-09-20)
- F16 — Não há limite de dispositivos logados nem de reprodução simultânea. Compartilhamento de conta é medido (sobreposição de aulas distintas do mesmo aluno) e não bloqueado; restrição só com dado que a justifique. Sessão única, trava de reprodução e limite por IP foram considerados e descartados. (decisão do time, 2026-09-20)

**Hipóteses** (plausíveis, ainda não confirmadas)
- H1 — Taxa por transação e lock-in de dados da plataforma atual são a motivação econômica central.
- H2 — O sequenciamento das Fases 2 a 5 corresponde à prioridade do negócio.
- H3 — A venda avulsa é suficiente para validar a Fase 1 sem assinatura.
- H4 — O nível do curso (iniciante/intermediário/avançado) é atributo do curso inteiro, não de módulo ou aula.

**Pontos em aberto** (com responsável; bloqueiam decisões futuras, não esta visão)
- A1 — Métricas de sucesso do produto (conversão, conclusão, churn, ticket médio) — **negócio**, necessário antes de encerrar a Fase 1.
- A2 — Identidade visual e design system — **time de design**, necessário antes de a vitrine ir a público.

---

## 8. Histórico de Revisões (Revision History)

| Versão | Data | Autor | Alterações |
|---|---|---|---|
| 0.1 | 2026-09-19 | Tasso Gomes | Versão inicial a partir de `docs/draft.md` e decisões de escopo, monetização, primeira entrega e restrições |
| 0.2 | 2026-09-19 | Tasso Gomes | Nome do produto, segmentação por nível, migração fora de escopo, ausência de prazo, equipe de dois engenheiros com codificação autônoma, direito de acesso configurável por oferta, identidade visual como insumo externo |
| 0.3 | 2026-09-19 | Tasso Gomes | Pré-requisito definido como recomendação pedagógica informativa, sem gate de acesso entre cursos |
| 1.0 | 2026-09-19 | Tasso Gomes | Visão aprovada |
| 1.1 | 2026-09-20 | Tasso Gomes | Decisão de não contratar DRM: proteção de vídeo passa a ser própria (marca d'água com e-mail do aluno, URL assinada, HLS AES-128) e de medir — em vez de bloquear — indício de compartilhamento de conta. DRM removido das dependências externas, risco de pirataria reescrito e nova restrição técnica em §5 |

---

*Vision Doc gerado com o agente `tsg-flow-vision-creator`. Próximo passo: `tsg-flow-domain-decomposer`, que produz `context/domain-map.md`.*
