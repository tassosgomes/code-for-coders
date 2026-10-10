---
id: TASK-19.2
title: 'Cortesias: stepper, resumos de passo e passo Aluno conforme Figma'
status: In Progress
assignee:
  - '@claude'
created_date: '2026-10-10 22:29'
updated_date: '2026-10-10 22:34'
labels:
  - frontend
dependencies: []
modified_files:
  - src/admin-spa/src/features/courtesies/components/step-summary.tsx
  - src/admin-spa/src/assets/courtesies-flow.css
  - src/admin-spa/src/assets/app.css
  - src/admin-spa/src/features/courtesies/components/courtesy-student-screen.tsx
  - src/admin-spa/src/features/courtesies/components/student-account-card.tsx
  - src/admin-spa/src/features/courtesies/components/student-access-grants.tsx
  - src/admin-spa/src/features/courtesies/components/access-grant-row.tsx
  - src/admin-spa/src/features/courtesies/hooks/use-courtesy-student-lookup.ts
  - src/admin-spa/src/app/routes/courtesy-student-lookup.test.tsx
  - src/admin-spa/src/app/routes/courtesy-student-grants.test.tsx
parent_task_id: TASK-19
type: bug
ordinal: 21000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Parte da auditoria TASK-19 que cobre o cabeçalho do fluxo e o passo 1 (frames T0.a, T0.c, T1.b–T1.g2, T6.a sem stepper). Hoje: o passo 3 se chama Prazo, o stepper não marca passos concluídos nem tem conectores, o resumo é só "Aluno: Nome" com botão desabilitado na revisão, o erro de aluno inexistente vem em alerta sem ponto, o cartão do aluno não tem avatar e tem botão Trocar aluno, e as concessões são uma lista sem cabeçalho com "Ver mais".
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 Stepper usa os rótulos Aluno, Curso, Vigência, Motivo, Revisão, Resultado; passos concluídos mostram check e conector destacado; o atual tem círculo contornado; o stepper some no passo Resultado
- [ ] #2 Nos passos Curso, Vigência e Motivo aparece o cartão de resumo ALUNO com "Nome · e-mail" e link Editar que volta ao passo Aluno
- [ ] #3 Aluno inexistente marca o input como inválido e mostra abaixo dele "Não há conta de aluno com este e-mail." e "Confira o e-mail e tente de novo."; falha de serviço mostra caixa de erro com "Não foi possível localizar agora." e "Tentar de novo"; carregamento usa blocos esqueleto
- [ ] #4 Cartão do aluno tem avatar com iniciais, selo Ativa ou Desativada, aviso de e-mail não confirmado com o texto e selo do Figma, aviso de conta desativada com o texto do Figma, e as ações Voltar e Continuar no lugar de Trocar aluno
- [ ] #5 Concessões aparecem como CONCESSÕES DESTA CONTA com "10/página", tabela Curso/Origem/Vigência/Situação, rodapé "Vigência sempre em data absoluta.", paginação de 10 por página e vazio "Nenhuma concessão para este aluno ainda."
- [ ] #6 Testes do admin-spa cobrem stepper, resumo, erro de aluno inexistente e paginação das concessões
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. StepSummary compartilhado (feito antes da divisão) e CSS em assets/courtesies-flow.css.
2. Stepper com rótulo Vigência, check, conectores, contorno no atual; oculto no Resultado.
3. Resumo ALUNO com Editar nos passos Curso a Motivo.
4. Erros/estados do lookup, cartão do aluno e tabela paginada de concessões.
5. Testes.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Implementado por subagente: stepper com check/conectores e oculto no Resultado, resumo ALUNO, erros e esqueleto do lookup, cartão do aluno com avatar/selos, concessões em tabela paginada (10/página, cliente). Lint ok; vitest 277/277; tsc pendente só da prop onViewGrants da 19.3.
<!-- SECTION:NOTES:END -->
