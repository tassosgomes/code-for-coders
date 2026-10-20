import { cleanup, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';

import { AppProviders } from '@/app/providers';
import { StudentLoginRoute } from '@/app/routes/student-login-route';
import { StudentPasswordRecoveryRoute } from '@/app/routes/student-password-recovery-route';
import { StudentRegistrationRoute } from '@/app/routes/student-registration-route';
import { StudentShowcaseCourseRoute } from '@/app/routes/student-showcase-course-route';
import { StudentShowcaseRoute } from '@/app/routes/student-showcase-route';
import { AuthLayout } from '@/components/auth-layout';
import { PublicLayout } from '@/components/public-layout';
import { env } from '@/config/env';
import { queryClient } from '@/lib/query-client';
import { server } from '@/testing/server';

const noticeName = 'Ambiente didático e de demonstração';
const originalEnabled = env.DIDACTIC_NOTICE_ENABLED;
const courseId = '3b4c5d6e-7f80-4a91-8b2c-4d5e6f7a8b9c';

const renderAt = (entry: string) => {
  const router = createMemoryRouter([
    {
      element: <PublicLayout />,
      children: [
        { path: '/cursos', element: <StudentShowcaseRoute /> },
        { path: '/cursos/:courseId', element: <StudentShowcaseCourseRoute /> },
      ],
    },
    {
      element: <AuthLayout />,
      children: [
        { path: '/entrar', element: <StudentLoginRoute /> },
        { path: '/cadastro', element: <StudentRegistrationRoute /> },
        { path: '/recuperar-senha', element: <StudentPasswordRecoveryRoute /> },
      ],
    },
  ], { initialEntries: [entry] });
  render(<AppProviders><RouterProvider router={router} /></AppProviders>);
};

beforeEach(() => {
  queryClient.clear();
  server.use(
    http.get(`${env.API_URL}/api/v1/showcase/courses`, () => HttpResponse.json({
      data: [], pagination: { page: 1, size: 12, total: 0, totalPages: 0 },
    })),
    http.get(`${env.API_URL}/api/v1/showcase/courses/:courseId`, () => HttpResponse.json({
      courseId, title: 'Curso de demonstração', level: 'beginner', description: 'Conteúdo didático.',
      prerequisite: { text: null, recommendedCourses: [] }, modules: [], offers: [],
    })),
  );
});

afterEach(() => {
  cleanup();
  env.DIDACTIC_NOTICE_ENABLED = originalEnabled;
  queryClient.clear();
});

describe.each([true, false])('didactic notice enabled: %s', (enabled) => {
  beforeEach(() => { env.DIDACTIC_NOTICE_ENABLED = enabled; });

  it.each(['/cursos', `/cursos/${courseId}`, '/entrar', '/cadastro', '/recuperar-senha'])(
    'respects the flag when opening %s directly', async (entry) => {
      renderAt(entry);
      await screen.findByRole('heading', { level: 1 });

      if (!enabled) {
        expect(screen.queryByRole('note', { name: noticeName })).not.toBeInTheDocument();
        return;
      }

      const notice = screen.getByRole('note', { name: noticeName });
      expect(notice).toHaveTextContent('Não informe dados pessoais, senhas ou informações de pagamento reais. Utilize apenas dados fictícios.');
      expect(screen.getAllByRole('note', { name: noticeName })).toHaveLength(1);
      const firstInput = screen.queryAllByRole('textbox')[0];
      if (firstInput) {
        expect(notice.compareDocumentPosition(firstInput) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
      }
    },
  );

  it('keeps the flag when navigating from the showcase to registration', async () => {
    const user = userEvent.setup();
    renderAt('/cursos');

    await user.click(screen.getByRole('link', { name: 'Criar conta' }));
    expect(await screen.findByRole('textbox', { name: /E-mail/i })).toBeInTheDocument();
    expect(screen.queryByRole('note', { name: noticeName }) !== null).toBe(enabled);
  });
});
