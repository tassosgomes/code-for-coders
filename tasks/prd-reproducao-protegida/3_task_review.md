# Revisão focused — Task 3.0 (V-01: abrir a aula, lista de aulas e motivo de acesso)

Run: run.UOtcOCJY

- Modo: focused · Tentativa: 1/3 · HEAD revisado: d2b9ea53b86166c848349730984be661043507af (árvore 4a2503d50fc8b4b411a515c59271efb64dbf44c1) · checkpoint anterior: d2b9ea5
- Escopo: alterações não commitadas sobre o checkpoint (39 arquivos modificados + untracked da task em identity, commerce, learning, bff-student, student-spa, compose e `scripts/generate-local-env.sh`). HEAD, `git status` e hash do diff idênticos antes e depois da revisão.
- Comandos executados via `rtk proxy`, em sequência (Testcontainers sem paralelismo), em primeiro plano.

## Gate declarado (exit code é o veredito)

| # | Comando (resumo) | Exit | Testes |
|---|---|---|---|
| 1 | Identity.UnitTests `StudentTokenAudienceTests` (mín. 5) | 0 | 5 |
| 2 | Identity.IntegrationTests `StudentTokenJwksTests` (mín. 3) | 0 | 3 |
| 3 | Commerce.IntegrationTests `AccessDecisionIssuersTests` (mín. 3) | 0 | 3 |
| 4 | Learning.IntegrationTests `StudentLessonTests` (mín. 10) | 0 | 15 |
| 5 | Learning.IntegrationTests `AccessDecisionClientTests` (mín. 4) | 0 | 5 |
| 6 | BffStudent.IntegrationTests `StudentLessonProxyTests` (mín. 7) | 0 | 14 |
| 7 | student-spa `test -- student-lesson-screen` | 0 | 9 |
| 8 | student-spa `test -- student-login-return` | 0 | 6 |

`gate_expect` (≥ 43 testes): 60 testes passaram, todos os mínimos atendidos.

## Verificações do projeto (16)

Todas exit 0: `dotnet format --verify-no-changes` (identity, commerce, learning, bff-student); `dotnet build` dos 4 slnx (0 warnings, 0 erros); ArchitectureTests dos 4 componentes (8, 11, 9 e 8 testes); student-spa `lint`, `typecheck`, suíte inteira (16 arquivos, 78 testes) e `build -- --base=/student/`.
Cobertura agregada, `dotnet publish` e imagens ficam para a validação full (conforme a task).

## Revisão semântica (resumo)

- **Ordem fixa e vazamento:** `GetStudentLesson` busca a aula (404 `LESSON_NOT_AVAILABLE`), depois a decisão (null → 503 `ACCESS_DECISION_UNAVAILABLE`; negada → 403 `ACCESS_DENIED` com `reason` e `accessEndedAt`) e só então devolve a tela. Nada do curso sai antes da decisão positiva (RN-R02, RN-R06).
- **Versão vigente:** `CourseQueries.FindAsync` usa `@>` sobre `modules` (índice GIN `jsonb_path_ops`, migration gerada pelo EF) com `NOT EXISTS` de versão maior e `current_version IS NOT NULL`: aula removida na republicação, curso não publicado e outra escola dão o mesmo 404.
- **Política de aluno:** escopo `lessons:read`, ausência de `permissions` e `sessionId` válido; a política administrativa passou a exigir `permissions`, então aluno não abre autoria e ator não abre a rota de aula. `studentId` vem só da claim `sub` (teste de `studentId` por query).
- **Cliente da decisão:** cache por (escola, aluno, curso), 30 s, limitado ao término informado, guarda permitido e negado, nunca falha; sem nova tentativa; resposta inválida vira indisponível. Exercitado pelo adaptador real (`AccessDecisionClient`) com handler de rede de teste no `StudentLessonApiFactory`, que sobe o `Program` real.
- **BFF:** audiência por rota (`RouteAudiences`, `/api/v1/lessons` → `learning`), mapeamento 502/503/504 e códigos públicos preservados; sem sessão nunca chama Identity nem `learning`.
- **SPA:** rota dentro do bloco autenticado; retorno após login só por caminho interno (`internalReturnPath` rejeita `//`, barra invertida e controles, inclusive codificados).

## Bloqueantes

Nenhum.

## Recomendações (não bloqueantes) — 2

1. O item "Pronto quando" de verificação no navegador contra o ambiente de desenvolvimento (cortesia vigente vê a lista, sem concessão vê a mensagem, sem sessão volta à aula) **não foi executado por este validator**: exige `remote-infra.sh provision/migrate`, `apps.sh start --remote` e conceder cortesia, ações sobre infraestrutura compartilhada que o transporte não autoriza. A cobertura automatizada (learning, BFF e SPA) é consistente, mas a conferência manual deve acontecer na validação full/QA antes da entrega.
2. O adaptador real do cliente da decisão é exercitado com um handler de rede de teste, não com uma instância real de `commerce`; a integração ponta a ponta com o `commerce` verdadeiro (asserção `learning` aceita pelo `ServiceAssertionVerifier`) fica coberta só por `AccessDecisionIssuersTests`. Vale reconferir na full.

## Resultado

VALIDAÇÃO APROVADA (0 bloqueantes, 2 recomendações).
