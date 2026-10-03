# API — `admin-spa` → `bff-admin`: área Cortesias (cortesia)

> Derivado de [api-contract.yaml](api-contract.yaml), versão 1.0.0, OpenAPI 3.1.0. PRD: [prd.md](prd.md) v1.0. Estado: **Aprovado para implementação** em 2026-10-01.

Toda operação exige a sessão do backoffice e a permissão `cortesia.conceder`, concedida ao financeiro (RF-01). O BFF recusa antes; `commerce` e `identity` recusam de novo com o JWT do ator e a sessão (ADR-0005). O aluno é identificado pelo e-mail exato, enviado no **corpo**, nunca na URL (RN-21, G23).

## Premissas e decisões

| Decisão | Escolha | Motivo |
|---|---|---|
| Autenticação | Cookie de sessão do backoffice; escrita com `X-CSRF-Token` | RN-11, G18; precedente de CAP-003 |
| Idempotência | `Idempotency-Key` obrigatória em `grantCourtesy`; reenvio com o mesmo corpo → 200 com a concessão original | RN-D10, G09 |
| Vigência | `accessPeriod` `{months}` (1 a 60) ou `{lifetime}`; resposta traz `endsOn` (data) e `expiresAt` (instante UTC, exclusivo) | DP-03, DP-04; C-05 de CAP-003 |
| Término | Fim do dia no fuso da escola; `expiresAt` é o início do dia seguinte, e há acesso se a pergunta é anterior a ele | DP-03, RF-05 |
| Prévia do término | Operação de leitura sem efeito (`previewCourtesyTerm`), derivada da mesma função da concessão: a tela nunca recalcula a regra. Vale se a confirmação ocorrer no mesmo dia no fuso da escola | RF-03 passo 3; C-11 |
| Situação | `status` calculado na leitura (`active`, `expired`) | RN-D06 |
| Paginação | `_page`/`_size` (padrão 10, máximo 50) | Convenção HTTP |
| Localizar aluno | `POST` de leitura, para o e-mail ficar fora da URL; 404 indistinto para inexistente, ator interno e outra escola | RF-02, G07, G23 |

## Resumo de endpoints

| Método | Path | Descrição | Status |
|---|---|---|---|
| `POST` | `/student-account-lookups` | Localizar a conta de aluno pelo e-mail: e-mail, nome, confirmação e situação | 200, 400, 401, 403, 404, 502, 504 |
| `GET` | `/courtesy-term-preview?months=` | Prévia do término de uma vigência por período, pela regra da concessão e sem efeito | 200, 400, 401, 403, 502, 504 |
| `GET` | `/courtesy-courses?title=` | Cursos com versão vigente, sem exigir oferta | 200, 400, 401, 403, 502, 504 |
| `GET` | `/students/{studentId}/access-grants` | Concessões do aluno, de qualquer origem | 200, 400, 401, 403, 502, 504 |
| `POST` | `/courtesy-grants` | Conceder cortesia (curso, vigência, motivo obrigatório) | 200, 201, 400, 401, 403, 422, 502, 504 |
| `GET` | `/courtesy-grants/{grantId}` | Ver uma cortesia concedida | 200, 401, 403, 404, 502, 504 |

Erros de `grantCourtesy` (422): `FIELD_INVALID`, `STUDENT_ACCOUNT_NOT_ELIGIBLE`, `COURSE_NOT_ELIGIBLE`, `IDEMPOTENCY_KEY_REUSED`. Em 502, `STUDENT_ACCOUNT_CHECK_UNAVAILABLE` indica que `commerce` não conseguiu reconfirmar a conta em Identity: **falha fechada**, nada é concedido (C-03). **Não há operação para desfazer** (DP-07).

Esquemas, respostas e exemplos têm como fonte o YAML.
