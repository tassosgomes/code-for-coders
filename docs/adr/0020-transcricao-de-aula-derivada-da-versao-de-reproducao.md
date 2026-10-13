# ADR-0020: Transcrição de aula derivada da Versão de Reprodução, com serviço externo assíncrono, aviso como gatilho e texto sob guarda própria

## Status

Accepted

## Identidade e escopo

- Caminho: `docs/adr/0020-transcricao-de-aula-derivada-da-versao-de-reproducao.md`
- Domínios/componentes afetados: Entrega de Mídia e Proteção (`media` — API e worker), `bff-admin`, `bff-student`, `admin-spa`, `student-spa`; serviço de transcrição próprio (repositório `tassosgomes/whisper-api`, dependência externa)
- Origem histórica: `tasks/archive/prd-transcricao-legenda-aula`, CAP-032 (TechSpec de 2026-10-12)
- Substitui: Nenhuma. Conforma e estende a [ADR-0006](0006-preparacao-de-video-e-custodia-de-chave.md) (custódia da chave); complementa as [ADR-0014](0014-entrega-de-segmentos-por-credencial-opaca-e-porta-de-distribuicao.md) e [ADR-0019](0019-runtime-e-deploy-atual.md)

## Data

2026-10-12

## Contexto

A capacidade CAP-032 transcreve a fala de cada vídeo pronto e entrega o texto ao aluno (legenda e transcrição pesquisável). Quatro forças definem a arquitetura e valem para qualquer produto derivado do vídeo que venha depois (capítulos, miniaturas, resumo):

1. **O original não existe depois de pronto.** A preparação apaga o arquivo enviado ao terminar (`PrepareVideo`, exclusão do original após `MarkReady`). O que resta é a Versão de Reprodução: HLS em três qualidades, cifrada com AES-128, cuja chave só o `media` conhece (ADR-0006). Vídeos prontos antes da capacidade, o disparo automático e o "refazer" precisam do **mesmo** caminho de obtenção da fala.
2. **O serviço de transcrição é externo e assíncrono.** Serviço próprio na rede privada (decisão de produto DP-01, o conteúdo não sai para terceiro). Aceita áudio por URL assinada de leitura, responde com um job, avisa o término por webhook assinado (Standard Webhooks v1, reenvio por até 72 h), retém o resultado por 24 h e tem SLO de até 24 h (p95) para o estado terminal. O contrato dele é de outro repositório.
3. **A Transcrição nunca pode atrasar nem alterar a Mídia** (RN-M19) e é Ativo Protegido derivado: só quem passa pela verificação de reprodução a recebe (RN-M21), igual para todos, sem dado do aluno (RN-M22).
4. **O ambiente não garante que o serviço alcance o `media`.** Em desenvolvimento remoto o `media` roda na máquina do desenvolvedor; o aviso por webhook pode não chegar.

## Decisão

1. **O áudio é extraído pelo worker do `media` a partir da Versão de Reprodução**, nunca do original. O worker lê a variante de menor `BANDWIDTH` do `master.m3u8`, baixa a playlist e os segmentos para o diretório de trabalho temporário, desfaz o marcador de chave opaco apontando-o para um arquivo local com a chave (obtida do banco por `IVideoKeyProtector`, a mesma custódia da ADR-0006), e o ffmpeg extrai áudio mono 16 kHz em AAC (`.m4a`). A chave existe só nesse diretório (permissão de dono, apagada com ele ao fim, com sucesso ou falha) e nunca em objeto, mensagem, log, span ou resposta. Um único caminho serve ao automático, ao refazer e ao vídeo antigo.
2. **O áudio vive em prefixo privado fora de `hls/`**: `{tenantId}/{videoId}/transcription/{transcriptionId}/audio.m4a`. A política de distribuição por prefixo da ADR-0014 (`hls/*`) não o alcança. O `media` entrega ao serviço uma URL pré-assinada de leitura (GET) de validade de 90 minutos, gerada em memória no momento do envio e **nunca persistida nem registrada**. O objeto é apagado assim que o serviço passa do estado `downloading` (ou o término, o que vier primeiro); uma varredura apaga o que sobrar.
3. **A Transcrição é uma entidade própria no banco do `media`, e ela é a fila.** Não é um estado do vídeo (RN-M19 proíbe tocar o estado da Mídia). Fila com concessão (lease) e `FOR UPDATE SKIP LOCKED`, no mesmo padrão da preparação (ADR-0006, decisão 2). Uma só Transcrição em andamento por vídeo, garantida por índice único parcial; uma só vigente por vídeo, também. O disparo automático é uma linha inserida **na mesma transação** que marca o vídeo pronto, sem chamada remota.
4. **O aviso por webhook é só gatilho; quem decide é a consulta.** O receptor verifica a assinatura, registra o evento (dedupe por `webhook-id`) e marca a tentativa para o worker olhar já. O worker consulta o serviço, e só um estado `completed` consultado vale. **Conciliação por consulta periódica** cobre o aviso perdido e o ambiente sem receptor alcançável; ela é o caminho obrigatório, o aviso é a otimização.
5. **O texto pronto é copiado para guarda própria antes das 24 h do serviço**, em tabela de conteúdo no banco do `media` (`jsonb` com os trechos, aparados), e servido dali ao professor, à página da aula e à legenda (WebVTT gerado na hora a partir dos trechos). Sem cache compartilhado, sem objeto público, sem endereço estável.
6. **O serviço fica atrás de camada anticorrupção** no `media`: o resto do sistema só conhece os cinco motivos de falha do produto e o formato interno dos trechos. Códigos, estados e `schemaVersion` do serviço não atravessam essa fronteira.

## Alternativas Consideradas

### Alternativa 1: extrair o áudio na preparação, enquanto o original existe

- **Descrição:** o `PrepareVideo` produziria também um `.m4a` e o guardaria.
- **Prós:** sem decifrar HLS; áudio perfeito.
- **Contras:** não serve a vídeo já pronto, nem a "refazer" (dois caminhos); aumenta a duração e o risco do caminho crítico de "pronto" (RN-M19); guarda um artefato a mais por vídeo para sempre.
- **Por que rejeitada:** exige um segundo caminho para vídeos antigos e acopla a preparação à transcrição.

### Alternativa 2: guardar o original por mais tempo

- **Descrição:** adiar a exclusão do original até a transcrição terminar.
- **Contras:** reverte a decisão de custo de armazenamento da preparação; não ajuda vídeos antigos (originais já apagados).
- **Por que rejeitada:** não atende RN-M24 (toda mídia pronta é transcritível).

### Alternativa 3: o serviço de transcrição baixar o HLS ou o vídeo direto

- **Descrição:** entregar a URL da playlist ou dos segmentos.
- **Contras:** o serviço teria de receber a chave AES, ou o vídeo teria de sair cifrado e inútil; abriria a chave a outro sistema, contra a ADR-0006.
- **Por que rejeitada:** a chave não sai do `media`.

### Alternativa 4: texto no armazenamento de objetos

- **Descrição:** guardar o resultado como objeto JSON privado.
- **Prós:** nada no banco.
- **Contras:** uma leitura de S3 por requisição do aluno no caminho de página; mais um local de persistência de conteúdo protegido para auditar; atualização atômica com o estado (refazer) exige coordenação.
- **Por que rejeitada:** o banco já dá atomicidade com o estado e isolamento por escola; o tamanho (centenas de KB por aula de 2 horas, estimado) cabe em `jsonb`.

### Alternativa 5: depender só do webhook

- **Contras:** ambientes onde o serviço não alcança o `media` ficariam sem resultado; aviso perdido deixaria a transcrição presa até perder o resultado (24 h).
- **Por que rejeitada:** o PRD exige conciliação (RF-07).

## Consequências

### Positivas

- Um caminho único e reproduzível para qualquer vídeo pronto, novo ou antigo.
- O serviço de transcrição pode cair, atrasar ou ser trocado sem tocar a Mídia, o player ou a proteção.
- A chave AES continua em um único componente, com o mesmo regime de descarte.
- Produtos derivados futuros reutilizam o padrão (leitura da Versão de Reprodução pelo worker, entidade-fila própria, serviço atrás de camada anticorrupção).

### Negativas

- O worker baixa os segmentos da menor qualidade (na ordem de 1 GiB para 2 horas a 480p, estimado) e usa disco temporário e CPU além da preparação.
- O texto completo da aula fica no banco do `media`; backup e retenção do banco passam a cobri-lo.
- Há dois estados de "em andamento" para o professor reconciliar com o estado do serviço (consulta periódica).
- A URL de leitura do áudio é uma credencial temporária que, em rede privada com HTTP (ambientes de desenvolvimento), trafega sem TLS até o serviço; a janela é encurtada ao apagar o objeto logo que o serviço o baixa.

### Riscos

- **Consumo de disco e CPU competindo com a preparação.** Mitigação: orçamento de disco antes de reivindicar, concorrência 1, extração limitada a 1 thread.
- **Resultado perdido por expiração (24 h).** Mitigação: aviso mais consulta periódica; resultado expirado volta à fila como falha transitória dentro do limite de envios.
- **Mudança de `schemaVersion` do serviço.** Mitigação: versão desconhecida é falha tratada, não interpretação adivinhada.

## Notas de Implementação

- Estados, tabelas, política de envio e nova tentativa, limites e sinais ficam na TechSpec `tasks/archive/prd-transcricao-legenda-aula/techspec.md`; esta ADR registra só a decisão durável.
- Evidência de viabilidade (2026-10-12): com ffmpeg 9.0.1, HLS AES-128 de 480p com marcador de chave `c4c-key:{videoId}` reescrito para um arquivo local foi decifrado e extraído em `.m4a` mono 16 kHz AAC (cerca de 6 KB/s, ou ~43 MB para 2 h). A imagem do `media` usa o ffmpeg do Debian; a primeira task confirma o mesmo comando na imagem real.
- Configuração operacional do serviço (listas de destinos privados e credenciais por ambiente) fica na TechSpec; não é decisão arquitetural.

## Referências

- [ADR-0006](0006-preparacao-de-video-e-custodia-de-chave.md), [ADR-0014](0014-entrega-de-segmentos-por-credencial-opaca-e-porta-de-distribuicao.md), [ADR-0019](0019-runtime-e-deploy-atual.md).
- [Baseline arquitetural](../../context/architecture-baseline.md): camada anticorrupção, webhook por endpoint dedicado e idempotente, G21, G22, G23.
- [Domínio Entrega de Mídia e Proteção](../../domains/entrega-de-midia-e-protecao/domain.md): RN-M18 a RN-M27.
- Contrato do serviço de transcrição: repositório `tassosgomes/whisper-api`, `tasks/prd-api-transcricao-assincrona/api-contract.yaml` 1.0.0 e ADR-003 dele (destinos privados).
- Origem histórica: `tasks/archive/prd-transcricao-legenda-aula`.
