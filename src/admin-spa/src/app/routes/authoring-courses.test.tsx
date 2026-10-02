import { cleanup, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { afterEach, describe, expect, it } from 'vitest';

import { AdminLayoutRoute } from '@/app/routes/admin-layout-route';
import { AuthoringCourseRoute } from '@/app/routes/authoring-course-route';
import { AuthoringCoursesRoute } from '@/app/routes/authoring-courses-route';
import { DashboardRoute } from '@/app/routes/dashboard-route';
import { loadStaffSession } from '@/app/routes/staff-session-loader';
import { env } from '@/config/env';
import { authoringCourseFixture, authoringCourseHandlers, authoringCourseSummaryFixture } from '@/testing/authoring-course-handlers';
import { server } from '@/testing/server';
import { renderWithProviders } from '@/testing/test-utils';

const renderCourses = (permissions = ['autoria.ler', 'autoria.editar'], path = '/autoria') => {
  server.use(...authoringCourseHandlers, http.get(`${env.API_URL}/api/v1/staff-sessions/current`, () => HttpResponse.json({
    accountId: '0198dfac-674a-7000-8000-000000000002', name: 'Professor B', roles: ['professor'], permissions, csrfToken: 'course-csrf',
  })));
  const router = createMemoryRouter([{
    path: '/', loader: loadStaffSession, element: <AdminLayoutRoute title="Autoria" serviceName="admin-spa" />,
    children: [{ index: true, element: <DashboardRoute /> }, { path: 'autoria', element: <AuthoringCoursesRoute /> }, { path: 'autoria/:courseId', element: <AuthoringCourseRoute /> }],
  }], { initialEntries: [path] });
  renderWithProviders(<RouterProvider router={router} />);
  return router;
};

describe('authoring courses', () => {
  afterEach(cleanup);

  it('lists the school course and links to its editor', async () => {
    renderCourses();
    expect(await screen.findByText('.NET do zero à API')).toBeInTheDocument();
    expect(screen.getByText(/Professor B ·/)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Abrir .NET do zero à API' })).toHaveAttribute('href', `/autoria/${authoringCourseFixture.courseId}`);
    expect(screen.getByText('Conteúdo')).toBeInTheDocument();
  });

  it('validates a blank title then creates with CSRF and opens the editor', async () => {
    const user = userEvent.setup();
    const router = renderCourses();
    let key: string | null = null; let csrf: string | null = null;
    server.use(http.post(`${env.API_URL}/api/v1/courses`, async ({ request }) => {
      key = request.headers.get('Idempotency-Key'); csrf = request.headers.get('X-CSRF-Token');
      const input: unknown = await request.json();
      expect(input).toEqual({ title: 'Novo curso de APIs', description: 'Aprenda APIs' });
      return HttpResponse.json({ ...authoringCourseFixture, title: 'Novo curso de APIs', description: 'Aprenda APIs' }, { status: 201 });
    }));
    await user.click(await screen.findByRole('button', { name: '+ Novo curso' }));
    const dialog = screen.getByRole('dialog', { name: 'Novo curso' });
    await user.click(within(dialog).getByRole('button', { name: 'Criar curso' }));
    expect(await screen.findByText('Informe o título do curso.')).toBeInTheDocument();
    expect(key).toBeNull();
    await user.type(screen.getByLabelText('Título *'), 'Novo curso de APIs');
    await user.type(screen.getByLabelText('Descrição pedagógica (opcional)'), 'Aprenda APIs');
    await user.click(within(dialog).getByRole('button', { name: 'Criar curso' }));
    expect(await screen.findByRole('heading', { name: '.NET do zero à API' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe(`/autoria/${authoringCourseFixture.courseId}`);
    expect(key).toBeTruthy(); expect(csrf).toBe('course-csrf');
  });

  it('keeps the form and the same intention when retrying a failed creation', async () => {
    const user = userEvent.setup();
    renderCourses();
    const keys: (string | null)[] = [];
    server.use(http.post(`${env.API_URL}/api/v1/courses`, ({ request }) => {
      keys.push(request.headers.get('Idempotency-Key'));
      return HttpResponse.json({ code: 'LEARNING_UNAVAILABLE' }, { status: 502 });
    }));
    await user.click(await screen.findByRole('button', { name: '+ Novo curso' }));
    await user.type(screen.getByLabelText('Título *'), 'Dados preservados');
    await user.click(screen.getByRole('button', { name: 'Criar curso' }));
    expect(await screen.findByText(/Seus dados foram mantidos/)).toBeInTheDocument();
    expect(screen.getByLabelText('Título *')).toHaveValue('Dados preservados');
    await user.click(screen.getByRole('button', { name: 'Criar curso' }));
    await screen.findByRole('button', { name: 'Criar curso' });
    expect(keys).toHaveLength(2); expect(keys[0]).toBe(keys[1]);
  });

  it('reader sees list and refreshed editor without writing controls', async () => {
    const user = userEvent.setup();
    renderCourses(['autoria.ler']);
    expect(await screen.findByText('.NET do zero à API')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: '+ Novo curso' })).not.toBeInTheDocument();
    await user.click(screen.getByRole('link', { name: 'Abrir .NET do zero à API' }));
    expect(await screen.findByRole('heading', { name: '.NET do zero à API' })).toBeInTheDocument();
    expect(screen.getByText(/Somente leitura/)).toBeInTheDocument();
    cleanup();
    renderCourses(['autoria.ler'], `/autoria/${authoringCourseFixture.courseId}`);
    expect(await screen.findByRole('heading', { name: '.NET do zero à API' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Editar|Publicar|Excluir/ })).not.toBeInTheDocument();
  });

  it('actor without authoring cannot open a direct link or request school data', async () => {
    renderCourses(['acesso.gerir']);
    let calls = 0;
    server.use(http.get(`${env.API_URL}/api/v1/courses`, () => { calls += 1; return HttpResponse.json({}); }));
    expect(await screen.findByRole('heading', { name: 'Esta área não é do seu papel' })).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Autoria' })).not.toBeInTheDocument();
    expect(calls).toBe(0);
  });

  it('filters and paginates through the URL and API and displays published status', async () => {
    const user = userEvent.setup(); const router = renderCourses();
    const requests: string[] = [];
    server.use(http.get(`${env.API_URL}/api/v1/courses`, ({ request }) => {
      const url = new URL(request.url); requests.push(url.search);
      return HttpResponse.json({ data: [{ ...authoringCourseSummaryFixture, status: 'published', currentVersion: 2, hasUnpublishedChanges: true }], pagination: { page: Number(url.searchParams.get('_page')), size: 20, total: 21, totalPages: 2 } });
    }));
    expect(await screen.findByText('Publicado · v2 · alterações não publicadas')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Publicados' }));
    expect(router.state.location.search).toBe('?status=published');
    await waitFor(() => expect(router.state.navigation.state).toBe('idle'));
    await waitFor(() => {
      expect(requests.some((query) => query.includes('status=published'))).toBe(true);
      expect(screen.queryByText('Carregando cursos…')).not.toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Próxima página' })).toBeEnabled();
    });
    await user.click(screen.getByRole('button', { name: 'Próxima página' }));
    await waitFor(() => expect(router.state.location.search).toContain('page=2'));
    await screen.findByText('Página 2 de 2 · 20 por página');
    await waitFor(() => expect(requests.some((query) => query.includes('_page=2') && query.includes('status=published'))).toBe(true));
  });

  it('shows a new-school empty state and retries a list failure', async () => {
    const user = userEvent.setup(); renderCourses(); let failed = true;
    server.use(http.get(`${env.API_URL}/api/v1/courses`, () => failed ? HttpResponse.json({ code: 'LEARNING_UNAVAILABLE' }, { status: 502 }) : HttpResponse.json({ data: [], pagination: { page: 1, size: 20, total: 0, totalPages: 0 } })));
    expect(await screen.findByRole('heading', { name: 'Não foi possível carregar os cursos' })).toBeInTheDocument();
    failed = false; await user.click(screen.getByRole('button', { name: 'Tentar novamente' }));
    const empty = await screen.findByRole('heading', { name: 'Nenhum curso ainda.' });
    expect(empty).toBeInTheDocument();
    await user.click(within(empty.closest('section')!).getByRole('button', { name: '+ Novo curso' }));
    expect(screen.getByRole('dialog', { name: 'Novo curso' })).toBeVisible();
  });

  it('opens the authoring card from the professor home', async () => {
    const user = userEvent.setup(); renderCourses(['autoria.ler', 'autoria.editar'], '/');
    await user.click(await screen.findByRole('link', { name: 'Abrir Autoria' }));
    expect(await screen.findByRole('heading', { name: 'Cursos da escola' })).toBeInTheDocument();
  });
});
