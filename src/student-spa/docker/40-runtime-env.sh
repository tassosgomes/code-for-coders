#!/bin/sh
set -eu

: "${API_URL:?API_URL nao definida no ambiente}"
export SCHOOL_TIME_ZONE="${SCHOOL_TIME_ZONE:-America/Sao_Paulo}"
export DIDACTIC_NOTICE_ENABLED="${DIDACTIC_NOTICE_ENABLED:-true}"
export OTEL_ENDPOINT="${OTEL_ENDPOINT:-}"
export TURNSTILE_SITE_KEY="${TURNSTILE_SITE_KEY:-}"

envsubst '${DIDACTIC_NOTICE_ENABLED} ${API_URL} ${OTEL_ENDPOINT} ${SCHOOL_TIME_ZONE} ${TURNSTILE_SITE_KEY}' \
  < /etc/nginx/runtime-env.template.js \
  > /usr/share/nginx/html/runtime-env.js
