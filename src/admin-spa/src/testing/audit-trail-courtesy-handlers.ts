import { http, HttpResponse } from 'msw';

import { env } from '@/config/env';

export const courtesyAuditRecordId = '0198dfac-674a-7000-8000-000000000061';
export const courtesyAuditStudentId = '0198dfac-674a-7000-8000-000000000062';
export const courtesyAuditCourseId = '0198dfac-674a-7000-8000-000000000063';
export const courtesyAuditEmail = 'joana@example.test';

type CourtesyAuditBoundaryOptions = {
  period?: string;
  resolved?: boolean;
  status?: number;
};

export const createCourtesyAuditBoundary = ({ period = '6m', resolved = true, status = 200 }: CourtesyAuditBoundaryOptions = {}) => {
  const searches: unknown[] = [];
  const summary = {
    id: courtesyAuditRecordId, type: 'cortesia-concedida', practicedAt: '2026-10-01T12:20:00Z',
    author: { type: 'conta-interna', id: '0198dfac-674a-7000-8000-000000000064', label: 'Marina Costa' },
    target: { type: 'conta-aluno', id: courtesyAuditStudentId, label: resolved ? 'Joana Ribeiro' : null },
    compliant: true, hasComplements: false,
  };
  const detail = {
    ...summary, origin: 'matricula', receivedAt: '2026-10-01T12:20:03Z',
    reason: 'Bolsa integral do parceiro municipal — turma 2027/1.',
    attributes: {
      curso: courtesyAuditCourseId, concessao: '0198dfac-674a-7000-8000-000000000065', vigencia: period,
      ...(resolved ? { cursoTitulo: 'Testes na prática' } : {}),
    },
    nonComplianceReasons: [], complements: [],
  };
  const denied = () => HttpResponse.json({ code: 'PERMISSION_DENIED' }, { status });
  return { searches, handlers: [
    http.get(`${env.API_URL}/api/v1/audit-records/:recordId`, () => status === 200 ? HttpResponse.json(detail) : denied()),
    http.post(`${env.API_URL}/api/v1/audit-record-searches`, async ({ request }) => {
      searches.push(await request.json());
      return status === 200
        ? HttpResponse.json({ data: [summary], pagination: { page: 1, size: 20, total: 1, totalPages: 1, snapshot: 'courtesy-snapshot' } })
        : denied();
    }),
  ] };
};
