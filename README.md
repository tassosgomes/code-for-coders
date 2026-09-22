# Code for Coders

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
