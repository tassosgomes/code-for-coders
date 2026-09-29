# API interna — `bff-admin` → `learning`

> Derivado de [internal-api-contract-learning.yaml](internal-api-contract-learning.yaml), versão 1.0.0, OpenAPI 3.1.0. Estado: Aprovado para implementação em 2026-09-28.

Rota privada `/internal/v1`, inacessível ao navegador. Consumidor único: `bff-admin`.

- **Autoria (15 operações `*Internal`):** espelham 1:1 [api-contract.yaml](api-contract.yaml), por transformação mecânica. Diferenças:
  - segurança por JWT de ator interno emitido por Identity com `audience: learning`, validado localmente por JWKS; `learning` exige `autoria.ler` ou `autoria.editar` no claim `permissions`. Ator = `sub`, escola = `tenantId` do token;
  - sem cookie nem CSRF; 401 `TOKEN_INVALID` em vez de `SESSION_REQUIRED`; sem 502/504;
  - escritas recebem `X-Actor-Name`, o nome do ator no momento da ação, só para exibição (C-04). Nunca vai para log, span, métrica ou fato;
  - o vídeo da aula traz só `videoId`: `learning` não guarda título nem duração; o BFF os obtém de Media (C-05).
- **`POST /course-references/resolve` — `resolveCourseReferencesInternal` (C-08):** usada só pela consulta da trilha de auditoria, para mostrar o título do curso alvo de `versao-publicada`. Exige JWT de audiência `learning` com papel `administrador`; não exige `autoria.ler` e devolve apenas o título atual. Até 50 ids por chamada; curso excluído, inexistente ou de outra escola volta sem `title`, sem distinguir os casos.

Esquemas, respostas e exemplos têm como fonte o YAML.
