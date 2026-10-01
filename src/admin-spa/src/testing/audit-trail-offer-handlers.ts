import { http, HttpResponse } from 'msw';

import { env } from '@/config/env';

export const offerAuditRecordId = '0198dfac-674a-7000-8000-000000000051';
export const auditOfferId = '0198dfac-674a-7000-8000-000000000052';
export const auditOfferCourseId = '0198dfac-674a-7000-8000-000000000053';

export const createOfferAuditBoundary = (type = 'oferta-alterada', withLabel = true, attributes?: Record<string, string>) => {
  const searches: unknown[] = [];
  const detail = {
    id: offerAuditRecordId, type, practicedAt: '2026-10-01T15:00:00Z',
    author: { type: 'conta-interna', id: '0198dfac-674a-7000-8000-000000000054', label: 'Administrador' },
    target: { type: 'oferta', id: auditOfferId, ...(withLabel ? { label: 'Curso de C# — Acesso por 12 meses' } : {}) },
    compliant: true, hasComplements: false, origin: 'catalogo', receivedAt: '2026-10-01T15:00:01Z',
    reason: null, attributes: attributes ?? {
      curso: auditOfferCourseId, precoNovo: '39700', vigenciaNova: 'vitalicia', precoAnterior: '49700', vigenciaAnterior: '12m',
    }, nonComplianceReasons: [], complements: [],
  };
  return { searches, handlers: [
    http.get(`${env.API_URL}/api/v1/audit-records/:recordId`, () => HttpResponse.json(detail)),
    http.post(`${env.API_URL}/api/v1/audit-record-searches`, async ({ request }) => {
      searches.push(await request.json());
      return HttpResponse.json({ data: [detail], pagination: { page: 1, size: 20, total: 1, totalPages: 1, snapshot: 'offer-snapshot' } });
    }),
  ] };
};
