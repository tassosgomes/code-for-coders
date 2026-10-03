# API interna de `commerce` — Matrícula e Direito de Acesso

> Derivado de [internal-api-contract-commerce.yaml](internal-api-contract-commerce.yaml), versão 1.2.0, OpenAPI 3.1.0 (evolui 1.1.0 de CAP-003). PRD: [prd.md](prd.md) v1.0. Estado: **Aprovado para implementação** em 2026-10-01.

Dois consumidores e dois esquemas de segurança. As cinco operações de cortesia são derivadas mecanicamente de [api-contract.yaml](api-contract.yaml): sem cookie nem CSRF, sem 502/504, com o JWT do ator de audiência `commerce` (ADR-0005) e `cortesia.conceder`. A decisão de acesso é de outro tipo de chamador: serviços, não atores.

| Operação | Método e caminho | Consumidor | Segurança | Resultado |
|---|---|---|---|---|
| `previewCourtesyTermInternal` | `GET /internal/v1/courtesy-term-preview?months=` | `bff-admin` | JWT do ator, `cortesia.conceder` | Término que a concessão teria agora, sem efeito; mesma função do domínio (C-11) |
| `listCourtesyCoursesInternal` | `GET /internal/v1/courtesy-courses` | `bff-admin` | JWT do ator, `cortesia.conceder` | Cursos concedíveis, da visão própria de Matrícula (C-01) |
| `listStudentAccessGrantsInternal` | `GET /internal/v1/students/{studentId}/access-grants` | `bff-admin` | idem | Concessões do aluno; `status` calculado na leitura |
| `grantCourtesyInternal` | `POST /internal/v1/courtesy-grants` | `bff-admin` | idem | Concessão + fato + ato na mesma transação; reconfirma a conta em Identity; 503 `STUDENT_ACCOUNT_CHECK_UNAVAILABLE` |
| `getCourtesyGrantInternal` | `GET /internal/v1/courtesy-grants/{grantId}` | `bff-admin` | idem | A concessão |
| `decideAccessInternal` | `GET /internal/v1/access-decision?studentId=&courseId=` | `media`, `learning` (CAP-007 e CAP-017; nenhum consumidor nesta entrega) | Asserção de serviço, escopo `access-decision:read` | `allowed` ou `denied`, término efetivo e motivo |

## A decisão de acesso

Fonte única do direito (RN-D01, G11). Responde **200 também quando nega**. O aluno pode se há ao menos uma concessão ativa dentro da vigência (RN-D07); `validity` traz o término da que dura mais, ou `lifetime`. Negado: `no-grant` ou `grant-ended` (com `lastExpiredAt`). Aluno desconhecido, conta que não é de aluno, curso desconhecido e dado de outra escola → `denied`/`no-grant`, indistintos (G07, RN-D17). O consumidor guarda a resposta por **até 30 segundos** (`Cache-Control: private, max-age=30`), nunca além de `expiresAt`, e **não libera acesso** se não obtiver resposta (BA07). Nada de dado pessoal na consulta nem na resposta.

Erros: `401 TOKEN_INVALID` (ator) ou `SERVICE_UNAUTHORIZED` (serviço); `403 PERMISSION_DENIED` ou `SCOPE_DENIED`. Esquemas e exemplos normativos estão no YAML.
