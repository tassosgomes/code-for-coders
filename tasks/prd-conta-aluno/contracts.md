# Contratos de integração — conta e autenticação do aluno

> - PRD: [prd.md](prd.md), v1.1, aprovado em 2026-09-22
> - Data da revisão: 2026-09-22
> Estado do conjunto: **Aprovado para implementação** em 2026-09-22 — a aprovação da TechSpec fechou a segurança BFF → Identity e a janela de idempotência HTTP; os dois OpenAPI passaram no lint.

Este conjunto registra o acordo para o recorte de `CAP-001`. A aprovação dos documentos significa acordo para implementar as interfaces descritas; não afirma implantação, nem altera o contrato aprovado de `CAP-026`.

## Seleção e escopo

O SPA de aluno consome a interface HTTP do BFF; o BFF precisa integrar com o serviço de Identidade para executar os casos de uso de conta. Identidade publica três fatos e envia pedidos endereçados a Notificação por RabbitMQ. Não há produto de dados disponibilizado a consumidores por tabela, arquivo ou API analítica; o banco interno e os fatos de integração não criam um contrato ODCS.

| Documento | Padrão e versão | Versão do contrato | Fronteira | Estado |
|---|---|---|---|---|
| [api-contract.yaml](api-contract.yaml) e [api-contract.md](api-contract.md) | OpenAPI 3.1.0 | 1.0.0 | Recorte de CAP-001 na borda SPA → BFF do aluno | **Aprovado para implementação** em 2026-09-22; lint sem erros |
| [asyncapi-contract.yaml](asyncapi-contract.yaml) | AsyncAPI 3.0.0 | 1.0.0 | Recorte de CAP-001 da aplicação Identity, produtora de quatro mensagens | **Aprovado para implementação** em 2026-09-22; parser sem erros |
| [internal-api-contract.yaml](internal-api-contract.yaml) e [internal-api-contract.md](internal-api-contract.md) | OpenAPI 3.1.0 | 1.0.0 | Recorte BFF do aluno → Identity, inclusive pré-login, validação de sessão e JWT interno | **Aprovado para implementação** em 2026-09-22; lint sem erros |

O contrato de [CAP-026](../prd-notificacao-transacional/asyncapi-contract.yaml) continua sendo a referência para a mensagem `notificacao.envio-solicitado.v1`. O documento AsyncAPI desta entrega descreve a aplicação **produtora** Identity e inclui o formato do pedido necessário para ser validável isoladamente; não redefine o receptor.

Na implementação, Identity deve publicar o pedido no exchange efetivo de Notificação, configurado com o namespace do consumidor. O código atual de Identity publica no seu próprio exchange; uma routing key coincidente não entrega mensagens entre exchanges. A TechSpec registra a alteração do outbox/publicador e exige prova de consumo real.

## Participantes e interfaces

| Identificador | Provedor/produtor | Consumidores conhecidos | Comportamento e requisitos |
|---|---|---|---|
| `registerStudent`, `confirmStudentAccount`, `requestAccountConfirmation` | BFF do aluno, com decisão de Identidade | SPA do aluno | Cadastro não confirmado, uso único do link e reenvio; RF-01 e RF-02 |
| `createStudentSession`, `getCurrentStudentSession`, `endCurrentStudentSession` | BFF do aluno | SPA do aluno | Cookie opaco, inatividade deslizante, logout imediato e sessões paralelas; RF-03 e RF-04 |
| `requestPasswordReset`, `resetStudentPassword`, `changeStudentPassword` | BFF do aluno, com decisão de Identidade | SPA do aluno | Recuperação neutra, uso único, revogação das outras sessões e troca autenticada; RF-05 e RF-06 |
| `createStudentAccountInternal`, `confirmStudentAccountInternal`, `requestAccountConfirmationInternal`, `createStudentSessionInternal`, `requestPasswordResetInternal`, `resetStudentPasswordInternal`, `changeStudentPasswordInternal` | Identity | BFF do aluno | Executam as decisões de conta e credencial das operações públicas correspondentes, sob asserção de serviço |
| `validateStudentSessionInternal`, `revokeStudentSessionInternal` | Identity | BFF do aluno | Vigência/renovação/JWT opcional para audiência permitida e revogação imediata de sessão; ADR-0003 |
| `publicarContaCriada`, `publicarContaConfirmada`, `publicarSenhaRedefinida` | Identity pelo outbox | Matrícula e Inteligência de Negócio são interessados citados no domínio; assinaturas efetivas neste PRD não foram declaradas | Fatos difundidos com `eventId`, `tenantId`, `accountId`, `occurredAt`; nenhum e-mail, link, token ou senha; RF-01, RF-02, RF-05, RF-06 |
| `publicarPedidoDeEnvioSolicitado` | Identity pelo outbox | Notificação (`CAP-026`) | Pedido endereçado e idempotente por `pedidoId`; só modelos `confirmacao-de-conta` e `recuperacao-de-senha`; RF-01, RF-02, RF-05 |

Relação entre modalidades: `registerStudent` e `requestAccountConfirmation` podem produzir `publicarPedidoDeEnvioSolicitado` com finalidade `confirmacao-de-conta`; `requestPasswordReset` pode produzi-lo com `recuperacao-de-senha`; `confirmStudentAccount` produz `publicarContaConfirmada`; `resetStudentPassword` e `changeStudentPassword` produzem `publicarSenhaRedefinida`. `registerStudent` também produz `publicarContaCriada`. Pedido para e-mail inexistente ou inelegível não publica envio. A confirmação e a alteração de senha só publicam fatos após persistir a mudança e seu outbox.

## Origem e decisões

| Entrada consultada | Versão e data/revisão | Decisão herdada ou tomada neste contrato |
|---|---|---|
| [PRD de CAP-001](prd.md) | v1.1, 2026-09-22 | RF-01 a RF-06, sessão opaca, resposta neutra de recuperação, três fatos e pedido endereçado; política de senha e teste local por smtp4dev aprovados |
| [Baseline](../../context/architecture-baseline.md) | v1.2, 2026-09-21 | BA05/BA06: SPA → BFF e cookie opaco; G18: CSRF; G06: outbox; G14: versionamento; RabbitMQ, JSON e ProblemDetails; idempotência em escrita externa |
| [Domínio Identidade e Acesso](../../domains/identidade-e-acesso/domain.md) | v1.1, 2026-09-21 | RN-01 a RN-11, RN-13/13a/13b, RN-21/22/24/26–28; Notificação não assina `identidade.conta-criada` para construir o e-mail |
| [PRD CAP-026](../prd-notificacao-transacional/prd.md) e [contrato AsyncAPI](../prd-notificacao-transacional/asyncapi-contract.yaml) | PRD v1.1 e contrato v1.0.0, aprovados em 2026-09-21; consultados em 2026-09-22 | Canal `notificacao.envio-solicitado.v1`, `pedidoId` de idempotência, dois modelos, `dados` com `nome` e `link`, sem campo `validade`; consumidor Notificação |
| [ADRs 0001 e 0002](../../docs/adr/index.md) | índice consultado em 2026-09-22 | Organização do monorepo e runtime; nenhuma decisão adicional de payload ou autenticação entre BFF e Identity |
| [TechSpec CAP-001](techspec.md) e [ADRs 0003 e 0004](../../docs/adr/index.md) | aprovadas em 2026-09-22 | Verificação de vigência em Identity por ação protegida, asserção de serviço assimétrica antes do login e idempotência por 24 horas |

Novas escolhas deste contrato: URLs públicas inglesas, versão `/api/v1`, `Idempotency-Key` nas escritas com janela de 24 horas, `X-CSRF-Token` ligado à sessão, erros com `code` estável e `traceId`, 202 sem corpo para pedidos de confirmação/recuperação, `eventId` para deduplicação de fatos. Senha **nova** exige no mínimo oito caracteres, maiúscula, minúscula, dígito e símbolo; o schema declara o mínimo e a regra de classes na descrição. A senha atual na troca não reaplica a política. Validade de link e inatividade permanecem configurações sem números fixados no schema. A interface interna usa `/internal/v1` e asserção de serviço distinta do JWT de aluno.

**Decisão de encaminhamento concluída em 2026-09-22:** a TechSpec e as ADRs 0003/0004 definiram vigência de sessão e autenticação BFF → Identity; o OpenAPI interno registra essa interface. O OpenAPI público e o AsyncAPI não pressupõem detalhes da chamada interna.

**Aprovações confirmadas pelo usuário em 2026-09-22:** os contratos público e AsyncAPI foram aprovados antes da TechSpec; em seguida, o usuário aprovou a TechSpec, as duas decisões de ADR e a janela de 24 horas. O contrato interno deriva dessas decisões e está validado para a implementação do PRD.

## Evolução e compatibilidade

Não foi encontrado contrato HTTP anterior para o ciclo do aluno. Compatibilidade HTTP com uma versão em produção **não foi verificada**. O contrato de CAP-026 já existe e foi preservado: o pedido deste PRD tem os mesmos campos obrigatórios, nomes, enum de finalidade/modelo e campos de `dados` do receptor v1.0.0. A revisão foi estrutural; a implementação ainda deve provar serialização e consumo. Reenvio deliberado usa novo `pedidoId`; reentrega do mesmo pedido mantém o mesmo `pedidoId`.

O contrato interno BFF → Identity é novo; não foi encontrada versão anterior nem implantação a comparar. A interface pública preserva operações e rotas aprovadas, explicita a janela antes configurável e **restringe** o campo de senha nova de mínimo 1 para mínimo 8 caracteres com classes exigidas. A restrição de entrada seria incompatível com consumidores que já enviassem senhas menores; não há implementação anterior do fluxo encontrada no repositório, e compatibilidade em produção não foi verificada. Na repetição de login, Identity retorna a mesma sessão lógica; o BFF pode reemitir cookie opaco próprio, sem restaurar sessão revogada. A garantia de idempotência abrange o efeito e status/corpo lógico da resposta, não igualdade byte a byte de `Set-Cookie`.

Os três fatos `identidade.*.v1` são novos neste PRD; não há versões anteriores disponíveis para verificar compatibilidade nem assinaturas de consumidores atuais documentadas. Consumidores futuros devem deduplicar por `eventId`; a entrega é ao menos uma vez e não há ordenação prometida entre canais. Evolução incompatível exige nova versão da rota ou routing key com convivência definida antes de substituir qualquer consumidor.

## Validação e verificação

| Documento | Comando e ferramenta | Padrão/ruleset | Resultado |
|---|---|---|---|
| [api-contract.yaml](api-contract.yaml) | `npx --yes @stoplight/spectral-cli@6.15.0 lint tasks/prd-conta-aluno/api-contract.yaml --ruleset .agents/skills/tsg-flow-contract-creator/rulesets/openapi.yaml --fail-severity=error` | OpenAPI 3.1.0, ruleset local | Sem erros nem avisos reportados |
| [internal-api-contract.yaml](internal-api-contract.yaml) | `npx --yes @stoplight/spectral-cli@6.15.0 lint tasks/prd-conta-aluno/internal-api-contract.yaml --ruleset .agents/skills/tsg-flow-contract-creator/rulesets/openapi.yaml --fail-severity=error` | OpenAPI 3.1.0, ruleset local | Sem erros nem avisos reportados |
| [asyncapi-contract.yaml](asyncapi-contract.yaml) | `npx --yes @asyncapi/cli@6.1.0 validate tasks/prd-conta-aluno/asyncapi-contract.yaml` | AsyncAPI 3.0.0, parser da CLI | Válido; uma informação recomenda 3.1.0, mantida a versão 3.0.0 da skill. Avisos de `node-config` são do ambiente da CLI |

Revisão adicional: referências locais resolvidas pelo lint/parser; comparação estrutural do pedido de envio com o receptor CAP-026 conferiu campos obrigatórios, nomes, enums e `dados`. Exemplos usam valores compatíveis com os tipos; links de exemplo contêm segredo fictício apenas na mensagem endereçada. A validade estrutural não comprova segurança da integração, idempotência, entrega, revogação ou compatibilidade operacional.

Na implementação, verificar: conta duplicada e normalização de e-mail; login inexistente e senha errada indistinguíveis; conta não confirmada só identificada após senha correta; recuperação neutra; token usado/expirado/trocado de finalidade; CSRF; sessão expirada/revogada; sessão paralela preservada; alteração de senha revogando outras sessões; publicação do outbox uma vez lógica apesar de reentrega; pedido recebido por Notificação sem vazamento de token nos fatos.

## Pendências e handoff

1. **QT-01 do PRD:** validade de links e inatividade da sessão, a serem fixadas como configuração antes de tráfego real. A política de senha foi fechada em DP-05. **QT-02:** provedor e DNS para destinatários externos, fora do schema. O teste local usa smtp4dev com adaptador SMTP em Notificação. Responsáveis: Segurança/Produto e Plataforma, respectivamente.
2. **Sincronia da validade do link:** CAP-026 renderiza o prazo a partir de configuração própria; Identidade aplica a validade real. A operação deve mantê-las coerentes. Nenhum campo `validade` foi incluído no pedido aprovado.

Para `tsg-flow-task-creator`, usar este índice, [techspec.md](techspec.md), os dois OpenAPI e [asyncapi-contract.yaml](asyncapi-contract.yaml). Tipos e mocks HTTP podem derivar dos OpenAPI; integração de mensagens precisa testar envio, recebimento, reentrega e falha de publicação.
