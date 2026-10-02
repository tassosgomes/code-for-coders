import { http, HttpResponse } from 'msw';

import { env } from '@/config/env';

export const courtesyGrantFixture = { grantId: '0198dfac-674a-7000-8000-000000000099', studentId: '0198dfac-674a-7000-8000-000000000001', courseId: '0198dfac-674a-7000-8000-000000000088', courseTitle: 'Fundamentos de C#', origin: 'courtesy', status: 'active', accessPeriod: { type: 'months', months: 6 }, grantedAt: '2026-10-02T12:00:00Z', endsOn: '2027-04-02', expiresAt: '2027-04-03T03:00:00Z', reason: 'Bolsa de mentoria' };
export const courtesyGrantHandlers = [
  http.get(`${env.API_URL}/api/v1/courtesy-term-preview`, ({ request }) => HttpResponse.json({ months: Number(new URL(request.url).searchParams.get('months')), computedAt: '2026-10-02T12:00:00Z', endsOn: '2027-04-02', expiresAt: '2027-04-03T03:00:00Z' })),
  http.post(`${env.API_URL}/api/v1/courtesy-grants`, async ({ request }) => {
    const body = await request.json();
    return HttpResponse.json({ ...courtesyGrantFixture, ...(typeof body === 'object' && body !== null ? body : {}) }, { status: 201 });
  }),
];
