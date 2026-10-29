import { fileURLToPath } from 'node:url';
import path from 'node:path';

const here = path.dirname(fileURLToPath(import.meta.url));

const trimSlash = (value: string) => value.replace(/\/+$/, '');

/**
 * Configuração da suíte, toda por variável de ambiente. Os padrões são os do modo local de
 * `scripts/apps.sh start` (o mesmo usado no CI), então a suíte roda sem configurar nada contra a stack local.
 */
export const env = {
  repoRoot: path.resolve(here, '..', '..'),
  stateFile: path.resolve(here, '..', '.state', 'seed.json'),
  fixtureVideo: path.resolve(here, '..', 'fixtures', 'aula-e2e.mp4'),

  /** Origem do student-spa (nginx) e caminho-base do app. */
  studentOrigin: trimSlash(process.env.E2E_STUDENT_ORIGIN ?? 'http://localhost:8082'),
  studentBasePath: process.env.E2E_STUDENT_BASE_PATH ?? '/student',
  /** Origem do admin-spa (nginx) e caminho-base do app. */
  adminOrigin: trimSlash(process.env.E2E_ADMIN_ORIGIN ?? 'http://localhost:8081'),
  adminBasePath: process.env.E2E_ADMIN_BASE_PATH ?? '/admin',

  /**
   * Origem usada pelas chamadas de API feitas pelo Node (seed, atalhos). Igual à do navegador, salvo quando o
   * `localhost` do navegador é remapeado (E2E_HOST_RULES) para contornar uma porta ocupada na máquina local.
   */
  studentApiOrigin: trimSlash(process.env.E2E_STUDENT_API_ORIGIN ?? process.env.E2E_STUDENT_ORIGIN ?? 'http://localhost:8082'),
  adminApiOrigin: trimSlash(process.env.E2E_ADMIN_API_ORIGIN ?? process.env.E2E_ADMIN_ORIGIN ?? 'http://localhost:8081'),

  /** smtp4dev que recebe os e-mails transacionais (confirmação, convite, redefinição). */
  smtpUrl: trimSlash(process.env.E2E_SMTP4DEV_URL ?? 'http://127.0.0.1:5000'),
  smtpUser: process.env.E2E_SMTP4DEV_USERNAME ?? '',
  smtpPassword: process.env.E2E_SMTP4DEV_PASSWORD ?? '',

  tenantId: process.env.E2E_TENANT_ID ?? process.env.STUDENT_TENANT_ID ?? '00000000-0000-7000-8000-000000000001',
  identityContainer: process.env.E2E_IDENTITY_CONTAINER ?? 'code-for-coders-identity',

  /**
   * Administrador pré-existente. Sem estas variáveis, o setup provisiona o primeiro administrador do tenant
   * (stack efêmera, tenant vazio) e define a senha pelo link recebido por e-mail.
   */
  adminEmail: process.env.E2E_ADMIN_EMAIL ?? '',
  adminPassword: process.env.E2E_ADMIN_PASSWORD ?? '',

  /** Token aceito pelo siteverify com as chaves de teste oficiais do Turnstile (sempre aprova). */
  turnstileDummyToken: 'XXXX.DUMMY.TOKEN.XXXX',
} as const;

export const studentUrl = (route = '') => `${env.studentOrigin}${env.studentBasePath}${route}`;
export const adminUrl = (route = '') => `${env.adminOrigin}${env.adminBasePath}${route}`;
