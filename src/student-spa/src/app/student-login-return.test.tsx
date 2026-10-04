import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { requireStudentSession } from '@/app/routes/dashboard-route';
import { StudentLoginRoute } from '@/app/routes/student-login-route';
import { StudentLessonRoute } from '@/app/routes/student-lesson-route';
import { env } from '@/config/env';
import { paths } from '@/config/paths';
import { server } from '@/testing/server';
import { lessonId } from '@/testing/student-lesson-data';
import { renderWithProviders } from '@/testing/test-utils';

const enter = async () => {
  await userEvent.type(await screen.findByLabelText('E-mail'), 'ana@example.com');
  await userEvent.type(screen.getByLabelText('Senha'), 'SecurePassword1!');
  await userEvent.click(screen.getByRole('button', { name: 'Entrar' }));
};
const routes = [
  { path: paths.studentLogin.path, element: <StudentLoginRoute /> },
  { path: paths.studentLesson.path, loader: requireStudentSession, element: <StudentLessonRoute /> },
  { path: '/', element: <h1>Home</h1> },
];

describe('Student login return', () => {
  afterEach(() => vi.unstubAllEnvs());
  it('direct lesson under student basename returns to the same lesson after login', async () => {
    vi.stubEnv('BASE_URL', '/student/');
    let loggedIn = false;
    server.use(
      http.get(`${env.API_URL}/api/v1/student-sessions/current`, () => loggedIn ? HttpResponse.json({ accountId: '00000000-0000-7000-8000-000000000001', name: 'Ana', csrfToken: 'csrf' }) : HttpResponse.json({ code: 'SESSION_REQUIRED' }, { status: 401 })),
      http.post(`${env.API_URL}/api/v1/student-sessions`, () => { loggedIn = true; return HttpResponse.json({ accountId: '00000000-0000-7000-8000-000000000001', name: 'Ana', csrfToken: 'csrf' }); }),
    );
    const router = createMemoryRouter(routes, { basename: '/student', initialEntries: [`/student${paths.studentLesson.getHref(lessonId)}`] });
    renderWithProviders(<RouterProvider router={router} />);
    await screen.findByLabelText('E-mail');
    expect(new URLSearchParams(router.state.location.search).get('returnTo')).toBe(paths.studentLesson.getHref(lessonId));
    await enter(); expect(await screen.findByRole('heading', { name: 'Injeção de dependência' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe(`/student${paths.studentLesson.getHref(lessonId)}`);
  });
  it.each(['https://evil.example', '//evil.example', '/\\evil.example', '/%2f%2fevil.example', 'aulas/anything'])('discards unsafe login return %s', async (returnTo) => {
    const router = createMemoryRouter(routes, { initialEntries: [paths.studentLogin.getHref(returnTo)] });
    renderWithProviders(<RouterProvider router={router} />); await enter();
    await waitFor(() => expect(router.state.location.pathname).toBe('/'));
    expect(screen.getByRole('heading', { name: 'Home' })).toBeInTheDocument();
  });
});
