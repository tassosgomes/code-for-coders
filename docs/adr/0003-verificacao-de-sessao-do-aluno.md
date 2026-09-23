# ADR-0003: Verificação de vigência da sessão do aluno em Identity

## Status

Accepted

## Identidade e escopo

- Caminho: `docs/adr/0003-verificacao-de-sessao-do-aluno.md`
- Domínios/componentes afetados: Identidade e Acesso, BFF do aluno, validação de sessão e emissão de JWT interno
- Origem histórica: `tasks/prd-conta-aluno`, CAP-001
- Substitui: Nenhuma; propõe exceção explícita à regra geral de `context/architecture-baseline.md` que dispensa consulta a Identity por request

## Data

2026-09-22

## Contexto

O sistema exige sessão opaca em Valkey no BFF, JWT interno curto emitido apenas por Identity e revogação imediata por logout ou alteração de senha. A Conta, a Credencial e a Sessão pertencem ao domínio Identidade e Acesso. Após alteração de senha, todas as outras sessões precisam falhar na próxima ação protegida, inclusive se um BFF reiniciar ou uma mensagem atrasar.

O baseline também diz que Identity não deve ser consultado em cada request e que os serviços validam JWT localmente via JWKS. Essas regras não explicam como o BFF descobre revogação de todas as sessões após mudança na base de Identity com efeito imediato. O estado atual do BFF armazena uma sessão opaca com token de upstream no Valkey, mas não há integração de revogação ou renovação deslizante. Uma mensagem assíncrona não garante efeito imediato; gravar no banco de Identity e no Valkey do BFF não é transação atômica.

## Decisão

Para a sessão de **aluno** de CAP-001, Identity será a fonte de verdade da vigência e revogação. O BFF verificará a sessão opaca em Valkey **e** chamará Identity para validar e renovar atomicamente a vigência antes de cada ação protegida. Só então renovará o TTL no Valkey e pedirá/encaminhará JWT interno curto com audiência do serviço alvo. Falha de qualquer verificação fecha o acesso e não renova a sessão no BFF. A consulta BFF → Identity é a exceção à regra geral de não consultar Identity por request; serviços de domínio continuam validando JWT localmente via JWKS, sem consultar Identity.

Logout revoga a sessão em Identity antes de considerar a operação concluída e remove o registro operacional no Valkey. Troca/redefinição de senha revoga as demais sessões no mesmo commit da Credencial em Identity. Uma request já admitida antes desse commit pode terminar; a garantia é para a próxima ação protegida após o commit. A API interna precisa declarar autenticação de serviço, tenant, identificador opaco, resultado da verificação, expiração e falhas, sem expor o JWT ao browser.

Esta decisão não aplica limite de sessões, nem altera a separação entre identidade de aluno e direito de assistir a curso. Não habilita outros serviços a ler tabelas de Identity ou a usar o Valkey do BFF como fonte de verdade.

## Alternativas consideradas

### Revogação por evento de integração

- **Prós:** BFF decide localmente e evita latência de consulta por request.
- **Contras:** atraso, reentrega e indisponibilidade do broker permitem que uma sessão revogada continue aceita após o commit.
- **Por que rejeitada:** não atende revogação imediata nem o cenário de falha parcial.

### Índice de revogação compartilhado no Valkey

- **Prós:** consulta local rápida no caminho do BFF.
- **Contras:** exige gravar estado de segurança em dois donos/armazenamentos, sem commit atômico entre PostgreSQL de Identity e Valkey do BFF; falha entre escritas deixa divergência.
- **Por que rejeitada:** introduz uma segunda fonte de decisão difícil de reconciliar com garantia imediata.

## Consequências

### Positivas

- Uma mudança de senha revoga outras sessões no mesmo commit e a próxima ação protegida as rejeita.
- Reinício do BFF, atraso de evento ou token interno ainda válido não restauram a sessão revogada na borda.

### Negativas

- Cada ação protegida do aluno soma uma chamada BFF → Identity e depende da sua disponibilidade.
- A exceção ao baseline precisa ser observada e revista se a carga real a tornar inviável.

### Riscos

- **Latência/indisponibilidade de Identity:** medir latência e taxa de falha da verificação; aplicar timeout, circuit breaker e fail closed. Não repetir uma operação de escrita sem chave de idempotência.
- **Janela de request em andamento:** definir a garantia como próxima ação admitida após commit e testar essa fronteira; JWT de vida curta não é prova independente de sessão ativa no BFF.
- **Crescimento de carga:** medir antes de otimizar; qualquer cache que permita sessão revogada por mais tempo exige nova decisão explícita.

## Notas de implementação

- Registrar interface BFF → Identity em OpenAPI interno aprovado antes de implementar; autenticar a chamada mesmo quando o aluno ainda não fez login.
- O BFF deve verificar a sessão antes de emitir/obter JWT ou encaminhar request protegida. Sessão em Valkey não é prova suficiente de vigência.
- JWT interno continua curto e específico para a audiência, com validação local por JWKS no serviço destinatário.
- Configurar prazo de inatividade e testar revogação de uma entre várias sessões, troca de senha, redefinição, falha de Identity e falha de Valkey.

## Referências

- [Baseline arquitetural](../../context/architecture-baseline.md) — BA05/BA06, seção Sessão e autenticação, comunicação síncrona e G18.
- [Domínio Identidade e Acesso](../../domains/identidade-e-acesso/domain.md) — Sessão, RN-07 a RN-09.
- [ADR-0001](0001-monorepo-de-codigo.md) — serviços e dados independentes.
- [TechSpec de origem](../../tasks/prd-conta-aluno/techspec.md) — fatias V-03 a V-05.
