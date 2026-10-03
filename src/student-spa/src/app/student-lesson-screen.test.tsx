import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { delay, http, HttpResponse } from 'msw';
import { MemoryRouter, Route, Routes } from 'react-router';
import { describe, expect, it } from 'vitest';

import { StudentAppLayoutRoute } from '@/app/routes/student-app-layout-route';
import { StudentLessonRoute } from '@/app/routes/student-lesson-route';
import { env } from '@/config/env';
import { paths } from '@/config/paths';
import { server } from '@/testing/server';
import { lessonId, secondLessonId, studentLessonData } from '@/testing/student-lesson-data';
import { renderWithProviders } from '@/testing/test-utils';

const endpoint = `${env.API_URL}/api/v1/lessons/:lessonId`;
const renderLesson = () => renderWithProviders(<MemoryRouter initialEntries={[paths.studentLesson.getHref(lessonId)]}>
  <Routes><Route element={<StudentAppLayoutRoute />}><Route path={paths.studentLesson.path} element={<StudentLessonRoute />} /></Route></Routes>
</MemoryRouter>);

describe('Student lesson screen', () => {
  it('shows loading without titles before the decision', async () => {
    server.use(http.get(endpoint, async () => { await delay(100); return HttpResponse.json(studentLessonData); }));
    renderLesson();
    expect(screen.getByRole('status', { name: 'Carregando aula' })).toBeInTheDocument();
    expect(screen.queryByText('APIs com .NET')).not.toBeInTheDocument();
    expect(await screen.findByRole('heading', { name: 'Injeção de dependência' })).toBeInTheDocument();
  });
  it('shows curriculum with current lesson and player loading', async () => {
    renderLesson();
    expect(await screen.findByRole('heading', { name: 'Injeção de dependência' })).toBeInTheDocument();
    expect(screen.getByRole('navigation', { name: 'Aulas do curso' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Injeção de dependência.*Aula atual/u })).toHaveAttribute('aria-current', 'page');
    expect(screen.getByText('Carregando vídeo…')).toBeInTheDocument();
    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
    expect(document.title).toBe('Injeção de dependência');
  });
  it('shows no access without leaking course or lesson titles', async () => {
    server.use(http.get(endpoint, () => HttpResponse.json({ code: 'ACCESS_DENIED', reason: 'no-grant' }, { status: 403 })));
    renderLesson(); expect(await screen.findByRole('alert')).toHaveTextContent('Você não tem acesso a este curso.');
    expect(screen.queryByText('APIs com .NET')).not.toBeInTheDocument(); expect(screen.queryByText('Injeção de dependência')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Tentar de novo' })).not.toBeInTheDocument();
  });
  it('shows exclusive access end as the last day in school time zone', async () => {
    server.use(http.get(endpoint, () => HttpResponse.json({ code: 'ACCESS_DENIED', reason: 'grant-ended', accessEndedAt: '2028-03-16T03:00:00Z' }, { status: 403 })));
    renderLesson(); expect(await screen.findByRole('alert')).toHaveTextContent('Seu acesso a este curso terminou em 15/03/2028.');
    expect(screen.queryByText('APIs com .NET')).not.toBeInTheDocument();
  });
  it('shows the same unavailable lesson state for a 404', async () => {
    server.use(http.get(endpoint, () => HttpResponse.json({ code: 'LESSON_NOT_AVAILABLE' }, { status: 404 })));
    renderLesson(); expect(await screen.findByRole('alert')).toHaveTextContent('Esta aula não está disponível.');
    expect(screen.queryByRole('navigation', { name: 'Aulas do curso' })).not.toBeInTheDocument();
  });
  it.each([503, 502, 504])('allows retry after unavailable upstream %s', async (status) => {
    server.use(http.get(endpoint, () => HttpResponse.json({ code: 'ACCESS_DECISION_UNAVAILABLE' }, { status })));
    renderLesson(); expect(await screen.findByRole('alert')).toHaveTextContent('Não foi possível confirmar seu acesso agora.');
    server.use(http.get(endpoint, () => HttpResponse.json(studentLessonData)));
    await userEvent.click(screen.getByRole('button', { name: 'Tentar de novo' }));
    expect(await screen.findByRole('heading', { name: 'Injeção de dependência' })).toBeInTheDocument();
  });
  it('navigates to the selected lesson and checks access again', async () => {
    server.use(http.get(endpoint, ({ params }) => HttpResponse.json(params.lessonId === secondLessonId
      ? { ...studentLessonData, lesson: { ...studentLessonData.lesson, lessonId: secondLessonId, title: 'Persistência', position: 2 } } : studentLessonData)));
    renderLesson(); await screen.findByRole('heading', { name: 'Injeção de dependência' });
    await userEvent.click(screen.getByRole('link', { name: '2. Persistência' }));
    await waitFor(() => expect(screen.getByRole('heading', { name: 'Persistência' })).toBeInTheDocument());
    expect(screen.getByRole('link', { name: /Persistência.*Aula atual/u })).toHaveAttribute('aria-current', 'page');
  });
});
