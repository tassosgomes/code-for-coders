# Ambiente estável

O ambiente estável roda os serviços Code for Coders e suas dependências próprias no
`infra-server` (`192.168.0.11`). O Caddy e a entrada de rede permanecem no host de borda
`desenv-server` (`192.168.0.5`), que encaminha apenas `/admin/` e `/students/` para as SPAs
estáveis. Os bancos, filas, cache e objetos do ambiente estável não são compartilhados com dev.

## Compose

No `infra-server`, a pasta `/home/tsgomes/infra` contém a infraestrutura da aplicação. O arquivo
`docker-compose.stable.infra.yml` é instalado como `docker-compose.yml`, junto com
`otel-collector.yaml`, `minio/Dockerfile` e um `.env` com permissão `0600`.

Na pasta `/home/tsgomes/code-for-coders-stable`, o stack da aplicação usa estes arquivos:

```bash
docker compose --env-file .env \
  -f docker-compose.komodo.apis.yml \
  -f docker-compose.komodo.web.yml \
  -f docker-compose.remote.yml \
  -f docker-compose.stable.yml \
  up -d --build
```

`REMOTE_INFRA_HOST=192.168.0.11`, `REMOTE_INFRA_SSH=infra-server` e
`STABLE_HOST_IP=192.168.0.11` identificam o host estável. O `scripts/remote-infra.sh` provisiona
roles e bancos PostgreSQL, o vhost e os usuários RabbitMQ, e o bucket/usuário MinIO; em seguida,
`migrate` aplica as migrations.

### Migração única de permissões do volume MinIO

A imagem MinIO roda como UID/GID `10001`. Antes de reconstruí-la em um host cujo volume `infra-minio`
foi criado pela imagem anterior executada como root, pare o serviço e transfira a propriedade dos
arquivos uma vez:

```bash
cd /home/tsgomes/infra
docker compose stop minio
docker run --rm --user 0:0 --volume infra-minio:/data alpine:3.22.2 chown -R 10001:10001 /data
docker compose up -d --build minio
```

Volumes novos recebem a propriedade do diretório `/data` da imagem e não precisam dessa migração.

Para manter segredos e chaves de runtime separados dos valores locais, a configuração pode ser
gerada em outro arquivo e selecionada pelos scripts:

```bash
LOCAL_ENV_FILE=/caminho/seguro/.env.stable scripts/generate-local-env.sh
REMOTE_INFRA_ENV_FILE=/caminho/seguro/.env.stable scripts/remote-infra.sh provision
REMOTE_INFRA_ENV_FILE=/caminho/seguro/.env.stable scripts/remote-infra.sh migrate
```

## Entrada e DNS

O registro DNS `code4coders.tasso.dev.br` segue o padrão dos demais hosts e aponta para
`192.168.0.5`. O bloco [`Caddyfile.snippet`](Caddyfile.snippet) é acrescentado ao Caddyfile do
`desenv-server`; o Caddy mantém TLS e encaminha os prefixos para `192.168.0.11:18081` e
`192.168.0.11:18082`.

## Variáveis ainda pendentes

`AWS_MEDIA_BUCKET`, `BILLING_WEBHOOK_TUNNEL_CREDENTIALS` e `MEDIA_EDGE_PUBLIC_URL` ficam vazias.
Os perfis `media-config` e `stripe-webhook` não são iniciados. Billing usa as chaves Stripe de teste
existentes no ambiente local; sem as credenciais do túnel, os eventos de pagamento não chegam ao
serviço.

O serviço de notificações usa o transporte HTTP de `Production`, cujo endpoint padrão termina em
`.invalid`. Assim o serviço inicia sem selecionar o transporte SMTP, que o código restringe a
`Development`, mas e-mails não serão entregues até que o provedor e seu endpoint sejam configurados.
O smtp4dev isolado permanece disponível apenas para diagnóstico manual e não é o transporte usado
pela aplicação estável.

O OTel Collector recebe OTLP em gRPC/HTTP e escreve no exporter `debug`, disponível pelos logs do
container com rotação limitada; esta stack não inclui Elasticsearch/Kibana para retenção e consulta.

## Topologia e atualização dos containers

O diagrama de DNS, Caddy, redes Docker e serviços por host está em [topology.md](../topology.md).

Na raiz do working tree local, use o script de atualização:

```bash
scripts/update-containers.sh dev --dry-run
scripts/update-containers.sh dev
scripts/update-containers.sh stable
scripts/update-containers.sh all
```

O alvo `dev` atualiza o Compose de desenvolvimento no `desenv-server`, usado pelo Komodo. O alvo
`stable` atualiza a aplicação no `infra-server`; `all` executa dev e depois stable. Hoje são estes
os dois stacks de aplicação configurados, sem um terceiro destino `homol` separado.

O script requer os aliases SSH `desenv-server` e `infra-server`, `rsync` local/remoto e Docker
Compose V2 nos servidores. Ele envia o working tree local como está, inclusive alterações ainda
não commitadas. Faça `--dry-run` para ver a lista de arquivos antes de atualizar. `.env` e arquivos
`.env.*` não são enviados; a configuração existente em cada host continua sendo usada. A
sincronização não remove arquivos remotos, e `docker compose up -d --build` não remove volumes.

O script atualiza apenas os containers da aplicação. Provisionamento de dependências e aplicação
de migrations continuam separados; quando uma versão incluir migrations, aplique-as no ambiente
de destino antes de atualizar os containers.
