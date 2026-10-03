---
tsg_artifact: contract
product: code-4-coders
capability: CAP-007
version: 1.0
status: approved
updated: 2026-10-03
sources: tasks/prd-reproducao-protegida/prd.md@1.0, domains/entrega-de-midia-e-protecao/domain.md@1.0, domains/matricula-e-direito-de-acesso/domain.md@1.0, domains/identidade-e-acesso/domain.md@1.1, domains/conteudo-e-curriculo/domain.md@1.1, context/architecture-baseline.md@1.2
---

# Contratos de integração — reprodução protegida

> - PRD: [prd.md](prd.md), v1.0, aprovado em 2026-10-03
> - Data: 2026-10-03
> - Estado do conjunto: **Aprovado para implementação** em 2026-10-03. C-01 a C-12 aprovadas pelo responsável, inclusive **C-02** (antecipa para esta entrega a credencial de `learning` em `commerce`, prevista pela ADR-0011 para `CAP-017`). Todos os documentos técnicos validados sem erros.

Este conjunto registra o acordo para o primeiro PRD de `CAP-007`. Contratos de PRDs anteriores são preservados nas pastas de origem e **nenhum é alterado**: as mudanças sobre eles são recortes aditivos, declarados aqui. PRDs posteriores podem evoluir este conjunto. Não afirma qual contrato está em produção (nenhum está: o sistema só vai a homologação ao fim do MVP). Catalogação, armazenamento definitivo e mecanismos de atualização do acervo são do time de plataforma.

## Seleção e escopo

| Integração identificada | Modalidade | Justificativa |
|---|---|---|
| `student-spa` → `bff-student` | OpenAPI | Tela da aula, abrir e renovar a sessão, playlist, chave e avanço (RF-01 a RF-07). |
| `bff-student` → `learning` | OpenAPI | Aula e estrutura vigente do curso, entregues só ao aluno com direito (RF-01, RF-06). |
| `bff-student` → `media` | OpenAPI | Sessão de Reprodução, renovação, playlist, variante, chave e avanço (RF-01 a RF-07). |
| `bff-student` → `identity` | OpenAPI | JWT de aluno para `media` e `learning`, com e-mail em `media` (RF-04). |
| `media` → `commerce`; `learning` → `commerce` | OpenAPI **já existente** | `decideAccessInternal` (CAP-008, ADR-0011). **Sem documento novo**: a operação não muda, ela ganha consumidores ativos (C-12). |
| `media` → consumidores futuros | AsyncAPI | Fato `midia.reproducao-avancou.v1` (RF-07). Consumidor previsto: `learning`, em `CAP-017`. |
| Sinais de operação (RF-08) | Não é contrato | Telemetria OTLP e painel no Kibana, padrão da ADR-0008. Nenhum acordo entre aplicações. |
| Avanço como produto de dados | Não aplicável | Banco interno de `media`; nenhum consumidor recebe dataset com compromisso próprio. Nenhum ODCS. |

| Documento | Padrão e versão | Versão do contrato | Fronteira | Estado |
|---|---|---|---|---|
| [api-contract.yaml](api-contract.yaml) e [.md](api-contract.md) | OpenAPI 3.1.0 | `bff-student` reprodução 1.0.0 | Recorte: 7 operações novas; as de `CAP-001`, `CAP-003` e `CAP-008` não mudam | **Aprovado para implementação**; lint sem erros nem avisos |
| [internal-api-contract-media.yaml](internal-api-contract-media.yaml) e [.md](internal-api-contract-media.md) | OpenAPI 3.1.0 | `media` 1.0.1 → 1.1.0 | Recorte: 6 operações de entrega ao aluno; as de autoria de `CAP-006` não mudam | **Aprovado para implementação**; lint sem erros nem avisos |
| [internal-api-contract-learning.yaml](internal-api-contract-learning.yaml) e [.md](internal-api-contract-learning.md) | OpenAPI 3.1.0 | `learning` 1.0.0 → 1.1.0 | Recorte: 1 leitura nova; as de autoria de `CAP-005` não mudam | **Aprovado para implementação**; lint sem erros nem avisos |
| [internal-api-contract-identity.yaml](internal-api-contract-identity.yaml) e [.md](internal-api-contract-identity.md) | OpenAPI 3.1.0 | Identity 1.0.0 → 1.1.0 | Recorte: só `validateStudentSessionInternal`, com mudança aditiva | **Aprovado para implementação**; lint sem erros nem avisos |
| [asyncapi-contract.yaml](asyncapi-contract.yaml) | AsyncAPI 3.0.0 | `media` 1.1.0 → 1.2.0 | Recorte: produtor de `midia.reproducao-avancou.v1` | **Aprovado para implementação**; parser sem erros |

## Participantes e interfaces

| Identificador | Provedor/produtor | Consumidores | Comportamento | PRD |
|---|---|---|---|---|
| `getStudentLesson`, `getStudentLessonInternal` | `bff-student`, decisão de `learning` | `student-spa` | A aula e o curso na versão vigente, só depois da decisão positiva; 404 indistinto; 403 `ACCESS_DENIED` com `reason`; 503 se Matrícula não responde | RF-01, RF-06 |
| `openPlaybackSession`, `openPlaybackSessionInternal` | `bff-student`, decisão de `media` | `student-spa` | Referência de Uso → vídeo pronto → decisão → e-mail; sessão de 5 minutos | RF-01, RF-02, RF-04 |
| `renewPlaybackSession`, `renewPlaybackSessionInternal` | idem | `student-spa` | Repete a decisão; nova validade e nova `segmentAccess`; 410 depois do fim | RF-02 |
| `getPlaybackPlaylist`, `getPlaybackVariantPlaylist` (+ `Internal`) | idem | player (hls) | Playlists HLS com endereços relativos; segmentos sem credencial | RF-03, RF-05 |
| `getPlaybackKey` (+ `Internal`) | idem | player (hls) | Chave AES-128, só com sessão válida daquele vídeo, `no-store` | RF-03 |
| `recordPlaybackProgress`, `recordPlaybackProgressInternal` | idem | `student-spa` | Idempotente por (sessão, sequência); coalesce abaixo de 10 s; `recorded` informa | RF-07 |
| `validateStudentSessionInternal` (revisada) | `identity` | `bff-student` | Audiências `media` e `learning`; claim `email` só em `media` | RF-04 |
| `decideAccessInternal` (CAP-008, **sem mudança**) | `commerce` | `media`, `learning` | Decisão de acesso, cache de até 30 s por chamador, falha fechada | RF-01, RF-02 |
| `publicarReproducaoAvancou` · `midia.reproducao-avancou.v1` | `media`, por outbox | nenhum nesta entrega; `learning` em `CAP-017` | Avanço bruto, sem e-mail, retido até haver consumidor | RF-07 |

| Ação do aluno | Operações HTTP | Mensagens |
|---|---|---|
| Abrir a aula | `getStudentLesson` → `getStudentLessonInternal` → `decideAccessInternal`; depois `openPlaybackSession` → `validateStudentSessionInternal` (audiência `media`) → `openPlaybackSessionInternal` → `decideAccessInternal` | nenhuma |
| Tocar | `getPlaybackPlaylist`, `getPlaybackVariantPlaylist`, `getPlaybackKey` (via BFF); segmentos direto da distribuição com `segmentAccess.query` | nenhuma |
| Continuar assistindo | `renewPlaybackSession` → `renewPlaybackSessionInternal` → `decideAccessInternal`, a partir de `renewAfter` | nenhuma |
| Pausar, sair, chegar ao fim, a cada 30 s | `recordPlaybackProgress` → `recordPlaybackProgressInternal` | `midia.reproducao-avancou.v1` |
| Trocar de aula | `getStudentLesson` e `openPlaybackSession` da nova aula | nenhuma |

`lessonId`, `courseId` e `videoId` são os mesmos de `tasks/prd-autoria-curso` e dos fatos `midia.*`; `studentId` é o `accountId` de Identity, o mesmo de `CAP-008`; `sessionId` identifica a Sessão de Reprodução em todas as interfaces e no fato.

## Origem e decisões

| Entrada consultada | Versão e data | Decisão herdada |
|---|---|---|
| [PRD de CAP-007](prd.md) | v1.0, 2026-10-03 | RF-01 a RF-08, RN-R01 a RN-R08, DP-01 a DP-10; OD66, OD67 |
| [Entrega de Mídia e Proteção](../../domains/entrega-de-midia-e-protecao/domain.md) | v1.0, 2026-09-25 | RN-M01 a RN-M17; evento `midia.reproducao-avancou`; Referência de Uso |
| [Matrícula e Direito de Acesso](../../domains/matricula-e-direito-de-acesso/domain.md) | v1.0, 2026-09-30 | RN-D01, RN-D06 a RN-D08, RN-D15 |
| [Identidade e Acesso](../../domains/identidade-e-acesso/domain.md) | v1.1, 2026-09-30 | RN-21 (e-mail ao player), RN-09 |
| [Conteúdo e Currículo](../../domains/conteudo-e-curriculo/domain.md) | v1.1, 2026-09-28 | RN-C07, RN-C09, RN-C12 |
| [Baseline](../../context/architecture-baseline.md) | v1.2, 2026-09-21 | BA07, BA15, BA16, G06, G07, G08, G09, G15, G18, G21 a G24, G26 |
| [ADR-0003](../../docs/adr/0003-verificacao-de-sessao-do-aluno.md) | aceita | BFF valida a sessão em Identity a cada ação protegida e obtém JWT por audiência |
| [ADR-0006](../../docs/adr/0006-preparacao-de-video-e-custodia-de-chave.md) | aceita | Playlists guardadas não apontam para chave real; o serviço que entrega a playlist reescreve a chave para o endpoint da sessão |
| [ADR-0011](../../docs/adr/0011-autenticacao-de-servico-media-learning-em-commerce.md) | aceita | `media` e `learning` emissores em `commerce`, escopo `access-decision:read`; **item 4 revisado por C-02** |
| [Contratos de concessão de acesso (CAP-008)](../prd-concessao-acesso/contracts.md) | 1.1, 2026-10-01 | `decideAccessInternal` e o envelope da decisão (`decision`, `validity`, `deniedReason`, `lastExpiredAt`) |
| [Contratos de autoria de curso (CAP-005)](../prd-autoria-curso/contracts.md) | 1.0 | Fato de versão com `lessonId` e `videoId`; Referência de Uso registrada por `media` |
| [Contratos de ingestão de mídia (CAP-006)](../prd-ingestao-midia/contracts.md) | 1.0.1 | Convenções de `media` interno e de fatos `midia.*` (`eventId`, `correlationId`) |
| [Contratos de conta do aluno (CAP-001)](../prd-conta-aluno/contracts.md) | 1.0 | Cookie `student_session`, CSRF, `validateStudentSessionInternal` |

Decisões deste contrato:

| ID | Decisão | Alternativa descartada | Estado |
|---|---|---|---|
| C-01 | **A superfície do aluno é o `bff-student`**, inclusive playlist e chave, repassadas **sem alteração** (endereços relativos); os **segmentos vêm direto da distribuição** | SPA falando com `media` (G15); segmentos pelo BFF (a banda do vídeo passaria pelo BFF e o cache da distribuição deixaria de servir) | **Aprovada em 2026-10-03** |
| C-02 | **`learning` consulta a decisão de acesso antes de devolver a aula e a estrutura** (`getStudentLessonInternal`), com asserção de serviço própria. Isso **antecipa para `CAP-007`** a credencial de `learning` em `commerce` que a ADR-0011 (item 4) previa só para `CAP-017`; a TechSpec revisa a ADR | O BFF chamar `media` primeiro e só então ler a estrutura (a regra passaria a viver no BFF e `learning` serviria título de aula sem checar direito); devolver a estrutura sem decisão (a aula de um curso que o aluno não tem apareceria) | **Aprovada em 2026-10-03** |
| C-03 | **Abertura de sessão** na ordem de RN-M10: Referência de Uso (404 `LESSON_NOT_AVAILABLE`, **antes** de Matrícula) → vídeo pronto (409 `MEDIA_NOT_READY`) → decisão (403 `ACCESS_DENIED`; 503 `ACCESS_DECISION_UNAVAILABLE`) → e-mail disponível (422 `WATERMARK_UNAVAILABLE`). Cada chamada abre uma sessão nova, sem limite de simultâneas | Idempotência por aluno e aula (impediria várias abas e dispositivos: RN-M13, BA16) | **Aprovada em 2026-10-03** |
| C-04 | **JWT de aluno com audiências `media` e `learning`** emitido por `validateStudentSessionInternal`. **A claim `email` existe só na audiência `media`**, e é a exposição declarada de Identidade RN-21 para a marca d'água. `media` a repassa em `watermark.text`; nada a persiste, loga ou publica | E-mail em todo JWT de aluno (espalharia o dado); Identity devolver o e-mail em chamada à parte por request (round-trip a mais no caminho crítico) | **Aprovada em 2026-10-03** |
| C-05 | **Sessão de 5 minutos**, com `renewAfter` (hoje 90 s antes do fim). O player renova a partir daí e repete a tentativa enquanto a resposta for 503, até `expiresAt`. A renovação **repete a decisão**; sessão vencida dá 410 e **não renova**: o player abre outra e retoma pela posição que ele mesmo guarda. A política de produto (`watermark.repositionSeconds`, `progress.intervalSeconds`, `progress.minGapSeconds`) **vem na resposta**, configurável no servidor, não fixa no cliente | Renovar sessão vencida (estenderia acesso sem decisão a tempo: RF-M04); valores fixos no player (mudar a cadência exigiria republicar o SPA) | **Aprovada em 2026-10-03** |
| C-06 | **Os endereços dos segmentos na playlist não levam credencial.** O player acrescenta `segmentAccess.query`, **opaca**, obtida na abertura e substituída a cada renovação. Playlists e chave usam endereços relativos. Nada do provedor aparece no contrato (BA14) | Credencial embutida em cada endereço da playlist: expira em 5 minutos e a playlist de um vídeo VOD é lida uma vez, então o player pararia na primeira renovação; validade longa das credenciais (derrota a vida curta de RN-M01) | **Aprovada em 2026-10-03** |
| C-07 | **A chave AES-128** é um recurso da sessão (`GET /playback-sessions/{sessionId}/key`), `application/octet-stream`, `Cache-Control: no-store`, só para sessão válida **daquele** vídeo (RN-M08). A playlist guardada continua com o marcador opaco de ADR-0006; a reescrita para `../key` é de `media` | Endereço estável de chave; chave servida pela distribuição | **Aprovada em 2026-10-03** |
| C-08 | **Exceção ao gate HTTP:** três respostas 200 não são JSON (`application/vnd.apple.mpegurl` nas duas playlists e `application/octet-stream` na chave). O arquivo [.spectral-exception.yaml](.spectral-exception.yaml) estende o ruleset único e só redeclara a regra de tipo de conteúdo, aceitando esses dois tipos **em respostas**; corpo de requisição continua só JSON. Nenhuma outra regra muda | Reescrever playlist como JSON (o player não a entende); desligar a regra no conjunto todo | **Aprovada em 2026-10-03** |
| C-09 | **Avanço:** `POST .../progress` com `sequence`, `positionSeconds` e `reason`; aluno, curso e aula **saem da sessão**, nunca do corpo. Idempotente por (`sessionId`, `sequence`). Aceito também depois do fim da validade (a negação não apaga o assistido). Resposta 200 com `recorded`; menos de 10 s desde o avanço aceito anterior da mesma sessão **não vira fato**, e não é erro. A saída usa `fetch` com `keepalive`, porque `sendBeacon` não leva o cabeçalho CSRF. 410 só fora da janela de aceitação (valor da TechSpec) | Aluno e aula no corpo (permitiria atribuir avanço a outra aula); `sendBeacon` sem CSRF (abriria a escrita a pedido forjado); 429 por excesso de avanço (o aluno pagante veria erro por apertar play e pause) | **Aprovada em 2026-10-03** |
| C-10 | **Fato `midia.reproducao-avancou.v1` 1.0.0:** `eventId` determinístico por (`sessionId`, `sequence`); `tenantId`, `sessionId`, `studentId`, `courseId`, `lessonId`, `sequence`, `positionSeconds`, `reason`, `occurredAt`. **Sem e-mail, nome, título, percentual ou `videoId`.** Pelo outbox. **Retido no registro do outbox enquanto não há consumidor**, e reenviável a uma fila nova sem efeito duplicado; o mecanismo é da TechSpec | Publicar sem retenção (o broker descarta o que não tem fila ligada, e `CAP-017` nasceria sem histórico); `watchedSeconds` no fato (dado declarado pelo cliente, útil a `CAP-017`/`CAP-029`: entra como adição compatível se pedido) | **Aprovada em 2026-10-03** |
| C-11 | **Erros do aluno:** `ACCESS_DENIED` (403) carrega `reason` e, quando vencido, `accessEndedAt`; **`ACCESS_DECISION_UNAVAILABLE` (503) é outro código e outro status**, para a mensagem de indisponibilidade nunca virar "sem acesso" (RN-R02, DP-08). `LESSON_NOT_AVAILABLE` (404) é único para inexistente, de outra escola, de curso não publicado e removido da versão vigente (RN-R07). `accessEndedAt` é o instante exclusivo de `commerce`; a data exibida é a do último dia no fuso da escola | Um 403 único para negado e indisponível; `endedOn` calculado no BFF (o BFF passaria a conhecer o fuso e a regra de vigência) | **Aprovada em 2026-10-03** |
| C-12 | **`commerce` não ganha documento novo.** `decideAccessInternal` (CAP-008) já prevê `media` e `learning`; esta entrega os torna **consumidores ativos**, cada um com cache de até 30 s e chave `{tenant}:{aluno}:{curso}:v{n}` (BA07, G11), e provisiona os pares de chaves e emissores de ADR-0011 | Duplicar a operação num documento de `media` (duas descrições de uma mesma interface) | **Aprovada em 2026-10-03** |

## Evolução e compatibilidade

- **`bff-student` reprodução 1.0.0:** novo, sem versão anterior. Compatibilidade com produção não verificada: nenhum contrato do aluno está em produção.
- **`media` 1.0.1 → 1.1.0:** só acrescenta seis operações (entrega ao aluno). As de autoria por ator interno (`StaffUserToken`) não mudam; a segurança ganha o esquema `StudentUserToken`, distinto, e cada rota declara um só (ADR-0009, item 4).
- **`learning` 1.0.0 → 1.1.0:** só acrescenta uma leitura. Passa a ser **chamador de `commerce`** (C-02, C-12), e a autoria não muda.
- **Identity 1.0.0 → 1.1.0:** aditiva. Dois valores novos de audiência e uma claim nova **só** em `media`. Quem não pede essas audiências não vê diferença. Um JWT para `media` sem a claim `email` deve ser tratado por `media` como `WATERMARK_UNAVAILABLE` (422), nunca como acesso sem marca (RN-R03).
- **AsyncAPI `media` 1.1.0 → 1.2.0 (recorte):** mensagem nova; os fatos de `CAP-006` e a recepção de `conteudo.versao-publicada.v1` não mudam. Nenhum consumidor existe: não há consumidor afetado.
- **`decideAccessInternal`:** sem mudança de operação, de schema ou de versão (`commerce` 1.2.0). Muda quem a consome.
- **Mudanças incompatíveis:** nenhuma sobre contrato anterior.
- **Dados existentes:** sem preocupação com dado legado, por decisão de 2026-10-03 (OD67): nenhuma carga inicial, reenvio histórico ou compatibilidade com dado antigo é exigida. A Referência de Uso de cursos já publicados em desenvolvimento pode ser refeita publicando de novo.
- **Ordem de implantação do conjunto:** Identity 1.1.0 (audiências e claim) → provisionar emissores `media` e `learning` em `commerce` (ADR-0011) → `learning` 1.1.0 → `media` 1.1.0 → `bff-student` → `student-spa`. Os fatos de avanço começam a ser retidos na primeira reprodução, antes de existir `CAP-017`.

## Validação e verificação

| Documento | Comando e ferramenta | Padrão/ruleset | Resultado |
|---|---|---|---|
| [api-contract.yaml](api-contract.yaml) | `npx --yes @stoplight/spectral-cli@6.15.0 lint tasks/prd-reproducao-protegida/api-contract.yaml --ruleset tasks/prd-reproducao-protegida/.spectral-exception.yaml -F hint` | OpenAPI 3.1.0; ruleset único da skill + exceção C-08 | Sem erros nem avisos |
| [internal-api-contract-media.yaml](internal-api-contract-media.yaml) | mesmo comando | idem | Sem erros nem avisos |
| [internal-api-contract-learning.yaml](internal-api-contract-learning.yaml) | mesmo comando | idem | Sem erros nem avisos |
| [internal-api-contract-identity.yaml](internal-api-contract-identity.yaml) | mesmo comando | idem | Sem erros nem avisos |
| [asyncapi-contract.yaml](asyncapi-contract.yaml) | `npx --yes @asyncapi/cli@6.1.0 validate tasks/prd-reproducao-protegida/asyncapi-contract.yaml` | AsyncAPI 3.0.0 | Válido; uma informação recomenda 3.1.0, mantida a 3.0.0 da skill |
| Exemplos HTTP e de mensagem | `jsonschema` 4.26.0 (2020-12, com formato), verificação local | Schemas dos próprios documentos | **99 exemplos conformes.** Recusados como devem: `sequence` 0, posição negativa, `reason` fora do enum, e-mail ou `lessonId` no corpo do avanço, e-mail e percentual no fato |

Revisão de compatibilidade e lint não comprovam comportamento. A implementação deve verificar:

- **Abertura:** aluno com cortesia vigente → 201 com `expiresAt` 5 minutos adiante, `watermark.text` igual ao e-mail e `segmentAccess`; sem concessão → 403 `ACCESS_DENIED`/`no-grant`; concessão vencida → 403 `grant-ended` com `accessEndedAt`; Matrícula sem resposta → 503 `ACCESS_DECISION_UNAVAILABLE` e **nenhuma sessão criada**; aula com o vídeo de **outra** aula → 404 **sem** consulta a Matrícula; vídeo não pronto → 409; JWT sem `email` → 422.
- **Indistinção:** aula inexistente, de outra escola, de curso não publicado e removida na republicação devolvem o mesmo 404 `LESSON_NOT_AVAILABLE`.
- **Sigilo antes da decisão:** `getStudentLesson` negado não devolve título de curso, módulo nem aula.
- **Renovação:** antes do fim → mesma `sessionId`, nova validade e `segmentAccess`, e **uma nova decisão consultada**; decisão negada → 403 e a reprodução para em até 5 minutos mais o trecho carregado; 503 repetido até `expiresAt` → a sessão não se estende; depois do fim → 410.
- **Entrega:** playlist, variante e chave sem sessão, com sessão vencida, ou de outro aluno → recusadas; endereço de segmento de um vídeo adaptado para outro → recusado; credencial vencida → recusada; **nenhum objeto acessível sem credencial**; duas contas diferentes recebem os mesmos objetos e a mesma chave do vídeo.
- **E-mail:** busca do e-mail de um aluno de teste em log, trace, métrica, endereço, nome de objeto, chave de cache, mensagem e routing key → **nenhuma ocorrência**, exceto `watermark.text` e a claim para `media`.
- **Avanço:** 10 minutos contínuos → cerca de 20 fatos; pausa aos 252 s → fato com posição 252; mesma `sequence` duas vezes → um fato e `recorded: false` na repetição; dois avanços com menos de 10 s → o segundo com `recorded: false`; avanço depois da negação → aceito e não invalida o anterior; corpo com e-mail, aula ou curso → 400.
- **Retenção:** com nenhum consumidor ligado, os fatos **permanecem** no registro; ao ligar um consumidor novo e reenviar, cada `eventId` chega **uma vez** efetiva.
- **Concorrência:** o mesmo aluno com três sessões simultâneas → três sessões válidas, nenhuma interrompida pelas outras.
- **Cache da decisão:** duas aberturas em 30 s → uma consulta a `commerce` por chamador; revogação/término depois de 30 s → negada.
- **Linguagem:** nenhuma mensagem da tela usa "protegido contra cópia" ou equivalente (RN-M15).

## Pendências e handoff

| ID | Pendência | Responsável | Bloqueia? |
|---|---|---|---|
| P-01 | ~~Aprovar C-01 a C-12~~ **Resolvida em 2026-10-03.** C-02 vale: `learning` vira chamador de `commerce` já em `CAP-007`, e a TechSpec revisa o item 4 da ADR-0011 | Tasso | Não |
| P-02 | **TechSpec:** mecanismo de retenção e reenvio dos fatos de avanço (QA-02 do PRD, "replay") | TechSpec | Não bloqueia o contrato |
| P-03 | **TechSpec:** tempo limite da consulta de decisão antes de declarar indisponibilidade; limiar de alerta de acerto de cache; janela de aceitação do avanço depois do fim da sessão (410) | TechSpec | Não bloqueia o contrato |
| P-04 | **TechSpec:** como a distribuição valida `segmentAccess.query` e como o player a troca na renovação sem recarregar a playlist; custo de validar a sessão em Identity a cada pedido de playlist e de chave (ADR-0003) | TechSpec | Não bloqueia o contrato |
| P-05 | **TechSpec:** ADR nova ou revisão da ADR-0011 pelo item C-02, e ADR para a claim `email` em audiência `media` (C-04) | TechSpec | Não bloqueia o contrato |
| P-06 | `accessEndedAt` → data no fuso da escola: o desenho decide onde a conversão acontece | Desenho do SPA | Não |

Handoff: `tsg-flow-techspec-creator` com [prd.md](prd.md) e este índice. A TechSpec mapeia os acordos à implementação e aos cenários acima sem duplicar schemas; os tipos do SPA e os mocks do BFF podem ser derivados de [api-contract.yaml](api-contract.yaml). Antes dela, o design: **wireframe ASCII e Figma da tela da aula** são tarefas anteriores ao código, como nas demais telas do produto.
