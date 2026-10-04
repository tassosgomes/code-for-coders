import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { MemoryRouter, Route, Routes, useParams } from 'react-router';
import { beforeEach, describe, expect, it } from 'vitest';

import { DashboardRoute } from '@/app/routes/dashboard-route';
import { env } from '@/config/env';
import { endedCoursesData, myCoursesData, startLessonId, unavailableProgressData } from '@/testing/my-courses-data';
import { server } from '@/testing/server';
import { renderWithProviders } from '@/testing/test-utils';

const endpoint = `${env.API_URL}/api/v1/my-courses`;
const unavailableMessage = 'Não foi possível carregar seus cursos agora. Tente de novo em instantes.';
const LessonDestination = () => <h1>Aula {useParams().lessonId}</h1>;
const renderDashboard = () => renderWithProviders(
  <MemoryRouter basename="/student" initialEntries={['/student/']}>
    <Routes>
      <Route path="/" element={<DashboardRoute />} />
      <Route path="/aulas/:lessonId" element={<LessonDestination />} />
    </Routes>
  </MemoryRouter>,
);
beforeEach(() => {
  localStorage.clear();
  server.use(http.get(endpoint, () => HttpResponse.json(endedCoursesData)));
});

describe('My courses with ended access or unavailable data', () => {
  it('shows ended access with the retained progress and calendar date without actions', async () => {
    renderDashboard();
    const section = await screen.findByRole('region', { name: 'Acesso encerrado' });
    expect(within(section).getByRole('heading', { name: 'C# e ASP.NET Core' })).toBeInTheDocument();
    expect(within(section).getByText('50% · 5 de 10 aulas')).toBeInTheDocument();
    expect(within(section).getByText('Seu acesso terminou em 01/10/2026')).toBeInTheDocument();
    expect(within(section).queryByRole('button')).not.toBeInTheDocument();
    expect(within(section).queryByRole('link')).not.toBeInTheDocument();
    expect(screen.queryByText('Você ainda não tem cursos')).not.toBeInTheDocument();
  });

  it('uses the same end date message for an unknown ended reason', async () => {
    server.use(http.get(endpoint, () => HttpResponse.json({
      ...endedCoursesData, ended: endedCoursesData.ended.map((course) => ({ ...course, endedReason: 'future-reason' })),
    })));
    renderDashboard();
    expect(await screen.findByText('Seu acesso terminou em 01/10/2026')).toBeInTheDocument();
    expect(screen.queryByText('future-reason')).not.toBeInTheDocument();
  });

  it('preserves the server order and separates ended access from active course actions', async () => {
    server.use(http.get(endpoint, () => HttpResponse.json({
      ...myCoursesData, ended: [
        ...endedCoursesData.ended,
        { ...endedCoursesData.ended[0], courseId: '018f1000-0000-7000-8000-000000000004', title: 'Curso anterior', endedOn: '2026-09-01' },
      ],
    })));
    renderDashboard();
    const section = await screen.findByRole('region', { name: 'Acesso encerrado' });
    expect(within(section).getAllByRole('heading', { level: 4 }).map((item) => item.textContent))
      .toEqual(['C# e ASP.NET Core', 'Curso anterior']);
    expect(within(section).queryByRole('link')).not.toBeInTheDocument();
    expect(within(screen.getByRole('region', { name: 'Seus cursos' })).getByRole('link', { name: 'Continuar' })).toBeInTheDocument();
  });

  it('announces unavailable courses with retry and keeps the account instead of showing empty', async () => {
    server.use(http.get(endpoint, () => HttpResponse.json({ code: 'COURSE_ACCESS_UNAVAILABLE' }, { status: 503 })));
    renderDashboard();
    expect(await screen.findByText(unavailableMessage)).toHaveAttribute('role', 'alert');
    expect(screen.getByRole('button', { name: 'Tentar de novo' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Sua conta' })).toBeInTheDocument();
    expect(screen.queryByText('Você ainda não tem cursos')).not.toBeInTheDocument();
  });

  it('retries the failed request and shows courses when the service recovers', async () => {
    server.use(http.get(endpoint, () => HttpResponse.json({ code: 'COURSE_ACCESS_UNAVAILABLE' }, { status: 503 })));
    const user = userEvent.setup(); renderDashboard();
    const retry = await screen.findByRole('button', { name: 'Tentar de novo' });
    server.use(http.get(endpoint, () => HttpResponse.json(myCoursesData)));
    await user.click(retry);
    expect(await screen.findByRole('heading', { name: 'React do zero' })).toBeInTheDocument();
    expect(screen.queryByText(unavailableMessage)).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Tentar de novo' })).not.toBeInTheDocument();
  });

  it('keeps retry available when the service is still unavailable', async () => {
    let requests = 0;
    server.use(http.get(endpoint, () => {
      requests += 1;
      return HttpResponse.json({ code: 'COURSE_ACCESS_UNAVAILABLE' }, { status: 503 });
    }));
    const user = userEvent.setup(); renderDashboard();
    await user.click(await screen.findByRole('button', { name: 'Tentar de novo' }));
    expect(await screen.findByText(unavailableMessage)).toBeInTheDocument();
    expect(requests).toBe(2);
    expect(screen.getByRole('button', { name: 'Tentar de novo' })).toBeInTheDocument();
    expect(screen.queryByText('Você ainda não tem cursos')).not.toBeInTheDocument();
  });

  it('warns about unavailable progress, hides counts and begins at the server supplied first lesson', async () => {
    server.use(http.get(endpoint, () => HttpResponse.json(unavailableProgressData)));
    const user = userEvent.setup(); renderDashboard();
    expect(await screen.findByText('Seu progresso não pôde ser carregado agora')).toBeInTheDocument();
    expect(screen.getByRole('alert')).toHaveTextContent('Seu progresso não pôde ser carregado agora');
    expect(screen.queryByText(/\d+%|\d+ de \d+ aulas/u)).not.toBeInTheDocument();
    expect(screen.queryByRole('progressbar')).not.toBeInTheDocument();
    expect(screen.getByText('Seu acesso terminou em 01/10/2026')).toBeInTheDocument();
    const links = screen.getAllByRole('link', { name: 'Começar' });
    for (const link of links) expect(link).toHaveAttribute('href', `/student/aulas/${startLessonId}`);
    await user.click(links[0]!);
    expect(await screen.findByRole('heading', { name: `Aula ${startLessonId}` })).toBeInTheDocument();
  });
});
