# Cenário de renovação e fim do direito

Use somente uma cortesia de cenário no ambiente de desenvolvimento. A origem `courtesy`,
a escola e a concessão exatas limitam a alteração; nenhum outro direito do aluno deve
estar vigente para esse curso. O script não representa revogação do produto (CAP-009).

1. Valide a infraestrutura com `scripts/remote-infra.sh check`. Atualize somente os containers
   de aplicação com `scripts/apps.sh start --remote` após as migrations do PRD.
2. Entre como o aluno dessa cortesia, abra `/student/aulas/{lessonId}` e reproduza um vídeo
   com duração suficiente. Nas requisições de rede, observe a abertura e o primeiro segmento.
3. Com o vídeo tocando, execute
   `scripts/expire-playback-grant.sh <tenantId> <grantId> 30`. O script mostra a validade
   anterior e a validade de cenário. Escolha uma concessão descartável: o worker de
   expiração pode publicar o fato de término; não restaure a validade depois do cenário.
4. Continue assistindo. A próxima renovação (`POST …/renewals`, a partir de 210 segundos
   da abertura/renovação anterior) deve responder 403 `ACCESS_DENIED`, `reason=grant-ended`
   e `accessEndedAt`. O player para e mostra a data de término. O limite é cinco minutos
   após o fim do direito, acrescido apenas do trecho já carregado pelo navegador.
5. Em outro aluno/cortesia válida, assista vinte minutos: cada renovação conserva o
   `sessionId`, altera `expiresAt`/`segmentAccess`, e não busca uma nova playlist principal
   nem pausa o vídeo. Pause por mais de cinco minutos; ao reproduzir, deve abrir outra
   sessão e retomar a posição.

A validação automatizada desses comportamentos fica em `PlaybackRenewalTests` e
`playback-renewal.test.tsx`, com relógios controlados, decisão HTTP e player simulados.
O relatório da execução distingue essa prova das observações feitas no navegador real.
