# ADR-0013: JWT de aluno validado localmente por serviços de domínio, com o e-mail do aluno só na audiência `media`

## Status

Accepted

## Identidade e escopo

- Caminho: `docs/adr/0013-jwt-de-aluno-validado-por-servicos-de-dominio.md`
- Domínios/componentes afetados: Identidade e Acesso (`identity`: emissão e JWKS), `media` e `learning` (validação), `bff-student` (obtenção por rota)
- Origem histórica: `tasks/prd-reproducao-protegida`, `CAP-007` (decisão C-04 do conjunto de contratos)
- Substitui: Nenhuma. Estende a [ADR-0003](0003-verificacao-de-sessao-do-aluno.md), que decidiu que o BFF obtém "JWT interno curto com audiência do serviço alvo, com validação local por JWKS no serviço destinatário", sem tê-lo exercido ainda para um serviço de domínio

## Data

2026-10-03

## Contexto

A ADR-0003 define que, depois de validar a sessão do aluno em Identity, o BFF pede um JWT interno curto com a audiência do serviço de destino, e que o serviço de destino o valida localmente pelo JWKS de Identity. Hoje nenhum serviço de domínio recebe esse JWT: `media` e `learning` validam só o JWT de ator interno (a chave de assinatura do ator, `StaffSessionTokens`, publicada no JWKS), e a emissão do JWT de aluno (`StudentSessionTokens`) existe no código sem audiência configurada e **sem a chave pública publicada**.

`CAP-007` faz `media` e `learning` receberem requisições do aluno. Há três consequências a decidir:

1. **Como `media` e `learning` validam o JWT de aluno.** O JWKS publicado por Identity (`UserTokenSigningKeySet`) só contém a chave de ator. Sem a chave de aluno ali, a validação local é impossível.
2. **Como não confundir ator interno e aluno.** Os dois tokens têm o mesmo emissor (`identity`) e podem ter a mesma audiência (`media`, `learning`). Um token de ator não pode abrir sessão de reprodução, e um de aluno não pode enviar vídeo.
3. **Como a marca d'água recebe o e-mail do aluno.** O player precisa do e-mail para compô-lo sobre o vídeo (Identidade RN-21, RN-M12). A sessão do BFF guarda `accountId` e `name`, não o e-mail. O e-mail é dado pessoal sob G10 e G23: só pode existir onde a marca d'água o exige.

## Decisão

1. **O JWKS de Identity publica também a chave pública de aluno**, com `kid` próprio, ao lado da chave de ator. As opções de aluno ganham `PreviousSigningPublicKeys`, no mesmo formato das de ator, para rotação com sobreposição. `media` e `learning` validam o JWT de aluno **localmente**, sem consultar Identity (ADR-0003).
2. **O JWT de aluno é para uma audiência e um escopo.** `identity` emite com `audience` = `media` ou `learning` e `scope` configurado por audiência (`StudentSessionTokens:AudienceScopes`: `media` → `playback:use`, `learning` → `lessons:read`). Vida de 5 minutos, claims `iss`, `aud`, `sub` (= `studentId`), `tenantId`, `sessionId`, `scope`, `iat`, `nbf`, `exp`, `jti`.
3. **Separação ator × aluno nas rotas de domínio.** As rotas de aluno exigem a política de aluno: claim `scope` igual ao escopo da audiência **e ausência da claim `permissions`**. As rotas de ator seguem exigindo a permissão que já exigem (`midia.enviar`, `autoria.*`), que um token de aluno nunca carrega. Um token de ator não tem o escopo de aluno, e um de aluno não tem permissão de ator.
4. **O e-mail do aluno é uma claim `email`, emitida só na audiência `media`** (`StudentSessionTokens:EmailAudiences`). Em `learning` e em qualquer outra audiência a claim não existe. É a exposição declarada de Identidade RN-21, junto com a de Notificação (RN-26). `media` a repassa ao player em `watermark.text`, e **nunca a persiste, loga, mede ou publica**. Um JWT de `media` sem a claim impede a abertura da sessão (`WATERMARK_UNAVAILABLE`): sem marca, sem reprodução.
5. **O `studentId` que `media` e `learning` usam, inclusive na consulta de decisão (ADR-0012), é a claim `sub` do JWT que validaram.** Nenhuma rota aceita identificador de aluno por parâmetro, corpo ou cabeçalho.
6. **O BFF pede o JWT com a audiência da rota**, uma validação de sessão em Identity por requisição protegida, sem cache de JWT (ADR-0003). A tabela de rotas e audiências é configuração do BFF.

## Alternativas Consideradas

### Alternativa 1: Identity devolver o e-mail em chamada separada, por requisição

- **Descrição:** `media` chamaria Identity para obter o e-mail na abertura e na renovação.
- **Prós:** o e-mail não viaja em token.
- **Contras:** um salto síncrono a mais no caminho crítico "aluno autorizado → vídeo começa" (G08), e `media` ganharia uma segunda dependência síncrona.
- **Por que rejeitada:** o JWT já é o veículo de identidade entre BFF e serviço, curto, restrito a uma audiência e validado localmente.

### Alternativa 2: E-mail em todo JWT de aluno

- **Descrição:** uma claim `email` em qualquer audiência.
- **Contras:** espalha o dado pessoal por serviços que não precisam dele (`learning`, depois `commerce`), contra G10 e G23.
- **Por que rejeitada:** a exposição deve ser a menor possível; só a marca d'água a exige.

### Alternativa 3: O BFF guardar o e-mail na sessão e enviá-lo por cabeçalho

- **Descrição:** o e-mail ficaria na sessão opaca do BFF (Valkey) e iria a `media` em cabeçalho.
- **Contras:** `media` aceitaria um cabeçalho que o BFF controla, sem assinatura, e o e-mail passaria a persistir em Valkey, um local novo de dado pessoal.
- **Por que rejeitada:** o token assinado por Identity é a única fonte confiável do e-mail para `media`.

### Alternativa 4: Chaves separadas por tipo de token, sem publicar a de aluno no JWKS

- **Descrição:** `media` e `learning` obteriam a chave de aluno por configuração própria.
- **Contras:** duas formas de distribuir chave pública de Identity; rotação manual em cada serviço.
- **Por que rejeitada:** o JWKS já é o mecanismo.

## Consequências

### Positivas

- `media` e `learning` ganham uma autenticação do aluno sem round-trip a Identity.
- A exposição do e-mail fica em uma claim, em uma audiência, com uma finalidade.
- A rotação da chave de aluno segue o mesmo formato da de ator.

### Negativas

- Tokens de ator e de aluno convivem no mesmo JWKS e no mesmo emissor; a separação depende de `scope` e `permissions`, e precisa de teste.
- Um segundo par de chaves de assinatura de Identity para provisionar localmente (`scripts/generate-local-env.sh`).
- Cada requisição protegida do aluno soma uma validação de sessão em Identity (já assumida pela ADR-0003) e a emissão de um JWT.

### Riscos

- **Token de ator aceito em rota de aluno, ou o contrário.** Mitigação: política de aluno exige `scope` e ausência de `permissions`; testes de integração dos dois sentidos em `media` e `learning`.
- **E-mail em log, métrica, trace ou URL.** Mitigação: nenhuma log do conteúdo de token; busca do e-mail de teste em toda saída de telemetria faz parte da verificação.
- **Custo das validações de sessão no caminho de playlist, chave e avanço.** Mitigação: medir a latência de `validateStudentSessionInternal` e a taxa por aluno ativo; qualquer cache que permita sessão revogada por mais tempo exige nova decisão (ADR-0003).

## Notas de Implementação

- O contrato de `validateStudentSessionInternal` revisado está em `tasks/prd-reproducao-protegida/internal-api-contract-identity.yaml` (Identity 1.1.0): audiências `media` e `learning`, claim `email` só em `media`.
- A claim `email` sai do mesmo registro de conta que a sessão já consulta ao renová-la (`RenewActiveSessionAsync`), sem consulta adicional.
- O papel de `media` e `learning` como chamadores de `commerce` é da ADR-0012.

## Referências

- [ADR-0003](0003-verificacao-de-sessao-do-aluno.md), [ADR-0005](0005-sessao-e-servico-do-backoffice.md), [ADR-0012](0012-autenticacao-de-servico-media-learning-em-commerce-revisada.md).
- [Baseline arquitetural](../../context/architecture-baseline.md) — G10, G23, "Proteção de conteúdo".
- [Domínio Identidade e Acesso](../../domains/identidade-e-acesso/domain.md) — RN-21, RN-26.
- Origem histórica: `tasks/prd-reproducao-protegida/contracts.md` (C-04).
