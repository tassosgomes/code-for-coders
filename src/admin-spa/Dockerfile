# syntax=docker/dockerfile:1

# ---- build ----
FROM node:24-alpine AS build
WORKDIR /app

# The admin workspace is served below /admin/ by default.
ARG BASE_PATH=/admin/
ENV BASE_PATH=${BASE_PATH}

COPY package*.json .npmrc ./
RUN npm ci

COPY . .
RUN npm run build

# ---- serve ----
# Unprivileged nginx listens on 8080. Root is only used to install files.
FROM nginxinc/nginx-unprivileged:alpine

USER root

ARG BASE_PATH=/admin/
ENV BASE_PATH=${BASE_PATH}

COPY --from=build /app/dist /usr/share/nginx/html
RUN mv /usr/share/nginx/html/runtime-env.template.js /etc/nginx/runtime-env.template.js \
 && chown -R nginx:nginx /usr/share/nginx/html /etc/nginx/runtime-env.template.js
COPY nginx.conf.template /etc/nginx/templates/default.conf.template
COPY docker/40-runtime-env.sh /docker-entrypoint.d/40-runtime-env.sh
RUN chmod 755 /docker-entrypoint.d/40-runtime-env.sh

USER nginx

EXPOSE 8080
