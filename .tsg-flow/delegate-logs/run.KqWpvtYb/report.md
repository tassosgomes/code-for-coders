Run: run.KqWpvtYb

# Implementação da task 4.0 — emissão de convites internos

Outcome: `implementation_complete`  
Gate: `passed`

## Entrega

- Identity cria convites internos com token aleatório armazenado como hash, prazo configurável, idempotência e substituição de convites não resolvidos do mesmo e-mail. A gravação do convite, da solicitação protegida de notificação e do fato protegido de auditoria ocorre na mesma unidade de trabalho. A listagem retorna apenas convites pendentes e não expirados do tenant.
- Notification aceita `convite-interno` sem nome, exige papel e link, apresenta a validade configurada e mantém os contratos dos modelos existentes. O papel do destinatário também é persistido no registro de entrega.
- BFF expõe criação e listagem protegidas por `acesso.gerir`, encaminha sessão, chave de idempotência e scopes configurados para Identity. A SPA adiciona a área Acessos com formulário, lista de convites pendentes e mensagens específicas para os três erros previstos.
- Foram geradas migrations pelos comandos do Entity Framework: `20260925183745_AddStaffInvitations` e `20260925183757_AddStaffInvitationRole`.
- O compose configura URL e validade compartilhada do convite e o exchange de auditoria.

## Verificações

| Verificação | Resultado |
| --- | --- |
| Gate da task: Identity focado | 7/7 passaram |
| Gate da task: Notification focado | 3/3 passaram |
| Gate da task: BFF focado | 3/3 passaram |
| Gate da task: SPA `StaffInvitationIssuing` | 4 passaram |
| Identity: format, build da solução, arquitetura, unidade e integração completa | passou; arquitetura 8/8, unidade 15/15, integração 45/45; build sem warnings |
| Notification: format, build da solução, unidade e integração completa | passou; unidade 20/20, integração 22/22; build sem warnings |
| BFF: format, build da solução, arquitetura, unidade e E2E completos | passou; arquitetura 8/8, unidade 2/2, E2E 14/14; build sem warnings |
| SPA: lint, typecheck, build e suite completa | passou; 11/11 testes |
| `git diff --check` | passou |

O build da SPA concluiu com dois avisos do Vite já não bloqueantes: o script runtime em `index.html` não pode ser empacotado sem `type="module"`, e o chunk JavaScript principal excede 500 kB.

## Limites da execução

- O smoke de aceitação via Docker Compose não foi executado conforme `context.txt`, que proíbe iniciar os containers compartilhados. As suites com Testcontainers foram executadas.
- `tasks/prd-acesso-interno/4_task.md` e `tasks/prd-acesso-interno/flow-state.json` já apareciam modificados na preflight; foram preservados e não usados como parte da implementação.
