#!/usr/bin/env bash
# Gera o certificado autoassinado (somente teste, descartável) da página de pagamento do Stripe falso.
# O commerce só aceita `paymentUrl` https; o Playwright usa `ignoreHTTPSErrors`.
set -euo pipefail

target="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)/.state/tls"
mkdir -p "$target"
if [[ -s "$target/cert.pem" && -s "$target/key.pem" ]]; then
  exit 0
fi
openssl req -x509 -newkey rsa:2048 -nodes -days 3 -subj "/CN=localhost" \
  -addext "subjectAltName=DNS:localhost,IP:127.0.0.1" \
  -keyout "$target/key.pem" -out "$target/cert.pem" 2>/dev/null
chmod 644 "$target/key.pem" "$target/cert.pem"
echo "[fake-stripe] certificado de teste gerado em $target"
