# API interna — backoffice → Identity e Commerce

> Derivado de [internal-api-contract.yaml](internal-api-contract.yaml) e [internal-api-contract-commerce.yaml](internal-api-contract-commerce.yaml), versões 1.0.1 e 1.0.0, OpenAPI 3.1.0. EN-01 da [TechSpec](techspec.md) de `CAP-002`. Estado: Aprovado para implementação em 2026-09-25.

Rotas privadas em `/internal/v1`, inacessíveis ao navegador. O `bff-admin` é o único consumidor. Campos, respostas e erros têm como fonte os YAML.

## Identity

| Método | Rota | Operação | Escopo da asserção | Ator (`X-Staff-Session`) | Pública correspondente |
|---|---|---|---|---|---|
| `POST` | `/staff-sessions` | `createStaffSessionInternal` | `staff-sessions:create` | — | `createStaffSession` |
| `POST` | `/staff-session-validations` | `validateStaffSessionInternal` | `staff-sessions:validate` | — (sessão no corpo) | toda ação protegida |
| `POST` | `/staff-session-revocations` | `revokeStaffSessionInternal` | `staff-sessions:revoke` | — (sessão no corpo) | `endCurrentStaffSession` |
| `POST` | `/staff-password-reset-requests` | `requestStaffPasswordResetInternal` | `staff-passwords:reset` | — | `requestStaffPasswordReset` |
| `POST` | `/staff-password-resets` | `resetStaffPasswordInternal` | `staff-passwords:reset` | — | `resetStaffPassword` |
| `POST` | `/staff-invitation-lookups` | `lookupStaffInvitationInternal` | `staff-invitations:read` | — | `lookupStaffInvitation` |
| `POST` | `/staff-invitation-acceptances` | `acceptStaffInvitationInternal` | `staff-invitations:write` | — | `acceptStaffInvitation` |
| `GET` | `/staff-invitations` | `listPendingStaffInvitationsInternal` | `staff-invitations:read` | `acesso.gerir` | `listPendingStaffInvitations` |
| `POST` | `/staff-invitations` | `createStaffInvitationInternal` | `staff-invitations:write` | `acesso.gerir` | `createStaffInvitation` |
| `GET` | `/staff-members` | `listStaffMembersInternal` | `staff-members:read` | `acesso.gerir` | `listStaffMembers` |
| `POST` | `/staff-members/{accountId}/role-grants` | `grantStaffRoleInternal` | `staff-members:write` | `acesso.gerir`, ≠ alvo | `grantStaffRole` |
| `POST` | `/staff-members/{accountId}/role-revocations` | `revokeStaffRoleInternal` | `staff-members:write` | `acesso.gerir`, ≠ alvo | `revokeStaffRole` |
| `POST` | `/staff-members/{accountId}/role-changes` | `changeStaffRoleInternal` | `staff-members:write` | `acesso.gerir`, ≠ alvo | `changeStaffRole` |
| `GET` | `/jwks` | `getUserTokenSigningKeysInternal` | nenhum (chaves públicas) | — | — (serviços de domínio) |

## Commerce

| Método | Rota | Operação | Autenticação | Pública correspondente |
|---|---|---|---|---|
| `GET` | `/finance-area` | `getFinanceAreaInternal` | JWT de ator interno, `aud=commerce`, `financeiro.ler` | `getFinanceArea` |

## Regras de integração

- **Asserção de serviço** do emissor `bff-admin`, chave e `kid` próprios. Identity recusa asserção do `bff-student` nestas rotas e a do `bff-admin` nas de aluno (ADR-0005).
- **Ator das operações de gestão:** o BFF envia o identificador opaco da sessão interna em `X-Staff-Session`. Identity valida a sessão, o tenant e a permissão e resolve o ator; o BFF nunca informa conta, papel ou permissão. Sessão inválida → 401 `SESSION_REQUIRED`; sem permissão → 403 `PERMISSION_DENIED`.
- **Validação por ação:** antes de toda ação protegida o BFF chama `validateStaffSessionInternal`; a resposta traz papéis e permissões vigentes e, com `audience`, o JWT para o serviço destino. Falha fecha o acesso.
- **JWT de usuário:** RS256, vida curta, claims `roles` e `permissions`; nunca sai do lado servidor do BFF. `commerce` valida pelo JWKS de Identity.
- **Idempotência:** `Idempotency-Key` propagada sem alteração; conflito interno `IDEMPOTENCY_CONFLICT` vira `IDEMPOTENCY_KEY_REUSED` na borda pública.
- **Troca de papel:** `fromRole` ausente → 422 `ROLE_NOT_HELD`; `fromRole` igual a `toRole` → 422 `ROLE_CHANGE_INVALID`, código repassado sem tradução na borda pública.
- **Erros:** RFC 9457 com `code`; nenhum erro carrega e-mail, nome, motivo, senha ou segredo.
