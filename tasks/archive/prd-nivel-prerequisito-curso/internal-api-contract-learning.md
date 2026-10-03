# API interna — `bff-admin` → `learning`: nível e pré-requisito

> Derivado de [internal-api-contract-learning.yaml](internal-api-contract-learning.yaml), versão 1.1.0, OpenAPI 3.1.0. Estado: Aprovado para implementação em 2026-09-30.

Rota privada `/internal/v1`. Espelha 1:1 as quatro operações de [api-contract.yaml](api-contract.yaml) (`listCoursesInternal`, `getCourseInternal`, `updateCourseInternal`, `getCourseVersionInternal`), pela mesma transformação mecânica da autoria 1.0.0:

- JWT de ator interno com `audience: learning`, validado por JWKS; `learning` exige `autoria.ler` nas leituras e `autoria.editar` na alteração;
- sem cookie nem CSRF; 401 `TOKEN_INVALID`; sem 502/504;
- `updateCourseInternal` recebe `X-Actor-Name` (C-04 de CAP-005);
- `learning` é quem valida cada curso recomendado: mesma escola, com versão vigente, diferente do próprio curso → senão 422 `RECOMMENDED_COURSE_INVALID`.

Esquemas, respostas e exemplos têm como fonte o YAML.
