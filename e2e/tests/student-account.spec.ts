import { expect, test } from '@playwright/test';

import { submitCaptchaForm } from '../support/captcha-form';
import { studentUrl } from '../support/env';
import { extractLink, waitForMail } from '../support/mail';
import { newStudent, registerConfirmedStudent, signInStudent } from '../support/student';

test.describe('Aluno: conta e login', () => {
  test('cria a conta, confirma o e-mail e entra', async ({ page }) => {
    const student = newStudent('conta');
    const since = new Date(Date.now() - 2_000);

    await page.goto(studentUrl('/cadastro'));
    await expect(page.getByRole('heading', { name: 'Criar conta' })).toBeVisible();
    await page.getByLabel('Nome').fill(student.name);
    await page.getByLabel('E-mail').fill(student.email);
    await page.getByLabel('Senha', { exact: true }).fill(student.password);
    await submitCaptchaForm(page.getByRole('button', { name: 'Criar conta' }), () =>
      expect(page.getByRole('heading', { name: 'Confira seu e-mail' })).toBeVisible({ timeout: 5_000 }),
    );

    // O link de confirmação chega por e-mail (smtp4dev) e leva ao SPA.
    const mail = await waitForMail({ to: student.email, subjectIncludes: 'Confirme', since });
    await page.goto(extractLink(mail, '/confirm-account'));
    await expect(page.getByText('E-mail confirmado')).toBeVisible();

    await page.getByRole('link', { name: 'Entrar na plataforma' }).click();
    await signInStudent(page, student);

    await expect(page).not.toHaveURL(/\/entrar/);
    await expect(page.getByRole('region', { name: 'Sua conta' })).toBeVisible();
  });

  test('senha incorreta não abre sessão', async ({ page }) => {
    const student = newStudent('senha-errada');
    await registerConfirmedStudent(student);

    await signInStudent(page, { ...student, password: 'Outra!Senha1' }, { expectError: true });

    await expect(page.getByText('Não foi possível entrar')).toBeVisible();
    await expect(page).toHaveURL(/\/entrar/);
  });
});
