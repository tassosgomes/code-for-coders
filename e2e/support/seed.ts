import { randomBytes } from 'node:crypto';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { spawnSync } from 'node:child_process';
import path from 'node:path';

import { AdminApi } from './admin-api';
import { env } from './env';
import { extractLink, waitForMail } from './mail';

export const LESSON_TITLE = 'Aula E2E com player';
export const OFFER_NAME = 'Acesso E2E por 12 meses';
export const OFFER_PRICE_CENTS = 19_900;
/** Senha descartável por processo que cumpre a política (8+, maiúscula, minúscula, número e símbolo). */
export const STRONG_PASSWORD = `Aa1!${randomBytes(12).toString('base64url')}`;

export type Seed = {
  admin: { email: string; password: string };
  /** Conta da equipe com os papéis de professor e financeiro, usada para montar o curso e a oferta. */
  editor: { email: string; password: string };
  /** O título leva a data da montagem: a vitrine de um ambiente reutilizado pode ter cursos de execuções anteriores. */
  course: { courseId: string; title: string; lessonId: string; offerId: string };
};

const sleep = (milliseconds: number) => new Promise((resolve) => setTimeout(resolve, milliseconds));

export const readSeed = async (): Promise<Seed> => JSON.parse(await readFile(env.stateFile, 'utf8')) as Seed;

const log = (message: string) => console.log(`[e2e-seed] ${message}`);

/**
 * Garante um administrador com senha conhecida. Em stack efêmera o tenant está vazio: provisiona o primeiro
 * administrador (comando oficial do Identity) e define a senha pelo link do e-mail, como um humano faria.
 * Contra um ambiente que já tem administrador, informe E2E_ADMIN_EMAIL/E2E_ADMIN_PASSWORD.
 */
const ensureAdmin = async (): Promise<Seed['admin']> => {
  if (env.adminEmail && env.adminPassword) {
    log(`usando o administrador informado (${env.adminEmail})`);
    return { email: env.adminEmail, password: env.adminPassword };
  }

  const email = `e2e-admin-${Date.now()}@example.test`;
  const result = spawnSync(
    'docker',
    ['exec', env.identityContainer, 'dotnet', 'CodeForCoders.Identity.Api.dll', 'provision-first-admin', '--tenant', env.tenantId, '--email', email, '--name', 'Administrador E2E'],
    { encoding: 'utf8' },
  );
  const output = `${result.stdout}${result.stderr}`;
  if (result.status !== 0) throw new Error(`provision-first-admin falhou: ${output}`);
  if (output.includes('already has an administrator')) {
    throw new Error('O tenant já tem administrador. Informe E2E_ADMIN_EMAIL e E2E_ADMIN_PASSWORD.');
  }

  const body = await waitForMail({ to: email, subjectIncludes: '' });
  const token = new URL(extractLink(body, '/redefinir-senha')).searchParams.get('token');
  if (!token) throw new Error('O e-mail de definição de senha não trouxe token.');

  const response = await fetch(`${env.adminApiOrigin}/api/v1/staff-password-resets`, {
    method: 'POST',
    headers: { 'content-type': 'application/json', 'idempotency-key': crypto.randomUUID(), origin: env.adminOrigin },
    body: JSON.stringify({ token, newPassword: STRONG_PASSWORD }),
  });
  if (!response.ok) throw new Error(`Definir a senha do admin: HTTP ${response.status} ${await response.text()}`);
  log(`primeiro administrador provisionado (${email})`);
  return { email, password: STRONG_PASSWORD };
};

/**
 * O administrador só tem `acesso.gerir`. Quem monta o curso (autoria, mídia e oferta) é uma conta da equipe criada
 * pelo próprio fluxo de convite: professor + financeiro.
 */
const ensureEditor = async (admin: AdminApi): Promise<Seed['editor']> => {
  const email = `e2e-editor-${Date.now()}@example.test`;
  await admin.inviteStaff(email, 'professor', 'Conta de montagem do cenário da suíte e2e');
  const body = await waitForMail({ to: email, subjectIncludes: '' });
  const token = new URL(extractLink(body, '/convite')).searchParams.get('token');
  if (!token) throw new Error('O e-mail de convite não trouxe token.');
  const session = await AdminApi.acceptInvitation(token, 'Editor E2E', STRONG_PASSWORD);
  await admin.grantRole(session.accountId, 'financeiro', 'Monta a oferta do curso do cenário e2e');
  return { email, password: STRONG_PASSWORD };
};

const seedCourse = async (api: AdminApi): Promise<Seed['course']> => {
  log('enviando o vídeo da aula');
  const videoId = await api.uploadVideo('Vídeo E2E', env.fixtureVideo);

  log('criando e publicando o curso');
  const title = `Curso E2E ${new Date().toISOString().slice(0, 19).replace('T', ' ')}`;
  let course = await api.createCourse(title, 'Curso criado pela suíte e2e para validar compra e reprodução.');
  course = await api.updateCourse(course.courseId, { level: 'beginner' });
  course = await api.createModule(course.courseId, 'Módulo E2E');
  const moduleId = course.modules[0].moduleId;

  log('aguardando a preparação do vídeo (ffmpeg/HLS)');
  await api.waitForVideoReady(videoId);

  course = await api.createLesson(course.courseId, moduleId, LESSON_TITLE, videoId);
  const lessonId = course.modules[0].lessons[0].lessonId;
  await api.publishCourse(course.courseId, course.draftRevision);

  log('criando e publicando a oferta');
  await api.waitForCatalogCourse(course.courseId);
  await api.setTagline(course.courseId, 'Curso de verificação automática dos fluxos críticos.');
  const offer = await api.createOffer(course.courseId, OFFER_NAME, OFFER_PRICE_CENTS);
  await api.publishOffer(offer.offerId);

  return { courseId: course.courseId, title, lessonId, offerId: offer.offerId };
};

/** Monta o cenário uma vez por execução e grava em `e2e/.state/seed.json`. */
export const seedScenario = async (): Promise<Seed> => {
  const admin = await ensureAdmin();
  const adminApi = await AdminApi.signIn(admin.email, admin.password);
  let editor: Seed['editor'];
  try {
    editor = await ensureEditor(adminApi);
  } finally {
    await adminApi.dispose();
  }

  const api = await AdminApi.signIn(editor.email, editor.password);
  try {
    const course = await seedCourse(api);
    const seed: Seed = { admin, editor, course };
    await mkdir(path.dirname(env.stateFile), { recursive: true });
    await writeFile(env.stateFile, JSON.stringify(seed, null, 2));
    log('cenário pronto');
    return seed;
  } finally {
    await api.dispose();
  }
};

export { sleep };
