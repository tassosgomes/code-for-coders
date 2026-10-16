#!/bin/sh
set -eu

: "${API_URL:?API_URL nao definida no ambiente}"
export OTEL_ENDPOINT="${OTEL_ENDPOINT:-}"
export TURNSTILE_SITE_KEY="${TURNSTILE_SITE_KEY:-}"

envsubst '${API_URL} ${OTEL_ENDPOINT} ${TURNSTILE_SITE_KEY}' \
  < /etc/nginx/runtime-env.template.js \
  > /usr/share/nginx/html/runtime-env.js
