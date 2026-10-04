#!/bin/sh
set -eu

: "${API_URL:?API_URL nao definida no ambiente}"
export SCHOOL_TIME_ZONE="${SCHOOL_TIME_ZONE:-America/Sao_Paulo}"
export OTEL_ENDPOINT="${OTEL_ENDPOINT:-}"

envsubst '${API_URL} ${OTEL_ENDPOINT} ${SCHOOL_TIME_ZONE}' \
  < /etc/nginx/runtime-env.template.js \
  > /usr/share/nginx/html/runtime-env.js
