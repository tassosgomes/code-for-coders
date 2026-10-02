import { http, HttpResponse } from 'msw';

import { env } from '@/config/env';

export const courtesyStudentFixture = { studentId: '0198dfac-674a-7000-8000-000000000099', name: 'Joana Ribeiro', email: 'joana@student.test', emailConfirmed: false, status: 'active' };
export const courtesyStudentLookupHandlers = [http.post(`${env.API_URL}/api/v1/student-account-lookups`, async ({ request }) => {
  const body = await request.json();
  if (typeof body !== 'object' || body === null || !('email' in body) || typeof body.email !== 'string') return HttpResponse.json({ code: 'VALIDATION_ERROR' }, { status: 400 });
  return HttpResponse.json({ ...courtesyStudentFixture, email: body.email }, { headers: { 'Cache-Control': 'no-store' } });
})];
