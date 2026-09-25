import { http, HttpResponse } from 'msw';

import { env } from '@/config/env';

export const handlers = [
  http.get(`${env.API_URL}/v1/admin/workspace/status`, () =>
    HttpResponse.json({
      status: 'ready',
      message: 'The admin workspace service is ready.',
    }),
  ),
  http.post(`${env.API_URL}/api/v1/staff-password-resets`, () => new HttpResponse(null, { status: 204 })),
];
