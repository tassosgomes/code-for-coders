# AGENTS.md

* Use **Context7** when working with third-party libraries to consult up-to-date documentation.
* Before completing a task, ensure **lint**, **build**, and relevant **tests** pass.
* Never create database migrations manually. Use the project's migration tooling. In .NET, use **Entity Framework**.
* Do not manually edit generated artifacts. Use the tool or generator responsible for them.
* Converse com o usuário no mesmo idioma que ele está conversando com você

## Ambiente de testes

* O servidor já tem PostgreSQL, RabbitMQ, Valkey, **S3 (MinIO)**, OTel Collector e smtp4dev. Não suba essa infraestrutura localmente (nem o MinIO). A não ser que seja explicitamente solicitado
* Apenas os containers de aplicação (APIs e SPAs, mais leves) sobem na máquina local, apontando para o servidor: `scripts/remote-infra.sh provision && scripts/remote-infra.sh migrate` (idempotentes, rode `migrate` quando houver novas migrations) e `scripts/apps.sh start --remote`. Use `scripts/remote-infra.sh check` para validar a conectividade.
* Ao criar uma nova dependência de infraestrutura em `docker-compose.yml`, replique o override em `docker-compose.remote.yml` para o modo `--remote` não depender de hostnames locais (`valkey`, `rabbitmq`, `minio`, etc.).
* Testes com **Testcontainers** consomem muitos recursos: evite executar vários em paralelo. A disputa por recursos degrada o tempo e a estabilidade dos testes (flakiness). Prefira execução sequencial ou paralelismo mínimo.

<!-- groma:start -->
## Groma

This project uses Groma. Before you scan, inspect, or curate architecture, or change files for a Backlog task, run `groma agent-instructions` and read the guide it names for that job. When it reports a first scan, ask the user whether they want you to curate the architecture. Do not edit Groma-owned architecture files directly.
<!-- groma:end -->

<!-- BACKLOG.MD GUIDELINES START -->
<!-- backlog.md-instructions-version: 1.53.0 -->
<CRITICAL_INSTRUCTION>

## Backlog.md Workflow

This project uses Backlog.md for task and project management.

**At the beginning of each conversation in this project, run `backlog instructions overview` before answering or taking action. Re-read it only if you have not read it yet in the current conversation.**

Use the overview to decide whether to search, read, create, or update Backlog tasks.

Before task lifecycle actions, read the matching detailed guide:
- `backlog instructions task-creation` before creating or splitting tasks
- `backlog instructions task-execution` before planning, changing status or assignee, adding a plan or implementation notes, or implementing task work
- `backlog instructions task-finalization` before checking acceptance criteria, writing final summaries, or moving tasks to terminal statuses

Use `backlog <command> --help` before running unfamiliar commands. Help shows options, fields, and examples.

Do not edit Backlog task, draft, document, decision, or milestone markdown files directly. Use the `backlog` CLI so metadata, relationships, and history stay consistent.

</CRITICAL_INSTRUCTION>
<!-- BACKLOG.MD GUIDELINES END -->
