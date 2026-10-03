# API HTTP — acesso interno ao backoffice

> Derivado de [api-contract.yaml](api-contract.yaml), versão 1.0.1, OpenAPI 3.1.0. Recorte do PRD de `CAP-002` v1.0 (2026-09-25). Estado: Aprovado para implementação em 2026-09-25.

O SPA do backoffice usa apenas o BFF do backoffice em `/api/v1`. Sessão por cookie opaco `staff_session`, separado do cookie do aluno; o BFF não entrega token ao navegador. Campos, respostas, erros e exemplos têm como fonte o YAML.

| Método | Rota | Operação | Sucesso | Segurança | PRD |
|---|---|---|---|---|---|
| `POST` | `/staff-sessions` | `createStaffSession` — Entrar no backoffice | 200 | pública | RF-10, RF-08 |
| `GET` | `/staff-sessions/current` | `getCurrentStaffSession` — Consultar a sessão atual | 200 | cookie | RF-10, RF-01 |
| `DELETE` | `/staff-sessions/current` | `endCurrentStaffSession` — Sair | 204 | cookie + CSRF | RF-10 |
| `POST` | `/staff-password-reset-requests` | `requestStaffPasswordReset` — Pedir recuperação | 202 | pública | RF-11 |
| `POST` | `/staff-password-resets` | `resetStaffPassword` — Redefinir senha | 204 | pública | RF-11 |
| `POST` | `/staff-invitation-lookups` | `lookupStaffInvitation` — Consultar convite pelo token | 200 | pública | RF-05 |
| `POST` | `/staff-invitation-acceptances` | `acceptStaffInvitation` — Aceitar convite | 200 | pública | RF-05 |
| `GET` | `/staff-invitations` | `listPendingStaffInvitations` — Convites pendentes | 200 | cookie, `acesso.gerir` | RF-12 |
| `POST` | `/staff-invitations` | `createStaffInvitation` — Emitir convite | 201 | cookie + CSRF, `acesso.gerir` | RF-03, RF-04 |
| `GET` | `/staff-members` | `listStaffMembers` — Atores internos | 200 | cookie, `acesso.gerir` | RF-12 |
| `POST` | `/staff-members/{accountId}/role-grants` | `grantStaffRole` — Conceder papel | 200 | cookie + CSRF, `acesso.gerir` | RF-06 |
| `POST` | `/staff-members/{accountId}/role-revocations` | `revokeStaffRole` — Revogar papel | 200 | cookie + CSRF, `acesso.gerir` | RF-07 |
| `POST` | `/staff-members/{accountId}/role-changes` | `changeStaffRole` — Trocar papel | 200 | cookie + CSRF, `acesso.gerir` | RF-09 |
| `GET` | `/finance-area` | `getFinanceArea` — Área financeira reservada | 200 | cookie, `financeiro.ler` | RF-13 |

## Regras de integração

- **Papéis e permissões** usam os nomes canônicos do domínio: papéis `professor`, `suporte`, `financeiro`, `administrador`; permissões `acesso.gerir`, `financeiro.ler`, `autoria.ler`, `suporte.atender` (DP-03). O administrador não herda as áreas dos outros papéis. `permissions` na sessão monta o menu; a autorização é refeita no BFF e no serviço dono.
- **Entrada:** credencial errada, e-mail inexistente e conta de aluno respondem o mesmo `INVALID_CREDENTIALS`. Conta interna sem papel entra com `permissions: []`, e o SPA mostra a orientação de conta sem acesso.
- **Revogação e troca** encerram todas as sessões do ator afetado (`sessionsEnded: true`); a próxima chamada dele responde 401 `SESSION_REQUIRED`.
- **Ação sem efeito** (conceder papel já concedido, revogar papel ausente) responde 200 com `changed: false` e não comunica ato à Auditoria.
- **Troca de papel** com `fromRole` que a conta não tem responde 422 `ROLE_NOT_HELD`; com `fromRole` igual a `toRole`, 422 `ROLE_CHANGE_INVALID`.
- **Ação sobre a própria conta** responde 422 `SELF_ROLE_CHANGE_FORBIDDEN`; a listagem marca a própria linha com `isSelf: true` para o SPA não oferecer ação.
- **Motivo** é obrigatório em convite, concessão, revogação e troca; ausente ou só com espaços responde 422 `REASON_REQUIRED`.
- **Convite:** e-mail de conta interna responde `EMAIL_BELONGS_TO_STAFF`, de conta de aluno `EMAIL_BELONGS_TO_STUDENT` (DP-05). Novo convite para e-mail com convite pendente substitui o anterior e devolve `supersededInvitationId` (DP-04). Convite expirado, aceito, substituído ou inexistente responde sempre `INVITATION_INVALID`.
- **Tokens** de convite e de redefinição viajam no corpo JSON, nunca na URL da API. A página do link lê o token da URL do SPA e o envia no corpo.
- **Escritas** exigem `Idempotency-Key` (janela de 24 horas, como em CAP-001); escritas com sessão exigem `X-CSRF-Token`, obtido em `GET /staff-sessions/current`.
- **Listagens** paginam com `_page` e `_size` (máximo 50) e nunca cruzam tenant.
- **Erros** seguem RFC 9457 com `code` estável e `traceId`; nenhum erro carrega e-mail, nome, motivo ou token.

## Exemplo derivado do contrato

`POST /staff-members/7a8b9c0d-1e2f-4a3b-9c4d-5e6f7a8b9c0d/role-changes`

```json
{"fromRole":"professor","toRole":"financeiro","reason":"Mudou de função para o time financeiro"}
```

Responde 200 com `roles: ["financeiro"]`, `changed: true`, `sessionsEnded: true`, e a Auditoria recebe `papel-revogado` e `papel-concedido` com o mesmo motivo ([asyncapi-contract.yaml](asyncapi-contract.yaml)).

## Handoff

A chamada BFF do backoffice → Identity (e BFF → serviço dono da área financeira) sai com a TechSpec, como em CAP-001. Tipos e mocks podem ser derivados de `api-contract.yaml`; os cenários de revogação, menor privilégio e atomicidade da troca exigem testes da implementação.
