# Code for Coders

[![Quality gate status](https://sonarcloud.io/api/project_badges/measure?project=tassosgomes_code-for-coders&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=tassosgomes_code-for-coders)
[![admin-spa](https://github.com/tassosgomes/code-for-coders/actions/workflows/admin-spa.yml/badge.svg?branch=main)](https://github.com/tassosgomes/code-for-coders/actions/workflows/admin-spa.yml)
[![student-spa](https://github.com/tassosgomes/code-for-coders/actions/workflows/student-spa.yml/badge.svg)](https://github.com/tassosgomes/code-for-coders/actions/workflows/student-spa.yml)
[![audit](https://github.com/tassosgomes/code-for-coders/actions/workflows/audit.yml/badge.svg)](https://github.com/tassosgomes/code-for-coders/actions/workflows/audit.yml)
[![bff-admin](https://github.com/tassosgomes/code-for-coders/actions/workflows/bff-admin.yml/badge.svg)](https://github.com/tassosgomes/code-for-coders/actions/workflows/bff-admin.yml)
[![bff-student](https://github.com/tassosgomes/code-for-coders/actions/workflows/bff-student.yml/badge.svg)](https://github.com/tassosgomes/code-for-coders/actions/workflows/bff-student.yml)
[![commerce](https://github.com/tassosgomes/code-for-coders/actions/workflows/commerce.yml/badge.svg)](https://github.com/tassosgomes/code-for-coders/actions/workflows/commerce.yml)
[![identity](https://github.com/tassosgomes/code-for-coders/actions/workflows/identity.yml/badge.svg)](https://github.com/tassosgomes/code-for-coders/actions/workflows/identity.yml)
[![learning](https://github.com/tassosgomes/code-for-coders/actions/workflows/learning.yml/badge.svg)](https://github.com/tassosgomes/code-for-coders/actions/workflows/learning.yml)
[![media](https://github.com/tassosgomes/code-for-coders/actions/workflows/media.yml/badge.svg)](https://github.com/tassosgomes/code-for-coders/actions/workflows/media.yml)
[![notification](https://github.com/tassosgomes/code-for-coders/actions/workflows/notification.yml/badge.svg)](https://github.com/tassosgomes/code-for-coders/actions/workflows/notification.yml)

## Local application stack

The local stack runs the eight .NET APIs, both SPAs, and their PostgreSQL, RabbitMQ, Valkey, and
OpenTelemetry dependencies with Docker Compose. It requires Docker Engine, a Docker Compose version
that supports `up --wait`, and `curl`. The following host ports must be available: 13133, 4317,
4318, 5432, 5672, 6379, 8081, 8082, 15672, and 5101–5108.

```bash
./scripts/apps.sh start
./scripts/apps.sh status
./scripts/apps.sh stop
```

`start` builds the application images, provisions the local PostgreSQL roles and databases, and
waits for API readiness and frontend health checks. It creates databases but does not apply schema
migrations. `stop` stops the stack and preserves its named data volumes. Run `start` again to resume
it.

| Application | Local URL |
| --- | --- |
| Admin SPA | <http://localhost:8081/admin/> |
| Student SPA | <http://localhost:8082/student/> |
| Identity API | <http://localhost:5101/health/ready> |
| Learning API | <http://localhost:5102/health/ready> |
| Media API | <http://localhost:5103/health/ready> |
| Commerce API | <http://localhost:5104/health/ready> |
| Notification API | <http://localhost:5105/health/ready> |
| Audit API | <http://localhost:5106/health/ready> |
| Admin BFF | <http://localhost:5107/health/ready> |
| Student BFF | <http://localhost:5108/health/ready> |

RabbitMQ management console: <http://localhost:15672> (local credentials: `code_for_coders` /
`code-for-coders-local`).

Container logs can be followed with `docker compose logs -f <service>`.

### Shared development infrastructure

The APIs and SPAs can also run locally against the shared development server (`192.168.0.5`, see
[docs/infra-servidor-desenv.md](docs/infra-servidor-desenv.md)) instead of local PostgreSQL,
RabbitMQ, Valkey, MinIO (S3), OTel Collector, and smtp4dev containers. `docker-compose.remote.yml` overrides the
API settings; `scripts/remote-infra.sh` prepares the server and requires SSH access through the
`desenv-server` host alias.

```bash
./scripts/remote-infra.sh provision   # roles, databases, RabbitMQ vhost/user, S3 bucket/user; stores secrets in .env
./scripts/remote-infra.sh migrate     # applies the EF Core migrations of every service
./scripts/apps.sh start --remote
```

`provision` and `migrate` are idempotent; run `migrate` again whenever a branch adds migrations.
`scripts/remote-infra.sh check` verifies connectivity. The project uses the RabbitMQ vhost
`code-for-coders`, Valkey database `1` (`REMOTE_VALKEY_DATABASE`), and the MinIO bucket
`code-for-coders-media` with a bucket-scoped user. Emails land in the shared
smtp4dev at <https://smtp.tasso.dev.br> and telemetry in Kibana at <https://kibana.tasso.dev.br>.

### Coolify development environment

The stack is deployed on Coolify (project `code4coders`, environment `development`, server
`192.168.0.11`) as two resources: `code4coders-apis` (nine backend services, from
`docker-compose.coolify.apis.yml`) and `code4coders-web` (both BFFs and both SPAs, from
`docker-compose.coolify.web.yml`). The split keeps each rendered compose below the kernel
per-argument limit that breaks Coolify deploys of large compose files
([coollabsio/coolify#11737](https://github.com/coollabsio/coolify/issues/11737)). Deploy
`code4coders-apis` first: the BFFs resolve the API hostnames (`identity`, `learning`, `media`,
`commerce`, `audit`) through the external `c4c-shared` Docker network, which must exist on the
server (`docker network create c4c-shared`). Builds run on the Coolify server itself, and only
the domains below are public through the Coolify proxy (Traefik, wildcard TLS
`*.lab.tasso.dev.br`):

| Application | URL |
| --- | --- |
| Admin SPA | <https://c4c-admin.lab.tasso.dev.br/admin/> |
| Student SPA | <https://c4c-student.lab.tasso.dev.br/student/> |
| Identity API | <https://c4c-identity.lab.tasso.dev.br> |
| Learning API | <https://c4c-learning.lab.tasso.dev.br> |
| Media API | <https://c4c-media.lab.tasso.dev.br> |
| Commerce API | <https://c4c-commerce.lab.tasso.dev.br> |
| Notification API | <https://c4c-notification.lab.tasso.dev.br> |
| Audit API | <https://c4c-audit.lab.tasso.dev.br> |
| Media Edge | <https://c4c-media-edge.lab.tasso.dev.br> |
| Admin BFF | <https://c4c-bff-admin.lab.tasso.dev.br> |
| Student BFF | <https://c4c-bff-student.lab.tasso.dev.br> |

Secrets (`REMOTE_*`, `*_KEY_B64`) live only in the Coolify environment variables — never in the
repository. Pushes to `main` under the watched paths redeploy automatically. The local stack and
the Coolify stack share the same PostgreSQL/RabbitMQ/Valkey on `192.168.0.5`; avoid running both
at the same time against the same databases.
