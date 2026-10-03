FROM nginx:1.27-alpine
COPY src/media-edge/nginx.conf /etc/nginx/nginx.conf
COPY src/media-edge/default.conf.template /etc/nginx/templates/default.conf.template
COPY src/media-edge/s3-signing.js /etc/nginx/s3-signing.js
