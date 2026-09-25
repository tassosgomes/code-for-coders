#!/bin/sh
set -eu

: "${API_URL:?API_URL nao definida no ambiente}"
export OTEL_ENDPOINT="${OTEL_ENDPOINT:-}"

envsubst '${API_URL} ${OTEL_ENDPOINT}' \
  < /etc/nginx/runtime-env.template.js \
  > /usr/share/nginx/html/runtime-env.js
