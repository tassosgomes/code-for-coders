# syntax=docker/dockerfile:1

# ---- build ----
FROM node:24-alpine AS build
WORKDIR /app

# The student workspace is served below /student/ by default.
ARG BASE_PATH=/student/
ENV BASE_PATH=${BASE_PATH}

COPY package*.json .npmrc ./
RUN npm ci

COPY . .
RUN npm run build

# ---- serve ----
FROM nginx:alpine

ARG BASE_PATH=/student/
ENV BASE_PATH=${BASE_PATH}

COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf.template /etc/nginx/templates/default.conf.template
COPY docker/40-runtime-env.sh /docker-entrypoint.d/40-runtime-env.sh
RUN chmod +x /docker-entrypoint.d/40-runtime-env.sh

EXPOSE 80
