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

## Backoffice service credential (CAP-002)

Identity also trusts service assertions from the `bff-admin` edge, each edge with its own key pair and
allowed scopes: `bff-student` keeps the `student-*` scopes, `bff-admin` receives the `staff-*` scopes.
The generator creates both pairs (`BFF_IDENTITY_*` and `BFF_ADMIN_IDENTITY_*`); the Compose file injects
the `bff-admin` public key into Identity and the private key into `bff-admin`.

The generator preserves an existing `.env` rather than replacing local configuration. If your `.env`
was created before the `bff-admin` keys existed, delete it (or append the missing keys) and regenerate:

```bash
rm .env
scripts/generate-local-env.sh
docker compose up --build
```

Without the `BFF_ADMIN_IDENTITY_*` values Identity refuses to start, reporting the missing
`bff-admin` issuer key.

## First backoffice administrator

With the local stack running, provision the first administrator for the configured tenant. Identity
stores the account, administrator role, hashed reset token, and protected notification request in one
commit. The command never prints the email address or reset link.

Apply the Identity schema before provisioning. The local PostgreSQL password below is the Compose
development password; use the configured database connection if yours differs.

```bash
dotnet tool restore
ConnectionStrings__DefaultConnection='Host=localhost;Port=5432;Database=code_for_coders_identity;Username=code_for_coders_identity;Password=code-for-coders-local' \
  dotnet ef database update \
  --project src/identity/src/CodeForCoders.Identity.Infra.Data/CodeForCoders.Identity.Infra.Data.csproj \
  --startup-project src/identity/src/CodeForCoders.Identity.Api/CodeForCoders.Identity.Api.csproj
```

```bash
docker compose exec identity dotnet CodeForCoders.Identity.Api.dll provision-first-admin \
  --tenant 00000000-0000-7000-8000-000000000001 \
  --email admin@example.com \
  --name "Backoffice administrator"
```

Open the captured message in smtp4dev at `http://127.0.0.1:5000` and follow the link under
`http://localhost:8081/admin/redefinir-senha`. A second invocation reports that the tenant already
has an administrator and leaves the data unchanged. The link base can be changed with
`STAFF_PASSWORD_RESET_URL`. If `.env` changes `STUDENT_TENANT_ID`, pass that same tenant ID to the
command.
