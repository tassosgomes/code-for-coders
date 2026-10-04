#!/usr/bin/env bash
# Development scenario: shorten one courtesy grant while its student is watching.
set -Eeuo pipefail

if [[ $# -ne 3 ]]; then
  printf 'Usage: %s <tenant-uuid> <grant-uuid> <seconds-until-expiry>\n' "$0" >&2
  exit 2
fi
readonly tenant_id=$1 grant_id=$2 expires_in=$3
readonly uuid_pattern='^[[:xdigit:]]{8}-[[:xdigit:]]{4}-[[:xdigit:]]{4}-[[:xdigit:]]{4}-[[:xdigit:]]{12}$'
[[ "$tenant_id" =~ $uuid_pattern && "$grant_id" =~ $uuid_pattern && "$expires_in" =~ ^[0-9]+$ ]] || exit 2
(( 10#$expires_in <= 300 )) || { printf 'Use a duration between 0 and 300 seconds.\n' >&2; exit 2; }
readonly infra_ssh=${REMOTE_INFRA_SSH:-desenv-server}
ssh -o BatchMode=yes -o ConnectTimeout=10 "$infra_ssh" \
  docker exec -i infra-postgres psql --quiet -U postgres -d code_for_coders_commerce \
  -v ON_ERROR_STOP=1 -v tenant_id="$tenant_id" -v grant_id="$grant_id" -v expires_in="$expires_in" <<'SQL'
BEGIN;
SELECT count(*) = 1 AS found FROM entitlement.access_grants
WHERE tenant_id = :'tenant_id'::uuid AND id = :'grant_id'::uuid AND origin = 'courtesy' \gset
\if :found
SELECT id, expires_at AS previous_expiry FROM entitlement.access_grants
WHERE tenant_id = :'tenant_id'::uuid AND id = :'grant_id'::uuid AND origin = 'courtesy';
UPDATE entitlement.access_grants
SET expires_at = clock_timestamp() + make_interval(secs => :'expires_in'::int)
WHERE tenant_id = :'tenant_id'::uuid AND id = :'grant_id'::uuid AND origin = 'courtesy'
RETURNING id, expires_at AS scenario_expiry;
COMMIT;
\else
ROLLBACK;
\echo No matching courtesy grant in this tenant.
\quit 3
\endif
SQL
