---
tsg_artifact: contract
product: code-4-coders
capability: CAP-017
version: 1.0
status: approved
updated: 2026-10-04
sources: tasks/prd-progresso-aluno/prd.md@1.0, domains/entrega-de-midia-e-protecao/domain.md@1.0, domains/matricula-e-direito-de-acesso/domain.md@1.0, domains/conteudo-e-curriculo/domain.md@1.1, context/architecture-baseline.md@1.2
---

# Contratos de integração — progresso do aluno

> - PRD: [prd.md](prd.md), v1.0, aprovado em 2026-10-04
> - Data: 2026-10-04
> - Estado do conjunto: **Aprovado para implementação** em 2026-10-04. C-01 a C-10 aprovadas pelo responsável. Todos os documentos técnicos validados sem erros.

Este conjunto registra o acordo para o primeiro PRD de `CAP-017`. Parte dos contratos vigentes em `contracts/` (ver `contracts/README.md`), consultados em 2026-10-04, e registra só o que muda: todos os documentos são **recortes aditivos**. Não afirma qual contrato está em produção (nenhum está: o sistema só vai a homologação ao fim do MVP). Catalogação, armazenamento definitivo e mecanismos de atualização do acervo são do time de plataforma.

## Seleção e escopo

| Integração identificada | Modalidade | Justificativa |
|---|---|---|
| `student-spa` → `bff-student` | OpenAPI | Meus cursos e progresso do curso na tela da aula (RF-03 a RF-06). |
| `bff-student` → `learning` | OpenAPI | `learning` é dono do progresso e compõe meus cursos (RF-01 a RF-06). |
| `learning` → `commerce` | OpenAPI | Lista dos cursos do aluno em Matrícula, vigentes e encerrados (RF-04). `decideAccessInternal` ganha um uso, sem mudar. |
| `media` → `learning` | AsyncAPI | Consumo de `midia.reproducao-avancou.v1` (RF-01, RF-02) e da duração em `midia.ativo-pronto.v1` (RN-P06). |
| `media` → `learning`, uma vez | AsyncAPI | Reenvio dos avanços retidos desde `CAP-007` (RF-01). |
| `bff-student` → `identity` | Sem mudança | O JWT de aluno com audiência `learning` e escopo `lessons:read` já existe (ADR-0013); C-06. |
| Avanço bruto como insumo de `CAP-029` | Não aplicável agora | Banco interno de `learning`. `CAP-029` (Fase 2) consulta dentro do mesmo serviço ou define o acordo no PRD dela. Nenhum ODCS. |

| Documento | Padrão e versão | Versão do contrato | Fronteira | Estado |
|---|---|---|---|---|
| [api-contract.yaml](api-contract.yaml) e [.md](api-contract.md) | OpenAPI 3.1.0 | `bff-student/openapi-aula.yaml` 1.0.0 → 1.1.0 | Recorte: 2 leituras novas; as 7 operações de `CAP-007` não mudam | **Aprovado para implementação**; lint sem erros, 1 aviso registrado (C-05) |
| [internal-api-contract-learning.yaml](internal-api-contract-learning.yaml) e [.md](internal-api-contract-learning.md) | OpenAPI 3.1.0 | `learning/openapi-internal.yaml` 1.2.0 → 1.3.0 | Recorte: 2 leituras do aluno; autoria e `getStudentLessonInternal` não mudam | **Aprovado para implementação**; lint sem erros, 1 aviso registrado (C-05) |
| [internal-api-contract-commerce.yaml](internal-api-contract-commerce.yaml) e [.md](internal-api-contract-commerce.md) | OpenAPI 3.1.0 | `commerce/openapi-internal.yaml` 1.2.0 → 1.3.0 | Recorte: 1 leitura nova de Matrícula | **Aprovado para implementação**; lint sem erros, 1 aviso registrado (C-05) |
| [asyncapi-contract.yaml](asyncapi-contract.yaml) | AsyncAPI 3.0.0 | `learning/asyncapi.yaml` 1.1.0 → 1.2.0 | Recorte: recepção nova do avanço; recepção de vídeo pronto passa a guardar a duração | **Aprovado para implementação**; parser sem erros |
| [asyncapi-contract-media.yaml](asyncapi-contract-media.yaml) | AsyncAPI 3.0.0 | `media/asyncapi.yaml` 1.2.0 → 1.3.0 | Recorte: reenvio único dos avanços retidos | **Aprovado para implementação**; parser sem erros |

## Participantes e interfaces

| Identificador | Provedor/produtor | Consumidores | Comportamento | PRD |
|---|---|---|---|---|
| `listMyCourses` → `listStudentCoursesInternal` | `bff-student` → `learning` | `student-spa` | Duas listas (`active`, `ended`) vindas de Matrícula, com título da versão vigente, progresso e `continueLessonId`; 503 se Matrícula não responde; `progressAvailable: false` se só o progresso falha | RF-04, RF-05, RN-P08, RN-P10 |
| `getCourseProgress` → `getStudentCourseProgressInternal` | `bff-student` → `learning` | `student-spa` (tela da aula) | Contagem e percentual na versão vigente; por aula com registro, `completed`, `lastPositionSeconds` e `resumeAtSeconds`; exige decisão de acesso | RF-03, RF-05, RF-06, RN-P07, RN-P09 |
| `listStudentCourseAccessInternal` | `commerce` (Matrícula) | `learning` | Um item por curso com concessão: `active` (`since`) ou `ended` (`endedOn`, `endedAt`, `endedReason`); sem motivo, origem nem `grantId` | RF-04, RN-D06, RN-D07 |
| `decideAccessInternal` (**sem mudança**) | `commerce` | `media`, `learning` | Agora também antes de `getStudentCourseProgressInternal`, com o mesmo cache de até 30 s | RF-06 |
| `receberReproducaoAvancou` · `midia.reproducao-avancou.v1` | `media` (1.2.0, sem mudança) | `learning` | Registro bruto, idempotente por `eventId`, última posição por `occurredAt`, conclusão a 90% ou no fim | RF-01, RF-02, RN-P01 a RN-P05 |
| `receberAtivoPronto` · `midia.ativo-pronto.v1` (revisada) | `media` (sem mudança) | `learning` | Passa a guardar `durationSeconds` no módulo de progresso | RN-P06 |
| `reenviarReproducaoAvancouRetido` | `media` | `learning` | Reenvio único dos fatos retidos, mesmo `eventId` | RF-01 |

| Ação do aluno | Operações HTTP | Mensagens |
|---|---|---|
| Entrar e ver meus cursos | `listMyCourses` → `validateStudentSessionInternal` (audiência `learning`) → `listStudentCoursesInternal` → `listStudentCourseAccessInternal` | nenhuma |
| Continuar / Começar | abre a aula `continueLessonId` (fluxo de `CAP-007`) | nenhuma |
| Abrir a aula | `getStudentLesson` e `getCourseProgress` em paralelo; o player começa em `resumeAtSeconds` | nenhuma |
| Assistir | `recordPlaybackProgress` (`CAP-007`, sem mudança); `getCourseProgress` ao pausar, no fim e a cada 60 s | `midia.reproducao-avancou.v1` → `learning` |
| Vídeo fica pronto | — | `midia.ativo-pronto.v1` → `learning` guarda a duração |

`studentId` é o `sub` do JWT de aluno (o `accountId` de Identity, o mesmo de `CAP-008` e do fato de avanço); `courseId` e `lessonId` são os de Conteúdo, os mesmos do fato de avanço; `sessionId` é o da Sessão de Reprodução de `CAP-007`.

## Origem e decisões

| Entrada consultada | Versão e data | Decisão herdada |
|---|---|---|
| [PRD de CAP-017](prd.md) | v1.0, 2026-10-04 | RF-01 a RF-06, RN-P01 a RN-P10, DP-01 a DP-09; OD71, OD72, OD73 |
| `contracts/bff-student/openapi-aula.yaml` | 1.0.0, consultado em 2026-10-04 | `getStudentLesson`, `recordPlaybackProgress`, `Problem` com `reason`/`accessEndedAt`, `StudentSessionCookie` |
| `contracts/learning/openapi-internal.yaml` | 1.2.0 | `getStudentLessonInternal` (ordem 404 → decisão), `StudentUserToken`, erros `ACCESS_DENIED`, `ACCESS_DECISION_UNAVAILABLE` |
| `contracts/commerce/openapi-internal.yaml` | 1.2.0 | `decideAccessInternal`, `DomainServiceAssertion`, `AccessGrant` (`endsOn`, `expiresAt`, `status` calculado na leitura) |
| `contracts/media/asyncapi.yaml` | 1.2.0 | `ReproducaoAvancou` (compromissos 1 a 6, retenção sem consumidor) e `AtivoPronto` (`durationSeconds`) |
| `contracts/learning/asyncapi.yaml` | 1.1.0 | `receberAtivoPronto` já existente; padrão de reenvio único (`reenviarVersaoVigente`) |
| [Entrega de Mídia e Proteção](../../domains/entrega-de-midia-e-protecao/domain.md) | v1.0 | RN-M02, RN-M14; duração é atributo da Mídia |
| [Matrícula e Direito de Acesso](../../domains/matricula-e-direito-de-acesso/domain.md) | v1.0 | RN-D01, RN-D06, RN-D07, RN-D13, RN-D17 |
| [Conteúdo e Currículo](../../domains/conteudo-e-curriculo/domain.md) | v1.1 | RN-C04, RN-C07 a RN-C09, RN-C15 |
| [Baseline](../../context/architecture-baseline.md) | v1.2 | BA07, BA12, G06, G07, G09, G10, G11, G15, G18, G20, G23, G25 |
| [ADR-0003](../../docs/adr/0003-verificacao-de-sessao-do-aluno.md), [ADR-0013](../../docs/adr/0013-jwt-de-aluno-validado-por-servicos-de-dominio.md) | aceitas | Sessão validada pelo BFF; JWT de aluno por audiência, aluno = `sub` |
| [ADR-0012](../../docs/adr/0012-autenticacao-de-servico-media-learning-em-commerce-revisada.md) | aceita | `learning` emissor em `commerce`; **escopo único** `access-decision:read`, revisado por C-03 |
| Contratos de `CAP-007` (`tasks/archive/prd-reproducao-protegida/contracts.md`) | 1.0, histórico | C-09/C-10 (avanço e retenção), C-11 (erros do aluno). Referência histórica, confirmada em `contracts/` |

Decisões deste contrato:

| ID | Decisão | Alternativa descartada | Estado |
|---|---|---|---|
| C-01 | **Duas leituras novas no BFF do aluno**, no arquivo da tela da aula: `GET /my-courses` e `GET /courses/{courseId}/progress`. O progresso do curso é **separado** de `getStudentLesson`: a falha de um não derruba o outro (o vídeo toca mesmo sem progresso), e a atualização periódica da marca de concluída não relê a estrutura | Embutir o progresso em `LessonScreen` (acopla a falha; atualizar a marca exigiria reler estrutura e decisão a cada 60 s; e muda um schema `additionalProperties: false` de `CAP-007`) | **Aprovada em 2026-10-04** |
| C-02 | **"Meus cursos" é composto por `learning`**, que pede a Matrícula os cursos do aluno (`listStudentCourseAccessInternal`) com a asserção de serviço dele e junta título, estrutura e progresso. O BFF só repassa | O BFF compor (`commerce` + `learning`): exigiria um JWT de aluno com audiência `commerce` (mudança em Identity e na ADR-0013) e poria no BFF a regra de ordenação e de Continuar | **Aprovada em 2026-10-04** |
| C-03 | **Escopo novo `course-access:read` em `commerce`**, concedido só ao emissor `learning`. `access-decision:read` não dá acesso à lista. A TechSpec revisa a ADR-0012 (item 2, "escopo único") | Reusar `access-decision:read` (`media` passaria a poder listar cursos de qualquer aluno sem precisar) | **Aprovada em 2026-10-04** |
| C-04 | **O progresso do curso exige a decisão de acesso**, na ordem de `getStudentLessonInternal`: curso com versão vigente (404 `COURSE_NOT_AVAILABLE`) → decisão (403 `ACCESS_DENIED`, 503 `ACCESS_DECISION_UNAVAILABLE`). O cache de 30 s da decisão é o mesmo da abertura da aula. O curso encerrado mostra seu progresso só por `listMyCourses` | Sem decisão (devolveria identificadores e contagem de aulas de curso que o aluno não tem: RN-R07) | **Aprovada em 2026-10-04** |
| C-05 | **Sem paginação, com teto fixo**: 500 cursos por lista em meus cursos e na resposta de Matrícula; 20000 aulas no progresso (100 módulos × 200 aulas, o limite da estrutura). Os três avisos `tsg-collection-pagination-params` são essa exceção; em `/courses/{courseId}/progress` o aviso é falso positivo (recurso único) | Paginar meus cursos (a ordenação por avanço mais recente cruza Matrícula e progresso; paginar exigiria trazer tudo de qualquer forma) | **Aprovada em 2026-10-04** |
| C-06 | **`learning` reaproveita o escopo `lessons:read`** do JWT de aluno nas duas leituras novas. Identity não muda | Escopo novo `progress:read` (a ADR-0013 configura um escopo por audiência; mudaria Identity sem ganho de separação, porque as leituras são todas do próprio aluno) | **Aprovada em 2026-10-04** |
| C-07 | **A duração vem de `midia.ativo-pronto.v1`**, que `learning` já consome: a recepção passa a guardar `durationSeconds` por `videoId` **no módulo de progresso**, nunca em Conteúdo (RN-C04, RN-C15). Media não muda | Leitura síncrona da duração em `media` (dependência síncrona nova no consumo do avanço); `durationSeconds` no fato de avanço (mudaria o fato de Media e o player declararia a duração). **Efeito aceito:** vídeos que ficaram prontos antes desta entrega não têm a duração guardada (o fato já foi deduplicado) e só concluem pelo avanço de fim, sem carga inicial (sem dados legados até o MVP) | **Aprovada em 2026-10-04** |
| C-08 | **Reenvio único dos avanços retidos** (`reenviarReproducaoAvancouRetido` em `media`), com o mesmo `eventId`, **depois** de a fila de `learning` existir. Repetível sem efeito | Ligar a fila e confiar só no que vier dali em diante (perde o histórico que `CAP-007` reteve de propósito, contra RF-01) | **Aprovada em 2026-10-04** |
| C-09 | **Encerrado leva `endedOn` e `endedReason`** (`grant-ended`; `revoked` virá com `CAP-009`). Matrícula calcula o último dia no fuso da escola; ninguém mais calcula. Fecha QA-03 do PRD: a data existe para qualquer motivo de encerramento | `endedOn` calculado em `learning` ou no BFF a partir de `expiresAt` (espalharia fuso e regra de vigência) | **Aprovada em 2026-10-04** |
| C-10 | **Retomada e Continuar calculados em `learning`** (`resumeAtSeconds`, `continueLessonId`): RN-P09 e RN-P10 vivem no dono do progresso, não no SPA | O SPA calcular a partir das posições (a regra se repetiria em cada cliente e precisaria da duração no navegador) | **Aprovada em 2026-10-04** |

## Evolução e compatibilidade

- **`bff-student/openapi-aula.yaml` 1.0.0 → 1.1.0:** só acrescenta duas operações, schemas e respostas com nomes novos (`CourseNotAvailable`, `CourseAccessUnavailable`). Nenhum schema de `CAP-007` muda. O SPA é o único consumidor.
- **`learning/openapi-internal.yaml` 1.2.0 → 1.3.0:** só acrescenta duas leituras. As de autoria (`StaffUserToken`) e `getStudentLessonInternal` não mudam.
- **`commerce/openapi-internal.yaml` 1.2.0 → 1.3.0:** só acrescenta uma leitura e um escopo. `decideAccessInternal` não muda. **Mudança de configuração:** o emissor `learning` passa a ter dois escopos (C-03).
- **`learning/asyncapi.yaml` 1.1.0 → 1.2.0:** recepção nova; `receberAtivoPronto` ganha um efeito (guardar a duração) sem mudar a mensagem nem as garantias. Os envios não mudam, então nenhum consumidor de `learning` é afetado.
- **`media/asyncapi.yaml` 1.2.0 → 1.3.0:** operação de envio nova, sobre canal e mensagem existentes. `learning` é o único consumidor do canal.
- **Mudanças incompatíveis:** nenhuma.
- **Dados existentes:** sem dado legado até o MVP. Os avanços retidos em desenvolvimento chegam pelo reenvio (C-08). A duração de vídeos já prontos não é recarregada (C-07).
- **Ordem de implantação:** `commerce` 1.3.0 (operação e escopo) → `learning` (fila ligada a `midia.reproducao-avancou.v1`, consumo, duração e leituras) → `media` executa `reenviarReproducaoAvancouRetido` → `bff-student` 1.1.0 → `student-spa`.

## Validação e verificação

| Documento | Comando e ferramenta | Padrão/ruleset | Resultado |
|---|---|---|---|
| [api-contract.yaml](api-contract.yaml) | `npx --yes @stoplight/spectral-cli@6.17.0 lint tasks/prd-progresso-aluno/api-contract.yaml --ruleset contracts/.spectral-exception.yaml -F hint` | OpenAPI 3.1.0; ruleset único da skill (+ exceção C-08 de `CAP-007`, que não afeta estas operações JSON) | 0 erros; 1 aviso `tsg-collection-pagination-params` (C-05) |
| [internal-api-contract-learning.yaml](internal-api-contract-learning.yaml) | mesmo comando | idem | 0 erros; 1 aviso (C-05) |
| [internal-api-contract-commerce.yaml](internal-api-contract-commerce.yaml) | mesmo comando | idem | 0 erros; 1 aviso (C-05) |
| [asyncapi-contract.yaml](asyncapi-contract.yaml) | `npx --yes @asyncapi/cli@6.1.0 validate tasks/prd-progresso-aluno/asyncapi-contract.yaml` | AsyncAPI 3.0.0 | Válido; uma informação recomenda 3.1.0, mantida a 3.0.0 da skill |
| [asyncapi-contract-media.yaml](asyncapi-contract-media.yaml) | mesmo comando | AsyncAPI 3.0.0 | Válido; mesma informação |
| Exemplos HTTP e de mensagem | `jsonschema` 4.26.0 (2020-12, com formato), verificação local | Schemas dos próprios documentos e de `contracts/media/asyncapi.yaml` | **31 exemplos HTTP conformes** e o exemplo do fato de avanço conforme. Recusados como devem: percentual 101, campo `email` em curso, campo `reason` (motivo da cortesia) na resposta de Matrícula |

Lint e revisão não comprovam comportamento. A implementação deve verificar:

- **Consumo do avanço:** mesmo `eventId` duas vezes → um registro; avanço às 14:10 na posição 480 e outro às 14:02 na 252, chegando nesta ordem → última posição 480; dois aparelhos (5 min às 10:00, 2 min às 11:00) → 120; valor novo de `reason` → registrado, não conclui; aula fora da versão vigente → registrada, fora do percentual; o registro guarda sessão, aluno, curso, aula, posição, motivo e instante, e nada mais.
- **Conclusão:** aula de 600 s → 540 conclui, 530 não; `ended` conclui em qualquer posição; avanço antes da duração conhecida → conclui ao chegar a duração, sem novo avanço; rever e parar no minuto 2 → continua concluída.
- **Reenvio:** com avanços retidos e a fila ligada, `reenviarReproducaoAvancouRetido` → cada `eventId` efetivo uma vez; repetir o reenvio → nenhuma mudança.
- **Percentual:** 3 de 8 → 37; 2 de 3 → 66; 10 de 10 republicado com 12 → 83; aula concluída removida → fora do numerador e do total; volta numa versão seguinte → conta de novo.
- **Meus cursos:** cortesia vigente em 2 cursos → ordem por avanço mais recente, nunca começado por último; cortesia vencida → em `ended` com `endedOn`; nova cortesia sobre curso encerrado → volta a `active` com o progresso; sem concessão → listas vazias; progresso de curso sem concessão → não aparece; `commerce` sem resposta → 503 `COURSE_ACCESS_UNAVAILABLE`, nunca 200 vazio; falha só no progresso → `progressAvailable: false`.
- **Matrícula:** asserção de `media` em `/course-access` → 403 `SCOPE_DENIED`; aluno de outra escola → lista vazia; a resposta nunca traz motivo, origem nem `grantId`.
- **Progresso do curso:** sem concessão → 403 sem nada do curso; Matrícula sem resposta → 503; curso inexistente ou de outra escola → 404 indistinto; aula sem registro ausente de `lessons`.
- **Retomada e Continuar:** RN-P09 (fim, menos de 10 s do fim, posição além da duração atual → 0) e os cinco casos de RN-P10 do PRD.
- **Prazo:** avanço publicado → reflexo em meus cursos e no progresso do curso em até 1 minuto (p95).
- **Segurança:** token de ator nas rotas de aluno de `learning` → 401; aluno sempre o `sub`, nenhum parâmetro de aluno aceito; e-mail do aluno ausente de toda saída de `learning`.

## Pendências e handoff

| ID | Pendência | Responsável | Bloqueia? |
|---|---|---|---|
| P-01 | ~~Aprovar C-01 a C-10~~ **Resolvida em 2026-10-04.** | Tasso | Não |
| P-02 | Revisar a ADR-0012 (escopo `course-access:read` para `learning`, C-03) | TechSpec | Não bloqueia o contrato; bloqueia a implementação de `commerce` |
| P-03 | Prazo de guarda do avanço bruto e do registro retido em `media` depois do reenvio (QA-01 do PRD) | Encarregado de dados + negócio | Não |

Depois da aprovação: `tsg-flow-techspec-creator` a partir de [prd.md](prd.md) e deste [contracts.md](contracts.md), mapeando cada operação e mensagem aos módulos (`learning` Progress, `commerce` Matrícula, `media` outbox, `bff-student`, `student-spa`) e aos cenários acima, sem duplicar schema.
