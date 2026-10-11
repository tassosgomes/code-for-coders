---
id: TASK-19.3
title: 'Cortesias: passos Curso, Vigência, Motivo, Revisão e Resultado conforme Figma'
status: In Progress
assignee:
  - '@claude'
created_date: '2026-10-10 22:29'
updated_date: '2026-10-11 01:57'
labels:
  - frontend
dependencies:
  - TASK-19.1
modified_files:
  - src/admin-spa/src/features/courtesies/components/review-sentence.tsx
  - src/admin-spa/src/app/routes/courtesy-steps.test.tsx
parent_task_id: TASK-19
type: bug
ordinal: 22000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Parte da auditoria TASK-19 que cobre os passos 2 a 6 (frames T2.a–T6.b3). Bugs encontrados em dev: o erro de meses inválidos fica na tela ao trocar para Vitalícia ou voltar a um valor válido; com meses inválidos a mesma mensagem aparece duas vezes; a frase de revisão duplica o ponto final do motivo. Além disso faltam a tabela de cursos com Oferta, os cartões de vigência, a tabela de revisão com Editar, o botão de confirmação vitalícia, o link e o toast do sucesso e as telas de erro do Resultado.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 Curso: busca com botão na mesma linha e placeholder "Digite parte do título"; tabela Curso/Oferta com selo Com oferta/Sem oferta vindo de `hasOffer`; linha escolhida destacada com "✓ Escolhido"; rodapé "Só cursos com versão vigente." com paginação ‹ 1 2 ›; vazio e erro como no Figma
- [ ] #2 Vigência: cartões Por período e Vitalícia com subtítulos; input de meses estreito com ajuda ao lado; prévia "Termina em até DD/MM/AAAA — vindo do servidor." com "A prévia vale se a confirmação for hoje."; vitalícia "Acesso vitalício, sem data de término."; erro de meses some ao trocar de opção ou corrigir o valor e não se repete na prévia; falha da prévia mostra aviso com "Tentar de novo" e só Voltar
- [ ] #3 Nos passos Vigência e Motivo aparecem os cartões de resumo CURSO e VIGÊNCIA com Editar
- [ ] #4 Motivo: descrição "Diga por que esta cortesia existe. O motivo vai para a trilha.", rótulo Motivo, ajuda "Não inclua dado pessoal de terceiros." com contador N/500 na mesma linha; vazio e acima de 500 marcam o campo, mostram "Informe o motivo da cortesia." ou "Use até 500 caracteres: tire N." e deixam só Voltar
- [ ] #5 Revisão: frase sem vírgula antes de "até" e sem ponto duplicado; tabela Aluno/Curso/Vigência/Motivo com Editar por linha; aviso de acesso existente com o texto do Figma; vitalícia com botão "Confirmar cortesia vitalícia"; falha de confirmação em caixa de erro com "Tentar de novo" dentro do cartão
- [ ] #6 Resultado: sucesso com ícone, "Cortesia concedida.", cartão do curso, botão Conceder outra cortesia, link "Ver concessões do aluno" que volta ao passo Aluno com o aluno carregado, e toast; erros STUDENT_ACCOUNT_NOT_ELIGIBLE/COURSE_NOT_ELIGIBLE, IDEMPOTENCY_KEY_REUSED e 502/504 viram telas de Resultado com textos e ações do Figma
- [ ] #7 Testes do admin-spa cobrem os bugs de meses, a frase de revisão, a coluna Oferta e as telas de Resultado
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. Consumir hasOffer na tabela de cursos; CSS em assets/courtesies-steps.css.
2. Vigência com cartões, prévia e correção do erro de meses persistente/duplicado.
3. Motivo, Revisão (frase, tabela com Editar, aviso, vitalícia) e Resultado (sucesso com link/toast, telas de erro).
4. Resumos CURSO e VIGÊNCIA via StepSummary.
5. Testes.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Implementado por subagente: Curso em tabela com Oferta (hasOffer), Vigência em cartões com prévia (bugs B1/B2 corrigidos), Motivo com contador e erros, Revisão com frase corrigida (B3), tabela com Editar e botão vitalício, Resultado com link/toast e telas de erro T6.b1–b3. Lint ok, build ok, vitest 290/290 (51 arquivos).

Conferência em dev (2026-10-11): a frase da revisão baixava a primeira letra de siglas ("motivo: qA: …"). Agora só baixa quando a primeira palavra é capitalizada normalmente.
<!-- SECTION:NOTES:END -->
