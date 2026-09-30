# API interna — `commerce`, módulo Catálogo

> Derivado de [internal-api-contract-commerce.yaml](internal-api-contract-commerce.yaml), versão 1.1.0, OpenAPI 3.1.0. Estado: Aprovado para implementação em 2026-09-30.

Rota privada `/internal/v1`. Evolui `commerce` 1.0.0 ([área financeira](../prd-acesso-interno/internal-api-contract-commerce.yaml), sem mudança). Dois consumidores, dois esquemas de segurança:

- **`bff-admin` — ficha e oferta (8 operações `*Internal`):** espelham 1:1 [api-contract.yaml](api-contract.yaml). JWT de ator interno com `audience: commerce`, validado por JWKS; `commerce` exige `oferta.editar` no claim `permissions`. Autor dos atos = `sub`, escola = `tenantId`. Sem cookie nem CSRF; 401 `TOKEN_INVALID`; sem 502/504.
- **`bff-admin` — `POST /offer-references/resolve` (`resolveOfferReferencesInternal`):** rótulos "curso — opção" para a consulta da trilha (RF-10). Exige o papel `administrador`; até 50 ids; oferta desconhecida ou de outra escola é omitida.
- **`bff-student` — vitrine, página e clique (3 operações `*Internal`):** espelham [api-contract-student.yaml](api-contract-student.yaml). Asserção de serviço do `bff-student` (C-04): emissor `bff-student`, audiência `commerce`, escopos `showcase:read` e `purchase-intent:write`, `tenantId` da escola, `jti` contra replay. 401 `SERVICE_ASSERTION_INVALID`, 403 `SCOPE_DENIED`. O limite de requisições (429) é da borda, não daqui.

Esquemas, respostas e exemplos têm como fonte o YAML.
