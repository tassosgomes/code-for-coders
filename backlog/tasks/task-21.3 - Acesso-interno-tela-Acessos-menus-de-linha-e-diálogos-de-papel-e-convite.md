---
id: TASK-21.3
title: 'Acesso interno: tela Acessos, menus de linha e diálogos de papel e convite'
status: In Progress
assignee:
  - '@claude'
created_date: '2026-10-11 00:22'
updated_date: '2026-10-11 00:32'
labels:
  - frontend
dependencies: []
modified_files:
  - src/admin-spa/src/features/staff-access/components/staff-access-screen.tsx
  - src/admin-spa/src/features/staff-access/components/staff-role-labels.ts
  - src/admin-spa/src/features/staff-access/components/staff-dialog.tsx
  - src/admin-spa/src/features/staff-access/components/staff-dialog-notice.tsx
  - src/admin-spa/src/features/staff-access/components/reason-field.tsx
  - src/admin-spa/src/features/staff-access/components/role-radio-options.tsx
  - src/admin-spa/src/features/staff-access/components/staff-row-menu.tsx
  - src/admin-spa/src/features/staff-access/components/staff-member-row.tsx
  - src/admin-spa/src/features/staff-access/components/invitation-row.tsx
  - src/admin-spa/src/features/staff-access/components/invite-dialog.tsx
  - src/admin-spa/src/features/staff-access/components/grant-role-dialog.tsx
  - src/admin-spa/src/features/staff-access/components/revoke-role-dialog.tsx
  - src/admin-spa/src/features/staff-access/components/change-role-dialog.tsx
  - src/admin-spa/src/app/routes/staff-access-route.test.tsx
  - src/admin-spa/src/assets/staff-access.css
parent_task_id: TASK-21
type: bug
ordinal: 27000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Tela Acessos diverge do Figma (B6–B10): sem rodapés das abas, sem coluna Convidado em (a API já envia invitedAt), menu ⋯ só com texto e revogar por lista, ⋯ do convite abre o diálogo direto, diálogos com select e sem Cancelar, confirmações num aviso no topo. Decisão do usuário: uma linha 'Revogar <papel>' por papel no menu ⋯.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 Abas têm rodapés 'N pessoas · página X de Y' e 'N convites pendentes · cada convite vale 7 dias'; Convites tem a coluna 'Convidado em'
- [ ] #2 Menu ⋯ da pessoa tem ícones, 'Conceder papel', 'Trocar papel', separador e uma linha vermelha 'Revogar <papel>' por papel; quem não tem papel não mostra linhas de revogar; ⋯ do convite abre menu com 'Enviar novo convite'
- [ ] #3 Convidar usa cartões de rádio por papel e placeholder no e-mail; sucesso mostra ícone, '<e-mail> recebeu o convite como <Papel>. Ele vale até <data>.', aviso de convite anterior e botões 'Convidar outra pessoa' e 'Concluir'
- [ ] #4 Conceder: título 'Conceder papel a <nome>', Papéis atuais, rádios, nota 'Vale na próxima ação dele, sem desconectar', Cancelar e 'Conceder papel'. Revogar: título 'Revogar <Papel> de <nome>?', aviso que difere quando é o último papel, Cancelar e botão destrutivo 'Revogar e desconectar'. Trocar: colunas De/Para com rádios, linha 'Resultado:', aviso, Cancelar e 'Trocar e desconectar'
- [ ] #5 Confirmações de conceder, revogar e trocar aparecem como aviso no canto (toast) em vez do aviso no topo
- [ ] #6 Testes do admin-spa cobrem menu ⋯, revogar por papel e os diálogos
<!-- AC:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Tela dividida em componentes. Não existe --destructive no tema; usado --danger. Aviso dos diálogos de revogar/trocar em texto neutro: Sessão de {primeiro nome} será encerrada agora. Convites vazios seguem só com texto (sem Empty State com tile).
<!-- SECTION:NOTES:END -->
