---
tsg_artifact: domain
product: code-4-coders
version: 1.0
status: approved
updated: 2026-09-25
sources: vision.md@1.2, context/domain-map.md@1.2, backlog/capabilities.md@1.4
---

# Domain Document — Entrega de Mídia e Proteção

> Detalha o bounded context de **um** domínio do Domain Map. Não decide prioridade, ordem nem
> escopo de entrega — isso é do backlog de capacidades e do PRD. Forneça este arquivo junto com o
> `vision.md` ao iniciar um PRD de capacidade que toque este domínio.

**Domínio:** Entrega de Mídia e Proteção
**Capacidades atendidas:** `CAP-006`, `CAP-007`
**Restrições arquiteturais pertinentes:** BA07, BA08, BA09, BA14, BA15, BA16 · G06, G07, G08, G10, G11, G21, G22, G23, G24, G26

---

## 1. Propósito do Domínio (Domain Purpose)

### Responsabilidade Principal

Guardar, preparar e entregar o vídeo e os materiais pesados **somente ao aluno autorizado, no
momento autorizado**, com uma proteção que dissuade a cópia sem prometer impedi-la.

### Problema que Resolve

O conteúdo é o ativo do negócio, e o projeto nasceu para tirá-lo de plataforma de terceiro. Este
domínio resolve três dores que mudam por razão técnica e de custo, não pedagógica nem comercial:

1. **Colocar o ativo sob controle próprio** — o professor envia um arquivo e a plataforma o torna
   reproduzível, sem que ele fique exposto em lugar nenhum.
2. **Entregar sem vazar** — o aluno autorizado assiste; quem não é autorizado, ou deixou de ser, não
   obtém o arquivo por link, por cache nem por reuso de sessão.
3. **Dissuadir o repasse sem punir o pagante** — sem DRM (BA15), a proteção é uma pilha barata:
   nada público, URL assinada de vida curta, HLS com AES-128 e marca d'água com o e-mail do aluno.
   Ela desencoraja o repasse casual e **não** detém quem quer extrair.

É também o domínio que concentra o custo de armazenamento e de banda (R10). Isolá-lo (DE03) é o
que permite trocar provedor, formato ou estratégia de proteção sem tocar em regra pedagógica ou
comercial.

### Fora do Escopo deste Domínio (Out of Scope)

- **Decidir quem pode assistir** → Matrícula e Direito de Acesso. Este domínio **pergunta**, a cada
  abertura de sessão, e nunca guarda uma decisão própria (G11).
- **Saber o que é curso, módulo, aula, preço ou progresso** → Conteúdo e Currículo, Catálogo e
  Oferta, Aprendizagem e Progresso (DE03). Do uso de um ativo, este domínio guarda só uma
  referência opaca (RN-M09).
- **Estruturar e publicar o currículo, e decidir qual ativo entra em qual aula** → Conteúdo e
  Currículo.
- **Concluir aula, liberar etapa, calcular percentual** → Aprendizagem e Progresso. Este domínio
  informa o avanço como fato bruto (RN-M14).
- **Saber quem é o ator e o que ele pode fazer** → Identidade e Acesso. A permissão de enviar ativo
  vive no catálogo de lá (RN-M03).
- **Detectar e tratar caso individual de compartilhamento de conta** → `CAP-029` (G25). Este domínio
  só produz o fato bruto do qual a medição agregada deriva (BA16).
- **Produzir ou editar vídeo** — cortar, legendar, compor. A preparação faz só o necessário para a
  entrega (visão, "o que não fará").
- **DRM, trava de concorrência, sessão única, limite por IP** — rejeitados (BA15, BA16, G24, G26).
- **Registro de auditoria do envio** — enviar ou preparar ativo não é ato administrativo com efeito
  sobre aluno ou dinheiro; não gera evento para Auditoria e Conformidade.

---

## 2. Usuários do Domínio (Domain Users)

| Perfil (Role) | O que faz neste domínio | Frequência de uso |
|---|---|---|
| Professor | Envia vídeo e material, acompanha a preparação até ficar pronto ou falhar | Semanal, em ciclos de produção de curso |
| Aluno | Reproduz a aula e baixa material pelo player, sem perceber o domínio | Diária |
| Conteúdo e Currículo (domínio) | Consulta se um ativo está pronto antes de vinculá-lo; informa quais ativos cada conteúdo publicado usa | A cada vínculo e a cada publicação |
| Aprendizagem e Progresso (domínio) | Recebe o avanço da reprodução | Contínua durante a reprodução |

A ação mais crítica do professor é **enviar e saber se ficou pronto**; a do aluno é **apertar play e
o vídeo começar** — é o caminho crítico (b) do baseline.

---

## 3. Entidades Principais (Core Entities)

| Entidade | Descrição | Atributos Principais | Relacionamentos |
|---|---|---|---|
| Ativo Protegido | Qualquer arquivo sob guarda do domínio que nunca é público. Tem dois tipos: **vídeo** e **material** | tipo, estado (recebido, em preparação, pronto, falhou), autor do envio, momento do envio, escola (tenant) | é um de: Mídia, Material · usado por: Referência de Uso |
| Mídia | O Ativo Protegido do tipo vídeo. É o nome pelo qual os demais domínios o conhecem | duração, estado da preparação | gera: Versão de Reprodução · reproduzida em: Sessão de Reprodução |
| Material | O Ativo Protegido do tipo arquivo complementar (PDF, código-fonte, planilha). Não é preparado: é entregue como foi enviado | formato, tamanho | entregue por: acesso individual de vida curta |
| Versão de Reprodução | A forma entregável da Mídia: segmentada e cifrada, **única para todos os alunos**, com uma Chave de Cifra por Mídia | qualidades disponíveis, chave de cifra | pertence a: Mídia |
| Referência de Uso | Registro **opaco** de que um conteúdo publicado usa um ativo. O domínio não sabe o que o identificador significa, só que foi informado por Conteúdo e Currículo | identificador opaco do conteúdo, ativo, escola | liga: Ativo Protegido ↔ conteúdo (externo) |
| Sessão de Reprodução | Autorização temporária e individual para um aluno reproduzir **um** ativo no contexto de **um** conteúdo | aluno, ativo, referência de uso, validade | concedida após: decisão de Matrícula · compõe: Marca d'Água |
| Marca d'Água | A identificação do aluno sobreposta ao vídeo durante a reprodução, com posição variável, composta no cliente a partir da sessão | conteúdo exibido (e-mail do aluno), regra de variação | pertence a: Sessão de Reprodução |

---

## 4. Capacidades Atendidas (Capabilities Served)

> Só referência. Prioridade, fase, dependência entre capacidades e ordem de implementação vivem em
> `backlog/capabilities.md`.

| Capacidade | O que este domínio entrega a ela | Situação |
|---|---|---|
| `CAP-006` | Receber o vídeo, preparar a Versão de Reprodução, expor o estado a quem enviou e deixar a Mídia pronta consultável para vínculo | Não iniciada — fatia do primeiro PRD em OD29 |
| `CAP-007` | Abrir Sessão de Reprodução após a decisão de acesso, entregar segmentos e chave por acesso de vida curta, fornecer ao player o necessário para a Marca d'Água e informar o avanço | Não iniciada |

Ambas derivam de `C04` na visão.

**Material** é entidade deste domínio (Domain Map: "vídeo e materiais pesados"), mas nenhuma das
duas capacidades o nomeia; `CAP-005` é quem anexa material à aula. Qual PRD entrega o ciclo de
Material está em QM-05.

### Estado de entrega do domínio

> Fotografia em 2026-09-25. Registra o que já existe em `src/media`, não redefine regra.

| Elemento | Entregue | Pendente |
|---|---|---|
| Fundação | Serviço, esteira e portas neutras de armazenamento e distribuição com adaptadores S3/CloudFront que devolvem referência determinística, sem chamada de rede | Adaptadores reais → primeira capacidade |
| Entidades e regras | — | Todas → `CAP-006` e `CAP-007` |
| Preparação | Decidida: ffmpeg dentro do próprio serviço (OD28) | — |

---

## 5. Juntas com Outros Domínios (Domain Joints)

> Herdadas da tabela de interações do Domain Map. O refinamento da junta com Conteúdo e Currículo
> está explicado abaixo da tabela upstream.

### Depende de (Upstream)

| Domínio | O que consome | Tipo | Dono do dado | Criticidade |
|---|---|---|---|---|
| Matrícula e Direito de Acesso | "Este aluno pode acessar este conteúdo agora?" | Síncrono, no caminho quente, 1 salto (G08) | Matrícula e Direito de Acesso | **Alta** — sem resposta, não há reprodução (falha fechada, BA07) |
| Identidade e Acesso | O aluno corrente, com o e-mail para a Marca d'Água; o ator interno e a permissão de enviar ativo | Síncrono, por claim validada localmente (sem round-trip) | Identidade e Acesso | **Alta** |
| Conteúdo e Currículo | Quais ativos cada conteúdo publicado usa (Referência de Uso) | Evento (fato consumado) | Conteúdo e Currículo (o vínculo) / este domínio (o registro opaco) | **Alta** — sem ela nenhuma sessão abre (RN-M10) |

**Junta com Conteúdo e Currículo — decidida em 2026-09-25 (OD30).** O Domain Map diz que Conteúdo
"solicita a preparação de uma mídia". Na prática do backlog, o professor envia o vídeo **a este
domínio**, e Conteúdo só referencia uma Mídia que já está pronta. A junta fica assim:

- **Envio e preparação** acontecem aqui, pedidos pelo professor pelo backoffice. A Mídia não sabe a
  que aula vai servir; ela existe antes do vínculo e independe dele.
- **Vínculo** é ato de Conteúdo. Antes de vincular, Conteúdo consulta este domínio para saber se o
  ativo existe, pertence à escola e está pronto.
- **Referência de Uso.** Ao publicar uma versão, Conteúdo informa por evento quais ativos cada
  conteúdo usa. Este domínio guarda só o identificador opaco. Na abertura de sessão, ele confere se
  o conteúdo alegado está entre as referências do ativo **antes** de perguntar a Matrícula.
- **Por que assim:** Matrícula decide por conteúdo, não por arquivo. Sem a referência, um aluno com
  direito ao curso A reproduziria qualquer mídia do curso B informando o identificador de A.
  Perguntar a Conteúdo na hora seriam dois saltos síncronos no caminho quente, o que viola G08.
  A referência opaca mantém DE03 (cego a curso) e, de brinde, diz se um ativo está em uso — base
  para a futura exclusão (QM-04).
- **Descartado:** confiar no conteúdo informado pelo BFF (brecha acima); consultar Conteúdo na
  abertura da sessão (G08).

O texto da linha do Domain Map ("solicitar a preparação") fica como lacuna de redação para a
próxima revisão do mapa (QM-06). A fronteira não muda.

### Fornece para (Downstream)

| Domínio | O que fornece | Tipo | Dono do dado | Criticidade |
|---|---|---|---|---|
| Conteúdo e Currículo | Existência, escola e estado de um ativo, para permitir o vínculo | Síncrono (leitura) | Este domínio | Alta |
| Conteúdo e Currículo | O fato de que uma Mídia ficou pronta ou falhou | Evento | Este domínio | Média |
| Aprendizagem e Progresso | O avanço bruto da reprodução, com a Referência de Uso da sessão | Evento | Aprendizagem (o progresso) / este domínio (o fato) | **Alta** — `CAP-017` depende disto |
| Inteligência de Negócio | Fatos de reprodução e o sinal de custo de armazenamento e distribuição | Evento / métrica | Este domínio | Média — R10 e BA16 |

### Integrações Externas (External Integrations)

| Sistema Externo | Finalidade | Direção |
|---|---|---|
| AWS S3 | Guardar o arquivo enviado e a Versão de Reprodução, sem nenhum objeto público | Saída |
| AWS CloudFront | Distribuir os segmentos por URL assinada de vida curta | Saída |

Os dois ficam atrás de camada anticorrupção (baseline, regra 8): nenhum nome de bucket, distribuição
ou conceito do provedor aparece em contrato ou em outro domínio. A preparação roda com ffmpeg
dentro do serviço (OD28): é ferramenta interna, não integração externa.

---

## 6. Regras de Negócio (Business Rules)

| ID | Regra | Origem |
|---|---|---|
| RN-M01 | **Nenhum Ativo Protegido é acessível publicamente.** Segmento, chave, arquivo enviado e material só são obtidos por acesso individual, de vida curta, emitido depois de uma decisão de acesso positiva. Um link copiado deixa de funcionar ao fim da validade e não serve para outro ativo | G21 · BA15 |
| RN-M02 | **O domínio é cego a curso, preço e progresso.** Não guarda título de curso ou aula, preço nem percentual. Do uso de um ativo, guarda só a Referência de Uso opaca (RN-M09) | DE03 |
| RN-M03 | Só envia ativo o **ator interno cujo papel carrega a permissão de envio de mídia**, definida no catálogo de Identidade e Acesso. O aluno nunca envia. Quais papéis recebem a permissão é decisão de Identidade, não daqui | Identidade RN-12, RN-16, RN-18 |
| RN-M04 | **O ativo pertence à escola, não a quem o enviou.** O autor do envio é registrado, mas a revogação do papel dele não retira o ativo, não interrompe a preparação e não desfaz vínculo. Todo ativo carrega a escola (tenant) | G07 · BA10 |
| RN-M05 | **Estado do ativo:** recebido → em preparação → pronto, ou → falhou. A transição só anda para frente. Só ativo **pronto** pode ser vinculado e reproduzido. **Falhou** é terminal e só é declarado depois de esgotadas as tentativas automáticas; o professor reenvia como um novo ativo | `CAP-006` |
| RN-M06 | O estado da preparação é **visível a quem enviou**, incluindo o motivo da falha em linguagem de quem produz o vídeo (arquivo corrompido, formato não aceito), nunca como erro técnico | `CAP-006` |
| RN-M07 | **A Versão de Reprodução é produzida uma única vez por Mídia e é a mesma para todos os alunos.** É segmentada e cifrada (HLS com AES-128), com uma Chave de Cifra por Mídia. Nenhuma versão, cópia ou chave é derivada por aluno | G22 · BA15 · R10 |
| RN-M08 | **A Chave de Cifra só sai do domínio para uma Sessão de Reprodução válida daquela Mídia**, e pelo próprio domínio, nunca pela CDN nem num endereço estável | Baseline (proteção de conteúdo) |
| RN-M09 | **Referência de Uso** é registrada quando Conteúdo e Currículo informa, por evento de publicação, que um conteúdo usa um ativo. O domínio não interpreta o identificador. Uma referência a ativo que ainda não está pronto é guardada, mas nenhuma sessão abre até o ativo ficar pronto (RN-M05) | OD30 |
| RN-M10 | **Abrir Sessão de Reprodução exige, nesta ordem:** (1) o conteúdo alegado estar entre as Referências de Uso do ativo; (2) Matrícula responder que o aluno pode acessar aquele conteúdo **agora**. Sem resposta de Matrícula, a sessão **não** abre (falha fechada). A resposta pode ser reaproveitada por no máximo 30 segundos; nenhuma decisão é guardada além disso | BA07 · G11 · OD30 |
| RN-M11 | **A Sessão de Reprodução é individual e de vida curta:** um aluno, um ativo, uma Referência de Uso. Estender a reprodução repete a decisão de RN-M10; uma sessão não sobrevive por mais tempo que a própria validade a uma revogação de acesso. Os valores de validade são do PRD | BA07 · `CAP-007` |
| RN-M12 | **A Marca d'Água é composta no cliente a partir da sessão, com o e-mail do aluno, em posição que varia durante a reprodução.** Nunca é gravada no arquivo. O e-mail chega ao player dentro da sessão e não aparece em URL, nome de objeto, chave de cache de CDN, log, métrica nem routing key | G22 · G23 · Identidade RN-21 |
| RN-M13 | **Nenhuma restrição de concorrência.** O mesmo aluno pode ter várias sessões ao mesmo tempo, em dispositivos diferentes. IP não é sinal de autorização nem de suspeita | BA16 · G24 · G26 · Identidade RN-09 |
| RN-M14 | **O avanço da reprodução é informado como fato bruto** (sessão, Referência de Uso, posição), com granularidade controlada — nunca um fato por segundo assistido. Concluir aula e calcular progresso é de Aprendizagem e Progresso | Domain Map · baseline (premissa 4) |
| RN-M15 | **A proteção é dissuasão declarada.** Nenhum texto de produto, PRD ou TechSpec apresenta o conteúdo como protegido contra cópia ou como exclusividade garantida | BA15 · R8 |
| RN-M16 | **Material** é Ativo Protegido: não é público, não é preparado, e é entregue por acesso individual de vida curta depois da mesma decisão de RN-M10. RN-M07 e RN-M08 (versão e chave) não se aplicam a ele | OD31 · Domain Map |
| RN-M17 | **O custo de armazenamento e distribuição por aluno ativo é sinal obrigatório** emitido por este domínio desde a primeira capacidade entregue | R10 · baseline (sinais obrigatórios) |

---

## 7. Eventos do Domínio (Domain Events)

> O contrato real materializa no pacote de contratos e na TechSpec; aqui fica o fato de negócio.
> Publicação sempre por outbox (G06).

### Produz (Publishes)

- `midia.ativo-pronto` — a Mídia tem Versão de Reprodução e pode ser vinculada e reproduzida
- `midia.preparacao-falhou` — a preparação esgotou as tentativas; o ativo não ficará pronto
- `midia.reproducao-avancou` — fato bruto de avanço de uma sessão, com a Referência de Uso,
  consumido por Aprendizagem e Progresso. Não carrega e-mail nem dado pessoal além do identificador
  opaco do aluno

### Consome (Subscribes)

- Publicação de versão do currículo (de: Conteúdo e Currículo) — registra as Referências de Uso
  (RN-M09). O nome e o formato do evento são de Conteúdo e Currículo; este domínio é um dos
  assinantes

---

## 8. Riscos de Fronteira (Boundary Risks)

| ID | Risco | Probabilidade | Impacto | Mitigação |
|---|---|---|---|---|
| RF-M01 | **O domínio aprender o que é curso** — guardar título de aula para exibir no backoffice, ou decidir acesso por curso — e virar dono paralelo de Conteúdo ou de Matrícula | Média | Alto | RN-M02 e RN-M10: só referência opaca; a decisão é sempre perguntada, nunca guardada |
| RF-M02 | **A proteção ser vendida como garantia** em peça de venda, PRD ou TechSpec | Média | Alto | RN-M15; limite honesto registrado no baseline |
| RF-M03 | **Custo por aluno crescer** por uma decisão que parece de segurança: marca d'água gravada no arquivo, versão ou chave por aluno, sessão que fura o cache da CDN | Média | Alto | RN-M07, RN-M12, RN-M17; alerta de taxa de acerto de cache (G22) |
| RF-M04 | **A Sessão de Reprodução virar um direito de acesso paralelo** — sessão longa ou renovada sem nova decisão continua tocando depois de uma revogação | Média | Alto | RN-M10 e RN-M11: validade curta e renovação com nova decisão |
| RF-M05 | **O provedor vazar pela fronteira** — nome de bucket, distribuição ou URL do provedor em contrato ou em outro domínio, travando uma futura troca | Média | Médio | Camada anticorrupção; portas neutras já existentes em `src/media` |
| RF-M06 | **A preparação disputar recurso com a entrega** no mesmo serviço — o ffmpeg consome CPU em rajada; a entrega precisa de latência baixa | Média | Médio | A preparação é assíncrona e isolada do caminho de entrega; a forma de isolar é da TechSpec de `CAP-006` (OD28) |
| RF-M07 | **O e-mail do aluno vazar pelo caminho legítimo da Marca d'Água** — parâmetro de URL, nome de objeto, log do player | Média | Alto | RN-M12; G23 |

---

## 9. Questões em Aberto (Open Questions)

- [ ] **QM-01 — Qualidades da Versão de Reprodução.** Uma só, ou uma escada adaptativa? Pesa em
      custo de armazenamento e de preparação (R10) e na experiência em rede móvel. → PRD de `CAP-006`.
- [ ] **QM-02 — Formatos aceitos e limite de tamanho do envio**, de vídeo e de material. → PRD de
      `CAP-006` (vídeo) e de quem entregar material (QM-05).
- [ ] **QM-03 — Visibilidade dos ativos entre atores internos.** Só quem enviou vê o estado e
      pode vincular, ou todo ator com a permissão? RN-M04 diz que o ativo é da escola; a decisão é
      de experiência de uso. → PRD de `CAP-006`.
- [ ] **QM-04 — Retenção do arquivo original, substituição e exclusão de ativo.** Fora do MVP
      (OD29). A Referência de Uso já permite saber se um ativo está em uso quando isso entrar.
- [ ] **QM-05 — Qual PRD entrega o ciclo de Material** (envio, guarda e entrega). As regras estão
      aqui (RN-M16); falta decidir se entra no PRD de `CAP-005`, que anexa o material, ou exige
      ajuste no backlog. → planejamento de `CAP-005`.
- [ ] **QM-06 — Redação da junta no Domain Map.** A linha "Conteúdo solicita a preparação de uma
      mídia" deve passar a "Conteúdo consulta a mídia pronta e informa o uso ao publicar" (OD30). É
      lacuna de redação, não de fronteira. → próxima revisão do Domain Map.
- [ ] **QM-07 — Validade da sessão e do acesso de vida curta, e cadência do avanço.** → PRD de
      `CAP-007`.
- [x] **QM-08 — Onde roda a preparação. Fechada em 2026-09-25 (OD28):** ffmpeg dentro do serviço;
      S3 + CloudFront só guardam e distribuem. Descartado: transcodificação gerenciada da AWS.
- [x] **QM-09 — Como Mídia confere a que conteúdo um ativo pertence. Fechada em 2026-09-25
      (OD30):** Referência de Uso opaca informada por Conteúdo na publicação. Detalhe em §5.
- [x] **QM-10 — Materiais pesados neste domínio. Fechada em 2026-09-25 (OD31):** Ativo Protegido
      com dois tipos, vídeo e material, sob as mesmas regras de sigilo; a preparação vale só para
      vídeo.

---

## Histórico de Revisões

| Versão | Data | Autor | Alterações |
|---|---|---|---|
| 1.0 | 2026-09-25 | Tasso Gomes | Criação: bounded context, juntas com Matrícula, Identidade e Conteúdo (Referência de Uso), regras RN-M01 a RN-M17 |

*Domain Doc gerado com a skill `tsg-flow-domain-creator`. Para criar o PRD de uma capacidade que
toca este domínio, use `tsg-flow-prd-creator` fornecendo o `vision.md`, este arquivo, os demais
domain docs que a capacidade atravessa e o ID da capacidade.*
