# Revisão full — Conta e autenticação do aluno

Run: run.vynf1sPf

## Veredito

**FULL VALIDATION APROVADA** — gate `passed`; 0 bloqueantes e 1 recomendação não bloqueante.

## Escopo e estabilidade

- `base_ref`: `d1c046564281e7d3d15f57bd7e6d57ffff426a40`
- `target_ref`: `d1c046564281e7d3d15f57bd7e6d57ffff426a40`
- `validated_commit`: `f1cfce51d8c29a89e60d4587bc525ec6eb5e8239`
- `validated_tree`: `542ccda7d802b2b7470643e49d58031df5eff76e`

Revisei as specs selecionadas, PRD, cinco tasks verticais, contratos público/interno e AsyncAPI, domínio, baseline e ADRs pertinentes. O HEAD e a árvore permaneceram estáveis; a alteração preexistente em `flow-state.json` foi preservada.

## Evidências

- Suítes completas: Identity 40/40, BFF 131/131, Notification 43/43 e SPA 16/16. Lint, typecheck, format, publish e builds de imagens passaram. Cobertura de linhas: Identity 73,46%, BFF 83,56%, Notification 76,53% e SPA 92,71% (mínimo 70%).
- Spectral não encontrou erros nos dois OpenAPI; AsyncAPI producer/consumer válidos. Sem mudanças pendentes no modelo EF de Identity; a verificação de imutabilidade das migrations passou.
- Smoke no Compose atualizado: cadastro 202; dois e-mails de confirmação, incluindo reenvio; caminho `/student/confirm-account`; confirmação 204; pedidos de reset para conta existente e inexistente com resposta igual 202; e-mail de reset em `/student/redefinir-senha`; reset 204 e login com a nova senha 200. A resposta pública de login não contém JWT.
- O container SPA inicialmente ativo era antigo e registrou a query de uma sonda fictícia. Reconstruí e substituí somente esse serviço pela imagem do HEAD. No smoke final, a sonda retornou 200 sem aparecer no log de acesso; Identity, BFF e Notification registraram zero ocorrências do e-mail ou tokens de teste. SPA e Notification ficaram saudáveis.
- Sensor em worktree isolada: as cinco mutações comportamentais falharam nas suítes focais — V-01 guarda da política de senha (6/6); V-02 finalidade do token (3/4); V-03 revogação da sessão corrente (1/4); V-04 revogação das sessões no reset (1/6); V-05 seleção da sessão corrente na troca de senha (2/4). A worktree foi removida e nenhum código foi alterado.

Os logs detalhados estão em `.tsg-flow/delegate-logs/run.vynf1sPf/`.

## Recomendação não bloqueante

`3_task.md` declara `status: done`, mas mantém desmarcados os quatro itens de conclusão (linhas 41–44). As evidências desta revisão cobrem esses critérios; sincronize os marcadores no estado do fluxo.
