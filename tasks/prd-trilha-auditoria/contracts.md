---
tsg_artifact: contract
product: code-4-coders
capability: CAP-030
version: 1.1
status: approved
updated: 2026-09-24
sources: tasks/prd-trilha-auditoria/prd.md@1.1
---

# Contratos de integração — Trilha de auditoria (CAP-030, fatia mínima)

> PRD: [`tasks/prd-trilha-auditoria/prd.md`](prd.md) v1.1
> Data: 2026-09-24
> Estado do conjunto: Aprovado para implementação

Este conjunto registra o acordo para esta implementação. PRDs posteriores podem evoluí-lo.
Não afirma qual contrato está em produção. Catalogação, armazenamento definitivo e mecanismos
de atualização do acervo são definidos pelo time de plataforma.

## Seleção e escopo

| Integração no escopo | Modalidade | Justificativa |
|---|---|---|
| Domínios de origem comunicam o ato à Auditoria | AsyncAPI | A única entrada da trilha é mensagem (G13, RN-A02) |
| Consulta da trilha | — | Fora desta fatia (segundo PRD de `CAP-030`); nenhum OpenAPI gerado |
| Dado da trilha oferecido a consumidores | — | Nenhum domínio consome a trilha (RN-A10); ODCS não se aplica |

| Documento | Modalidade/versão do padrão | Versão do contrato | Escopo completo ou recorte | Status |
|---|---|---|---|---|
| [asyncapi-contract.yaml](asyncapi-contract.yaml) | AsyncAPI 3.0.0 | 1.0.1 | Interface completa de entrada da aplicação `audit` nesta fatia | Validado · Aprovado |

## Participantes e interfaces

| Documento e identificador técnico | Provedor/produtor | Consumidores conhecidos | Comportamento ou compromisso | Requisitos do PRD |
|---|---|---|---|---|
| `receberAtoPraticado` · canal `auditoria.ato-praticado.v1` · mensagem `AtoPraticado` | Todo domínio com ato administrativo; nesta fatia, Identidade e Acesso (a implementar em `CAP-002`) | `audit` | Registra o ato conforme; faltas de conteúdo viram registro não conforme; sem `fatoId`/`origem`/`tenantId` a mensagem fica retida e alertada; idempotente por (`origem`, `fatoId`) | RF-01 a RF-04 · RN-A03 a RN-A06, RN-A08, RN-A13, RN-A14 |

Campos do envelope: `fatoId`, `origem`, `tipo`, `tenantId`, `praticadoEm`, `autor` e `alvo` (referência
`{tipo, id}`), `complemento` (só identificadores; nesta fatia, `papel`) e `motivo`, obrigatório nos
tipos `convite-interno-emitido`, `papel-concedido` e `papel-revogado`. O schema é a obrigação do
produtor; a reação da Auditoria a cada violação está descrita na operação.

RF-05 (imutabilidade) não é compromisso de mensagem: é verificado pela implementação sobre o
armazenamento da trilha.

## Origem e decisões

| Entrada consultada | Versão e revisão/data | Decisão herdada ou alterada | Motivo |
|---|---|---|---|
| [PRD CAP-030](prd.md) | v1.0, aprovado 2026-09-24 | Herdadas DP-02 a DP-05 | Forma do envelope, tolerância a ato incompleto, tipo desconhecido, mensagem ilegível |
| [Domain doc Auditoria](../../domains/auditoria-e-conformidade/domain.md) | v1.1 | RN-A14: contrato é da Auditoria; prefixo `auditoria.` | Dono da forma é quem guarda |
| [Domain doc Identidade](../../domains/identidade-e-acesso/domain.md) | v1.1 | Tipos e obrigatoriedade de motivo vêm de RN-19 | Conteúdo é do domínio de origem |
| [Contrato de Notificação](../prd-notificacao-transacional/asyncapi-contract.yaml) | 1.0.0 | Herdados: servidor, convenção de routing key versionada, `correlationId` em header, `tenantId` no payload | Mesmo modelo de dono (mensagem endereçada ao domínio que a recebe) |
| Baseline | v1.2 | G06 (outbox, DLQ, `x-delivery-limit`), G13, BA10 | Convenções já decididas, não reabertas |

Decisões desta autoria, dentro do que o PRD fixou:

- **`origem`, `tipo` e `alvo.tipo` são string com padrão, não enum.** Um domínio ou tipo novo não
  quebra o contrato, e o tipo desconhecido precisa chegar para ser registrado como não conforme
  (DP-04). A lista de tipos aceitos está na descrição de `tipo`.
- **Ack manual (`ack: true`).** A mensagem só é confirmada depois de gravada; é o que sustenta
  "nenhum ato perdido".
- **O e-mail do convidado não viaja.** `convite-interno-emitido` referencia o convite; o e-mail é
  alcançável por ele em Identidade (RN-A08). Diferença deliberada em relação a Notificação, onde o
  e-mail é o destinatário.
- **O momento do recebimento não está na mensagem**: quem o registra é a Auditoria (RN-A04).

## Evolução e compatibilidade

Contrato novo; não há versão anterior de `auditoria.ato-praticado`. Não altera nenhum contrato
existente.

**1.0.1 (2026-09-24, durante a TechSpec):** `tenantId` passa a declarar `format: uuid` e sua falta
torna a mensagem ilegível (RN-A13: nenhum registro sem tenant). Os exemplos usavam `code-4-coders`,
que não é o formato dos serviços (`TenantId` é `Guid` em Identity, Notification e Audit). Sem
produtor implementado, não há consumidor afetado.

Consumidor afetado a jusante: nenhum. **Produtor afetado:** Identidade e Acesso passa a publicar
os quatro atos neste envelope em `CAP-002`. O §7 do domain doc de Identidade ainda os lista como
eventos próprios `identidade.*`; a revisão é pendência do PRD de `CAP-002` (QT-03 do PRD).

Evolução prevista, compatível (minor): novo `tipo` aceito, novo `alvo.tipo`, nova chave em
`complemento`. Incompatível (major, novo canal `.v2`): tornar obrigatório campo hoje opcional,
mudar a chave de idempotência ou o significado de `praticadoEm`.

## Validação e verificação

| Documento | Comando e versão da ferramenta | Schema/ruleset e versão | Resultado e avisos |
|---|---|---|---|
| [asyncapi-contract.yaml](asyncapi-contract.yaml) | `npx --yes @asyncapi/cli@6.1.0 validate tasks/prd-trilha-auditoria/asyncapi-contract.yaml` | AsyncAPI 3.0.0, parser da CLI | Válido, 0 erros e 0 avisos. Uma informação recomenda 3.1.0; mantida 3.0.0, versão da skill e dos demais contratos do repositório. Avisos de `node-config` são do ambiente da CLI |
| Exemplos do mesmo documento | Ajv 8 + ajv-formats, script local de verificação | `AtoPraticadoPayload` com referências resolvidas | Os dois exemplos validam; `papel-concedido` sem `motivo` é rejeitado pela regra condicional |

A validade estrutural não comprova idempotência, entrega, retenção na fila de erro nem
imutabilidade. Cenários para a implementação:

1. Os quatro tipos, completos, geram registro conforme com `praticadoEm` e momento de recebimento.
2. `convite-interno-aceito` sem `motivo` é conforme; os outros três sem `motivo` são não conformes.
3. Sem `autor` e sem `alvo`: um registro não conforme com as duas razões e um único alerta.
4. `tipo` desconhecido: registro não conforme "tipo desconhecido", tipo informado preservado.
5. Reentrega idêntica: um registro, sem alerta repetido. Reentrega divergente: original intacto,
   nenhum registro novo, alerta.
6. Mesmo conteúdo com `fatoId` distinto: dois registros.
7. Sem `fatoId`, `origem` ou `tenantId` válido, ou JSON inválido: nenhum registro, mensagem retida intacta, alerta.
8. Falha de gravação: a mensagem é reentregue; após o limite, vai à fila de erro e o reprocessamento
   não duplica.
9. Log, métrica e rastreamento sem `motivo`, e-mail ou nome.
10. Registros de tenants diferentes nunca se misturam.

## Pendências e handoff

- Conjunto aprovado pelo usuário em 2026-09-24. Não há decisão de contrato em aberto.
- QT-01 do PRD (`papel-alterado`, OD22): se nascer, entra como tipo novo, compatível. Não bloqueia.
- QT-03 do PRD: Identidade adota este envelope no PRD de `CAP-002`. Não bloqueia esta implementação.

Handoff: `tsg-flow-techspec-creator` com [prd.md](prd.md), este índice e
[asyncapi-contract.yaml](asyncapi-contract.yaml). A TechSpec mapeia os cenários acima à
implementação e define o canal de alerta, o limite de entregas e como RF-05 é demonstrado.
