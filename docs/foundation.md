# Fundação do `code-4-coders`

Este documento é o ponto de entrada operacional da fundação. A Etapa 3 registrou o monorepo, o
golden path dos serviços e a decisão de runtime; os detalhes de arquitetura continuam no
[`context/architecture-baseline.md`](../context/architecture-baseline.md) e as decisões duráveis nos
[ADRs](adr/index.md).

## O que a fundação entrega

- Um monorepo com fronteiras explícitas por unidade deployável. Monorepo não significa processo,
  banco, credencial ou release compartilhados — essa distinção está no
  [ADR-0001](adr/0001-monorepo-de-codigo.md).
- Defaults de .NET 10, análise, pacotes, símbolos proibidos e build na raiz (`global.json`,
  `Directory.*`, `BannedSymbols.txt`, `.editorconfig`, `.dockerignore` e `nuget.config`).
- Onze unidades, cada uma com caminho próprio, imagem e workflow chamador próprio:

| Unidade | Stack | Solução/pacote | Workflow |
|---|---|---|---|
| `identity` | .NET | `src/identity/CodeForCoders.Identity.slnx` | `.github/workflows/identity.yml` |
| `commerce` | .NET | `src/commerce/CodeForCoders.Commerce.slnx` | `.github/workflows/commerce.yml` |
| `billing` | .NET | `src/billing/CodeForCoders.Billing.slnx` | `.github/workflows/billing.yml` |
| `learning` | .NET | `src/learning/CodeForCoders.Learning.slnx` | `.github/workflows/learning.yml` |
| `media` | .NET | `src/media/CodeForCoders.Media.slnx` | `.github/workflows/media.yml` |
| `audit` | .NET | `src/audit/CodeForCoders.Audit.slnx` | `.github/workflows/audit.yml` |
| `notification` | .NET | `src/notification/CodeForCoders.Notification.slnx` | `.github/workflows/notification.yml` |
| `bff-student` | .NET + YARP | `src/bff-student/CodeForCoders.BffStudent.slnx` | `.github/workflows/bff-student.yml` |
| `bff-admin` | .NET + YARP | `src/bff-admin/CodeForCoders.BffAdmin.slnx` | `.github/workflows/bff-admin.yml` |
| `student-spa` | React/Vite/TypeScript | `src/student-spa/package.json` | `.github/workflows/student-spa.yml` |
| `admin-spa` | React/Vite/TypeScript | `src/admin-spa/package.json` | `.github/workflows/admin-spa.yml` |

- Workflows com filtro de caminho, contexto de build na raiz quando necessário, Dockerfile explícito,
  configuração de teste `Debug` para os projetos .NET e publicação de imagem por serviço. O CI é
  reutilizável no `template-pipeline`; a fundação deste repositório não duplica a implementação da
  esteira.
- Um ambiente local comum em `docker-compose.yml`: PostgreSQL, RabbitMQ com DLX, Valkey e coletor
  OTLP. As aplicações usam bancos/credenciais próprios por serviço; os containers locais não são
  um banco compartilhado de produção.
- Health checks, outbox, heartbeat técnico, DLQ e telemetria OTLP no golden path. O endpoint vivo não
  depende das dependências; o endpoint pronto verifica as dependências exigidas pelo serviço.

## Subir o ambiente local

Na raiz do repositório:

```bash
docker compose up -d
docker compose ps
```

As portas de desenvolvimento são PostgreSQL `5432`, RabbitMQ `5672` (management em `15672`), Valkey
`6379` e OTLP `4317`/`4318`. O coletor imprime traces, métricas e logs no exporter de debug; ele não
é um backend de retenção.

Para validar um serviço .NET, use a solução do próprio serviço. Por exemplo:

```bash
dotnet test src/identity/CodeForCoders.Identity.slnx
dotnet test src/commerce/CodeForCoders.Commerce.slnx
```

Repita o mesmo formato para `learning`, `media`, `audit`, `notification`, `bff-student` e
`bff-admin`. Os testes de integração levantam suas dependências com Testcontainers quando o projeto
exigir; não coloque credenciais reais nos argumentos ou em `appsettings.json`.

Para as SPAs:

```bash
cd src/student-spa
npm ci
npm run lint
npm run test
npm run build
```

Use o mesmo conjunto em `src/admin-spa`. O `BASE_PATH` padrão do student SPA é `/student/`; a imagem
de produção recebe a configuração de runtime pelo mecanismo definido no próprio workspace.

O smoke check do `identity`, quando a aplicação estiver em execução, é:

```bash
curl -sf http://localhost:8080/health/live
curl -s http://localhost:8080/health/ready | jq
```

O endpoint técnico de heartbeat exercita o caminho HTTP → outbox → RabbitMQ → consumidor e deve
produzir um único trace correlacionado. A URL e as portas da aplicação são definidas pelo perfil de
execução do serviço; o compose fornece as dependências, não inicia automaticamente todos os onze
processos.

## Como nasce um serviço novo

1. Escolha um caminho `src/<serviço>` e registre a unidade no `foundation.services` de
   [`flow-state.json`](../flow-state.json). O nome do serviço, da imagem e do workflow deve ser
   estável.
2. Para backend, crie uma solution `.slnx` com as camadas `Domain`, `Application`, `Infra.Data`,
   `Infra.Messaging`, `Api` e `Contracts` quando houver contrato publicado. Mantenha módulos e
   schemas separados; não referencie diretamente a solution de outro serviço.
3. Dê ao serviço um banco e uma credencial próprios. Cada tabela tem um único dono de migration;
   migration é step de deploy e nunca boot da aplicação. Todo dado de negócio leva `tenant_id` desde
   o primeiro commit.
4. Adicione Dockerfile multi-stage com contexto compatível com a raiz e um workflow chamador em
   `.github/workflows/<serviço>.yml`. O workflow deve filtrar seu caminho, passar `docker-context`,
   `dockerfile`, versão de toolchain, configuração de teste e nome de imagem ao workflow reutilizável
   correto (`ci-dotnet.yml` ou `ci-react-ts.yml`).
5. Adicione testes de arquitetura, unidade, integração e ponta a ponta na proporção do serviço.
   Inclua health/live, health/ready, `service.name`, `traceparent`, logs estruturados e o contrato
   de erro vigente.
6. Atualize o checklist da fundação, o `flow-state.json` e a documentação do serviço. Rode o gate
   de estado antes de avançar:

```bash
python3 .tsg/validate_state.py
```

## Runtime e segredos

A implantação atual mantém dois ambientes isolados. Dev roda no `desenv-server` (`192.168.0.5`) no
projeto Compose `code-for-coders`, gerenciado pelo Komodo. O estável roda no `infra-server`
(`192.168.0.11`) no projeto `code-for-coders-stable`, com dependências próprias no projeto Compose
`infra`. O Caddy no host de borda termina TLS e encaminha os dois domínios; detalhes estão no
[ADR-0019](adr/0019-runtime-e-deploy-atual.md) e no [diagrama de topologia](../deploy/topology.md).

Cada host mantém sua configuração de ambiente local. `.env` e credenciais não entram no repositório,
Dockerfiles ou imagens. A atualização da aplicação recompõe os containers a partir do working tree;
provisionamento e migrations continuam em passos explícitos, fora do boot dos serviços.

Os serviços de mídia usam a API S3 compatível. Os ambientes atuais têm MinIO isolado por host; um
provedor externo pode ser configurado no ambiente de execução quando necessário. Credenciais e
valores operacionais não devem ser incorporados ao código.

Quando uma entrega depender de capacidade ainda ausente no `template-pipeline`, registre a lacuna
como issue lá e mantenha a decisão e seu histórico neste repositório.

