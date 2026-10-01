import { cleanup, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { createMemoryRouter, RouterProvider, type RouteObject } from 'react-router';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';

import { AppProviders } from '@/app/providers';
import { RootRoute } from '@/app/routes/root-route';
import { RouteError } from '@/app/routes/route-error';
import { StudentShowcaseCourseRoute } from '@/app/routes/student-showcase-course-route';
import { StudentShowcaseRoute } from '@/app/routes/student-showcase-route';
import { PublicLayout } from '@/components/public-layout';
import { env } from '@/config/env';
import { queryClient } from '@/lib/query-client';
import { server } from '@/testing/server';

const routes: RouteObject[] = [
  {
    path: '/',
    element: <RootRoute />,
    errorElement: <RouteError />,
    children: [
      {
        element: <PublicLayout />,
        errorElement: <RouteError layout="public" />,
        children: [
          { path: 'cursos', element: <StudentShowcaseRoute /> },
          { path: 'cursos/:courseId', element: <StudentShowcaseCourseRoute /> },
        ],
      },
    ],
  },
];

const cards = [
  { courseId: '3b4c5d6e-7f80-4a91-8b2c-4d5e6f7a8b9c', title: 'Fundamentos de C#', level: 'beginner', summary: 'Sintaxe e tipos.', lowestPriceCents: 29700, offerCount: 1 },
  { courseId: '6f1e2d3c-4b5a-4c69-8d7e-1f2a3b4c5d6e', title: '.NET do zero à API', level: 'advanced', summary: 'Uma API no ar.', lowestPriceCents: 39700, offerCount: 2 },
  { courseId: '9a8b7c6d-5e4f-4a3b-8c2d-1e0f9a8b7c6d', title: 'Testes na prática', level: 'intermediate', summary: 'Escreva testes.', lowestPriceCents: 19700, offerCount: 1 },
];

const requestedUrls: URL[] = [];

const showcaseHandler = http.get(`${env.API_URL}/api/v1/showcase/courses`, ({ request }) => {
  const url = new URL(request.url);
  requestedUrls.push(url);
  const level = url.searchParams.get('level');
  const data = level ? cards.filter((card) => card.level === level) : cards;

  return HttpResponse.json({ data, pagination: { page: 1, size: 12, total: data.length, totalPages: 1 } });
});

const renderShowcase = (entry: string) => {
  const router = createMemoryRouter(routes, { initialEntries: [entry] });
  render(
    <AppProviders>
      <RouterProvider router={router} />
    </AppProviders>,
  );

  return router;
};

describe('student showcase flow', () => {
  beforeEach(() => {
    queryClient.clear();
    requestedUrls.length = 0;
    window.localStorage.clear();
    window.sessionStorage.clear();
    server.use(showcaseHandler);
  });

  afterEach(() => {
    cleanup();
    queryClient.clear();
  });

  it('lists the showcase for a visitor without ever reading the student session', async () => {
    const sessionRequests: string[] = [];
    server.events.on('request:start', ({ request }) => {
      if (request.url.includes('student-sessions')) {
        sessionRequests.push(request.url);
      }
    });
    renderShowcase('/cursos');

    expect(await screen.findByRole('link', { name: 'Fundamentos de C#' })).toBeInTheDocument();
    expect(screen.getAllByRole('heading', { level: 2 })).toHaveLength(3);
    expect(screen.getByRole('link', { name: 'Entrar' })).toHaveAttribute('href', '/entrar');
    expect(screen.getByRole('link', { name: 'Criar conta' })).toHaveAttribute('href', '/cadastro');
    expect(screen.getByText('a partir de R$ 397,00')).toBeInTheDocument();
    expect(requestedUrls[0]?.searchParams.get('level')).toBeNull();
    expect(requestedUrls[0]?.searchParams.get('_size')).toBe('12');
    expect(sessionRequests).toEqual([]);
    server.events.removeAllListeners();
  });

  it('keeps the same public page and header for a signed-in student, even if a session event fires', async () => {
    const router = renderShowcase('/cursos');
    await screen.findByRole('link', { name: 'Fundamentos de C#' });

    window.dispatchEvent(new Event('app:session-expired'));

    expect(router.state.location.pathname).toBe('/cursos');
    expect(screen.getByRole('link', { name: 'Entrar' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Entrar' })).not.toBeInTheDocument();
  });

  it('puts the chosen level in the address, translates it to the contract enum and keeps focus on the option', async () => {
    const user = userEvent.setup();
    const router = renderShowcase('/cursos');
    await screen.findByRole('link', { name: 'Fundamentos de C#' });

    await user.click(screen.getByRole('radio', { name: 'Iniciante' }));

    await waitFor(() => expect(router.state.location.search).toBe('?nivel=iniciante'));
    expect(await screen.findByRole('status', { name: '' })).toHaveTextContent('1 curso');
    expect(screen.queryByRole('link', { name: '.NET do zero à API' })).not.toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Fundamentos de C#' })).toBeInTheDocument();
    expect(requestedUrls.at(-1)?.searchParams.get('level')).toBe('beginner');
    expect(screen.getByRole('radio', { name: 'Iniciante' })).toHaveFocus();
  });

  it('opens a shared address with the filter already marked', async () => {
    renderShowcase('/cursos?nivel=avancado');

    expect(await screen.findByRole('link', { name: '.NET do zero à API' })).toBeInTheDocument();
    expect(screen.getByRole('radio', { name: 'Avançado' })).toBeChecked();
    expect(screen.queryByRole('link', { name: 'Fundamentos de C#' })).not.toBeInTheDocument();
    expect(requestedUrls[0]?.searchParams.get('level')).toBe('advanced');
  });

  it('treats an unknown level in the address as "Todos" without an error', async () => {
    renderShowcase('/cursos?nivel=xyz');

    expect(await screen.findByRole('link', { name: 'Fundamentos de C#' })).toBeInTheDocument();
    expect(screen.getByRole('radio', { name: 'Todos' })).toBeChecked();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    expect(requestedUrls[0]?.searchParams.get('level')).toBeNull();
  });

  it('shows the empty state for a level without courses and goes back to "Todos"', async () => {
    const user = userEvent.setup();
    server.use(http.get(`${env.API_URL}/api/v1/showcase/courses`, ({ request }) => {
      const level = new URL(request.url).searchParams.get('level');
      const data = level ? [] : cards;

      return HttpResponse.json({ data, pagination: { page: 1, size: 12, total: data.length, totalPages: data.length ? 1 : 0 } });
    }));
    const router = renderShowcase('/cursos?nivel=iniciante');

    expect(await screen.findByText('Nenhum curso neste nível por enquanto.')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Ver todos os cursos' }));

    expect(await screen.findByRole('link', { name: 'Fundamentos de C#' })).toBeInTheDocument();
    expect(router.state.location.search).toBe('');
    expect(screen.getByRole('radio', { name: 'Todos' })).toBeChecked();
  });

  it('explains a failure from the BFF and loads again on retry', async () => {
    const user = userEvent.setup();
    server.use(http.get(`${env.API_URL}/api/v1/showcase/courses`, () =>
      HttpResponse.json({ code: 'SHOWCASE_UNAVAILABLE' }, { status: 502 }), { once: true }));
    renderShowcase('/cursos');

    expect(await screen.findByRole('alert')).toHaveTextContent('Não foi possível carregar os cursos agora.');
    await user.click(screen.getByRole('button', { name: 'Tentar de novo' }));

    expect(await screen.findByRole('link', { name: 'Fundamentos de C#' })).toBeInTheDocument();
  });

  it('opens the course by id from the card, never by title', async () => {
    const user = userEvent.setup();
    const router = renderShowcase('/cursos');

    await user.click(await screen.findByRole('link', { name: 'Testes na prática' }));

    expect(router.state.location.pathname).toBe('/cursos/9a8b7c6d-5e4f-4a3b-8c2d-1e0f9a8b7c6d');
    expect(within(await screen.findByRole('main')).getByRole('link', { name: 'Ver todos os cursos' })).toHaveAttribute('href', '/cursos');
  });
});
