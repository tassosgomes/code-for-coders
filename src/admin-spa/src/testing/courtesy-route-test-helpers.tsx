import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { createMemoryRouter, RouterProvider } from 'react-router';

import { routes } from '@/app/app-routes';
import { env } from '@/config/env';
import { courtesyCourseHandlers } from '@/testing/courtesy-course-handlers';
import { courtesyStudentFixture, courtesyStudentLookupHandlers } from '@/testing/courtesy-student-lookup-handlers';
import { server } from '@/testing/server';

export const renderCourtesy = (permissions = ['financeiro.ler', 'cortesia.conceder'], role = 'financeiro') => {
  server.use(
    ...courtesyCourseHandlers,
    ...courtesyStudentLookupHandlers,
    http.get(`${env.API_URL}/api/v1/staff-sessions/current`, () => HttpResponse.json({
      accountId: '0198dfac-674a-7000-8000-000000000002',
      name: 'Financeiro',
      roles: [role],
      permissions,
      csrfToken: 'courtesy-csrf',
    })),
  );

  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const router = createMemoryRouter(routes, { initialEntries: ['/cortesias'] });
  render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );

  return { router, queryClient };
};

export const lookupCourtesyStudent = async (email = courtesyStudentFixture.email) => {
  const user = userEvent.setup();
  const input = await screen.findByRole('textbox', { name: 'E-mail do aluno' });
  await user.clear(input);
  await user.type(input, email);
  await user.click(screen.getByRole('button', { name: 'Localizar' }));
  return user;
};
