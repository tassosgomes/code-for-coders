# API HTTP — conta e autenticação do aluno

> Derivado de [api-contract.yaml](api-contract.yaml), versão 1.0.0, OpenAPI 3.1.0. Recorte do PRD v1.1 (2026-09-22). Estado: Aprovado para implementação em 2026-09-22.

O SPA usa apenas o BFF do aluno em `/api/v1`. Sessão por cookie opaco; o BFF não entrega JWT ao navegador. Campos, respostas, erros e exemplos têm como fonte o YAML.

| Método | Rota | Operação | Sucesso | Segurança |
|---|---|---|---|---|
| `POST` | `/student-accounts` | `registerStudent` — Cadastrar conta de aluno | 202 | pública |
| `POST` | `/account-confirmations` | `confirmStudentAccount` — Confirmar e-mail | 204 | pública |
| `POST` | `/account-confirmation-requests` | `requestAccountConfirmation` — Pedir novo link de confirmação | 202 | pública |
| `POST` | `/student-sessions` | `createStudentSession` — Entrar como aluno | 200 | pública |
| `GET` | `/student-sessions/current` | `getCurrentStudentSession` — Consultar sessão atual | 200 | cookie de sessão |
| `DELETE` | `/student-sessions/current` | `endCurrentStudentSession` — Sair da sessão atual | 204 | cookie de sessão |
| `POST` | `/password-reset-requests` | `requestPasswordReset` — Solicitar recuperação de senha | 202 | pública |
| `POST` | `/password-resets` | `resetStudentPassword` — Redefinir senha por link | 204 | pública |
| `POST` | `/password-changes` | `changeStudentPassword` — Trocar senha autenticado | 204 | cookie de sessão |

## Regras de integração

- Todas as escritas aceitam `Idempotency-Key` com janela de 24 horas, aprovada em 2026-09-22. Repetição com a mesma chave e corpo mantém o resultado lógico; corpo divergente falha. No login, o cookie pode ser reemitido para a mesma sessão lógica, mas nunca reativa sessão já revogada.
- `GET /student-sessions/current` fornece `csrfToken`; `DELETE /student-sessions/current` e `POST /password-changes` exigem `X-CSRF-Token`. O BFF protege também origem e uso do cookie.
- Cadastro de e-mail duplicado retorna `ACCOUNT_ALREADY_EXISTS`; login errado responde `INVALID_CREDENTIALS` sem distinguir endereço inexistente de senha errada. Conta não confirmada só recebe `EMAIL_NOT_CONFIRMED` após credencial correta.
- Nova senha em cadastro, recuperação ou troca exige no mínimo oito caracteres, letra maiúscula, letra minúscula, dígito e símbolo; espaços não contam como símbolo. A senha atual na troca é verificada como credencial existente, sem reaplicar a política nova.
- Pedido de recuperação devolve sempre 202 sem corpo, independentemente da existência da Conta. Tokens de confirmação e recuperação viajam no corpo JSON da API, nunca na URL da API.
- Logout revoga a sessão corrente. Redefinição e troca de senha encerram as demais sessões; a sessão corrente da troca permanece válida.
- Erros seguem RFC 9457 em `application/problem+json`, com `code` estável e `traceId`; textos podem mudar. O BFF não repassa detalhes internos.

## Exemplo derivado do contrato

```json
{"email":"aluno@example.com"}
```

`POST /password-reset-requests` responde 202 sem corpo para conta elegível e inexistente.

## Handoff

A chamada BFF → Identity está em [internal-api-contract.yaml](internal-api-contract.yaml). Tipos e mocks podem ser derivados de `api-contract.yaml`; os cenários de segurança e revogação requerem testes da implementação.
