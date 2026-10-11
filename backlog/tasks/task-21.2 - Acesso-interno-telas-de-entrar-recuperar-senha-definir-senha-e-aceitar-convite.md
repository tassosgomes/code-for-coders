---
id: TASK-21.2
title: >-
  Acesso interno: telas de entrar, recuperar senha, definir senha e aceitar
  convite
status: Done
assignee:
  - '@claude'
created_date: '2026-10-11 00:22'
updated_date: '2026-10-11 14:08'
labels:
  - frontend
dependencies: []
modified_files:
  - src/admin-spa/src/components/auth-layout.tsx
  - src/admin-spa/src/components/password-requirements.tsx
  - src/admin-spa/src/components/auth-result-card.tsx
  - src/admin-spa/src/components/auth-notice.tsx
  - src/admin-spa/src/features/staff-session/components/staff-login-screen.tsx
  - >-
    src/admin-spa/src/features/staff-session/components/staff-login-screen.test.tsx
  - >-
    src/admin-spa/src/features/staff-password-recovery/components/staff-password-recovery-screen.tsx
  - >-
    src/admin-spa/src/features/staff-password-recovery/components/staff-password-recovery-screen.test.tsx
  - >-
    src/admin-spa/src/features/staff-password-reset/components/staff-password-reset-screen.tsx
  - >-
    src/admin-spa/src/features/staff-password-reset/components/staff-password-reset-screen.test.tsx
  - >-
    src/admin-spa/src/features/staff-access/components/staff-invitation-acceptance-screen.tsx
  - src/admin-spa/src/app/routes/staff-invitation-acceptance-route.test.tsx
  - src/admin-spa/src/assets/auth-screens.css
parent_task_id: TASK-21
type: bug
ordinal: 26000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Telas de autenticação do backoffice divergem do Figma (B1–B4): sem placeholders, aviso 'Sessão Expirada', sem aviso ao sair, textos de recuperar senha diferentes, telas de resultado sem ícone/título próprio, aceite de convite sem resumo e com o campo de nome dentro do bloco do campo de senha, e detalhes no painel escuro.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 Entrar tem placeholders 'voce@escola.com' e 'Sua senha'; sessão encerrada mostra 'Sua sessão foi encerrada' / 'Entre de novo para continuar.'; após sair aparece o aviso 'Você saiu do backoffice.'
- [x] #2 Recuperar senha usa o botão 'Receber link de recuperação' e o link 'Voltar para a entrada'; o envio mostra o cartão 'Confira seu e-mail' com ícone e botão 'Voltar para a entrada'
- [x] #3 Definir senha: sucesso mostra cartão com ícone 'Senha definida' e botão 'Entrar com a nova senha'; link sem token ou inválido mostra cartão 'Este link não vale mais' com 'Pedir novo link' e 'Voltar para a entrada'
- [x] #4 Aceitar convite: carregamento 'Conferindo seu convite…' com esqueleto; formulário com título 'Você foi convidado para a equipe', resumo Papel oferecido/Vale até, campos 'Seu nome' e 'Crie uma senha' em blocos separados e botão 'Aceitar e entrar'; convite inválido mostra cartão 'Este convite não vale mais' com 'Já tem conta? Entrar'
- [x] #5 Painel escuro: só 'acesso por papel' em roxo (ponto final na cor do texto) e coloração do código conforme Figma
- [x] #6 Testes do admin-spa cobrem os novos estados e textos
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
AuthResultCard e AuthNotice compartilhados; textos, placeholders e estados de B1–B4 nas telas de entrar, recuperar/definir senha e convite; aviso de saída via loggedOutStaffSessionMarker; painel escuro; testes.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Link inválido = 422 RESET_TOKEN_INVALID (contracts/bff-admin/openapi-acesso-interno.yaml); demais falhas mantêm o formulário com caixa de erro. Convite: INVITATION_INVALID → cartão Este convite não vale mais; INVITATION_EMAIL_UNAVAILABLE → cartão E-mail indisponível. Aviso de saída lê loggedOutStaffSessionMarker e some em 6s. Requisitos da senha em linha com quebra (gap 6px 16px), como no Figma.
<!-- SECTION:NOTES:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
Telas de entrar, recuperar senha, definir senha e aceitar convite alinhadas ao Figma B1–B4 (#210, correção Sonar S6438). Verificado em dev (placeholders, aviso 'Você saiu do backoffice.' ao sair, painel escuro), em servidor local (recuperar senha, link sem token, convite inválido) e por testes dos estados de sessão encerrada, Confira seu e-mail, Senha definida, token rejeitado e convite; admin-spa em main (2026-10-11): lint ok, build ok, vitest 52 arquivos/313 testes.
<!-- SECTION:FINAL_SUMMARY:END -->
