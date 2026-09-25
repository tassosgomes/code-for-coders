# ADR-0005: Sessão do ator interno e autenticação de serviço do BFF do backoffice

## Status

Accepted

## Identidade e escopo

- Caminho: `docs/adr/0005-sessao-e-servico-do-backoffice.md`
- Domínios/componentes afetados: Identidade e Acesso, BFF do backoffice (`bff-admin`), serviços que recebem chamadas do backoffice (primeiro: `commerce`)
- Origem histórica: `tasks/prd-acesso-interno`, CAP-002
- Substitui: Nenhuma. Estende ao backoffice as decisões de [ADR-0003](0003-verificacao-de-sessao-do-aluno.md) e [ADR-0004](0004-autenticacao-de-servico-bff-identity.md), que declararam expressamente não decidir o backoffice

## Data

2026-09-25

## Contexto

O backoffice passa a ter atores internos com papéis (professor, suporte, financeiro, administrador) que se acumulam, e cada papel agrupa permissões. Duas regras do domínio mandam no desenho: revogar um papel vale **na próxima decisão de autorização** e encerra as sessões do ator (RN-17), e papel/permissão trafegam como claim, nunca como string solta (RN-18). O baseline exige sessão opaca no BFF sem token no navegador (BA06), autorização em duas camadas — BFF no grosso, serviço dono no fino, sem confiar no BFF — e JWT interno curto emitido só por Identity, validado localmente via JWKS.

ADR-0003 resolveu revogação imediata para o aluno com consulta à Identity a cada ação protegida; ADR-0004 resolveu a autenticação da chamada BFF do aluno → Identity com asserção assimétrica. Ambas limitaram o escopo ao aluno. No código atual, a verificação de asserção em Identity aceita **um único emissor** (`ServiceAssertionOptions.Issuer`, configurado como `bff-student`), e nenhum serviço de domínio ainda valida JWT de usuário nem Identity expõe JWKS.

## Decisão

1. **Vigência da sessão do ator interno é decidida em Identity a cada ação protegida do backoffice**, como na ADR-0003. O `bff-admin` guarda só o identificador opaco em Valkey e, antes de toda ação protegida, pede a Identity que valide e renove a sessão. A resposta traz os papéis e a permissão efetiva **vigentes naquele momento** e, quando a ação vai a um serviço, um JWT curto com audiência desse serviço. Falha fecha o acesso. A sessão do ator interno é entidade própria em Identity, separada da sessão do aluno.
2. **Revogar, trocar papel ou redefinir senha encerra as sessões do ator no mesmo commit da mudança** em Identity. A próxima ação admitida depois do commit já é recusada.
3. **O `bff-admin` autentica-se em Identity com asserção de serviço própria**, no mecanismo da ADR-0004, com chave, `kid` e emissor exclusivos (`bff-admin`). Identity passa a aceitar **mais de um emissor**, cada um com o seu conjunto de chaves públicas e de **escopos permitidos**: o `bff-student` não obtém escopo de backoffice nem o `bff-admin` escopo de aluno.
4. **O JWT do ator interno carrega papéis e permissões como claims** (além de `sub`, `tenantId`, `sessionId`, `aud`, `scope`, `exp`, `jti`), com vida curta (a mesma configuração dos tokens de aluno, 5 minutos por padrão). **Identity publica as chaves públicas de assinatura em JWKS** na rede interna, e o serviço dono valida o token localmente — assinatura, emissor, audiência, validade — e decide pela permissão do claim. Nenhum serviço consulta Identity para autorizar.
5. A decisão vale para toda chamada do backoffice a serviço de domínio a partir de agora; o primeiro serviço é `commerce`, na área financeira reservada.

## Alternativas Consideradas

### Alternativa 1: Papéis e permissões guardados na sessão do BFF

- **Descrição:** o BFF copia papéis/permissões para o Valkey no login e autoriza localmente.
- **Prós:** nenhuma chamada extra por ação.
- **Contras:** a revogação só vale quando a cópia expira; exige invalidar Valkey a partir de Identity, sem transação entre os dois.
- **Por que rejeitada:** viola RN-17 (efeito na próxima decisão) — o mesmo motivo que rejeitou revogação por evento e índice compartilhado na ADR-0003.

### Alternativa 2: Serviço de domínio consulta Identity para autorizar

- **Descrição:** o serviço dono pergunta a Identity se o ator tem a permissão.
- **Prós:** decisão sempre atual no serviço.
- **Contras:** acrescenta um salto síncrono a toda chamada, transforma Identity em gargalo do caminho quente (RF-03 do domain doc) e contraria o baseline (validação local via JWKS).
- **Por que rejeitada:** a vigência já é garantida na borda por (1); o serviço recebe um token emitido segundos antes.

### Alternativa 3: Reusar a asserção e a chave do `bff-student`

- **Descrição:** um emissor único para as duas bordas.
- **Prós:** nenhuma mudança no verificador.
- **Contras:** vazamento da chave de uma borda compromete a outra; impossível restringir escopos por borda.
- **Por que rejeitada:** as duas superfícies têm audiência e risco diferentes (BA05).

## Consequências

### Positivas

- Revogação de papel, troca de papel e redefinição de senha barram o ator na próxima ação, mesmo com reinício do BFF.
- O serviço dono autoriza sem depender de Identity em tempo de execução e sem confiar no BFF.
- Cada borda tem credencial e escopos próprios; comprometer uma não abre a outra.

### Negativas

- Cada ação protegida do backoffice soma uma chamada `bff-admin` → Identity.
- Identity passa a expor JWKS e a manter configuração de vários emissores; a rotação de chaves cresce em duas frentes.
- Um JWT já emitido continua válido no serviço até expirar (até 5 minutos). O serviço não é alcançável pelo navegador; a garantia de RN-17 vale na borda, que não reemite token para sessão encerrada.

### Riscos

- **Latência/indisponibilidade de Identity:** timeout, circuit breaker e falha fechada; medir latência da validação da sessão interna separadamente da do aluno.
- **Escopo de emissor mal configurado:** validação na partida — cada emissor com ao menos uma chave e um escopo; teste que recusa asserção do `bff-student` em operação de backoffice e vice-versa.
- **Claims desatualizados em token longo:** manter a vida do JWT curta e não criar cache de token no BFF além da requisição corrente.

## Referências

- [Baseline arquitetural](../../context/architecture-baseline.md) — BA05, BA06, Autorização, JWKS.
- [Domínio Identidade e Acesso](../../domains/identidade-e-acesso/domain.md) — RN-12, RN-16 a RN-18, RF-03.
- [ADR-0003](0003-verificacao-de-sessao-do-aluno.md) e [ADR-0004](0004-autenticacao-de-servico-bff-identity.md).
- [TechSpec de origem](../../tasks/prd-acesso-interno/techspec.md).
