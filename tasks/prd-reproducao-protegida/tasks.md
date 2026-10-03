# Plano de Implementação — Reprodução protegida (CAP-007, 1º PRD)

> **TechSpec de origem:** [techspec.md](techspec.md) 1.0, aprovada em 2026-10-03
> **Escopo:** Full-stack (`identity`, `commerce` só configuração, `learning`, `media`, `bff-student`, `student-spa`, borda de desenvolvimento `media-edge`, painel no Kibana)
> **ADRs pertinentes:** [0002](../../docs/adr/0002-plataforma-de-runtime-coolify.md), [0003](../../docs/adr/0003-verificacao-de-sessao-do-aluno.md), [0006](../../docs/adr/0006-preparacao-de-video-e-custodia-de-chave.md), [0008](../../docs/adr/0008-observabilidade-kibana-como-codigo.md), [0009](../../docs/adr/0009-autenticacao-de-servico-bff-aluno-em-servicos-de-dominio.md), [0012](../../docs/adr/0012-autenticacao-de-servico-media-learning-em-commerce-revisada.md), [0013](../../docs/adr/0013-jwt-de-aluno-validado-por-servicos-de-dominio.md), [0014](../../docs/adr/0014-entrega-de-segmentos-por-credencial-opaca-e-porta-de-distribuicao.md)
> **Contratos:** [contracts.md](contracts.md) 1.0 — OpenAPI do [BFF do aluno](api-contract.yaml) 1.0.0, interno de [`media`](internal-api-contract-media.yaml) 1.1.0, de [`learning`](internal-api-contract-learning.yaml) 1.1.0 e de [Identity](internal-api-contract-identity.yaml) 1.1.0; AsyncAPI de [`media`](asyncapi-contract.yaml) 1.2.0
> **Design:** novo documento `docs/design/wireframes-aula.md` (a produzir em 1.0 e 2.0), sobre o design system do aluno
> **Status do plano:** Confirmado para implementação (aprovado pelo responsável em 2026-10-03)

## Visão Geral

Ao final, o aluno com cortesia (CAP-008) abre o link de uma aula e vê o vídeo tocar com o **e-mail dele sobre o vídeo**, em tela cheia
também, e a lista de aulas do curso para trocar de aula. Quem não tem direito, ou deixou de ter, não obtém vídeo, segmento nem chave,
e vê a mensagem certa para cada caso, sem nunca ver título de curso. A reprodução se renova sozinha a cada 5 minutos repetindo a decisão
de acesso, e para em minutos quando o direito acaba. Um link de segmento copiado não funciona sozinho. O avanço da reprodução sai como
fato bruto, sem e-mail, e fica retido até a `CAP-017` existir. A equipe vê custo, erros e cache por um painel no Kibana.

O design (ASCII → Figma → aprovação) vem antes de qualquer código de tela. Toda fatia atravessa as pontas que o comportamento exige
(serviço, BFF e SPA); nenhuma é dividida por camada.

## Fases

### Fase 0 — Design aprovado

1.0 wireframes ASCII e 2.0 Figma. Checkpoint: cabeçalho de `docs/design/wireframes-aula.md` com a linha `Status` registrando `ASCII e
Figma aprovados`.

### Fase 1 — Abrir a aula

3.0 a aula e a lista de aulas, com o acesso decidido. Checkpoint: em `http://localhost:8082/student/aulas/{lessonId}` o aluno com
cortesia vê a lista; o aluno sem concessão vê a mensagem, sem título; sem sessão de login, o link leva ao login e volta à aula.

### Fase 2 — Assistir

4.0 sessão, entrega protegida, marca d'água e player. Checkpoint: o aluno com cortesia assiste a uma aula de ponta a ponta, com o
e-mail sobre o vídeo, em tela cheia também; o endereço de um segmento copiado é recusado sem a credencial e com a credencial vencida.

### Fase 3 — Manter, navegar, informar e observar

5.0 renovação e parada por fim de direito; 6.0 trocar de aula e velocidade; 7.0 avanço e retenção; 8.0 sinais e painel. Checkpoint:
20 minutos de reprodução sem interrupção; o direito antecipado para a reprodução dentro da janela; a segunda aula abre do início; os
fatos de avanço ficam retidos sem alarme do outbox; o painel mostra uma reprodução de teste, sem dado pessoal.

## Mapa de Entrega

Uma linha por task. A fatia vem da TechSpec; nas fatias com tela, ela cruza SPA → BFF → serviço na mesma task.

| Fatia | Task | Comportamento observável | Gate | Bloqueado por |
|---|---|---|---|---|
| EN-01 (ASCII) | 1.0 | Wireframes ASCII da tela da aula aprovados | `rg` do status em `wireframes-aula.md` | Nenhum |
| EN-01 (Figma) | 2.0 | Figma da tela da aula aprovado e registrado | `rg` de `ASCII e Figma aprovados` no status | 1.0 |
| V-01 | 3.0 | O aluno com direito abre a aula e vê a lista de aulas; sem direito, vê o motivo; sem login, volta à aula depois de entrar | Identity `StudentTokenAudienceTests` + `StudentTokenJwksTests` + Commerce `AccessDecisionIssuersTests` + Learning `StudentLessonTests` + `AccessDecisionClientTests` + BFF `StudentLessonProxyTests` + SPA `student-lesson-screen` + `student-login-return` | 2.0 |
| V-02 | 4.0 | O aluno assiste à aula com o e-mail sobre o vídeo, e a entrega recusa tudo que não seja a sessão | Identity `StudentTokenEmailClaimTests` + `StudentSessionEmailTests` + Commerce `AccessDecisionIssuersTests` + Media `PlaylistRewriterTests` + `DeliveryCredentialTests` + `PlaybackSessionOpenTests` + `PlaybackDeliveryTests` + BFF `PlaybackProxyTests` + SPA `playback-open` + `telemetry-url-redaction` | 3.0 |
| V-03 | 5.0 | A reprodução se renova sem pausa e para dentro da janela quando o direito acaba; indisponível não derruba antes do fim | Media `PlaybackRenewalTests` + SPA `playback-renewal` | 4.0 |
| V-04 | 6.0 | O aluno troca de aula pela lista e muda a velocidade; a versão vigente e a identidade da aula valem | Learning `PublishedLessonRepublicationTests` + SPA `playback-navigation` | 4.0 |
| V-05 | 7.0 | O avanço sai como fato sem e-mail, idempotente e coalescido, retido sem alarme e reenviável | Media `PlaybackProgressTests` + `RetainedOutboxFactTests` + BFF `PlaybackProgressProxyTests` + SPA `playback-progress` | 4.0 |
| V-06 | 8.0 | A equipe vê sessões por desfecho, decisões indisponíveis, tempo até começar, alunos ativos e custo, sem dado pessoal | Media `PlaybackTelemetryTests` + `PlaybackSignalsTests` + SPA `playback-telemetry` + `import_observabilidade.py --verify-only` | 4.0 |

EN-01 da TechSpec vira duas tasks (ASCII e Figma) porque cada aprovação é uma decisão separada do responsável e o Figma parte do
ASCII aprovado. As seis fatias seguem a TechSpec uma a uma; a fatia V-NN está na task (NN + 2).0, porque 1.0 e 2.0 são o design.

### Habilitadores

| Enabler | Task | Por que não cabe numa fatia | Desbloqueia |
|---|---|---|---|
| EN-01 (ASCII) | 1.0 | O desenho da tela é decidido pelo responsável antes do código e vale para todas as fatias com tela | V-01 a V-04 (parte de tela) |
| EN-01 (Figma) | 2.0 | A aprovação do Figma é decisão do responsável e vale para todos os estados de uma vez | V-01 a V-04 (parte de tela) |

Não há habilitador técnico: cada migration nasce na fatia que usa a coluna (`dotnet ef migrations add`): o índice de aula em
`learning` em 3.0; `playback_sessions` e o índice de Referência de Uso em 4.0; os campos de avanço da sessão em 7.0. A chave de aluno
de Identity e o emissor `learning` nascem em 3.0, o e-mail e o emissor `media` em 4.0, porque 3.0 é a primeira que os usa.

## Tasks

- [x] 1.0 Wireframes ASCII da tela da aula aprovados
- [x] 2.0 Figma da tela da aula aprovado
- [x] 3.0 O aluno com direito abre a aula e vê a lista de aulas; sem direito, vê o motivo
- [x] 4.0 O aluno assiste à aula, com marca d'água e sem link solto
- [x] 5.0 A reprodução continua sem interrupção e para quando o direito acaba
- [x] 6.0 O aluno troca de aula e ajusta a velocidade
- [ ] 7.0 O avanço da reprodução chega como fato, sem e-mail e sem perder nada
- [ ] 8.0 A equipe vê custo, erros e cache da entrega

## Caminho crítico e lanes

`1.0 → 2.0 → 3.0 → 4.0`, e depois `5.0`, `6.0`, `7.0` e `8.0`, cada uma dependendo só de `4.0`. A ordem numérica é a de execução:
4.0 a 8.0 tocam o player do `student-spa`, `EndpointExtensions.cs` de `media` e do `bff-student`, o `MediaTelemetry.cs` e os handlers
MSW, então **não há lane paralela segura**; 5.0 a 8.0 só dependem de 4.0 e podem ser reordenadas entre si, desde que serializadas.

**Estado intermediário conhecido:** entre 3.0 e 4.0 a tela mostra a lista de aulas e a área do player em carregamento, sem vídeo; entre
4.0 e 5.0 a sessão dura 5 minutos e não renova (a reprodução para ao vencer). O plano é integrado numa única entrega; nenhuma
implantação intermediária acontece. A ordem de implantação do conjunto é a de `contracts.md`: Identity 1.1.0 → emissores `media` e
`learning` em `commerce` → `learning` → `media` → `bff-student` → `student-spa`; o sistema só vai a homologação ao fim do MVP, sem dado
legado a reconciliar (OD67).

## Integridade dos gates

- .NET: `dotnet test --project <csproj> -- --filter-class <classe> --minimum-expected-tests N` (Microsoft.Testing.Platform: exit 8
  se nada rodar, 9 se rodar menos que N). A classe usa o nome completo com o namespace do projeto de teste.
- SPA: `npm --prefix src/student-spa run test -- <fragmento de caminho>`. **Não** usar `-t` sozinho: o Vitest sai com 0 quando o nome não
  casa; o filtro de caminho sai com 1 quando nenhum arquivo casa. Os testes de cada fatia ficam em arquivos cujo caminho contém o
  fragmento do gate, e nenhum fragmento é prefixo de arquivo de outra fatia ou preexistente: `student-lesson-screen` e
  `student-login-return` (3.0), `playback-open` (4.0), `playback-renewal` (5.0), `playback-navigation` (6.0), `playback-progress`
  (7.0), `playback-telemetry` (8.0). `telemetry-url-redaction` (4.0) é o arquivo **preexistente** estendido, cujos testes novos entram
  no mesmo caminho; o fragmento `playback-` casa todos e por isso nunca é gate.
- Kibana (8.0): `python3 scripts/kibana/import_observabilidade.py --verify-only` não usa rede e sai com 0 só se o NDJSON, os
  instrumentos e as regras forem válidos.
- Design (1.0, 2.0): verificação estática da linha `> **Status:**` do cabeçalho de `docs/design/wireframes-aula.md`, com o texto de
  aprovação do responsável.
- As classes de teste nomeadas nos gates são criadas na própria task, **exceto** `AccessDecisionIssuersTests` (criada em 3.0 e
  **estendida** em 4.0, que sobe o mínimo de 3 para 5) e `telemetry-url-redaction` (preexistente, estendido em 4.0).
- Testcontainers (PostgreSQL, RabbitMQ) pesados rodam **em sequência**; os comandos do `gate` já são encadeados e não rodam em paralelo.

## Artefatos compartilhados

| Artefato | Produzido em | Evolui em |
|---|---|---|
| `docs/design/wireframes-aula.md` (ASCII + frames Figma) | 1.0, 2.0 | referência de 3.0 a 6.0 |
| Chave de aluno no JWKS de Identity, audiência `learning` com escopo `lessons:read`, par de chaves e emissor `learning` em `commerce` | 3.0 | — |
| Política de aluno (escopo da audiência e ausência de `permissions`) em `learning` | 3.0 | reproduzida em `media` (4.0) |
| Cliente da decisão de acesso com asserção de serviço, timeout de 2 s e cache de 30 s | 3.0 (`learning`) | 4.0 (`media`) |
| Audiência por rota no `bff-student` | 3.0 | 4.0 a 7.0 |
| Rota `/aulas/:lessonId` e a tela da aula do `student-spa` | 3.0 | 4.0 (player), 5.0 a 7.0 |
| Audiência `media` com escopo `playback:use` e a claim `email` só nela; emissor `media` em `commerce` | 4.0 | — |
| `playback_sessions`, índice `(tenant_id, lesson_id)` de Referência de Uso | 4.0 | 5.0 (renovação), 7.0 (avanço) |
| Porta de distribuição e adaptadores CloudFront e borda de desenvolvimento | 4.0 | 5.0 (renovação reusa) |
| Container `media-edge` e o segredo compartilhado | 4.0 | — |
| Player (hls.js), marca d'água, controles e o registro de segredos de telemetria | 4.0 | 5.0, 6.0, 7.0, 8.0 |
| `Outbox:RetainedRoutingKeys` e a operação de reenvio de avanço | 7.0 | `CAP-017` (virada para o consumidor) |
| Instrumentos de sessão, decisão e alunos ativos; painel e alerta de reprodução | 8.0 | — |

## Verificação herdada

| Componente | Fonte | Checks e limites | Estado da base | Resolução planejada |
|---|---|---|---|---|
| `src/identity` | `.github/workflows/identity.yml` → `tassosgomes/template-pipeline/.github/workflows/ci-dotnet.yml@v1` (Debug, cobertura 70, container, `security-mode: observe`) | restore; `dotnet format --verify-no-changes`; `dotnet test` (MTP) com cobertura ≥ 70%; `dotnet publish -c Release`; imagem `src/identity/Dockerfile` | Não medido nesta sessão. A base entregou `CAP-008` em `main` (PR #141) | Nenhuma falha herdada comprovada. A cobertura agregada é medida na validação full |
| `src/commerce` | `.github/workflows/commerce.yml` → `ci-dotnet.yml@v1` | Idem, cobertura ≥ 70% | Não medido | Nenhuma. Só configuração e testes |
| `src/learning` | `.github/workflows/learning.yml` → `ci-dotnet.yml@v1` | Idem, cobertura ≥ 70% | Não medido | Nenhuma |
| `src/media` | `.github/workflows/media.yml` → `ci-dotnet.yml@v1` (`runs-on: ubuntu-latest`, ffmpeg provisionado pela fixture `FfmpegTools`) | Idem, cobertura ≥ 70% | Não medido | Nenhuma. Os testes de entrega não dependem de ffmpeg; os de preparação preexistentes seguem como estão |
| `src/bff-student` | `.github/workflows/bff-student.yml` → `ci-dotnet.yml@v1` | Idem, cobertura ≥ 70% | Não medido | Nenhuma |
| `src/student-spa` | `.github/workflows/student-spa.yml` → `ci-react-ts.yml@v1` (`build-args: --base=/student/`, cobertura 70, container) | lint; typecheck; `vitest run --coverage` ≥ 70%; `vite build --base=/student/`; imagem `src/student-spa/Dockerfile` | Não medido | Nenhuma. Dependência nova (hls.js): licença e versão a conferir ao instalar, e a imagem precisa continuar construindo |
| Compose e borda | `docker-compose.yml`, `docker-compose.remote.yml`, `docker-compose.coolify.yml`; AGENTS.md | `docker compose config -q` nos três arranjos; todo hostname local novo replicado no override `--remote` | Não medido | A imagem do nginx da borda precisa trazer `secure_link` (D-10): checagem inicial de 4.0 |
| Kibana | ADR-0008; `scripts/kibana/import_observabilidade.py` | `--verify-only` (sem rede); `--import` no Kibana de desenvolvimento | Painel de ingestão (`CAP-006`) entregue | Nenhuma |

## Cobertura

| Requisito | Task(s) |
|---|---|
| RF-01 | 3.0, 4.0 |
| RF-02 | 4.0, 5.0 |
| RF-03 | 4.0 |
| RF-04 | 4.0 |
| RF-05 | 4.0, 6.0 |
| RF-06 | 3.0, 6.0 |
| RF-07 | 7.0 |
| RF-08 | 8.0 |
| RN-R01 | 4.0, 5.0 |
| RN-R02 | 3.0, 5.0 |
| RN-R03 | 4.0 |
| RN-R04 | 4.0 |
| RN-R05 | 7.0 |
| RN-R06 | 3.0, 6.0 |
| RN-R07 | 3.0, 6.0 |
| RN-R08 | 3.0, 6.0 |
| US-01 | 3.0, 4.0 |
| US-02 | 6.0 |
| US-03 | 3.0 |
| US-04 | 5.0 |
| US-05 | 4.0 |
| US-06 | 5.0 |
| US-07 | 7.0 |
| US-08 | 8.0 |

Mapeamento das histórias do PRD, na ordem em que aparecem em "Histórias de Usuário": US-01 aluno com acesso abre a aula e o vídeo
começa; US-02 trocar de aula pela lista; US-03 aluno sem acesso ou vencido entende o que houve; US-04 não ser desconectado por falha
momentânea; US-05 escola: link copiado não serve e o e-mail aparece no vídeo; US-06 escola: revogado ou vencido para a reprodução em
minutos; US-07 `CAP-017` recebe o avanço; US-08 equipe vê custo e erros.

> Verificado por `python3 <skill-dir>/scripts/validate_plan.py tasks/prd-reproducao-protegida/` antes do handoff.
