# Projeção de vídeos para Autoria

`COURSE_AUTHORING_ENABLED` começa em `false` nos dois Compose. O BFF só registra as rotas de
curso com `CourseAuthoring:Enabled=true`. Não habilitar antes de reconciliar **todos** os tenants
atendidos. Os comandos operacionais abaixo são pontuais: encerram com exit 0 no sucesso e
exit diferente de zero na falha; não iniciam outro consumidor nem o publisher periódico.

1. Aplicar a migration EF `ProjectVideoAvailability` de Learning como etapa de deploy:

   ```bash
   dotnet ef database update --project src/learning/src/CodeForCoders.Learning.Infra.Data --startup-project src/learning/src/CodeForCoders.Learning.Api
   ```

2. Iniciar Learning com suas credenciais e verificar a fila quorum durável
   `learning.video-availability`, sua DLQ e os bindings `midia.ativo-pronto.v1` e
   `midia.preparacao-falhou.v1` em `media.events`. A fila não tem TTL nem descarte por tamanho.
   Em infra compartilhada, configurar `RabbitMq:VideoFactsQueue` com namespace isolado e
   `Outbox:VideoFactReplayQueue` com o mesmo nome. Nunca disputar uma fila entre ambientes.

3. Com a configuração **de Media** (seu próprio banco, broker e credenciais; papel `api`),
   reproduzir os fatos retidos de cada tenant:

   ```bash
   dotnet run --project src/media/src/CodeForCoders.Media.Api -- --Outbox:VideoFactReplayTenantId=UUID_DO_TENANT
   ```

   O worker verifica que a fila de Learning já existe e publica os registros históricos em
   lotes, com confirms, `mandatory`, `MessageId`, routing key e payload originais. Não redefine
   `processed_on`/tentativas, não gera fatos novos e não usa a fila temporária de Auditoria.
   A reentrega é deduplicada atomicamente em Learning. Também reproduz falhas históricas.

4. Aguardar o esvaziamento da fila e confirmar DLQ vazia/consumidor ativo. Obter um token
   curto de Media com `midia.enviar` para o tenant. Usar a configuração **de Learning** para
   seu próprio banco e executar:

   ```bash
   export MEDIA_ACCESS_TOKEN # fornecer o valor pelo ambiente; não salvar no repositório
   ./scripts/reconcile-course-videos.sh UUID_DO_TENANT http://media:8080
   ```

   O script verifica o tenant do token, pagina `listVideos?status=ready`, confirma contagem e
   ausência de duplicados e fornece a lista à operação de Learning
   `--VideoProjection:ReconcileManifest=/caminho/manifest.json`. Esta compara **exatamente** os
   IDs ready do tenant com sua projeção local. Nenhum serviço lê o banco de outro. Pode-se
   preservar o manifesto com `RECONCILE_MANIFEST_OUTPUT`; guardar também os exits/logs por tenant.
   Se houver preparação concorrente, repetir após o consumo; snapshot divergente sempre falha.

5. Somente após todos os tenants passarem, definir `COURSE_AUTHORING_ENABLED=true` e recriar
   o BFF. Falha ou falta de histórico mantém a área desligada e requer solução com Mídia.
   Não sintetizar fatos nem liberar um tenant com IDs faltantes. Desabilitar a flag é o rollback
   de exposição; preservar os dados e a migration.

## Ativação no desenvolvimento com infraestrutura remota

O PRD concluído indica que a implementação foi validada; a ativação é uma etapa por ambiente.
A SPA pode mostrar Autoria enquanto a flag está desligada. Nesse caso, uma sessão autenticada
recebe `404` vazio em `/api/v1/courses`, pois o BFF não registra essas rotas.

Após cumprir os passos acima e os pré-requisitos de
[`course-publication-rollout.md`](course-publication-rollout.md), configure
`COURSE_AUTHORING_ENABLED=true` no `.env` da raiz. Preserve as demais configurações. A variável
no `.env` mantém a ativação nas próximas execuções de `scripts/apps.sh start --remote`.

O worker de Mídia deve estar ativo para consumir publicações. No modo remoto, inicie-o
explicitamente e recrie o BFF para carregar a flag:

```bash
docker compose --file docker-compose.yml --file docker-compose.remote.yml \
  up --detach --no-deps --wait media-worker
docker compose --file docker-compose.yml --file docker-compose.remote.yml \
  up --detach --no-deps --force-recreate --wait bff-admin
```

Confirme `CourseAuthoring__Enabled=true` no container e abra
`http://localhost:8081/admin/autoria` com uma conta com `autoria.ler`. A chamada
`GET /api/v1/courses?_page=1&_size=20` deve retornar `200` com `data` e `pagination`.
Uma chamada sem sessão retorna `401` tanto antes quanto depois da ativação; portanto, esse
teste sozinho não comprova que a rota foi registrada. `/health/ready` também pode estar
saudável com a autoria desligada.

Monitorar `learning.video_facts.consumed`, `learning.video_facts.lag` e
`learning.video_facts.dead_lettered` pelo OTLP. `/health/ready` fica unhealthy se a DLQ contém
mensagens ou não há consumidor. Alertar nessas condições e no crescimento do lag antes de
liberar. Recuperar incidentes por replay dos registros originais e reconciliar novamente.

O vínculo usa exclusivamente a visão local. Learning não guarda título/duração. `getCourse`
consulta os IDs distintos em Media com no máximo quatro requisições simultâneas e orçamento
total de três segundos (inclui token); em falha mantém HTTP 200 e `videoId`. O seletor consulta
somente `ready`, exibe autor/duração, preserva a aula durante erro/retry e não oferece player.

Smoke: em ambiente isolado, preparar ready/preparing/failed em dois tenants; conferir seleção
de colega, recusa cross-tenant, troca/desvínculo com lessonId estável, replay histórico,
reentrega, atraso de projeção e leitura com Media parada. Não habilitar o ambiente compartilhado
com base apenas nos testes locais: executar a reconciliação dos dados reais dele.
