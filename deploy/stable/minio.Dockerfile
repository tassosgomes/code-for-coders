FROM golang:1.24.10-alpine3.22 AS build

RUN apk add --no-cache git
WORKDIR /src
RUN git init . \
    && git remote add origin https://github.com/minio/minio.git \
    && git fetch --depth 1 origin refs/tags/RELEASE.2025-10-15T17-29-55Z \
    && test "$(git rev-parse FETCH_HEAD)" = "9e49d5e7a648f00e26f2246f4dc28e6b07f8c84a" \
    && git checkout --detach FETCH_HEAD

RUN mkdir -p /out \
    && CGO_ENABLED=0 go build -mod=readonly -trimpath -o /out/minio .

FROM alpine:3.22.2

RUN apk add --no-cache ca-certificates
RUN addgroup -S -g 10001 minio \
    && adduser -S -D -H -u 10001 -G minio minio \
    && mkdir -p /data /home/minio/.minio \
    && chown -R 10001:10001 /data /home/minio

ENV HOME=/home/minio
COPY --from=build /out/minio /usr/local/bin/minio

EXPOSE 9000 9001
VOLUME ["/data"]
USER 10001:10001
ENTRYPOINT ["/usr/local/bin/minio"]
