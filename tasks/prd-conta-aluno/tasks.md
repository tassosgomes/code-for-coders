# Plano de Implementação — Conta e autenticação do aluno

> **TechSpec de origem:** [techspec.md](techspec.md), aprovada em 2026-09-22
> **Escopo:** Full-stack
> **ADRs pertinentes:** [0001](../../docs/adr/0001-monorepo-de-codigo.md), [0002](../../docs/adr/0002-plataforma-de-runtime-coolify.md), [0003](../../docs/adr/0003-verificacao-de-sessao-do-aluno.md), [0004](../../docs/adr/0004-autenticacao-de-servico-bff-identity.md)
> **Status do plano:** Confirmado para implementação

## Visão Geral

Cinco fatias atravessam SPA, BFF e Identity para entregar cadastro, confirmação, sessão, recuperação e troca de senha. A primeira também liga os pedidos de e-mail à Notificação e ao smtp4dev local. Cada incremento deixa os serviços compiláveis e tem testes selecionados das três partes; o aceite completo ainda exige validação full dos contratos e do fluxo local.

## Fases

### Fase 1 — Abrir e confirmar a conta

V-01 cria conta não confirmada e entrega um pedido de confirmação; V-02 consome ou reenvia o link. Checkpoints: testes focalizados e mensagem capturada no smtp4dev, com token de uso único.

### Fase 2 — Entrar e sair

V-03 autentica, mantém a sessão opaca e encerra somente a sessão corrente. Checkpoint: testes de duas sessões, inatividade, CSRF e falha fechada.

### Fase 3 — Recuperar e proteger a credencial

V-04 redefine por link sem revelar existência de conta; V-05 troca senha na sessão atual. Checkpoints: respostas neutras, revogação imediata das outras sessões e testes de política de senha.

## Mapa de Entrega

| Fatia | Task | Comportamento observável | Gate | Bloqueado por |
|---|---|---|---|---|
| V-01 | [1.0](1_task.md) | Cadastro cria conta não confirmada e e-mail capturado localmente | Testes selecionados Identity + BFF + SPA | Nenhum |
| V-02 | [2.0](2_task.md) | Link confirma uma vez; reenvio emite novo pedido | Testes selecionados Identity + BFF + SPA | 1.0 |
| V-03 | [3.0](3_task.md) | Login, sessão opaca e logout revogável | Testes selecionados Identity + BFF + SPA | 2.0 |
| V-04 | [4.0](4_task.md) | Recuperação neutra redefine senha e revoga sessões | Testes selecionados Identity + BFF + SPA | 3.0 |
| V-05 | [5.0](5_task.md) | Troca de senha mantém a sessão atual e revoga as demais | Testes selecionados Identity + BFF + SPA | 3.0 |

### Habilitadores

Nenhum. Migration, autenticação de serviço, roteamento do outbox e SMTP local entram na primeira fatia que os consome; não há dependência horizontal sem comportamento observável.

## Tasks

- [x] 1.0 Cadastrar aluno e solicitar confirmação
- [x] 2.0 Confirmar conta e reenviar link
- [ ] 3.0 Entrar, manter sessão e sair
- [ ] 4.0 Recuperar senha sem revelar conta
- [ ] 5.0 Trocar senha com sessão ativa

## Rastreabilidade PRD → TechSpec → tasks

| Jornada do PRD | Fatia da TechSpec | Task |
|---|---|---|
| Criar conta e confirmar | V-01, V-02 | 1.0, 2.0 |
| Entrar e sair | V-03 | 3.0 |
| Recuperar acesso | V-04 | 4.0 |
| Trocar senha | V-05 | 5.0 |

## Cobertura

As cinco histórias aparecem como US-01 a US-05 na ordem do PRD. As RN abaixo incluem as regras herdadas explicitamente pelo PRD; RN-23 é respeitada sem incluir desativação ou exclusão nesta entrega.

| Requisito | Task(s) |
|---|---|
| RF-01 | 1.0 |
| RF-02 | 2.0 |
| RF-03 | 3.0 |
| RF-04 | 3.0 |
| RF-05 | 4.0 |
| RF-06 | 5.0 |
| US-01 | 1.0, 2.0, 3.0 |
| US-02 | 2.0 |
| US-03 | 3.0 |
| US-04 | 4.0 |
| US-05 | 5.0 |
| RN-01 | 1.0, 3.0 |
| RN-02 | 1.0, 2.0, 3.0 |
| RN-03 | 2.0, 4.0 |
| RN-04 | 4.0 |
| RN-05 | 3.0, 4.0 |
| RN-06 | 1.0, 4.0, 5.0 |
| RN-07 | 4.0, 5.0 |
| RN-08 | 3.0, 4.0, 5.0 |
| RN-09 | 3.0 |
| RN-10 | 3.0 |
| RN-11 | 5.0 |
| RN-13 | 1.0, 3.0 |
| RN-13a | 1.0, 3.0 |
| RN-13b | 3.0 |
| RN-21 | 1.0, 2.0, 3.0, 4.0, 5.0 |
| RN-22 | 3.0 |
| RN-23 | 3.0 |
| RN-24 | 1.0, 2.0, 3.0, 4.0, 5.0 |
| RN-26 | 1.0, 2.0, 4.0 |
| RN-27 | 1.0, 2.0, 4.0 |
| RN-28 | 1.0, 2.0, 4.0 |

## Integridade dos gates e artefatos

Os gates usam `dotnet test` com Microsoft.Testing.Platform (`--filter-class` e `--minimum-expected-tests`) e `npm run test` com filtro Vitest; são os comandos de teste dos projetos executados pelas pipelines, com seletores de fatia. As classes e títulos selecionados são produzidos na própria task. O exit code da cadeia é o veredito. A validação full roda as suítes completas, build, lint e contratos de todas as operações.

V-01 cria o modelo persistido, a migration evolutiva e o destino do outbox que V-02/V-04/V-05 reutilizam. V-03 cria o estado autoritativo e operacional de sessão que V-04/V-05 revogam. Cada task produz seus testes e fixtures antes de executar seu gate; não há dependência de arquivo produzido por task posterior.

## Lanes e caminho crítico

Ordem obrigatória: **1.0 → 2.0 → 3.0**. Após 3.0, **4.0** e **5.0** podem avançar em lanes independentes, coordenando alterações nos arquivos compartilhados. O caminho crítico de aceite é 1.0 → 2.0 → 3.0 → 4.0, seguido da validação full com 5.0 integrado.

## Checkpoints e limites do aceite

1. Cadastro normalizado, pedido aceito por Notificação e link capturado no smtp4dev; duplicidade e replay sem efeito extra.
2. Link confirma só uma vez, reenvio cria novo pedido e token incorreto não confirma.
3. Sessões simultâneas, logout isolado, expiração, CSRF e falha de Identity/Valkey fecham acesso.
4. Recuperação tem resposta pública neutra, link de uso único e revogação imediata.
5. Troca exige senha atual, preserva a sessão corrente e revoga as demais.

Segurança e Produto ainda definirão os prazos operacionais dos links e da inatividade; Plataforma ainda definirá provedor, domínio e DNS para e-mail real. Esses valores são configuração obrigatória e validada no início, sem número inventado no plano. O smtp4dev cobre o aceite local; ativação com alunos reais depende dessas definições externas. Não há migração de contas externas. Observabilidade e proteção de segredos entram em cada fatia, sem task artificial separada.

> Gate estrutural: `python3 .agents/skills/tsg-flow-task-creator/scripts/validate_plan.py tasks/prd-conta-aluno/`.
