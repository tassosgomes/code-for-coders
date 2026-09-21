# ADR-0002: Plataforma de runtime com Coolify em VPS

## Status

Accepted

## Identidade e escopo

- Caminho: `docs/adr/0002-plataforma-de-runtime-coolify.md`
- Domínios/componentes afetados: plataforma de runtime, deploy dos serviços, dependências de dados e mensageria, observabilidade, gestão de segredos e serviço de mídia
- Origem histórica: `docs/foundation-plan.md`, Etapa 3 e seção de decisões tomadas na Fundação
- Substitui: Nenhuma; formaliza uma decisão já tomada e explicita a divergência em relação a `vision.md`

## Data

2026-09-21

## Contexto

O baseline arquitetural adotou microsserviços, mas condicionou sua adoção a uma plataforma que preserve o serviço como unidade independente de deploy, escala e falha. Para isso, a plataforma precisa entregar pipeline por serviço com rollback próprio, feed interno de contratos, RabbitMQ com DLQ, PostgreSQL com banco e credencial por serviço, Valkey, coleta OTLP com backend de telemetria e um secret manager. Sem essas dependências, os serviços formariam um monólito distribuído.

`vision.md`, na seção `Restrições Técnicas`, registrou originalmente uma exigência ampla de infraestrutura AWS: “nuvem AWS, com S3 + CloudFront para armazenamento e distribuição de vídeo e materiais”. A decisão tomada na Fundação é mais restrita: Coolify hospedado em VPS para compute, dados e runtime da plataforma; AWS S3 + CloudFront permanecem exclusivamente para armazenamento e distribuição de mídia. Esta ADR registra a divergência de forma explícita para que a restrição ampla da visão não seja interpretada como obrigação de executar todos os serviços na AWS.

O produto está em estágio inicial, com equipe pequena e volume inicial baixo. A prioridade é provar o golden path ponta a ponta com isolamento por serviço, operação simples, artefatos imutáveis, observabilidade e rollback verificável, mantendo uma rota de evolução caso disponibilidade, escala ou requisitos de operação deixem de ser atendidos pela VPS.

## Decisão

Usaremos Coolify em VPS como plataforma inicial de runtime para os serviços da aplicação e para as dependências de compute, dados e infraestrutura de execução. AWS não será usada como plataforma geral de compute ou dados nesta fase.

O limite da decisão é o seguinte:

- Os serviços continuam sendo unidades independentes de deploy, escala e falha. Cada serviço terá sua própria imagem, configuração, pipeline e rollback.
- O PostgreSQL será provisionado com um banco e uma credencial por serviço. Cada tabela terá um único serviço responsável por sua migration; migration será um step de deploy e nunca fará parte do boot da aplicação.
- O RabbitMQ será provisionado com filas quorum, DLX/DLQ e política de retenção. O outbox continua sendo responsabilidade do produtor e o consumidor deve ser idempotente.
- O Valkey atenderá cache e sessão opaca dos BFFs; não será fonte de verdade de dados de negócio.
- Um coletor OTLP e os backends de traces, métricas e logs serão executados na plataforma de runtime, preservando `service.name`, propagação de `traceparent` e correlação por `traceId`.
- Segredos serão entregues por um secret manager integrado ao Coolify e por GitHub Environments quando aplicável. Nenhuma credencial será armazenada no repositório ou na imagem de container.
- O CD usará a imagem identificada por digest. `dev` terá deploy automático; `staging` e `prod` serão GitHub Environments protegidos por reviewers. A promoção entre ambientes reutilizará o mesmo digest validado.
- Rollback será o redeploy explícito do digest anterior, seguido de health checks e evidência funcional. Não haverá rollback baseado apenas em uma tag mutável.
- AWS S3 e CloudFront serão usados exclusivamente pelo domínio de mídia para armazenamento e distribuição de vídeos e materiais. O compute da aplicação, os bancos, o broker, o cache, a telemetria e o secret manager não serão movidos para AWS por esta decisão.

## Alternativas Consideradas

### Alternativa 1: AWS integral

- **Descrição:** Executar compute e dependências de runtime/dados em serviços gerenciados ou nativos da AWS, além de manter S3 e CloudFront para mídia.
- **Prós:** Maior oferta de alta disponibilidade e escalabilidade gerenciada; integração ampla de IAM, rede, observabilidade, backup e secrets; menor dependência de administrar o host físico.
- **Contras:** Custo fixo e variável maior; mais componentes e decisões de rede, IAM e operação; maior complexidade para uma equipe pequena e para o volume inicial; o caminho de deploy e rollback ainda precisaria ser construído por serviço.
- **Por que rejeitada:** A decisão atual prioriza simplicidade operacional e custo proporcional ao estágio do produto. AWS continua sendo a escolha específica para S3 + CloudFront, mas a exigência de AWS para toda a plataforma foi restringida.

### Alternativa 2: Kubernetes

- **Descrição:** Executar os serviços e dependências em um cluster Kubernetes, em VPS ou em um serviço gerenciado, usando manifests ou charts para deploy, configuração e rollback.
- **Prós:** Modelo declarativo e extensível; bom suporte a escala horizontal, isolamento, automação e evolução para múltiplos nós; reduz dependência de uma ferramenta única de PaaS.
- **Contras:** Introduz custo operacional de cluster, rede, ingress, volumes, upgrades, secrets, observabilidade e política de segurança; exige mais conhecimento e automação antes de entregar o golden path; não há necessidade de sua superfície de operação no estágio atual.
- **Por que rejeitada:** Coolify atende o requisito de deploy independente, ambientes, secrets e rollback por digest com menor complexidade. Kubernetes permanece uma possível evolução quando os requisitos de disponibilidade, escala ou governança justificarem esse custo.

## Consequências

### Positivas

- A equipe consegue operar compute e dependências de runtime em uma superfície menor, mantendo o isolamento lógico e operacional entre serviços.
- O mesmo artefato imutável é promovido entre ambientes, e o rollback é determinístico por digest.
- Postgres por serviço, RabbitMQ com DLQ, Valkey, OTLP e secrets ficam alinhados ao baseline sem exigir uma plataforma Kubernetes ou uma adoção integral da AWS.
- S3 + CloudFront preservam a distribuição eficiente de mídia e mantêm o domínio de mídia separado das decisões de compute.
- O caminho de migração futura permanece aberto porque os serviços são empacotados como containers e o deploy é definido por contrato de imagem, ambiente e digest.

### Negativas

- A VPS e o plano de controle do Coolify concentram mais risco de disponibilidade do que uma plataforma totalmente gerenciada ou um cluster multi-nó.
- A equipe passa a ser responsável por hardening, atualizações, capacidade, backups, restauração e recuperação das dependências de dados e mensageria.
- Operar dois provedores — VPS/Coolify para runtime e AWS para mídia — exige credenciais, conectividade, monitoramento e diagnóstico entre ambientes distintos.
- A plataforma ganha acoplamento operacional ao Coolify, embora os serviços permaneçam portáveis como imagens de container.

### Riscos

- **Falha ou saturação da VPS:** pode interromper vários serviços e dependências simultaneamente. Mitigar com monitoramento de capacidade e saúde, backups externos, procedimento de restauração testado e gatilhos explícitos para adicionar capacidade ou migrar para uma topologia de maior disponibilidade.
- **Perda ou corrupção de dados:** Postgres, RabbitMQ e Valkey têm necessidades diferentes de retenção e recuperação. Mitigar com backup e restauração testados por dependência, retenção documentada, isolamento de credenciais e verificação de quorum/DLQ.
- **Exposição de segredos:** um segredo no repositório, na imagem ou em logs compromete mais de um ambiente. Mitigar com secret manager, GitHub Environments com reviewers, permissões mínimas, rotação e bloqueio de credenciais no CI.
- **Rollback incompatível com o schema:** uma imagem anterior pode não entender uma migration já aplicada. Mitigar com migrations compatíveis e evolutivas, execução antes do rollout, validação de imutabilidade e correção progressiva para mudanças irreversíveis; rollback de aplicação não deve ser presumido como rollback de banco.
- **Interrupção da integração com AWS:** falhas de credencial, política, rede ou CDN podem indisponibilizar mídia mesmo com o runtime saudável. Mitigar com políticas mínimas para S3/CloudFront, URLs assinadas de vida curta, testes de integração, alertas e rotação controlada das credenciais.
- **Dependência excessiva da plataforma:** limites do Coolify ou da VPS podem aparecer com o crescimento. Mitigar mantendo imagens OCI, configuração versionada, deploy por digest, evidências de restore/rollback e critérios de reavaliação para AWS integral ou Kubernetes.

## Notas de Implementação

- Implementar o workflow reutilizável `cd-coolify.yml` para consumir os outputs `image-digest` e `version` da CI por serviço.
- Mapear `dev`, `staging` e `prod` para os ambientes de deploy; deixar `dev` automático e exigir reviewers nos gates de `staging` e `prod` por GitHub Environments.
- Executar a migration como job/step de deploy antes do rollout da aplicação, com exatamente um serviço responsável por cada tabela. Nunca executar migration no boot.
- Provisionar no Coolify o PostgreSQL com banco e credencial por serviço, RabbitMQ com quorum queues e DLX/DLQ, Valkey, coletor OTLP e backends de traces, métricas e logs.
- Injetar segredos no runtime por meio do secret manager; não copiá-los para o repositório, Dockerfile ou imagem. Usar permissões específicas também para o acesso do domínio `media` a S3 e CloudFront.
- Promover o mesmo digest entre ambientes, validar `/health/live` e `/health/ready`, e registrar a evidência de redeploy do digest anterior com confirmação funcional.
- O ambiente local continua usando as dependências definidas no `docker-compose.yml`; o deploy de produção não deve depender de estado local ou de tags mutáveis.
- `vision.md` foi atualizado para v1.2 na mesma Etapa 3, restringindo a exigência AWS a armazenamento e distribuição de mídia. Esta ADR registra a decisão operacional; a visão permanece a fonte estratégica.

## Referências

- `docs/foundation-plan.md` — decisões da Fundação, dependências de plataforma, F0-04 a F0-07, ambientes, deploy por digest e rollback.
- `vision.md` — seção `Restrições Globais > Restrições Técnicas`, fonte da exigência ampla de AWS que esta ADR restringe.
- `context/architecture-baseline.md` — dependências de plataforma, propriedade de dados, mensageria, sessão em Valkey, observabilidade OTLP e proteção de mídia.
