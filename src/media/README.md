# Media service

Media validates staff tokens through Identity JWKS and stores private multipart uploads in S3-compatible storage. The browser writes file parts only through short-lived presigned `PUT` URLs; Media lists and completes parts through its internal storage client.

Object keys contain only the configured prefix, tenant ID, video ID, and `original`. Titles, file names, uploader names, provider upload IDs, and presigned URLs are not written to logs or spans.

Local development uses MinIO from `docker-compose.yml`. The bucket is private, and the public signing endpoint must be reachable by the browser from the admin SPA origin.

Set `Media__Role` on every Media process. The value is required and must be `api` or `worker`: `api` serves HTTP requests and consumes platform heartbeats; `worker` runs video preparation and the periodic abandoned-upload expiration scan.
