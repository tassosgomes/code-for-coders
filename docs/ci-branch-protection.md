# Proteção da main e check obrigatório (`ci-gate`)

A main é protegida por um Repository Ruleset versionado em
`.github/rulesets/main.json`. Ele exige PR (sem push direto), bloqueia force
push e deleção, e exige o check `ci-gate` atualizado com a main. Aprovação de
revisor é 0 (repositório de um dev só). Admin só ignora as regras via PR, nunca
por push direto.

## Por que um check agregador

Cada workflow de serviço (`identity.yml`, `admin-spa.yml` etc.) tem filtro de
`paths`. Exigir esses checks por nome travaria qualquer PR que não toca o
serviço, porque o check nunca reporta. O workflow `.github/workflows/ci-gate.yml`
roda em todo PR, espera os workflows de PR disparados para o mesmo commit e só
passa se todos terminarem em `success`, `skipped` ou `neutral`. Workflows que o
filtro de paths não disparou não existem para o commit e não contam.

Para incluir um novo workflow (ex.: e2e) basta ele rodar em `pull_request`: o
gate o enxerga sozinho. Se o novo workflow também precisar ser exigido mesmo
quando o gate não o vê (por exemplo, um check de outra fonte), acrescente mais
um item em `required_status_checks` no `main.json` e reaplique.

## Aplicar

Só depois que `ci-gate.yml` estiver na main (senão o check nunca reporta):

```bash
scripts/apply-ruleset.sh                 # repo atual, cria ou atualiza
scripts/apply-ruleset.sh org/outro-repo  # outro repo
```

Equivale a `gh api -X POST repos/OWNER/REPO/rulesets --input .github/rulesets/main.json`
(PUT em `rulesets/ID` para atualizar). Exige `gh` autenticado como admin.

## Replicar em outro repo da empresa

1. Copie `.github/workflows/ci-gate.yml`, `.github/rulesets/main.json` e
   `scripts/apply-ruleset.sh`.
2. Faça merge do workflow na main do outro repo e confirme que um PR mostra o
   check `ci-gate`.
3. Rode `scripts/apply-ruleset.sh OWNER/REPO`.
4. Em repos com revisores, ajuste `required_approving_review_count` no JSON.
5. Repos privados precisam de plano que suporte rulesets; do contrário, use a
   proteção de branch clássica com o mesmo check `ci-gate`.

O `actor_id` 5 em `bypass_actors` é o papel Admin do repositório, com
`bypass_mode: pull_request` (ignora as regras apenas dentro de um PR).
