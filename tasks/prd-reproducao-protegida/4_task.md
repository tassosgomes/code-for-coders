---
status: done
task_kind: vertical
blocked_by: ["3.0"]
gate: 'dotnet test --project src/identity/tests/CodeForCoders.Identity.UnitTests/CodeForCoders.Identity.UnitTests.csproj -- --filter-class CodeForCoders.Identity.UnitTests.StudentTokenEmailClaimTests --minimum-expected-tests 4 && dotnet test --project src/identity/tests/CodeForCoders.Identity.IntegrationTests/CodeForCoders.Identity.IntegrationTests.csproj -- --filter-class CodeForCoders.Identity.IntegrationTests.StudentSessionEmailTests --minimum-expected-tests 2 && dotnet test --project src/commerce/tests/CodeForCoders.Commerce.IntegrationTests/CodeForCoders.Commerce.IntegrationTests.csproj -- --filter-class CodeForCoders.Commerce.IntegrationTests.AccessDecisionIssuersTests --minimum-expected-tests 5 && dotnet test --project src/media/tests/CodeForCoders.Media.UnitTests/CodeForCoders.Media.UnitTests.csproj -- --filter-class CodeForCoders.Media.UnitTests.PlaylistRewriterTests --minimum-expected-tests 6 && dotnet test --project src/media/tests/CodeForCoders.Media.UnitTests/CodeForCoders.Media.UnitTests.csproj -- --filter-class CodeForCoders.Media.UnitTests.DeliveryCredentialTests --minimum-expected-tests 6 && dotnet test --project src/media/tests/CodeForCoders.Media.IntegrationTests/CodeForCoders.Media.IntegrationTests.csproj -- --filter-class CodeForCoders.Media.IntegrationTests.PlaybackSessionOpenTests --minimum-expected-tests 9 && dotnet test --project src/media/tests/CodeForCoders.Media.IntegrationTests/CodeForCoders.Media.IntegrationTests.csproj -- --filter-class CodeForCoders.Media.IntegrationTests.PlaybackDeliveryTests --minimum-expected-tests 11 && dotnet test --project src/bff-student/tests/CodeForCoders.BffStudent.IntegrationTests/CodeForCoders.BffStudent.IntegrationTests.csproj -- --filter-class CodeForCoders.BffStudent.IntegrationTests.PlaybackProxyTests --minimum-expected-tests 9 && npm --prefix src/student-spa run test -- playback-open && npm --prefix src/student-spa run test -- telemetry-url-redaction'
gate_expect: "Pelo menos 70 testes passam: 4 e 2 de identity, 5 de commerce, 12 unitários e 20 de integração de media, 9 do BFF, 18 do SPA"
---

# 4.0 O aluno assiste à aula, com marca d'água e sem link solto

**Fatia:** V-02 · **Cobre:** RF-01 (abertura: vídeo não pronto, sem e-mail), RF-02 (abrir sessão, várias simultâneas), RF-03, RF-04, RF-05 (tocar, pausar, avançar e voltar, volume, tela cheia, teclado, qualidade automática), RN-R03, RN-R04, RN-M01, RN-M05, RN-M07, RN-M08, RN-M10, RN-M11, RN-M12, RN-M13, RN-M15, US de assistir e de link copiado · **Spec:** `techspec.md#v-02` · **ADR:** [ADR-0003](../../docs/adr/0003-verificacao-de-sessao-do-aluno.md), [ADR-0006](../../docs/adr/0006-preparacao-de-video-e-custodia-de-chave.md), [ADR-0012](../../docs/adr/0012-autenticacao-de-servico-media-learning-em-commerce-revisada.md), [ADR-0013](../../docs/adr/0013-jwt-de-aluno-validado-por-servicos-de-dominio.md), [ADR-0014](../../docs/adr/0014-entrega-de-segmentos-por-credencial-opaca-e-porta-de-distribuicao.md)

## Comportamento

O aluno com direito vê o vídeo da aula tocar, com o **e-mail dele sobre o vídeo**, e ninguém sem sessão válida daquele vídeo obtém
playlist, chave ou segmento. Operações em `api-contract.yaml` e `internal-api-contract-media.yaml`: `openPlaybackSession`,
`getPlaybackPlaylist`, `getPlaybackVariantPlaylist`, `getPlaybackKey` (e as `Internal`).

- **`identity`:** a audiência `media` é configurada com o escopo `playback:use`; o JWT de `media` carrega a claim `email`
  **somente** nessa audiência (`StudentSessionTokens:EmailAudiences`); `learning` e qualquer outra audiência **não** a têm. O e-mail
  vem do mesmo `select` da renovação da sessão (campo novo em `StudentSessionDetails`), sem consulta adicional.
- **`commerce`:** o emissor `media` entra na configuração de emissores, ao lado de `learning`.
- **`media` — abertura (RN-M10), nesta ordem:** (1) Referência de Uso por (escola, `lessonId`) → curso e vídeo; ausente → 404
  `LESSON_NOT_AVAILABLE` **sem consultar Matrícula**; (2) vídeo `ready`, senão 409 `MEDIA_NOT_READY`; (3) decisão de acesso do
  aluno (`sub`) sobre o curso, com a asserção de `media` (timeout de 2 s, cache em memória de 30 s, mesmo regime de 3.0): negada 403
  `ACCESS_DENIED`, indisponível 503 `ACCESS_DECISION_UNAVAILABLE`; (4) e-mail presente na claim, senão 422 `WATERMARK_UNAVAILABLE`.
  Grava `playback_sessions` (sem coluna de e-mail), com `expiresAt` = agora + 5 min e `renewAfter` = `expiresAt` − 90 s, e devolve
  `PlaybackSession` com a política (`watermark`, `progress`) e a `segmentAccess`. Cada chamada abre uma sessão nova; **vários
  dispositivos e abas do mesmo aluno funcionam** (RN-M13).
- **`media` — entrega:** playlist principal, variantes e chave são recursos da sessão e **só servem sessão que existe, não venceu
  e é do aluno do JWT** (outro aluno, vencida, inexistente → 404/410). As playlists guardadas no S3 são reescritas em memória por
  requisição: variantes viram `variants/{quality}`; segmentos viram endereços absolutos da distribuição **sem credencial**; o
  marcador `c4c-key:{videoId}` vira `../key`. A chave é decifrada na requisição, devolvida com `Cache-Control: no-store` e nunca
  guardada em claro. As playlists originais ficam em cache de memória (imutáveis por vídeo).
- **`media` — porta de distribuição (ADR-0014):** gera a base e a `segmentAccess.query` opaca, por prefixo `{tenant}/{video}/hls/`
  e validade até `expiresAt`. Adaptador **CloudFront** (política por prefixo, assinatura local com a chave privada de configuração)
  e adaptador **borda de desenvolvimento** (`secure_link`: hash do prefixo do vídeo, da expiração e do segredo compartilhado).
  Escolha por `Playback:Delivery:Adapter`. Nome e formato do provedor ficam dentro dos adaptadores.
- **Índice e migrations (EF):** `playback_sessions` e o índice `(tenant_id, lesson_id)` em `course_video_references`.
- **Borda `media-edge` (desenvolvimento):** container nginx na frente do MinIO, nos Compose local, remoto e Coolify de dev, com
  `secure_link` e o segredo compartilhado com `media`, CORS para a origem do SPA, repasse de `Range` e **log de acesso sem a
  consulta**. A primeira checagem da task é que a imagem escolhida traz `secure_link`; se não trouxer, trocar a imagem, sem mudar
  contrato.
- **`bff-student`:** rotas `openPlaybackSession` (com `X-CSRF-Token`) e as três de entrega, com a audiência `media`; playlists e chave
  são repassadas **sem alteração do corpo**, com o tipo de conteúdo e `Cache-Control: no-store` de `media`; erros de `media` mapeados
  conforme `api-contract.yaml`; timeouts de 5 s.
- **`student-spa`:** o player com **hls.js** (dependência nova), na tela de 3.0. Playlists e chave pelo BFF na mesma origem;
  **segmentos** reescritos por `xhrSetup(xhr, url)` com a `segmentAccess.query` vigente. **Marca d'água** sobre o **contêiner** do
  player (a tela cheia é pedida ao contêiner), `pointer-events: none`, fora da ordem de leitura, em zonas que não tocam os
  controles, trocando a cada `watermark.repositionSeconds` sem repetir a posição anterior, visível também pausado. **Controles
  próprios** (tocar, pausar, linha do tempo, volume, tela cheia), por teclado, com nome acessível e foco visível; qualidade
  automática. Aviso de uso pessoal sob o player. Estados: vídeo indisponível, não foi possível iniciar, navegador sem suporte. O
  e-mail existe só no estado do componente. A `segmentAccess.query` é registrada como **segredo de telemetria** e removida de todo
  atributo de span antes da exportação.

Casos negativos que a task prova: aula usando o vídeo de **outra** aula → 404 e **nenhuma** consulta a `commerce`; sem concessão →
403; Matrícula indisponível → 503 e nenhuma sessão criada; vídeo não pronto → 409; JWT sem `email` → 422 e o player não inicia;
playlist, variante e chave sem sessão, com sessão vencida, de outro aluno ou de outro vídeo → recusadas; credencial de segmento para
outro prefixo e depois de `expiresAt` → recusada; **nenhum objeto acessível sem credencial**; token de ator em rota de aluno e o
inverso → recusados.

## Fora do escopo desta task

- Renovação da sessão e parada por direito encerrado → 5.0. Trocar de aula, velocidade → 6.0. Avanço, fato e retenção → 7.0.
  Sinais e painel → 8.0.
- A sessão aqui vale 5 minutos mas **não renova**; ao vencer, a reprodução depende de 5.0. A abertura sozinha é o escopo desta task.
- A velocidade de reprodução é de 6.0. Recarregar a posição ao reabrir a aula não existe (DP-06).

## Decisões fechadas

- Sessão em Postgres de `media`, sem e-mail (D-01); a limpeza de linhas 24 h depois de `expires_at` é rotina do papel `worker`.
- Decisão em memória, 30 s, nunca indisponível; sem cache de JWT nem de asserção (D-02, D-12).
- hls.js, controles próprios e tela cheia no contêiner (D-05, D-06); playlists reescritas por requisição, originais em cache (D-04).
- `secure_link` na borda de desenvolvimento, MD5 só em desenvolvimento (D-10); política cliente-servidor vem na resposta (D-11).
- A chave e a `segmentAccess.query` nunca em log, span, métrica, mensagem ou armazenamento do navegador (`techspec.md`, percursos).
- O e-mail só na claim da audiência `media`, em `watermark.text` e no estado do componente; nunca em banco, outbox, cache, log,
  span, métrica, endereço ou nome de objeto (ADR-0013).
- Nenhuma mensagem ao aluno diz "protegido contra cópia" (RN-M15).

## Modificar / Referenciar

Arquivos a criar não são listados: a estrutura vem das skills `dotnet` e `react`.

- **modificar:** `src/identity/src/CodeForCoders.Identity.Api/Security/StudentSessionTokenIssuer.cs`, `StudentSessionTokenOptions.cs`; `Application/Common/StudentSessionDetails.cs`; `Application/UseCases/Accounts/ValidateStudentSession/ValidateStudentSession.cs`; `Infra.Data/Accounts/IdentitySessionStore.cs` (e-mail no mesmo `select`); `Api/Endpoints/StudentSessionEndpoints.cs`
- **modificar:** `src/commerce/src/CodeForCoders.Commerce.Api/appsettings.json` e os `docker-compose*.yml` (emissor `media`)
- **modificar:** `src/media/src/CodeForCoders.Media.Api/Security/MediaJwtBearerOptionsSetup.cs`, `Extensions/ServiceConfigurationExtensions.cs`, `Extensions/EndpointExtensions.cs`, `appsettings.json`; `Infra.Data/MediaDbContext.cs`, `CourseReferences/CourseVideoReferenceConfiguration.cs`, `DependencyInjection.cs`, `Adapters/S3MediaStorageAdapter.cs` (leitura de playlist e chave; adaptadores de distribuição)
- **modificar:** `src/bff-student/src/CodeForCoders.BffStudent.Api/Security/BffSecurityMiddleware.cs`, `Extensions/EndpointExtensions.cs`, `appsettings.json` (rotas `media`, repasse sem alteração de corpo)
- **modificar:** `src/student-spa/package.json`, `src/lib/telemetry-url-redaction.ts`, `src/lib/telemetry.ts`, `src/app/router.tsx` (tela da aula de 3.0 ganha o player), `src/testing/` (handlers MSW de sessão e entrega)
- **modificar:** `docker-compose.yml`, `docker-compose.remote.yml`, `docker-compose.coolify.yml`, `scripts/generate-local-env.sh`, `scripts/apps.sh` (container `media-edge`, segredo compartilhado, porta, chaves de asserção de `media`)
- **ref:** `src/media/src/CodeForCoders.Media.Application/UseCases/Videos/PrepareVideo/PrepareVideo.cs` (marcador `c4c-key:`, prefixo `hls/`); `src/media/src/CodeForCoders.Media.Infra.Messaging/FfmpegVideoTranscoder.cs` (formato das playlists guardadas); `src/media/src/CodeForCoders.Media.Application/Interfaces/IMediaStoragePort.cs` e `IVideoKeyProtector.cs`; `tasks/prd-autoria-curso/asyncapi-contract-media.yaml` (Referência de Uso); `docs/design/wireframes-aula.md`; `api-contract.yaml`, `internal-api-contract-media.yaml`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/identity` | `dotnet format src/identity/CodeForCoders.Identity.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/identity` | `dotnet build src/identity/CodeForCoders.Identity.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/identity` | `dotnet test --project src/identity/tests/CodeForCoders.Identity.ArchitectureTests/CodeForCoders.Identity.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/commerce` | `dotnet format src/commerce/CodeForCoders.Commerce.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/commerce` | `dotnet build src/commerce/CodeForCoders.Commerce.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/commerce` | `dotnet test --project src/commerce/tests/CodeForCoders.Commerce.ArchitectureTests/CodeForCoders.Commerce.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/media` | `dotnet format src/media/CodeForCoders.Media.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/media` | `dotnet build src/media/CodeForCoders.Media.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/media` | `dotnet test --project src/media/tests/CodeForCoders.Media.ArchitectureTests/CodeForCoders.Media.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/bff-student` | `dotnet format src/bff-student/CodeForCoders.BffStudent.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/bff-student` | `dotnet build src/bff-student/CodeForCoders.BffStudent.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/bff-student` | `dotnet test --project src/bff-student/tests/CodeForCoders.BffStudent.ArchitectureTests/CodeForCoders.BffStudent.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/student-spa` | `npm --prefix src/student-spa run lint` | exit 0 | `ci-react-ts.yml` |
| `src/student-spa` | `npm --prefix src/student-spa run typecheck` | exit 0 | `ci-react-ts.yml` |
| `src/student-spa` | `npm --prefix src/student-spa run test --` | exit 0 (suíte inteira, sem regressão) | `ci-react-ts.yml` |
| `src/student-spa` | `npm --prefix src/student-spa run build -- --base=/student/` | exit 0 | `ci-react-ts.yml` (`build-args: --base=/student/`) |
| compose | `docker compose -f docker-compose.yml -f docker-compose.remote.yml config -q` | exit 0, `media-edge` presente | `scripts/apps.sh` / AGENTS.md |
| compose | `docker compose -f docker-compose.yml config -q` | exit 0, sem hostname local de infraestrutura nova no modo `--remote` | AGENTS.md (replicar override em `docker-compose.remote.yml`) |

Cobertura agregada ≥ 70%, `dotnet publish` e imagens ficam para a validação full. Testcontainers pesados rodam **em sequência**, nunca em paralelo (AGENTS.md).

## Pronto quando

- [x] Gate focalizado passa (exit 0), com os mínimos do `gate`.
- [x] A aplicação de `media` inicia com os registros reais (política de aluno, cliente da decisão, adaptador de distribuição) e os adaptadores de armazenamento e de distribuição são exercitados **de verdade** (S3/MinIO e a credencial de cada adaptador), não por dublê da porta.
- [ ] No navegador, contra o ambiente de desenvolvimento: aluno com cortesia assiste a uma aula de ponta a ponta com o e-mail sobre o vídeo, **inclusive em tela cheia**, trocando de posição; o endereço de um segmento copiado, sem a credencial e com a credencial vencida, é recusado pela borda. Pré-requisitos reproduzíveis: `scripts/remote-infra.sh provision` e `migrate`, `scripts/apps.sh start --remote` com `media-edge`, vídeo preparado de verdade (pipeline de CAP-006), duas aulas publicadas, cortesia concedida pelo backoffice.
- [ ] A busca do e-mail de teste em log, spans, métricas, endereços, nomes de objeto e mensagens não encontra nada além de `watermark.text` e da claim para `media`; a `segmentAccess.query` de teste não aparece nos spans exportados.
- [x] Dois alunos recebem os mesmos objetos e a mesma chave do vídeo.
