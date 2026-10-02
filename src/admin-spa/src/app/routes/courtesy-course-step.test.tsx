import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { cleanup, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { afterEach, describe, expect, it } from 'vitest';

import { routes } from '@/app/app-routes';
import { env } from '@/config/env';
import { courtesyCourseFixture, courtesyCourseHandlers } from '@/testing/courtesy-course-handlers';
import { courtesyStudentFixture, courtesyStudentLookupHandlers } from '@/testing/courtesy-student-lookup-handlers';
import { server } from '@/testing/server';

const renderCourtesy = (permissions = ['financeiro.ler', 'cortesia.conceder'], role = 'financeiro') => {
  server.use(...courtesyCourseHandlers, ...courtesyStudentLookupHandlers, http.get(`${env.API_URL}/api/v1/staff-sessions/current`, () => HttpResponse.json({
    accountId: '0198dfac-674a-7000-8000-000000000002', name: 'Financeiro', roles: [role], permissions, csrfToken: 'courtesy-csrf',
  })));
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const router = createMemoryRouter(routes, { initialEntries: ['/cortesias'] });
  render(<QueryClientProvider client={queryClient}><RouterProvider router={router} /></QueryClientProvider>);
  return { router, queryClient };
};
const lookup = async (email = courtesyStudentFixture.email) => {
  const user = userEvent.setup();
  const input = await screen.findByRole('textbox', { name: 'E-mail do aluno' });
  await user.clear(input); await user.type(input, email); await user.click(screen.getByRole('button', { name: 'Localizar' }));
  return user;
};

const openCourseStep = async () => {
  const user = await lookup();
  await screen.findByRole('heading', { name: 'Joana Ribeiro' });
  await user.click(screen.getByRole('button', { name: 'Continuar' }));
  return user;
};

describe('courtesy course step', () => {
  afterEach(cleanup);

  it('lists courses without offers, requires a choice and keeps it in the form when returning', async () => {
    const { queryClient } = renderCourtesy(); const user = await openCourseStep();
    expect(await screen.findByRole('list', { name: 'Cursos publicados' })).toBeInTheDocument();
    expect(screen.getByText('Escolha um curso publicado da escola. Cortesia não exige oferta.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Continuar' })).toBeDisabled();
    await user.click(screen.getByRole('button', { name: 'Escolher Fundamentos de C#' }));
    expect(screen.getByRole('button', { name: 'Escolhido Fundamentos de C#' })).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByRole('button', { name: 'Continuar' })).toBeEnabled();
    expect(queryClient.getQueryCache().getAll().some((query) => query.queryKey[0] === 'courtesy-courses')).toBe(true);
    await user.click(screen.getByRole('button', { name: 'Voltar' }));
    expect(await screen.findByRole('heading', { name: 'Joana Ribeiro' })).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Continuar' }));
    expect(await screen.findByRole('button', { name: 'Escolhido Fundamentos de C#' })).toHaveAttribute('aria-pressed', 'true');
    await user.click(screen.getByRole('button', { name: 'Continuar' }));
    expect(await screen.findByRole('region', { name: 'Passo Prazo' })).toHaveTextContent('Fundamentos de C#');
  });

  it('submits title search, resets pagination, pages results and offers show-all on an empty result', async () => {
    renderCourtesy(); const requests: URL[] = [];
    server.use(http.get(`${env.API_URL}/api/v1/courtesy-courses`, ({ request }) => {
      const url = new URL(request.url); requests.push(url); const title = url.searchParams.get('title'); const page = Number(url.searchParams.get('_page'));
      return HttpResponse.json({ data: title === 'inexistente' ? [] : [{ ...courtesyCourseFixture, title: page === 2 ? 'Testes na prática' : courtesyCourseFixture.title }], pagination: { page, size: 10, total: title === 'inexistente' ? 0 : 11, totalPages: title === 'inexistente' ? 0 : 2 } });
    }));
    const user = await openCourseStep(); await screen.findByRole('button', { name: 'Escolher Fundamentos de C#' });
    await user.click(screen.getByRole('button', { name: 'Próxima' }));
    expect(await screen.findByRole('button', { name: 'Escolher Testes na prática' })).toBeInTheDocument();
    const input = screen.getByRole('textbox', { name: 'Buscar curso pelo título' });
    await user.type(input, 'fundamentos'); await user.click(screen.getByRole('button', { name: 'Buscar' }));
    expect(await screen.findByText('11 cursos com este título')).toBeInTheDocument();
    expect(requests.at(-1)?.searchParams.get('_page')).toBe('1'); expect(requests.at(-1)?.searchParams.get('title')).toBe('fundamentos');
    expect(screen.getByRole('textbox', { name: 'Buscar curso pelo título' })).toHaveValue('fundamentos');
    await user.clear(screen.getByRole('textbox', { name: 'Buscar curso pelo título' })); await user.type(screen.getByRole('textbox', { name: 'Buscar curso pelo título' }), 'inexistente');
    await user.click(screen.getByRole('button', { name: 'Buscar' }));
    expect(await screen.findByText('Nenhum curso publicado com este título.')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Ver todos' }));
    expect(await screen.findByRole('button', { name: 'Escolher Fundamentos de C#' })).toBeInTheDocument();
    expect(screen.getByRole('textbox', { name: 'Buscar curso pelo título' })).toHaveValue('');
  });

  it('shows loading and an upstream error and retries the same query', async () => {
    renderCourtesy(); let release: (() => void) | undefined;
    server.use(http.get(`${env.API_URL}/api/v1/courtesy-courses`, async () => {
      await new Promise<void>((resolve) => { release = resolve; });
      return HttpResponse.json({ code: 'COMMERCE_UNAVAILABLE' }, { status: 502 });
    }));
    const user = await openCourseStep();
    expect(await screen.findByRole('status', { name: 'Carregando cursos' })).toBeInTheDocument();
    release?.();
    expect(await screen.findByRole('alert')).toHaveTextContent('Não foi possível carregar os cursos agora.');
    server.use(...courtesyCourseHandlers);
    await user.click(screen.getByRole('button', { name: 'Tentar de novo' }));
    expect(await screen.findByRole('button', { name: 'Escolher Fundamentos de C#' })).toBeInTheDocument();
  });
});
