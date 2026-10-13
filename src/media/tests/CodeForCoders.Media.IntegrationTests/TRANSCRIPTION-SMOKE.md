# Smoke de transcrição com o serviço real

Este smoke usa `https://whisper.lab.tasso.dev.br/v1`, ffmpeg/ffprobe reais,
PostgreSQL e MinIO remotos. Não sobe containers. Prepara um MP4 com fala pela
implementação `PrepareVideo`, extrai o áudio do HLS criptografado e chama
`TranscriptionServiceClient` com a mesma política de timeout/retry do worker.
Conduz o workflow por consulta até `ready`, lê o conteúdo persistido pelo caso
de uso do professor e verifica a remoção do áudio **antes** da limpeza do teste.

## Preparação pelo operador

1. Confirmar `TRUSTED_PRIVATE_SOURCES=http://192.168.0.5:9000` no deploy do
   Whisper de desenvolvimento (estável: `http://192.168.0.11:9000`). A confirmação
   precisa ser do deploy, não apenas do arquivo de configuração deste projeto.
2. Disponibilizar uma conta do ambiente e sua `WHISPER_API_KEY`. Não registrar
   a chave, URLs assinadas ou transcrições em evidências.
3. Provisionar um banco PostgreSQL **exclusivo**, vazio, cujo nome termina em
   `_transcription_smoke`, com um usuário autorizado a aplicar migrations EF.
   Nenhuma API ou worker da aplicação pode apontar para ele. O teste aplica as
   migrations existentes com EF Core; não cria nem remove o banco. Não usar
   `code_for_coders_media`: os claims dos workers atravessam tenants. Não rodar
   dois smokes simultaneamente no mesmo banco.
4. Preparar um MP4 curto, até 60 segundos e 20 MiB, com fala clara e imagem de
   pelo menos 480p. Use material de teste autorizado. Áudio senoidal ou silêncio
   não valida texto legível. Instalar ffmpeg e ffprobe no executor.
5. Usar o bucket provisionado `code-for-coders-media` e credenciais do usuário
   S3 desse projeto, com leitura, escrita, listagem e remoção. O endpoint deve
   ser alcançável tanto pelo executor quanto pelo Whisper.

O smoke usa um tenant e um vídeo novos e só remove objetos e registros desta
execução. A limpeza também ocorre após falha ou timeout; uma falha de limpeza
faz o teste falhar. O prefixo verificado é
`media/{tenantId}/{videoId}/transcription/`, sem exigir que outras aulas do
bucket deixem de transcrever. Não há `TRUNCATE`, remoção de bucket ou limpeza
global. O original e o HLS da execução são removidos ao final.

## Variáveis obrigatórias

| Variável | Valor / origem |
| --- | --- |
| `TRANSCRIPTION_SMOKE_CONNECTION_STRING` | Conexão secreta ao banco exclusivo terminado em `_transcription_smoke` |
| `TRANSCRIPTION_SMOKE_VIDEO_PATH` | Caminho absoluto do MP4 com fala |
| `WHISPER_API_KEY` | Chave da conta de desenvolvimento no Whisper |
| `TRANSCRIPTION_SMOKE_S3_ENDPOINT` | `http://192.168.0.5:9000` no dev; origem confirmada pelo operador |
| `TRANSCRIPTION_SMOKE_S3_ACCESS_KEY` | Usuário S3 provisionado (`code_for_coders_media`) |
| `TRANSCRIPTION_SMOKE_S3_SECRET_KEY` | Segredo S3 (`REMOTE_S3_SECRET_KEY` do `.env` provisionado) |
| `TRANSCRIPTION_SMOKE_PRIVATE_SOURCES_CONFIRMED` | `true` somente depois da confirmação operacional da lista no Whisper |

`TRANSCRIPTION_SMOKE_TIMEOUT_MINUTES` é opcional: padrão 30, intervalo 1–1800.
A fila real tem SLO p95 de 24 horas; exceder o prazo local não comprova uma
regressão. Ajustar o prazo antes da execução quando necessário. A consulta é
agendada a cada 15 segundos, sem forçar datas ou estados no banco.

O teste não carrega `.env` automaticamente. Exporte as variáveis por um
gerenciador de segredos ou pelo ambiente do executor, sem `set -x`. A chave
Whisper e o segredo S3 do checkout principal podem ser reutilizados no dev;
isso não provisiona o banco exclusivo nem confirma as listas externas.

## Execução explícita

```bash
dotnet test --project src/media/tests/CodeForCoders.Media.IntegrationTests/CodeForCoders.Media.IntegrationTests.csproj -- --filter-class CodeForCoders.Media.IntegrationTests.TranscriptionRealServiceSmokeTests --minimum-expected-tests 1
```

Com a regra RTK deste repositório, prefixar o comando com `rtk proxy`.
O atributo `TranscriptionRealServiceSmokeFact` mantém o teste explícito na
execução agregada e o habilita somente para o filtro exato de classe acima.
Essa ponte é necessária porque o runner MTP não habilita `[Fact(Explicit=true)]`
automaticamente para filtros de classe. Seleções amplas de classe e filtros
por trait mantêm o teste fora da execução normal. Sem qualquer variável
obrigatória, a seleção explícita **falha**, antes de conectar à infraestrutura;
não retorna sucesso nem skip.

Um resultado aprovado deve mostrar um teste executado e aprovado, com exit 0.
Ausência de variáveis, erro de configuração, falha, timeout ou zero testes
executados não são aprovação. O smoke não emite o texto nem detalhes de
exceções externas; preserve essa proteção ao investigar.

## Checkpoint da aplicação em dev

Separadamente do banco exclusivo do teste, preparar o ambiente da aplicação:

```bash
scripts/remote-infra.sh provision
scripts/remote-infra.sh migrate
scripts/remote-infra.sh check
TRANSCRIPTION_ENABLED=true scripts/apps.sh start --remote
```

O override remoto já configura `AwsMedia__EndpointTranscription` com a origem
remota e passa `WHISPER_API_KEY` ao worker. Em
`http://localhost:8081/admin/videos`, enviar uma aula curta com fala, aguardar
a preparação/transcrição e conferir `transcrevendo` → `pronta`. Ler o texto no
painel do professor, abrir a mesma aula como aluno e conferir legenda, busca
e salto no painel. Conferir o prefixo `transcription/` daquela aula no S3.
Este checkpoint de UI não é automatizado pelo teste de integração.

O teste fecha por consulta e não cadastra receptor de webhook. Quando houver
um receptor alcançável, o operador também deve confirmar
`TRUSTED_PRIVATE_WEBHOOKS`, cadastrar o destino
`/webhooks/v1/transcription-events` e entregar o segredo a
`TRANSCRIPTION_WEBHOOK_SECRET`. Nesse checkpoint, conferir que o aviso antecipa
o estado pronto em relação à cadência de consulta. Sem receptor alcançável,
registrar explicitamente a validação por consulta, sem alegar entrega de aviso.

Com o primeiro resultado real, registrar duração/tamanho do download (sem
payload) para reavaliar o `AttemptTimeout` atual de 5 segundos, em especial
para resultados próximos do limite de 8 MiB. Este smoke não altera esse
timeout sem evidência.
