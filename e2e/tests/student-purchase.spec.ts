import { expect, test } from '@playwright/test';

import { studentUrl } from '../support/env';
import { LESSON_TITLE, OFFER_NAME, readSeed } from '../support/seed';
import { newStudent, registerConfirmedStudent, signInStudent } from '../support/student';

test.describe('Aluno: compra avulsa', () => {
  test('escolhe o curso na vitrine, paga e recebe o acesso', async ({ page }) => {
    const seed = await readSeed();
    const student = newStudent('compra');
    await registerConfirmedStudent(student);

    // Vitrine pública: o visitante anônimo escolhe a opção e é levado a entrar antes de comprar.
    await page.goto(studentUrl('/cursos'));
    await page.getByRole('link', { name: seed.course.title }).first().click();
    await expect(page.getByRole('heading', { name: seed.course.title })).toBeVisible();
    await page.getByRole('link', { name: `Comprar ${OFFER_NAME}` }).click();
    await expect(page.getByText('Entre para continuar sua compra')).toBeVisible();

    // Depois de entrar, o aluno volta ao resumo da opção escolhida.
    await signInStudent(page, student);
    await expect(page.getByRole('heading', { name: 'Resumo da compra' })).toBeVisible();
    await expect(page.getByRole('heading', { name: seed.course.title })).toBeVisible();

    // O pagamento acontece no gateway (Stripe falso, ver e2e/fake-stripe), que devolve o webhook assinado ao billing.
    await page.getByRole('button', { name: 'Ir para o pagamento' }).click();
    await expect(page.getByRole('heading', { name: 'Pagamento de teste' })).toBeVisible();
    await expect(page.getByTestId('payment-description')).toContainText(OFFER_NAME);
    await page.getByRole('button', { name: 'Pagar com cartão de teste' }).click();

    // A volta do pagamento acompanha o pedido até o commerce liberar o acesso.
    await expect(page).toHaveURL(/\/pedidos\//);
    await expect(page.getByRole('heading', { name: 'Compra confirmada' })).toBeVisible({ timeout: 60_000 });
    await page.getByRole('link', { name: 'Ir para o curso' }).click();

    await expect(page).toHaveURL(new RegExp(`/aulas/${seed.course.lessonId}`));
    await expect(page.getByRole('heading', { name: LESSON_TITLE })).toBeVisible();
  });
});
