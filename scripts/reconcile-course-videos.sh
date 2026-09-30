#!/usr/bin/env bash
set -euo pipefail

# Query Media's own HTTP contract and inspect only Learning's own projection.
tenant_id="${1:?Usage: reconcile-course-videos.sh TENANT_ID MEDIA_BASE_URL}"
media_base_url="${2:?Usage: reconcile-course-videos.sh TENANT_ID MEDIA_BASE_URL}"
: "${MEDIA_ACCESS_TOKEN:?Export a short-lived Media token for this tenant with midia.enviar}"
export RECONCILE_TENANT_ID="$tenant_id"
python3 - <<'PY'
import base64, json, os
token = os.environ['MEDIA_ACCESS_TOKEN'].split('.')[1]
claims = json.loads(base64.urlsafe_b64decode(token + '=' * (-len(token) % 4)))
if claims.get('tenantId', '').lower() != os.environ['RECONCILE_TENANT_ID'].lower():
    raise SystemExit('Media token tenant does not match the reconciliation tenant.')
PY

temp_dir=$(mktemp -d)
trap 'rm -rf "$temp_dir"' EXIT
printf '[]' > "$temp_dir/ids.json"
page=1
total=0
while true; do
  curl --silent --show-error --fail --max-time 20 \
    -H "Authorization: Bearer $MEDIA_ACCESS_TOKEN" \
    "${media_base_url%/}/internal/v1/videos?status=ready&_page=$page&_size=50" > "$temp_dir/page.json"
  jq -e --argjson page "$page" '.pagination.page == $page and (.data | type == "array") and all(.data[]; .status == "ready")' "$temp_dir/page.json" > /dev/null
  jq -s '.[0] + [.[1].data[].videoId]' "$temp_dir/ids.json" "$temp_dir/page.json" > "$temp_dir/next.json"
  mv "$temp_dir/next.json" "$temp_dir/ids.json"
  total=$(jq '.pagination.total' "$temp_dir/page.json")
  pages=$(jq '.pagination.totalPages' "$temp_dir/page.json")
  if (( page >= pages )); then break; fi
  page=$((page + 1))
done
jq -e --argjson total "$total" 'length == $total and (unique | length) == $total' "$temp_dir/ids.json" > /dev/null
jq --arg tenant "$tenant_id" '{tenantId: $tenant, readyVideoIds: .}' "$temp_dir/ids.json" > "$temp_dir/manifest.json"
repo_root=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)
if [[ -n "${RECONCILE_MANIFEST_OUTPUT:-}" ]]; then cp "$temp_dir/manifest.json" "$RECONCILE_MANIFEST_OUTPUT"; fi
cd "$repo_root"
dotnet run --project src/learning/src/CodeForCoders.Learning.Api -- \
  "--VideoProjection:ReconcileManifest=$temp_dir/manifest.json"
