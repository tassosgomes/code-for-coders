---
id: TASK-21.2
title: >-
  Acesso interno: telas de entrar, recuperar senha, definir senha e aceitar
  convite
status: In Progress
assignee:
  - '@claude'
created_date: '2026-10-11 00:22'
updated_date: '2026-10-11 00:32'
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
- [ ] #1 Entrar tem placeholders 'voce@escola.com' e 'Sua senha'; sessão encerrada mostra 'Sua sessão foi encerrada' / 'Entre de novo para continuar.'; após sair aparece o aviso 'Você saiu do backoffice.'
- [ ] #2 Recuperar senha usa o botão 'Receber link de recuperação' e o link 'Voltar para a entrada'; o envio mostra o cartão 'Confira seu e-mail' com ícone e botão 'Voltar para a entrada'
- [ ] #3 Definir senha: sucesso mostra cartão com ícone 'Senha definida' e botão 'Entrar com a nova senha'; link sem token ou inválido mostra cartão 'Este link não vale mais' com 'Pedir novo link' e 'Voltar para a entrada'
- [ ] #4 Aceitar convite: carregamento 'Conferindo seu convite…' com esqueleto; formulário com título 'Você foi convidado para a equipe', resumo Papel oferecido/Vale até, campos 'Seu nome' e 'Crie uma senha' em blocos separados e botão 'Aceitar e entrar'; convite inválido mostra cartão 'Este convite não vale mais' com 'Já tem conta? Entrar'
- [ ] #5 Painel escuro: só 'acesso por papel' em roxo (ponto final na cor do texto) e coloração do código conforme Figma
- [ ] #6 Testes do admin-spa cobrem os novos estados e textos
<!-- AC:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Link inválido = 422 RESET_TOKEN_INVALID (contracts/bff-admin/openapi-acesso-interno.yaml); demais falhas mantêm o formulário com caixa de erro. Convite: INVITATION_INVALID → cartão Este convite não vale mais; INVITATION_EMAIL_UNAVAILABLE → cartão E-mail indisponível. Aviso de saída lê loggedOutStaffSessionMarker e some em 6s. Requisitos da senha em linha com quebra (gap 6px 16px), como no Figma.
<!-- SECTION:NOTES:END -->
