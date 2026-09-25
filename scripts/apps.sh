#!/usr/bin/env bash

set -Eeuo pipefail

readonly script_directory="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
readonly repository_root="$(cd -- "$script_directory/.." && pwd)"
readonly compose_file="$repository_root/docker-compose.yml"
readonly remote_compose_file="$repository_root/docker-compose.remote.yml"
readonly database_bootstrap="$script_directory/init-local-databases.sql"
readonly infrastructure_services=(postgres rabbitmq valkey otel-collector)
readonly api_services=(identity learning media commerce notification audit bff-admin bff-student)
readonly spa_services=(admin-spa student-spa)
readonly app_services=("${api_services[@]}" "${spa_services[@]}")
readonly readiness_checks=(
  "identity=http://localhost:5101/health/ready"
  "learning=http://localhost:5102/health/ready"
  "media=http://localhost:5103/health/ready"
  "commerce=http://localhost:5104/health/ready"
  "notification=http://localhost:5105/health/ready"
  "audit=http://localhost:5106/health/ready"
  "bff-admin=http://localhost:5107/health/ready"
  "bff-student=http://localhost:5108/health/ready"
  "admin-spa=http://localhost:8081/healthz"
  "student-spa=http://localhost:8082/healthz"
)

readonly infrastructure_timeout=120
readonly app_startup_timeout=300
readonly application_readiness_timeout=120
readonly local_database_password=code-for-coders-local
infrastructure_mode=local

compose() {
  local files=(--file "$compose_file")
  if [[ "$infrastructure_mode" == remote ]]; then
    files+=(--file "$remote_compose_file")
  fi
  docker compose --project-directory "$repository_root" "${files[@]}" "$@"
}

log() {
  printf '[apps] %s\n' "$*"
}

die() {
  log "ERROR: $*" >&2
  exit 1
}

usage() {
  cat <<'EOF'
Usage: scripts/apps.sh <start|stop|status> [--remote]

Commands:
  start   Build and start infrastructure, APIs, and SPAs; wait for readiness.
  stop    Stop the complete local stack without deleting its data volumes.
  status  Show the state of all local stack containers.

Options:
  --remote  Use the shared development infrastructure (docker-compose.remote.yml) instead of
            local PostgreSQL, RabbitMQ, Valkey, OTel Collector, and smtp4dev containers.
            Prepare it once with: scripts/remote-infra.sh provision && scripts/remote-infra.sh migrate
EOF
}

require_docker() {
  command -v docker >/dev/null 2>&1 || die "Docker is required. Install Docker Engine and Compose v2."
  docker compose version >/dev/null 2>&1 || die "Docker Compose v2 is required."
  docker info >/dev/null 2>&1 || die "The Docker daemon is not running or is inaccessible."
}

require_compose_wait() {
  docker compose up --help 2>&1 | grep -Fq -- '--wait' \
    || die "This script requires a Docker Compose version that supports 'up --wait'."
}

validate_compose() {
  compose config --quiet || die "docker-compose.yml has invalid Compose configuration."
}

wait_for_readiness() {
  local deadline=$((SECONDS + application_readiness_timeout))
  local check service url
  local pending=()

  while ((SECONDS < deadline)); do
    pending=()
    for check in "${readiness_checks[@]}"; do
      service=${check%%=*}
      url=${check#*=}
      if ! curl --fail --silent --output /dev/null --connect-timeout 2 --max-time 3 "$url"; then
        pending+=("$service=$url")
      fi
    done

    if ((${#pending[@]} == 0)); then
      return 0
    fi

    sleep 2
  done

  for check in "${pending[@]}"; do
    service=${check%%=*}
    log "Timed out waiting for $service at ${check#*=}." >&2
    compose logs --no-color --tail=80 "$service" >&2 || true
  done

  return 1
}

start_stack() {
  require_docker
  require_compose_wait
  command -v curl >/dev/null 2>&1 || die "curl is required to check application readiness."
  validate_compose

  if [[ "$infrastructure_mode" == remote ]]; then
    log "Checking the shared development infrastructure..."
    "$script_directory/remote-infra.sh" check \
      || die "Remote infrastructure is not ready. Run: scripts/remote-infra.sh provision"
  else
    log "Starting PostgreSQL, RabbitMQ, Valkey, and OpenTelemetry Collector..."
    compose up --detach --wait --wait-timeout "$infrastructure_timeout" "${infrastructure_services[@]}" \
      || die "Infrastructure did not become healthy. Inspect it with: docker compose logs postgres rabbitmq valkey otel-collector"

    log "Provisioning local databases and application roles..."
    if ! compose exec -T postgres psql \
      --set=ON_ERROR_STOP=1 \
      --set="app_password=$local_database_password" \
      --username=code_for_coders_identity \
      --dbname=code_for_coders_identity \
      < "$database_bootstrap"; then
      die "Could not provision the local PostgreSQL databases."
    fi
  fi

  log "Building and starting the eight APIs and two SPAs..."
  if ! compose up --detach --build --wait --wait-timeout "$app_startup_timeout" "${app_services[@]}"; then
    compose logs --no-color --tail=100 "${app_services[@]}" >&2 || true
    compose stop --timeout 30 "${app_services[@]}" >/dev/null 2>&1 || true
    die "One or more application containers failed to start."
  fi

  log "Checking API readiness and frontend health endpoints..."
  if ! wait_for_readiness; then
    compose stop --timeout 30 "${app_services[@]}" >/dev/null 2>&1 || true
    die "The application stack did not become ready. Infrastructure remains running."
  fi

  log "Local applications are ready:"
  printf '  Admin SPA:    http://localhost:8081/admin/\n'
  printf '  Student SPA:  http://localhost:8082/student/\n'
  printf '  Admin BFF:    http://localhost:5107\n'
  printf '  Student BFF:  http://localhost:5108\n'
}

stop_stack() {
  require_docker
  compose stop --timeout 30
  log "Stack stopped. Named data volumes were preserved."
}

show_status() {
  require_docker
  compose ps --all
}

if (($# < 1 || $# > 2)); then
  usage >&2
  exit 2
fi

if (($# == 2)); then
  [[ "$2" == --remote ]] || { usage >&2; exit 2; }
  infrastructure_mode=remote
fi

cd "$repository_root"

case "$1" in
  start)
    start_stack
    ;;
  stop)
    stop_stack
    ;;
  status)
    show_status
    ;;
  -h|--help|help)
    usage
    ;;
  *)
    usage >&2
    exit 2
    ;;
esac
