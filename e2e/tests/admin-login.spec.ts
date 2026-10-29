import { expect, test } from '@playwright/test';

import { submitStaffCredentials } from '../support/admin-ui';
import { adminUrl } from '../support/env';
import { readSeed } from '../support/seed';

test.describe('Backoffice: login', () => {
  test('o administrador entra, vê o painel e sai', async ({ page }) => {
    const { admin } = await readSeed();

    await submitStaffCredentials(page, admin.email, admin.password);

    await expect(page.getByRole('heading', { name: /^Olá, / })).toBeVisible();
    await expect(page.getByText(/Seus papéis:\s*administrador/)).toBeVisible();
    await expect(page.getByRole('link', { name: 'Acessos' }).first()).toBeVisible();

    await page.getByRole('button', { name: /^[A-Z]{1,2} / }).click();
    await page.getByRole('menuitem', { name: 'Sair' }).click();

    await expect(page).toHaveURL(/\/entrar/);
    await expect(page.getByText('Você saiu do backoffice.')).toBeVisible();
  });

  test('senha incorreta não abre sessão', async ({ page }) => {
    const { admin } = await readSeed();

    await submitStaffCredentials(page, admin.email, 'Senha!Errada-1', { expectError: true });

    await expect(page.getByText('E-mail ou senha incorretos')).toBeVisible();
    await expect(page).toHaveURL(/\/entrar/);
  });

  test('área interna sem sessão leva à tela de entrada', async ({ page }) => {
    await page.goto(adminUrl('/acessos'));

    await expect(page).toHaveURL(/\/entrar/);
    await expect(page.getByRole('heading', { name: 'Entrar na operação' })).toBeVisible();
  });
});
