import { randomUUID } from 'node:crypto';
import { expect, type Page } from '@playwright/test';

import { submitCaptchaForm } from './captcha-form';
import { env, studentUrl } from './env';
import { extractLink, waitForMail } from './mail';
import { STRONG_PASSWORD } from './seed';

export type Student = { name: string; email: string; password: string };

/** Aluno novo e único por teste: a suíte nunca depende de dados deixados por outra execução. */
export const newStudent = (label: string): Student => ({
  name: `Aluno E2E ${label}`,
  email: `e2e-${label}-${Date.now()}-${randomUUID().slice(0, 8)}@example.test`,
  password: STRONG_PASSWORD,
});

/** Cria a conta pela API do BFF (sem UI) e confirma o e-mail pelo link recebido. */
export const registerConfirmedStudent = async (student: Student): Promise<void> => {
  const response = await fetch(`${env.studentApiOrigin}/api/v1/student-accounts`, {
    method: 'POST',
    headers: { 'content-type': 'application/json', 'idempotency-key': randomUUID(), 'x-captcha-token': env.turnstileDummyToken, origin: env.studentOrigin },
    body: JSON.stringify({ name: student.name, email: student.email, password: student.password }),
  });
  if (!response.ok) throw new Error(`Cadastro do aluno: HTTP ${response.status} ${await response.text()}`);

  const mail = await waitForMail({ to: student.email, subjectIncludes: 'Confirme' });
  const token = new URL(extractLink(mail, '/confirm-account')).searchParams.get('token');
  if (!token) throw new Error('O e-mail de confirmação não trouxe token.');
  const confirmation = await fetch(`${env.studentApiOrigin}/api/v1/account-confirmations`, {
    method: 'POST',
    headers: { 'content-type': 'application/json', 'idempotency-key': randomUUID(), origin: env.studentOrigin },
    body: JSON.stringify({ token }),
  });
  if (!confirmation.ok) throw new Error(`Confirmação da conta: HTTP ${confirmation.status} ${await confirmation.text()}`);
};

/**
 * Entra pela tela de login, como o aluno faria. Se já está nela (ex.: vindo de "Comprar"), não navega de novo.
 * Com `expectError`, espera o alerta de falha em vez da saída da tela.
 */
export const signInStudent = async (page: Page, student: Student, { expectError = false } = {}): Promise<void> => {
  if (!/\/entrar(\?|$)/.test(page.url())) await page.goto(studentUrl('/entrar'));
  await expect(page.getByRole('heading', { name: 'Entrar', exact: true })).toBeVisible();
  await page.getByLabel('E-mail').fill(student.email);
  await page.getByLabel('Senha', { exact: true }).fill(student.password);
  await submitCaptchaForm(page.getByRole('button', { name: 'Entrar', exact: true }), () =>
    expectError
      ? expect(page.getByText('Não foi possível entrar')).toBeVisible({ timeout: 5_000 })
      : expect(page.getByRole('heading', { name: 'Entrar', exact: true })).toBeHidden({ timeout: 5_000 }),
  );
};

/**
 * Compra a oferta do cenário pela interface e espera o acesso ser liberado (webhook do Stripe falso -> billing ->
 * commerce). Os specs de aula usam este atalho; o passo a passo comentado está em `student-purchase.spec.ts`.
 */
export const purchaseSeededCourse = async (page: Page, student: Student, offer: { offerId: string; courseId: string }): Promise<void> => {
  await page.goto(studentUrl(`/comprar/${offer.offerId}?courseId=${offer.courseId}`));
  // Sem sessão, a compra pede a entrada e volta ao resumo depois dela.
  await signInStudent(page, student);
  await page.getByRole('button', { name: 'Ir para o pagamento' }).click();
  await page.getByRole('button', { name: 'Pagar com cartão de teste' }).click();
  await expect(page.getByRole('heading', { name: 'Compra confirmada' })).toBeVisible({ timeout: 60_000 });
};
