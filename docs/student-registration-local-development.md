# Local student registration

The local stack signs BFF-to-Identity assertions with an RSA key and HMACs idempotency keys and
request fingerprints. Identity also encrypts addressed notification requests (recipient and tokenized
link) in its outbox with a 256-bit key and decrypts them only when publishing. Create a local `.env`
with development-only keys and endpoint defaults:

```bash
scripts/generate-local-env.sh
docker compose up --build
```

The generator creates `.env` at the repository root with permissions restricted to the current user;
Git ignores this file. It preserves an existing `.env` rather than replacing local configuration.

Open the SPA at `http://localhost:8082/student/cadastro` and submit the registration form. The message is captured
in smtp4dev at `http://127.0.0.1:5000`. Its link points to `http://localhost:8082/student/confirm-account?token=...`,
the confirmation route served under the SPA base path `/student/`; override it with
`STUDENT_ACCOUNT_CONFIRMATION_URL` if the SPA is served from another origin or base path. The Compose configuration uses the same
`ACCOUNT_CONFIRMATION_VALIDITY_HOURS` value for Identity and Notification; edit `.env` if the local
link lifetime should differ from 24 hours.

Password recovery starts at `http://localhost:8082/student/recuperar-senha`. The captured message links to
`http://localhost:8082/student/redefinir-senha?token=...`, the reset route under the same base path; override it
with `STUDENT_PASSWORD_RESET_URL` if the SPA is served from another origin or base path. Identity and Notification
share `PASSWORD_RESET_VALIDITY_HOURS` (default 1 hour); edit `.env` to change it.
