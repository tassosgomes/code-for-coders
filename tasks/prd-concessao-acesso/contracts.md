---
tsg_artifact: contract
product: code-4-coders
capability: CAP-008
version: 1.1
status: approved
updated: 2026-10-01
sources: tasks/prd-concessao-acesso/prd.md@1.1, domains/matricula-e-direito-de-acesso/domain.md@1.0, domains/identidade-e-acesso/domain.md@1.1, domains/auditoria-e-conformidade/domain.md@1.2, domains/conteudo-e-curriculo/domain.md@1.1, context/architecture-baseline.md@1.2
---

# Contratos de integração — concessão de acesso

> - PRD: [prd.md](prd.md), v1.1 (errata de nome da área), aprovado em 2026-10-01
> - Data da revisão: 2026-10-01
> - Estado do conjunto: **Aprovado para implementação (1.1)** em 2026-10-01 — C-01 a C-11 aprovadas pelo responsável; todos os documentos validados sem erros.
> - **Revisão 1.1 (2026-10-01), antes de qualquer implementação:** acrescenta a operação de leitura `previewCourtesyTerm` (C-11), pedida pela TechSpec (D-03), e renomeia a área para **Cortesias** (o nome "Acessos" já é da gestão de acesso interno). Nenhuma outra operação, mensagem ou schema muda; os documentos YAML conservam as versões, porque nenhuma 1.0.0 foi implantada.

Este conjunto registra o acordo para o primeiro PRD de `CAP-008`. A aprovação significa acordo para implementar; não afirma implantação. Contratos de PRDs anteriores são preservados nas pastas de origem; as únicas mudanças neles são **aditivas**, estão em C-06 (Auditoria, recorte novo aqui), C-07 (referência `conta-aluno` na resolução da trilha) e C-08 (catálogo de permissões), e foram aplicadas nos arquivos de `tasks/prd-acesso-interno/` e `tasks/prd-consulta-trilha-auditoria/`, aprovadas com este conjunto.

## Seleção e escopo

| Integração identificada | Modalidade | Justificativa |
|---|---|---|
| `admin-spa` → `bff-admin` | OpenAPI | Localizar aluno, listar cursos e concessões, conceder cortesia (RF-01 a RF-05). |
| `bff-admin` → `commerce` | OpenAPI | As mesmas regras, decididas pelo serviço dono com o JWT do ator; e a **decisão de acesso**, consultada por `media` e `learning` (RF-03, RF-04, RF-06). |
| `bff-admin` → `identity`; `commerce` → `identity` | OpenAPI | Localização do beneficiário pelo e-mail e reconfirmação no ato de conceder (RF-02, RF-03, RN-D09). |
| `learning` → `commerce` | AsyncAPI | Matrícula mantém a própria visão mínima do curso publicado pelo fato de versão (C-01). |
| `commerce` → consumidores futuros | AsyncAPI | Fatos `matricula.acesso-concedido` e `matricula.acesso-expirado` (RF-07, domain doc §7). |
| `commerce` → Auditoria | AsyncAPI | Ato `cortesia-concedida` (RF-08, RN-D11). |
| Identity | OpenAPI (enum) | `cortesia.conceder` no catálogo de permissões (C-08). |
| Matrícula como produto de dados | Não aplicável | Banco interno de `commerce`; nenhum consumidor recebe dataset com compromisso próprio. Nenhum ODCS. |

| Documento | Padrão e versão | Versão do contrato | Fronteira | Estado |
|---|---|---|---|---|
| [api-contract.yaml](api-contract.yaml) e [.md](api-contract.md) | OpenAPI 3.1.0 | `bff-admin` Cortesias 1.0.0 | Recorte: 6 operações novas; as de CAP-002, CAP-003, CAP-005, CAP-006 e CAP-030 não mudam | **Aprovado para implementação** em 2026-10-01; lint sem erros nem avisos |
| [internal-api-contract-commerce.yaml](internal-api-contract-commerce.yaml) e [.md](internal-api-contract-commerce.md) | OpenAPI 3.1.0 | `commerce` 1.1.0 → 1.2.0 | 5 operações de cortesia e `decideAccessInternal`; Catálogo e área financeira sem mudança | **Aprovado para implementação** em 2026-10-01; lint sem erros nem avisos |
| [internal-api-contract-identity.yaml](internal-api-contract-identity.yaml) e [.md](internal-api-contract-identity.md) | OpenAPI 3.1.0 | Identity 1.3.0 → 1.4.0 | Recorte: `lookupStudentAccountInternal` e `confirmStudentAccountInternal` | **Aprovado para implementação** em 2026-10-01; lint sem erros nem avisos |
| [asyncapi-contract.yaml](asyncapi-contract.yaml) | AsyncAPI 3.0.0 | `commerce` 1.0.0 → 1.1.0 | Receptor de `conteudo.versao-publicada.v1` 1.1.0; produtor de `matricula.acesso-*.v1` e do ato | **Aprovado para implementação** em 2026-10-01; parser sem erros |
| [asyncapi-contract-audit.yaml](asyncapi-contract-audit.yaml) | AsyncAPI 3.0.0 | `audit` 1.3.0 → 1.4.0 | Recorte aditivo do canal `auditoria.ato-praticado.v1` | **Aprovado para implementação** em 2026-10-01; parser sem erros |
| `tasks/prd-acesso-interno/api-contract.yaml` e `internal-api-contract.yaml` | OpenAPI 3.1.0 | Identity 1.3.0 → 1.4.0 | Só o enum `Permission` ganha `cortesia.conceder` (C-08) | Aplicado e aprovado em 2026-10-01; lint sem erros |
| `tasks/prd-consulta-trilha-auditoria/internal-api-contract-identity.yaml` e `.md` | OpenAPI 3.1.0 | 1.0.0 → 1.1.0 | Só os enums de referência ganham `conta-aluno` (C-07) | Aplicado e aprovado em 2026-10-01; lint sem erros |

## Participantes e interfaces

| Identificador | Provedor/produtor | Consumidores | Comportamento | PRD |
|---|---|---|---|---|
| `lookupStudentAccount`, `lookupStudentAccountInternal` | `bff-admin`, decisão de `identity` | `admin-spa` | E-mail no corpo; devolve e-mail, nome, confirmação e situação; 404 indistinto | RF-02 |
| `previewCourtesyTerm`, `previewCourtesyTermInternal` | `bff-admin`, decisão de `commerce` | `admin-spa` | Término que a concessão teria agora, sem efeito, pela mesma função do domínio | RF-03, RF-05 |
| `listCourtesyCourses`, `listStudentAccessGrants`, `getCourtesyGrant` | `bff-admin`, decisão de `commerce` | `admin-spa` | Cursos concedíveis (sem exigir oferta); concessões do aluno com `status` calculado | RF-02, RF-03, RF-04 |
| `grantCourtesy` | idem | `admin-spa` | Cortesia com `Idempotency-Key`; concessão, fato e ato na mesma transação; 200 no reenvio | RF-03, RF-05, RF-07, RF-08 |
| `*Internal` de cortesia (5) | `commerce` | `bff-admin` | Mesmas regras, JWT de ator `audience: commerce`, `cortesia.conceder` | RF-01 a RF-05 |
| `confirmStudentAccountInternal` | `identity` | `commerce` | `eligible` só para conta de aluno ativa do tenant; falha fechada | RF-03, RN-D09 |
| `decideAccessInternal` | `commerce` (Matrícula) | `media`, `learning` — **nenhum nesta entrega** | `allowed`/`denied`, término efetivo e motivo; cache de até 30 s; falha fechada no consumidor | RF-06 |
| `receberVersaoPublicadaEmMatricula` | `commerce` | — | Visão mínima do curso: id, título da versão vigente, `versionNumber` | RF-03 |
| `publicarAcessoConcedido`, `publicarAcessoExpirado` · `matricula.acesso-*.v1` | `commerce`, por outbox | nenhum nesta entrega | Concessão passou a valer; vigência terminou (informativo, até 1 h depois) | RF-07 |
| `publicarAtoDeCortesia` · `auditoria.ato-praticado.v1` | `commerce`, por outbox | `audit` 1.4.0 | Ato `cortesia-concedida`, alvo `conta-aluno`, motivo obrigatório | RF-08 |

| Ação | Operação HTTP | Mensagens (mesma transação) |
|---|---|---|
| Conceder cortesia | `grantCourtesy` → `grantCourtesyInternal` → `confirmStudentAccountInternal` | `matricula.acesso-concedido.v1` + ato `cortesia-concedida` (`fatoId` = `eventId`) |
| Concessão vencer | nenhuma; `decideAccessInternal` calcula na pergunta | `matricula.acesso-expirado.v1`, em até 1 h, informativo |
| Localizar aluno, listar cursos e concessões | `lookupStudentAccount`, `listCourtesyCourses`, `listStudentAccessGrants` | nenhuma |
| Decidir acesso | `decideAccessInternal` | nenhuma |

`studentId` é o identificador da conta de aluno em Identity em todas as interfaces, nos fatos e no alvo do ato; `courseId` é o mesmo de `learning`; `grantId` é o mesmo nas APIs, nos fatos e em `complemento.concessao` do ato.

## Origem e decisões

| Entrada consultada | Versão e data | Decisão herdada |
|---|---|---|
| [PRD de CAP-008](prd.md) | v1.0, 2026-10-01 | RF-01 a RF-08, DP-01 a DP-10; OD61 a OD63 |
| [Matrícula e Direito de Acesso](../../domains/matricula-e-direito-de-acesso/domain.md) | v1.0, 2026-09-30 | RN-D01 a RN-D11, RN-D15, RN-D17; eventos §7 |
| [Identidade e Acesso](../../domains/identidade-e-acesso/domain.md) | v1.1, 2026-09-30 | RN-01, RN-12, RN-13, RN-16, RN-17, RN-18, RN-21, RN-23 |
| [Auditoria e Conformidade](../../domains/auditoria-e-conformidade/domain.md) | v1.2, 2026-09-27 | RN-A05, RN-A06, RN-A08, RN-A14 |
| [Baseline](../../context/architecture-baseline.md) | v1.2, 2026-09-21 | BA07 (decisão síncrona, cache ≤ 30 s, falha fechada), G06, G07, G09, G10, G11, G15, G18, G23 |
| [ADR-0004](../../docs/adr/0004-autenticacao-de-servico-bff-identity.md), [ADR-0005](../../docs/adr/0005-sessao-e-servico-do-backoffice.md), [ADR-0009](../../docs/adr/0009-autenticacao-de-servico-bff-aluno-em-servicos-de-dominio.md) | aceitas | Asserção de serviço assimétrica, vários emissores com escopos; JWT de ator com audiência do serviço dono |
| [Contratos de vitrine e oferta (CAP-003)](../prd-vitrine-oferta/contracts.md) | 1.0, 2026-09-30 | `accessPeriod` (C-05), padrão de ato `oferta-*` (C-09), reenvio e visão do curso (C-01), rótulo de alvo (C-11) |
| [Contratos de nível e pré-requisito (CAP-005)](../prd-nivel-prerequisito-curso/contracts.md) | 1.0, 2026-09-30 | Fato de versão 1.1.0 e reenvio (C-01) |
| [Contratos da consulta da trilha (CAP-030)](../prd-consulta-trilha-auditoria/contracts.md) | 1.2.0 | Resolução de referências pelo dono (C-02); `bff-admin` → `identity` |
| [Contratos de acesso interno (CAP-002)](../prd-acesso-interno/contracts.md) | 1.3.0 | Catálogo de permissões; `X-Staff-Session`; JWT de ator |

Decisões deste contrato:

| ID | Decisão | Alternativa descartada | Estado |
|---|---|---|---|
| C-01 | **Matrícula mantém a própria visão mínima dos cursos** (id, título da versão vigente, `versionNumber`) a partir de `conteudo.versao-publicada.v1`, **em fila própria**, com a carga inicial pelo mesmo reenvio de `learning` que já alimenta o Catálogo (C-01 de CAP-003, OD56). O reenvio precisa vir **depois** deste consumidor; se já aconteceu, é repetido na implantação (Media e Catálogo o ignoram por `versionNumber` e `eventId`) | Matrícula ler a visão do Catálogo, módulo irmão (acoplaria dois módulos de `commerce`, e `Entitlement` fala com Catálogo como se fossem serviços distintos); leitura síncrona `commerce` → `learning` (OD56) | **Aprovada em 2026-10-01** |
| C-02 | **A localização do beneficiário é de Identity**: `POST /student-account-lookups`, e-mail no corpo, devolvendo só e-mail, nome, `emailConfirmed` e `status` (RN-16, RN-21). Exige asserção do `bff-admin` com escopo novo `student-account:lookup` e `X-Staff-Session` com `cortesia.conceder`. 404 único para inexistente, ator interno e outra escola | `GET` com e-mail na URL (G23); lista ou busca aproximada de alunos (DP-02) | **Aprovada em 2026-10-01** |
| C-03 | **`commerce` reconfirma a conta em Identity antes de gravar a cortesia** (`confirmStudentAccountInternal`, asserção de `commerce`, escopo `student-account:confirm`), para Matrícula aplicar RN-D09 com a fonte única da identidade. `eligible: false` não distingue o motivo. **Falha fechada**: sem resposta, nada é concedido (503 em `commerce`, 502 no BFF). `commerce` ainda não é emissor em Identity: **ADR própria na TechSpec**, como a ADR-0009 fez para o `bff-student` | Confiar no BFF (a regra deixaria de ser do serviço dono); projeção de contas por fatos de Identity (exige carga inicial e deixa janela em que uma conta recém-desativada ainda recebe, contra o "recusado" de RF-03) | **Aprovada em 2026-10-01** |
| C-04 | **A decisão de acesso é `GET /access-decision?studentId=&courseId=`**, 200 também quando nega, para serviços: asserção de `media` ou `learning` com escopo `access-decision:read`, sem consumidor nesta entrega. Resposta: `decision`, `validity` (`until` com `expiresAt`, ou `lifetime`), `deniedReason` (`no-grant` · `grant-ended`), `lastExpiredAt`, `decidedAt`. Aluno, conta ou curso desconhecidos e outra escola → `denied`/`no-grant`. `Cache-Control: private, max-age=30`. **ADR própria na TechSpec** para `media` e `learning` como emissores em `commerce` | Direito dentro do JWT do aluno (réplica do direito, G11); 404 para desconhecido (enumeração; negar é resposta, não erro) | **Aprovada em 2026-10-01** |
| C-05 | **Vigência nas APIs e nos fatos:** `accessPeriod` herdado de CAP-003 (`months` 1 a 60 ou `lifetime`) mais `endsOn` (último dia, data no fuso da escola) e `expiresAt` (**instante UTC exclusivo**: início do dia seguinte a `endsOn`). Há acesso se a pergunta é anterior a `expiresAt` | Um único `endsAt` inclusivo às 23:59:59 (ambíguo na precisão e no arredondamento) | **Aprovada em 2026-10-01** |
| C-06 | **Auditoria 1.3.0 → 1.4.0, aditiva:** origem `matricula`, tipo `cortesia-concedida`, **motivo obrigatório** (entra na regra condicional), alvo `conta-aluno`, complemento `curso`, `concessao` e `vigencia` (`6m` ou `vitalicia`). `fatoId` = `eventId` do fato de concessão | Deixar o ato chegar como tipo desconhecido (poluiria a trilha e o alerta); alvo `concessao` (a referência por identidade é do aluno, RN-A08) | **Aprovada em 2026-10-01** |
| C-07 | **Rótulo da conta de aluno na trilha resolvido por Identity**: `resolveAuditIdentityReferencesInternal` passa a aceitar `conta-aluno`, devolvendo o **nome**, nunca o e-mail (Identity 1.0.0 → 1.1.0 do recorte de CAP-030). O curso do complemento segue a resolução por `learning` (C-08 de CAP-005), sem mudança | Mostrar só "Conta de aluno" e o id; e-mail como rótulo (G23) | **Aprovada em 2026-10-01** (aplicada no recorte de CAP-030) |
| C-08 | **`cortesia.conceder` entra no enum `Permission`** de `tasks/prd-acesso-interno/` (Identity 1.3.0 → 1.4.0), concedida só ao financeiro; a audiência `commerce` e a sessão validada pela localização a carregam | — | **Aprovada em 2026-10-01** (aplicada; decorre de DP-01) |
| C-09 | **Idempotência da cortesia:** `Idempotency-Key` obrigatória; mesma chave e mesmo corpo → 200 com a concessão original, sem novo fato nem ato; mesma chave e corpo diferente → 422 `IDEMPOTENCY_KEY_REUSED`; janela de 24 h (padrão de CAP-003) | Idempotência só por (aluno, curso, vigência): impediria duas cortesias deliberadas (RN-D07) | **Aprovada em 2026-10-01** |
| C-10 | **Os fatos `matricula.acesso-concedido` e `matricula.acesso-expirado` existem já nesta entrega**, sem consumidor, porque o domain doc os declara e o outbox os produz junto com o ato. `originRef` é o pedido na compra e nulo na cortesia | Produzir só o ato: o primeiro consumidor (Notificação, `CAP-011`) exigiria carga retroativa | **Aprovada em 2026-10-01** |
| C-11 | **Prévia do término, só leitura:** `GET /courtesy-term-preview?months=` devolve `endsOn` e `expiresAt` que a concessão teria agora, pela **mesma função** do domínio (DP-03, RF-05); nada é gravado e a tela não recalcula a regra. Vale se a confirmação ocorrer no mesmo dia no fuso da escola; o definitivo é o de `grantCourtesy`. Pedida pela TechSpec (D-03). Revisão aditiva **antes da implementação** | Calcular a data no SPA (a regra existiria em dois lugares); omitir a data antes de confirmar (contraria RF-03, passo 3) | **Aprovada em 2026-10-01** |

## Evolução e compatibilidade

- **`bff-admin` Cortesias 1.0.0:** novo, sem versão anterior. Compatibilidade com produção não verificada.
- **`commerce` 1.1.0 → 1.2.0 (HTTP) e 1.0.0 → 1.1.0 (mensagens):** só acrescenta; Catálogo e área financeira não mudam. A segurança ganha o esquema `DomainServiceAssertion` (C-04).
- **Auditoria 1.3.0 → 1.4.0 (C-06):** mudança de **receptor**, aditiva; produtores atuais continuam válidos. `complemento` já aceitava chaves texto adicionais; as novas são declaradas. A regra condicional de motivo ganha um tipo, e só vale para `cortesia-concedida`. **Ordem de implantação obrigatória:** `audit` 1.4.0 antes de `commerce` publicar o primeiro ato.
- **Identity 1.3.0 → 1.4.0 (C-08):** aditiva, no enum; clientes ignoram permissão desconhecida. As duas operações novas (C-02, C-03) exigem **emissores e escopos novos** em Identity (`bff-admin` com `student-account:lookup`; `commerce` com `student-account:confirm`).
- **Consulta da trilha, Identity 1.0.0 → 1.1.0 (C-07):** aditiva; `conta-interna` e `convite-interno` continuam como estavam.
- **`conteudo.versao-publicada.v1` 1.1.0:** consumido como publicado por CAP-005; fila nova de Matrícula. Mensagem 1.0.0 ainda em fila é aplicada ignorando o que Matrícula não usa.
- **Ordem de implantação do conjunto:** Identity 1.4.0 (permissão, emissores e as duas operações) → `audit` 1.4.0 → `commerce` com o consumidor de versão de Matrícula → reenvio de `learning` (repetido, se já ocorreu) → `commerce` expondo cortesia e decisão → `bff-admin` → `admin-spa`.

## Validação e verificação

| Documento | Comando e ferramenta | Padrão/ruleset | Resultado |
|---|---|---|---|
| [api-contract.yaml](api-contract.yaml) | `npx --yes @stoplight/spectral-cli@6.15.0 lint tasks/prd-concessao-acesso/api-contract.yaml --ruleset .claude/skills/tsg-flow-contract-creator/rulesets/openapi.yaml -F hint` | OpenAPI 3.1.0, ruleset local | Sem erros nem avisos |
| [internal-api-contract-commerce.yaml](internal-api-contract-commerce.yaml) | mesmo comando | OpenAPI 3.1.0, ruleset local | Sem erros nem avisos (um aviso de coleção pela rota `/access-decisions` sumiu ao nomeá-la `/access-decision`, recurso singular) |
| [internal-api-contract-identity.yaml](internal-api-contract-identity.yaml) | mesmo comando | OpenAPI 3.1.0, ruleset local | Sem erros nem avisos |
| `tasks/prd-acesso-interno/api-contract.yaml` e `internal-api-contract.yaml` 1.4.0; `tasks/prd-consulta-trilha-auditoria/internal-api-contract-identity.yaml` 1.1.0 | mesmo comando, `-F error` | OpenAPI 3.1.0, ruleset local | Sem erros |
| [asyncapi-contract.yaml](asyncapi-contract.yaml), [-audit](asyncapi-contract-audit.yaml) | `npx --yes @asyncapi/cli@6.1.0 validate <arquivo>` | AsyncAPI 3.0.0 | Válidos, referências externas resolvidas; uma informação recomenda 3.1.0, mantida a 3.0.0 da skill |
| Exemplos HTTP e de mensagem | `jsonschema` 4.26.0 (2020-12, com formato), script de verificação local | Schemas dos próprios documentos | 108 exemplos conformes. Recusados: 61 e 0 meses, motivo vazio, só espaços e com 501 caracteres, motivo ausente, vitalícia com `months`, campo extra, e `cortesia-concedida` sem motivo |

Revisão de compatibilidade e lint não comprovam comportamento. A implementação deve verificar:

- `grantCourtesy` com 6 meses → concessão `courtesy`, término no fim do dia 6 meses depois, fato `acesso-concedido` e ato `cortesia-concedida` **conforme**, na mesma transação; falha forçada entre eles → nada nasce;
- `previewCourtesyTerm` com 1 a 60 meses devolve o mesmo término que `grantCourtesy` devolveria no mesmo instante; 0 e 61 → 400; sem a permissão → 403; **nada é gravado**;
- os seis casos de RF-05: 15/03 + 12 m → 15/03 seguinte; 31/01 + 1 m → 28/02; 31/01/2028 + 1 m → 29/02; 31/03 + 6 m → 30/09; concedida às 23:59 → conta do dia 15 no fuso da escola; vitalícia → sem término;
- `decideAccessInternal` às 23:59:59 do último dia → `allowed`; às 00:00:00 do dia seguinte → `denied`/`grant-ended`, **sem rotina rodada**; duas concessões (uma vencida, uma ativa) → `allowed`; 3 meses e vitalícia → `lifetime`; outra escola, conta que não é de aluno, curso desconhecido → `denied`/`no-grant`;
- decisão **inalterada** por nova versão do curso e por alterar ou despublicar oferta; resposta traz `Cache-Control: private, max-age=30`;
- consumidor sem resposta de `commerce` **não libera** (falha fechada, quando houver consumidor);
- motivo vazio ou de 501 caracteres, 0 ou 61 meses → 422 `FIELD_INVALID`, nada concedido; curso nunca publicado ou de outra escola → 422 `COURSE_NOT_ELIGIBLE`; conta desativada, de ator interno, de outra escola ou desativada entre a localização e a confirmação → 422 `STUDENT_ACCOUNT_NOT_ELIGIBLE`; Identity sem resposta → 503/502 `STUDENT_ACCOUNT_CHECK_UNAVAILABLE`, nada concedido;
- curso publicado **sem oferta** aceita cortesia; aluno com concessão ativa ao curso aceita outra, e as duas convivem;
- mesma `Idempotency-Key` e mesmo corpo → 200, **uma** concessão, **um** fato, **um** ato; corpo diferente → `IDEMPOTENCY_KEY_REUSED`;
- ator sem `cortesia.conceder` (professor, suporte, administrador, aluno) → 403 no BFF **e** em `commerce` chamado direto; papel revogado → recusado na sessão que estava aberta, e as cortesias dele continuam valendo;
- localização: e-mail com maiúsculas e espaços → conta encontrada; inexistente e de ator interno → o **mesmo** 404; o e-mail não aparece em URL, log, métrica nem chave de cache;
- `acesso-expirado` sai uma vez por concessão por período, em até uma hora do término; vitalícia nunca; decisão igual se o fato atrasar ou se perder;
- nenhum e-mail, nome ou texto de motivo em fato, em log ou em span (G10, G23); o motivo só na concessão e no ato;
- reenvio de `learning`: Matrícula passa a conhecer todos os cursos publicados; fato com `versionNumber` menor ou igual é ignorado;
- o ato aparece na trilha com o rótulo do aluno (nome), o curso e o motivo.

## Pendências e handoff

1. **Duas ADRs na TechSpec:** `commerce` como emissor de asserção em Identity (C-03) e `media`/`learning` como emissores em `commerce` (C-04), ambas estendendo a ADR-0004, a ADR-0005 e a ADR-0009.
2. **Ordem de implantação acima, incluindo a repetição do reenvio de `learning` depois do consumidor de Matrícula (C-01).
3. **Para a TechSpec:** modelo de Matrícula e Concessão em `commerce`, com schema e casos de uso próprios (baseline); cálculo do término no fuso da escola; rotina de `acesso-expirado` idempotente e com marcador por concessão; transação de concessão, fato e ato; cache da decisão no consumidor; visão mínima do curso e filtro por título sem acento.

Para `tsg-flow-techspec-creator`: usar este índice, [api-contract.yaml](api-contract.yaml), [internal-api-contract-commerce.yaml](internal-api-contract-commerce.yaml), [internal-api-contract-identity.yaml](internal-api-contract-identity.yaml), [asyncapi-contract.yaml](asyncapi-contract.yaml) e [asyncapi-contract-audit.yaml](asyncapi-contract-audit.yaml), sem duplicar schemas.
