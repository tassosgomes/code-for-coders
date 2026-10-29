import { randomUUID } from 'node:crypto';
import { readFile } from 'node:fs/promises';
import { request, type APIRequestContext, type APIResponse } from '@playwright/test';

import { env } from './env';

const sleep = (milliseconds: number) => new Promise((resolve) => setTimeout(resolve, milliseconds));

export type StaffSession = { accountId: string; name: string; roles: string[]; permissions: string[]; csrfToken: string };
export type CourseDetail = {
  courseId: string;
  draftRevision: number;
  modules: Array<{ moduleId: string; lessons: Array<{ lessonId: string }> }>;
};

/**
 * Cliente de API do backoffice usado só para montar o cenário (seed). Os fluxos que a suíte valida pela
 * interface não passam por aqui. Fala com o mesmo caminho do navegador: nginx do admin-spa -> bff-admin.
 */
export class AdminApi {
  private csrfToken = '';

  private constructor(private readonly http: APIRequestContext) {}

  static async signIn(email: string, password: string): Promise<AdminApi> {
    const http = await request.newContext({ baseURL: env.adminApiOrigin, extraHTTPHeaders: { Origin: env.adminOrigin } });
    const api = new AdminApi(http);
    const response = await http.post('/api/v1/staff-sessions', {
      data: { email, password },
      headers: { 'Idempotency-Key': randomUUID(), 'X-Captcha-Token': env.turnstileDummyToken },
    });
    await api.expectOk(response, 'login do admin');
    const session = await api.currentSession();
    api.csrfToken = session.csrfToken;
    return api;
  }

  async dispose() {
    await this.http.dispose();
  }

  async currentSession(): Promise<StaffSession> {
    return this.json(await this.http.get('/api/v1/staff-sessions/current'), 'sessão atual');
  }

  private async expectOk(response: APIResponse, what: string) {
    if (!response.ok()) {
      throw new Error(`${what}: HTTP ${response.status()} ${await response.text()}`);
    }
  }

  private async json<T>(response: APIResponse, what: string): Promise<T> {
    await this.expectOk(response, what);
    return (await response.json()) as T;
  }

  private write(method: 'post' | 'patch' | 'put' | 'delete', path: string, data?: unknown) {
    return this.http.fetch(`/api/v1${path}`, {
      method,
      data,
      headers: { 'Idempotency-Key': randomUUID(), 'X-CSRF-Token': this.csrfToken },
    });
  }

  async inviteStaff(email: string, role: string, reason: string): Promise<{ invitationId: string }> {
    return this.json(await this.write('post', '/staff-invitations', { email, role, reason }), 'convidar membro da equipe');
  }

  async grantRole(accountId: string, role: string, reason: string) {
    await this.expectOk(await this.write('post', `/staff-members/${accountId}/role-grants`, { role, reason }), 'conceder papel');
  }

  /** Aceita um convite (sem sessão) e devolve a sessão criada; o aceite já entra na conta. */
  static async acceptInvitation(token: string, name: string, password: string): Promise<StaffSession> {
    const response = await fetch(`${env.adminApiOrigin}/api/v1/staff-invitation-acceptances`, {
      method: 'POST',
      headers: { 'content-type': 'application/json', 'idempotency-key': randomUUID(), origin: env.adminOrigin },
      body: JSON.stringify({ token, name, password }),
    });
    if (!response.ok) throw new Error(`aceitar convite: HTTP ${response.status} ${await response.text()}`);
    return (await response.json()) as StaffSession;
  }

  async createCourse(title: string, description: string): Promise<CourseDetail> {
    return this.json(await this.write('post', '/courses', { title, description }), 'criar curso');
  }

  async updateCourse(courseId: string, patch: Record<string, unknown>): Promise<CourseDetail> {
    return this.json(await this.write('patch', `/courses/${courseId}`, patch), 'atualizar curso');
  }

  async createModule(courseId: string, title: string): Promise<CourseDetail> {
    return this.json(await this.write('post', `/courses/${courseId}/modules`, { title }), 'criar módulo');
  }

  async createLesson(courseId: string, moduleId: string, title: string, videoId: string): Promise<CourseDetail> {
    return this.json(
      await this.write('post', `/courses/${courseId}/modules/${moduleId}/lessons`, { title, description: `${title} (e2e)`, videoId }),
      'criar aula',
    );
  }

  async publishCourse(courseId: string, draftRevision: number) {
    return this.json(
      await this.write('post', `/courses/${courseId}/versions`, { draftRevision, versionNote: 'Seed da suíte e2e' }),
      'publicar curso',
    );
  }

  /** Envia o vídeo pelo mesmo caminho do SPA (multipart com URLs assinadas) e devolve o id do vídeo. */
  async uploadVideo(title: string, filePath: string): Promise<string> {
    const bytes = await readFile(filePath);
    const upload = await this.json<{ uploadId: string; partSize: number; partCount: number }>(
      await this.write('post', '/video-uploads', {
        title,
        fileName: 'aula-e2e.mp4',
        fileSize: bytes.length,
        contentType: 'video/mp4',
        fingerprint: `e2e-${randomUUID()}`,
      }),
      'iniciar envio de vídeo',
    );

    const partNumbers = Array.from({ length: upload.partCount }, (_, index) => index + 1);
    const { parts } = await this.json<{ parts: Array<{ partNumber: number; url: string }> }>(
      await this.write('post', `/video-uploads/${upload.uploadId}/part-urls`, { partNumbers }),
      'URLs das partes',
    );
    for (const part of parts) {
      const start = (part.partNumber - 1) * upload.partSize;
      const body = bytes.subarray(start, start + upload.partSize);
      const put = await fetch(part.url, { method: 'PUT', body });
      if (!put.ok) throw new Error(`PUT da parte ${part.partNumber}: HTTP ${put.status} ${await put.text()}`);
    }

    const video = await this.json<{ videoId: string }>(await this.write('post', `/video-uploads/${upload.uploadId}/complete`), 'concluir envio');
    return video.videoId;
  }

  async waitForVideoReady(videoId: string, timeoutMs = 300_000): Promise<void> {
    const deadline = Date.now() + timeoutMs;
    let status = 'unknown';
    while (Date.now() < deadline) {
      const video = await this.json<{ status: string; failureReason: string | null }>(await this.http.get(`/api/v1/videos/${videoId}`), 'consultar vídeo');
      status = video.status;
      if (status === 'ready') return;
      if (status === 'failed') throw new Error(`Preparação do vídeo falhou: ${video.failureReason}`);
      await sleep(2_000);
    }
    throw new Error(`Vídeo ${videoId} não ficou pronto em ${timeoutMs} ms (último estado: ${status})`);
  }

  /** O curso publicado chega ao catálogo do commerce por evento; espera a projeção antes de criar oferta. */
  async waitForCatalogCourse(courseId: string, timeoutMs = 60_000): Promise<void> {
    const deadline = Date.now() + timeoutMs;
    while (Date.now() < deadline) {
      const response = await this.http.get(`/api/v1/catalog/courses/${courseId}`);
      if (response.ok()) return;
      await sleep(1_000);
    }
    throw new Error(`Curso ${courseId} não apareceu no catálogo em ${timeoutMs} ms`);
  }

  async setTagline(courseId: string, tagline: string) {
    return this.json(await this.write('patch', `/catalog/courses/${courseId}`, { tagline }), 'chamada comercial');
  }

  async createOffer(courseId: string, name: string, priceCents: number): Promise<{ offerId: string }> {
    return this.json(
      await this.write('post', `/catalog/courses/${courseId}/offers`, { name, priceCents, accessPeriod: { type: 'months', months: 12 } }),
      'criar oferta',
    );
  }

  async publishOffer(offerId: string) {
    return this.json(await this.write('post', `/catalog/offers/${offerId}/publish`), 'publicar oferta');
  }
}
