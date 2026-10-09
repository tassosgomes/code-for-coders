# Topologia de rede e containers

## Visão geral

```mermaid
flowchart LR
    browser["Navegador na rede local ou VPN"] --> dns["DNS A<br/>dev-code4coders.tasso.dev.br<br/>code4coders.tasso.dev.br<br/>192.168.0.5"]
    dns -->|HTTPS 443| caddy

    subgraph edge["desenv-server · 192.168.0.5 · Docker Engine"]
        subgraph infraDev["Compose project infra"]
            caddy["Caddy<br/>TLS e roteamento"]
            komodo["Komodo Core e Periphery"]
            devDeps["Infra de dev<br/>PostgreSQL · RabbitMQ · Valkey<br/>MinIO · OTel · smtp4dev"]
        end

        subgraph devApp["Compose project code-for-coders · dev"]
            subgraph devNet["c4c-shared · rede Docker local"]
                devSpas["admin-spa · student-spa"]
                devBffs["bff-admin · bff-student"]
                devApis["identity · learning · commerce<br/>billing · notification · audit"]
                devSpas --> devBffs --> devApis
            end
        end
    end

    caddy -->|/admin/* e /students/*<br/>pela c4c-shared local| devSpas
    devApp -->|192.168.0.5 · portas publicadas<br/>25 · 5432 · 5672 · 6379 · 9000 · 4317/4318| devDeps

    subgraph stable["infra-server · 192.168.0.11 · Docker Engine"]
        subgraph stableApp["Compose project code-for-coders-stable"]
            subgraph stableNet["c4c-shared · rede Docker local"]
                stableSpas["admin-spa · student-spa<br/>18081 · 18082"]
                stableBffs["bff-admin · bff-student"]
                stableApis["identity · learning · commerce<br/>billing · notification · audit"]
                stableSpas --> stableBffs --> stableApis
            end
        end

        subgraph stableInfra["Compose project infra · rede infra_default"]
            stableDeps["Infra estável isolada<br/>PostgreSQL · RabbitMQ · Valkey<br/>MinIO · OTel · smtp4dev para diagnóstico"]
        end
    end

    caddy -->|code4coders.tasso.dev.br<br/>/admin/* → 192.168.0.11:18081<br/>/students/* → 192.168.0.11:18082| stableSpas
    stableApp -->|192.168.0.11 · portas publicadas<br/>5432 · 5672 · 6379 · 9000 · 4317/4318| stableDeps

    classDef ingress fill:#0b7285,color:#fff,stroke:#075766
    classDef apps fill:#364fc7,color:#fff,stroke:#243a9e
    classDef infra fill:#2b8a3e,color:#fff,stroke:#1b5e2b
    class browser,dns,caddy ingress
    class devSpas,devBffs,devApis,stableSpas,stableBffs,stableApis apps
    class devDeps,stableDeps,komodo infra
```

## Como o tráfego passa

- Os dois registros DNS A apontam atualmente para `192.168.0.5`, endereço privado do `desenv-server`.
- O Caddy termina TLS e atende os dois domínios. A raiz redireciona para `/admin/`; as rotas sem barra final recebem redirecionamento para a versão com barra.
- No domínio `dev-code4coders.tasso.dev.br`, o Caddy acessa as SPAs diretamente pela rede Docker `c4c-shared` do `desenv-server`.
- No domínio `code4coders.tasso.dev.br`, o Caddy encaminha `/admin/*` e `/students/*` pelas portas `18081` e `18082` do `infra-server`.
- Cada SPA encaminha as chamadas da sua API ao BFF correspondente. BFFs e APIs ficam nas redes locais `c4c-shared`; não há publicação externa dessas portas.
- As aplicações conectam-se às dependências pelas portas publicadas no endereço do próprio host: `.5` para dev e `.11` para estável. As redes `c4c-shared` têm o mesmo nome, mas pertencem a Docker Engines diferentes e não formam uma rede entre hosts.
- PostgreSQL, RabbitMQ, Valkey e MinIO são separados por ambiente. O stack estável não usa dados nem filas do ambiente dev.
- No estável, o smtp4dev fica disponível para diagnóstico, mas não transporta e-mails da aplicação. Os serviços opcionais de mídia e do túnel Stripe também não estão iniciados enquanto as variáveis correspondentes permanecerem vazias.

## Projetos Compose

| Ambiente | Host e diretório | Projeto de aplicação | Arquivos da aplicação |
| --- | --- | --- | --- |
| Dev | `desenv-server:/home/tsgomes/code-for-coders` | `code-for-coders` | `docker-compose.komodo.apis.yml`, `docker-compose.komodo.web.yml` |
| Estável | `infra-server:/home/tsgomes/code-for-coders-stable` | `code-for-coders-stable` | Os dois arquivos Komodo, `docker-compose.remote.yml` e `docker-compose.stable.yml` |

Em ambos os hosts, o projeto `infra` é separado do projeto da aplicação. No `desenv-server`, ele também contém Caddy e Komodo; no `infra-server`, contém apenas as dependências próprias do ambiente estável.

## Alcance de rede

Como os registros DNS publicados apontam para um IP privado, o desenho descreve o acesso pela rede local ou VPN. O acesso pela internet pública depende de rota de entrada/NAT e DNS público apropriado; ele não foi confirmado nesta implantação.

Para atualizar os containers a partir do working tree local, consulte [scripts/update-containers.sh](../scripts/update-containers.sh) e a seção de atualização no [guia do ambiente estável](stable/README.md).
