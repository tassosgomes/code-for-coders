#!/usr/bin/env bash
# Boot script for Cloud Agents. systemd is not running in this VM, so Docker
# is started directly. The shared dev server is not reachable; this brings up
# the local Compose stack and applies EF Core migrations before the APIs.

set -euo pipefail

repository_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repository_root"

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export COMPOSE_PARALLEL_LIMIT="${COMPOSE_PARALLEL_LIMIT:-1}"
export DOCKER_BUILDKIT=1

readonly local_database_password=code-for-coders-local
readonly readiness_urls=(
  "http://localhost:5101/health/ready"
  "http://localhost:5102/health/ready"
  "http://localhost:5103/health/ready"
  "http://localhost:5109/healthz"
  "http://localhost:5104/health/ready"
  "http://localhost:5105/health/ready"
  "http://localhost:5106/health/ready"
  "http://localhost:5107/health/ready"
  "http://localhost:5108/health/ready"
  "http://localhost:8081/healthz"
  "http://localhost:8082/healthz"
)

log() {
  printf '[cloud-agent] %s\n' "$*"
}

tmux_cmd() {
  if [[ -f /exec-daemon/tmux.portal.conf ]]; then
    tmux -f /exec-daemon/tmux.portal.conf "$@"
  else
    tmux "$@"
  fi
}

stack_is_ready() {
  local url
  for url in "${readiness_urls[@]}"; do
    curl --fail --silent --output /dev/null --connect-timeout 2 --max-time 3 "$url" || return 1
  done
}

ensure_docker() {
  if sudo docker info >/dev/null 2>&1; then
    sudo chmod 666 /var/run/docker.sock || true
    return 0
  fi

  sudo rm -f /var/run/docker.sock /var/run/docker.pid
  tmux_cmd has-session -t dockerd 2>/dev/null || tmux_cmd new-session -d -s dockerd -- \
    "sudo dockerd --host unix:///var/run/docker.sock >>/tmp/dockerd.log 2>&1"
  local attempt
  for attempt in $(seq 1 60); do
    if sudo docker info >/dev/null 2>&1; then
      sudo chmod 666 /var/run/docker.sock || true
      return 0
    fi
    sleep 1
  done
  log "Docker daemon did not become ready. See /tmp/dockerd.log."
  return 1
}

apply_local_migrations() {
  local targets=(
    "identity|src/identity/src/CodeForCoders.Identity.Infra.Data|code_for_coders_identity|code_for_coders_identity|ConnectionStrings__DefaultConnection"
    "learning|src/learning/src/CodeForCoders.Learning.Infra.Data|code_for_coders_learning|code_for_coders_learning|ConnectionStrings__DefaultConnection"
    "media|src/media/src/CodeForCoders.Media.Infra.Data|code_for_coders_media|code_for_coders_media|ConnectionStrings__DefaultConnection"
    "commerce|src/commerce/src/CodeForCoders.Commerce.Infra.Data|code_for_coders_commerce|code_for_coders_commerce|ConnectionStrings__DefaultConnection"
    "notification|src/notification/src/CodeForCoders.Notification.Infra.Data|code_for_coders_notification|code_for_coders_notification|ConnectionStrings__DefaultConnection"
    "audit|src/audit/src/CodeForCoders.Audit.Infra.Data|code_for_coders_audit|code_for_coders_audit|ConnectionStrings__MigrationConnection"
    "bff-admin|src/bff-admin/src/CodeForCoders.BffAdmin.Infra.Data|code_for_coders_bff_admin|code_for_coders_bff_admin|ConnectionStrings__DefaultConnection"
    "bff-student|src/bff-student/src/CodeForCoders.BffStudent.Infra.Data|code_for_coders_bff_student|code_for_coders_bff_student|ConnectionStrings__DefaultConnection"
  )
  local target service project database role variable

  dotnet tool restore
  for target in "${targets[@]}"; do
    IFS='|' read -r service project database role variable <<<"$target"
    log "Applying EF Core migrations for ${service}..."
    env "${variable}=Host=localhost;Port=5432;Database=${database};Username=${role};Password=${local_database_password}" \
      dotnet ef database update --project "$project" --startup-project "$project"
  done
}

prepare_local_databases() {
  log "Starting PostgreSQL, RabbitMQ, Valkey, and the OpenTelemetry collector..."
  docker compose --project-directory "$repository_root" --file "$repository_root/docker-compose.yml" \
    up --detach --wait --wait-timeout 120 postgres rabbitmq valkey otel-collector

  log "Provisioning local databases and roles..."
  docker compose --project-directory "$repository_root" --file "$repository_root/docker-compose.yml" \
    exec -T postgres psql \
    --set=ON_ERROR_STOP=1 \
    --set="app_password=${local_database_password}" \
    --username=code_for_coders_identity \
    --dbname=code_for_coders_identity \
    < "$repository_root/scripts/init-local-databases.sql"
}

ensure_docker

if stack_is_ready; then
  log "Local application stack is already ready."
  exit 0
fi

bash "$repository_root/scripts/generate-local-env.sh"
prepare_local_databases
apply_local_migrations

log "Building and starting APIs, workers, and SPAs..."
bash "$repository_root/scripts/apps.sh" start

log "Local stack is ready."
