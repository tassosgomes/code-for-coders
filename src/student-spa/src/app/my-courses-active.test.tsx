import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { MemoryRouter, Route, Routes, useParams } from 'react-router';
import { beforeEach, describe, expect, it } from 'vitest';

import { DashboardRoute } from '@/app/routes/dashboard-route';
import { env } from '@/config/env';
import { continueLessonId, myCoursesData, startLessonId } from '@/testing/my-courses-data';
import { server } from '@/testing/server';
import { renderWithProviders } from '@/testing/test-utils';

const endpoint = `${env.API_URL}/api/v1/my-courses`;
const LessonDestination = () => <h1>Aula {useParams().lessonId}</h1>;
const renderDashboard = () => renderWithProviders(
  <MemoryRouter basename="/student" initialEntries={['/student/']}>
    <Routes>
      <Route path="/" element={<DashboardRoute />} />
      <Route path="/aulas/:lessonId" element={<LessonDestination />} />
      <Route path="/cursos" element={<h1>Vitrine de cursos</h1>} />
    </Routes>
  </MemoryRouter>,
);
beforeEach(() => {
  localStorage.clear();
  server.use(http.get(endpoint, () => HttpResponse.json(myCoursesData)));
});

describe('My active courses', () => {
  it('shows the current titles, counts, percentages and preserves the account card', async () => {
    renderDashboard();
    expect(await screen.findByRole('heading', { name: 'React do zero' })).toBeInTheDocument();
    expect(screen.getByText('37% · 3 de 8 aulas')).toBeInTheDocument();
    expect(screen.getByText('0% · 0 de 10 aulas')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Sua conta' })).toBeInTheDocument();
    expect(screen.getByRole('progressbar', { name: 'Progresso de React do zero' })).toHaveAttribute('aria-valuetext', '3 de 8 aulas concluídas');
    expect(screen.getByRole('progressbar', { name: 'Progresso de React do zero' })).toHaveAttribute('aria-valuenow', '37');
  });

  it('preserves the server order of started courses followed by courses to begin', async () => {
    renderDashboard(); await screen.findByRole('heading', { name: 'React do zero' });
    const section = screen.getByRole('region', { name: 'Seus cursos' });
    expect(within(section).getAllByRole('heading', { level: 4 }).map((item) => item.textContent))
      .toEqual(['React do zero', 'TypeScript na prática']);
  });

  it('continues to the lesson chosen by learning under the student base path', async () => {
    const user = userEvent.setup(); renderDashboard();
    const link = await screen.findByRole('link', { name: 'Continuar' });
    expect(link).toHaveAttribute('href', `/student/aulas/${continueLessonId}`);
    await user.click(link);
    expect(await screen.findByRole('heading', { name: `Aula ${continueLessonId}` })).toBeInTheDocument();
  });

  it('begins a course at its server supplied first lesson', async () => {
    const user = userEvent.setup(); renderDashboard();
    await user.click(await screen.findByRole('link', { name: 'Começar' }));
    expect(await screen.findByRole('heading', { name: `Aula ${startLessonId}` })).toBeInTheDocument();
  });

  it('offers the catalog for a student without any grants', async () => {
    server.use(http.get(endpoint, () => HttpResponse.json({ progressAvailable: true, active: [], ended: [] })));
    const user = userEvent.setup(); renderDashboard();
    expect(await screen.findByRole('heading', { name: 'Você ainda não tem cursos' })).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Continuar' })).not.toBeInTheDocument();
    await user.click(screen.getByRole('link', { name: 'Explorar cursos' }));
    expect(screen.getByRole('heading', { name: 'Vitrine de cursos' })).toBeInTheDocument();
  });

  it('shows course skeletons until the authenticated list arrives without flashing empty', async () => {
    let release: (() => void) | undefined;
    const pending = new Promise<void>((resolve) => { release = resolve; });
    server.use(http.get(endpoint, async () => { await pending; return HttpResponse.json(myCoursesData); }));
    renderDashboard();
    expect(await screen.findByRole('status', { name: 'Carregando seus cursos' })).toBeInTheDocument();
    expect(screen.queryByText('Você ainda não tem cursos')).not.toBeInTheDocument();
    release?.();
    expect(await screen.findByRole('heading', { name: 'React do zero' })).toBeInTheDocument();
  });
});
