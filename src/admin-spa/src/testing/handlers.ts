import { http, HttpResponse } from 'msw';

import { env } from '@/config/env';

export const handlers = [
  http.post(`${env.API_URL}/api/v1/staff-sessions`, () =>
    HttpResponse.json({
      accountId: '3e4f5a6b-7c8d-4e9f-8a0b-1c2d3e4f5a6b',
      name: 'Marina Alves',
      roles: ['administrador'],
      permissions: ['acesso.gerir'],
      csrfToken: 'staff-session-csrf',
    }),
  ),
  http.get(`${env.API_URL}/api/v1/staff-sessions/current`, () =>
    HttpResponse.json({
      accountId: '3e4f5a6b-7c8d-4e9f-8a0b-1c2d3e4f5a6b',
      name: 'Marina Alves',
      roles: ['administrador'],
      permissions: ['acesso.gerir'],
      csrfToken: 'staff-session-csrf',
    }),
  ),
  http.delete(`${env.API_URL}/api/v1/staff-sessions/current`, () => new HttpResponse(null, { status: 204 })),
  http.get(`${env.API_URL}/v1/admin/workspace/status`, () =>
    HttpResponse.json({
      status: 'ready',
      message: 'The admin workspace service is ready.',
    }),
  ),
  http.post(`${env.API_URL}/api/v1/staff-password-resets`, () => new HttpResponse(null, { status: 204 })),
  http.get(`${env.API_URL}/api/v1/staff-invitations`, () => HttpResponse.json({
    data: [],
    pagination: { page: 1, size: 100, total: 0, totalPages: 0 },
  })),
  http.post(`${env.API_URL}/api/v1/staff-invitations`, async ({ request }) => {
    const body = await request.json() as { email: string; role: string };
    return HttpResponse.json({
      invitationId: '5f6e7d8c-9b0a-4c1d-8e2f-3a4b5c6d7e8f',
      email: body.email,
      offeredRole: body.role,
      invitedAt: '2026-10-02T14:05:11Z',
      expiresAt: '2026-10-09T14:05:11Z',
      supersededInvitationId: null,
    }, { status: 201 });
  }),
];
