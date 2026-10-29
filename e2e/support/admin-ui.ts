import { expect, type Page } from '@playwright/test';

import { submitCaptchaForm } from './captcha-form';
import { adminUrl } from './env';

/** Preenche e envia a tela de entrada do backoffice, como a equipe faria. */
export const submitStaffCredentials = async (page: Page, email: string, password: string, { expectError = false } = {}): Promise<void> => {
  await page.goto(adminUrl('/entrar'));
  await expect(page.getByRole('heading', { name: 'Entrar na operação' })).toBeVisible();
  await page.getByLabel('E-mail').fill(email);
  await page.getByLabel('Senha', { exact: true }).fill(password);
  await submitCaptchaForm(page.getByRole('button', { name: 'Entrar', exact: true }), () =>
    expectError
      ? expect(page.getByText('E-mail ou senha incorretos')).toBeVisible({ timeout: 5_000 })
      : expect(page.getByRole('heading', { name: 'Entrar na operação' })).toBeHidden({ timeout: 5_000 }),
  );
};

/** Entra e espera o painel inicial. */
export const signInStaff = async (page: Page, email: string, password: string): Promise<void> => {
  await submitStaffCredentials(page, email, password);
  await expect(page.getByRole('heading', { name: /^Olá, / })).toBeVisible();
};
