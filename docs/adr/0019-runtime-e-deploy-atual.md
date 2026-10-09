# ADR-0019: Runtime e deploy atuais

## Status

Accepted

## Identidade e escopo

- Caminho: `docs/adr/0019-runtime-e-deploy-atual.md`
- Domínios/componentes afetados: deploy da aplicação, dependências de runtime, entrada HTTP e segredos operacionais
- Data: 2026-10-09
- Substitui: a decisão inicial de runtime registrada na ADR-0002

## Contexto

A implantação atual ocupa dois hosts. Desenvolvimento roda em `desenv-server` (`192.168.0.5`),
com a aplicação gerenciada pelo Komodo. O ambiente estável roda em `infra-server` (`192.168.0.11`)
como um projeto Docker Compose separado. A entrada HTTPS permanece no Caddy do `desenv-server`.

Os dois ambientes precisam manter aplicação, bancos, mensageria, cache e objetos isolados. O DNS
atual aponta ambos os domínios para um endereço privado; o desenho descreve acesso pela rede local
ou VPN.

## Decisão

- Dev usa o projeto Compose `code-for-coders`, gerenciado pelo Komodo no `desenv-server`, com
  `docker-compose.komodo.apis.yml` e `docker-compose.komodo.web.yml`.
- Estável usa o projeto Compose `code-for-coders-stable` no `infra-server`, combinando os mesmos
  arquivos com `docker-compose.remote.yml` e `docker-compose.stable.yml`.
- Cada ambiente mantém um projeto `infra` próprio. PostgreSQL, RabbitMQ, Valkey e MinIO não são
  compartilhados entre dev e estável. O coletor OTel e smtp4dev também são isolados.
- O Caddy do `desenv-server` termina TLS para `dev-code4coders.tasso.dev.br` e
  `code4coders.tasso.dev.br`. No domínio dev, encaminha `/admin/` e `/students/` pela rede Docker
  local `c4c-shared`. No estável, encaminha esses prefixos a `192.168.0.11:18081` e
  `192.168.0.11:18082`. O nome `c4c-shared` em cada host representa redes Docker independentes.
- Atualizações da aplicação sincronizam o working tree com os diretórios de cada host e executam
  `docker compose up -d --build` com os arquivos correspondentes. `.env` permanece local a cada
  host e fora do repositório. Provisionamento da infraestrutura e migrations são operações
  explícitas e separadas do boot da aplicação.
- Os serviços de mídia integram com armazenamento S3 compatível; os stacks atuais provisionam
  instâncias MinIO próprias por ambiente.

## Consequências

- Dev e estável podem ser atualizados de forma independente; a opção `all` do script de deploy os
  atualiza em sequência.
- A atualização recompila as imagens no host de destino a partir do working tree sincronizado; não
  existe promoção automática do mesmo digest entre ambientes neste fluxo.
- Dados e filas permanecem isolados por host, enquanto Caddy fornece uma única entrada HTTPS.
- Os registros DNS atuais usam `192.168.0.5`. A exposição pela internet pública depende de rota de
  entrada e DNS público apropriados.

## Referências

- [Topologia de rede e containers](../../deploy/topology.md)
- [Guia do ambiente estável](../../deploy/stable/README.md)
- [Script de atualização](../../scripts/update-containers.sh)
