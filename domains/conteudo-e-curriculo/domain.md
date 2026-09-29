---
tsg_artifact: domain
product: code-4-coders
version: 1.0
status: approved
updated: 2026-09-28
sources: vision.md@1.2, context/domain-map.md@1.2, backlog/capabilities.md@1.4, context/architecture-baseline.md@1.2, domains/entrega-de-midia-e-protecao/domain.md@1.0, domains/auditoria-e-conformidade/domain.md@1.2, domains/identidade-e-acesso/domain.md@1.1
---

# Domain Document — Conteúdo e Currículo

> Detalha o bounded context de **um** domínio do Domain Map. Não decide prioridade, ordem nem
> escopo de entrega — isso é do backlog de capacidades e do PRD. Forneça este arquivo junto com o
> `vision.md` ao iniciar um PRD de capacidade que toque este domínio.

**Domínio:** Conteúdo e Currículo
**Capacidades atendidas:** `CAP-005` (dono) · consumido por `CAP-003`, `CAP-007`, `CAP-017`, `CAP-018`, `CAP-020`
**Restrições arquiteturais pertinentes:** DE02, DE03, DE04, DE10 (Domain Map) · BA10/G07 (tenant em toda entidade) · G08 (um salto síncrono no caminho quente) · G13 (auditoria só por evento) · RN-18 de Identidade (permissão como claim)

---

## 1. Propósito do Domínio (Domain Purpose)

### Responsabilidade Principal

Estruturar o que se aprende — cursos, módulos, aulas, materiais complementares e a ordem pedagógica
entre eles — e publicar versões desse currículo para consumo.

### Problema que Resolve

O conteúdo é o ativo central da escola e evolui por decisão pedagógica: aula regravada, módulo
reordenado, material atualizado. Sem um dono próprio, essa evolução arrasta consigo preço, progresso
de aluno e histórico de compra — o professor não consegue corrigir uma aula sem medo de quebrar quem
já está no meio do curso, e o negócio não consegue vender o mesmo curso de formas diferentes sem
duplicá-lo. Este domínio separa **o aprendível** do **vendável** (DE02) e do **arquivo de vídeo**
(DE03), e faz da publicação um ato explícito, versionado e auditado.

### Fora do Escopo deste Domínio (Out of Scope)

- **Preço, condição comercial, vigência do acesso prometida, nível e pré-requisito** → Catálogo e
  Oferta. A visão (§ restrições) e DE04 põem nível e pré-requisito como atributos da **oferta**,
  ainda que o professor seja quem os sugere.
- **Receber, guardar, preparar, cifrar e entregar o vídeo e o material** → Entrega de Mídia e
  Proteção. Este domínio só **referencia** um ativo que Mídia já declarou pronto (OD30).
- **Decidir se um aluno pode acessar o curso** → Matrícula e Direito de Acesso.
- **Registrar o que cada aluno já assistiu, calcular conclusão e liberar a próxima aula** →
  Aprendizagem e Progresso (DE05). Este domínio define a ordem; não decide o ritmo.
- **Criar, corrigir e guardar avaliações** → Avaliação. Este domínio só declara **onde** uma
  avaliação se encaixa no currículo.
- **Guardar o registro imutável do ato de publicação** → Auditoria e Conformidade. Este domínio
  publica o fato; a Auditoria grava (RN-A02, RN-A14).
- **Definir quem é professor e que permissões o papel carrega** → Identidade e Acesso.
- **Dúvida pedagógica sobre a aula** → Comunidade e Engajamento.

---

## 2. Usuários do Domínio (Domain Users)

| Perfil (Role) | O que faz neste domínio | Frequência de uso |
|---|---|---|
| Professor / autor | Cria o curso, organiza módulos e aulas, vincula vídeo e material, publica e republica | Semanal, em ciclos de produção de curso |
| Administrador | Nenhuma ação de autoria por padrão (DP-03 de `CAP-002`: não herda áreas de outros papéis); consulta a publicação na trilha de auditoria | Eventual |
| Aluno | Percorre a versão publicada, sem perceber o domínio — sempre por Aprendizagem, Mídia e Catálogo | Diária |
| Aprendizagem e Progresso (domínio) | Lê a estrutura publicada a percorrer | A cada publicação |
| Catálogo e Oferta (domínio) | Referencia o curso publicado ao montar uma oferta | A cada oferta criada ou revista |
| Entrega de Mídia e Proteção (domínio) | Recebe as Referências de Uso de cada publicação | A cada publicação |

---

## 3. Entidades Principais (Core Entities)

| Entidade | Descrição | Atributos Principais | Relacionamentos |
|---|---|---|---|
| Curso | O aprendível: um conjunto ordenado de módulos sob um título. Pertence à escola, não a quem o criou | título, descrição pedagógica, autor original, escola (tenant), estado (nunca publicado, publicado) | contém: Módulo · tem: Rascunho, Versão de Publicação |
| Módulo | Agrupamento de aulas dentro do curso, na ordem pedagógica | título, posição | pertence a: Curso · contém: Aula |
| Aula | Menor unidade de conteúdo consumível. Referencia uma mídia de vídeo e pode anexar materiais. **Tem identidade estável entre versões** (RN-C07) | título, descrição, posição, vídeo vinculado | pertence a: Módulo · referencia: Mídia (externa) · anexa: Material Complementar |
| Material Complementar | Arquivo de apoio à aula (PDF, código-fonte, planilha). O arquivo é um Ativo Protegido de Mídia; aqui fica só o vínculo e o rótulo | rótulo, ativo referenciado | pertence a: Aula · referencia: Ativo Protegido do tipo material (externo) |
| Rascunho | O currículo em edição. É o único lugar onde se altera estrutura e vínculo. Nunca é visto por aluno | estrutura em edição, último editor, momento da última edição | pertence a: Curso · origina: Versão de Publicação |
| Versão de Publicação | Retrato imutável do currículo tornado disponível para consumo. Há no máximo uma **vigente** por curso | número, estrutura publicada, autor da publicação, momento, nota de versão (opcional) | pertence a: Curso · gera: Referências de Uso, ato de publicação |
| Encaixe de Avaliação | Posição, no currículo, de uma avaliação que pertence a Avaliação | avaliação referenciada, posição | pertence a: Curso ou Módulo · referencia: Avaliação (externa) |

---

## 4. Capacidades Atendidas (Capabilities Served)

| Capacidade | O que este domínio entrega a ela |
|---|---|
| `CAP-005` | Todo o ciclo: estruturar curso, módulos e aulas; vincular vídeo pronto e material; publicar e republicar versões; informar uso de ativos e o ato de publicação |
| `CAP-003` | O curso publicado que uma oferta referencia. Oferta só se monta sobre curso com versão vigente |
| `CAP-007` | Indiretamente: a Referência de Uso que Mídia confere antes de abrir sessão (RN-M10) nasce aqui |
| `CAP-017` | A estrutura publicada — ordem de módulos e aulas, com identidade estável de aula — que o aluno percorre |
| `CAP-018` | A ordem pedagógica sobre a qual Aprendizagem decide liberação progressiva |
| `CAP-020` | O ponto do currículo onde a avaliação se encaixa |

---

## 5. Juntas com Outros Domínios (Domain Joints)

> Herdadas da tabela de interações do Domain Map. A junta com Mídia foi refinada em 2026-09-25
> (OD30) e está registrada nos dois domain docs; a redação da linha do Domain Map é lacuna de
> redação (QC-05), não de fronteira.

### Depende de (Upstream)

| Domínio | O que consome | Tipo | Dono do dado | Criticidade |
|---|---|---|---|---|
| Entrega de Mídia e Proteção | Existência, escola e estado de um ativo, para permitir o vínculo | Síncrono (leitura, pelo backoffice) + evento (`midia.ativo-pronto`, `midia.preparacao-falhou`) | Entrega de Mídia e Proteção | **Alta** — sem ela não há vínculo (RN-C04) |
| Identidade e Acesso | Ator corrente e suas permissões de autoria | Claim na requisição | Identidade e Acesso | **Alta** — sem ela ninguém edita (RN-C10) |

**Junta com Mídia — como está decidida (OD30, OD35).** O professor envia o vídeo **a Mídia**, que o
prepara sem saber a que aula vai servir. O vínculo é ato **deste** domínio: o professor escolhe entre
os ativos prontos da escola, e este domínio confere o estado por uma visão própria alimentada pelos
fatos `midia.ativo-pronto` e `midia.preparacao-falhou` — não por consulta síncrona entre os dois
domínios. Ao publicar, este domínio informa por evento quais ativos cada aula usa; Mídia guarda o
identificador opaco (RN-M09) e o confere antes de abrir sessão (RN-M10).

### Fornece para (Downstream)

| Domínio | O que fornece | Tipo | Dono do dado | Criticidade |
|---|---|---|---|---|
| Entrega de Mídia e Proteção | Referências de Uso: quais ativos cada aula da versão vigente usa | Evento (fato consumado) | Este domínio (o vínculo) / Mídia (o registro opaco) | **Alta** — sem ela nenhuma sessão de reprodução abre (RN-M10) |
| Aprendizagem e Progresso | A estrutura da versão vigente a percorrer | Evento + leitura | Este domínio | **Alta** — `CAP-017` depende disto |
| Catálogo e Oferta | O curso publicado a referenciar numa oferta | Leitura | Este domínio | Alta |
| Auditoria e Conformidade | O ato de publicação de versão | Evento no envelope `auditoria.ato-praticado` | Este domínio (o fato) / Auditoria (o registro) | Média |
| Avaliação | O encaixe da avaliação no currículo | Leitura | Este domínio (a posição) / Avaliação (a avaliação) | Baixa até `CAP-020` |
| Inteligência de Negócio | Fatos de publicação | Evento | Este domínio | Baixa |

### Integrações Externas (External Integrations)

Nenhuma. Armazenamento e distribuição de arquivo são de Mídia.

---

## 6. Regras de Negócio (Business Rules)

| ID | Regra | Origem |
|---|---|---|
| RN-C01 | **Currículo é Curso → Módulo → Aula, em ordem pedagógica total.** Todo módulo tem posição única no curso e toda aula tem posição única no módulo. Não há aula fora de módulo nem módulo fora de curso | Domain Map (linguagem ubíqua) |
| RN-C02 | **O curso pertence à escola, não a quem o criou.** O autor original e o autor de cada publicação são registrados, mas a revogação do papel deles não retira, não trava e não despublica o curso. Todo curso carrega a escola (tenant) | OD40 · G07 · paralelo a RN-M04 |
| RN-C03 | **Todo ator com a permissão de autoria vê e edita todos os cursos da escola**, e pode publicá-los. Não existe curso privado de um professor | OD40 · coerente com OD33 |
| RN-C04 | **Aula só vincula vídeo que Mídia declarou pronto e que pertence à mesma escola.** Vídeo recebido, em preparação ou que falhou não é oferecido para vínculo; ativo de outra escola nunca é aceito, mesmo com identificador válido | RN-M05 · OD35 · G07 |
| RN-C05 | **Edição só acontece no Rascunho; aluno só enxerga Versão de Publicação.** Nenhuma alteração de rascunho — estrutura, título, vínculo — aparece para o aluno antes de uma publicação | Domain Map (Publicação) |
| RN-C06 | **Versão de Publicação é imutável.** Mudar o que o aluno vê exige publicar uma nova versão; a anterior nunca é editada | Domain Map · `CAP-005` |
| RN-C07 | **Aula e módulo têm identidade estável entre versões.** Reordenar, renomear ou trocar o vídeo de uma aula mantém a mesma aula; só remover e criar gera aula nova. É essa identidade que Aprendizagem usa para ancorar o progresso | OD41 · backlog (`CAP-005`: versão não invalida progresso) |
| RN-C08 | **Há uma única versão vigente por curso, e ela vale para todos os alunos.** Uma nova publicação substitui a anterior para quem já está no meio do curso e para quem começar depois. Não existe aluno preso a versão antiga | OD41 |
| RN-C09 | **Publicar não apaga progresso, compra nem direito.** Uma aula removida sai do percurso da versão vigente; o que Aprendizagem já registrou sobre ela é preservado lá, e como isso aparece para o aluno é decisão de Aprendizagem | backlog (`CAP-005`) · Domain Map (justificativa) |
| RN-C10 | **Só ator interno cuja permissão de autoria vem de um papel edita ou publica.** O aluno nunca edita. Quais papéis recebem a permissão é decisão de Identidade; o nome da permissão nasce no PRD | Identidade RN-12, RN-16, RN-18 |
| RN-C11 | **Publicação exige currículo completo:** ao menos um módulo, todo módulo com ao menos uma aula, e toda aula com vídeo pronto vinculado. Rascunho incompleto é salvo, mas não publica | Domain Map (Aula referencia mídia) · RN-C04 |
| RN-C12 | **Toda publicação informa as Referências de Uso da versão vigente**, identificando para cada ativo o curso — unidade sobre a qual Matrícula decide — e a aula — unidade sobre a qual Aprendizagem registra progresso. Para Mídia, os dois identificadores são opacos | OD30 · RN-M09 · RN-M10 · RN-M14 |
| RN-C13 | **Publicação é ato administrativo auditado.** Publicar uma versão comunica o ato no envelope `auditoria.ato-praticado`, com autor e alvo (o curso) identificados; **motivo não é obrigatório**. A nota de versão é opcional e sua ausência não torna o ato não conforme. A versão e o ato nascem juntos: não existe versão publicada sem ato comunicado | OD42 · OD19 · RN-A05 · RN-A14 · G13 |
| RN-C14 | **Este domínio não guarda preço, condição comercial, vigência, nível nem pré-requisito.** Tudo que muda por decisão comercial vive em Catálogo e Oferta; o curso pode ser vendido de várias formas sem que o currículo mude | DE02 · DE04 · visão (restrições) |
| RN-C15 | **Material Complementar é Ativo Protegido de Mídia.** Este domínio guarda só o vínculo e o rótulo; o arquivo nunca fica aqui e nunca é público. O vínculo segue RN-C04 (mesma escola, disponível em Mídia) e entra na Referência de Uso como qualquer ativo | OD31 · RN-M16 |
| RN-C16 | **Este domínio só declara onde uma avaliação se encaixa**, por referência à avaliação de Avaliação. Não cria, não corrige e não decide aprovação; condicionar o avanço à aprovação é de Aprendizagem | Domain Map · DE05 |
| RN-C17 | **Oferta só se monta sobre curso com versão vigente.** Curso nunca publicado não é referenciável por Catálogo | Domain Map (Catálogo referencia currículo publicado) · `CAP-003` |

---

## 7. Eventos do Domínio (Domain Events)

> O contrato real materializa no pacote de contratos e na TechSpec; aqui fica o fato de negócio.

### Produz (Publishes)

- `conteudo.versao-publicada` — uma nova versão do currículo passou a vigorar para um curso. Carrega
  a estrutura publicada (módulos e aulas com identidade estável) e os ativos que cada aula usa.
  Assinantes: Entrega de Mídia e Proteção (Referências de Uso, RN-M09), Aprendizagem e Progresso,
  Catálogo e Oferta, Inteligência de Negócio
- Ato `conteudo.versao-publicada` no envelope `auditoria.ato-praticado` — o contrato do envelope é
  da Auditoria (RN-A14); o tipo do ato é vocabulário deste domínio

### Consome (Subscribes)

- `midia.ativo-pronto` (de: Entrega de Mídia e Proteção) — o ativo passa a ser oferecido para vínculo
- `midia.preparacao-falhou` (de: Entrega de Mídia e Proteção) — o ativo nunca será oferecido para vínculo

---

## 8. Riscos de Fronteira (Boundary Risks)

| Risco | Probabilidade | Impacto | Mitigação |
|---|---|---|---|
| **Curso virar oferta** — nível, preço ou vigência entrarem no formulário de autoria porque "o professor sabe" | Alta | Alto | RN-C14 e Fora do Escopo: o professor pode sugerir; o atributo é da oferta (DE02, DE04) |
| **Nova versão arrastar progresso** — aula trocada virar aula nova e o aluno perder o que assistiu | Média | Alto | RN-C07 (identidade estável) e RN-C09; Aprendizagem ancora na aula, não na versão |
| **Rascunho vazar para o aluno** por alguém ler a estrutura em edição em vez da versão vigente | Média | Alto | RN-C05: leitura de fora do domínio só enxerga Versão de Publicação |
| **Virar dono paralelo de mídia** — guardar duração, qualidade ou arquivo do vídeo "para exibir" | Média | Médio | RN-C04 e RN-C15: só vínculo; o estado do ativo vem de fato de Mídia |
| **Aprendizagem copiar a estrutura e divergir** da versão vigente | Média | Médio | `conteudo.versao-publicada` como fato único de mudança; a estrutura tem um dono só |
| **Publicação sem ato auditado** quando a gravação da versão e a comunicação do ato se separam | Baixa | Alto | RN-C13: versão e ato nascem juntos |

---

## 9. Questões em Aberto (Open Questions)

- [x] **QC-01 — Retirar curso de circulação (despublicar). Fechada em 2026-09-28 (OD45):** fora do
      MVP. Quando entrar, é ato auditado com motivo obrigatório e exige decidir o efeito em Catálogo
      (a oferta some) e em Matrícula (quem tem direito vitalício continua vendo?).
- [ ] **QC-02 — Excluir curso nunca publicado.** Não afeta aluno nem oferta. → PRD de `CAP-005`.
- [ ] **QC-03 — Aula sem vídeo.** Confirmado em 2026-09-28 que haverá aulas só com artigo, texto ou
      recomendação de links. RN-C11 continua exigindo vídeo até essa revisão, que muda a definição de
      *Aula* do Domain Map ("referencia uma mídia") e o que Aprendizagem entende por concluir a aula.
      → revisão deste domain doc antes do PRD que introduzir aula sem vídeo.
- [x] **QC-04 — Dois autores editando o mesmo rascunho ao mesmo tempo. Fechada em 2026-09-28
      (OD44):** resolvido por processo interno da escola no início; o produto não bloqueia nem avisa
      edição concorrente. Revisitar se o conflito aparecer na operação.
- [ ] **QC-05 — Redação da junta no Domain Map.** A linha "Conteúdo solicita a preparação de uma
      mídia" deve passar a "Conteúdo consulta a mídia pronta e informa o uso ao publicar". Mesma
      lacuna de QM-06 no domain doc de Mídia. Redação acordada em 2026-09-28. → aplicar na próxima
      revisão do Domain Map.
- [x] **QC-06 — O que acontece com o aluno no meio do curso quando sai nova versão. Fechada em
      2026-09-28 (OD41):** vê a nova; aula com identidade estável. Descartado: aluno preso à versão
      em que começou, e o híbrido por data de início — os dois criam versões simultâneas vivas e
      obrigam Aprendizagem e Mídia a conhecer versão.
- [x] **QC-07 — De quem é o curso. Fechada em 2026-09-28 (OD40):** da escola; todo ator com a
      permissão de autoria edita. Descartado: curso do professor que criou — exigiria regra de
      transferência e deixaria curso órfão na saída do professor.
- [x] **QC-08 — Motivo na publicação. Fechada em 2026-09-28 (OD42):** não exigido; nota de versão
      opcional. Descartado: motivo obrigatório na republicação ou em toda publicação.
- [x] **QC-09 — Onde entra o ciclo de Material (QM-05 de Mídia). Fechada em 2026-09-28 (OD43):**
      não entra no primeiro PRD de `CAP-005`; vai para o segundo PRD de `CAP-005`, que atravessa
      Mídia (envio e guarda do material) e este domínio (vínculo). As regras já valem (RN-C15, RN-M16).

---

*Domain Doc gerado com a skill `tsg-flow-domain-creator`. Para criar o PRD de uma capacidade que
toca este domínio, use `tsg-flow-prd-creator` fornecendo o `vision.md`, este arquivo, os demais
domain docs que a capacidade atravessa e o ID da capacidade.*

---

## Histórico de Revisões

| Versão | Data | Autor | Alterações |
|---|---|---|---|
| 1.0 | 2026-09-28 | Tasso Gomes | Criação: bounded context, juntas com Mídia (OD30/OD35), Auditoria, Aprendizagem, Catálogo e Identidade; regras RN-C01 a RN-C17; decisões OD40 a OD45. Aprovado em 2026-09-28 |
