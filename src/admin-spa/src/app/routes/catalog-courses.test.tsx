import { cleanup, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { afterEach, describe, expect, it } from 'vitest';

import { routes } from '@/app/app-routes';
import { env } from '@/config/env';
import { catalogCourseFixture, catalogCoursesHandlers } from '@/testing/catalog-courses-handlers';
import { server } from '@/testing/server';
import { renderWithProviders } from '@/testing/test-utils';

const renderCatalog = (permissions = ['financeiro.ler', 'oferta.editar'], role = 'financeiro', path = '/catalogo') => {
  server.use(...catalogCoursesHandlers, http.get(`${env.API_URL}/api/v1/staff-sessions/current`, () => HttpResponse.json({
    accountId: '0198dfac-674a-7000-8000-000000000002', name: 'Financeiro', roles: [role], permissions, csrfToken: 'catalog-csrf',
  })));
  const router = createMemoryRouter(routes, { initialEntries: [path] });
  renderWithProviders(<RouterProvider router={router} />); return router;
};

describe('catalog courses', () => {
  afterEach(cleanup);

  it('finance sees the commercial menu and published courses with missing level and zero offers', async () => {
    renderCatalog();
    expect(await screen.findByRole('heading', { name: 'Cursos publicados da escola' })).toBeInTheDocument();
    expect(await screen.findByText('Fundamentos de C#')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Catálogo' })).toHaveAttribute('href', '/catalogo');
    expect(screen.getByText('Comercial')).toBeInTheDocument(); expect(screen.getByRole('link', { name: 'Financeiro' })).toBeInTheDocument();
    expect(screen.getByText('Sem nível')).toBeInTheDocument(); expect(screen.getByText('– Não')).toBeInTheDocument(); expect(screen.getByText('Sem ofertas')).toBeInTheDocument();
  });

  it.each([['administrador', 'acesso.gerir'], ['professor', 'autoria.ler'], ['suporte', 'suporte.atender']])('%s has no menu and cannot request catalog data via direct route', async (role, permission) => {
    let calls = 0; renderCatalog([permission], role);
    server.use(http.get(`${env.API_URL}/api/v1/catalog/courses`, () => { calls++; return HttpResponse.json({}); }));
    expect(await screen.findByRole('heading', { name: 'Você não tem acesso a esta área.' })).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Catálogo' })).not.toBeInTheDocument(); expect(calls).toBe(0);
  });

  it('paginates using the URL and cached query and displays level and offer counts', async () => {
    const user = userEvent.setup(); const router = renderCatalog(); const pages: number[] = [];
    server.use(http.get(`${env.API_URL}/api/v1/catalog/courses`, ({ request }) => {
      const url = new URL(request.url); const page = Number(url.searchParams.get('_page')); pages.push(page);
      expect(url.searchParams.get('_size')).toBe('10');
      return HttpResponse.json({ data: [{ ...catalogCourseFixture, title: page === 1 ? 'Curso A' : 'Curso B', level: 'advanced', inShowcase: true, offerCounts: { published: 2, draft: 1, unpublished: 1 } }], pagination: { page, size: 10, total: 11, totalPages: 2 } });
    }));
    expect(await screen.findByText('Curso A')).toBeInTheDocument(); expect(screen.getByText('Avançado')).toBeInTheDocument();
    expect(screen.getByText('✓ Sim')).toBeInTheDocument(); expect(screen.getByText('2 publicadas · 1 rascunho · 1 despublicada')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Próxima página' }));
    expect(await screen.findByText('Curso B')).toBeInTheDocument(); expect(router.state.location.search).toBe('?page=2');
    await user.click(screen.getByRole('button', { name: 'Página anterior' }));
    expect(await screen.findByText('Curso A')).toBeInTheDocument(); await waitFor(() => expect(pages).toContain(2));
  });

  it('shows loading then retries an unavailable catalog into the published-course empty state', async () => {
    const user = userEvent.setup(); renderCatalog(); let failed = true;
    server.use(http.get(`${env.API_URL}/api/v1/catalog/courses`, () => failed ? HttpResponse.json({ code: 'COMMERCE_UNAVAILABLE' }, { status: 502 })
      : HttpResponse.json({ data: [], pagination: { page: 1, size: 10, total: 0, totalPages: 0 } })));
    expect(await screen.findByRole('heading', { name: 'Não foi possível carregar o catálogo agora.' })).toBeInTheDocument();
    failed = false; await user.click(screen.getByRole('button', { name: 'Tentar de novo' }));
    expect(await screen.findByRole('heading', { name: 'Nenhum curso publicado ainda.' })).toBeInTheDocument();
    expect(screen.getByText('Só cursos publicados pelo professor aparecem aqui para receber ofertas.')).toBeInTheDocument();
  });
});
