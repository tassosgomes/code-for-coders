# ADR-0006: Preparação de vídeo em worker próprio do media e custódia da chave HLS

## Status

Proposed

## Identidade e escopo

- Caminho: `docs/adr/0006-preparacao-de-video-e-custodia-de-chave.md`
- Domínios/componentes afetados: Entrega de Mídia e Proteção (`media` — API e worker), implantação (compose e Coolify)
- Origem histórica: `tasks/prd-ingestao-midia`, CAP-006
- Substitui: Nenhuma

## Data

2026-09-25

## Contexto

O serviço `media` precisa transformar o vídeo enviado pelo professor numa versão HLS segmentada e cifrada
com AES-128, em três qualidades (1080p, 720p, 480p), com ffmpeg rodando dentro do próprio serviço — e não
num transcodificador gerenciado. A preparação consome CPU em rajada por dezenas de minutos e precisa de
alguns gigabytes de disco temporário por vídeo. O mesmo serviço vai servir, na capacidade seguinte, a
entrega ao aluno (sessão de reprodução, chave, URL assinada), que é latência no caminho crítico "aluno
autorizado → vídeo começa a tocar".

Proteção sem DRM: não existe objeto de vídeo público, a versão de reprodução é a mesma para todos os alunos
e a chave AES só pode sair do serviço para uma sessão de reprodução válida. Duas perguntas têm resposta
durável para todo o domínio: **onde roda o ffmpeg** e **onde vive a chave**.

## Decisão

1. **A preparação roda num worker do `media`, em container próprio, com a mesma imagem da API.** Um
   parâmetro de configuração define o papel do processo: `api` atende HTTP e publica o outbox; `worker`
   prepara vídeos, descarta envios expirados, emite os sinais de volume e também publica o outbox. Os dois
   papéis compartilham banco, código e pipeline; só o conjunto de hosted services e de endpoints muda. O
   worker expõe apenas health checks.
2. **A fila de preparação é o próprio estado do vídeo no banco do `media`.** O worker reivindica um vídeo em
   `received` com `FOR UPDATE SKIP LOCKED` e grava uma concessão (lease) com prazo, renovada enquanto o
   ffmpeg roda. Lease vencida volta o vídeo a ser elegível e conta como tentativa. Não há fila de
   mensageria para o trabalho interno do serviço.
3. **Uma chave AES-128 aleatória por vídeo, gerada no worker e guardada no banco do `media` cifrada com
   AES-GCM** por uma chave-mestra de configuração secreta, fora do repositório e da imagem. A chave nunca é
   gravada no armazenamento de objetos, em mensagem, em log, em span ou em resposta HTTP. O arquivo de
   informação de chave exigido pelo ffmpeg vive só no diretório de trabalho temporário do worker e é apagado
   ao fim da preparação, com sucesso ou falha.
4. **As playlists guardadas não apontam para endereço real de chave.** A URI de chave gravada na playlist é
   um marcador opaco por vídeo; o serviço que entregar a playlist ao aluno a reescreve para o endpoint de
   chave vinculado à sessão. Nenhum objeto no armazenamento revela como obter a chave.

## Alternativas Consideradas

### Alternativa 1: BackgroundService dentro do processo da API

- **Descrição:** a própria API reivindica e prepara vídeos, com concorrência 1 e threads do ffmpeg limitadas.
- **Prós:** um container a menos; implantação idêntica à de hoje.
- **Contras:** a entrega da capacidade seguinte disputa CPU e memória com o ffmpeg; um crash do ffmpeg ou
  falta de disco derruba a API; escalar a API escala a preparação junto.
- **Por que rejeitada:** a latência de entrega é caminho crítico do produto, e separar depois vira retrabalho.

### Alternativa 2: Fila de trabalho no RabbitMQ

- **Descrição:** a conclusão do envio publica um comando de preparação consumido pelo worker.
- **Prós:** entrega e retentativa pelo broker.
- **Contras:** uma mensagem de 40 minutos de processamento exige ack tardio ou estado paralelo no banco; o
  estado do vídeo já é a fonte de verdade e teria de ser reconciliado com a fila.
- **Por que rejeitada:** duplica a fonte de verdade sem ganho para um único serviço consumidor de si mesmo.

### Alternativa 3: Chave no armazenamento de objetos ou no secret manager

- **Descrição:** gravar a chave como objeto privado ao lado dos segmentos, ou uma entrada de segredo por vídeo.
- **Prós:** nenhuma cifra de aplicação.
- **Contras:** no armazenamento, uma política de bucket errada expõe segmentos e chave juntos; no secret
  manager, uma entrada por vídeo não escala e acopla o domínio ao provedor de segredos.
- **Por que rejeitada:** a chave precisa de controle de acesso próprio, separado dos segmentos.

## Consequências

### Positivas

- A API de mídia mantém latência previsível mesmo com lote de preparações.
- Um vídeo nunca é preparado duas vezes ao mesmo tempo, e um worker que morre não deixa vídeo preso.
- Vazamento do bucket não entrega a chave; vazamento do banco não entrega os segmentos.

### Negativas

- Mais um container no compose e no Coolify, com volume de trabalho de alguns gigabytes.
- A chave-mestra passa a ser segredo operacional crítico: perdê-la inutiliza todos os vídeos preparados.
- A entrega ao aluno precisa reescrever a playlist em vez de servi-la direto do armazenamento.

### Riscos

- **Perda da chave-mestra:** backup do segredo no secret manager com o mesmo cuidado do banco; rotação
  suportada guardando o identificador da chave-mestra junto de cada chave cifrada.
- **Disco temporário insuficiente:** o worker verifica o espaço livre antes de reivindicar e só pega um vídeo
  que caiba; a métrica de vídeo preso alerta o resto.
- **Lease curta demais para vídeos longos:** a lease é renovada durante o processamento, não dimensionada
  pela duração.

## Referências

- [Baseline arquitetural](../../context/architecture-baseline.md) — proteção de conteúdo sem DRM, G21, G22, camada anticorrupção.
- [Domínio Entrega de Mídia e Proteção](../../domains/entrega-de-midia-e-protecao/domain.md) — RN-M01, RN-M07, RN-M08, RF-M06.
- [ADR-0002](0002-plataforma-de-runtime-coolify.md) — compute no Coolify, AWS restrita a S3 + CloudFront.
- [TechSpec de origem](../../tasks/prd-ingestao-midia/techspec.md).
