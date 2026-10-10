import { cleanup, screen, within } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { afterEach, describe, expect, it } from 'vitest';

import { AdminLayoutRoute } from '@/app/routes/admin-layout-route';
import { AuthoringCourseRoute } from '@/app/routes/authoring-course-route';
import { AuthoringCoursesRoute } from '@/app/routes/authoring-courses-route';
import { AuthoringVersionRoute } from '@/app/routes/authoring-version-route';
import { loadStaffSession } from '@/app/routes/staff-session-loader';
import { env } from '@/config/env';
import { authoringCourseFixture, authoringCourseSummaryFixture } from '@/testing/authoring-course-handlers';
import { server } from '@/testing/server';
import { renderWithProviders } from '@/testing/test-utils';

const at = (daysAgo: number, hour = 10, minute = 20) => { const date = new Date(); date.setDate(date.getDate() - daysAgo); date.setHours(hour, minute, 0, 0); return date.toISOString(); };
const markdown = '## Sobre o curso\n\nTexto com **negrito** e [site](https://example.com).\n\n- item um\n- item dois\n\n<script>window.hacked = true</script><b>html cru</b>\n\n![pixel](https://example.com/x.png)';
const renderAt = (path: string, handlers: Parameters<typeof server.use>) => {
  server.use(...handlers, http.get(`${env.API_URL}/api/v1/staff-sessions/current`, () => HttpResponse.json({
    accountId: '0198dfac-674a-7000-8000-000000000002', name: 'Professor B', roles: ['professor'], permissions: ['autoria.ler', 'autoria.editar'], csrfToken: 'csrf',
  })));
  const router = createMemoryRouter([{ path: '/', loader: loadStaffSession, element: <AdminLayoutRoute title="Autoria" serviceName="admin-spa" />,
    children: [{ path: 'autoria', element: <AuthoringCoursesRoute /> }, { path: 'autoria/:courseId', element: <AuthoringCourseRoute /> }, { path: 'autoria/:courseId/versoes/:versionNumber', element: <AuthoringVersionRoute /> }],
  }], { initialEntries: [path] });
  renderWithProviders(<RouterProvider router={router} />);
};
const courseHandler = (course: object) => http.get(`${env.API_URL}/api/v1/courses/:courseId`, () => HttpResponse.json({ ...authoringCourseFixture, ...course }));

describe('authoring Figma text and header', () => {
  afterEach(cleanup);

  it('renders the course description as safe markdown without raw HTML or images', async () => {
    renderAt(`/autoria/${authoringCourseFixture.courseId}`, [courseHandler({ description: markdown })]);
    expect(await screen.findByText('negrito')).toContainHTML('<strong>negrito</strong>');
    expect(screen.getByText('Sobre o curso').tagName).toBe('STRONG');
    expect(screen.queryByRole('heading', { name: 'Sobre o curso' })).not.toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('.NET do zero à API');
    expect(screen.getAllByRole('listitem').map((item) => item.textContent)).toEqual(expect.arrayContaining(['item um', 'item dois']));
    expect(screen.getByRole('link', { name: 'site' })).toHaveAttribute('rel', 'noopener noreferrer');
    expect(document.querySelector('script, img[alt=pixel], b')).toBeNull();
    expect(screen.queryByText(/##|\*\*/)).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Editar dados' })).toBeInTheDocument();
  });

  it('shows breadcrumb as Autoria › Cursos › Título', async () => {
    renderAt(`/autoria/${authoringCourseFixture.courseId}`, [courseHandler({})]);
    const nav = await screen.findByRole('navigation', { name: 'Caminho' });
    expect(nav).toHaveTextContent('Autoria ›Cursos› .NET do zero à API');
    expect(within(nav).getByRole('link', { name: 'Cursos' })).toHaveAttribute('href', '/autoria');
  });

  it('shows course metadata with relative time and keeps the machine-readable time', async () => {
    const lastEditedAt = at(0);
    renderAt(`/autoria/${authoringCourseFixture.courseId}`, [courseHandler({ lastEditedAt, createdBy: { name: 'Rafael Souza' }, lastEditedBy: { name: 'Marina Alves' } })]);
    const metadata = await screen.findByText(/Criado por Rafael Souza/);
    expect(metadata).toHaveTextContent('Criado por Rafael Souza · Editado por Marina Alves hoje, 10:20');
    expect(within(metadata).getByText('hoje, 10:20')).toHaveAttribute('datetime', lastEditedAt);
  });

  it('keeps an absolute date with "em" for old edits', async () => {
    renderAt(`/autoria/${authoringCourseFixture.courseId}`, [courseHandler({ lastEditedAt: new Date(2026, 8, 29, 9, 5).toISOString() })]);
    expect(await screen.findByText(/Criado por/)).toHaveTextContent('Editado por Professor B em 29/09/2026 · 09:05');
  });

  it('lists relative edit times and puts Sem nível in the Estado column with the alert icon', async () => {
    const summary = { ...authoringCourseSummaryFixture, status: 'published', currentVersion: 1, currentLevel: null, lastEditedAt: at(0), lastEditedBy: { name: 'Marina Alves' } };
    const second = { ...summary, courseId: '0198dfac-674a-7000-8000-000000000002', title: 'Introdução a APIs', currentLevel: 'beginner', lastEditedAt: at(1) };
    renderAt('/autoria', [http.get(`${env.API_URL}/api/v1/courses`, () => HttpResponse.json({ data: [summary, second], pagination: { page: 1, size: 20, total: 2, totalPages: 1 } }))]);
    const row = (await screen.findByText('.NET do zero à API')).closest('tr')!;
    const cells = within(row).getAllByRole('cell');
    expect(cells[0]).not.toHaveTextContent('Sem nível');
    expect(cells[1]).toHaveTextContent('Sem nível'); expect(cells[1]!.querySelector('svg')).not.toBeNull();
    expect(cells[2]).toHaveTextContent('Marina Alves · hoje, 10:20');
    expect(within(screen.getByText('Introdução a APIs').closest('tr')!).getAllByRole('cell')[1]).not.toHaveTextContent('Sem nível');
    expect(within(screen.getByText('Introdução a APIs').closest('tr')!).getAllByRole('cell')[2]).toHaveTextContent('Marina Alves · ontem, 10:20');
  });

  it('renders the version description as markdown and the publisher as "Publicada por X · data"', async () => {
    const version = { courseId: authoringCourseFixture.courseId, versionNumber: 3, title: 'Título publicado', description: markdown, publishedAt: new Date(2026, 9, 3, 14, 10).toISOString(), publishedBy: { name: 'Marina Alves' }, versionNote: 'Nota', current: true, level: null, prerequisite: { text: null, recommendedCourses: [] }, modules: [] };
    renderAt(`/autoria/${authoringCourseFixture.courseId}/versoes/3`, [http.get(`${env.API_URL}/api/v1/courses/:courseId/versions/:versionNumber`, () => HttpResponse.json(version))]);
    expect(await screen.findByText(/Publicada por Marina Alves/)).toHaveTextContent('Publicada por Marina Alves · 03/10/2026 · 14:10');
    expect(screen.getByText('negrito')).toContainHTML('<strong>negrito</strong>');
    expect(screen.queryByText(/##/)).not.toBeInTheDocument();
    expect(document.querySelector('script')).toBeNull();
  });

  it('prerequisite field has the Figma placeholder and the empty recommendation message', async () => {
    renderAt(`/autoria/${authoringCourseFixture.courseId}`, [courseHandler({})]);
    expect(await screen.findByPlaceholderText('O que a pessoa deveria saber antes...')).toBeInTheDocument();
    expect(screen.getByText('Nenhum curso recomendado.')).toHaveClass('course-recommended-empty');
  });
});
