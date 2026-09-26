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
  http.get(`${env.API_URL}/api/v1/videos`, () => HttpResponse.json({
    data: [],
    pagination: { page: 1, size: 10, total: 0, totalPages: 0 },
  })),
  http.post(`${env.API_URL}/api/v1/video-uploads`, () => HttpResponse.json({
    uploadId: 'e2ef6f47-cb6b-4a08-b126-c3b21e9475d2',
    title: 'Aula de exemplo',
    fileName: 'aula.mp4',
    fileSize: 4,
    partSize: 67108864,
    partCount: 1,
    receivedParts: [],
    expiresAt: '2026-09-27T14:05:11Z',
  }, { status: 201 })),
  http.get(`${env.API_URL}/api/v1/video-uploads`, () => HttpResponse.json({
    data: [],
    pagination: { page: 1, size: 50, total: 0, totalPages: 0 },
  })),
  http.get(`${env.API_URL}/api/v1/video-uploads/:uploadId`, () => HttpResponse.json({
    uploadId: 'e2ef6f47-cb6b-4a08-b126-c3b21e9475d2',
    title: 'Aula de exemplo',
    fileName: 'aula.mp4',
    fileSize: 4,
    partSize: 67108864,
    partCount: 1,
    receivedParts: [],
    expiresAt: '2026-09-27T14:05:11Z',
  })),
  http.post(`${env.API_URL}/api/v1/video-uploads/:uploadId/part-urls`, async ({ request }) => {
    const body = await request.json() as { partNumbers: number[] };
    return HttpResponse.json({
      parts: body.partNumbers.map((partNumber) => ({
        partNumber,
        url: `http://localhost:9000/part-${partNumber}?X-Amz-Signature=test`,
        expiresAt: '2026-09-27T14:05:11Z',
      })),
      uploadExpiresAt: '2026-09-28T14:05:11Z',
    });
  }),
  http.put('http://localhost:9000/:part', () => new HttpResponse(null, { status: 200 })),
  http.post(`${env.API_URL}/api/v1/video-uploads/:uploadId/complete`, () => HttpResponse.json({
    videoId: 'c2733b6f-51ee-4c10-8f9c-255904b08a93',
    title: 'Aula de exemplo',
    status: 'received',
    uploadedBy: {
      accountId: '3e4f5a6b-7c8d-4e9f-8a0b-1c2d3e4f5a6b',
      name: 'Marina Alves',
    },
    uploadedAt: '2026-09-26T14:05:11Z',
    durationSeconds: null,
    failureReason: null,
  }, { status: 201 })),
  http.delete(`${env.API_URL}/api/v1/staff-sessions/current`, () => new HttpResponse(null, { status: 204 })),
  http.get(`${env.API_URL}/v1/admin/workspace/status`, () =>
    HttpResponse.json({
      status: 'ready',
      message: 'The admin workspace service is ready.',
    }),
  ),
  http.post(`${env.API_URL}/api/v1/staff-password-resets`, () => new HttpResponse(null, { status: 204 })),
  http.post(`${env.API_URL}/api/v1/staff-password-reset-requests`, () => new HttpResponse(null, { status: 202 })),
  http.post(`${env.API_URL}/api/v1/staff-invitation-lookups`, () => HttpResponse.json({
    offeredRole: 'professor',
    expiresAt: '2026-10-09T14:05:11Z',
  })),
  http.post(`${env.API_URL}/api/v1/staff-invitation-acceptances`, () => HttpResponse.json({
    accountId: '7a8b9c0d-1e2f-4a3b-9c4d-5e6f7a8b9c0d',
    name: 'Marina Alves',
    roles: ['professor'],
    permissions: ['autoria.ler'],
    csrfToken: 'accepted-staff-session-csrf',
  })),
  http.get(`${env.API_URL}/api/v1/staff-invitations`, () => HttpResponse.json({
    data: [],
    pagination: { page: 1, size: 100, total: 0, totalPages: 0 },
  })),
  http.get(`${env.API_URL}/api/v1/staff-members`, () => HttpResponse.json({
    data: [],
    pagination: { page: 1, size: 100, total: 0, totalPages: 0 },
  })),
  http.post(`${env.API_URL}/api/v1/staff-members/:accountId/role-changes`, async ({ params, request }) => {
    const body = await request.json() as { fromRole: string; toRole: string };
    return HttpResponse.json({
      member: {
        accountId: params.accountId,
        name: 'Marina Alves',
        email: 'marina@example.com',
        roles: [body.toRole],
        isSelf: false,
      },
      changed: true,
      sessionsEnded: true,
    });
  }),
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
