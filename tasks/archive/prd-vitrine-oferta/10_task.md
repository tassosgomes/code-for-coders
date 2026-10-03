---
status: done
task_kind: vertical
blocked_by: ["9.0"]
gate: 'dotnet test --project src/commerce/tests/CodeForCoders.Commerce.IntegrationTests/CodeForCoders.Commerce.IntegrationTests.csproj -- --filter-class CodeForCoders.Commerce.IntegrationTests.ShowcaseCourseDetailTests --minimum-expected-tests 9 && dotnet test --project src/bff-student/tests/CodeForCoders.BffStudent.IntegrationTests/CodeForCoders.BffStudent.IntegrationTests.csproj -- --filter-class CodeForCoders.BffStudent.IntegrationTests.ShowcaseCourseProxyTests --minimum-expected-tests 3 && npm --prefix src/student-spa run test -- student-course-page'
gate_expect: "Pelo menos 20 testes passam: 9 de integração de commerce, 3 do BFF do aluno, 8 do SPA"
---

# 10.0 O visitante lê a página do curso e entende o que compra

**Fatia:** V-08 · **Cobre:** RF-08, US-06, US-07, RN-O02, RN-O05, RN-O06, RN-O08, RN-O13, RN-O18, RN-C18, RN-D04, C-02, C-03, C-07 · **Spec:** `techspec.md#v-08` · **ADR:** [ADR-0009](../../../docs/adr/0009-autenticacao-de-servico-bff-aluno-em-servicos-de-dominio.md) (Proposed)

## Comportamento

- **`commerce` — `getShowcaseCourseInternal`:** por lista explícita de campos, devolve título, nível,
  descrição (saída inteira da visão do Catálogo, C-03), pré-requisito (texto e recomendados com `inShowcase`;
  título **atual** quando o Catálogo conhece o curso, senão o da publicação, C-02), estrutura de módulos e
títulos de aula **sem vídeo** e as ofertas **publicadas** do menor para o maior preço, cada uma com nome,
`priceCents` e `accessPeriod` (sem `currency`: decisão do responsável em 2026-10-01 — os contratos OpenAPI
aprovados prevalecem; `currency: BRL` permanece forma dos fatos AsyncAPI, acordo CAP-008/CAP-011). Curso fora da vitrine, inexistente ou de outra escola → o
  mesmo 404 `SHOWCASE_COURSE_NOT_FOUND`, com respostas idênticas entre os três casos. Asserção de serviço
  com escopo `showcase:read`, como em 9.0; resposta `Cache-Control: no-store`. Republicar o curso em
  `learning` com aula nova aparece na página sem ação do financeiro (fato → visão → página).
- **`bff-student`:** rota anônima `getShowcaseCourse` sob o mesmo prefixo e regras de 9.0; 404 de `commerce`
  chega ao SPA com o mesmo `code`.
- **`student-spa`:** `/cursos/:courseId` mostra título, nível, descrição, "Recomendamos saber antes" (texto e
  recomendados, com link para a página do recomendado só quando `inShowcase`; só o título quando não está;
  nunca como condição de compra), a estrutura e uma opção de compra por oferta. A vigência vem sempre com o
  ponto de partida: "Acesso por 12 meses, contados a partir da liberação", no singular "Acesso por 1 mês,
  contado a partir da liberação", ou "Acesso vitalício"; o preço em reais com centavos, em texto. Descrição,
  chamada e nomes vêm do servidor e são renderizados como **texto**, nunca HTML. Endereço de curso fora da
  vitrine, inexistente ou de outra escola mostra "este curso não está disponível" com link para a vitrine
  (mesma tela para os três). Conforme o Figma aprovado em 2.0.
- **Caso negativo principal:** `courseId` inexistente, de outra escola e fora da vitrine devolvem respostas
  idênticas; nenhum texto gerado pela plataforma afirma exclusividade ou proteção contra cópia (RN-O06).

## Fora do escopo desta task

O clique em *Comprar* e o aviso (11.0) — a página já mostra a opção de compra, sem ação até 11.0. Tempo de
acesso efetivo e concessão (`CAP-008`).

## Decisões fechadas

- A página pública sai inteira da visão do Catálogo, sem chamada a `learning` (C-01, C-03).
- Recomendado sem curso na vitrine aparece só como título, sem link (RN-O05); o link usa `courseId` (C-07).
- Sem cache nesta entrega (`techspec.md#decisões-técnicas`).

## Modificar / Referenciar

- **modificar:** `src/commerce/src/CodeForCoders.Commerce.Api/Extensions/EndpointExtensions.cs`, `ExceptionHandlers/GlobalExceptionHandler.cs` (`SHOWCASE_COURSE_NOT_FOUND`)
- **modificar:** `src/bff-student/src/CodeForCoders.BffStudent.Api/Extensions/EndpointExtensions.cs` (rota da página)
- **modificar:** `src/student-spa/src/config/paths.ts`, `src/student-spa/src/app/router.tsx` (somente se a rota de 9.0 não cobrir a página)
- **ref:** `internal-api-contract-commerce.yaml` e `api-contract-student.yaml` (`getShowcaseCourse`); `src/student-spa/src/features/**` (padrões de tela); `docs/design/wireframes-catalogo-vitrine.md`, `DESIGN.md` e Figma aprovado; skills `dotnet` e `react`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/commerce` | `dotnet format src/commerce/CodeForCoders.Commerce.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/commerce` | `dotnet build src/commerce/CodeForCoders.Commerce.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/commerce` | `dotnet test --project src/commerce/tests/CodeForCoders.Commerce.IntegrationTests/CodeForCoders.Commerce.IntegrationTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/commerce` | `dotnet test --project src/commerce/tests/CodeForCoders.Commerce.ArchitectureTests/CodeForCoders.Commerce.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/bff-student` | `dotnet format src/bff-student/CodeForCoders.BffStudent.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/bff-student` | `dotnet build src/bff-student/CodeForCoders.BffStudent.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/bff-student` | `dotnet test --project src/bff-student/tests/CodeForCoders.BffStudent.IntegrationTests/CodeForCoders.BffStudent.IntegrationTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/student-spa` | `npm --prefix src/student-spa run lint` | exit 0 | `ci-react-ts.yml` |
| `src/student-spa` | `npm --prefix src/student-spa run typecheck` | exit 0 | `ci-react-ts.yml` |
| `src/student-spa` | `npm --prefix src/student-spa run test -- student-showcase` | exit 0 (vitrine de 9.0 sem regressão) | `ci-react-ts.yml` |
| `src/student-spa` | `npm --prefix src/student-spa run build -- --base=/student/` | exit 0 | `ci-react-ts.yml` |

Cobertura agregada ≥ 70%, `dotnet publish` e imagens ficam para a validação full.

## Pronto quando

- [ ] Gate passa (exit 0) com pelo menos 20 testes.
- [ ] Página com "Avançado", "Recomendamos saber antes", link para o recomendado quando ele está na vitrine e só o título quando não está; republicar o curso com aula nova e reabrir mostra a aula nova.
- [ ] `courseId` inexistente, de outra escola e fora da vitrine → respostas idênticas; resposta JSON sem autor, identificador de pessoa nem `videoId`.
- [ ] SPA: teste que falha se um texto gerado pela plataforma contiver "exclusiv" ou "proteg"; vigência no plural, no singular e vitalícia; título e preço navegáveis por leitor de tela.
- [ ] Smoke no Compose: `http://localhost:8082/student/cursos/{courseId}` recarregada direto abre a página; o link do recomendado abre a página do curso recomendado; o endereço de curso fora da vitrine mostra "não está disponível" com link para a vitrine.
