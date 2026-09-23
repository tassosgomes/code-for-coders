# API interna — BFF do aluno → Identity

> Derivado de [internal-api-contract.yaml](internal-api-contract.yaml), OpenAPI 3.1.0, contrato v1.0.0. Acordo aprovado para CAP-001 em 2026-09-22.

Somente o BFF do aluno consome esta interface. Toda chamada usa TLS e uma asserção curta de serviço assinada pelo BFF, inclusive antes do login. A asserção identifica serviço e tenant autorizado, não aluno. Identity valida assinatura, `kid`, emissor, audiência, escopo, validade e `jti` contra replay. O JWT de aluno é emitido apenas por Identity e nunca aparece na API pública ou no cookie do navegador.

| Método | Rota sob `/internal/v1` | operationId | Papel |
|---|---|---|---|
| POST | `/student-accounts` | `createStudentAccountInternal` | conta não confirmada e pedido de e-mail |
| POST | `/account-confirmations` | `confirmStudentAccountInternal` | consumo único do token |
| POST | `/account-confirmation-requests` | `requestAccountConfirmationInternal` | novo pedido deliberado |
| POST | `/student-sessions` | `createStudentSessionInternal` | autenticação e sessão lógica em Identity |
| POST | `/student-session-validations` | `validateStudentSessionInternal` | vigência, renovação e JWT opcional por audiência |
| POST | `/student-session-revocations` | `revokeStudentSessionInternal` | revogação da sessão corrente |
| POST | `/password-reset-requests` | `requestPasswordResetInternal` | pedido neutro de recuperação |
| POST | `/password-resets` | `resetStudentPasswordInternal` | redefinição e revogação das sessões |
| POST | `/password-changes` | `changeStudentPasswordInternal` | troca autenticada e revogação das outras sessões |

Todos os corpos, campos e respostas têm o YAML como fonte. Todas as escritas de negócio levam `Idempotency-Key` propagada do BFF, com janela de 24 horas; a validação de sessão é uma operação interna repetível que não cria outra sessão. Identity guarda a intenção e seu resultado lógico junto da transação de negócio. A chave e o fingerprint do corpo são armazenados sem segredo em claro. Repetição com chave e corpo iguais preserva o mesmo efeito; corpo diferente recebe `IDEMPOTENCY_CONFLICT`. O replay de login retorna o mesmo `sessionId` interno. O BFF pode reemitir um cookie opaco próprio para essa sessão, mas a repetição nunca a reativa após revogação.

Identity valida a política de toda senha **nova**: mínimo de oito caracteres, uma letra maiúscula, uma minúscula, um dígito e um símbolo; espaços não contam como símbolo. O BFF e a SPA podem orientar antes do envio, mas a decisão é do domínio. A senha atual na troca pode ter sido criada antes da política e só precisa corresponder à Credencial existente.

`validateStudentSessionInternal` é chamado antes de cada ação protegida; valida e renova a vigência em Identity. Com `audience`, emite JWT interno curto para serviço permitido; sem ela, retorna somente identidade e expiração. O BFF renova seu registro em Valkey após o sucesso. Falha de Identity ou Valkey fecha o acesso.

Este documento registra apenas o recorte de CAP-001. Não foi encontrado contrato interno anterior nem implantação a comparar; compatibilidade operacional permanece não verificada. A implementação deve testar asserção inválida, `jti` repetido, tenant/audiência fora de escopo, replays e falha após commit, além de revogação na próxima ação protegida.
