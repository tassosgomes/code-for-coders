#!/usr/bin/env bash
# Aplica (cria ou atualiza, idempotente) o ruleset versionado em
# .github/rulesets/main.json no repositório atual. Requer `gh` autenticado com
# permissão de admin. Só rode depois que .github/workflows/ci-gate.yml estiver
# na main, senão o check obrigatório nunca reporta e trava todos os PRs.
# Uso: scripts/apply-ruleset.sh [owner/repo] [arquivo.json]
set -euo pipefail
repo="${1:-$(gh repo view --json nameWithOwner --jq .nameWithOwner)}"
file="${2:-$(dirname "$0")/../.github/rulesets/main.json}"
name=$(jq -r .name "$file")
id=$(gh api "repos/$repo/rulesets" --jq ".[] | select(.name==\"$name\") | .id" | head -1)
if [ -n "$id" ]; then
  gh api -X PUT "repos/$repo/rulesets/$id" --input "$file" --jq '"atualizado: \(.id) \(.name)"'
else
  gh api -X POST "repos/$repo/rulesets" --input "$file" --jq '"criado: \(.id) \(.name)"'
fi
