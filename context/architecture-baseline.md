---
tsg_artifact: architecture-baseline
product: code-4-coders
version: 1.2
status: approved
updated: 2026-09-20
sources: vision.md@1.1, context/domain-map.md@1.1
---

# Baseline Arquitetural

> **Nível 2 da hierarquia de documentação.** Deriva de `vision.md` (v1.1) e `context/domain-map.md` (v1.1).
> Traduz fronteiras conceituais em **regras estruturais de implementação**. Não projeta funcionalidade:
> nenhuma feature, tela, endpoint ou tabela é decidida aqui. TechSpecs consomem este documento; PRDs e
> backlog herdam as restrições aplicáveis.

**Versão:** 1.1 · **Data:** 2026-09-20 · **Origem:** `vision.md` v1.1, `context/domain-map.md` v1.1 · **Produto:** code-4-coders
**Padrões herdados:** skills `dotnet` (.NET 10 / ASP.NET Core) e `react` (React + Vite + TS) — são a
decisão de stack e de convenção do time; este baseline **não as repete**, só define o que elas não cobrem:
a fronteira entre serviços.

---

## Escopo deste Baseline

**Dentro:** estilo arquitetural, granularidade de serviço, comunicação, propriedade de dados, sessão e
autorização, observabilidade, escalabilidade, guardrails — backend **e** a parte técnica do frontend
(topologia de aplicações, borda, sessão no browser, configuração de runtime, telemetria).

**Fora, por decisão explícita do time:**

| Fora | Dono | Consequência aqui |
|---|---|---|
| CI/CD, provisionamento AWS, orquestrador de containers, malha de rede, secret manager | Time de plataforma | O baseline declara o que **exige** da plataforma (abaixo) e não decide como será entregue |
| Identidade visual, design system, layout de tela | Time de design | O frontend é tratado aqui só na dimensão técnica; nenhum agente inventa visual |
| Modelagem de features, entidades e endpoints | TechSpec / PRD | O baseline define a regra, não a instância |

### Dependências de plataforma que este estilo exige

Microsserviços é determinação de cima para baixo e está aceita. Registrada uma condição estrutural, não
um veto: **sem esteira de deploy independente por serviço, microsserviços vira monolito distribuído** —
o pior dos dois mundos (acoplamento de monólito com latência e falha parcial de distribuído). O baseline
foi desenhado para reduzir esse risco (poucos serviços, cadeia síncrona curta, banco por serviço), mas a
condição precisa ser atendida pelo time de plataforma antes da Fase 1 ir a produção:

1. Pipeline de build, teste e deploy **independente por serviço**, com rollback próprio.
2. Feed NuGet interno privado para o pacote de contratos (`Contracts`).
3. Broker RabbitMQ gerenciado, com DLQ e política de retenção.
4. PostgreSQL com **credencial e banco por serviço** — não um banco com vários donos.
5. Valkey (cache e sessão de BFF).
6. Coletor OTLP e backend de traces, métricas e logs.
7. Secret manager; nenhuma credencial em repositório ou imagem.

---

## Estilo Arquitetural

### Decisão: microsserviços com serviço internamente modular

**Serviço é unidade de deploy, escala e falha. Módulo é fronteira de domínio.** Um serviço agrupa os
domínios que mudam pelo mesmo motivo e no mesmo ritmo; dentro dele, cada domínio do Domain Map continua
sendo um módulo com Clean Architecture completa, `Contracts` próprio e **schema próprio**.

É a composição deliberada dos dois formatos da skill `dotnet` (`references/solution-formats.md`):
microsserviços **entre** serviços, monolito modular **dentro** de cada serviço. O motivo é o risco
declarado na visão — 16 domínios contra 2 engenheiros — e o fato de que 16 serviços na largada custariam
16 esteiras, 16 bancos e 16 pontos de falha para um sistema com zero aluno.

O que isso compra: **extrair um módulo para serviço próprio é mover projetos, não redesenhar fronteira.**
O que isso exige: a fronteira de módulo é tratada com a mesma severidade da fronteira de serviço. Módulo
não é pasta — é contrato. Compartilhar tabela entre módulos "porque estão no mesmo processo" é a única
forma de perder essa opção, e é proibido.

### Topologia inicial

```text
  student-spa ─────▶ bff-student ──┬──▶ identity
  (React/Vite)       (YARP + BFF)  ├──▶ commerce
                                   ├──▶ learning
  admin-spa ───────▶ bff-admin ────┼──▶ media
  (React/Vite)                     ├──▶ notification
                                   └──▶ (audit: só leitura)

                     validação pública de certificado ──▶ rota anônima no bff-student (Fase 3)

  identity   │ Identidade e Acesso
  commerce   │ Catalog · Sales · Entitlement          ← 3 módulos, 3 schemas
  learning   │ Content · Progress                     ← 2 módulos, 2 schemas
  media      │ Entrega de Mídia e Proteção
  audit      │ Auditoria e Conformidade (mínimo)      ← append-only, consumidor de eventos
  notification │ Notificação (mínimo)                ← e-mail transacional, um canal

  RabbitMQ (topic) ── eventos de integração entre serviços
  PostgreSQL ─────── um banco por serviço
  Valkey ─────────── cache + sessão dos BFFs
```

**Por que `identity` sozinho:** muda por razão de segurança, tem superfície de ataque própria e é
consultado por todos. Acoplá-lo a domínio de negócio faz toda mudança comercial tocar autenticação.

**Por que `commerce` agrupa Catálogo, Vendas e Matrícula:** os três participam da mesma transação de
negócio (comprar → conceder acesso) e mudam juntos quando a condição comercial muda. Mantê-los em
serviços distintos na Fase 1 criaria uma cadeia síncrona de três saltos no caminho crítico da compra
para benefício nenhum. **Ressalva registrada:** `Entitlement` é o nó mais consultado do sistema (5
domínios perguntam a ele) e carrega o risco DE01 — direito de acesso virar flag de checkout. Ele tem
schema, `Contracts` e casos de uso próprios desde o primeiro commit, e `Sales` fala com ele **por evento,
como se já fosse outro serviço**. É o primeiro candidato a extração (critério abaixo).

**Por que `media` sozinho:** é o único domínio cujo comportamento é ditado por restrição externa — CDN,
formato de entrega e custo de banda —, concentra o gasto de infraestrutura e escala em dimensão diferente de
todo o resto (banda, não CPU). Separá-lo é o que permite mudar a estratégia de proteção ou de entrega sem
tocar em regra de negócio. Com a decisão de não contratar DRM (BA15), essa estratégia **vai** mudar ao longo
do tempo: a fronteira é o que torna isso barato.

**Por que `audit` existe já na Fase 1, mesmo mínimo:** a razão de ser de Auditoria (DE13) é estar **fora
do alcance de quem praticou o ato**. Um módulo de auditoria dentro de `commerce`, com a mesma credencial
de banco de quem dá reembolso, não é auditoria. É o único caso em que a afinidade de tamanho perde para a
propriedade estrutural. Na Fase 1 ele é um consumidor de eventos com escrita append-only e leitura
restrita ao backoffice.

### Agrupamento de domínios em unidades de deploy

> Este baseline decide **agrupamento** e **gatilho de extração**, não *quando* cada unidade nasce.
> Fase é consequência do sequenciamento das capacidades, e este documento é escrito antes do backlog
> existir — sem enxergar dependência entre capacidades, qualquer fase atribuída aqui contradiz o
> backlog na primeira dependência cruzada. A ordem em que cada unidade aparece deriva de
> `backlog/capabilities.md`.

| Grupo | Serviço | Domínios | Por quê |
|---|---|---|---|
| inicial | `identity` | Identidade e Acesso | Isolamento de segurança |
| inicial | `commerce` | Catálogo e Oferta · Vendas e Checkout · Matrícula e Direito de Acesso | Mesma transação de negócio |
| inicial | `learning` | Conteúdo e Currículo · Aprendizagem e Progresso | Currículo e percurso mudam por decisão pedagógica |
| inicial | `media` | Entrega de Mídia e Proteção | CDN, custo de banda e escala próprios; estratégia de proteção volátil |
| inicial | `audit` | Auditoria e Conformidade | Independência de quem pratica o ato |
| posterior | `billing` | Cobrança e Assinatura · **Fiscal** (módulo) | Maior complexidade de estado no tempo; Fiscal é módulo com consumo assíncrono, o que já garante DE06 (falha fiscal não trava venda) |
| inicial | `notification` | Notificação | Dono único do consentimento LGPD; integra provedores externos. No grupo inicial porque `CAP-001` e `CAP-011` não fecham ciclo sem e-mail transacional — decisão de 2026-09-20 |
| posterior | `certification` | Certificação | Único consumidor externo **não autenticado**: disponibilidade e imutabilidade de outra natureza |
| posterior | — | Avaliação | Módulo `Assessment` em `learning`; extrai quando a correção dissertativa por professor virar fluxo próprio |
| posterior | `community` | Comunidade e Engajamento | Volume de escrita de aluno e moderação |
| posterior | `support` | Atendimento e Suporte | Atores, permissões e prazos distintos de Comunidade (exigência da visão) |
| posterior | `analytics` | Inteligência de Negócio | Consumidor derivado; nunca escreve, nunca é consultado |

No fim da evolução: **~11 serviços para 16 domínios.** Nenhum domínio do mapa desaparece; alguns vivem como
módulo.

### Critério de extração de módulo para serviço

Extrair quando **pelo menos duas** forem verdadeiras — nunca por estética:

- o módulo precisa escalar em dimensão diferente da do serviço que o hospeda (medido, não suposto);
- o ritmo de deploy conflita: uma mudança nele bloqueia ou é bloqueada por outra parte do serviço;
- a falha dele derruba algo que deveria sobreviver (ou o contrário);
- ele passou a ter dependência externa pesada e própria;
- o time que o mantém deixou de ser o mesmo.

Extrair é uma mudança arquitetural: exige ADR em `docs/adr/` com o gatilho que a justificou.

---

## Princípios de Interação entre Domínios

1. **A fronteira do Domain Map é inviolável, dentro ou fora do processo.** Que dois domínios estejam no
   mesmo serviço não autoriza um a ler a tabela, a entidade ou o `DbContext` do outro.
2. **Quem decide, pergunta. Quem mudou, publica.** Consulta síncrona é para **decisão no caminho crítico**
   ("este aluno pode?"). Evento é para **fato consumado** ("o pagamento foi confirmado"). Não existe
   comando síncrono entre serviços para alterar estado alheio.
3. **Entre módulos do mesmo serviço:** síncrono por interface de `Contracts` resolvida na DI — **nunca
   HTTP interno**; assíncrono por evento de integração no outbox do módulo de origem.
4. **Entre serviços:** síncrono por cliente tipado atrás de porta em `Application/Interfaces`
   (`IHttpClientFactory` + `AddStandardResilienceHandler`); assíncrono por RabbitMQ com outbox no produtor
   e idempotência ou inbox no consumidor.
5. **Profundidade máxima de cadeia síncrona: 1 salto entre serviços.** O BFF pode chamar um serviço, e
   esse serviço pode chamar no máximo um outro. Três saltos exigem ADR — na prática, significam que a
   fronteira está errada.
6. **Nenhuma chamada de rede dentro de transação de banco.** Publicação é outbox; integração externa é
   depois do commit ou por worker.
7. **Identidade não se consulta por request.** O ator corrente vem do token validado localmente; nenhum
   serviço chama `identity` no caminho quente para saber quem é o usuário.
8. **Toda dependência externa tem camada anticorrupção.** Gateway de pagamento, provedor de NFS-e, CDN e armazenamento de mídia,
   WhatsApp e e-mail entram por porta em `Application/Interfaces` com vocabulário do **nosso** domínio. Tipo,
   enum, código de erro ou DTO de terceiro não cruza para `Domain` nem aparece em contrato nosso.
9. **Inteligência de Negócio consome e nunca é consultado.** Nenhum domínio lê do `analytics` para decidir
   nada (DE11).
10. **Auditoria só recebe.** Nenhum domínio consulta `audit` para tomar decisão de negócio; o backoffice lê,
    a plataforma não escreve por outro caminho que não o evento.

### Decisão de acesso ("este aluno pode acessar isto agora?")

Consulta **síncrona ao dono** (`Entitlement`) com **cache curto**:

- fonte de verdade única, consistência imediata — suspensão por inadimplência e revogação por reembolso
  valem em segundos, não "eventualmente";
- cache de decisão no consumidor, **TTL absoluto de até 30s**, chave `{tenant}:{aluno}:{conteudo}:v{n}`;
  cache é aceleração, **nunca réplica**;
- `Entitlement` indisponível: o player **degrada** (não libera acesso novo), nunca libera por padrão.
  Falha fechada é a regra em decisão de acesso;
- proibida réplica local do direito de acesso em outro serviço: duas fontes de verdade sobre quem pode
  assistir é exatamente o risco que a visão mandou evitar.

---

## Regras de Propriedade dos Dados

1. **Um banco por serviço, um schema por módulo.** Nenhum serviço lê tabela, view ou schema de outro —
   garantido por credencial, não por disciplina: cada serviço recebe usuário com acesso apenas ao seu banco.
2. **Um dono por dado.** A tabela "Responsabilidade dos dados" do Domain Map é normativa. Quem não é dono
   guarda, no máximo, **Id + cópia congelada** do que o contrato exigiu (o pedido congela preço e condição;
   isso é o contrato da compra, não duplicação).
3. **Referência entre domínios é por Id.** Sem FK cruzando schema, sem join entre módulos, sem view
   compartilhada.
4. **Cache não é réplica.** TTL absoluto sempre, chave versionada, invalidação após o commit. Nenhuma
   decisão de negócio irreversível (conceder acesso, cobrar, emitir certificado) se apoia em cache.
5. **Projeção derivada é read-only e descartável.** Deve poder ser reconstruída dos eventos de origem.
6. **`tenant_id` em toda entidade de negócio desde a Fase 1** (DE10), mesmo mono-tenant: em toda tabela,
   em todo filtro de query, em toda chave de cache e em todo evento. Query sem filtro de tenant é bug de
   segurança, não de dado. A estratégia de isolamento futura (schema por tenant, banco por tenant) fica
   **aberta** — o baseline só garante que nenhuma decisão de hoje a impeça.
7. **Dado pessoal é catalogado por serviço.** Cada serviço que persiste dado pessoal expõe um handler de
   *solicitação do titular* (acesso, correção, exclusão/anonimização), coordenado por `audit`. Nenhum
   serviço decide sozinho apagar dado que sustenta obrigação fiscal.
8. **Auditoria é append-only.** Sem `UPDATE`, sem `DELETE`, escrita apenas por consumo de evento.
9. **Migration é step de deploy**, nunca no boot, e cada tabela tem exatamente um serviço que a migra.

---

## Padrões de Comunicação

### Síncrono (HTTP)

- REST/JSON, contrato OpenAPI gerado pelo passo `tsg-flow-contract-creator` — o contrato é a fonte, não
  a documentação posterior.
- Erro sempre `ProblemDetails`, por um único `IExceptionHandler`, com `traceId`. Mapeamento herdado da
  skill `dotnet`: `ValidationException`→400, `NotFoundException`→404, `EntityValidationException` e
  `RelatedAggregateException`→422, demais→500.
- **O BFF nunca repassa erro interno.** Falha de serviço vira erro de borda com `traceId`; stack, nome de
  serviço e detalhe de infraestrutura não chegam ao browser.
- Resiliência: `AddStandardResilienceHandler` (tentativa 5s, total 20s, 3 retries, sem retry em método
  não idempotente sem chave). 404 de sistema externo vira `null`; o resto propaga.
- **Idempotência obrigatória** em toda escrita disparada por cliente externo, por retry ou por mensagem:
  chave de idempotência aceita e honrada por janela declarada.

### Assíncrono (RabbitMQ)

- Exchange `topic`, filas quorum, DLX/DLQ por fila, `x-delivery-limit`, consumidor com `autoAck: false`.
- Routing key é contrato versionado: `{servico}.{agregado}.{evento}.v{n}`.
- **Outbox obrigatório no produtor**, gravado no mesmo `SaveChangesAsync` do dado. Caso de uso nunca
  publica direto no broker.
- Consumidor idempotente; efeito não idempotente exige inbox.
- **Evento de domínio ≠ evento de integração.** O domínio levanta o evento de domínio; a conversão para
  evento de integração acontece na Application, antes do outbox. `Domain` não referencia `Contracts`.
- Evento carrega **fato e identificadores**, não o agregado inteiro e nunca dado pessoal além do necessário.

### Contratos e versionamento

- DTOs e eventos de integração vivem em pacote NuGet interno versionado (`ProjectName.Contracts`), nunca
  em projeto referenciado entre solutions. Nada de entidade ou `DbContext` ali.
- **Evolução é aditiva:** campo novo é opcional; **nunca** se muda o significado de um campo existente.
  Mudança incompatível = nova versão (`.v2` na routing key, `/v2` na rota) com período de convivência
  declarado e prazo de remoção da anterior.
- API pública do BFF versionada em rota (`/api/v1/...`). API interna entre serviços segue a versão do
  pacote de contratos.
- Quebrar contrato sem versão é incidente, não refactor.

### Frontend (parte técnica)

- **Dois SPAs** React + Vite + TS, conforme a skill `react`: `student-spa` (aluno e visitante) e
  `admin-spa` (backoffice). Separar é consequência da borda: audiências, permissões, perfil de risco e
  ritmo de mudança diferentes. Código comum entra por biblioteca, nunca por rota escondida atrás de papel.
- **O SPA só conhece o seu BFF.** URL de serviço, nome de serviço e topologia nunca aparecem no frontend.
  Uma tela que precise de dados de três serviços recebe **uma** resposta composta pelo BFF.
- **Validação pública de certificado** (Fase 3) é rota anônima no `bff-student`, com rate limit e resposta
  mínima — é a única superfície sem sessão do sistema.
- Configuração que muda por ambiente vai por `window.RUNTIME_ENV` (imagem única, sem rebuild por ambiente).
- React Query é o cache de servidor; dado de API não entra em store global. Erro de API tratado no
  interceptor do `api-client`; error boundary por rota, não um global.
- Telemetria: OpenTelemetry Web com propagação W3C (`traceparent`) até o BFF — um clique do aluno e o
  consumidor de fila que ele acabou gerando pertencem ao mesmo trace.

### Proibido

Comunicação por banco compartilhado, por arquivo, por tabela de "integração", por polling de tabela alheia;
chamada síncrona dentro de transação; HTTP entre módulos do mesmo processo; SPA falando direto com serviço.

---

## Princípios de Segurança

### Sessão e autenticação

**Sessão opaca no BFF: token nunca chega ao browser.**

- O SPA carrega apenas cookie de sessão — `HttpOnly`, `Secure`, `SameSite`, escopo de path mínimo.
- O BFF guarda a sessão em Valkey (TTL deslizante) e troca por **JWT interno de vida curta** ao chamar um
  serviço, com `audience` do serviço alvo.
- Consequência aceita: o BFF tem estado externo e precisa de Valkey; em troca, XSS não rende token, e
  **logout e revogação são imediatos** — propriedade que vale muito num sistema onde o acesso pago é o
  produto.
- **Consequência obrigatória do cookie: proteção CSRF em toda escrita** (token double-submit ou header
  customizado exigido, além de `SameSite`). Cookie sem CSRF é vulnerabilidade, não simplificação.
- `identity` é o único emissor; serviços validam o JWT interno **localmente via JWKS**, sem round-trip.
- **Nem login nem reprodução são limitados na Fase 1.** Nenhum teto de dispositivos, nenhuma trava de
  concorrência, nenhuma sessão derrubada. A plataforma **observa e não age** (BA16). Isso é decisão, não omissão:
  toda restrição que já foi considerada — sessão única, lease de reprodução, limite por IP — cobra o erro do
  aluno legítimo e entrega pouco além do que a marca d'água já entrega.
- **Sinal de desvio, não de concorrência.** O indicador é a sobreposição de **aulas distintas** do mesmo aluno,
  por tempo sustentado e de forma recorrente. Concorrência na mesma aula é ruído e não deve ser reportada.
  Dimensões que afiam o sinal, em ordem de força: cursos diferentes > aulas diferentes > progresso avançando em
  duas trilhas em paralelo > recorrência ao longo de dias.
- **Agregado primeiro, individual só com propósito declarado.** Métrica agregada é medição de produto. Lista de
  alunos suspeitos é **tratamento de dado pessoal para perfilamento**: exige base legal, finalidade, retenção e
  registro em Auditoria e Conformidade antes de existir. Não se materializa caso individual enquanto não houver
  uma ação decidida para ele — investigar sem saber o que se fará com o resultado é criar risco de LGPD sem
  contrapartida.
- **O que se perde, declarado:** observação não dissuade ninguém. Enquanto medimos, quem compartilha continua
  compartilhando. A dissuasão na Fase 1 é a marca d'água com o e-mail, e só. A aposta é que o custo disso é menor
  que o de interromper aluno pagante — e ela é revisável com dado, que é a razão de medir.
- A regra vale para o papel **aluno**. Política de sessão de ator interno do backoffice fica com a revisão de
  segurança do `admin-spa`; não é vetor de pirataria e tem necessidade operacional diferente.

### Autorização

- **RBAC** para papéis internos (aluno, professor, suporte, financeiro, administrador). Papel e permissão
  são claim, resolvidos em `Policies.*`/`Roles.*` — nunca string solta.
- **Divisão de responsabilidade:** o BFF autoriza o grosso (audiência e papel — este usuário pode estar
  nesta área?). O **serviço dono** autoriza o fino (este aluno é dono deste recurso? este direito de acesso
  está vigente?). BFF **não é fronteira de confiança**: o serviço valida de novo, sempre, e nunca aceita
  identidade vinda de header.
- Menor privilégio no backoffice: suporte não vê dado financeiro completo; professor não vê dado financeiro;
  todo ato administrativo com efeito sobre aluno ou dinheiro gera evento de auditoria com autor, momento,
  alvo e motivo.

### Dados e conteúdo

- **Nenhum dado pessoal** em log, span, tag, dimensão de métrica, `data` de health check, payload de erro,
  URL ou routing key. Id em URL é identificador opaco, não CPF nem e-mail.
- Dado de cartão **não trafega e não é persistido** pela plataforma: tokenização no gateway, mantendo o
  escopo PCI mínimo.
- Mídia servida só por **URL assinada de vida curta** vinculada à sessão de reprodução; nenhum objeto de
  vídeo público em S3. Ver *Proteção de conteúdo* abaixo.
### Proteção de conteúdo (sem DRM)

**Não haverá provedor de DRM** — decisão de custo, tomada e aceita (BA15). A proteção passa a ser uma pilha de
medidas baratas, e o que ela entrega é **dissuasão e rastreabilidade**, nunca impedimento. A visão já dizia
"dissuasão, não garantia"; a decisão mantém a natureza da proteção e baixa o teto dela.

- **Nenhum objeto de vídeo público.** Entrega só por URL assinada de vida curta, emitida para uma sessão de
  reprodução individual, depois da decisão de `Entitlement`.
- **HLS com AES-128**, chave servida por endpoint do `media` que exige a mesma sessão. Custo zero e sobe a
  barreira do download trivial. Não é DRM e não deve ser apresentado como tal.
- **Marca d'água dinâmica com o e-mail do aluno**, sobreposta no player e **renderizada no cliente a partir da
  sessão — nunca queimada no arquivo**. Queimar por aluno significa transcodificar por aluno, o que destrói o
  cache compartilhado da CDN e multiplica justamente o custo que a visão registrou como risco. A posição da
  marca varia ao longo da reprodução (recorte fixo não resolve) e ela pertence ao player, não ao layout da página.
- **Nenhuma trava de concorrência na Fase 1. Observar antes de restringir** (BA16). Reprodução simultânea em
  vários dispositivos é, na maior parte, comportamento legítimo — aba esquecida aberta, aluno que troca de
  notebook para celular. Restringir isso cobra o preço do falso positivo **do aluno pagante, no meio da aula**,
  para deter um vazamento que a marca d'água já desencoraja. O sinal que distingue as duas situações não é
  concorrência, é **incoerência de estudo**: duas aulas *diferentes* avançando ao mesmo tempo, de forma
  sustentada e recorrente, não é uma pessoa estudando. É isso que se mede.
- **A medição não exige mecanismo novo.** `media` já informa o avanço da reprodução a Aprendizagem e Progresso
  (dependência registrada no Domain Map). A sobreposição de aulas distintas por aluno é uma **consulta sobre
  dado que já será coletado** — custo próximo de zero e nenhum caminho quente novo. A restrição, se vier, vem
  depois, com limiar tirado de dado real em vez de suposição sobre uma base que ainda tem zero aluno.
- **Não usar IP como sinal de conta compartilhada** (rejeitado em BA16). Erra nos dois sentidos: o celular no 4G
  tem IP diferente do Wi-Fi da mesma casa e troca de IP ao longo do dia, então o uso legítimo mais comum seria
  barrado; e o CGNAT das operadoras brasileiras põe milhares de assinantes atrás do mesmo IPv4, então dois
  estranhos compartilhando conta podem parecer a mesma casa. Some-se que o IP muda sozinho (reconexão, troca de
  torre, Wi-Fi para dados) e que é dado pessoal sob LGPD, com base legal e retenção a sustentar. Se um dia for
  preciso apertar além da reprodução, o sinal correto é **dispositivo** — que é o que "celular, tablet e
  notebook" realmente quer dizer — e não endereço de rede.
- O e-mail exibido é dado pessoal mostrado ao próprio titular — legítimo, e é exatamente aí que mora a dissuasão.
  Mas G10 continua valendo: e-mail **não** entra em log, span, métrica, URL, nome de objeto, chave de cache de
  CDN nem routing key. Ele viaja na sessão e morre no player.
- **Limite honesto, registrado para não ser prometido adiante:** overlay de cliente é removível por quem abre o
  DevTools. Isso dissuade gravação de tela casual e repasse de arquivo — que é o caso comum — e não detém quem
  quer extrair. Nenhuma TechSpec deve prometer mais que isso, e nenhum PRD deve tratar "conteúdo protegido" como
  garantia de exclusividade.

- Superfície pública de certificado revela o mínimo (nome, curso, data, status) e é rate-limited.
- Segredos fora do repositório e da imagem: env var no deploy, `user-secrets` em desenvolvimento.
- Validação de entrada na borda **e** no serviço; a borda filtra, não protege.

---

## Padrões de Observabilidade

- **OpenTelemetry + OTLP** em todo serviço e nos dois SPAs. Uma `ActivitySource` e um `Meter` por serviço,
  `service.name` em todo log e span, `traceparent` propagado do browser ao consumidor de fila.
- Log estruturado sempre, exceção como primeiro argumento, agregado depois de loop. Sem PII (acima).
- `ProblemDetails` carrega `traceId`: todo erro visto pelo usuário é rastreável até o span que o gerou.
- `/health/live` só com `self`; `/health/ready` com as dependências obrigatórias, com timeout, dependência
  opcional como `Degraded` e resposta pública só com o status.
- Métrica nomeada `{servico}.{agregado}.{evento}` com `unit`; **dimensão nunca é Id**.
- **Sinais obrigatórios desde a Fase 1** (estes existem porque a visão registrou o risco, não por hábito):
  outbox atrasado ou esgotado; DLQ não vazia; falha de concessão de acesso após compra confirmada; latência
  e taxa de erro da decisão de `Entitlement`; erro de sessão de reprodução; **custo de storage/CDN por
  aluno ativo**; **sobreposição de reprodução de aulas distintas por aluno** (BA16) — este último é métrica de
  decisão de produto, não alerta operacional: ninguém é acordado de madrugada por ele.
- **SLO por caminho crítico**, não por serviço: (a) pagamento confirmado → acesso liberado; (b) aluno
  autorizado → vídeo começa a tocar. Valores a definir com o negócio junto de A1.
- Coleta, dashboards e roteamento de alerta são do time de plataforma. A aplicação garante **emissão** em
  OTLP e a existência dos sinais acima.

---

## Premissas de Escalabilidade

Declaradas para serem checadas, não para justificar otimização antecipada.

1. **A Fase 1 não tem problema de escala de CPU.** Volume inicial é baixo; o gargalo previsto é **banda e
   custo de CDN de vídeo**, que é justamente o que `media` isola. Otimizar serviço de negócio antes de
   medir é regressão, não melhoria.
2. **Serviços são stateless e escalam horizontalmente.** A única exceção é a sessão dos BFFs, e ela é estado
   **externo** (Valkey) — o processo continua descartável. Nada de afinidade de sessão.
3. **A marca d'água não pode virar custo de escala.** Overlay no cliente preserva um único objeto em cache na
   CDN para todos os alunos. Qualquer proposta futura de marca d'água por sessão no servidor precisa vir com o
   custo de transcodificação e de cache miss medido — é uma decisão de custo disfarçada de decisão de segurança.
4. **Perfis de carga assumidos:** leitura domina catálogo e currículo (cache + CDN resolvem); `Progress` é o
   domínio write-heavy (heartbeat do player) e deve gravar com granularidade controlada — nunca um `INSERT`
   por segundo de vídeo assistido; `Entitlement` é read-heavy e no caminho crítico (por isso o cache de 30s).
5. **Fora do baseline por serem otimização prematura:** event sourcing, CQRS com base de leitura separada,
   sharding, cache de agregado, réplica de leitura dedicada. Qualquer um exige medição e ADR.
6. **Multi-tenant não é problema de escala nesta fase, é restrição de fronteira** (`tenant_id` em tudo). A
   decisão de isolamento físico fica em aberto por design.
7. **Latência não compõe** porque a cadeia síncrona é curta (máx. 1 salto). Esse guardrail é uma premissa de
   escalabilidade tanto quanto uma regra de design: a alternativa é um monolito distribuído cuja latência é
   a soma de todos os saltos.

---

## Guardrails Arquiteturais

Guardrail que depende de alguém lembrar não é guardrail. A coluna **Mecanismo** diz o que falha sozinho.

| # | Regra | Mecanismo |
|---|---|---|
| G01 | Módulo não referencia `Domain`, `Application`, `Infra.*` nem `Api` de outro módulo — só `Contracts` | `ArchitectureTests` |
| G02 | `Domain` não referencia `Contracts` | `ArchitectureTests` |
| G03 | Nenhum HTTP entre módulos do mesmo processo | `ArchitectureTests` + review |
| G04 | Nenhum serviço acessa banco, schema ou tabela de outro | Credencial por serviço (plataforma) + review |
| G05 | Nenhuma FK, join ou view cruzando schema de módulo | Review de migration |
| G06 | Caso de uso não publica no broker; publicação só por outbox | `ArchitectureTests` |
| G07 | `tenant_id` presente em toda entidade de negócio e em todo filtro | `ArchitectureTests` + global query filter |
| G08 | Cadeia síncrona entre serviços limitada a 1 salto | Review + trace (span com profundidade anômala é achado) |
| G09 | Toda escrita externa aceita chave de idempotência | Review de contrato (OpenAPI) |
| G10 | Nenhum dado pessoal em log, span, métrica ou payload de erro | Máscara + review + gate de release |
| G11 | Decisão de acesso nunca é resolvida por réplica local nem por cache além do TTL | Review + `ArchitectureTests` (só `Entitlement` expõe o caso de uso de decisão) |
| G12 | `analytics` não escreve em domínio algum e não é consultado por nenhum | Review + credencial read-only |
| G13 | Auditoria é append-only e escrita só por consumo de evento | Permissão de banco (sem `UPDATE`/`DELETE`) |
| G14 | Mudança de contrato é aditiva; incompatível exige nova versão | Diff de OpenAPI e do pacote `Contracts` na CI |
| G15 | Frontend não conhece URL nem nome de serviço; só o BFF | Lint/review + `config/paths.ts` |
| G16 | Dado de servidor não entra em store global no SPA | Review (regra da skill `react`) |
| G17 | Nenhuma chamada de rede dentro de transação de banco | Review + trace |
| G18 | Escrita autenticada por cookie exige proteção CSRF | Teste end-to-end de borda |
| G19 | Nenhum domínio novo sem entrada no Domain Map | Gate do fluxo TSG |
| G21 | Nenhum objeto de vídeo acessível sem URL assinada e sessão de reprodução válida | Teste end-to-end de `media` + política de bucket |
| G22 | Marca d'água renderizada no cliente; nenhum artefato de vídeo derivado por aluno | Review + alerta de cache hit ratio da CDN |
| G23 | E-mail do aluno não aparece em log, URL, nome de objeto, chave de cache nem routing key | Máscara + review (extensão de G10) |
| G24 | Nenhuma restrição de concorrência de sessão ou de reprodução sem decisão explícita com dado que a justifique | Review de PRD e de TechSpec |
| G25 | Detecção de compartilhamento é agregada; caso individual só com finalidade, base legal e retenção registradas | Review + Auditoria e Conformidade |
| G26 | IP não é usado como sinal de autorização, de sessão ou de detecção de compartilhamento | Review (decisão registrada em BA16) |
| G20 | PRD que confunde "direito de acesso" (comercial) com "liberação progressiva" (pedagógica) é recusado | Revisão de PRD |

### Camadas anticorrupção obrigatórias

| Externo | Domínio dono | Regra |
|---|---|---|
| Gateway de pagamento | Cobrança e Assinatura | Estado e código do gateway traduzidos para o nosso vocabulário; webhook entra por endpoint dedicado, validado por assinatura e idempotente |
| Provedor de NFS-e | Fiscal | Regra municipal não vaza para nenhum outro domínio; falha nunca propaga para venda ou acesso |
| CDN e armazenamento de mídia | Entrega de Mídia e Proteção | Nenhum outro domínio conhece o provedor nem o formato de entrega; mudar CDN, empacotamento ou estratégia de proteção é mudança interna de `media` |
| WhatsApp / e-mail | Notificação | Template e aprovação externa são detalhe de `notification`; nenhum domínio monta mensagem de canal |
| Provedor de identidade futuro (SSO) | Identidade e Acesso | Fora do escopo hoje; a fronteira já isola a decisão |

---

## Decisões Registradas

| # | Decisão | Racional | Origem |
|---|---|---|---|
| BA01 | Microsserviços com serviço internamente modular (módulo = domínio, schema por módulo) | Determinação top-down de microsserviços, com custo operacional compatível com 2 engenheiros e extração posterior sem redesenho | Decisão do time + risco de escopo da visão |
| BA02 | Grupo inicial com 6 serviços + 2 BFFs | Agrupamento por afinidade de mudança e de ritmo de deploy. `notification` entra no grupo inicial por dependência de `CAP-001` e `CAP-011`; a ordem de nascimento é do backlog, não deste baseline | Decisão do time (2026-09-20) |
| BA03 | `audit` é serviço próprio mesmo mínimo | Auditoria precisa estar fora do alcance de quem pratica o ato | DE13 |
| BA04 | `Entitlement` é módulo com schema e contrato próprios, primeiro candidato a extração | Nó mais consultado do sistema; risco DE01 de virar flag de checkout | DE01 |
| BA05 | BFF por audiência (aluno e backoffice), SPA nunca fala com serviço | Superfícies com permissão, risco e ritmo diferentes; topologia não vaza para o frontend | Decisão do time |
| BA06 | Sessão opaca no BFF; token nunca no browser | Revogação imediata e imunidade a roubo de token por XSS, ao custo de estado externo no BFF | Decisão do time |
| BA07 | Decisão de acesso por consulta síncrona ao dono + cache de até 30s, falha fechada | Fonte de verdade única; suspensão e revogação com efeito imediato | Decisão do time + DE01 |
| BA08 | Cadeia síncrona máxima de 1 salto entre serviços | Impedir monolito distribuído e composição de latência e falha | Consequência de BA01 |
| BA09 | Evento para fato consumado; síncrono só para decisão no caminho crítico | Direção de dependência estável e desacoplamento temporal | Domain Map |
| BA10 | `tenant_id` em toda entidade desde a Fase 1 | Mono-tenant sem decisão irreversível | DE10 |
| BA11 | Fiscal como módulo de `billing`, consumido por evento | Assimetria de criticidade preservada sem serviço extra | DE06 + candidato a fusão do Domain Map |
| BA12 | Avaliação como módulo de `learning`; `certification` como serviço | Avaliação compartilha ciclo com progresso; certificação tem consumidor público não autenticado | Domain Map (candidatos a fusão) |
| BA13 | Contratos em pacote NuGet interno versionado, evolução aditiva | Evitar acoplamento de build entre serviços e quebra silenciosa | Skill `dotnet` |
| BA14 | CI/CD, infra AWS e design visual fora deste baseline | Donos são time de plataforma e time de design | Decisão do time |
| BA16 | Nenhuma restrição de login ou de reprodução na Fase 1. Medir a sobreposição de **aulas distintas** por aluno e decidir depois com dado. **Sessão única, lease de reprodução e limite por IP rejeitados** | Concorrência é majoritariamente comportamento legítimo; restringi-la cobra o erro do aluno pagante em troca de dissuasão marginal sobre a marca d'água. O sinal de compartilhamento não é concorrência, é incoerência de estudo — e ele é derivável do avanço de reprodução que já será coletado, sem mecanismo novo | Decisão do time (2026-09-20) |
| BA15 | Sem provedor de DRM. Proteção = URL assinada + HLS AES-128 + marca d'água com e-mail do aluno no cliente. **Nenhuma trava de concorrência** — sessão única, lease de reprodução e limite por IP foram rejeitados em BA16 | Restrição de custo declarada. Mantém a proteção como dissuasão, que é o que a visão já assumia, sem criar transcodificação por aluno. A pilha é composta só de medidas que não cobram do aluno legítimo o erro de quem compartilha | Decisão do time (2026-09-20) |

Nenhuma destas exigiu ADR: todas nascem aqui com racional autocontido e sem decisão anterior a substituir.
**Alterar BA01, BA05, BA06, BA07, BA08 ou BA15 exige ADR em `docs/adr/`** — são as que, mudadas depois, invalidam
TechSpecs já escritas.

---

## Pontos em Aberto

| # | Questão | Dono | Bloqueia |
|---|---|---|---|
| AB01 | Estratégia de isolamento físico multi-tenant (schema ou banco por tenant) | Arquitetura | Fase 5; o baseline só garante que nada a impeça |
| AB02 | Valores de SLO dos dois caminhos críticos | Negócio (junto de A1 da visão) | Alertas com limiar; não bloqueia a Fase 1 |
| AB03 | Política de retenção e anonimização de dado de aluno (LGPD) | Negócio + Auditoria | Fase que tratar dado em escala; não a Fase 1 |
| AB04 | Limiar de sobreposição que caracteriza compartilhamento, e **que ação tomar** ao encontrá-lo (nada, aviso ao aluno, contato do suporte, restrição) | Negócio, com a métrica de BA16 em mãos | Nada na Fase 1 — é o que a medição existe para responder. Definir a ação **antes** de materializar caso individual, por causa de G25. Se a restrição voltar à mesa, o sinal é contagem de dispositivos, nunca IP |
| AB05 | Confirmação das dependências de plataforma listadas no início | Time de plataforma | Ir a produção na Fase 1 |

---

*Baseline gerado com o agente `tsg-flow-architecture-baseline`. Próximo passo sugerido:
`tsg-flow-capability-backlog` ou `tsg-flow-domain-creator` para o primeiro domínio da Fase 1.
Revisar este documento apenas quando uma premissa estrutural mudar.*

---

## Histórico de Revisões

| Versão | Data | Autor | Alterações |
|---|---|---|---|
| 1.0 | 2026-09-20 | Tasso Gomes | Baseline inicial (BA01–BA16, G01–G26) sobre `vision.md` v1.1 e `context/domain-map.md` v1.1 |
| 1.2 | 2026-09-20 | Tasso Gomes | Correção de contradição interna: BA15 listava "limite de sessões simultâneas" na pilha de proteção, enquanto BA16 rejeita sessão única e lease de reprodução e G24 proíbe restrição de concorrência sem decisão com dado. BA15 passa a declarar a ausência de trava e a remeter a BA16. A prosa de "Proteção de conteúdo" já estava correta; a divergência era só da tabela. Nenhuma decisão de mérito mudou — achado do domain doc de Identidade e Acesso (QA-02) |
| 1.1 | 2026-09-20 | Tasso Gomes | O roadmap de serviços por fase vira agrupamento em unidades de deploy: o baseline decide agrupamento e gatilho de extração, não sequência — ele é escrito antes do backlog e não enxerga dependência entre capacidades. `notification` passa ao grupo inicial (BA02: 6 serviços), porque `CAP-001` e `CAP-011` não fecham ciclo sem e-mail transacional |
