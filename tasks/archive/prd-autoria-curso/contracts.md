---
tsg_artifact: contract
product: code-4-coders
capability: CAP-005
version: 1.0
status: approved
updated: 2026-09-28
sources: tasks/prd-autoria-curso/prd.md@1.0, domains/conteudo-e-curriculo/domain.md@1.0, domains/entrega-de-midia-e-protecao/domain.md@1.0, domains/auditoria-e-conformidade/domain.md@1.2, context/architecture-baseline.md@1.2
---

# Contratos de integração — autoria de curso

> - PRD: [prd.md](prd.md), v1.0, aprovado em 2026-09-28
> - Data da revisão: 2026-09-28
> - Estado do conjunto: **Aprovado para implementação (1.0)** em 2026-09-28 — C-01 a C-11 aprovadas pelo responsável; todos os documentos validados sem erros.

Este conjunto registra o acordo para o primeiro PRD de `CAP-005`. A aprovação significa acordo para implementar; não afirma implantação. Contratos de PRDs anteriores são preservados nas pastas de origem; as únicas mudanças neles são aditivas e estão em C-01 (Auditoria, recorte novo aqui) e C-10 (catálogo de permissões, aplicado em `tasks/prd-acesso-interno/`).

## Seleção e escopo

| Integração identificada | Modalidade | Justificativa |
|---|---|---|
| `admin-spa` → `bff-admin` → `learning` | OpenAPI | Editor, publicação e histórico são interfaces HTTP; o SPA conhece só o BFF. |
| `bff-admin` → `learning` para a consulta da trilha | OpenAPI | O título do curso alvo do ato é resolvido na consulta (C-08). |
| `learning` → Media, Auditoria e consumidores futuros | AsyncAPI | Publicação comunica fato de versão e ato (RF-10, RF-11); Media registra as Referências de Uso (RF-12). |
| Media → `learning` | AsyncAPI (sem mudança) | `learning` passa a consumir `midia.ativo-pronto`/`midia.preparacao-falhou` de CAP-006 (OD35). |
| Currículo como produto de dados | Não aplicável | O banco é interno de `learning`; nenhum consumidor recebe dataset com compromisso próprio. O estado publicado viaja no fato (C-09). Nenhum ODCS gerado. |

| Documento | Padrão e versão | Versão do contrato | Fronteira | Estado |
|---|---|---|---|---|
| [api-contract.yaml](api-contract.yaml) e [api-contract.md](api-contract.md) | OpenAPI 3.1.0 | `bff-admin` autoria 1.0.0 | Recorte `admin-spa` → `bff-admin`: 15 operações novas; as de CAP-002, CAP-006 e CAP-030 não mudam | **Aprovado para implementação** em 2026-09-28; lint sem erros nem avisos |
| [internal-api-contract-learning.yaml](internal-api-contract-learning.yaml) e [.md](internal-api-contract-learning.md) | OpenAPI 3.1.0 | `learning` 1.0.0 | `bff-admin` → `learning`: as 15 operações com JWT de ator + `resolveCourseReferencesInternal` | **Aprovado para implementação** em 2026-09-28; lint sem erros nem avisos |
| [asyncapi-contract.yaml](asyncapi-contract.yaml) | AsyncAPI 3.0.0 | `learning` 1.0.0 | `learning` produtora de `conteudo.versao-publicada.v1` e do ato; receptora dos fatos `midia.*` | **Aprovado para implementação** em 2026-09-28; parser sem erros |
| [asyncapi-contract-media.yaml](asyncapi-contract-media.yaml) | AsyncAPI 3.0.0 | `media` 1.1.0 | Recorte aditivo: Media receptora de `conteudo.versao-publicada.v1` | **Aprovado para implementação** em 2026-09-28; parser sem erros |
| [asyncapi-contract-audit.yaml](asyncapi-contract-audit.yaml) | AsyncAPI 3.0.0 | `audit` 1.2.0 | Recorte aditivo do canal `auditoria.ato-praticado.v1`; o canal de complemento (1.1.0) não muda | **Aprovado para implementação** em 2026-09-28; parser sem erros |
| `tasks/prd-acesso-interno/api-contract.yaml` e `internal-api-contract.yaml` | OpenAPI 3.1.0 | Identity 1.1.0 → 1.2.0 | Só o enum `Permission` ganha `autoria.editar` (C-10) | Aplicado; lint sem erros |

## Participantes e interfaces

| Identificador | Provedor/produtor | Consumidores | Comportamento | PRD |
|---|---|---|---|---|
| `listCourses`, `getCourse` | `bff-admin`, com decisão de `learning` | `admin-spa` | Cursos da escola; rascunho em ordem, `draftRevision`, vídeo enriquecido por Media | RF-03 a RF-05 |
| `createCourse`, `updateCourse`, `deleteCourse` | idem | `admin-spa` | Curso nasce `draft`; exclusão só de `draft` | RF-02, RF-09 |
| `createModule`, `updateModule`, `deleteModule`, `createLesson`, `updateLesson`, `deleteLesson` | idem | `admin-spa` | Edição por item (C-02), identidade estável, vídeo só pronto e da escola | RF-03, RF-04 |
| `publishCourse`, `discardCourseDraft` | idem | `admin-spa` | Conferem `draftRevision` (C-03); publicar confere completude e cria a versão vigente | RF-06, RF-07 |
| `listCourseVersions`, `getCourseVersion` | idem | `admin-spa` | Histórico e versão como foi publicada | RF-07, RF-08 |
| `*Internal` (15 operações) | `learning` | `bff-admin` | Mesmas regras; ator = `sub`, escola = `tenantId`; `X-Actor-Name` nas escritas (C-04) | RF-01 a RF-09 |
| `resolveCourseReferencesInternal` | `learning` | `bff-admin` (consulta da trilha) | Título atual do curso, papel administrador, sem `autoria.ler` | C-08 |
| `publicarVersaoPublicada` · `conteudo.versao-publicada.v1` | `learning`, por outbox | Media (agora); Aprendizagem e Catálogo (previstos) | Um fato por versão, estado completo, vale a maior `versionNumber` | RF-10 |
| `publicarAtoVersaoPublicada` · `auditoria.ato-praticado.v1` | `learning`, por outbox | `audit` | Ato `conteudo`/`versao-publicada`, alvo `curso`, sem motivo | RF-11 |
| `receberVersaoPublicada` | Media | — | Substitui as Referências de Uso do curso pelas da versão vigente | RF-12 |
| `receberAtivoPronto`, `receberPreparacaoFalhou` | `learning` | — | Visão local do que pode ser vinculado | RF-04 |
| `receberAtoPraticado` 1.2.0 | `audit` | — | Aceita `versao-publicada` como conforme | RF-11 |

Relação entre modalidades:

| Ação | Operação HTTP | Mensagens (mesma transação) |
|---|---|---|
| Publicar ou republicar | `publishCourse` | `conteudo.versao-publicada.v1` + `auditoria.ato-praticado.v1` (`fatoId` = `eventId`) |
| Qualquer alteração de rascunho, descarte, exclusão | demais escritas | nenhuma |

`courseId`, `moduleId`, `lessonId` e `videoId` são os mesmos nas APIs, no fato de versão, na Referência de Uso e no alvo do ato.

## Origem e decisões

| Entrada consultada | Versão e data | Decisão herdada |
|---|---|---|
| [PRD de CAP-005](prd.md) | v1.0, 2026-09-28 | RF-01 a RF-12, DP-01 a DP-04; OD35, OD40 a OD45 |
| [Conteúdo e Currículo](../../../domains/conteudo-e-curriculo/domain.md) | v1.0, 2026-09-28 | RN-C01 a RN-C13, RN-C17 |
| [Entrega de Mídia e Proteção](../../../domains/entrega-de-midia-e-protecao/domain.md) | v1.0, 2026-09-25 | RN-M02, RN-M05, RN-M09, RN-M10 |
| [Auditoria e Conformidade](../../../domains/auditoria-e-conformidade/domain.md) | v1.2, 2026-09-27 | RN-A03, RN-A05, RN-A06, RN-A08, RN-A14 |
| [Baseline](../../../context/architecture-baseline.md) | v1.2, 2026-09-21 | BFF por audiência, autorização em duas camadas, G06, G07, G08, G23 |
| [ADR-0005](../../../docs/adr/0005-sessao-e-servico-do-backoffice.md) | aceita em 2026-09-25 | Sessão validada em Identity a cada ação; JWT curto com `permissions` e `roles`, validado por JWKS no serviço dono |
| [Contratos de CAP-006](../prd-ingestao-midia/contracts.md) | 1.1, 2026-09-26 | Convenções HTTP do backoffice; `midia.*` 1.0.0 consumidos sem mudança; C-12 (validação por visão local); C-13 (nome como retrato); padrão de `*_UNAVAILABLE` |
| [Auditoria, fatia mínima](../prd-trilha-auditoria/asyncapi-contract.yaml) e [consulta](../prd-consulta-trilha-auditoria/contracts.md) | 1.0.1 e 1.1.0/1.1, 2026-09-27 | Envelope, idempotência por (`origem`, `fatoId`), tolerância a ato incompleto; `label` ausente quando não resolvido |
| Código de `audit` (`AdministrativeActPolicy`) | `main` em 2026-09-28 | Confirma que tipo fora da lista vira `tipo-desconhecido` (base de C-01) |

Decisões deste contrato:

| ID | Decisão | Alternativa descartada | Estado |
|---|---|---|---|
| C-01 | **Auditoria evolui a 1.2.0, aditiva:** aceita `origem: conteudo`, `tipo: versao-publicada` sem motivo, alvo `{tipo: curso}` e `complemento.versao`. **Corrige o PRD**, que diz "a Auditoria não muda": o envelope é da Auditoria (RN-A14), mas o contrato 1.0.1 e o código aceitam só os quatro tipos de Identidade, e o ato chegaria como não conforme, contrariando RF-11. O próprio contrato prevê tipo novo como versão minor | Deixar o ato chegar não conforme e "complementar" depois: poluiria a trilha e o alerta de operação a cada publicação | **Aprovada em 2026-09-28** |
| C-02 | **Edição do rascunho por item**; cada escrita devolve o curso inteiro com a nova `draftRevision` | PUT do rascunho inteiro: uma gravação apagaria mudanças do colega e um id omitido viraria aula removida | **Decidida em 2026-09-28** |
| C-03 | **Publicar e descartar conferem `draftRevision`** e respondem `409 DRAFT_CHANGED` se o rascunho mudou. Edição não confere (OD44) | Publicar o rascunho atual sem conferir: publicaria mudança que o professor não viu | **Decidida em 2026-09-28** |
| C-04 | **Nome do ator como retrato**, repassado pelo BFF em `X-Actor-Name` nas escritas internas (mesmo racional de C-13 de CAP-006). Identidade vem de `sub`. Só para exibição; nunca em log, fato ou métrica | Resolver o nome em Identity a cada listagem: exigiria `acesso.gerir`, que o professor não tem | **Aprovada em 2026-09-28** |
| C-05 | **Título e duração do vídeo da aula vêm de Media na consulta**, pelo BFF, com o JWT de audiência `media` do próprio professor (`midia.enviar`). Media fora → a aula traz só `videoId`, sem 502 | `learning` copiar título do vídeo: o título muda em Media (RF-07 de CAP-006) e a cópia envelheceria | **Aprovada em 2026-09-28** |
| C-06 | **Conteúdo do ato de publicação:** `autor` = conta interna, `alvo` = `{curso, courseId}`, `complemento.versao`, `praticadoEm` = `publishedAt`, `fatoId` = `eventId` do fato de versão | `fatoId` próprio: perderia a correlação direta entre trilha e versão sem ganho | **Aprovada em 2026-09-28** |
| C-07 | **Limites (fecha QP-02 do PRD):** título 1–200, descrição ≤ 5 000, nota ≤ 1 000; ≤ 100 módulos por curso e ≤ 200 aulas por módulo; `STRUCTURE_LIMIT_REACHED` acima | Sem limite: estrutura sem teto no fato e no editor | **Aprovada em 2026-09-28** |
| C-08 | **Título do curso na consulta da trilha resolvido por `learning`** (`resolveCourseReferencesInternal`, papel administrador). Muda o comportamento do BFF da consulta de CAP-030 — o `label` do alvo `curso` passa a vir de `learning` —, sem mudar schema: `IdentityReference.type` já é texto livre e `label` já é opcional. O `admin-spa` ganha o rótulo do tipo "Versão publicada" (fecha QP-01 do PRD) | Mostrar só "Curso" e o id: o administrador não saberia qual curso mudou | **Decidida em 2026-09-28** |
| C-09 | **O fato de versão carrega o estado completo da versão vigente**, com títulos de curso, módulo e aula, sem descrição e sem nota (fecha QP-03 do PRD). Consumidores substituem o que sabiam do curso; nenhum precisa consultar `learning` para a estrutura | Fato só com ids + leitura síncrona: acoplaria Catálogo e Aprendizagem a `learning` no caminho de exibição | **Aprovada em 2026-09-28** |
| C-10 | **`autoria.editar` entra no enum `Permission`** de `tasks/prd-acesso-interno/` (Identity 1.1.0 → 1.2.0, aditivo), concedida só ao professor; Identity passa a emitir `audience: learning` | Permissão fora do catálogo de Identity: contraria RN-12/RN-18 | **Aprovada em 2026-09-28** (arquivos atualizados, mesmo procedimento de C-14 de CAP-006) |
| C-11 | **Media aplica a maior `versionNumber` por curso** e substitui o conjunto de referências; fato mais antigo é confirmado sem efeito | Acumular referências de todas as versões: aula removida continuaria abrindo sessão em CAP-007 | **Aprovada em 2026-09-28** |

## Evolução e compatibilidade

- **`bff-admin` autoria 1.0.0 e `learning` 1.0.0:** novos, sem versão anterior. Compatibilidade com produção não verificada.
- **`conteudo.versao-publicada.v1`:** novo. `CourseStatus` e as pendências podem crescer; consumidores devem tolerar valor novo.
- **Media 1.0.0 → 1.1.0 (C-11):** só acrescenta uma operação de recepção; os fatos `midia.*` não mudam.
- **Auditoria 1.1.0 → 1.2.0 (C-01):** mudança de **receptor**, aditiva. Produtores atuais (Identity) continuam válidos: os quatro tipos e suas regras não mudam. **Ordem de implantação obrigatória:** `audit` 1.2.0 antes de `learning` publicar o primeiro ato, senão o ato é gravado como não conforme (`tipo-desconhecido`) — não se perde, mas polui a trilha. O schema da Auditoria não tem `additionalProperties: false` no payload; isso é herdado e não muda.
- **Consulta da trilha (CAP-030):** mudança só de comportamento no BFF (C-08). Registros de Identidade continuam resolvidos por Identity; o tipo desconhecido para a tela antiga apareceria com o código, por isso o rótulo entra junto.
- **Identity 1.1.0 → 1.2.0 (C-10):** aditiva. Clientes de `Permission` ignoram valor desconhecido (regra do contrato de CAP-002). A audiência `learning` é configuração de emissores, não campo novo.
- **Media interna (CAP-006):** não muda. O seletor de vídeo segue exigindo `midia.enviar`; a nota de C-12 de CAP-006 ("poderá exigir a permissão de autoria") não foi adotada, porque o professor tem as duas.

## Validação e verificação

| Documento | Comando e ferramenta | Padrão/ruleset | Resultado |
|---|---|---|---|
| [api-contract.yaml](api-contract.yaml) | `npx --yes @stoplight/spectral-cli@6.15.0 lint tasks/prd-autoria-curso/api-contract.yaml --ruleset .claude/skills/tsg-flow-contract-creator/rulesets/openapi.yaml --fail-severity=error` | OpenAPI 3.1.0, ruleset local | Sem erros nem avisos |
| [internal-api-contract-learning.yaml](internal-api-contract-learning.yaml) | mesmo comando | OpenAPI 3.1.0, ruleset local | Sem erros nem avisos |
| `tasks/prd-acesso-interno/api-contract.yaml` e `internal-api-contract.yaml` 1.2.0 | mesmo comando | OpenAPI 3.1.0, ruleset local | Sem erros |
| [asyncapi-contract.yaml](asyncapi-contract.yaml), [-media](asyncapi-contract-media.yaml), [-audit](asyncapi-contract-audit.yaml) | `npx --yes @asyncapi/cli@6.1.0 validate <arquivo>` | AsyncAPI 3.0.0 | Válidos, referências externas resolvidas; uma informação recomenda 3.1.0, mantida a 3.0.0 da skill |
| Exemplos de mensagem | `jsonschema` 4.26.0 (Draft 7, com formato) | Payloads de `learning` e `audit` 1.2.0 | `versao3` e os três exemplos da Auditoria conformes; campo extra recusado no fato de versão; `complemento.versao: '0'` recusado |

O contrato interno é derivado do público por transformação mecânica (mesmos schemas; segurança, erros e vídeo da aula trocados), para que os dois não divirjam.

A validade estrutural não comprova comportamento. A implementação deve verificar:

- renomear, mover de módulo e trocar vídeo preservam `lessonId`; remover e criar gera id novo;
- `publishCourse` com `draftRevision` antiga → `DRAFT_CHANGED`, nada publicado; com pendências → `COURSE_INCOMPLETE` com cada falta; duplo clique → uma versão;
- duas publicações concorrentes → números distintos e sequenciais;
- versão, `conteudo.versao-publicada` e ato nascem juntos ou não nascem (falha forçada entre eles);
- `updateLesson` com vídeo em preparação, que falhou, inexistente ou de outra escola → `VIDEO_NOT_AVAILABLE`; vídeo pronto aceito após `midia.ativo-pronto`;
- curso, módulo ou aula de outra escola ou de outro curso → 404 indistinto;
- professor com papel revogado → 401 no BFF na próxima ação; ator sem `autoria.editar` → 403 no BFF **e** em `learning`, chamado direto com JWT sem a permissão; `resolveCourseReferencesInternal` sem papel administrador → 403;
- Media: versão 2 sem a aula A invalida (curso, A); versão 1 atrasada não muda nada; reentrega não duplica;
- ato de publicação chega à trilha **conforme** e aparece na consulta com o título do curso;
- nenhum nome, e-mail, título ou descrição em log, span, métrica ou erro; nenhum dado pessoal nos fatos.

## Pendências e handoff

1. **Errata do PRD:** a seção "Restrições Técnicas de Alto Nível" diz que a Auditoria não muda; C-01 mostra que muda (aditivo). QP-01, QP-02 e QP-03 do PRD ficam fechadas por C-08, C-07 e C-09. Não bloqueia a TechSpec; a errata pode ir numa revisão 1.1 do PRD.
2. **Ordem de implantação:** Identity com `autoria.editar` e `audience: learning` → `audit` 1.2.0 → Media com `receberVersaoPublicada` → `learning` e `bff-admin` expondo a autoria.
3. **Para a TechSpec:** mecanismo da visão local de vídeos, da revisão do rascunho e da renumeração de posições; tempos-limite do BFF para `learning` e para o enriquecimento por Media.

Para `tsg-flow-techspec-creator`: usar este índice, [api-contract.yaml](api-contract.yaml), [internal-api-contract-learning.yaml](internal-api-contract-learning.yaml), [asyncapi-contract.yaml](asyncapi-contract.yaml), [asyncapi-contract-media.yaml](asyncapi-contract-media.yaml) e [asyncapi-contract-audit.yaml](asyncapi-contract-audit.yaml), sem duplicar schemas.
