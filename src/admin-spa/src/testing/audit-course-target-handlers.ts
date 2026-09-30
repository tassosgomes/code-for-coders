import { http, HttpResponse } from 'msw';

import { env } from '@/config/env';

export const auditCourseRecordId = '0198dfac-674a-7000-8000-000000000021';
export const auditCourseId = '0198dfac-674a-7000-8000-000000000022';
export const createAuditCourseBoundary = (withTitle = true) => {
  const searches: unknown[] = [];
  const detail = { id: auditCourseRecordId, type: 'versao-publicada', practicedAt: '2026-09-29T15:00:00Z', author: { type: 'conta-interna', id: '0198dfac-674a-7000-8000-000000000023', label: 'Professor' }, target: { type: 'curso', id: auditCourseId, ...(withTitle ? { label: 'Curso publicado' } : {}) }, compliant: true, hasComplements: false, origin: 'conteudo', receivedAt: '2026-09-29T15:00:01Z', reason: null, attributes: { versao: '1' }, nonComplianceReasons: [], complements: [] };
  return { searches, handlers: [
    http.get(`${env.API_URL}/api/v1/audit-records/:recordId`, () => HttpResponse.json(detail)),
    http.post(`${env.API_URL}/api/v1/audit-record-searches`, async ({ request }) => { searches.push(await request.json()); return HttpResponse.json({ data: [detail], pagination: { page: 1, size: 20, total: 1, totalPages: 1, snapshot: 'course-snapshot' } }); }),
  ] };
};
