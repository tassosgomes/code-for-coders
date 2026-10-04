# Revisão focused — Task 4.0 (V-02: assistir à aula com marca d'água e sem link solto)

Run: run.LGTP0xJR

- Modo: focused · Tentativa: 1/3 · HEAD revisado: c4179ef0869f47e3b8bdf902b8f004f749da287e (árvore 309b1914d168f63912082e5755e33cbd5f7f21b1) · checkpoint anterior: b74f17b
- Escopo: alterações não commitadas sobre o HEAD (45 arquivos modificados + untracked da task em identity, commerce, media, media-edge, bff-student, student-spa, compose e scripts). HEAD e `git status --porcelain` idênticos antes e depois da revisão.
- Tudo executado em primeiro plano, em sequência (Testcontainers sem paralelismo), sem monitor nem background.

## Gate declarado (exit code é o veredito)

Cadeia `&&` completa, **exit 0** (2m39s).

| # | Classe / filtro | Mínimo | Testes |
|---|---|---|---|
| 1 | Identity.UnitTests `StudentTokenEmailClaimTests` | 4 | 4 |
| 2 | Identity.IntegrationTests `StudentSessionEmailTests` | 2 | 2 |
| 3 | Commerce.IntegrationTests `AccessDecisionIssuersTests` | 5 | 5 |
| 4 | Media.UnitTests `PlaylistRewriterTests` | 6 | 6 |
| 5 | Media.UnitTests `DeliveryCredentialTests` | 6 | 6 |
| 6 | Media.IntegrationTests `PlaybackSessionOpenTests` | 9 | 9 |
| 7 | Media.IntegrationTests `PlaybackDeliveryTests` | 11 | 11 |
| 8 | BffStudent.IntegrationTests `PlaybackProxyTests` | 9 | 12 |
| 9 | student-spa `test -- playback-open` | — | 15 |
| 10 | student-spa `test -- telemetry-url-redaction` | — | 8 |

`gate_expect` (≥ 70): 78 testes passaram (4+2+5+12+20+12+23); todos os mínimos atendidos.

## Verificações do projeto (18 + 1 extra)

Todas **exit 0**: `dotnet format --verify-no-changes` (identity, commerce, media, bff-student); `dotnet build` dos 4 slnx (0 warnings); ArchitectureTests dos 4 componentes; student-spa `lint`, `typecheck`, suíte inteira (99 testes), `build -- --base=/student/` (apenas o aviso de tamanho de chunk do Vite, preexistente em natureza); `docker compose config -q` com `docker-compose.yml` + `docker-compose.remote.yml` (serviço `media-edge` presente) e com `docker-compose.yml` sozinho.
Cobertura agregada, `dotnet publish` e imagens ficam para a validação full.

Verificação adicional do validador (a borda não era coberta por teste automatizado): build da imagem `src/media-edge/Dockerfile` (ok, `secure_link` presente) e container nginx real com origem S3 inacessível, assinando a URL com a fórmula do `DevelopmentEdgeDeliveryAdapter` (`MD5("{exp}/{tenant}/{video}/hls/ {segredo}")`, base64url):

| Requisição ao segmento | Resultado |
|---|---|
| credencial válida | passa o `secure_link` (502 só porque a origem não existe) |
| sem credencial | 403 |
| credencial vencida | 410 |
| `e` adulterado | 403 |
| credencial de outro vídeo | 403 |
| `master.m3u8` / `key` direto na borda | 403 |

Container e imagem de teste removidos.

## Revisão semântica (resumo)

- **Ordem de abertura (RN-M10):** `OpenPlaybackSession` consulta Referência de Uso (404 sem tocar `commerce`) → `ready` (409) → decisão (503/403) → e-mail (422) → só então grava a sessão; falhas não persistem. Sessão sem coluna de e-mail; `expiresAt` = +5 min, `renewAfter` = −90 s. Cada chamada cria sessão nova (RN-M13).
- **Entrega:** playlist, variante e chave exigem sessão existente (404), não vencida (410) e do aluno do JWT; `tenant` via query filter global em `PlaybackSession`, `CourseVideoReference` e `Video`. Variante valida a qualidade por lista fechada. Chave decifrada na requisição, `no-store`, zerada após a escrita. Política `PlaybackAccess` exige `scope=playback:use`, `sessionId` e rejeita `permissions`/`roles` (token de ator fora da rota de aluno; o inverso coberto pelo teste).
- **Reescrita de playlist:** `c4c-key:{videoId}` só do próprio vídeo → `../key`; variantes → `variants/{q}`; segmentos → URL absoluta da distribuição sem credencial; qualquer linha fora do formato falha fechada.
- **Distribuição (ADR-0014):** porta `ISegmentDeliveryPort` com adaptadores CloudFront (política por prefixo, assinatura local) e borda de desenvolvimento (`secure_link`); credencial opaca por prefixo do vídeo e `expiresAt`. A borda nginx só serve `segment_*.ts` com credencial válida, repassa `Range`, zera `Cookie`/`Referer` e loga `$uri` sem consulta; `error_log` desligado para não vazar a URL.
- **E-mail:** só na claim da audiência `media`, em `watermark.text` e no estado do componente; sem log/span/persistência no código novo. O redator de telemetria remove também e-mails e a `segmentAccess.query` (valor e parâmetros `st`, `Policy`, `Signature`, `Key-Pair-Id`).
- **BFF:** a rota `POST /api/v1/lessons/{id}/playback-sessions` usa a audiência `media` (CSRF mantido), corpo e `Content-Type` de `media` repassados sem alteração, `no-store`, erros no padrão problem+json com `code`.
- **SPA:** hls.js com `xhrSetup` anexando a credencial apenas a URLs `…/hls/{q}/segment_N.ts` (sem credenciais de cookie na borda); marca d'água no contêiner (cobre a tela cheia), `pointer-events-none`, `aria-hidden`, reposicionamento sem repetir a zona, visível pausado; controles com nome acessível e teclado; aviso de uso pessoal; estados de erro/sem suporte. Nenhuma mensagem diz "protegido contra cópia".

## Bloqueantes

Nenhum.

## Recomendações (3, não bloqueantes)

1. **Itens manuais de "Pronto quando" não reexecutados pelo validador** — reprodução no navegador contra o ambiente de desenvolvimento (tela cheia, troca de posição, URL de segmento sem/vencida) e a busca do e-mail/`segmentAccess.query` em log/span/métrica exigem `remote-infra` + vídeo real e ficaram fora desta rodada; o implementer registrou a reprodução manual pela borda de dev. A prova da borda foi refeita aqui com nginx real (tabela acima), mas a verificação ponta a ponta com MinIO e o registro de acesso real ficam para a validação full/QA. O log de acesso foi verificado só estaticamente (`log_format credential_free`).
2. `PlaybackRepository.FindLessonAsync` usa `SingleOrDefault` sobre `(tenant_id, lesson_id)`, mas o índice criado é não único (a chave primária é tenant+curso+aula). Uma aula referenciada por dois cursos causaria 500 em vez de 404/decisão. Se a invariante "uma aula, um curso" valer, tornar o índice único ou documentar.
3. `docker-compose.coolify.yml` exige `MEDIA_EDGE_PUBLIC_URL` e `AWS_MEDIA_BUCKET` (por desenho, valores do Coolify); lembrar de configurá-los no ambiente de dev antes do deploy.

## Resultado

**VALIDAÇÃO APROVADA** (0 bloqueantes, 3 recomendações).

---

## Revalidação — tentativa 1/3 (pós-correção de B1/B2 da full run.Ejq25cS3)

Run: run.UKGEAx7m

- Modo: revalidation · HEAD revisado: 2e9990fd55bded044a8c27fd3b5f6141cc88b4bf · fix não commitado (6 arquivos de `src/` listados abaixo); `git status --porcelain` e HEAD idênticos antes e depois. Execução em primeiro plano, em sequência. Sensor de discriminação não exigível aqui (fica para a próxima full).
- Diff revisado (checkpoint 853c367 → árvore de trabalho): `MediaInfrastructureTests.cs`, `VideoLibraryAuthorizationTests.cs`, `PlaybackDeliveryTests.cs`, `GetPlaybackResource.cs`, `PlaybackMediaClient.cs`, `PlaybackProxyTests.cs`.

### Evidência executada (exit code é o veredito)

| Check | Resultado |
|---|---|
| Gate da task (cadeia `&&` completa, via `rtk proxy`) | **exit 0** — 61 testes .NET (4+2+5+6+6+9+11+18) + SPA 15 + 8 = 84 ≥ 70 (`gate_expect`); mínimos por classe atendidos |
| `dotnet test` completo `src/media` | **exit 0**, 229/229 (era 227/229 na full; B1) |
| `dotnet test` completo `src/bff-student` | **exit 0**, 205/205 |
| `dotnet format --verify-no-changes` media / bff-student | exit 0 / exit 0 |
| `dotnet build` slnx media / bff-student | exit 0 / exit 0, 0 warnings |
| ArchitectureTests media / bff-student | exit 0 / exit 0 |

Identity, commerce e student-spa não foram tocados pelo fix; suas verificações da revisão focused e da full continuam válidas (o SPA, ainda assim, rodou no gate: 23 testes).

### Bloqueios anteriores

- **B1 — resolvido.** `MediaInfrastructureTests.cs` e `MediaJwksUnavailableApiFactory` (`VideoLibraryAuthorizationTests.cs`) passam `Preparation:MasterKey`, `Preparation:MasterKeyId` e `Playback:Delivery:SharedSecret` nos hosts de teste. As validações de produção **não foram relaxadas**: `Infra.Data/DependencyInjection.cs` não aparece no diff e `Infra.Messaging/DependencyInjection.cs` só difere do checkpoint por um `using` já commitado (não faz parte do fix). A suíte completa de media agora sai 0.
- **B2 — resolvido.** `GetPlaybackResource.cs` emite `PLAYBACK_SESSION_NOT_FOUND` (404) e `PLAYBACK_SESSION_EXPIRED` (410) nos quatro pontos; `PlaybackMediaClient.KnownErrors` perdeu o par legado; `PlaybackProxyTests` e `PlaybackDeliveryTests` (que agora afirma o `code` no corpo de erro, 404 × 410) refletem o contrato. Busca por `SESSION_NOT_FOUND`/`SESSION_EXPIRED` em `src/`, `contracts/` e nos contratos do PRD: **0 ocorrências**. `api-contract.yaml`, `internal-api-contract-media.yaml` e `use-protected-playback.ts` só usam `PLAYBACK_SESSION_*`. (`SESSION_REQUIRED` em `contracts/` é outro código, de autenticação.)

### Regressões no diff novo

Nenhuma. Mudança mínima, só de vocabulário de erro e de configuração de teste; sem alteração de lógica de autorização, ordem de verificação ou cache.

### Bloqueantes

Nenhum.

### Recomendações (não bloqueantes, herdadas da full e sem mudança)

Política de reprodução em código, limite de corpo no avanço, `TenantId` em `GetPlaybackResource`/`RenewPlaybackSession`, poda de `secrets` em `telemetry-url-redaction.ts`, itens de entrega da PR (`routing.default.json`, `__pycache__/`, `prompt.txt`) e as 3 recomendações focused (itens manuais do navegador/log, índice não único de `(tenant_id, lesson_id)`, variáveis do Coolify). Contagem: 5 da full + 3 da focused, todas fora do escopo do fix.

**VALIDAÇÃO APROVADA** (0 bloqueantes).
