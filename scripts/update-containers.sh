#!/usr/bin/env bash
set -Eeuo pipefail

usage() {
  cat <<'USAGE'
Uso:
  scripts/update-containers.sh <dev|stable|all> [--dry-run]

Sincroniza o working tree atual para o host escolhido e recria os serviços
de aplicação com Docker Compose. O arquivo .env remoto e os volumes não são
substituídos ou removidos.

Alvos:
  dev      desenv-server:/home/tsgomes/code-for-coders
  stable   infra-server:/home/tsgomes/code-for-coders-stable
  all      dev e stable, nessa ordem

Opções:
  --dry-run  mostra os arquivos que seriam sincronizados sem gravar nos hosts
  -h, --help mostra esta ajuda
USAGE
}

if (($# == 0)); then
  usage >&2
  exit 2
fi

target=$1
shift
dry_run=false

for option in "$@"; do
  case "$option" in
    --dry-run)
      if [[ "$dry_run" == true ]]; then
        echo "Opção repetida: --dry-run" >&2
        exit 2
      fi
      dry_run=true
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Opção desconhecida: $option" >&2
      usage >&2
      exit 2
      ;;
  esac
done

case "$target" in
  dev|stable|all) ;;
  -h|--help)
    usage
    exit 0
    ;;
  *)
    echo "Alvo desconhecido: $target" >&2
    usage >&2
    exit 2
    ;;
esac

for command in rsync ssh; do
  if ! command -v "$command" >/dev/null 2>&1; then
    echo "Comando obrigatório não encontrado: $command" >&2
    exit 1
  fi
done

repo_root=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)

# Mantém o .dockerignore alinhado: não envia credenciais locais, estado Git,
# configuração de agentes, documentação de trabalho nem saídas de build.
rsync_args=(
  --archive
  --compress
  --human-readable
  --itemize-changes
  --exclude='/.env'
  --exclude='/.env.*'
  --exclude='**/.env'
  --exclude='**/.env.*'
  --exclude='/.mcp.json'
  --exclude='/.git/'
  --exclude='/.github/'
  --exclude='/.cursor/'
  --exclude='/.agents/'
  --exclude='/.claude/'
  --exclude='/.codex/'
  --exclude='/.groma/'
  --exclude='/.idea/'
  --exclude='/.vs/'
  --exclude='/.tsg-flow/'
  --exclude='/.playwright-mcp/'
  --exclude='/backlog/'
  --exclude='/context/'
  --exclude='/domains/'
  --exclude='/docs/'
  --include='/infra/'
  --include='/infra/cloudflared/'
  --include='/infra/cloudflared/billing-webhook.yml'
  --exclude='/infra/**'
  --exclude='/tasks/'
  --exclude='/anotacoes.md'
  --exclude='/flow-state.json'
  --exclude='/skills-lock.json'
  --exclude='/vision.md'
  --exclude='/qa-evidence/'
  --exclude='**/bin/'
  --exclude='**/obj/'
  --exclude='**/TestResults/'
  --exclude='**/node_modules/'
  --exclude='**/dist/'
  --exclude='**/coverage/'
  --exclude='**/test-results/'
  --exclude='**/playwright-report/'
)

if [[ "$dry_run" == true ]]; then
  rsync_args+=(--dry-run)
  echo "Simulação: nenhum arquivo será gravado e nenhum container será atualizado."
fi

sync_and_update() {
  local environment=$1
  local host=$2
  local remote_dir=$3

  echo
  echo "[$environment] Sincronizando $repo_root para $host:$remote_dir"
  rsync "${rsync_args[@]}" -e 'ssh -o BatchMode=yes' \
    "$repo_root/" "$host:$remote_dir/"

  if [[ "$dry_run" == true ]]; then
    return 0
  fi

  echo "[$environment] Validando configuração e atualizando containers"
  ssh -o BatchMode=yes "$host" bash -s -- "$environment" <<'REMOTE'
set -Eeuo pipefail

environment=$1
case "$environment" in
  dev)
    app_dir=/home/tsgomes/code-for-coders
    project=code-for-coders
    compose_files=(
      -f docker-compose.komodo.apis.yml
      -f docker-compose.komodo.web.yml
    )
    ;;
  stable)
    app_dir=/home/tsgomes/code-for-coders-stable
    project=code-for-coders-stable
    compose_files=(
      -f docker-compose.komodo.apis.yml
      -f docker-compose.komodo.web.yml
      -f docker-compose.remote.yml
      -f docker-compose.stable.yml
    )
    ;;
  *)
    echo "Alvo remoto desconhecido: $environment" >&2
    exit 2
    ;;
esac

cd "$app_dir"

docker compose --env-file .env --project-name "$project" \
  "${compose_files[@]}" config --quiet

docker compose --parallel 2 --env-file .env --project-name "$project" \
  "${compose_files[@]}" up -d --build

docker compose --env-file .env --project-name "$project" \
  "${compose_files[@]}" ps
REMOTE
}

case "$target" in
  dev)
    sync_and_update dev desenv-server /home/tsgomes/code-for-coders
    ;;
  stable)
    sync_and_update stable infra-server /home/tsgomes/code-for-coders-stable
    ;;
  all)
    sync_and_update dev desenv-server /home/tsgomes/code-for-coders
    sync_and_update stable infra-server /home/tsgomes/code-for-coders-stable
    ;;
esac
