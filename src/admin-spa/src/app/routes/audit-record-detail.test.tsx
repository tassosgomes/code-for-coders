import { http, HttpResponse } from 'msw';
import { RouterProvider, createMemoryRouter } from 'react-router';
import { cleanup, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it } from 'vitest';

import { AdminLayoutRoute } from '@/app/routes/admin-layout-route';
import { AuditRecordDetailRoute } from '@/app/routes/audit-record-detail-route';
import { AuditTrailRoute } from '@/app/routes/audit-trail-route';
import { loadStaffSession } from '@/app/routes/staff-session-loader';
import type { AuditRecordDetail } from '@/features/audit-trail/api/get-audit-record';
import { AuditTrailScreen } from '@/features/audit-trail/components/audit-trail-screen';
import { env } from '@/config/env';
import { server } from '@/testing/server';
import { renderWithProviders } from '@/testing/test-utils';

const recordId = '5137eb89-3e71-4462-9c52-3994f7be0f9a';
const authorId = '337fcd34-6bf6-4fe5-a1e6-608fe9426be7';
const targetId = '550e8400-e29b-41d4-a716-446655440000';
const adminSession = {
  accountId: '3e4f5a6b-7c8d-4e9f-8a0b-1c2d3e4f5a6b',
  name: 'Marina Alves',
  roles: ['administrador'],
  permissions: ['acesso.gerir'],
  csrfToken: 'staff-session-csrf',
};

const recordDetail: AuditRecordDetail = {
  id: recordId,
  type: 'papel-concedido',
  practicedAt: '2026-09-27T10:00:00Z',
  author: { type: 'conta-interna', id: authorId, label: 'Marina Costa' },
  target: { type: 'conta-interna', id: targetId, label: 'Rafael Silva' },
  compliant: true,
  hasComplements: false,
  origin: 'identidade',
  receivedAt: '2026-09-27T10:00:04Z',
  reason: 'Assumiu a turma de .NET após a saída do professor anterior.',
  attributes: { papel: 'professor' },
  nonComplianceReasons: [],
  complements: [],
};

const listPage = {
  data: [{
    id: recordId,
    type: 'papel-concedido',
    practicedAt: '2026-09-27T10:00:00Z',
    author: { type: 'conta-interna', id: authorId, label: 'Marina Costa' },
    target: { type: 'conta-interna', id: targetId, label: 'Rafael Silva' },
    compliant: true,
    hasComplements: false,
  }],
  pagination: { page: 1, size: 20, total: 1, totalPages: 1, snapshot: 'snap_7mQ2kV4b123456789012345678901234567890123' },
};

const detailPath = `${env.API_URL}/api/v1/audit-records/:recordId`;
const searchPath = `${env.API_URL}/api/v1/audit-record-searches`;

type RouterEntry = string | { pathname: string; state?: unknown };

const renderAuditRouter = (initialEntries: RouterEntry[], basename?: string, roles = adminSession.roles) => {
  server.use(http.get(`${env.API_URL}/api/v1/staff-sessions/current`, () => HttpResponse.json({
    ...adminSession,
    roles,
  })));
  const router = createMemoryRouter([{
    path: '/',
    loader: loadStaffSession,
    element: <AdminLayoutRoute serviceName="admin-spa" title="Admin Workspace" />,
    children: [{
      path: 'auditoria',
      element: <AuditTrailRoute />,
      children: [
        { index: true, element: <AuditTrailScreen /> },
        { path: ':recordId', element: <AuditRecordDetailRoute /> },
      ],
    }],
  }, {
    path: 'entrar',
    element: <p role="heading">B1 · Entrar</p>,
  }], { initialEntries, basename });
  renderWithProviders(<RouterProvider router={router} />);
  return router;
};

const respondWithDetail = (detail: AuditRecordDetail = recordDetail) => {
  server.use(http.get(detailPath, () => HttpResponse.json(detail)));
};

describe('AuditRecordDetail', () => {
  afterEach(cleanup);

  it('opens a detail from the row and shows the received evidence', async () => {
    const user = userEvent.setup();
    respondWithDetail();
    server.use(http.post(searchPath, () => HttpResponse.json(listPage)));
    renderAuditRouter(['/auditoria']);

    await user.click(await screen.findByRole('link', { name: 'Abrir registro papel-concedido' }));

    expect(await screen.findByRole('heading', { name: 'Papel concedido' })).toBeInTheDocument();
    expect(screen.getByText('Recebido da origem')).toBeInTheDocument();
    expect(screen.getByText('Identidade e Acesso')).toBeInTheDocument();
    expect(screen.getByText('Assumiu a turma de .NET após a saída do professor anterior.')).toBeInTheDocument();
    expect(screen.getByText('Professor', { selector: '.role-badge' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: /Complementos/ })).not.toBeInTheDocument();
  });

  it('opens a record when the list row receives Enter', async () => {
    const user = userEvent.setup();
    respondWithDetail();
    server.use(http.post(searchPath, () => HttpResponse.json(listPage)));
    renderAuditRouter(['/auditoria']);

    await user.keyboard('{Tab}');
    const rowLink = await screen.findByRole('link', { name: 'Abrir registro papel-concedido' });
    rowLink.focus();
    await user.keyboard('{Enter}');

    expect(await screen.findByRole('heading', { name: 'Papel concedido' })).toBeInTheDocument();
  });

  it('loads a direct detail URL below the admin base path', async () => {
    respondWithDetail();
    renderAuditRouter([`/admin/auditoria/${recordId}`], '/admin');

    expect(await screen.findByRole('heading', { name: 'Papel concedido' })).toBeInTheDocument();
  });

  it('shows missing values and non-compliance reasons without inventing evidence', async () => {
    respondWithDetail({
      ...recordDetail,
      type: 'acesso-suspenso',
      practicedAt: null,
      author: null,
      target: null,
      compliant: false,
      origin: 'origem-desconhecida',
      reason: null,
      attributes: {},
      nonComplianceReasons: ['tipo-desconhecido', 'autor-ausente', 'motivo-ausente'],
    });
    renderAuditRouter([`/auditoria/${recordId}`]);

    expect(await screen.findByRole('heading', { name: 'acesso-suspenso' })).toBeInTheDocument();
    expect(screen.getByText('Tipo de ato desconhecido')).toBeInTheDocument();
    expect(screen.getByText('Autor não informado pela origem')).toBeInTheDocument();
    expect(screen.getByText('Motivo obrigatório não informado')).toBeInTheDocument();
    expect(screen.getByText('origem-desconhecida', { selector: 'code' })).toBeInTheDocument();
    expect(screen.getAllByText('— ausente')).toHaveLength(4);
    expect(screen.queryByText('Assumiu a turma de .NET após a saída do professor anterior.')).not.toBeInTheDocument();
  });

  it('explains that a reason does not apply to an accepted invitation', async () => {
    respondWithDetail({
      ...recordDetail,
      type: 'convite-interno-aceito',
      reason: null,
    });
    renderAuditRouter([`/auditoria/${recordId}`]);

    expect(await screen.findByText('Não se aplica a este tipo')).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('shows the same not-found state for an unavailable record', async () => {
    server.use(http.get(detailPath, () => HttpResponse.json({ code: 'AUDIT_RECORD_NOT_FOUND' }, { status: 404 })));
    renderAuditRouter([`/auditoria/${recordId}`]);

    expect(await screen.findByRole('heading', { name: 'Registro não encontrado' })).toBeInTheDocument();
    expect(screen.getByText('Ele pode não existir ou não estar disponível para você.')).toBeInTheDocument();
  });

  it('shows the forbidden state for a session without the administrator role', async () => {
    server.use(http.get(detailPath, () => HttpResponse.json({ code: 'PERMISSION_DENIED' }, { status: 403 })));
    renderAuditRouter([`/auditoria/${recordId}`]);

    expect(await screen.findByRole('heading', { name: 'Esta área não é do seu papel' })).toBeInTheDocument();
  });

  it('filters by the selected person in navigation state and keeps the UUID out of the URL', async () => {
    const user = userEvent.setup();
    respondWithDetail();
    const requests: Record<string, unknown>[] = [];
    server.use(http.post(searchPath, async ({ request }) => {
      requests.push(await request.json() as Record<string, unknown>);
      return HttpResponse.json(listPage);
    }));
    const router = renderAuditRouter([`/auditoria/${recordId}`]);

    await user.click(await screen.findByRole('button', { name: 'Ver atos desta pessoa (autor)' }));

    expect(await screen.findByText('Autor: Marina Costa')).toBeInTheDocument();
    await waitFor(() => expect(requests.at(-1)).toMatchObject({ authorId }));
    expect(router.state.location.pathname).toBe('/auditoria');
    expect(router.state.location.pathname).not.toContain(authorId);
  });

  it('restarts a reloaded person filter and tells the administrator', async () => {
    const requests: Record<string, unknown>[] = [];
    server.use(http.post(searchPath, async ({ request }) => {
      requests.push(await request.json() as Record<string, unknown>);
      return HttpResponse.json(listPage);
    }));
    renderAuditRouter([{
      pathname: '/auditoria',
      state: {
        personFilter: { kind: 'author', id: authorId, label: 'Marina Costa' },
        returnToAuditDetail: recordId,
      },
    }]);

    expect(await screen.findByRole('status')).toHaveTextContent('A busca foi reiniciada sem o filtro de pessoa.');
    expect(screen.queryByText('Autor: Marina Costa')).not.toBeInTheDocument();
    await waitFor(() => expect(requests).toHaveLength(1));
    expect(requests[0]).not.toHaveProperty('authorId');
    expect(screen.getByRole('link', { name: 'Voltar ao registro' })).toHaveAttribute('href', `/auditoria/${recordId}`);
  });

  it('sends a 401 detail response back to the sign-in route', async () => {
    server.use(http.get(detailPath, () => HttpResponse.json({ code: 'SESSION_REQUIRED' }, { status: 401 })));
    const router = renderAuditRouter([`/auditoria/${recordId}`]);

    await waitFor(() => expect(router.state.location.pathname).toBe('/entrar'));
  });
});
