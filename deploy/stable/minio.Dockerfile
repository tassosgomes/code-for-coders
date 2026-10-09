FROM golang:1.24-alpine AS build

RUN go install -v github.com/minio/minio@RELEASE.2025-10-15T17-29-55Z

FROM alpine:3.22

RUN apk add --no-cache ca-certificates
COPY --from=build /go/bin/minio /usr/local/bin/minio

EXPOSE 9000 9001
VOLUME ["/data"]
ENTRYPOINT ["/usr/local/bin/minio"]
