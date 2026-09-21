import { http, HttpResponse } from 'msw';

import { env } from '@/config/env';

export const handlers = [
  http.get(`${env.API_URL}/v1/student/workspace/status`, () =>
    HttpResponse.json({
      status: 'ready',
      message: 'The student workspace service is ready.',
    }),
  ),
];
