#!/usr/bin/env bash

set -Eeuo pipefail

readonly script_directory="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
readonly repository_root="$(cd -- "$script_directory/.." && pwd)"
readonly env_file="$repository_root/.env"

if [[ -e "$env_file" ]]; then
  printf '[local-env] %s already exists; keeping the current configuration.\n' "$env_file"
  exit 0
fi

command -v openssl >/dev/null 2>&1 || {
  printf '[local-env] OpenSSL is required to generate local development keys.\n' >&2
  exit 1
}

umask 077
key_material_dir="$(mktemp -d)"
env_temp_file=""

cleanup() {
  rm -rf -- "$key_material_dir"
  if [[ -n "$env_temp_file" ]]; then
    rm -f -- "$env_temp_file"
  fi
}

trap cleanup EXIT

env_temp_file="$(mktemp "$repository_root/.env.tmp.XXXXXX")"
openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048 -out "$key_material_dir/bff-private.pem"

private_key_b64="$(openssl pkcs8 -topk8 -inform PEM -outform DER -nocrypt \
  -in "$key_material_dir/bff-private.pem" | base64 | tr -d '\n')"
public_key_b64="$(openssl pkey -in "$key_material_dir/bff-private.pem" -pubout \
  -outform DER | base64 | tr -d '\n')"
idempotency_key_b64="$(openssl rand -base64 32 | tr -d '\n')"
outbox_key_b64="$(openssl rand -base64 32 | tr -d '\n')"

{
  printf 'NOTIFICATION_NAMESPACE=default\n'
  printf 'STUDENT_ACCOUNT_CONFIRMATION_URL=http://localhost:8082/student/confirm-account\n'
  printf 'ACCOUNT_CONFIRMATION_VALIDITY_HOURS=24\n'
  printf 'STUDENT_PASSWORD_RESET_URL=http://localhost:8082/student/redefinir-senha\n'
  printf 'PASSWORD_RESET_VALIDITY_HOURS=1\n'
  printf 'STUDENT_TENANT_ID=00000000-0000-7000-8000-000000000001\n'
  printf 'IDENTITY_IDEMPOTENCY_KEY_B64=%s\n' "$idempotency_key_b64"
  printf 'IDENTITY_OUTBOX_KEY_B64=%s\n' "$outbox_key_b64"
  printf 'BFF_IDENTITY_PUBLIC_KEY_B64=%s\n' "$public_key_b64"
  printf 'BFF_IDENTITY_PRIVATE_KEY_B64=%s\n' "$private_key_b64"
} > "$env_temp_file"

chmod 600 "$env_temp_file"
mv -- "$env_temp_file" "$env_file"
env_temp_file=""

printf '[local-env] Created %s with development-only values.\n' "$env_file"
