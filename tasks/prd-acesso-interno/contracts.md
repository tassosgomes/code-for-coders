---
tsg_artifact: contract
product: code-4-coders
capability: CAP-002
version: 1.1
status: approved
updated: 2026-09-25
sources: tasks/prd-acesso-interno/prd.md@1.0, tasks/prd-acesso-interno/techspec.md@1.0
---

# Contratos de integração — acesso interno por papel e permissão

> - PRD: [prd.md](prd.md), v1.0, aprovado em 2026-09-25
> - Data da revisão: 2026-09-25
> - Estado do conjunto: **Aprovado para implementação (1.1)** em 2026-09-25 — C-01 a C-10 aprovadas; públicos, AsyncAPI e os dois contratos internos (EN-01) validados.

Este conjunto registra o acordo para o recorte de `CAP-002`. A aprovação significa acordo para implementar as interfaces descritas; não afirma implantação. Os contratos de `CAP-001`, `CAP-026` e `CAP-030` são preservados nas pastas de origem.

## Seleção e escopo

O SPA do backoffice consome a interface HTTP do BFF do backoffice. Identity passa a produzir duas mensagens endereçadas a outros donos: o ato administrativo à Auditoria, no contrato dela sem alteração, e o pedido de envio a Notificação, que precisa aceitar um modelo novo. Não há produto de dados entregue a consumidores; ODCS não se aplica.

| Documento | Padrão e versão | Versão do contrato | Fronteira | Estado |
|---|---|---|---|---|
| [api-contract.yaml](api-contract.yaml) e [api-contract.md](api-contract.md) | OpenAPI 3.1.0 | 1.0.0 | Recorte de CAP-002 na borda `admin-spa` → `bff-admin` | **Aprovado para implementação** em 2026-09-25; lint sem erros |
| [asyncapi-contract.yaml](asyncapi-contract.yaml) | AsyncAPI 3.0.0 | `identity` 1.1.0 | Aplicação Identity como **produtora** das duas mensagens deste PRD; as operações de CAP-001 (1.0.0) seguem sem mudança e não são repetidas | **Aprovado para implementação** em 2026-09-25; parser sem erros |
| [asyncapi-contract-notification.yaml](asyncapi-contract-notification.yaml) | AsyncAPI 3.0.0 | `notification` 1.1.0 | Evolução do contrato **completo** de Notificação para aceitar o modelo `convite-interno` | **Aprovado para implementação** em 2026-09-25; parser sem erros |
| [internal-api-contract.yaml](internal-api-contract.yaml) e [internal-api-contract.md](internal-api-contract.md) | OpenAPI 3.1.0 | 1.0.0 | Recorte `bff-admin` → Identity: 13 operações de backoffice, validação de sessão com papéis/permissões e JWT, JWKS | **Aprovado para implementação** em 2026-09-25; lint sem erros |
| [internal-api-contract-commerce.yaml](internal-api-contract-commerce.yaml) | OpenAPI 3.1.0 | 1.0.0 | Recorte `bff-admin` → Commerce: área financeira reservada com JWT de ator interno | **Aprovado para implementação** em 2026-09-25; lint sem erros |

O contrato de [Auditoria 1.0.1](../prd-trilha-auditoria/asyncapi-contract.yaml) continua sendo a referência do receptor de `auditoria.ato-praticado.v1` e **não é alterado**.

## Participantes e interfaces

| Identificador | Provedor/produtor | Consumidores | Comportamento | PRD |
|---|---|---|---|---|
| `createStaffSession`, `getCurrentStaffSession`, `endCurrentStaffSession` | `bff-admin`, com decisão de Identity | `admin-spa` | Entrada separada da do aluno, resposta idêntica para credencial errada/conta de aluno, sessão opaca, conta sem papel entra sem permissão | RF-08, RF-10 |
| `requestStaffPasswordReset`, `resetStaffPassword` | `bff-admin`, com decisão de Identity | `admin-spa` | Recuperação neutra; só conta interna gera pedido de envio | RF-11 |
| `lookupStaffInvitation`, `acceptStaffInvitation` | `bff-admin`, com decisão de Identity | `admin-spa` (página do link) | Mostra papel e validade; aceite cria conta interna ativa e abre sessão | RF-05 |
| `createStaffInvitation`, `listPendingStaffInvitations` | `bff-admin`, com decisão de Identity | `admin-spa` | Só `acesso.gerir`; motivo obrigatório; substitui pendente | RF-03, RF-12 |
| `listStaffMembers`, `grantStaffRole`, `revokeStaffRole`, `changeStaffRole` | `bff-admin`, com decisão de Identity | `admin-spa` | Só `acesso.gerir`; sem ação sobre si; revogação e troca encerram sessões | RF-06, RF-07, RF-09, RF-12 |
| `getFinanceArea` | `bff-admin`, com recusa repetida no serviço dono | `admin-spa` | Só `financeiro.ler`; área sem dado | RF-13 |
| `*StaffInternal` (13 operações) + `validateStaffSessionInternal` | Identity | `bff-admin` | Decisão de cada operação pública; ator por `X-Staff-Session`; validação por ação (ADR-0005) | RF-01 a RF-12, RF-14 |
| `getUserTokenSigningKeysInternal` | Identity | `commerce` (e serviços futuros) | JWKS com chave atual e anterior | RF-13 |
| `getFinanceAreaInternal` | Commerce | `bff-admin` | Valida JWT via JWKS e exige `financeiro.ler` | RF-13 |
| `publicarAtoPraticado` | Identity pelo outbox | Auditoria (`CAP-030`) | Quatro tipos; troca = dois atos atômicos; um ato por mudança efetiva | RF-09, RF-14 |
| `publicarPedidoDeEnvio` | Identity pelo outbox | Notificação (`CAP-026`) | `convite-interno` (papel, link) e `recuperacao-de-senha` (nome, link do backoffice) | RF-04, RF-11 |
| `receberPedidoDeEnvio` 1.1.0 | Notificação | — | Aceita `convite-interno`; nada mais muda | RF-04 |

Relação entre modalidades:

| Operação HTTP | Mensagens produzidas (mesma transação) |
|---|---|
| `createStaffInvitation` | `publicarPedidoDeEnvio` (`convite-interno`) + `publicarAtoPraticado` (`convite-interno-emitido`) |
| `acceptStaffInvitation` | `publicarAtoPraticado` (`convite-interno-aceito`) |
| `grantStaffRole` com `changed: true` | `publicarAtoPraticado` (`papel-concedido`) |
| `revokeStaffRole` com `changed: true` | `publicarAtoPraticado` (`papel-revogado`) |
| `changeStaffRole` | `publicarAtoPraticado` (`papel-revogado`) + `publicarAtoPraticado` (`papel-concedido`); só o primeiro quando a conta já tinha `toRole` |
| `requestStaffPasswordReset` para conta interna | `publicarPedidoDeEnvio` (`recuperacao-de-senha`) |
| Ação com `changed: false`, erro, ou seed do primeiro administrador | nenhuma |

## Origem e decisões

| Entrada consultada | Versão e data | Decisão herdada |
|---|---|---|
| [PRD de CAP-002](prd.md) | v1.0, 2026-09-25 | RF-01 a RF-14, DP-01 a DP-06 |
| [Baseline](../../context/architecture-baseline.md) | v1.2, 2026-09-21 | BA05 (BFF por audiência), BA06 (sessão opaca), autorização em duas camadas, G06 (outbox), G07 (tenant), G10/G23 (sem dado pessoal em telemetria) |
| [Identidade e Acesso](../../domains/identidade-e-acesso/domain.md) | v1.1 | RN-01, RN-04, RN-05, RN-08, RN-11 a RN-21, RN-24 a RN-28 |
| [Auditoria e Conformidade](../../domains/auditoria-e-conformidade/domain.md) | v1.1 | RN-A05, RN-A08, RN-A12, RN-A14 |
| [Contrato de Auditoria](../prd-trilha-auditoria/asyncapi-contract.yaml) | 1.0.1, integrado no PR #56 | Envelope, chave (`origem`, `fatoId`), `tenantId` uuid, `complemento.papel`, motivo exceto no aceite |
| [Contrato de Notificação](../prd-notificacao-transacional/asyncapi-contract.yaml) | 1.0.0, aprovado em 2026-09-21 | Canal, `pedidoId`, modelo = finalidade, texto em Notificação |
| [Contratos de CAP-001](../prd-conta-aluno/contracts.md) | 1.0.0, aprovados em 2026-09-22 | `/api/v1`, `Idempotency-Key` 24 h, `X-CSRF-Token`, Problem com `code`/`traceId`, política de senha, token no corpo; publicar no exchange efetivo do receptor |
| [ADRs 0003 e 0004](../../docs/adr/index.md) | aceitas em 2026-09-22 | Vigência de sessão e autenticação BFF → Identity — reaproveitadas pela TechSpec para o backoffice |

Decisões novas deste contrato, aprovadas em 2026-09-25:

| ID | Decisão | Alternativa descartada |
|---|---|---|
| C-01 | **Notificação evolui para 1.1.0:** `convite-interno` entra no enum de finalidade/modelo, com `dados` `papel` + `link`. `nome` passa a ser exigido por condição do modelo em vez de sempre | Mandar `nome` vazio ou inventado no convite: violaria a semântica do campo; o convidado ainda não tem nome |
| C-02 | **Recuperação do ator interno reusa `recuperacao-de-senha`** sem mudança no contrato de Notificação; o link aponta para o backoffice | Modelo próprio de recuperação interna: mais uma mudança em Notificação sem diferença de comportamento |
| C-03 | **Troca de papel = dois atos com o mesmo `praticadoEm` e `correlationId`**, `fatoId` distintos, sem campo de correlação no payload (OD22) | Campo de correlação no payload: mudaria o contrato de Auditoria |
| C-04 | **Aceite do convite abre sessão** na mesma resposta | Pedir nova entrada logo após definir a senha: um passo a mais sem ganho de segurança |
| C-05 | **Conta sem papel entra** (200, `permissions: []`) e o SPA mostra a orientação | Recusar na entrada: o ator não saberia se a senha estava errada ou se perdeu o acesso (RF-08) |
| C-06 | **Paths em inglês; papéis e permissões pelos nomes canônicos do domínio** (`professor`, `acesso.gerir`) — os mesmos valores de `complemento.papel` na Auditoria | Traduzir para inglês na API: dois vocabulários para o mesmo papel |
| C-07 | **Ação sem efeito responde 200 com `changed: false`**, sem ato | 409/422: o estado final pedido já vale; tratar como erro complicaria a repetição |

Decisões dos contratos internos, aprovadas em 2026-09-25:

| ID | Decisão | Alternativa descartada |
|---|---|---|
| C-08 | **Ator das operações de gestão por `X-Staff-Session`** (identificador opaco da sessão interna); Identity valida sessão, tenant e permissão e resolve o ator | BFF enviar `accountId`/permissões: seria identidade declarada pela borda, que o baseline proíbe. JWT de usuário com `aud=identity`: mais uma emissão por chamada sem ganho, já que Identity é a dona da sessão |
| C-09 | **JWKS em `/internal/v1/jwks` sem autenticação**, rede interna, até duas chaves (atual e anterior) | Proteger com asserção: cada serviço de domínio precisaria de credencial de serviço só para ler chave pública |
| C-10 | **Área financeira em documento próprio de Commerce**, 401 `TOKEN_INVALID` para token inválido e 403 `PERMISSION_DENIED` sem permissão | Incluir no documento de Identity: misturaria provedores com donos diferentes |

## Evolução e compatibilidade

- **Contratos internos:** novos. O documento interno de CAP-001 (`bff-student` → Identity) não muda; a verificação de asserção passa a aceitar dois emissores, e o `bff-student` mantém exatamente os seus escopos (regressão exigida na V-02).

- **HTTP do backoffice:** interface nova, sem versão anterior a comparar. Compatibilidade com produção não verificada.
- **Identity (produtora):** 1.1.0 acrescenta operações; as de 1.0.0 (CAP-001) não mudam. Os exemplos de `AtoPraticado` foram validados contra o schema do **receptor** Auditoria 1.0.1 — todos conformes. O schema do produtor é mais restrito que o do receptor (origem fixa, tipos fechados, motivo e complemento condicionados), o que é compatível.
- **Notificação 1.0.0 → 1.1.0:** mudança de **receptor**. Para produtores atuais (Identity de CAP-001) é compatível: os dois modelos anteriores continuam exigindo `nome` e `link` e o exemplo de 1.0.0 continua válido. **Ordem de implantação obrigatória:** Notificação 1.1.0 antes de Identity emitir convite — o pedido `convite-interno` é recusado por 1.0.0 (verificado: enum e `nome` obrigatório). A recuperação interna já é aceita por 1.0.0. Consumidores dos desfechos (`mensagem-entregue`, `entrega-falhou`) passam a ver `finalidade: convite-interno`; não há assinante declarado desses fatos, mas quem assinar deve tolerar valor novo.
- **Auditoria:** nenhuma mudança. Os quatro tipos já são aceitos.

## Validação e verificação

| Documento | Comando e ferramenta | Padrão/ruleset | Resultado |
|---|---|---|---|
| [api-contract.yaml](api-contract.yaml) | `npx --yes @stoplight/spectral-cli@6.15.0 lint tasks/prd-acesso-interno/api-contract.yaml --ruleset .claude/skills/tsg-flow-contract-creator/rulesets/openapi.yaml --fail-severity=error` | OpenAPI 3.1.0, ruleset local | Sem erros nem avisos (um erro de exemplo corrigido na primeira execução) |
| [internal-api-contract.yaml](internal-api-contract.yaml) | `npx --yes @stoplight/spectral-cli@6.15.0 lint tasks/prd-acesso-interno/internal-api-contract.yaml --ruleset .claude/skills/tsg-flow-contract-creator/rulesets/openapi.yaml --fail-severity=error` | OpenAPI 3.1.0, ruleset local | Sem erros nem avisos |
| [internal-api-contract-commerce.yaml](internal-api-contract-commerce.yaml) | `npx --yes @stoplight/spectral-cli@6.15.0 lint tasks/prd-acesso-interno/internal-api-contract-commerce.yaml --ruleset .claude/skills/tsg-flow-contract-creator/rulesets/openapi.yaml --fail-severity=error` | OpenAPI 3.1.0, ruleset local | Sem erros nem avisos |
| [asyncapi-contract.yaml](asyncapi-contract.yaml) | `npx --yes @asyncapi/cli@6.1.0 validate tasks/prd-acesso-interno/asyncapi-contract.yaml` | AsyncAPI 3.0.0 | Válido; uma informação recomenda 3.1.0, mantida a 3.0.0 da skill |
| [asyncapi-contract-notification.yaml](asyncapi-contract-notification.yaml) | `npx --yes @asyncapi/cli@6.1.0 validate tasks/prd-acesso-interno/asyncapi-contract-notification.yaml` | AsyncAPI 3.0.0 | Válido; mesma informação |
| Exemplos de mensagem | `jsonschema` 4.26.0 (Draft 7) contra os schemas do produtor e dos receptores | Auditoria 1.0.1, Notificação 1.0.0 e 1.1.0 | Todos conformes ao produtor, à Auditoria e à Notificação 1.1.0; convite recusado por Notificação 1.0.0, como esperado. Casos negativos recusados: aceite com motivo, revogação com alvo convite, convite sem papel, confirmação sem nome |

A validade estrutural não comprova entrega, atomicidade, idempotência nem autorização. A implementação deve verificar:

- troca de papel publicando os dois atos ou nenhum, inclusive com falha no meio;
- ação sem efeito sem ato; ato falho sem ato; seed sem ato;
- reentrega mantendo `fatoId`/`pedidoId` e a Auditoria gerando um registro por ato;
- revogação e troca encerrando sessões, com 401 na próxima chamada;
- professor recusado na área financeira pelo BFF **e** pelo serviço dono, chamado direto;
- conta de aluno recusada na entrada do backoffice e conta interna recusada na do aluno, com a mesma resposta de credencial inválida;
- convite substituído, expirado e aceito respondendo o mesmo `INVITATION_INVALID`;
- nenhum e-mail, nome, motivo, link ou token em log, span, métrica ou erro;
- e-mail de convite entregue por Notificação 1.1.0 com o modelo novo.

## Pendências e handoff

1. C-01 a C-07 aprovadas em 2026-09-25.
2. **Contratos internos:** produzidos (EN-01); C-08 a C-10 aprovadas em 2026-09-25. Dono da área financeira decidido na TechSpec: `commerce`.
3. **QT-02 do PRD** (validade do convite): configuração, não campo de contrato — o prazo viaja só como `expiresAt`. Notificação renderiza a validade a partir de parâmetro próprio, como em CAP-026; a operação mantém os dois coerentes.
4. **Ordem de implantação:** Notificação 1.1.0 antes do primeiro convite.

Para `tsg-flow-task-creator`: usar este índice, [techspec.md](techspec.md), os dois contratos internos, [api-contract.yaml](api-contract.yaml), [asyncapi-contract.yaml](asyncapi-contract.yaml) e [asyncapi-contract-notification.yaml](asyncapi-contract-notification.yaml), sem duplicar schemas.
