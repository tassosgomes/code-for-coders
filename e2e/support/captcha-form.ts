import { expect, type Locator } from '@playwright/test';

/**
 * Envia um formulário protegido pelo Turnstile e espera o resultado.
 *
 * O botão só habilita depois que o widget devolve o token, e o widget é remontado quando o tema resolve
 * (`key={theme}` em `CaptchaField`). Um clique que cai nessa janela é descartado em silêncio pelo handler
 * (`if (!captcha.token) return`). O clique é repetido até o resultado aparecer; como o clique descartado não
 * envia nada, repetir não duplica a requisição.
 */
export const submitCaptchaForm = async (submit: Locator, outcome: () => Promise<void>): Promise<void> => {
  await expect(async () => {
    await submit.click({ timeout: 5_000 });
    await outcome();
  }).toPass({ timeout: 45_000, intervals: [250, 500, 1_000] });
};
