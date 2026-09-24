import { http, HttpResponse } from 'msw';

import { env } from '@/config/env';

export const handlers = [
  http.get(`${env.API_URL}/api/v1/student-sessions/current`, () =>
    HttpResponse.json({
      accountId: '00000000-0000-4000-8000-000000000001',
      name: 'Ana Souza',
      csrfToken: 'student-session-csrf',
    }),
  ),
  http.post(`${env.API_URL}/api/v1/student-sessions`, () =>
    HttpResponse.json({
      accountId: '00000000-0000-4000-8000-000000000001',
      name: 'Ana Souza',
      csrfToken: 'student-session-csrf',
    }),
  ),
  http.delete(`${env.API_URL}/api/v1/student-sessions/current`, () =>
    new HttpResponse(null, { status: 204 }),
  ),
  http.get(`${env.API_URL}/v1/student/workspace/status`, () =>
    HttpResponse.json({
      status: 'ready',
      message: 'The student workspace service is ready.',
    }),
  ),
];
