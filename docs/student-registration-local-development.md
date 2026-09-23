# Local student registration

The local stack signs BFF-to-Identity assertions with an ephemeral RSA key and HMACs idempotency
keys and request fingerprints. Identity also encrypts addressed notification requests (recipient and
tokenized link) in its outbox with a 256-bit key and decrypts them only when publishing. Generate
those values in the shell that starts Compose; no key file is written into the repository:

```bash
set -eu
umask 077
key_dir="$(mktemp -d)"
trap 'rm -rf "$key_dir"' EXIT

openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048 -out "$key_dir/bff-private.pem"
export BFF_IDENTITY_PRIVATE_KEY_B64="$(openssl pkcs8 -topk8 -inform PEM -outform DER -nocrypt -in "$key_dir/bff-private.pem" | base64 | tr -d '\n')"
export BFF_IDENTITY_PUBLIC_KEY_B64="$(openssl pkey -in "$key_dir/bff-private.pem" -pubout -outform DER | base64 | tr -d '\n')"
export IDENTITY_IDEMPOTENCY_KEY_B64="$(openssl rand -base64 32 | tr -d '\n')"
export IDENTITY_OUTBOX_KEY_B64="$(openssl rand -base64 32 | tr -d '\n')"

docker compose up --build
```

Open the SPA at `http://localhost:8082/student/cadastro` and submit the registration form. The message is captured
in smtp4dev at `http://127.0.0.1:5000`. Its link points to `http://localhost:8082/student/confirm-account?token=...`,
the confirmation route served under the SPA base path `/student/`; override it with
`STUDENT_ACCOUNT_CONFIRMATION_URL` if the SPA is served from another origin or base path. The Compose configuration uses the same
`ACCOUNT_CONFIRMATION_VALIDITY_HOURS` value for Identity and Notification; set it in the shell
before `docker compose up` if the local link lifetime should differ from 24 hours.

Password recovery starts at `http://localhost:8082/student/recuperar-senha`. The captured message links to
`http://localhost:8082/student/redefinir-senha?token=...`, the reset route under the same base path; override it
with `STUDENT_PASSWORD_RESET_URL` if the SPA is served from another origin or base path. Identity and Notification
share `PASSWORD_RESET_VALIDITY_HOURS` (default 1 hour).
