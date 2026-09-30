---
tsg_artifact: techspec
product: code-4-coders
capability: CAP-003
version: 1.0-draft
status: in_review
updated: 2026-09-30
sources: tasks/prd-vitrine-oferta/prd.md@1.0, tasks/prd-vitrine-oferta/contracts.md@1.0, context/architecture-baseline.md@1.2, domains/catalogo-e-oferta/domain.md@1.1, tasks/prd-nivel-prerequisito-curso/contracts.md@1.0
---

# TechSpec — vitrine e oferta de curso

> **Escopo:** Full-stack (`commerce` módulo Catálogo, `bff-admin`, `admin-spa`, `bff-student`, `student-spa`; mudanças pontuais em `identity` e `audit`)
> **Modo:** Pipeline, API-First
> **PRD de origem:** [prd.md](prd.md), v1.0, aprovado em 2026-09-30
> **Contratos de integração:** [contracts.md](contracts.md) 1.0; OpenAPI [do backoffice](api-contract.yaml) 1.0.0, [da vitrine](api-contract-student.yaml) 1.0.0 e [interno de `commerce`](internal-api-contract-commerce.yaml) 1.1.0; AsyncAPI [de `commerce`](asyncapi-contract.yaml) 1.0.0 e [da Auditoria](asyncapi-contract-audit.yaml) 1.3.0 — todos aprovados em 2026-09-30
> **Data:** 2026-09-30
> **Status:** Em Revisão
> **Handoff:** draft — não gerar Tasks

## Resumo Executivo

O módulo Catálogo de `commerce` (hoje sem conteúdo de negócio) passa a ter duas metades:

- **Uma visão própria, somente leitura, dos cursos publicados**, mantida pelo consumo de `conteudo.versao-publicada.v1` (1.1.0). Ela guarda o que a vitrine exibe e o que as regras exigem (título, descrição, nível, pré-requisito, títulos de módulos e aulas) e é substituída por inteiro a cada fato mais novo. Nenhuma chamada a `learning` existe no caminho da vitrine nem na regra de nível (contrato C-01, G08).
- **A ficha de vitrine e as ofertas**, um agregado por curso (curso + até 50 ofertas) com as regras de limite, estado, nível exigido para publicar, elegibilidade de vitrine e `offerRevision`. Toda mudança que o PRD audita grava, **na mesma transação**, a oferta, o fato `catalogo.oferta-*` e o ato `oferta-*` no outbox do módulo (G06). O contador de cliques em *Comprar* é uma agregação diária por oferta, sem identificador.

`bff-admin` e `admin-spa` ganham a área **Catálogo**; `bff-student` e `student-spa` ganham a vitrine e a página do curso, **sem sessão**, com o `bff-student` autenticando-se em `commerce` por asserção de serviço (ADR-0009, nova). `identity` recebe a permissão `oferta.editar` no papel financeiro e `audit` aceita os três tipos de ato.

**Trade-off primário:** a visão própria tira `learning` do caminho quente e deixa a regra "só publica oferta de curso com nível" local e transacional; em troca, a vitrine é eventualmente consistente com a publicação do curso e depende da entrega dos fatos e do **reenvio inicial** de `learning` (CAP-005, ainda não implementado) para conhecer os cursos já publicados.

## Arquitetura da Solução

```text
financeiro → admin-spa (/admin/catalogo…) → bff-admin → commerce  (JWT de ator, audiência commerce, oferta.editar)
                                                          │  catalog.* (visão do curso, ofertas, contador, recibos)
                                                          │  outbox do módulo ──► commerce.events: catalogo.oferta-{publicada,alterada,despublicada}.v1
                                                          │                  └─► audit.events:    auditoria.ato-praticado.v1 (origem catalogo)
learning.events: conteudo.versao-publicada.v1 (1.1.0) ──► fila do Catálogo ──► visão do curso
visitante → student-spa (/student/cursos…) → bff-student (rotas anônimas) → commerce (asserção de serviço, ADR-0009)
```

**URLs públicas.** Compose: vitrine `http://localhost:8082/student/cursos` (filtro `?nivel=iniciante|intermediario|avancado`), página `http://localhost:8082/student/cursos/{courseId}`; backoffice `http://localhost:8081/admin/catalogo` e `http://localhost:8081/admin/catalogo/{courseId}`. Coolify de desenvolvimento: `https://c4c-student.lab.tasso.dev.br/student/cursos[/{courseId}]` e `https://c4c-admin.lab.tasso.dev.br/admin/catalogo[/{courseId}]`. A base (`/student/`, `/admin/`) vem do `BASE_PATH` de cada SPA; `paths.ts` guarda só a rota relativa (G15). Links entre páginas (cartão → página, recomendado → página) usam o `courseId` (C-07), nunca o título.

**Pré-requisitos de entrega (herdados, fora desta spec).** (1) Identity com `oferta.editar` no catálogo de permissões já está nos contratos de [acesso interno](../prd-acesso-interno/internal-api-contract.yaml) (C-10); a mudança de código é V-01. (2) O fato 1.1.0 e o **reenvio único** vêm de [prd-nivel-prerequisito-curso](../prd-nivel-prerequisito-curso/techspec.md) (estágio `tasks` no `flow-state.json`; nenhum código encontrado em `src/learning`). Sem eles, `learning` publica o fato 1.0.0 (sem descrição, nível nem pré-requisito): o Catálogo o aplica como curso **sem nível** (contrato), o que permite construir e evidenciar V-01 a V-06 e V-09/V-10, mas **nenhum curso entra na vitrine** até existir nível. V-07 e V-08 só têm evidência ponta a ponta com nível real (ver Riscos).

### Bloco Backend

**Visão do curso (réplica derivada).** Uma linha por curso e escola, com o número da versão aplicada, título, descrição, nível (nulo quando ausente), pré-requisito (texto e recomendados `{courseId, título da publicação}`) e a estrutura (módulos e títulos de aulas, **sem `videoId`**). Não é currículo do Catálogo: nada a edita, e cada fato mais novo a substitui por inteiro (RN-O02 preservada no seu sentido — o Catálogo não é dono nem edita). Aplicação do fato:

- aplica se `versionNumber` > aplicado; igual ou menor é confirmado sem efeito. `eventId` é fixo por versão, então a deduplicação por `eventId` decorre da regra de versão;
- **exceção de compatibilidade:** fato com a mesma `versionNumber` **e** formato 1.1.0 (as propriedades `description`, `level` e `prerequisite` presentes, mesmo nulas) substitui a linha derivada de uma mensagem 1.0.0 (propriedades ausentes). Sem isso, o reenvio de `learning` seria ignorado para o curso cuja publicação 1.0.0 chegou antes (ver Riscos). O registro guarda se o que tem veio de formato 1.0.0 ou 1.1.0;
- fato 1.0.0 é aplicado como nível nulo, pré-requisito vazio e descrição vazia;
- a aplicação é idempotente e atômica, e **serializa com as mutações de oferta do mesmo curso** (travando a linha do curso): uma oferta nunca é publicada com base num nível que uma versão concorrente acabou de remover;
- depois de aplicar, recalcula a elegibilidade de vitrine (abaixo). Versão sem nível tira o curso da vitrine **sem alterar o estado das ofertas** (DP-04).

**Agregado ficha + ofertas.** O curso conhecido tem ficha implícita (C-06: não há "criar ficha"); `tagline` é opcional, de 1 a 160 caracteres (DP-03), limpa com `null`. Ofertas: nome de 1 a 60 caracteres, `priceCents` de 1 a 9 999 999, `accessPeriod` `months` (1 a 60, inteiro) ou `lifetime`, estado `draft`/`published`/`unpublished`, até 50 por curso (o 51º → `FIELD_INVALID` no campo `offers`; teto declarado no contrato). Transições:

| Ação | De → para | Regra |
|---|---|---|
| criar | — → `draft` | curso conhecido; `offerRevision` = 1 |
| alterar | qualquer estado | mesmos limites; valores idênticos ao gravado não mudam nada (sem revisão, fato nem ato) |
| excluir | `draft` → removida | só rascunho; `published`/`unpublished` → `OFFER_STATE_CONFLICT` (RN-O11) |
| publicar | `draft`/`unpublished` → `published` | versão vigente com nível, senão `COURSE_LEVEL_REQUIRED`; oferta já publicada → `OFFER_STATE_CONFLICT`; fato + ato |
| despublicar | `published` → `unpublished` | só publicada; fato + ato |

`offerRevision` cresce em toda mudança persistida da oferta (criação, edição em qualquer estado, publicação, despublicação), e vai no fato. Toda operação sobre uma oferta trava a linha do curso dono (curso e ofertas são um agregado: no máximo 50 ofertas por trava). O conteúdo aprovado pelos limites é validado no domínio; o contrato só repete o formato.

**Elegibilidade de vitrine (definição única).** Um curso está na vitrine quando o nível da versão vigente não é nulo **e** existe ao menos uma oferta `published`. O momento em que ele **passa a estar** na vitrine é guardado na linha do curso (`in_showcase_since`): preenchido quando a elegibilidade passa de falsa a verdadeira, limpo quando passa de verdadeira a falsa, e recalculado na mesma transação por toda operação que a afeta (publicar, despublicar, aplicar fato). É a chave de ordenação "do mais recentemente colocado na vitrine" (desempate por `courseId`). O mesmo predicado alimenta `inShowcase` no backoffice e `inShowcase` dos recomendados na página pública.

**Atos e fatos (G06, G13).** Publicar, despublicar e alterar preço ou vigência de oferta **publicada** gravam no **outbox do módulo Catálogo** dois itens na transação da mudança: o fato `catalogo.oferta-*` (exchange de `commerce`) e o ato `auditoria.ato-praticado` (exchange da Auditoria), com `fatoId` do ato = `eventId` do fato. Autor = `sub` do JWT, escola = `tenantId`, `praticadoEm` = `occurredAt`, sem motivo (OD50). Alteração só de nome, ou de oferta em rascunho/despublicada, não gera nenhum dos dois (DP-05). Mudança de preço e vigência na mesma chamada gera **um** ato com os dois pares. Criar, excluir, editar rascunho e `tagline` não geram mensagem.

**Outbox do Catálogo.** O outbox existente de `commerce` vive no schema `sales` (`OutboxMessageConfiguration.cs:11`) e atende o heartbeat de plataforma; o Catálogo ganha o **seu** `outbox_messages` no schema `catalog`, como `learning` fez para o módulo `content`, e o publicador passa a drenar os dois. O publicador de `commerce` ainda precisa, como o de `learning`, enviar `auditoria.ato-praticado.v1` ao exchange da Auditoria e propagar o cabeçalho `correlationId` exigido pelos contratos (hoje só `traceparent`).

**Fatos sem consumidor.** Com `mandatory: true` e nenhuma fila ligada a `catalogo.oferta-*.v1`, o broker devolve a mensagem e o publicador trata como falha, esgotando as tentativas do outbox. A topologia de `commerce` declara, por isso, uma fila de retenção limitada (`commerce.catalog-offer-facts`, quorum, `x-max-length` e `x-message-ttl`, com `x-overflow: drop-head`; RabbitMQ 4.3 aceita os três em filas quorum) ligada às três chaves, como ponto de assinatura do futuro consumidor (Inteligência de Negócio). Ninguém a drena; o registro histórico dos fatos é o próprio outbox, que não é apagado.

**Consumo do fato de versão.** Fila de `commerce` ligada à chave `conteudo.versao-publicada.v1` do exchange de `learning` (declarado de forma idempotente na partida, para tolerar a ordem de subida), com DLX/DLQ e limite de entregas do padrão do serviço. Erro permanente (JSON inválido, `tenantId` ausente) vai à DLQ sem retry; falha transitória não confirma e é repetida.

**Cliques em Comprar.** Soma em agregação **diária (UTC) por oferta e escola**: contador numa linha por `(escola, oferta, dia)`, incrementado atomicamente. Nenhuma linha por clique, nenhuma coluna de sessão, cookie, navegador ou IP. Verificação e incremento são uma só instrução que exige a oferta `published` da escola na condição (despublicada ou de outra escola → 404 sem contar; nada de ler-e-depois-gravar). A `Idempotency-Key` do clique é guardada só como **hash SHA-256** num recibo com validade de 24 horas, gravado na mesma transação do incremento; repetição devolve 202 sem contar de novo. O backoffice lê o total de cada oferta como a soma de todos os dias (RF-09, "desde a publicação": só se clica enquanto publicada).

**Idempotência das escritas do backoffice (G09).** Recibo por (escola, ator, chave) com hash do pedido e resposta guardada por 24 horas, como nas escritas de autoria em `learning`; mesma chave com corpo diferente → 422 `IDEMPOTENCY_KEY_REUSED`. Repetir `publishOffer` com a mesma chave devolve a mesma resposta e **não** cria segundo fato nem segundo ato.

**Autenticação e tenant.**
- *Rotas de ator:* JWT de Identity com audiência `commerce`, validado por JWKS (já existente para a área financeira). Política nova: `oferta.editar` no claim `permissions`; a consulta de rótulos exige o papel `administrador` no claim `roles`. O tenant do contexto vem do claim `tenantId`; o autor, de `sub`.
- *Rotas públicas:* asserção de serviço do `bff-student` ([ADR-0009](../../docs/adr/0009-autenticacao-de-servico-bff-aluno-em-servicos-de-dominio.md)), escopos `showcase:read` e `purchase-intent:write`, `jti` consumido em Valkey. É um **segundo esquema de autenticação** no serviço: rota pública aceita só a asserção, rota de ator só o JWT de ator. O tenant do contexto vem da asserção verificada.
- Todas as consultas e recibos carregam `tenant_id` no filtro global (G07). Oferta ou curso de outra escola é indistinguível de inexistente.

**Leitura pública.** A vitrine e a página saem de consultas indexadas sobre `catalog.*`, com lista de campos **explícita** (nada de autor, e-mail ou identificador de pessoa; nenhum `videoId`). `summary` do cartão é a `tagline` ou, sem ela, o início da descrição cortado em 200 caracteres (sem cortar no meio de palavra é detalhe de UI; o corte é do servidor). Títulos dos recomendados: o título **atual** quando o Catálogo conhece o curso (visão do próprio módulo), senão o da publicação (C-02). As respostas públicas saem com `Cache-Control: no-store`: não há cache nesta entrega (ver Decisões).

### Bloco Frontend

**`admin-spa` — área Catálogo** (`/catalogo`, `/catalogo/:courseId`; item de menu **Catálogo** com `permission: 'oferta.editar'`, hoje a lista de áreas está em `get-staff-areas.ts`).
- *Lista:* cursos publicados por título, nível (ou texto "Sem nível"), se estão na vitrine e contagem de ofertas por estado; paginada. Estado do servidor só no cache de consultas.
- *Ficha:* título, nível e pré-requisito em leitura, com a indicação de que quem os altera é o professor, no curso (link para a Autoria quando o usuário tiver `autoria.ler`); aviso permanente quando o nível é nulo ("nenhuma oferta pode ser publicada até o professor declarar o nível"), mas criação de rascunho continua habilitada; campo de chamada comercial com contador de 160 e orientação de não prometer exclusividade nem proteção contra cópia (RN-O06); lista de ofertas (rascunhos primeiro), com estado, preço, vigência e "N cliques em Comprar".
- *Formulário de oferta:* o preço é digitado em reais e convertido para centavos **sem ponto flutuante**, recusando mais de duas casas; vigência alterna entre "Por período" (meses inteiros, 1 a 60) e "Vitalícia"; erros `FIELD_INVALID` viram mensagem no campo nomeado em `detail`.
- *Confirmações:* publicar mostra o **cartão exato** que o visitante verá (nome, preço, vigência); alterar preço ou vigência de oferta publicada mostra antes e depois e a frase "vale para compras futuras e não altera quem já comprou"; alterar só o nome não pede essa confirmação; despublicar e excluir rascunho pedem confirmação. A chave de idempotência nasce por intenção e é reutilizada na repetição após 504.
- *Consulta da trilha (RF-10):* rótulos, filtros e detalhe dos três atos (V-10).

**`student-spa` — área pública** (`/cursos`, `/cursos/:courseId`). Rotas **fora** do carregador `requireStudentSession`, em layout público próprio (marca, links para entrar e cadastrar); o aluno autenticado vê o mesmo (RN-O13). O hook global de eventos de sessão só age em rotas da conta, então a página pública não redireciona para o login.
- *Vitrine:* filtro de nível como grupo de opções com rótulo e estado anunciado (Todos, Iniciante, Intermediário, Avançado), guardado no endereço como `?nivel=` (`iniciante`/`intermediario`/`avancado`, traduzidos para o enum do contrato no cliente); estado vazio que oferece "Todos"; "a partir de R$ X" quando `offerCount` > 1; paginação de 12.
- *Página do curso:* título, nível, descrição, "Recomendamos saber antes" (texto e recomendados, com link só quando `inShowcase`; nunca como condição), estrutura (módulos e títulos de aulas, sem vídeo) e uma opção de compra por oferta. Vigência sempre com o ponto de partida: "Acesso por 12 meses, contados a partir da liberação" (singular: "Acesso por 1 mês, contado a partir da liberação") ou "Acesso vitalício". Preço em reais com centavos, em texto.
- *Comprar:* o clique **sempre abre o aviso** "compra em breve" (focável e anunciado, sem pedir dado), **independentemente** do resultado de `registerPurchaseIntent`; 404/429/5xx na contagem não escondem o aviso nem mostram erro ao visitante. A chave de idempotência é gerada por clique e não é persistida no navegador.
- *404 e vazio:* endereço de curso que saiu da vitrine, inexistente ou de outra escola mostra "este curso não está disponível" com link para a vitrine (mesma tela para os três casos).
- Nenhum texto gerado pela plataforma afirma exclusividade de conteúdo ou proteção contra cópia (RN-O06). Descrição, chamada e nomes vêm do servidor e são renderizados como **texto**, nunca como HTML.

> Biblioteca de fetching, formulários, estrutura de pastas e geração de tipos seguem a skill `react`; não há desvio.

## Mapa de Fatias Verticais

A ordem de construção está em `Bloqueado por`. Toda fatia cruza as pontas que o comportamento exige. Telas só começam depois do EN-01.

### V-01: o financeiro abre o Catálogo e vê os cursos publicados da escola

- **Cobre:** RF-01, RF-02 (lista e aviso de curso sem nível); RN-O01, RN-O14, RN-O18; Conteúdo RN-C17; Identidade RN-12, RN-16, RN-17, RN-18; DP-01; US "criar ofertas sobre curso publicado" (pré-condição: o financeiro enxerga os cursos).
- **Entrada / gatilho:** fato `conteudo.versao-publicada.v1` (1.0.0 ou 1.1.0) na fila do Catálogo; `listCatalogCourses` pelo `admin-spa`; menu **Catálogo**.
- **Processamento:** `identity` inclui `oferta.editar` no papel financeiro (professor, suporte e administrador não a recebem); `bff-admin` valida a sessão em Identity com audiência `commerce`, exige `oferta.editar` na sessão **e** repassa o JWT; `commerce` exige a permissão no claim e lista por título, da escola do `tenantId`; o consumidor cria/atualiza a visão do curso pelas regras de versão e de compatibilidade 1.0.0/1.1.0; `admin-spa` mostra menu e lista (texto "Sem nível" quando nulo).
- **Saída observável:** financeiro vê o item **Catálogo** e só os cursos publicados da escola; curso nunca publicado não aparece; papel sem a permissão não vê o item e recebe 403 na rota direta; revogar o papel recusa a próxima ação na sessão aberta (as ofertas já publicadas não mudam).
- **Evidência / checkpoint:** Compose com Identity, Learning, RabbitMQ, PostgreSQL e Valkey: publicar um curso em `learning` (fato 1.0.0 real) e abrir `http://localhost:8081/admin/catalogo` como financeiro → curso listado sem nível; as mesmas chamadas como professor, suporte e administrador → 403, e menu sem o item; `listCatalogCoursesInternal` direto sem a permissão → 403, com JWT de outra escola → lista só daquela escola; reentrega do mesmo fato e fato de versão menor não alteram a linha; fato 1.1.0 de mesma versão substitui o 1.0.0 (nível e descrição passam a existir); fato inválido vai à DLQ; revogar o papel e repetir a ação → 401/403.
- **Bloqueado por:** EN-01 (só a parte de tela).

### V-02: o financeiro abre a ficha do curso e escreve a chamada comercial

- **Cobre:** RF-02; RN-O02, RN-O06 (orientação na tela), RN-O03 (aviso); DP-03 (160 caracteres).
- **Entrada / gatilho:** abrir `/catalogo/{courseId}`; `getCatalogCourse`; salvar chamada com `updateCatalogCourse`.
- **Processamento:** `commerce` devolve a ficha com a visão do curso (título, nível, pré-requisito com título atual de cada recomendado), `tagline`, `inShowcase` e as ofertas (vazias até V-03); grava a `tagline` validada (1–160, `null` limpa) sob o bloqueio do curso e com recibo de idempotência; `admin-spa` mostra o aviso de nível nulo, o contador e a orientação.
- **Saída observável:** ficha com nível e pré-requisito sem ação de alteração; chamada salva e relida; 161 caracteres recusados com campo e limite; curso inexistente ou de outra escola → 404 `CATALOG_COURSE_NOT_FOUND`.
- **Evidência / checkpoint:** cenários do contrato para `getCatalogCourse` e `updateCatalogCourse` (200/400/403/404/422); repetição com a mesma chave não regrava; corpo diferente com a mesma chave → `IDEMPOTENCY_KEY_REUSED`; teste do SPA para aviso, contador e orientação.
- **Bloqueado por:** V-01.

### V-03: o financeiro cria, edita e exclui ofertas em rascunho

- **Cobre:** RF-03; RN-O01, RN-O04, RN-O07, RN-O08, RN-O11 (exclusão de rascunho); Matrícula RN-D04 (forma da vigência); DP-03; US "criar ofertas", "mais de uma forma".
- **Entrada / gatilho:** `createOffer`, `updateOffer` (rascunho), `deleteOffer` no formulário da ficha.
- **Processamento:** o agregado valida nome, preço, vigência e o teto de 50; nasce `draft` com `offerRevision` 1; edição incrementa a revisão; exclusão só de rascunho; tudo sob o bloqueio do curso e com recibo de idempotência; nenhuma mensagem é gravada; `admin-spa` converte reais em centavos sem ponto flutuante e confirma a exclusão.
- **Saída observável:** as duas ofertas do PRD (R$ 497,00 por 12 meses e R$ 897,00 vitalícia) coexistem como rascunho; preço 0, negativo ou acima de R$ 99.999,99, e vigência 0, fracionada ou acima de 60 meses são recusados com o campo; rascunho excluído deixa de existir; a mesma criação reenviada com a mesma chave gera uma só oferta.
- **Evidência / checkpoint:** `createOffer`/`updateOffer`/`deleteOffer` contra o OpenAPI, incluindo valores-limite (1, 9 999 999; 1, 60 meses) e o 51º rascunho; chamadas concorrentes de criação com a mesma chave → uma oferta; edição idêntica não muda `offerRevision`; teste do SPA para a conversão de preço (R$ 497,00 → 49700; "497,005" recusado).
- **Bloqueado por:** V-02.

### V-04: o financeiro publica a oferta — ela nasce com autor na trilha

- **Cobre:** RF-04; RN-O03, RN-O15, RN-O18; Auditoria RN-A05, RN-A06, RN-A08, RN-A14; Catálogo eventos; DP-05 (o que **não** audita).
- **Entrada / gatilho:** `publishOffer` após a confirmação do cartão; republicar oferta `unpublished`.
- **Processamento:** o agregado exige versão vigente com nível (`COURSE_LEVEL_REQUIRED`), estado `draft`/`unpublished` (`OFFER_STATE_CONFLICT` caso contrário), grava `published`, `publishedAt`, `offerRevision`+1 e recalcula `in_showcase_since`; na mesma transação entram no outbox do Catálogo o fato `catalogo.oferta-publicada.v1` e o ato `oferta-publicada` (`fatoId` = `eventId`); publicador drena o outbox do Catálogo, envia o ato ao exchange da Auditoria com `correlationId`, e a fila de retenção absorve o fato; `audit` aceita os três tipos novos (origem `catalogo`, alvo `oferta`, sem motivo) e grava o ato como conforme; `admin-spa` mostra o cartão na confirmação e o estado `published`.
- **Saída observável:** oferta `published`; fato conforme `OfertaPayload` (`offerRevision` crescente); registro `oferta-publicada` conforme na trilha, com o financeiro como autor e a oferta como alvo; curso sem nível → recusa com o motivo, oferta continua em rascunho, **nada** no outbox; republicar oferta despublicada gera novo ato.
- **Evidência / checkpoint:** Compose com Audit 1.3.0 **antes** do Catálogo publicar (ordem de implantação): publicar → uma publicação, um fato e um ato conformes; duplo clique com a mesma chave → um de cada; falha forçada entre a gravação da oferta e o commit → nada nasce (atomicidade); sem nível → 422 sem mensagens; o publicador completa o ciclo com a fila de retenção ligada e **esgota** as tentativas se ela não existir (teste que documenta a razão da fila); `audit` marca ato de `catalogo` com alvo que não seja `oferta` como não conforme.
- **Bloqueado por:** V-03.

### V-05: o financeiro altera preço ou vigência de oferta publicada — só para compras futuras

- **Cobre:** RF-05; RN-O09 (forma congelável), RN-O10, RN-O15; DP-05; US "corrigir o preço".
- **Entrada / gatilho:** `updateOffer` sobre oferta `published` depois da confirmação antes/depois.
- **Processamento:** o agregado compara com o gravado; mudança de preço e/ou vigência grava a revisão, o fato `catalogo.oferta-alterada.v1` (com `previous`) e o ato `oferta-alterada` com `precoAnterior`/`precoNovo` (centavos, em texto) e/ou `vigenciaAnterior`/`vigenciaNova` (`vitalicia` ou `12m`) só do que mudou; mudança só de nome grava e não emite nada; oferta em rascunho ou despublicada edita em silêncio.
- **Saída observável:** a vitrine passa a mostrar o valor novo; a trilha mostra `oferta-alterada` com anterior e novo; nome mudado aparece sem ato; a tela de confirmação diz que vale para compras futuras.
- **Evidência / checkpoint:** R$ 497,00 → R$ 397,00: fato e ato com os pares; 12 meses → vitalícia: só o par de vigência; preço e vigência juntos: **um** ato com os dois pares; só nome: nenhuma mensagem; valores idênticos: nenhuma mudança; teste do SPA para o texto "compras futuras".
- **Bloqueado por:** V-04.

### V-06: o financeiro despublica a oferta — sai da vitrine sem afetar quem comprou

- **Cobre:** RF-06; RN-O11, RN-O12, RN-O15; DP-04 (efeito inverso, ver V-07).
- **Entrada / gatilho:** `unpublishOffer` após confirmação; `deleteOffer` de oferta despublicada.
- **Processamento:** grava `unpublished`, revisão+1, recalcula `in_showcase_since` (limpa quando era a última publicada), fato `catalogo.oferta-despublicada.v1` e ato `oferta-despublicada` na mesma transação; `deleteOffer` de `unpublished` → `OFFER_STATE_CONFLICT`; `admin-spa` não oferece "Excluir" em oferta despublicada.
- **Saída observável:** a página do curso mostra só as outras ofertas; sem nenhuma publicada, o curso sai da vitrine e o endereço público mostra "não disponível"; a oferta segue visível no backoffice como *despublicada*; `oferta-despublicada` na trilha.
- **Evidência / checkpoint:** duas ofertas publicadas, despublicar uma → a outra continua e `in_showcase_since` permanece; despublicar a última → curso fora da vitrine; chamada direta de exclusão → 422; repetição com a mesma chave → um fato e um ato.
- **Bloqueado por:** V-04.

### V-07: o visitante abre a vitrine, filtra por nível e só vê o que está à venda

- **Cobre:** RF-07; RN-O03, RN-O04, RN-O05, RN-O12, RN-O13, RN-O18; Conteúdo RN-C18; DP-04; US "filtrar por nível".
- **Entrada / gatilho:** abrir `/student/cursos` (com ou sem `?nivel=`), com ou sem cookie de sessão de aluno; `listShowcaseCourses`.
- **Processamento:** `bff-student` trata `/api/v1/showcase/**` como rota **anônima** (ignora cookie, sem CSRF), assina a asserção de serviço (audiência `commerce`, escopo `showcase:read`, `tenantId` da configuração do BFF) e chama `listShowcaseCoursesInternal`; `commerce` verifica a asserção ([ADR-0009](../../docs/adr/0009-autenticacao-de-servico-bff-aluno-em-servicos-de-dominio.md): assinatura, `kid`, emissor, audiência, escopo, tenant, validade, `jti` em Valkey), filtra por nível, ordena por `in_showcase_since` decrescente e desempata por `courseId`, calcula menor preço e número de ofertas publicadas; `student-spa` renderiza cartões, filtro no endereço, estado vazio e "a partir de".
- **Saída observável:** só cursos com nível e ao menos uma oferta publicada; nunca curso de outra escola; filtro `iniciante` mostra só iniciantes e reflete no endereço (link compartilhável); filtro sem resultado mostra o estado vazio com "Todos"; curso cuja versão vigente perdeu o nível some **sem** despublicar as ofertas, e volta quando uma versão com nível é aplicada.
- **Evidência / checkpoint:** Compose com um curso de cada nível publicado em `learning` **com nível real** (depende do fato 1.1.0, ver pré-requisitos) e ofertas publicadas; abrir `http://localhost:8082/student/cursos`, `…?nivel=iniciante`, recarregar cada URL direto (o nginx devolve o SPA) e com cookie de aluno válido e inválido → mesma vitrine; chamar `commerce` direto sem asserção → 401, com asserção de escopo errado → 403, com `jti` repetido → 401, com JWT de aluno → 401; asserção de outra escola → só dados daquela escola; `nivel=xyz` → 400; nova versão sem nível → curso fora, ofertas ainda `published` no backoffice.
- **Bloqueado por:** V-04, EN-01 (parte de tela).

### V-08: o visitante lê a página do curso e entende o que compra

- **Cobre:** RF-08; RN-O02, RN-O05, RN-O06, RN-O08, RN-O13; Conteúdo RN-C18; Matrícula RN-D04; US "pré-requisito e conteúdo", "tempo de acesso".
- **Entrada / gatilho:** abrir `/student/cursos/{courseId}`; `getShowcaseCourse`.
- **Processamento:** `commerce` devolve, por lista explícita de campos, título, nível, descrição, pré-requisito (recomendados com `inShowcase`, título atual ou da publicação), estrutura sem vídeo e as ofertas publicadas do menor para o maior preço; curso fora da vitrine, inexistente ou de outra escola → o mesmo 404 `SHOWCASE_COURSE_NOT_FOUND`; `student-spa` monta a página com os textos de vigência e a tela de "não disponível".
- **Saída observável:** página com "Avançado", "Recomendamos saber antes", link para *Fundamentos de C#* quando ele está na vitrine e só o título quando não está; republicação do professor com aula nova aparece sem ação do financeiro; o endereço de curso fora da vitrine mostra "não está disponível" com link para a vitrine; nenhum texto da plataforma promete exclusividade ou proteção.
- **Evidência / checkpoint:** cenários do PRD para recomendado na vitrine e fora dela; republicar o curso em `learning` com aula nova e reabrir (fato → visão → página); `courseId` inexistente, de outra escola e fora da vitrine → respostas idênticas; resposta JSON inspecionada: nenhum autor, identificador de pessoa ou `videoId`; teste do SPA que falha se um texto gerado contiver "exclusiv" ou "proteg" (RN-O06); leitura com leitor de tela (títulos navegáveis, preço e vigência em texto).
- **Bloqueado por:** V-07.

### V-09: o visitante clica em Comprar, vê o aviso, e a escola tem um primeiro sinal de demanda

- **Cobre:** RF-09; G10, G26; OD53; DP-02; US "sinal de demanda".
- **Entrada / gatilho:** clique em *Comprar* numa oferta; `registerPurchaseIntent`; leitura do total na ficha do backoffice.
- **Processamento:** `student-spa` abre o aviso e chama a operação com uma chave nova por clique, sem corpo; `bff-student` aplica o limite de taxa **por oferta** (429 com `Retry-After`), assina a asserção (`purchase-intent:write`) e chama `commerce`; `commerce` verifica a asserção, e numa só transação grava o recibo (hash da chave) e incrementa o contador do dia **se** a oferta está `published` na escola; `admin-spa` soma os dias na ficha ("14 cliques em Comprar").
- **Saída observável:** o aviso "compra em breve" sempre aparece; a contagem da oferta clicada sobe em um; a mesma chave não conta de novo; oferta despublicada → 404 sem contar; o armazenamento contém só escola, oferta, dia e contagem (mais o recibo com hash e validade de 24 horas).
- **Evidência / checkpoint:** duas ofertas, clicar na vitalícia → só ela sobe; repetir a requisição com a mesma chave → 202 sem nova contagem; despublicar e chamar direto → 404 e contagem igual; inspeção das tabelas e dos logs/spans do clique: nenhuma sessão, cookie, IP, `User-Agent` ou identificador de navegador; estourar o limite → 429 e, na tela, o aviso ainda aparece; teste do SPA para o aviso independente do resultado; total da ficha = soma dos dias.
- **Bloqueado por:** V-08.

### V-10: o administrador vê os atos de oferta na trilha, com rótulo legível

- **Cobre:** RF-10; Auditoria RN-A14; OD50; C-11.
- **Entrada / gatilho:** abrir a consulta da trilha e o detalhe de um ato de oferta; filtrar por tipo.
- **Processamento:** `bff-admin` resolve os alvos `oferta` das páginas e do detalhe por `resolveOfferReferencesInternal` (papel administrador, lotes de até 50, oferta não encontrada fica sem rótulo) e os insere como rótulo "curso — opção", no mesmo desenho do enriquecimento de curso; `admin-spa` ganha rótulos *Oferta publicada*, *Oferta alterada* e *Oferta despublicada*, as opções de filtro, o tipo de referência "Oferta", a formatação dos atributos (`precoAnterior`/`precoNovo` em reais; `vigenciaAnterior`/`vigenciaNova` como "12 meses"/"Vitalícia"; `curso` como identificador) e o texto "Não se aplica a este tipo" para o motivo dos três tipos.
- **Saída observável:** detalhe de *Oferta alterada* mostra autor, oferta com rótulo e anterior → novo; o filtro oferece os três tipos; nenhum ato de oferta aparece como "motivo ausente" nem como tipo desconhecido.
- **Evidência / checkpoint:** atos reais de V-04, V-05 e V-06 no Compose, consultados em `http://localhost:8081/admin/auditoria` como administrador; ato de oferta excluída/desconhecida mostra o alvo sem rótulo, sem erro; chamada ao resolvedor sem o papel administrador → 403.
- **Bloqueado por:** V-06 (todos os três atos existem), EN-01 (parte de tela).

### Habilitadores inevitáveis

| Habilitador | Por que não cabe numa fatia | Menor escopo | Primeira fatia desbloqueada |
|---|---|---|---|
| EN-01 | O PRD exige o ciclo wireframe ASCII → Figma → aprovação antes de código de tela, para o backoffice (design system do backoffice) e para a área pública (`DESIGN.md` e Figma do design system, OD3); a aprovação é do responsável pelo produto e não é produzida por nenhuma fatia | Novo documento de wireframes em `docs/design/` (Catálogo: lista, ficha, formulário, confirmações; vitrine pública: vitrine, página, aviso; rótulos da trilha), com registro de aprovação no cabeçalho | V-01 (parte de tela), V-07, V-10 |

## Contratos e Fronteiras

Schemas, parâmetros, exemplos e erros vivem nos documentos aprovados de [contracts.md](contracts.md); esta spec não os repete. Os contratos **registram o acordo desta implementação**; compatibilidade com produção não foi verificada (não há versão anterior de nenhuma das operações novas).

### Mapeamento de mensagens e dados

| Contrato e identificador | Aplicação/produtor e consumidores | Comportamento a implementar | Evidência |
|---|---|---|---|
| AsyncAPI `commerce`: `receberVersaoPublicada` · `conteudo.versao-publicada.v1` 1.1.0 | `learning` (send) → Catálogo (receive) | visão por versão monotônica; 1.0.0 como sem nível; 1.1.0 de mesma versão substitui visão derivada de 1.0.0; DLQ em erro permanente; nenhuma chamada a `learning` | V-01, V-07 |
| `publicarOfertaPublicada`, `publicarOfertaAlterada`, `publicarOfertaDespublicada` · `catalogo.oferta-*.v1` | Catálogo (send); nenhum consumidor nesta entrega | outbox do módulo, mesma transação da mudança e do ato; `offerRevision` ordena; fila de retenção limitada mantém o publicador saudável | V-04, V-05, V-06 |
| `publicarAtoDeOferta` · `auditoria.ato-praticado.v1` 1.3.0 | Catálogo (send) → `audit` 1.3.0 (receive) | exchange da Auditoria, `fatoId` = `eventId` do fato, complemento só com identificadores e valores (sem nome nem texto), sem `motivo`; `audit` aceita origem `catalogo`, tipos de oferta e alvo `oferta` | V-04, V-05, V-06, V-10 |

**Diferenças para o acordo anterior (Auditoria).** 1.2.0 → 1.3.0 é aditiva, de **receptor**; produtores existentes seguem válidos. **Ordem de implantação obrigatória:** `audit` 1.3.0 antes de `commerce` publicar o primeiro ato, senão o ato é gravado como não conforme (precedente C-01 de CAP-005).

**Dados sensíveis e credenciais transitórias.**
- *Chave de idempotência do clique:* criada no navegador por clique; trafega até o BFF e `commerce`; persistida **só como hash SHA-256** no recibo de 24 horas; nunca em log, span nem métrica; não fica no navegador.
- *Asserção de serviço do BFF:* assinada por clique/leitura, vida de até 60 s, `jti` consumido em Valkey até `exp`; não é persistida em banco, não entra em log nem em span; a chave privada vem do secret manager.
- *Textos comerciais (nome, chamada, preço):* persistidos em `commerce` e backups; copiados para o outbox e para o broker (o fato leva nome e preço; o ato **não** leva nenhum dos dois, só identificadores e centavos/vigência do que mudou); nunca em log, span ou métrica (contrato, G10).
- *Visitante:* nenhum dado é criado — não há sessão, cookie, IP, `User-Agent` nem identificador de navegador em banco, outbox, log ou métrica. O limite de taxa é por oferta e vive em memória do BFF.

### Mapeamento do contrato de API

Duas camadas por operação, como nas demais áreas: BFF público ↔ interno de `commerce`, mesmos schemas.

| operationId público → interno | Caminho de implementação |
|---|---|
| `listCatalogCourses` → `listCatalogCoursesInternal` | catálogo de cursos por título, com contagem de ofertas por estado e `inShowcase` |
| `getCatalogCourse` / `updateCatalogCourse` → `…Internal` | ficha (visão + `tagline` + ofertas + total de cliques); escrita da `tagline` |
| `createOffer`, `updateOffer`, `deleteOffer` → `…Internal` | agregado ficha+ofertas sob bloqueio do curso; recibo de idempotência |
| `publishOffer`, `unpublishOffer` → `…Internal` | agregado + outbox do módulo (fato + ato) |
| `listShowcaseCourses`, `getShowcaseCourse` → `…Internal` | consultas de leitura sobre `catalog.*`; asserção `showcase:read` |
| `registerPurchaseIntent` → `…Internal` | incremento atômico condicionado ao estado da oferta; asserção `purchase-intent:write` |
| — → `resolveOfferReferencesInternal` | rótulos "curso — opção"; papel `administrador` no claim `roles`; invocada só pelo `bff-admin` na consulta da trilha |

**Validações além do contrato:**

| operationId | Regra | Camada |
|---|---|---|
| `createOffer`/`updateOffer` | limites de nome, preço, vigência; teto de 50 ofertas por curso | domain |
| `updateOffer` | valores idênticos ao gravado são no-op (sem revisão, fato ou ato); preço e vigência mudando numa chamada geram um único ato | domain |
| `publishOffer` | nível não nulo na visão vigente, lido sob o bloqueio do curso | domain |
| `deleteOffer` | só `draft` | domain |
| `createOffer`/`updateOffer`/`publishOffer`/`unpublishOffer`/`updateCatalogCourse` | `Idempotency-Key` obrigatória (400 `INVALID_REQUEST`); mesma chave, corpo diferente → `IDEMPOTENCY_KEY_REUSED` | application |
| `listShowcaseCourses` | `level` fora da lista → 400 `INVALID_REQUEST`; `_size` ≤ 48 | api |
| `registerPurchaseIntent` | oferta `published` da escola na própria instrução de incremento; 404 `OFFER_NOT_AVAILABLE` sem contar | application |
| rotas de ator × rotas públicas | cada rota aceita um único esquema de autenticação | api |

**Exceção → resposta HTTP:**

| Exceção | HTTP | `code` do contrato |
|---|---|---|
| campo fora dos limites, inclusive a 51ª oferta | 422 | `FIELD_INVALID` (campo em `detail`) |
| ação que não cabe no estado da oferta | 422 | `OFFER_STATE_CONFLICT` |
| publicar com nível nulo | 422 | `COURSE_LEVEL_REQUIRED` |
| chave de idempotência com corpo diferente | 422 | `IDEMPOTENCY_KEY_REUSED` |
| curso desconhecido da escola | 404 | `CATALOG_COURSE_NOT_FOUND` |
| oferta desconhecida da escola | 404 | `OFFER_NOT_FOUND` |
| curso fora da vitrine / oferta não publicada (rotas públicas) | 404 | `SHOWCASE_COURSE_NOT_FOUND` / `OFFER_NOT_AVAILABLE` |
| JWT ausente/inválido; asserção ausente/inválida/repetida | 401 | `TOKEN_INVALID` / `SERVICE_ASSERTION_INVALID` |
| sem `oferta.editar` (ou sem o papel, no resolvedor); escopo insuficiente | 403 | `PERMISSION_DENIED` / `SCOPE_DENIED` |
| `commerce` indisponível / tempo-limite, vistos pelo BFF | 502 / 504 | `COMMERCE_UNAVAILABLE`, `COMMERCE_TIMEOUT` (backoffice); `SHOWCASE_UNAVAILABLE`, `SHOWCASE_TIMEOUT` (vitrine) |
| limite de taxa do clique | 429 | `RATE_LIMITED` (do BFF, com `Retry-After`) |

**Tempos-limite e novas tentativas dos BFFs para `commerce`.** `bff-admin`: o padrão já usado com `commerce` e `audit` (5 s por tentativa, 20 s no total, nova tentativa só em métodos seguros); uma escrita que expira devolve 504 e a repetição pela **mesma** chave é segura. `bff-student`: leituras públicas com 3 s por tentativa, 8 s no total e uma nova tentativa só em `GET`; o clique não é repetido pelo cliente HTTP. Os valores do `bff-student` são propostas, sem orçamento de latência no PRD nem no baseline (ver Questões em Aberto).

### Mapeamento de jornada

| História | Tela / componente | operationId | Evidência |
|---|---|---|---|
| Financeiro cria ofertas sobre curso publicado; várias formas | Ficha → formulário de oferta | `getCatalogCourse`, `createOffer` | V-03 |
| Financeiro corrige preço sabendo que vale só para o futuro | Ficha → confirmação antes/depois | `updateOffer` | V-05 |
| Financeiro tira a oferta da vitrine | Ficha → confirmação | `unpublishOffer` | V-06 |
| Financeiro publica | Ficha → confirmação com o cartão | `publishOffer` | V-04 |
| Visitante filtra por nível | Vitrine | `listShowcaseCourses` | V-07 |
| Visitante vê pré-requisito e conteúdo; vê o tempo de acesso | Página do curso | `getShowcaseCourse` | V-08 |
| Visitante clica em Comprar | Página do curso → aviso | `registerPurchaseIntent` | V-09 |
| Administrador vê quem publicou, alterou ou despublicou | Consulta da trilha (detalhe e filtro) | (existentes de CAP-030) + `resolveOfferReferencesInternal` | V-10 |

### Entidades do domínio

| Entidade do Domain Doc | Representação técnica | Local |
|---|---|---|
| Ficha de Vitrine | linha do curso conhecido: `tagline`, `in_showcase_since`, versão aplicada e a visão derivada de Conteúdo (título, descrição, nível, pré-requisito, estrutura) | `commerce`, schema `catalog` |
| Oferta, Preço, Vigência Prometida | linha por oferta: nome, `price_cents`, vigência (`months`/`lifetime`), estado, `offer_revision`, `published_at`; preço e vigência na forma única do contrato | `commerce`, schema `catalog` |
| Vitrine | derivada (elegibilidade e ordenação sobre a linha do curso e suas ofertas); não persistida à parte | `commerce`, consulta |
| Intenção de Compra | contador diário por (escola, oferta, dia) e recibo de 24 h com hash da chave | `commerce`, schema `catalog` |
| Recibos de idempotência | (escola, ator, chave, hash do pedido, resposta, validade) | `commerce`, schema `catalog` |
| Ato Administrativo | item do outbox do módulo, conforme `AtoPraticado` 1.3.0 | `commerce` → `audit` |

### Interfaces entre fatias ou times

- **Elegibilidade de vitrine** (predicado e `in_showcase_since`) é um único ponto do domínio usado por V-01 (aplicação de fato), V-04, V-06, V-07 e V-08. Nenhuma fatia a reimplementa.
- **Forma da vigência prometida** (`accessPeriod`, `priceCents`, `currency: BRL` nos fatos) é o acordo com `CAP-008` e `CAP-011`: o pedido as congela sem conversão. Mudar a forma é mudança de contrato (G14).
- **Fato 1.1.0 × 1.0.0** é o acordo com `learning`: presença explícita das propriedades distingue os formatos (ver Decisões).

## Arquivos a Modificar e a Referenciar

### A modificar

| Caminho | Fatia | Alteração |
|---|---|---|
| `src/identity/src/CodeForCoders.Identity.Domain/Entities/StaffRoleCatalog.cs` | V-01 | permissão `oferta.editar` no papel financeiro (`financeiro.ler` permanece) |
| `src/commerce/src/CodeForCoders.Commerce.Api/Extensions/ServiceConfigurationExtensions.cs` | V-01, V-07 | políticas `oferta.editar` e papel administrador; segundo esquema de autenticação (asserção de serviço); opções do emissor `bff-student` |
| `src/commerce/src/CodeForCoders.Commerce.Api/Extensions/EndpointExtensions.cs` | V-01–V-10 | mapear os grupos do Catálogo e da vitrine |
| `src/commerce/src/CodeForCoders.Commerce.Api/ExceptionHandlers/GlobalExceptionHandler.cs` | V-02–V-09 | mapa exceção → HTTP do quadro acima |
| `src/commerce/src/CodeForCoders.Commerce.Infra.Data/CommerceDbContext.cs` | V-01 | entidades do Catálogo com filtro global de tenant (G07) |
| `src/commerce/src/CodeForCoders.Commerce.Infra.Data/Configuration/CommerceSchemas.cs`, `CommerceModules.cs` | V-04 | outbox no schema `catalog`; testes de arquitetura dos módulos seguem verdes |
| `src/commerce/src/CodeForCoders.Commerce.Infra.Data/Health/OutboxHealthCheck.cs` | V-04 | cobrir também o outbox do Catálogo |
| `src/commerce/src/CodeForCoders.Commerce.Infra.Data/Migrations/*` | V-01–V-09 | geradas por `dotnet ef migrations add`, nunca à mão |
| `src/commerce/src/CodeForCoders.Commerce.Infra.Messaging/OutboxPublisherWorker.cs` | V-04 | drenar o outbox do Catálogo além do de `sales` (hoje fixo em `sales`, linha 70) |
| `src/commerce/src/CodeForCoders.Commerce.Infra.Messaging/RabbitMqPublisher.cs` | V-04 | enviar `auditoria.ato-praticado.v1` ao exchange da Auditoria; cabeçalho `correlationId` (hoje exchange único, linha 33) |
| `src/commerce/src/CodeForCoders.Commerce.Infra.Messaging/RabbitMqTopologyInitializer.cs`, `Configuration/RabbitMqOptions.cs` | V-01, V-04 | fila do Catálogo ligada a `learning.events` (exchange declarado de forma idempotente); fila de retenção dos fatos de oferta; exchange da Auditoria |
| `src/commerce/src/CodeForCoders.Commerce.Application/Common/ITenantContext.cs` (e `TenantContext.cs`) | V-01, V-07 | tenant vindo de claim (ator) ou da asserção (público) e do payload (consumidor) |
| `src/audit/src/CodeForCoders.Audit.Domain/Policies/AdministrativeActPolicy.cs` | V-04 | tipos aceitos `oferta-publicada`/`oferta-alterada`/`oferta-despublicada` (sem motivo); origem `catalogo` e alvo `oferta` exigidos, como já é para `versao-publicada` (linhas 16–45) |
| `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Clients/CommerceApiOptions.cs`, `Extensions/ServiceConfigurationExtensions.cs` | V-01 | cliente HTTP do Catálogo (5 s/20 s; retry só em métodos seguros) |
| `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Extensions/EndpointExtensions.cs` (registro) e novo grupo `/api/v1/catalog/**` | V-01–V-06 | sessão validada em Identity com audiência `commerce`, `oferta.editar`, `Idempotency-Key`, mapa 502/504 |
| `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Endpoints/AuditRecordEndpoints.cs` | V-10 | enriquecer alvo `oferta` (lista e detalhe) com o rótulo, como o de `curso` (linhas 158 e 349) |
| `src/bff-student/src/CodeForCoders.BffStudent.Api/Security/BffSecurityMiddleware.cs` | V-07 | rotas `/api/v1/showcase/**` anônimas: sem sessão, sem CSRF, cookie ignorado (hoje todo `/api/v1/*` fora da lista exata exige sessão, linhas 139–151) |
| `src/bff-student/src/CodeForCoders.BffStudent.Api/Security/ServiceAssertionTokenFactory.cs`, `StudentIdentityOptions.cs`, `Extensions/ServiceConfigurationExtensions.cs` | V-07 | fábrica de asserção para a audiência `commerce` (hoje amarrada às opções de Identity), cliente HTTP (3 s/8 s), limite de taxa por oferta |
| `src/bff-student/src/CodeForCoders.BffStudent.Api/Extensions/EndpointExtensions.cs` | V-07–V-09 | mapear os endpoints da vitrine |
| `src/admin-spa/src/features/staff-session/utils/get-staff-areas.ts`, `src/config/paths.ts`, `src/app/app-routes.tsx` | V-01 | item de menu **Catálogo** (`oferta.editar`) e rotas `/catalogo`, `/catalogo/:courseId` |
| `src/admin-spa/src/features/audit-trail/components/audit-trail-screen.tsx` (`typeOptions`, linha 14), `audit-record-detail-screen.tsx` (`typeLabels` linha 12; motivo linha 253; `attributeLabel` linha 418; tipo de referência) | V-10 | rótulos, filtro, formatação dos atributos de oferta, "Não se aplica" |
| `src/student-spa/src/config/paths.ts`, `src/app/router.tsx` | V-07, V-08 | rotas públicas `/cursos`, `/cursos/:courseId`, fora de `requireStudentSession` |
| `src/student-spa/nginx.conf.template` | V-07 | nenhuma rota nova específica é esperada (o `try_files` já serve o SPA e `/api/v1/` já vai ao BFF); confirmar na evidência |
| `docker-compose.yml`, `docker-compose.coolify.yml`, `scripts/generate-local-env.sh` | V-07 | par de chaves do `bff-student` para `commerce`; emissor e escopos no `commerce`; `RabbitMq` da Auditoria no `commerce`; URLs do `bff-student` para `commerce` |
| `docs/adr/index.md` | — | ADR-0009 (já incluída no rascunho) |

### A referenciar (não alterar)

| Caminho | Por que consultar |
|---|---|
| `src/identity/src/CodeForCoders.Identity.Api/Security/ServiceAssertionVerifier.cs`, `ServiceAssertionOptions.cs`, `src/identity/src/CodeForCoders.Identity.Infra.Data/Outbox/ServiceAssertionReplayStore.cs` | comportamento de referência do verificador de asserção e do consumo de `jti` em Valkey |
| `src/learning/src/CodeForCoders.Learning.Infra.Messaging/OutboxPublisherWorker.cs`, `RabbitMqPublisher.cs`, `Infra.Data/Outbox/ContentOutboxMessage*.cs` | precedente de outbox por módulo, exchange da Auditoria e `correlationId` |
| `src/learning/src/CodeForCoders.Learning.Domain/Entities/CourseEditReceipt.cs`, `Infra.Data/Idempotency/CourseEditStore.cs` | precedente de recibo de idempotência com hash e resposta por 24 h |
| `src/bff-admin/src/CodeForCoders.BffAdmin.Api/Endpoints/CourseAuthoringEndpoints.cs`, `Clients/CourseAuditReferenceEnricher.cs` | padrão de validação de sessão por audiência e de enriquecimento de rótulo |
| `src/admin-spa/src/features/course-authoring/**` | padrão de tela, confirmação e idempotência por intenção |
| [wireframes de Autoria](../../docs/design/wireframes-autoria-curso.md), [DESIGN.md](../../DESIGN.md), [Components.md](../../docs/design/Components.md) | ciclo de design e componentes |
| [TechSpec de nível e pré-requisito](../prd-nivel-prerequisito-curso/techspec.md) | fato 1.1.0, reenvio e ordem operacional |

## Análise de Impacto

| Componente | Tipo | Impacto e risco | Ação requerida |
|---|---|---|---|
| `commerce` | modificado | primeiro conteúdo de negócio; ganha consumidor, dois esquemas de autenticação, outbox do Catálogo, leitura pública | testes de arquitetura dos módulos (G01–G07) seguem verdes; Valkey passa a ser dependência de leitura pública |
| `identity` | modificado | papel financeiro ganha permissão; nenhum schema HTTP novo (enum já aprovado, C-10) | implantar primeiro |
| `audit` | modificado | três tipos novos, aditivos | implantar antes de `commerce` publicar o primeiro ato |
| `learning` | dependência | fato 1.1.0 e reenvio (outro PRD) | habilitar o reenvio só com a fila do Catálogo declarada e ligada |
| `bff-admin` / `admin-spa` | modificado | área nova; rótulos na trilha; schemas estritos do SPA | implantar juntos |
| `bff-student` / `student-spa` | modificado | primeira superfície anônima; asserção de serviço para `commerce` | implantar juntos; verificar que o cookie de sessão não é exigido nas rotas públicas |
| RabbitMQ | modificado | fila do Catálogo em `learning.events`; fila de retenção; publicação no exchange da Auditoria | conferir permissões do usuário de `commerce` nos exchanges de `learning` e `audit` |
| Consulta da trilha (CAP-030) | modificado (comportamento) | nenhum schema muda; só rótulos | — |

**Ordem de implantação:** Identity (permissão) → `audit` 1.3.0 → `commerce` (consumidor, fila de retenção, outbox do Catálogo; sem expor rotas públicas) → `learning` 1.1.0 e **reenvio** (CAP-005) → `commerce` expondo Catálogo e vitrine → `bff-admin` + `admin-spa` → `bff-student` + `student-spa`. A fila do Catálogo precisa existir **antes** do reenvio; o reenvio não se repete (marcador em `learning`).

## Riscos e Preocupações

| Preocupação | Local (`arquivo:linha`) | Impacto | Mitigação |
|---|---|---|---|
| Reenvio de `learning` (mesma `versionNumber`, formato 1.1.0) seria ignorado se um 1.0.0 do mesmo curso chegou antes (consumidor sobe antes de `learning` 1.1.0) | contrato `asyncapi-contract.yaml` (`receberVersaoPublicada`) × regra "menor ou igual é ignorado" | curso sem descrição e sem nível na vitrine, sem correção automática | regra de compatibilidade de formato na aplicação do fato (V-01); teste dedicado; registrar para o dono do contrato que a regra de versão vale "a menos que o formato seja mais rico" |
| Publicador trata mensagem sem fila como falha (`mandatory: true`) | `src/commerce/src/CodeForCoders.Commerce.Infra.Messaging/RabbitMqPublisher.cs:35` | `catalogo.oferta-*` sem consumidor esgota as tentativas do outbox e degrada a saúde | fila de retenção limitada ligada às três chaves; teste que prova o esgotamento sem ela (V-04) |
| `commerce` publica tudo no exchange próprio; ato de auditoria não chegaria à Auditoria; falta `correlationId` | `…/RabbitMqPublisher.cs:33` e `:29` | ato nunca gravado; contrato violado | seleção de exchange por chave de roteamento e cabeçalho, como em `learning` (V-04) |
| Publicador de `commerce` lê só `sales.outbox_messages` | `src/commerce/src/CodeForCoders.Commerce.Infra.Messaging/OutboxPublisherWorker.cs:70` | mensagens do Catálogo jamais publicadas | outbox do Catálogo em `catalog`, drenado pelo mesmo worker (V-04) |
| Saúde do outbox só observa um conjunto | `src/commerce/src/CodeForCoders.Commerce.Infra.Data/Health/OutboxHealthCheck.cs:15` | fato/ato esgotado do Catálogo passa despercebido | cobrir o outbox do Catálogo |
| `bff-student` exige sessão em toda rota `/api/v1/**` fora da lista exata | `src/bff-student/src/CodeForCoders.BffStudent.Api/Security/BffSecurityMiddleware.cs:139` e `:154` | vitrine pública devolveria 401 `SESSION_REQUIRED` a visitante | prefixo `/api/v1/showcase` anônimo, ignorando cookie; teste com e sem cookie (V-07) |
| Verificador de asserção é de audiência única e está dentro de `identity` | `src/identity/src/CodeForCoders.Identity.Api/Security/ServiceAssertionVerifier.cs:77` | não reaproveitável em `commerce` sem cópia | verificador próprio em `commerce` seguindo o comportamento de referência; teste de conformidade cruzada com as asserções do BFF (ADR-0009) |
| Fábrica de asserção do BFF está amarrada às opções de Identity | `src/bff-student/src/CodeForCoders.BffStudent.Api/Security/ServiceAssertionTokenFactory.cs:9` | escopo, audiência e chave de `commerce` não cabem | fábrica e opções por destino (V-07) |
| `commerce` tem um único esquema JWT como padrão | `src/commerce/src/CodeForCoders.Commerce.Api/Extensions/ServiceConfigurationExtensions.cs:35` | asserção de serviço seria desafiada como JWT inválido | segundo esquema; cada rota declara o seu |
| JWT de ator emitido antes da revogação continua válido em `commerce` até expirar (≤ 5 min) | ADR-0005 (consequência negativa aceita) | chamada direta a `commerce` com token antigo passa | garantia de RN-17 vale na borda (BFF revalida a cada ação); `commerce` não é alcançável pelo navegador; registrado, sem mitigação nova |
| Preço/vigência mudam sem ato enquanto a oferta está em rascunho ou despublicada, e o ato `oferta-publicada` não leva preço | contrato `asyncapi-contract-audit.yaml` (complemento de `oferta-publicada` = `curso`) | a trilha não mostra com qual preço uma oferta voltou à venda | o fato `catalogo.oferta-publicada` leva nome, preço e vigência; ver Questões em Aberto sobre acrescentar o preço ao ato |
| Leituras públicas sem limite de taxa e sem cache | (V-07) | carga sobre `catalog.*` e Valkey por visitante anônimo | consultas indexadas, `_size` ≤ 48, resposta `no-store`; medir; cache de TTL absoluto curto com tenant na chave é a evolução (baseline, "perfis de carga") |
| Nenhum curso entra na vitrine antes do fato 1.1.0 e do reenvio | `src/learning` (sem `CatalogInitialLoad`/`PrerequisiteText`) | V-07 e V-08 sem evidência ponta a ponta | declarado em Pré-requisitos; evidência de V-07/V-08 com fatos 1.1.0 reais ou de fixture conforme o schema, e reexecução com `learning` 1.1.0 quando existir |
| Orçamento de latência e de carga da vitrine não definidos | PRD e baseline (não encontrei) | valores de tempo-limite do BFF são propostas | Questões em Aberto |

## Decisões Técnicas

- **Visão do curso como réplica derivada, substituída por inteiro a cada fato.** Racional: tira `learning` do caminho (G08) e torna a regra de nível transacional com as ofertas. Trade-off: eventualmente consistente; depende do reenvio inicial. Rejeitado: leitura síncrona `commerce` → `learning` (contrato C-01). RN-O02 ("não guarda nem edita currículo") é atendida no sentido de que nada no Catálogo a edita ou a torna própria; é réplica de Conteúdo.
- **Fato 1.1.0 de mesma versão supera visão derivada de 1.0.0.** Racional: sem isso, o reenvio é ignorado para cursos publicados entre a subida do consumidor e a de `learning` 1.1.0. Trade-off: o registro guarda o formato de origem. Rejeitado: congelar publicações de curso na janela de implantação (frágil e fora do controle desta entrega).
- **`in_showcase_since` guardado na linha do curso.** Racional: "do mais recentemente colocado na vitrine" tem um momento único e estável, que não muda por editar ou acrescentar outra oferta. Trade-off: valor derivado, mantido em toda operação que afeta a elegibilidade. Rejeitado: `max(publishedAt)` das ofertas (reordena a vitrine quando uma segunda oferta é publicada) e `min(publishedAt)` (muda quando uma oferta antiga é despublicada).
- **Outbox próprio no schema `catalog`.** Racional: cada módulo tem o seu, como `learning` fez para `content`; o de `sales` hoje atende só o heartbeat. Trade-off: o publicador passa a drenar dois. Rejeitado: escrever no outbox de `sales` (mistura módulos).
- **Fila de retenção limitada para os fatos de oferta.** Racional: `mandatory: true` exige fila roteável e C-12 manda produzir os fatos já. Trade-off: uma fila que ninguém drena e expira sozinha (limite e validade configuráveis; valores iniciais propostos: 7 dias e 10 000 mensagens, a confirmar). Rejeitado: `mandatory: false` só para estas chaves (desvia do padrão e perde o sinal de falha de roteamento).
- **Contador diário por oferta, sem linha por clique; recibo com hash da chave por 24 h.** Racional: "sinal de demanda por semana" precisa de tempo; a contagem não pode identificar ninguém; a repetição automática não pode contar duas vezes. Trade-off: o recibo guarda um hash por clique por 24 h (sem ligação com pessoa; a chave é aleatória e não persiste no navegador). Rejeitado: dedupe só em Valkey (contagem dupla se a falha ocorre entre os dois armazenamentos).
- **Limite de taxa do clique por oferta, em memória do BFF; o aviso nunca depende da contagem.** Racional: IP e identificador de navegador estão fora (G26, PRD); o contador é sinal sem valor financeiro, então perder cliques sob abuso é aceitável e o visitante nunca vê erro. Trade-off: o limite é por instância e um abusador pode esgotar a cota de uma oferta; valores iniciais propostos como configuração (sem número de negócio no PRD), a calibrar. Rejeitado: particionar por IP.
- **Sem cache de leitura na vitrine nesta entrega (`Cache-Control: no-store`).** Racional: RF-04 e RF-06 exigem efeito imediato da publicação e da despublicação, e o preço exibido deve ser o que a compra futura lerá. Trade-off: toda leitura vai ao banco; consultas indexadas e paginadas. Reavaliar com medição: TTL absoluto curto, chave com escola, invalidação após o commit (baseline).
- **Par de chaves dedicado do `bff-student` para `commerce`** ([ADR-0009](../../docs/adr/0009-autenticacao-de-servico-bff-aluno-em-servicos-de-dominio.md)). Racional: o comprometimento de um destino não abre o outro. Trade-off: mais um segredo por ambiente.
- **Filtro de nível no endereço em português (`?nivel=iniciante`).** Racional: consistente com as rotas em português do SPA (`/cadastro`, `/entrar`); o cliente traduz para o enum do contrato. Trade-off: uma tabela de tradução no cliente.

## Verificação

- **Cenários críticos não óbvios:** fato de versão sem nível chegando **durante** a publicação de uma oferta do mesmo curso (serialização pelo bloqueio); publicação concorrente da mesma oferta (um fato, um ato); republicação do fato 1.0.0 e depois 1.1.0 de mesma versão; fatos fora de ordem; oferta publicada cujo curso perde o nível e depois o recupera; 51ª oferta; `priceCents` e `months` nos limites; valor idêntico no `updateOffer`; clique concorrente em oferta sendo despublicada (contagem condicionada ao estado na própria instrução); `jti` repetido; asserção de outra escola; falha forçada entre oferta, fato e ato; publicador com e sem a fila de retenção.
- **Dados ou ambiente especiais:** Testcontainers de PostgreSQL, RabbitMQ e Valkey para `commerce`; Compose completo (`./scripts/generate-local-env.sh`, `./scripts/apps.sh start`, migrations aplicadas como nas TechSpecs anteriores, por `dotnet ef database update`), com Identity, Learning, Audit, `commerce`, `bff-admin`, `bff-student`, os dois SPAs, financeiro, professor, suporte, administrador, um curso de cada nível publicado em `learning` e dois tenants; as chaves novas do `bff-student` geradas pelo script; verificar que nenhum recurso compartilhado entre execuções (filas, outbox) carrega resíduo de outra rodada. Para V-07/V-08, o fato 1.1.0 vem de `learning` 1.1.0 ou, enquanto ele não existe, de mensagens conformes ao `VersaoPublicadaPayload` 1.1.0 publicadas no exchange de `learning`.
- **Observabilidade além do padrão:** contadores de fato aplicado/ignorado/DLQ e atraso do fato; contagem de leituras públicas por operação e resultado; cliques contados/repetidos/recusados **sem rótulo de oferta** (ids só em span); saúde do outbox cobrindo os dois schemas. Nenhum preço, nome de oferta, chamada, descrição ou dado de visitante em log, span ou métrica.
- **Verificação dos contratos:** respostas e erros das oito operações do backoffice, três públicas, o resolvedor e as oito interfaces `*Internal` conferidos contra os exemplos dos OpenAPI; payloads de `catalogo.oferta-*` e do ato capturados do outbox/broker validados contra `OfertaPayload`/`OfertaAlteradaPayload` e `AtoPraticadoPayload` 1.3.0; consumo de `conteudo.versao-publicada.v1` 1.0.0 e 1.1.0 validado nos dois formatos; a validação sintática dos YAMLs, já registrada em `contracts.md`, **não** comprova nada disso. As URLs públicas de vitrine, página (com e sem `?nivel=`) e Catálogo são abertas depois de recarregar, com o destino esperado; o link do recomendado abre a página do curso recomendado.

## Questões em Aberto

- [ ] Incluir `precoNovo`/`vigenciaNova` no ato `oferta-publicada` (aditivo em `audit` 1.3.0, ainda não implantado), para a trilha mostrar com qual condição a oferta voltou à venda depois de edição em silêncio — responsável: produto/dono do contrato de Auditoria — sem resposta, vale o contrato aprovado e o histórico fica só no fato.
- [ ] Orçamento de latência e de carga da vitrine, que calibra tempo-limite do BFF, limite de taxa do clique e a decisão de cache — responsável: produto/plataforma — sem resposta, valem os valores iniciais propostos nesta spec, revisados após medição.
- [ ] Limite e validade da fila de retenção dos fatos de oferta (propostos: 7 dias, 10 000 mensagens) — responsável: plataforma — sem resposta, vale o proposto.
- [ ] Aprovação do ciclo de design (EN-01) — responsável pelo produto — bloqueia só o código de tela.
- [ ] Esclarecer com o dono do contrato de `conteudo.versao-publicada` que "versão menor ou igual é ignorada" admite exceção de formato (ver Decisões) — responsável: dono de CAP-005 — sem resposta, vale a exceção desta spec.

## Architecture Decision Records

- [ADR-0009: Autenticação de serviço do BFF do aluno em serviços de domínio](../../docs/adr/0009-autenticacao-de-servico-bff-aluno-em-servicos-de-dominio.md) (**Proposed**, nova) — o `bff-student` chama `commerce` por asserção de serviço nas rotas públicas; estende a ADR-0004 sem substituí-la.
- [ADR-0001](../../docs/adr/0001-monorepo-de-codigo.md) — serviços com deploy independente; a coordenação é a ordem de implantação acima; o verificador de asserção é uma cópia por serviço.
- [ADR-0002](../../docs/adr/0002-plataforma-de-runtime-coolify.md) — segredos (chaves do BFF e do emissor) pelo secret manager do ambiente.
- [ADR-0004](../../docs/adr/0004-autenticacao-de-servico-bff-identity.md) — mecanismo de asserção, estendido pela ADR-0009.
- [ADR-0005](../../docs/adr/0005-sessao-e-servico-do-backoffice.md) — sessão revalidada no BFF, JWT de ator e autorização no serviço dono (`commerce`).

Nenhuma ADR Accepted conflita com o desenho; nenhuma foi substituída.
