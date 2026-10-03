# ADR-0014: Entrega de segmentos de vídeo por credencial opaca de sessão e porta de distribuição, com borda própria em desenvolvimento

## Status

Accepted

## Identidade e escopo

- Caminho: `docs/adr/0014-entrega-de-segmentos-por-credencial-opaca-e-porta-de-distribuicao.md`
- Domínios/componentes afetados: Entrega de Mídia e Proteção (`media`), distribuição (CloudFront em produção; borda nginx em desenvolvimento), `bff-student`, `student-spa`
- Origem histórica: `tasks/prd-reproducao-protegida`, `CAP-007` (decisões C-01, C-06, C-07 do conjunto de contratos)
- Substitui: Nenhuma. Complementa a [ADR-0006](0006-preparacao-de-video-e-custodia-de-chave.md) (a chave e a playlist guardada com marcador opaco) e a [ADR-0002](0002-plataforma-de-runtime-coolify.md) (S3 e CloudFront só para mídia)

## Data

2026-10-03

## Contexto

O baseline exige que nenhum objeto de vídeo seja público e que a mídia só saia por URL assinada de vida curta, emitida para uma Sessão de Reprodução individual (G21). A Versão de Reprodução é a mesma para todos os alunos e a marca d'água é composta no cliente, para preservar **um único objeto em cache na CDN** (G22, premissa de escala 3).

Uma aula tem centenas de segmentos de 6 segundos, em até três qualidades. Duas propriedades precisam valer ao mesmo tempo:

1. **A credencial dos segmentos expira com a sessão (5 minutos)** e a reprodução dura mais que isso. O player lê a playlist de um vídeo sob demanda uma vez; credencial embutida em cada endereço da playlist venceria depois de 5 minutos e o vídeo pararia.
2. **O endereço do segmento não pode variar por aluno ou por sessão**, senão cada aluno gera uma chave de cache própria e a taxa de acerto da CDN cai a zero, que é o custo que a visão registrou como risco (R10).

Em produção, a distribuição é CloudFront (ADR-0002), que aceita uma política assinada por **prefixo** (curinga), cujos parâmetros de assinatura trafegam na consulta. Em desenvolvimento só existe MinIO (S3 compatível), sem distribuição. O código de `media` tem apenas o adaptador S3; o domain doc prevê adaptadores S3 e CloudFront atrás de portas neutras (BA14).

## Decisão

1. **A playlist guardada e a playlist entregue ao player não levam credencial.** `media` reescreve em memória, a cada requisição, a playlist principal (para `variants/{quality}`) e as de qualidade (segmentos viram endereços absolutos da distribuição **sem credencial**, e a URI de chave guardada pelo marcador opaco `c4c-key:{videoId}` vira o endereço relativo `../key` do recurso da sessão). Nenhum objeto guardado contém endereço real de chave (ADR-0006, item 4).
2. **A credencial dos segmentos é uma cadeia de consulta opaca, única por sessão**, gerada por uma **porta de distribuição** neutra (`CreateSegmentAccess` para um prefixo de vídeo e um instante de validade), devolvida na abertura e **substituída a cada renovação**. O player acrescenta a cadeia vigente a cada pedido de segmento. Ela vale para o **prefixo inteiro do vídeo**, não para um segmento, e nunca passa de `expiresAt` da sessão.
3. **A cadeia não faz parte da chave de cache da distribuição.** O endereço do segmento, sem a cadeia, é o mesmo para todos os alunos. O adaptador de produção configura a distribuição para ignorar a consulta no cálculo da chave de cache; a taxa de acerto é um sinal obrigatório com alerta (G22).
4. **Dois adaptadores atrás da mesma porta**, escolhidos por configuração:
   - **CloudFront (produção e homologação):** política assinada por prefixo `{tenant}/{video}/hls/*`, com validade até `expiresAt`. A chave privada de assinatura é segredo operacional, nunca no repositório. A assinatura é local; nenhuma chamada à AWS é necessária para gerar a credencial.
   - **Borda nginx (desenvolvimento, CI e QA):** um container `media-edge`, na frente do MinIO, valida a credencial com `secure_link` (hash do prefixo do vídeo, da expiração e de um segredo compartilhado com `media`) e serve o objeto. Não chama `media`. Existe só onde não há CloudFront.
5. **O nome do provedor, o formato da assinatura e a topologia ficam dentro dos adaptadores.** Nenhum contrato, evento ou outro domínio os vê (BA14).
6. **Playlists e chave passam pelo `bff-student`** (G15) e são repassadas sem alteração; **os segmentos não passam pelo BFF** em nenhum ambiente.

## Alternativas Consideradas

### Alternativa 1: URL assinada por segmento, dentro da playlist

- **Descrição:** `media` assinaria cada endereço de segmento ao servir a playlist.
- **Prós:** nenhuma lógica de credencial no player.
- **Contras:** a assinatura vence em 5 minutos e a playlist VOD é lida uma vez; o player teria de recarregar a playlist a cada renovação, o que ele não faz para VOD. Centenas de assinaturas por requisição de playlist, e cada aluno com endereços distintos (sem cache compartilhado).
- **Por que rejeitada:** quebra a reprodução na primeira renovação e destrói o cache.

### Alternativa 2: Validade longa da credencial

- **Descrição:** assinar a playlist com validade de horas.
- **Contras:** um link copiado serve por horas; a revogação não pararia a reprodução em minutos.
- **Por que rejeitada:** derrota a vida curta (RN-M01) e a revogação em até 5 minutos (DP-01).

### Alternativa 3: Segmentos pelo BFF ou por `media`

- **Descrição:** a banda do vídeo passaria pelo BFF ou pela API de `media`.
- **Contras:** a CDN deixa de servir o vídeo; o custo e a latência vão para os serviços de negócio; contraria a premissa de escala 1 do baseline.
- **Por que rejeitada:** a distribuição existe para isso.

### Alternativa 4: Adaptador de proxy no BFF em desenvolvimento

- **Descrição:** em desenvolvimento, o BFF repassaria os segmentos vindos de `media`, com a sessão do aluno.
- **Prós:** nenhum container novo.
- **Contras:** cria uma operação pública só de desenvolvimento, e o caminho de segmento testado deixa de ser o de produção.
- **Por que rejeitada:** o responsável escolheu a borda própria, que mantém o contrato igual ao de produção.

### Alternativa 5: CloudFront também em desenvolvimento

- **Descrição:** uma distribuição real de teste.
- **Contras:** traz credencial e custo AWS ao ambiente de desenvolvimento, hoje apenas com MinIO.
- **Por que rejeitada:** custo e superfície sem ganho para o desenvolvimento diário.

## Consequências

### Positivas

- Um endereço de segmento por vídeo, igual para todos, preserva o cache da distribuição.
- A renovação troca a credencial sem recarregar a playlist nem interromper o vídeo.
- Revogar o acesso para a reprodução em até 5 minutos mais o trecho já carregado, sem estado por segmento.
- O contrato é o mesmo em todos os ambientes; só o adaptador muda.

### Negativas

- O player precisa reescrever o endereço de cada pedido de segmento com a cadeia vigente (comportamento do player, não do navegador nativo).
- Um container novo (`media-edge`) no compose local, no remoto e no Coolify de desenvolvimento, e um segredo compartilhado entre `media` e a borda.
- A borda de desenvolvimento usa MD5 na validação (limite do `secure_link` padrão do nginx): serve ao desenvolvimento e **não** deve ser usada fora dele.

### Riscos

- **Credencial de segmento aparecer em telemetria** (instrumentação de requisições do navegador registra URLs). Mitigação: o SPA registra a credencial como segredo e a remove de todo atributo de span antes da exportação.
- **A distribuição incluir a consulta na chave de cache e derrubar o acerto.** Mitigação: política de cache explícita sem consulta; alerta de taxa de acerto (G22); verificação em homologação, que é a primeira com CloudFront real. O efeito da consulta de assinatura na chave de cache **não foi confirmado em documentação nesta decisão** e é a primeira coisa a medir.
- **Navegador sem suporte ao player por segmentos reescritos** (por exemplo, Safari anterior ao suporte a MSE no iPhone). Mitigação: mensagem própria ao aluno; matriz de navegadores a validar em QA.
- **Escopo do curinga:** a credencial vale para todos os segmentos do vídeo daquela sessão, o que é o desejado, mas não para outro vídeo. Mitigação: teste de recusa para prefixo diferente.

## Notas de Implementação

- A porta e os adaptadores ficam em `media` (`Infra.Data/Adapters`, ao lado de `S3MediaStorageAdapter`), atrás de uma interface de `Application/Interfaces` no estilo de `IMediaStoragePort`.
- Configuração por ambiente: adaptador, base da distribuição, segredo da borda ou chave do CloudFront. Nenhum valor no repositório.
- A borda de desenvolvimento precisa de CORS para a origem do SPA do aluno e de repasse de `Range`.
- Os contratos estão em `tasks/prd-reproducao-protegida/api-contract.yaml` e `internal-api-contract-media.yaml` (campo `segmentAccess`, recurso `variants/{quality}`).

## Referências

- [ADR-0002](0002-plataforma-de-runtime-coolify.md), [ADR-0006](0006-preparacao-de-video-e-custodia-de-chave.md), [ADR-0013](0013-jwt-de-aluno-validado-por-servicos-de-dominio.md).
- [Baseline arquitetural](../../context/architecture-baseline.md) — G15, G21, G22, "Proteção de conteúdo" e "Premissas de Escalabilidade".
- [Domínio Entrega de Mídia e Proteção](../../domains/entrega-de-midia-e-protecao/domain.md) — RN-M01, RN-M07, RN-M08, RN-M12, RF-M03.
- Documentação consultada: [CloudFront, URL assinada com política personalizada](https://docs.aws.amazon.com/AmazonCloudFront/latest/DeveloperGuide/private-content-creating-signed-url-custom-policy.html) (parâmetros `Policy`, `Signature`, `Key-Pair-Id`); [hls.js, API de personalização de requisições](https://github.com/video-dev/hls.js/blob/master/docs/API.md) (`xhrSetup(xhr, url)`).
- Origem histórica: `tasks/prd-reproducao-protegida/contracts.md` (C-01, C-06, C-07).
