# Revisão full — Conta e autenticação do aluno

Run: run.TUd4coLe
Data: 2026-09-23
- modo: full; resultado: `rejected`; gate: `failed`.
- base_ref: `d1c046564281e7d3d15f57bd7e6d57ffff426a40`
- validated_commit: `bc636c011fce6c281d4d5d1afa9d01028530bad2`
- validated_tree: `c4569d6888652f247f34c7814210c68e1202531d`

## Revisão e gate

- Revistos PRD, TechSpec, baseline/ADRs, contratos, tasks V-01–V-05 e o diff completo desde `base_ref`.
- .NET restore, format e publish passaram em BFF, Identity e Notification. O comando de testes do CI terminou com exit 5 e `Zero tests ran` nos três serviços; cobertura .NET não foi medida. `global.json:6-8` seleciona Microsoft.Testing.Platform e `Directory.Build.targets:3-10` o ativa, mas os projetos usam referências xUnit/Visual Studio; alinhar runner/adapter e repetir o gate.
- SPA: `npm ci`, lint, typecheck, testes e build passaram; 14 testes/7 arquivos, cobertura de linhas 92,34%. Build em `/student/`; avisos não bloqueantes de chunk de 666,54 kB e `runtime-env.js`.
- Imagens locais BFF, Identity, Notification e SPA foram reconstruídas. Smoke atual passou cadastro, reenvio e confirmação, duas sessões, recuperação com revogação das duas sessões, troca de senha mantendo a sessão corrente e login com a nova senha.

## Bloqueios

- **B1 — token de recuperação aparece no access log.** PRD: `prd.md:254`; TechSpec: `techspec.md:33`. `nginx.conf.template:23-25` serve as rotas da SPA sem suprimir query string. Na imagem atual, o formato padrão do Nginx grava `$request`; uma requisição de prova a `/student/redefinir-senha?token=review-probe-only` apareceu no log com a query completa. Links reais usam esse mesmo formato. Suprimir/mascarar a query nas rotas de token e remover atributos de URL sensíveis da telemetria.
- **B1 — risco adicional em telemetria:** `main.tsx:10` inicializa antes do React; `telemetry.ts:49-63` não desativa nem sanitiza auto-instrumentações; as telas só removem o token em `useEffect` (`student-confirmation-screen.tsx:51`, `student-password-recovery-screen.tsx:63`). A instrumentação `document-load` pode registrar `url.full` com a query. Não capturei evento OTLP no smoke; validar e redigir esse atributo antes de aprovar.
- **B2 — gate .NET não valida testes.** Os três comandos `dotnet test --no-restore --configuration Debug --coverage --coverage-output-format cobertura` saíram com exit 5 e zero testes descobertos. Não há evidência de suíte backend passando; corrigir a configuração MTP/xUnit e repetir CI, incluindo cobertura.

## Sensor e limitações

- Em worktree isolada, mutações de rota para V-01 cadastro, V-02 confirmação, V-03 sessão, V-04 recuperação e V-05 troca fizeram seus testes SPA focados falharem (exit 1). Os cinco mutantes foram restaurados; worktree removida com HEAD e status iguais à linha de base. Sensor backend não executável enquanto o runner retorna zero testes.
- Publish em nível de solução gerou aviso `NETSDK1194`; não bloqueou. Docker push, SAST, scans de dependência e DAST não foram executados nesta validação.

## Nota do orquestrador sobre run.TUd4coLe (2026-09-23)

- **B2 (zero testes .NET) não reproduz no CI real:** o job `Build & Test` de identity em `main`
  (GitHub Actions run 35779580543, `dotnet test --no-restore --configuration Debug --coverage ...` em `src/identity`)
  descobriu e executou os testes ("Test run summary: Passed! total: 12") e falhou apenas no gate de cobertura
  (55,63% na base). Implementer (run.OIduKadP) e validator (run.Mnk5cooD, run.oIGrxsd8) executaram o mesmo comando com
  40/40 e 131/131. O "Zero tests ran" é tratado como problema de ambiente do validator; a próxima full deve executar o
  comando do CI no diretório do serviço, diretamente (sem wrapper `rtk`), e registrar contagem de testes e cobertura.
- **B1 (token em access log/telemetria)** é tratado reabrindo a task 4.0, cobrindo os links de confirmação e redefinição.
