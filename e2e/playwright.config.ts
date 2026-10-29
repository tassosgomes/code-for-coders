import { defineConfig } from '@playwright/test';

const ci = !!process.env.CI;

export default defineConfig({
  testDir: './tests',
  globalSetup: './global-setup.ts',
  // Cada teste cria o próprio aluno; o cenário compartilhado (curso/oferta) é só leitura.
  fullyParallel: false,
  workers: Number(process.env.E2E_WORKERS ?? 1),
  // Sem retry de propósito: a suíte precisa provar estabilidade (TASK-42.3, 10 execuções sem flake).
  retries: 0,
  forbidOnly: ci,
  timeout: 90_000,
  expect: { timeout: 15_000 },
  reporter: ci ? [['list'], ['html', { open: 'never' }], ['github']] : [['list']],
  use: {
    // H.264 (HLS do vídeo) exige o Google Chrome; o Chromium do Playwright não decodifica.
    channel: process.env.E2E_BROWSER_CHANNEL ?? 'chrome',
    headless: true,
    // A página de pagamento do Stripe falso usa certificado autoassinado (ver e2e/fake-stripe/make-cert.sh).
    ignoreHTTPSErrors: true,
    // Só para máquinas em que uma porta de `localhost` já está ocupada (ex.: E2E_HOST_RULES="MAP localhost 127.0.0.2").
    launchOptions: process.env.E2E_HOST_RULES ? { args: [`--host-resolver-rules=${process.env.E2E_HOST_RULES}`] } : {},
    locale: 'pt-BR',
    timezoneId: 'America/Sao_Paulo',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'off',
    actionTimeout: 15_000,
    navigationTimeout: 30_000,
  },
  projects: [{ name: 'chrome', use: { viewport: { width: 1280, height: 800 } } }],
});
