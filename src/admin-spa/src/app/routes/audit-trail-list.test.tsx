import { http, HttpResponse } from 'msw';
import { RouterProvider, createMemoryRouter } from 'react-router';
import { cleanup, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it } from 'vitest';

import { AdminLayoutRoute } from '@/app/routes/admin-layout-route';
import { AuditTrailRoute } from '@/app/routes/audit-trail-route';
import { AuditTrailScreen } from '@/features/audit-trail/components/audit-trail-screen';
import { DashboardRoute } from '@/app/routes/dashboard-route';
import { loadStaffSession } from '@/app/routes/staff-session-loader';
import { env } from '@/config/env';
import { server } from '@/testing/server';
import { renderWithProviders } from '@/testing/test-utils';

const adminSession = {
  accountId: '3e4f5a6b-7c8d-4e9f-8a0b-1c2d3e4f5a6b',
  name: 'Marina Alves',
  roles: ['administrador'],
  permissions: ['acesso.gerir'],
  csrfToken: 'staff-session-csrf',
};

const rowFixtures = [
  {
    id: '5137eb89-3e71-4462-9c52-3994f7be0f9a',
    type: 'papel-concedido',
    role: 'professor',
    practicedAt: '2026-09-27T10:00:00Z',
    author: { type: 'conta-interna', id: '337fcd34-6bf6-4fe5-a1e6-608fe9426be7', label: 'Marina Costa' },
    target: { type: 'conta-interna', id: '550e8400-e29b-41d4-a716-446655440000', label: 'Rafael Silva' },
    compliant: true,
    hasComplements: false,
  },
  {
    id: '6137eb89-3e71-4462-9c52-3994f7be0f9a',
    type: 'convite-interno-emitido',
    practicedAt: null,
    author: null,
    target: { type: 'convite-interno', id: '650e8400-e29b-41d4-a716-446655440000' },
    compliant: false,
    hasComplements: true,
  },
];

const pageFixture = (data = rowFixtures, page = 1, totalPages = 1) => ({
  data,
  pagination: { page, size: 20, total: data.length, totalPages, snapshot: 'snap_7mQ2kV4b123456789012345678901234567890123' },
});

const renderAuditRoute = (roles = ['administrador']) => {
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
      children: [{ index: true, element: <AuditTrailScreen /> }],
    }],
  }], { initialEntries: ['/auditoria'] });
  renderWithProviders(<RouterProvider router={router} />);
};

const searchPath = `${env.API_URL}/api/v1/audit-record-searches`;

describe('AuditTrailList', () => {
  afterEach(cleanup);

  it('shows authorized administrators the list with labels and compliance state', async () => {
    server.use(http.post(searchPath, () => HttpResponse.json(pageFixture())));
    renderAuditRoute();

    expect(await screen.findByRole('heading', { name: 'Trilha de atos administrativos' })).toBeInTheDocument();
    const table = await screen.findByRole('table', { name: 'Registros da trilha de auditoria' });
    expect(within(table).getByText('Marina Costa')).toBeInTheDocument();
    expect(within(table).getByText('Rafael Silva')).toBeInTheDocument();
    expect(within(table).getByText('Conforme')).toBeInTheDocument();
    expect(within(table).getByText('Não conforme')).toBeInTheDocument();
    expect(within(table).getByText('Complementado')).toBeInTheDocument();
    expect(screen.getAllByText('Professor', { selector: '.role-badge' })).toHaveLength(2);
    expect(screen.getByRole('button', { name: 'Abrir registro Papel concedido' })).toBeInTheDocument();
    expect(within(table).getAllByText('— ausente')).toHaveLength(2);
    expect(screen.getByRole('navigation', { name: 'admin-spa navigation' })).toHaveTextContent('Auditoria');
  });

  it('opens the mobile filter sheet and restores focus when it closes', async () => {
    const user = userEvent.setup();
    server.use(http.post(searchPath, () => HttpResponse.json(pageFixture())));
    renderAuditRoute();

    const trigger = await screen.findByRole('button', { name: 'Filtros' });
    await user.click(trigger);
    const dialog = screen.getByRole('dialog', { name: 'Filtros' });
    expect(within(dialog).getByLabelText('De')).toBeInTheDocument();
    expect(within(dialog).getByRole('button', { name: 'Aplicar' })).toBeInTheDocument();

    await user.keyboard('{Escape}');
    await waitFor(() => expect(screen.queryByRole('dialog', { name: 'Filtros' })).not.toBeInTheDocument());
    expect(trigger).toHaveFocus();
  });

  it('posts filters in the body and never puts them in the URL', async () => {
    const user = userEvent.setup();
    const receivedRequests: { url: string; method: string; body: Record<string, unknown> }[] = [];
    server.use(http.post(searchPath, async ({ request }) => {
      receivedRequests.push({
        url: request.url,
        method: request.method,
        body: await request.json() as Record<string, unknown>,
      });
      return HttpResponse.json(pageFixture());
    }));
    renderAuditRoute();

    await screen.findByRole('table', { name: 'Registros da trilha de auditoria' });
    await user.type(screen.getByLabelText('De'), '2026-09-01T08:00');
    await user.type(screen.getByLabelText('Até'), '2026-09-27T17:30');
    await user.selectOptions(screen.getByLabelText('Tipo'), 'papel-concedido');
    await user.click(screen.getByRole('button', { name: 'Buscar' }));

    await waitFor(() => expect(receivedRequests.at(-1)?.body).toMatchObject({
      _page: 1,
      _size: 20,
      type: 'papel-concedido',
    }));
    const receivedRequest = receivedRequests.at(-1)!;
    expect(receivedRequest.method).toBe('POST');
    expect(new URL(receivedRequest.url).search).toBe('');
    expect(receivedRequest.body.from).toBe(new Date('2026-09-01T08:00').toISOString());
    expect(receivedRequest.body.to).toBe(new Date('2026-09-27T17:30').toISOString());
  });

  it('blocks an inverted date range before sending another request', async () => {
    const user = userEvent.setup();
    let requests = 0;
    server.use(http.post(searchPath, () => {
      requests += 1;
      return HttpResponse.json(pageFixture());
    }));
    renderAuditRoute();

    await screen.findByRole('table', { name: 'Registros da trilha de auditoria' });
    const initialRequestCount = requests;
    await user.type(screen.getByLabelText('De'), '2026-09-27T17:30');
    await user.type(screen.getByLabelText('Até'), '2026-09-01T08:00');
    await user.click(screen.getByRole('button', { name: 'Buscar' }));

    expect(screen.getByRole('alert')).toHaveTextContent('A data final vem antes da inicial.');
    expect(requests).toBe(initialRequestCount);
  });

  it('keeps filters visible when no records match and lets the user clear them', async () => {
    const user = userEvent.setup();
    server.use(http.post(searchPath, () => HttpResponse.json(pageFixture([], 1, 0))));
    renderAuditRoute();

    await screen.findByText('Nenhum ato registrado ainda');
    await user.selectOptions(screen.getByLabelText('Tipo'), 'papel-concedido');
    await user.click(screen.getByRole('button', { name: 'Buscar' }));
    expect(await screen.findByText('Nenhum registro com esses filtros.')).toBeInTheDocument();
    expect(screen.getByLabelText('Tipo')).toHaveValue('papel-concedido');
    const emptyState = screen.getByRole('heading', { name: 'Nenhum registro com esses filtros.' }).closest('section');
    await user.click(within(emptyState!).getByRole('button', { name: 'Limpar filtros' }));
    expect(screen.getByLabelText('Tipo')).toHaveValue('');
  });

  it('restarts at page one with a new snapshot when the previous snapshot expires', async () => {
    const user = userEvent.setup();
    const requests: Record<string, unknown>[] = [];
    server.use(http.post(searchPath, async ({ request }) => {
      const body = await request.json() as Record<string, unknown>;
      requests.push(body);
      if (body._page === 2) {
        return HttpResponse.json({ code: 'AUDIT_FILTER_INVALID' }, { status: 422 });
      }
      return HttpResponse.json(pageFixture(rowFixtures, 1, 2));
    }));
    renderAuditRoute();

    await screen.findByRole('table', { name: 'Registros da trilha de auditoria' });
    await user.click(screen.getByRole('button', { name: 'Próxima página' }));
    expect(await screen.findByRole('status')).toHaveTextContent('A busca expirou e foi refeita. Você voltou à primeira página.');
    await waitFor(() => expect(requests.filter((request) => request._page === 1).length).toBeGreaterThanOrEqual(2));
    expect(requests[1]).toMatchObject({ _page: 2, snapshot: expect.any(String) });
    expect(requests.at(-1)).toMatchObject({ _page: 1 });
    expect(requests.at(-1)).not.toHaveProperty('snapshot');
  });

  it('refreshes page one with a new snapshot when the user asks to update', async () => {
    const user = userEvent.setup();
    const requests: Record<string, unknown>[] = [];
    server.use(http.post(searchPath, async ({ request }) => {
      const body = await request.json() as Record<string, unknown>;
      requests.push(body);
      const response = pageFixture(rowFixtures, 1, 2);
      return HttpResponse.json({
        ...response,
        pagination: { ...response.pagination, snapshot: `snap_${String(requests.length).padStart(43, '0')}` },
      });
    }));
    renderAuditRoute();

    await screen.findByRole('table', { name: 'Registros da trilha de auditoria' });
    expect(requests).toHaveLength(1);
    await user.click(screen.getByRole('button', { name: 'Atualizar resultados' }));

    await waitFor(() => expect(requests).toHaveLength(2));
    expect(requests[1]).toMatchObject({ _page: 1, _size: 20 });
    expect(requests[1]).not.toHaveProperty('snapshot');
    await waitFor(() => expect(screen.getByRole('button', { name: 'Próxima página' })).toBeEnabled());
    await user.click(screen.getByRole('button', { name: 'Próxima página' }));
    await waitFor(() => expect(requests).toHaveLength(3));
    expect(requests[2]).toMatchObject({ _page: 2, snapshot: `snap_${'2'.padStart(43, '0')}` });
  });

  it('refuses direct access for a non-administrator without querying the trail', async () => {
    let requests = 0;
    server.use(http.post(searchPath, () => {
      requests += 1;
      return HttpResponse.json(pageFixture());
    }));
    renderAuditRoute(['professor']);

    expect(await screen.findByRole('heading', { name: 'Esta área não é do seu papel' })).toBeInTheDocument();
    expect(screen.getByText('$ GET /admin/auditoria', { exact: false })).toBeInTheDocument();
    expect(screen.getByText('403 Forbidden')).toBeInTheDocument();
    expect(screen.getByText('Se você precisa dela, peça a um administrador.')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Voltar para o início' })).toHaveAttribute('href', '/');
    expect(requests).toBe(0);
    const navigation = within(screen.getByRole('navigation', { name: 'admin-spa navigation' }));
    expect(navigation.queryByRole('link', { name: 'Auditoria' })).not.toBeInTheDocument();
  });

  it('shows the audit card on the administrator dashboard', async () => {
    server.use(http.get(`${env.API_URL}/api/v1/staff-sessions/current`, () => HttpResponse.json(adminSession)));
    const router = createMemoryRouter([{
      path: '/',
      loader: loadStaffSession,
      element: <AdminLayoutRoute serviceName="admin-spa" title="Admin Workspace" />,
      children: [{ index: true, element: <DashboardRoute /> }],
    }], { initialEntries: ['/'] });
    renderWithProviders(<RouterProvider router={router} />);

    expect(await screen.findByRole('link', { name: 'Abrir Auditoria' })).toHaveAttribute('href', '/auditoria');
  });
});
