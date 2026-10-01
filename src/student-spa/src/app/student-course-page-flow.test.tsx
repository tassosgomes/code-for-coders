import { cleanup, render, screen, within } from '@testing-library/react';
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

const advanced = '6f1e2d3c-4b5a-4c69-8d7e-1f2a3b4c5d6e';
const csharp = '3b4c5d6e-7f80-4a91-8b2c-4d5e6f7a8b9c';

const pageOf = (courseId: string, title: string, overrides: Record<string, unknown> = {}) => ({
  courseId,
  title,
  level: courseId === csharp ? 'beginner' : 'advanced',
  description: `Descrição de ${title}.`,
  prerequisite: {
    text: courseId === csharp ? null : 'Git e C# básico.',
    recommendedCourses: courseId === csharp ? [] : [{ courseId: csharp, title: 'Fundamentos de C#', inShowcase: true }],
  },
  modules: [{ title: 'Módulo 1', lessons: [{ title: 'Primeira aula' }] }],
  offers: [{ offerId: '7c8d9e0f-1a2b-4c3d-8e4f-5a6b7c8d9e0f', name: 'Acesso por 1 mês', priceCents: 4990, accessPeriod: { type: 'months', months: 1 } }],
  ...overrides,
});

const requestedIds: string[] = [];

const courseHandler = http.get(`${env.API_URL}/api/v1/showcase/courses/:courseId`, ({ params }) => {
  const courseId = String(params.courseId);
  requestedIds.push(courseId);
  if (courseId === advanced) {
    return HttpResponse.json(pageOf(advanced, '.NET do zero à API'));
  }
  if (courseId === csharp) {
    return HttpResponse.json(pageOf(csharp, 'Fundamentos de C#'));
  }

  return HttpResponse.json(
    { type: 'about:blank', title: 'Curso não disponível.', status: 404, code: 'SHOWCASE_COURSE_NOT_FOUND', traceId: 'abc' },
    { status: 404 },
  );
});

const renderAt = (entry: string) => {
  const router = createMemoryRouter(routes, { initialEntries: [entry] });
  const view = render(
    <AppProviders>
      <RouterProvider router={router} />
    </AppProviders>,
  );

  return { router, view };
};

describe('student course page flow', () => {
  beforeEach(() => {
    queryClient.clear();
    requestedIds.length = 0;
    server.use(courseHandler);
  });

  afterEach(() => {
    cleanup();
    queryClient.clear();
  });

  it('opens the course page straight from its address, inside the public layout', async () => {
    renderAt(`/cursos/${advanced}`);

    expect(await screen.findByRole('heading', { level: 1, name: '.NET do zero à API' })).toBeInTheDocument();
    expect(screen.getByText('Acesso por 1 mês, contado a partir da liberação')).toBeInTheDocument();
    expect(screen.getByText('R$ 49,90')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Entrar' })).toBeInTheDocument();
    expect(document.title).toBe('.NET do zero à API | Code4Coders');
    expect(requestedIds).toEqual([advanced]);
  });

  it('follows the link of a recommended course to its own page', async () => {
    const user = userEvent.setup();
    const { router } = renderAt(`/cursos/${advanced}`);

    await user.click(await screen.findByRole('link', { name: 'Fundamentos de C#' }));

    expect(router.state.location.pathname).toBe(`/cursos/${csharp}`);
    expect(await screen.findByRole('heading', { level: 1, name: 'Fundamentos de C#' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Recomendamos saber antes' })).not.toBeInTheDocument();
  });

  it('shows the same screen for a course that does not exist, is not on sale or belongs to another school', async () => {
    const texts: string[] = [];
    for (const id of ['00000000-0000-4000-8000-000000000000', 'a0a0a0a0-0000-4000-8000-00000000abcd', 'not-an-identifier']) {
      queryClient.clear();
      const { view } = renderAt(`/cursos/${id}`);
      expect(await screen.findByRole('heading', { level: 1, name: 'Este curso não está disponível.' })).toBeInTheDocument();
      expect(within(screen.getByRole('main')).getByRole('link', { name: 'Ver todos os cursos' })).toHaveAttribute('href', '/cursos');
      texts.push(screen.getByRole('main').textContent ?? '');
      view.unmount();
    }

    expect(new Set(texts).size).toBe(1);
  });

  it('explains a failure of the BFF with a retry that loads the page', async () => {
    const user = userEvent.setup();
    server.use(
      http.get(`${env.API_URL}/api/v1/showcase/courses/:courseId`, () => HttpResponse.json({ code: 'SHOWCASE_UNAVAILABLE' }, { status: 502 }), {
        once: true,
      }),
    );
    renderAt(`/cursos/${advanced}`);

    expect(await screen.findByRole('alert')).toHaveTextContent('Não foi possível carregar este curso agora.');
    await user.click(screen.getByRole('button', { name: 'Tentar de novo' }));

    expect(await screen.findByRole('heading', { level: 1, name: '.NET do zero à API' })).toBeInTheDocument();
  });

  it('shows a lifetime offer without a period count and never reads the student session', async () => {
    const sessionRequests: string[] = [];
    server.events.on('request:start', ({ request }) => {
      if (request.url.includes('student-sessions')) {
        sessionRequests.push(request.url);
      }
    });
    server.use(
      http.get(`${env.API_URL}/api/v1/showcase/courses/:courseId`, () =>
        HttpResponse.json(
          pageOf(advanced, 'Vitalício', {
            offers: [{ offerId: '8d9e0f1a-2b3c-4d4e-9f5a-6b7c8d9e0f1a', name: 'Para sempre', priceCents: 89700, accessPeriod: { type: 'lifetime' } }],
          }),
        ),
      ),
    );
    renderAt(`/cursos/${advanced}`);

    expect(await screen.findByText('R$ 897,00')).toBeInTheDocument();
    expect(screen.getByText('Acesso vitalício')).toBeInTheDocument();
    expect(sessionRequests).toEqual([]);
    server.events.removeAllListeners();
  });
});
