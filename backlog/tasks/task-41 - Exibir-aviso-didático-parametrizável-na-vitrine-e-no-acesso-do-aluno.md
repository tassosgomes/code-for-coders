---
id: TASK-41
title: Exibir aviso didático parametrizável na vitrine e no acesso do aluno
status: Done
assignee:
  - '@codex'
created_date: '2026-10-20 12:44'
updated_date: '2026-10-20 12:54'
labels:
  - frontend
dependencies: []
references:
  - 'https://dev-code4coders.tasso.dev.br/students/cursos'
  - src/student-spa/src/components/public-layout.tsx
  - src/student-spa/src/components/auth-layout.tsx
  - src/student-spa/src/config/env.ts
  - student-spa-src-components-app-shell
documentation:
  - docs/student-didactic-notice.md
modified_files:
  - src/student-spa/src/components/didactic-notice.tsx
  - src/student-spa/src/components/public-layout.tsx
  - src/student-spa/src/components/auth-layout.tsx
  - src/student-spa/src/config/env.ts
  - src/student-spa/public/runtime-env.template.js
  - src/student-spa/docker/40-runtime-env.sh
  - docker-compose.yml
  - docker-compose.komodo.web.yml
  - docs/student-didactic-notice.md
  - src/student-spa/src/config/env.test.ts
  - src/student-spa/src/app/didactic-notice.test.tsx
type: enhancement
ordinal: 56000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
O ambiente de desenvolvimento está exposto publicamente pelo Cloudflare Tunnel em https://dev-code4coders.tasso.dev.br/students/cursos. Visitantes podem interpretá-lo como um serviço comercial e informar dados pessoais verdadeiros. É necessário esclarecer a finalidade didática antes de navegar ou preencher formulários e permitir remover o aviso quando houver ambiente produtivo.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 Com o aviso ativado, a vitrine e os detalhes de curso identificam claramente a finalidade didática e orientam a usar apenas dados fictícios.
- [x] #2 O aviso é visível antes do preenchimento de login, cadastro e recuperação de senha, inclusive por acesso direto às URLs.
- [x] #3 A faixa na área pública permanece visível ao rolar, não pode ser dispensada e não encobre navegação ou conteúdo em desktop e celular.
- [x] #4 A configuração do ambiente permite ativar ou remover todos os avisos ao recriar o container, sem recompilar a aplicação; o procedimento e o comportamento quando a variável estiver ausente são documentados.
- [x] #5 O aviso respeita os temas claro e escuro e possui identificação acessível em texto, sem depender somente de cor ou ícone.
- [x] #6 A implementação é validada com configuração ativada e desativada, acesso direto e navegação; lint, build e testes relevantes do student-spa passam.
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. Criar aviso compartilhado e aplicá-lo no cabeçalho persistente público e antes dos formulários de autenticação.
2. Adicionar DIDACTIC_NOTICE_ENABLED ao schema de runtime, template, entrypoint e Compose local/Komodo, ativo por padrão e desativável explicitamente com false.
3. Documentar ativação e remoção por ambiente sem rebuild.
4. Validar flags, rotas e navegação com testes, conferir desktop/celular e temas e executar lint, build e testes do student-spa.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Implementação autorizada pelo usuário após revisão do plano. Branch feature/task-41-aviso-didatico; worktree ../code-for-coders-aviso-didatico.

Validação visual do bundle de produção: desktop 1440px e celulares 390/320px, temas claro/escuro, sem overflow e aviso persistente ao rolar. Contraste medido: 4,84:1 no claro e 9,46:1 no escuro. Menu mobile navega para cadastro com o aviso antes dos campos. Runtime false remove avisos sem rebuild; entrypoint real validado com variável ausente, vazia, true e false, mantendo hashes dos assets. Detector Impeccable sem achados. Lint e build aprovados. Duas execuções completas concorrentes tiveram falhas transitórias de espera em suítes existentes (vídeo/transcrição e compra); em validação com um worker.

Compose validado por interpolação real com valores de teste em local, Komodo web e composição estável/remota completa: variável ausente resolve true e false explícito é preservado. Critérios 1–5 comprovados por testes das rotas, navegador e execução real do entrypoint.

Validação final: npm test -- --maxWorkers=1 passou com cobertura: 37 arquivos e 274 testes aprovados. As falhas variáveis das execuções concorrentes não se reproduziram na execução em série. Build passou após o ajuste final da faixa abaixo da navegação. Nenhum código ou timeout dos testes existentes foi alterado.
<!-- SECTION:NOTES:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
Aviso didático compartilhado na faixa persistente abaixo da navegação pública e antes dos formulários de acesso, cadastro, confirmação e recuperação. DIDACTIC_NOTICE_ENABLED controla a exibição em runtime, ativo por padrão e removível com false sem rebuild. Configuração local/Komodo e documentação atualizadas. Validado por lint, build, 274 testes com cobertura em série, testes reais do entrypoint/Compose e navegador em 320/390/1440px nos dois temas, incluindo rolagem e navegação mobile.
<!-- SECTION:FINAL_SUMMARY:END -->
