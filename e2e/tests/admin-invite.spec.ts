import { expect, test } from '@playwright/test';

import { signInStaff } from '../support/admin-ui';
import { adminUrl } from '../support/env';
import { extractLink, waitForMail } from '../support/mail';
import { readSeed, STRONG_PASSWORD } from '../support/seed';

test.describe('Backoffice: convite da equipe', () => {
  test('o administrador convida uma pessoa e ela aceita o convite', async ({ page }) => {
    const { admin } = await readSeed();
    const invitee = `e2e-convite-${Date.now()}@example.test`;

    await signInStaff(page, admin.email, admin.password);
    await page.goto(adminUrl('/acessos'));
    await page.getByRole('button', { name: 'Convidar' }).click();

    const dialog = page.getByRole('dialog', { name: 'Convidar para a equipe' });
    await dialog.getByLabel('E-mail').fill(invitee);
    await dialog.getByRole('radio', { name: /^Professor/ }).check();
    await dialog.getByLabel('Motivo').fill('Convite criado pela suíte e2e para validar o aceite');
    await dialog.getByRole('button', { name: 'Enviar convite' }).click();

    const sent = page.getByRole('dialog', { name: 'Convite enviado' });
    await expect(sent).toContainText(invitee);
    await sent.getByRole('button', { name: 'Concluir' }).click();
    await page.getByRole('tab', { name: /Convites pendentes/ }).click();
    await expect(page.getByText(invitee)).toBeVisible();

    // O convidado recebe o link por e-mail, define o nome e a senha e já entra com o papel oferecido.
    const mail = await waitForMail({ to: invitee, subjectIncludes: '' });
    const invitationPage = await page.context().newPage();
    await invitationPage.goto(extractLink(mail, '/convite'));
    await expect(invitationPage.getByRole('heading', { name: 'Você foi convidado para a equipe' })).toBeVisible();
    await invitationPage.getByLabel('Seu nome').fill('Pessoa Convidada E2E');
    await invitationPage.getByLabel('Crie uma senha').fill(STRONG_PASSWORD);
    await invitationPage.getByRole('button', { name: 'Aceitar e entrar' }).click();

    await expect(invitationPage.getByRole('heading', { name: 'Olá, Pessoa' })).toBeVisible();
    await expect(invitationPage.getByText(/Seus papéis:\s*professor/)).toBeVisible();
  });

  test('convite já usado não vale mais', async ({ page }) => {
    const { admin } = await readSeed();
    const invitee = `e2e-convite-usado-${Date.now()}@example.test`;

    await signInStaff(page, admin.email, admin.password);
    await page.goto(adminUrl('/acessos'));
    await page.getByRole('button', { name: 'Convidar' }).click();
    const dialog = page.getByRole('dialog', { name: 'Convidar para a equipe' });
    await dialog.getByLabel('E-mail').fill(invitee);
    await dialog.getByLabel('Motivo').fill('Convite para validar que o link só vale uma vez');
    await dialog.getByRole('button', { name: 'Enviar convite' }).click();
    await expect(page.getByRole('dialog', { name: 'Convite enviado' })).toBeVisible();

    const link = extractLink(await waitForMail({ to: invitee, subjectIncludes: '' }), '/convite');
    const first = await page.context().newPage();
    await first.goto(link);
    await first.getByLabel('Seu nome').fill('Primeiro Aceite E2E');
    await first.getByLabel('Crie uma senha').fill(STRONG_PASSWORD);
    await first.getByRole('button', { name: 'Aceitar e entrar' }).click();
    await expect(first.getByRole('heading', { name: /^Olá, / })).toBeVisible();

    const second = await page.context().newPage();
    await second.goto(link);
    await expect(second.getByRole('heading', { name: 'Este convite não vale mais' })).toBeVisible();
  });
});
