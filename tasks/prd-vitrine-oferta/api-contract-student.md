# API — `student-spa` → `bff-student`: vitrine pública

> Derivado de [api-contract-student.yaml](api-contract-student.yaml), versão 1.0.0, OpenAPI 3.1.0. PRD: [prd.md](prd.md) v1.0. Estado: Aprovado para implementação em 2026-09-30.

Operações públicas (`security: []`): nenhuma exige sessão, e o cookie `student_session`, se presente, não muda nada (RN-O13). Nenhuma resposta traz autor, e-mail ou identificador de pessoa.

## Premissas e decisões

| Decisão | Escolha | Motivo |
|---|---|---|
| Autenticação | Nenhuma no navegador; o BFF usa asserção de serviço para chegar ao `commerce` | C-04 |
| Endereço público do curso | Identificado por `courseId` | Estável: título muda, id não (C-07) |
| Preço | Centavos de real, inteiro | C-05 |
| Clique em Comprar | Sem corpo, sem sessão, sem CSRF, com `Idempotency-Key` por clique | OD53, G10, G26; C-08 |
| Paginação | `_page`/`_size` (padrão 12, máximo 48) | Grade da vitrine |

## Resumo de endpoints

| Método | Path | Descrição | Status |
|---|---|---|---|
| `GET` | `/showcase/courses?level=` | Cartões da vitrine: título, nível, resumo, menor preço, número de ofertas | 200, 400, 502, 504 |
| `GET` | `/showcase/courses/{courseId}` | Página do curso: descrição, pré-requisito, estrutura, opções de compra | 200, 404, 502, 504 |
| `POST` | `/showcase/offers/{offerId}/purchase-intents` | Conta um clique anônimo e responde `coming-soon` | 202, 404, 429, 502, 504 |

Curso fora da vitrine e oferta não publicada respondem 404 indistinto de inexistente (`SHOWCASE_COURSE_NOT_FOUND`, `OFFER_NOT_AVAILABLE`).

Esquemas, respostas e exemplos têm como fonte o YAML.
