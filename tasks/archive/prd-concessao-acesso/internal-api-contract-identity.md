# API interna de Identity — conta de aluno para cortesia

> Derivado de [internal-api-contract-identity.yaml](internal-api-contract-identity.yaml), versão 1.4.0, OpenAPI 3.1.0 (evolui 1.3.0). PRD: [prd.md](prd.md) v1.0. Estado: **Aprovado para implementação** em 2026-10-01.

| Operação | Método e caminho | Consumidor | Segurança | Resultado |
|---|---|---|---|---|
| `lookupStudentAccountInternal` | `POST /internal/v1/student-account-lookups` | `bff-admin` | Asserção do `bff-admin`, escopo `student-account:lookup`, e `X-Staff-Session` com `cortesia.conceder` | `200` com `studentId`, e-mail, nome, `emailConfirmed` e `status`; `404 STUDENT_ACCOUNT_NOT_FOUND` indistinto para inexistente, ator interno e outro tenant |
| `confirmStudentAccountInternal` | `POST /internal/v1/student-account-confirmations` | `commerce` | Asserção de `commerce`, escopo `student-account:confirm` | `200` com `eligible`: verdadeiro só para conta **de aluno ativa** do tenant da asserção; qualquer outro caso é `false`, sem motivo |

A localização devolve o mínimo (RN-16): e-mail e nome, nunca mais. O e-mail viaja no corpo, fora de URL, log e métrica (RN-21, G23). A confirmação existe para que Matrícula aplique RN-D09 com a fonte única da identidade e não confie no BFF; sem resposta, `commerce` não concede (falha fechada). `commerce` ainda não é emissor em Identity: a extensão do mecanismo da ADR-0004 e da ADR-0005 é registrada em ADR própria pela TechSpec (C-03).

Erros: `400 VALIDATION_ERROR`, `401 SERVICE_UNAUTHORIZED` ou `SESSION_REQUIRED`, `403 PERMISSION_DENIED`. Esquemas e exemplos normativos estão no YAML.
