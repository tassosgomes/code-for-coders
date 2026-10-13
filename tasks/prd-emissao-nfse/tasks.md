---
tsg_artifact: tasks
product: code-4-coders
capability: CAP-016
version: 1.0
status: in_review
updated: 2026-10-12
sources: tasks/prd-emissao-nfse/prd.md@1.0, tasks/prd-emissao-nfse/techspec.md@1.0, tasks/prd-emissao-nfse/contracts.md@1.0
---

# Plano de Implementação — Emissão de NFS-e da compra avulsa (CAP-016)

> **TechSpec de origem:** [techspec.md](techspec.md) 1.0, aprovada em 2026-10-12
> **Escopo:** Full-stack — `billing` (módulo Fiscal), `commerce`, `identity`, `notification`, `bff-student`, `bff-admin`, `student-spa`, `admin-spa`
> **ADRs pertinentes:** [0020](../../docs/adr/0020-jwt-de-aluno-em-billing.md), [0021](../../docs/adr/0021-billing-le-tomador-em-identity.md); herdadas [0005](../../docs/adr/0005-sessao-e-servico-do-backoffice.md), [0013](../../docs/adr/0013-jwt-de-aluno-validado-por-servicos-de-dominio.md), [0016](../../docs/adr/0016-jwt-de-aluno-em-commerce.md), [0017](../../docs/adr/0017-autenticacao-de-servico-commerce-em-billing.md), [0018](../../docs/adr/0018-notificacao-resolve-destinatario-pela-conta.md)
> **Contratos:** [contracts.md](contracts.md) 1.0, aprovado para implementação em 2026-10-12 (C-01 a C-13). Esta spec não promove nada para `contracts/`
> **Design:** adendo em `docs/design/wireframes-compra.md` (1.0 e 2.0). O documento de CAP-011 já está aprovado e não se reabre
> **Status do plano:** Em revisão

## Visão Geral

Ao final, um pedido de compra avulsa que passa a *Pago* abre um único documento fiscal em `billing`. A venda, a concessão e o comprovante de compra não esperam a nota. O aluno informa CPF, CNPJ ou NIF na conta, no próprio pedido, e baixa PDF e XML quando a nota fica disponível. O financeiro conecta o emissor da escola, vê a situação no pedido que já consulta e pede outra tentativa quando a nota falha. Um ano depois o arquivo e o documento do tomador saem; a linha do documento fica, para o mesmo pedido não ganhar outra nota.

O desenho das três superfícies novas (bloco no pedido do aluno, bloco no pedido do financeiro, tela do prestador) vem antes de qualquer código de tela. A abertura do documento (3.0) não tem tela e corre em paralelo ao design.

## Fases

### Fase 0 — Design aprovado

1.0 adendo ASCII e 2.0 Figma, no documento de compra que já existe. **Checkpoint:** o cabeçalho de `docs/design/wireframes-compra.md` traz a linha `> **Adendo NFS-e:** ASCII e Figma aprovados pelo responsável em <data>`.

### Fase 1 — O pedido pago abre a nota e não segura a venda

3.0 abre o documento a partir de `vendas.compra-concluida.v1` com `courseTitle`. **Checkpoint:** três entregas do mesmo fato deixam um documento; fato sem título não abre; cortesia e pedido não pago não publicam o fato; a matrícula ainda concede.

### Fase 2 — Tomador e prestador soltam quem estava esperando

4.0 o aluno grava o documento e a nota que só esperava isso sai de *Aguardando tomador*. 5.0 o financeiro torna o prestador utilizável e as notas que só esperavam isso entram na fila. As duas não compartilham arquivo e podem correr em paralelo depois de 2.0 e 3.0. **Checkpoint:** CPF válido com prestador ausente deixa *Aguardando prestador*; com prestador utilizável, *Na fila*. A primeira gravação do prestador sem chave responde 422 e a chave não volta na leitura.

### Fase 3 — Autorização, falha e guarda

6.0 o emissor autoriza, aluno e financeiro baixam, e sai um aviso. 7.0 a falha fica no mesmo documento e o financeiro pede outra tentativa. 8.0 um ano depois o arquivo e o documento do tomador saem. **Checkpoint do PRD:** na bancada de estudo, um pedido pago chega a *Emitida* com PDF e XML baixados pelo aluno e vistos pelo financeiro; um pedido pago sem documento do tomador libera o curso e fica *Aguardando tomador*; uma recusa deixa o pedido *Pago* e o documento em *Falha*, com nova tentativa no mesmo documento. O gate não chama a Notaas de produção: o adaptador real fala com um servidor HTTP local no contrato do recorte.

## Mapa de Entrega

| Fatia | Task | Comportamento observável | Gate | Bloqueado por |
|---|---|---|---|---|
| EN-01 (ASCII) | 1.0 | Adendo ASCII das três superfícies aprovado | `rg` da linha do adendo em `wireframes-compra.md` | Nenhum |
| EN-01 (Figma) | 2.0 | Figma do adendo aprovado e registrado | `rg` de `ASCII e Figma aprovados` | 1.0 |
| V-01 | 3.0 | O pedido pago abre um único documento e a venda segue | Commerce `PurchaseCompletedCourseTitleTests` + Billing `FiscalDocumentOpeningTests` + `FiscalHolderReadAdapterTests` + `NfseEnqueueAdapterTests` | Nenhum |
| V-02 | 4.0 | O aluno informa o tomador e a nota que só esperava isso entra na fila | Identity `TaxDocumentTests` + Billing `FiscalHolderInformedTests` + BFF `MyTaxDocumentProxyTests` + SPA `student-fiscal-holder` | 2.0, 3.0 |
| V-03 | 5.0 | O financeiro conecta o prestador e solta quem já tem tomador | Billing `ServiceProviderTests` + Identity `FiscalManagePermissionTests` + BFF `ServiceProviderProxyTests` + SPA `admin-service-provider` | 2.0, 3.0 |
| V-04 | 6.0 | O emissor autoriza, aluno e financeiro baixam, e sai um aviso | Billing `NfseWebhookTests` + `FiscalDocumentDownloadTests` + `NfseDocumentAdapterTests` + `FiscalNoticeContractTests` + Notification `FiscalNoticeDeliveryTests` + BFF aluno `FiscalDocumentProxyTests` + BFF admin `FinanceFiscalProxyTests` + SPAs `student-fiscal-issued`, `my-orders-fiscal`, `finance-fiscal-issued` | 2.0, 3.0, 4.0, 5.0 |
| V-05 | 7.0 | A falha fica no mesmo documento e o financeiro pede outra tentativa | Billing `FiscalFailureRetryTests` + BFF `FinanceFiscalRetryProxyTests` + SPA `finance-fiscal-retry` | 6.0 |
| V-06 | 8.0 | Um ano depois, o arquivo e o documento do tomador saem | Billing `FiscalRetentionTests` + Identity `TaxDocumentRetentionTests` + SPAs `student-fiscal-retention`, `finance-fiscal-retention` | 4.0, 6.0, 7.0 |

EN-01 vira duas tasks porque cada aprovação é decisão separada do responsável e o Figma parte do ASCII aprovado. O cabeçalho que CAP-011 já gravou (`> **Status:** ASCII e Figma aprovados…`) não serve de gate: o adendo tem linha própria.

6.0 depende de 4.0 porque as duas editam a página do pedido do aluno, e de 5.0 porque a leitura do financeiro em `billing` precisa da audiência de ator que 5.0 torna emitível. 8.0 depende de 7.0 porque as duas editam o detalhe do pedido no backoffice.

### Habilitadores

| Enabler | Task | Por que não cabe numa fatia | Desbloqueia |
|---|---|---|---|
| EN-01 (ASCII) | 1.0 | O projeto só implementa tela com design aprovado. O bloco do aluno, o bloco do financeiro e a tela do prestador compartilham a linguagem da situação, inclusive a nota cujo arquivo já saiu | 4.0, 5.0 e a tela de 6.0, 7.0 e 8.0 |
| EN-01 (Figma) | 2.0 | A aprovação visual vale para as três superfícies de uma vez | idem |

Não há habilitador de serviço novo: Fiscal nasce dentro de `billing`. A migration do documento e do prestador nasce em 3.0; a do documento na conta nasce em 4.0. As migrations são sempre geradas pela ferramenta do EF.

## Tasks

- [ ] 1.0 Adendo ASCII da nota no pedido do aluno, no pedido do financeiro e na tela do prestador
- [ ] 2.0 Figma do adendo da nota e do prestador aprovado
- [ ] 3.0 O pedido pago abre um único documento fiscal e a venda segue
- [ ] 4.0 O aluno informa o tomador e a nota que só esperava isso entra na fila
- [ ] 5.0 O financeiro conecta o prestador e solta quem já tem tomador
- [ ] 6.0 O emissor autoriza, o aluno e o financeiro baixam, e sai um aviso
- [ ] 7.0 A falha fica no mesmo documento e o financeiro pede outra tentativa
- [ ] 8.0 Um ano depois, o arquivo e o documento do tomador saem

## Caminho crítico e lanes

`1.0 → 2.0` em paralelo com `3.0`. Depois, `4.0` e `5.0` em paralelo (arquivos disjuntos). Em seguida `4.0` e `5.0` → `6.0` → `7.0` → `8.0`.

Caminho crítico: `3.0` (pode começar com o design) e `2.0`, depois o mais lento entre `4.0` e `5.0`, depois `6.0 → 7.0 → 8.0`.

**Estado intermediário:** depois de 3.0 existe documento sem tela; depois de 4.0 o aluno grava o tomador e ainda não baixa; depois de 5.0 o prestador entra na fila e ainda não há PDF; entre 6.0 e 7.0 a falha aparece para o aluno sem o botão de nova tentativa, que só o financeiro ganha em 7.0. O plano é uma única entrega. Ordem de deploy da TechSpec: Identity → Notificação → `commerce` (com Matrícula) → `billing` → BFFs → SPAs. Pedido que já estava *Pago* antes do título existir não ganha nota.

## Faixas de tamanho

Não há corte por contagem de arquivo. A task é a fatia da TechSpec. 6.0 é a maior porque autorização, download e aviso são a mesma transição: separar o e-mail deixaria a nota *Emitida* sem o efeito que o PRD exige nela. 4.0 e 5.0 ficam separadas porque o tomador e o prestador são jornadas observáveis distintas, com arquivos disjuntos.

## Integridade dos gates

- .NET: `dotnet test --project <csproj> -- --filter-class <classe> --minimum-expected-tests N` (Microsoft.Testing.Platform: exit 8 se nada rodar, 9 se rodar menos que N). Toda classe nomeada num gate é criada na própria task; nenhuma coincide com classe existente. As preexistentes que precisam continuar passando (`PurchaseGrantTests`, `PurchaseReceiptRequestTests`, `GatewayWebhookTests`, `StudentContactTests`, `OrderProxyTests`, `MyOrdersProxyTests`, `FinanceOrdersProxyTests`) aparecem só em Verificações do projeto.
- SPA: `npm --prefix src/<spa> run test -- <fragmento>`. O filtro de caminho sai com código diferente de zero quando nenhum arquivo casa; não usar `--passWithNoTests`. Fragmentos novos: `student-fiscal-holder` (4.0), `admin-service-provider` (5.0), `student-fiscal-issued`, `my-orders-fiscal` e `finance-fiscal-issued` (6.0), `finance-fiscal-retry` (7.0), `student-fiscal-retention` e `finance-fiscal-retention` (8.0). Nenhum é prefixo de arquivo existente nem de outro fragmento do plano.
- Design (1.0, 2.0): linha própria `> **Adendo NFS-e:**` no cabeçalho de `docs/design/wireframes-compra.md`.
- Adaptadores: a porta pode ser dublada no caso de uso. O adaptador real é testado contra um servidor HTTP local que responde como Identity (leitura do tomador, 3.0) e como o recorte da Notaas (enfileirar em 3.0, baixar PDF e XML em 6.0). Nenhum gate chama a Notaas de produção nem exige certificado A1.
- Testcontainers rodam em sequência; os comandos do `gate` são encadeados. Testes de integração sem exportador OTLP apontando para um coletor ausente.
- O mínimo de cada classe é o número de casos nomeados na task. Um método que junta dois casos não conta os dois.

## Artefatos compartilhados

| Artefato | Produzido em | Evolui em |
|---|---|---|
| Adendo de `docs/design/wireframes-compra.md` | 1.0, 2.0 | referência de 4.0 a 8.0 |
| `courseTitle` em `vendas.compra-concluida.v1` | 3.0 | — |
| Documento fiscal e prestador no schema `billing_access`; fila de Fiscal | 3.0 | 4.0 a 8.0 |
| Leitura HTTP do tomador em Identity | 3.0 | 4.0, 8.0 |
| Enfileirar no emissor (adaptador real) | 3.0 | 5.0, 6.0, 7.0 |
| Documento do tomador na conta e o fato sem o documento | 4.0 | 8.0 |
| `fiscal.gerir` e audiência de ator `billing` | 5.0 | 6.0, 7.0 |
| Webhook do emissor, arquivos, aviso `nota-fiscal-disponivel` | 6.0 | 7.0, 8.0 |
| Página do pedido do aluno com o bloco da nota | 4.0 | 6.0, 8.0 |
| Detalhe do pedido no backoffice com a seção da nota | 6.0 | 7.0, 8.0 |

## Categorias sem task própria

- **Observabilidade.** Contadores de documento aberto (3.0), de `issued` (6.0), de `failed` (7.0) e de autorização ignorada por submissão antiga (7.0). Nenhuma task só de telemetria. Log e atributo sem e-mail, CPF, CNPJ, NIF, chave ou corpo de webhook, verificado nas fatias que tocam esses dados.
- **Segurança.** HMAC, audiência, permissão e ausência da chave na resposta entram em 3.0, 4.0, 5.0 e 6.0. O scan de segredo do CI continua bloqueante (`mode: enforce` no workflow, mesmo com `security-mode: observe` no chamador) e não vira task.
- **Migração.** Só pelo EF, em 3.0 e 4.0. Sem task de migration isolada.
- **Promoção de contrato.** Fora deste plano. Os testes exercem os `operationId` dos recortes; a catalogação em `contracts/` fica para o encerramento do PRD.
- **Smoke da bancada.** O critério de seguir do PRD é o checkpoint da Fase 3. O ambiente local já tem PostgreSQL, RabbitMQ e `billing` em `http://localhost:5110`. O gate prova o adaptador contra HTTP controlado. Subir o fake-nfe e cadastrar o webhook nele é configuração de deploy, não caso de uso. O túnel público, se for o caminho, passa a aceitar `/webhooks/v1/notaas/events` em 6.0.

## Verificação herdada

Workflows reutilizáveis lidos em 2026-10-12: `tassosgomes/template-pipeline` `.github/workflows/ci-dotnet.yml@v1` e `ci-react-ts.yml@v1`. Parâmetros efetivos são os dos chamadores abaixo. Estado da base: não medido nesta sessão. Nenhuma falha herdada comprovada; a cobertura agregada é da validação full, não de uma fatia.

| Componente | Fonte | Checks e limites | Estado da base | Resolução planejada |
|---|---|---|---|---|
| `src/commerce` | `.github/workflows/commerce.yml` → `ci-dotnet.yml@v1` (`working-directory: src/commerce`, `test-configuration: Debug`, `coverage-threshold: 70`, `security-mode: observe`) | `dotnet restore`; `dotnet format --verify-no-changes --no-restore`; `dotnet test --no-restore -c Debug --coverage --coverage-output-format cobertura` (MTP); cobertura por união de linhas ≥ 70%; `dotnet publish -c Release`; imagem. Segurança em paralelo: SAST, segredos (gitleaks, enforce) e dependências. DAST desligado | Não medido | Nenhuma. 3.0 sustenta o fato com título sem mudar a concessão |
| `src/billing` | `.github/workflows/billing.yml` → o mesmo `ci-dotnet.yml@v1` | Idem, cobertura ≥ 70% | Não medido | Nenhuma. 3.0 a 8.0 |
| `src/identity` | `.github/workflows/identity.yml` → o mesmo | Idem, cobertura ≥ 70% | Não medido | Nenhuma. 4.0, 5.0, 8.0 |
| `src/notification` | `.github/workflows/notification.yml` → o mesmo | Idem, cobertura ≥ 70%. O workflow também dispara em `contracts/**` e `src/contract-testing/**` (AsyncAPI) | Não medido | Nenhuma. 6.0 acrescenta o modelo; a 1.2.0 que o recusa sobe antes de `billing` publicar o aviso |
| `src/bff-student`, `src/bff-admin` | `bff-student.yml`, `bff-admin.yml` → o mesmo | Idem, cobertura ≥ 70%. Estes chamadores não incluem `contracts/**` | Não medido | Nenhuma. 4.0, 5.0, 6.0, 7.0 |
| `src/student-spa` | `.github/workflows/student-spa.yml` → `ci-react-ts.yml@v1` (`version: 24`, `build-args: --base=/student/`, `coverage-threshold: 70`) | `npm ci`; `npm run lint`; `npx tsc --noEmit`; `npm test` (`vitest run --coverage`); linhas ≥ 70% em `coverage/coverage-summary.json`; `npm run build -- --base=/student/`; imagem | Não medido | Nenhuma. 4.0, 6.0, 8.0 |
| `src/admin-spa` | `.github/workflows/admin-spa.yml` → `ci-react-ts.yml@v1` (`build-args: --base=/admin/`, cobertura 70) | Idem, com `--base=/admin/` | Não medido | Nenhuma. 5.0, 6.0, 7.0, 8.0 |
| Migrations | AGENTS.md; `dotnet-ef` em `.config/dotnet-tools.json` | `dotnet ef migrations add` pela ferramenta; `has-pending-model-changes` sem pendência | Não medido | 3.0 (`billing`) e 4.0 (`identity`) |
| Compose | `scripts/apps.sh`; `scripts/update-containers.sh` | `docker compose -f docker-compose.yml config --quiet`; com `--remote`, o override `docker-compose.remote.yml`; dev: `komodo.apis` + `komodo.web`; estável: esses mais `remote` e `stable`. Dependência nova de infra replicada no override remoto | Não medido | 3.0, e o segredo que 5.0 e 6.0 acrescentam nos mesmos arquivos |
| Contratos | `contracts/README.md`; lint já registrado em `contracts.md` | Spectral dos recortes feito na aprovação de 2026-10-12. Não substitui os testes. Promoção para `contracts/` só no encerramento do PRD | Validado em 2026-10-12 | Nenhuma task deste plano edita `contracts/` |

## Cobertura

| Requisito | Task(s) |
|---|---|
| RF-01 | 3.0 |
| RF-02 | 1.0, 2.0, 5.0 |
| RF-03 | 1.0, 2.0, 4.0 |
| RF-04 | 6.0, 7.0 |
| RF-05 | 1.0, 2.0, 6.0 |
| RF-06 | 1.0, 2.0, 6.0, 7.0 |
| RF-07 | 1.0, 2.0, 6.0 |
| RN-F01 | 3.0 |
| RN-F02 | 3.0, 7.0, 8.0 |
| RN-F03 | 3.0, 7.0 |
| RN-F04 | 3.0, 6.0 |
| RN-F05 | 5.0, 6.0 |
| RN-F06 | 3.0, 6.0 |
| RN-F07 | 3.0, 4.0 |
| RN-F08 | 4.0 |
| RN-F09 | 4.0, 6.0, 8.0 |
| RN-F10 | 3.0, 5.0 |
| RN-F11 | 6.0, 7.0 |
| RN-F12 | 5.0, 6.0 |
| RN-F13 | 6.0 |
| RN-F14 | 6.0 |
| RN-16 | 5.0, 6.0 |
| US-01 (aluno baixa a nota) | 6.0 |
| US-02 (aluno informa o documento depois da compra) | 4.0 |
| US-03 (financeiro vê a situação no pedido pago) | 6.0, 7.0 |
| US-04 (financeiro conecta o emissor) | 5.0 |
| US-05 (financeiro pede outra tentativa) | 7.0 |
| DP-01 | 3.0 |
| DP-02 | 4.0 |
| DP-03 | 5.0 |
| DP-04 | 6.0 |
| DP-06 | 3.0 |
| QA-01 | 8.0 |
| QA-02 | 5.0 |
| QA-03 | 6.0 |

US-01 a US-05 são as cinco histórias de `prd.md`, na ordem em que aparecem. RN-16 é a regra de Identidade citada por RF-02: professor e suporte não veem dado fiscal.

> Verificado por `python3 .claude/skills/tsg-flow-task-creator/scripts/validate_plan.py tasks/prd-emissao-nfse/` antes do handoff.
