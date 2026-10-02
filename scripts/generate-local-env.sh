#!/usr/bin/env bash

set -Eeuo pipefail

readonly script_directory="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
readonly repository_root="$(cd -- "$script_directory/.." && pwd)"
readonly env_file="$repository_root/.env"

if [[ -e "$env_file" ]]; then
  command -v openssl >/dev/null 2>&1 || {
    printf '[local-env] OpenSSL is required to generate local development keys.\n' >&2
    exit 1
  }

  changed=false
  if ! rg -q '^BFF_ADMIN_STUDENT_LOOKUP_SCOPE=' "$env_file"; then
    printf '\nBFF_ADMIN_STUDENT_LOOKUP_SCOPE=student-account:lookup\n' >> "$env_file"
    changed=true
  fi
  if ! rg -q '^BFF_ADMIN_OUTBOX_KEY_B64=' "$env_file"; then
    bff_admin_outbox_key_b64="$(openssl rand -base64 32 | tr -d '\n')"
    printf '\nBFF_ADMIN_OUTBOX_KEY_B64=%s\n' "$bff_admin_outbox_key_b64" >> "$env_file"
    if ! rg -q '^BFF_ADMIN_OUTBOX_KEY_VERSION=' "$env_file"; then
      printf 'BFF_ADMIN_OUTBOX_KEY_VERSION=v1\n' >> "$env_file"
    fi
    printf '[local-env] Added a development-only BFF outbox key to %s.\n' "$env_file"
    changed=true
  fi

  # Dedicated key pair for the bff-student -> commerce service assertion (ADR-0009).
  if ! rg -q '^BFF_COMMERCE_PRIVATE_KEY_B64=' "$env_file"; then
    umask 077
    existing_key_dir="$(mktemp -d)"
    trap 'rm -rf -- "$existing_key_dir"' EXIT
    openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048 -out "$existing_key_dir/bff-commerce-private.pem"
    {
      printf '\nBFF_COMMERCE_PUBLIC_KEY_B64=%s\n' "$(openssl pkey -in "$existing_key_dir/bff-commerce-private.pem" -pubout -outform DER | base64 | tr -d '\n')"
      printf 'BFF_COMMERCE_PRIVATE_KEY_B64=%s\n' "$(openssl pkcs8 -topk8 -inform PEM -outform DER -nocrypt -in "$existing_key_dir/bff-commerce-private.pem" | base64 | tr -d '\n')"
    } >> "$env_file"
    printf '[local-env] Added the development-only bff-student commerce key pair to %s.\n' "$env_file"
    changed=true
  fi

  if ! rg -q '^COMMERCE_IDENTITY_PRIVATE_KEY_B64=' "$env_file"; then
    umask 077
    commerce_identity_key_dir="$(mktemp -d)"
    openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048 -out "$commerce_identity_key_dir/private.pem"
    {
      printf '\nCOMMERCE_IDENTITY_PUBLIC_KEY_B64=%s\n' "$(openssl pkey -in "$commerce_identity_key_dir/private.pem" -pubout -outform DER | base64 | tr -d '\n')"
      printf 'COMMERCE_IDENTITY_PRIVATE_KEY_B64=%s\n' "$(openssl pkcs8 -topk8 -inform PEM -outform DER -nocrypt -in "$commerce_identity_key_dir/private.pem" | base64 | tr -d '\n')"
      printf 'SCHOOL_TIME_ZONE=America/Sao_Paulo\n'
    } >> "$env_file"
    rm -rf -- "$commerce_identity_key_dir"
    changed=true
  fi

  chmod 600 "$env_file"
  if [[ "$changed" == false ]]; then
    printf '[local-env] %s already contains the generated configuration; keeping the current configuration.\n' "$env_file"
  fi
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
openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048 -out "$key_material_dir/bff-admin-private.pem"
openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048 -out "$key_material_dir/identity-staff-token-private.pem"
openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048 -out "$key_material_dir/bff-commerce-private.pem"
openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048 -out "$key_material_dir/commerce-identity-private.pem"
commerce_identity_private_key_b64="$(openssl pkcs8 -topk8 -inform PEM -outform DER -nocrypt -in "$key_material_dir/commerce-identity-private.pem" | base64 | tr -d '\n')"
commerce_identity_public_key_b64="$(openssl pkey -in "$key_material_dir/commerce-identity-private.pem" -pubout -outform DER | base64 | tr -d '\n')"

private_key_b64="$(openssl pkcs8 -topk8 -inform PEM -outform DER -nocrypt \
  -in "$key_material_dir/bff-private.pem" | base64 | tr -d '\n')"
public_key_b64="$(openssl pkey -in "$key_material_dir/bff-private.pem" -pubout \
  -outform DER | base64 | tr -d '\n')"
admin_private_key_b64="$(openssl pkcs8 -topk8 -inform PEM -outform DER -nocrypt \
  -in "$key_material_dir/bff-admin-private.pem" | base64 | tr -d '\n')"
admin_public_key_b64="$(openssl pkey -in "$key_material_dir/bff-admin-private.pem" -pubout \
  -outform DER | base64 | tr -d '\n')"
identity_staff_token_private_key_b64="$(openssl pkcs8 -topk8 -inform PEM -outform DER -nocrypt \
  -in "$key_material_dir/identity-staff-token-private.pem" | base64 | tr -d '\n')"
commerce_private_key_b64="$(openssl pkcs8 -topk8 -inform PEM -outform DER -nocrypt \
  -in "$key_material_dir/bff-commerce-private.pem" | base64 | tr -d '\n')"
commerce_public_key_b64="$(openssl pkey -in "$key_material_dir/bff-commerce-private.pem" -pubout \
  -outform DER | base64 | tr -d '\n')"
idempotency_key_b64="$(openssl rand -base64 32 | tr -d '\n')"
outbox_key_b64="$(openssl rand -base64 32 | tr -d '\n')"
bff_admin_outbox_key_b64="$(openssl rand -base64 32 | tr -d '\n')"

{
  printf 'COMMERCE_IDENTITY_PUBLIC_KEY_B64=%s\n' "$commerce_identity_public_key_b64"
  printf 'COMMERCE_IDENTITY_PRIVATE_KEY_B64=%s\n' "$commerce_identity_private_key_b64"
  printf 'SCHOOL_TIME_ZONE=America/Sao_Paulo\n'
  printf 'NOTIFICATION_NAMESPACE=default\n'
  printf 'STUDENT_ACCOUNT_CONFIRMATION_URL=http://localhost:8082/student/confirm-account\n'
  printf 'ACCOUNT_CONFIRMATION_VALIDITY_HOURS=24\n'
  printf 'STUDENT_PASSWORD_RESET_URL=http://localhost:8082/student/redefinir-senha\n'
  printf 'PASSWORD_RESET_VALIDITY_HOURS=1\n'
  printf 'STUDENT_TENANT_ID=00000000-0000-7000-8000-000000000001\n'
  printf 'IDENTITY_IDEMPOTENCY_KEY_B64=%s\n' "$idempotency_key_b64"
  printf 'IDENTITY_OUTBOX_KEY_B64=%s\n' "$outbox_key_b64"
  printf 'BFF_ADMIN_OUTBOX_KEY_B64=%s\n' "$bff_admin_outbox_key_b64"
  printf 'BFF_ADMIN_OUTBOX_KEY_VERSION=v1\n'
  printf 'BFF_ADMIN_STUDENT_LOOKUP_SCOPE=student-account:lookup\n'
  printf 'BFF_IDENTITY_PUBLIC_KEY_B64=%s\n' "$public_key_b64"
  printf 'BFF_IDENTITY_PRIVATE_KEY_B64=%s\n' "$private_key_b64"
  printf 'BFF_ADMIN_IDENTITY_PUBLIC_KEY_B64=%s\n' "$admin_public_key_b64"
  printf 'BFF_ADMIN_IDENTITY_PRIVATE_KEY_B64=%s\n' "$admin_private_key_b64"
  printf 'BFF_COMMERCE_PUBLIC_KEY_B64=%s\n' "$commerce_public_key_b64"
  printf 'BFF_COMMERCE_PRIVATE_KEY_B64=%s\n' "$commerce_private_key_b64"
  printf 'IDENTITY_STAFF_TOKEN_PRIVATE_KEY_B64=%s\n' "$identity_staff_token_private_key_b64"
} > "$env_temp_file"

chmod 600 "$env_temp_file"
mv -- "$env_temp_file" "$env_file"
env_temp_file=""

printf '[local-env] Created %s with development-only values.\n' "$env_file"
