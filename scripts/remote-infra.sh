#!/usr/bin/env bash

set -Eeuo pipefail

readonly script_directory="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
readonly repository_root="$(cd -- "$script_directory/.." && pwd)"
readonly env_file="$repository_root/.env"
readonly database_bootstrap="$script_directory/init-local-databases.sql"

readonly rabbitmq_vhost=code-for-coders
readonly rabbitmq_user=code_for_coders
# service|Infra.Data project|database|migration role|design-time connection variable
readonly migration_targets=(
  "identity|src/identity/src/CodeForCoders.Identity.Infra.Data|code_for_coders_identity|code_for_coders_identity|ConnectionStrings__DefaultConnection"
  "learning|src/learning/src/CodeForCoders.Learning.Infra.Data|code_for_coders_learning|code_for_coders_learning|ConnectionStrings__DefaultConnection"
  "media|src/media/src/CodeForCoders.Media.Infra.Data|code_for_coders_media|code_for_coders_media|ConnectionStrings__DefaultConnection"
  "commerce|src/commerce/src/CodeForCoders.Commerce.Infra.Data|code_for_coders_commerce|code_for_coders_commerce|ConnectionStrings__DefaultConnection"
  "notification|src/notification/src/CodeForCoders.Notification.Infra.Data|code_for_coders_notification|code_for_coders_notification|ConnectionStrings__DefaultConnection"
  "audit|src/audit/src/CodeForCoders.Audit.Infra.Data|code_for_coders_audit|code_for_coders_audit|ConnectionStrings__MigrationConnection"
  "bff-admin|src/bff-admin/src/CodeForCoders.BffAdmin.Infra.Data|code_for_coders_bff_admin|code_for_coders_bff_admin|ConnectionStrings__DefaultConnection"
  "bff-student|src/bff-student/src/CodeForCoders.BffStudent.Infra.Data|code_for_coders_bff_student|code_for_coders_bff_student|ConnectionStrings__DefaultConnection"
)

log() {
  printf '[remote-infra] %s\n' "$*"
}

die() {
  log "ERROR: $*" >&2
  exit 1
}

usage() {
  cat <<'EOF'
Usage: scripts/remote-infra.sh <provision|migrate|check>

Commands:
  provision  Create the PostgreSQL roles/databases, the RabbitMQ vhost/user, and store the
             generated credentials in .env (idempotent; requires SSH to the infra server).
  migrate    Apply the EF Core migrations of every service to the remote databases.
  check      Verify that PostgreSQL, RabbitMQ, Valkey, OTel, and SMTP accept connections.

Environment (read from .env, all optional):
  REMOTE_INFRA_HOST      Infra server address (default 192.168.0.5)
  REMOTE_INFRA_SSH       SSH host alias used by provision (default desenv-server)
  REMOTE_VALKEY_DATABASE Valkey logical database for this project (default 1)
EOF
}

env_value() {
  local key=$1
  [[ -f "$env_file" ]] || return 0
  grep -m1 "^${key}=" "$env_file" | cut -d= -f2- || true
}

set_env_if_missing() {
  local key=$1 value=$2
  if [[ -z "$(env_value "$key")" ]]; then
    printf '%s=%s\n' "$key" "$value" >> "$env_file"
    log "Stored $key in .env."
  fi
}

infra_host() {
  local host
  host="$(env_value REMOTE_INFRA_HOST)"
  printf '%s' "${REMOTE_INFRA_HOST:-${host:-192.168.0.5}}"
}

infra_ssh() {
  local alias
  alias="$(env_value REMOTE_INFRA_SSH)"
  printf '%s' "${REMOTE_INFRA_SSH:-${alias:-desenv-server}}"
}

require_value() {
  local key=$1 value
  value="$(env_value "$key")"
  [[ -n "$value" ]] || die "$key is missing from .env. Run: scripts/remote-infra.sh provision"
  printf '%s' "$value"
}

remote() {
  ssh -o BatchMode=yes -o ConnectTimeout=10 "$(infra_ssh)" "$@"
}

provision() {
  command -v ssh >/dev/null 2>&1 || die "ssh is required."
  command -v openssl >/dev/null 2>&1 || die "OpenSSL is required."

  if [[ ! -f "$env_file" ]]; then
    "$script_directory/generate-local-env.sh"
  fi

  remote true || die "Cannot reach $(infra_ssh) over SSH."

  # Hex keeps the secrets safe inside connection strings and remote shell commands.
  set_env_if_missing REMOTE_DB_PASSWORD "$(openssl rand -hex 24)"
  set_env_if_missing REMOTE_RABBITMQ_PASSWORD "$(openssl rand -hex 24)"

  if [[ -z "$(env_value REMOTE_VALKEY_PASSWORD)" ]]; then
    local valkey_password
    valkey_password="$(remote "grep -m1 '^VALKEY_PASSWORD=' ~/infra/.env | cut -d= -f2-")"
    valkey_password="${valkey_password#[\'\"]}"
    valkey_password="${valkey_password%[\'\"]}"
    [[ -n "$valkey_password" ]] || die "VALKEY_PASSWORD not found in ~/infra/.env on the server."
    [[ "$valkey_password" != *[,=\$\'\"[:space:]]* ]] \
      || die "The Valkey password contains characters that break the connection string (, = \$ quotes or spaces)."
    set_env_if_missing REMOTE_VALKEY_PASSWORD "$valkey_password"
  fi

  local db_password rabbitmq_password
  db_password="$(require_value REMOTE_DB_PASSWORD)"
  rabbitmq_password="$(require_value REMOTE_RABBITMQ_PASSWORD)"

  log "Provisioning PostgreSQL roles and databases..."
  { printf "SET client_min_messages = warning;\n\\set app_password '%s'\n" "$db_password"; cat "$database_bootstrap"; } \
    | remote docker exec -i infra-postgres psql --quiet -U postgres -d postgres -v ON_ERROR_STOP=1 >/dev/null \
    || die "Could not provision the PostgreSQL databases."

  log "Provisioning RabbitMQ vhost '$rabbitmq_vhost' and user '$rabbitmq_user'..."
  remote bash -s <<EOF || die "Could not provision RabbitMQ."
set -euo pipefail
ctl() { docker exec infra-rabbitmq rabbitmqctl --quiet "\$@"; }
ctl list_vhosts name | grep -qx '$rabbitmq_vhost' || ctl add_vhost '$rabbitmq_vhost' >/dev/null
if ctl list_users | awk '{print \$1}' | grep -qx '$rabbitmq_user'; then
  ctl change_password '$rabbitmq_user' '$rabbitmq_password' >/dev/null
else
  ctl add_user '$rabbitmq_user' '$rabbitmq_password' >/dev/null
fi
ctl set_user_tags '$rabbitmq_user' management >/dev/null
ctl set_permissions -p '$rabbitmq_vhost' '$rabbitmq_user' '.*' '.*' '.*' >/dev/null
EOF

  log "Remote infrastructure is provisioned. Next: scripts/remote-infra.sh migrate"
}

migrate() {
  command -v dotnet >/dev/null 2>&1 || die "The .NET SDK is required."

  local host db_password target service project database role variable
  host="$(infra_host)"
  db_password="$(require_value REMOTE_DB_PASSWORD)"

  cd "$repository_root"
  dotnet tool restore >/dev/null

  for target in "${migration_targets[@]}"; do
    IFS='|' read -r service project database role variable <<<"$target"
    log "Applying migrations for $service..."
    env "$variable=Host=$host;Port=5432;Database=$database;Username=$role;Password=$db_password" \
      dotnet ef database update --project "$project" --startup-project "$project" \
      || die "Migrations failed for $service."
  done

  log "All migrations were applied."
}

check_port() {
  local name=$1 port=$2 host
  host="$(infra_host)"
  if timeout 3 bash -c "echo >/dev/tcp/$host/$port" 2>/dev/null; then
    log "OK    $name ($host:$port)"
  else
    log "FAIL  $name ($host:$port)"
    return 1
  fi
}

check() {
  local host failed=0
  host="$(infra_host)"

  if command -v psql >/dev/null 2>&1; then
    if PGPASSWORD="$(require_value REMOTE_DB_PASSWORD)" PGCONNECT_TIMEOUT=5 psql -h "$host" \
      -U code_for_coders_identity -d code_for_coders_identity -Atqc 'select 1' >/dev/null 2>&1; then
      log "OK    PostgreSQL login as code_for_coders_identity"
    else
      log "FAIL  PostgreSQL login as code_for_coders_identity"
      failed=1
    fi
  else
    check_port PostgreSQL 5432 || failed=1
  fi

  # /api/vhosts lists only the vhosts this user holds permissions on.
  if curl --fail --silent --max-time 5 \
    --user "$rabbitmq_user:$(require_value REMOTE_RABBITMQ_PASSWORD)" \
    "http://$host:15672/api/vhosts?columns=name" | grep -Fq "\"name\":\"$rabbitmq_vhost\""; then
    log "OK    RabbitMQ vhost $rabbitmq_vhost as $rabbitmq_user"
  else
    log "FAIL  RabbitMQ vhost $rabbitmq_vhost as $rabbitmq_user"
    failed=1
  fi

  if command -v redis-cli >/dev/null 2>&1; then
    if [[ "$(REDISCLI_AUTH="$(require_value REMOTE_VALKEY_PASSWORD)" redis-cli -h "$host" ping 2>/dev/null)" == PONG ]]; then
      log "OK    Valkey authenticated PING"
    else
      log "FAIL  Valkey authenticated PING"
      failed=1
    fi
  else
    check_port Valkey 6379 || failed=1
  fi

  check_port "OTel Collector (gRPC)" 4317 || failed=1
  check_port "smtp4dev (SMTP)" 25 || failed=1

  ((failed == 0)) || die "One or more remote services are unreachable."
}

if (($# != 1)); then
  usage >&2
  exit 2
fi

case "$1" in
  provision) provision ;;
  migrate) migrate ;;
  check) check ;;
  -h|--help|help) usage ;;
  *)
    usage >&2
    exit 2
    ;;
esac
