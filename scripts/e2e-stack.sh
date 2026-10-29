#!/usr/bin/env bash
# Stack efêmera da suíte e2e (TASK-42.3): infraestrutura local, migrations, as nove APIs, os dois SPAs e o
# Stripe falso (docker-compose.e2e.yml). É o mesmo caminho do modo local de `scripts/apps.sh start`, com as
# migrations aplicadas antes de subir as aplicações (o /health/ready lê as tabelas de outbox).
#
#   scripts/e2e-stack.sh up      gera o .env, sobe a infraestrutura, aplica as migrations e sobe as aplicações
#   scripts/e2e-stack.sh down    derruba tudo e apaga os volumes (a stack é descartável)
#   scripts/e2e-stack.sh logs    mostra o final dos logs de cada container (para diagnosticar falha no CI)
#
# Para rodar contra o servidor compartilhado em vez de infraestrutura local, use
# `APPS_EXTRA_COMPOSE_FILES=docker-compose.e2e.yml scripts/apps.sh start --remote` (ver e2e/README.md).
set -Eeuo pipefail

readonly script_directory="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
readonly repository_root="$(cd -- "$script_directory/.." && pwd)"
readonly local_database_password=code-for-coders-local
readonly e2e_override=docker-compose.e2e.yml
# service|projeto Infra.Data|banco|papel de migration|variável da connection string de design-time
readonly migration_targets=(
  "identity|src/identity/src/CodeForCoders.Identity.Infra.Data|code_for_coders_identity|code_for_coders_identity|ConnectionStrings__DefaultConnection"
  "learning|src/learning/src/CodeForCoders.Learning.Infra.Data|code_for_coders_learning|code_for_coders_learning|ConnectionStrings__DefaultConnection"
  "media|src/media/src/CodeForCoders.Media.Infra.Data|code_for_coders_media|code_for_coders_media|ConnectionStrings__DefaultConnection"
  "commerce|src/commerce/src/CodeForCoders.Commerce.Infra.Data|code_for_coders_commerce|code_for_coders_commerce|ConnectionStrings__DefaultConnection"
  "billing|src/billing/src/CodeForCoders.Billing.Infra.Data|code_for_coders_billing|code_for_coders_billing|ConnectionStrings__DefaultConnection"
  "notification|src/notification/src/CodeForCoders.Notification.Infra.Data|code_for_coders_notification|code_for_coders_notification|ConnectionStrings__DefaultConnection"
  "audit|src/audit/src/CodeForCoders.Audit.Infra.Data|code_for_coders_audit|code_for_coders_audit|ConnectionStrings__MigrationConnection"
  "bff-admin|src/bff-admin/src/CodeForCoders.BffAdmin.Infra.Data|code_for_coders_bff_admin|code_for_coders_bff_admin|ConnectionStrings__DefaultConnection"
  "bff-student|src/bff-student/src/CodeForCoders.BffStudent.Infra.Data|code_for_coders_bff_student|code_for_coders_bff_student|ConnectionStrings__DefaultConnection"
)

log() { printf '[e2e-stack] %s\n' "$*"; }
die() { log "ERROR: $*" >&2; exit 1; }

compose() {
  LOCAL_DB_PASSWORD="$local_database_password" docker compose --project-directory "$repository_root" \
    --file "$repository_root/docker-compose.yml" --file "$repository_root/$e2e_override" "$@"
}

start_infrastructure() {
  log "Subindo PostgreSQL, RabbitMQ, Valkey e o coletor OTel..."
  compose up --detach --wait --wait-timeout 180 postgres rabbitmq valkey otel-collector
  log "Criando os bancos e papéis de aplicação..."
  compose exec -T postgres psql \
    --set=ON_ERROR_STOP=1 \
    --set="app_password=$local_database_password" \
    --username=code_for_coders_identity \
    --dbname=code_for_coders_identity \
    < "$script_directory/init-local-databases.sql"
}

apply_migrations() {
  command -v dotnet >/dev/null 2>&1 || die "O SDK .NET é necessário para aplicar as migrations."
  cd "$repository_root"
  dotnet tool restore >/dev/null

  # `dotnet ef` não restaura os pacotes. O restore é sequencial (compartilha o cache de pacotes) e deixa as
  # migrations, que só compilam o próprio projeto, rodarem em paralelo sem disputar estado.
  local solution
  for solution in src/*/CodeForCoders.*.slnx; do
    case "$solution" in src/contract-testing/*|src/media-edge/*) continue ;; esac
    log "Restaurando pacotes de $solution..."
    dotnet restore "$solution" --verbosity quiet
  done

  local pids=() services=() target service project database role variable
  for target in "${migration_targets[@]}"; do
    IFS='|' read -r service project database role variable <<<"$target"
    log "Aplicando migrations de $service..."
    env "$variable=Host=localhost;Port=5432;Database=$database;Username=$role;Password=$local_database_password" \
      dotnet ef database update --project "$project" --startup-project "$project" >"/tmp/e2e-migrate-$service.log" 2>&1 &
    pids+=("$!")
    services+=("$service")
  done

  local failed=0 index
  for index in "${!pids[@]}"; do
    if ! wait "${pids[$index]}"; then
      log "Migrations de ${services[$index]} falharam:" >&2
      tail -n 40 "/tmp/e2e-migrate-${services[$index]}.log" >&2 || true
      failed=1
    fi
  done
  ((failed == 0)) || die "Falha ao aplicar as migrations."
}

up() {
  cd "$repository_root"
  bash "$script_directory/generate-local-env.sh"
  bash "$repository_root/e2e/fake-stripe/make-cert.sh"
  start_infrastructure
  apply_migrations
  log "Construindo e subindo as aplicações (APIs, SPAs e Stripe falso)..."
  APPS_EXTRA_COMPOSE_FILES="$e2e_override" bash "$script_directory/apps.sh" start
  log "Stack pronta."
}

down() {
  cd "$repository_root"
  compose down --volumes --remove-orphans
}

logs() {
  cd "$repository_root"
  compose ps --all || true
  compose logs --no-color --tail=60 || true
}

case "${1:-}" in
  up) up ;;
  down) down ;;
  logs) logs ;;
  *) printf 'Uso: %s <up|down|logs>\n' "$0" >&2; exit 2 ;;
esac
