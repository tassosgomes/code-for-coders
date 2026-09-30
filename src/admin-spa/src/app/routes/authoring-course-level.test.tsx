import { readFileSync } from 'node:fs';
import { cleanup, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { afterEach, describe, expect, it } from 'vitest';
import { parse } from 'yaml';
import { z } from 'zod';

import { AdminLayoutRoute } from '@/app/routes/admin-layout-route';
import { AuthoringCourseRoute } from '@/app/routes/authoring-course-route';
import { AuthoringCoursesRoute } from '@/app/routes/authoring-courses-route';
import { loadStaffSession } from '@/app/routes/staff-session-loader';
import { env } from '@/config/env';
import { courseSchema, coursePageSchema, type Course } from '@/features/course-authoring/types/course';
import { authoringCourseFixture } from '@/testing/authoring-course-handlers';
import { createCourseLevelHandlers } from '@/testing/authoring-course-level-handlers';
import { server } from '@/testing/server';
import { renderWithProviders } from '@/testing/test-utils';

const publishedCourse = (): Course => courseSchema.parse({ ...authoringCourseFixture, status: 'published', currentVersion: 1,
  modules: [{ moduleId: '0198dfac-674a-7000-8000-000000000010', title: 'Módulo', position: 1,
    lessons: [{ lessonId: '0198dfac-674a-7000-8000-000000000011', title: 'Aula', position: 1, video: { videoId: '0198dfac-674a-7000-8000-000000000012' } }] }],
});
const renderLevel = (initial = publishedCourse(), permissions = ['autoria.ler', 'autoria.editar'], list = false) => {
  const boundary = createCourseLevelHandlers(initial);
  server.use(...boundary.handlers, http.get(`${env.API_URL}/api/v1/staff-sessions/current`, () => HttpResponse.json({
    accountId: '0198dfac-674a-7000-8000-000000000002', name: 'Professor', roles: ['professor'], permissions, csrfToken: 'level-csrf',
  })));
  const router = createMemoryRouter([{ path: '/', loader: loadStaffSession, element: <AdminLayoutRoute title="Autoria" serviceName="admin-spa" />,
    children: [{ path: 'autoria/:courseId', element: <AuthoringCourseRoute /> }, { path: 'autoria', element: <AuthoringCoursesRoute /> }],
  }], { initialEntries: [list ? '/autoria' : `/autoria/${initial.courseId}`] });
  renderWithProviders(<RouterProvider router={router} />);
  return boundary;
};

describe('authoring course level', () => {
  afterEach(cleanup);

  it('saves each level and clears it, using confirmed drafts, intent keys and csrf', async () => {
    const user = userEvent.setup(); const boundary = renderLevel();
    await screen.findByRole('group', { name: 'Nível do curso' });
    for (const label of ['Iniciante', 'Intermediário', 'Avançado', 'Sem nível']) {
      await user.click(screen.getByRole('radio', { name: label }));
      await screen.findByText('Nível salvo');
      await waitFor(() => expect(screen.getByRole('radio', { name: label })).toHaveFocus());
    }
    expect(boundary.requests.map((request) => request.body)).toEqual([{ level: 'beginner' }, { level: 'intermediate' }, { level: 'advanced' }, { level: null }]);
    expect(new Set(boundary.requests.map((request) => request.key)).size).toBe(4);
    expect(boundary.requests.every((request) => request.key && request.csrf === 'level-csrf')).toBe(true);
    expect(screen.getByText(/Editado por Professor confirmado/)).toBeInTheDocument();
    expect(screen.queryByText(/alterações não publicadas/)).not.toBeInTheDocument();
  });

  it('reader sees the level and notice without mutation controls', async () => {
    renderLevel({ ...publishedCourse(), level: 'beginner' }, ['autoria.ler']);
    await screen.findByRole('heading', { name: 'Para quem é este curso' });
    expect(screen.getByText('Iniciante')).toBeInTheDocument();
    expect(screen.getByText(/Peça a quem edita/)).toBeInTheDocument();
    expect(screen.queryByRole('radio')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Escolher nível' })).not.toBeInTheDocument();
  });

  it('announces current version without level, focuses the field and explains draft only level', async () => {
    const user = userEvent.setup(); renderLevel();
    await user.click(await screen.findByRole('button', { name: 'Escolher nível' }));
    await waitFor(() => expect(screen.getByRole('radio', { name: 'Sem nível' })).toHaveFocus());
    await user.click(screen.getByRole('radio', { name: 'Iniciante' }));
    const notice = await screen.findByText(/O nível Iniciante só vale depois de publicar/);
    expect(notice.closest('[role="status"]')).toHaveAttribute('aria-live', 'polite');
    expect(screen.getByText(/alterações não publicadas/)).toBeInTheDocument();
  });

  it('publication repeats missing level notice with publishing enabled and shortcut returns focus', async () => {
    const user = userEvent.setup(); renderLevel({ ...publishedCourse(), hasUnpublishedChanges: true });
    await user.click(await screen.findByRole('button', { name: 'Publicar nova versão' }));
    const dialog = screen.getByRole('dialog');
    expect(within(dialog).getByText(/Sem nível, este curso não pode entrar na vitrine/)).toBeInTheDocument();
    expect(within(dialog).getByRole('button', { name: 'Publicar versão 2' })).toBeEnabled();
    await user.click(within(dialog).getByRole('button', { name: 'Escolher nível' }));
    await waitFor(() => expect(screen.getByRole('radio', { name: 'Sem nível' })).toHaveFocus());
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('published list shows missing current level even when draft has a level', async () => {
    renderLevel({ ...publishedCourse(), level: 'advanced' }, undefined, true);
    const row = (await screen.findByText(authoringCourseFixture.title)).closest('tr');
    expect(row).toHaveTextContent('Sem nível');
  });

  it('a level on a never published draft explains publication without claiming a current version', async () => {
    renderLevel({ ...publishedCourse(), currentVersion: null, status: 'draft', level: 'beginner' });
    expect(await screen.findByText(/O nível Iniciante só vale depois de publicar. Este curso ainda não foi publicado./)).toBeInTheDocument();
    expect(screen.queryByText(/A versão vigente está sem nível/)).not.toBeInTheDocument();
  });

  it('field invalid is displayed at the group and retry preserves the selection and intent', async () => {
    const user = userEvent.setup(); const boundary = renderLevel(); boundary.failWith({ code: 'FIELD_INVALID', status: 422 });
    await user.click(await screen.findByRole('radio', { name: 'Avançado' }));
    expect(await screen.findByText('Escolha um dos níveis.')).toHaveAttribute('role', 'alert');
    expect(screen.getByRole('group', { name: 'Nível do curso' })).toHaveAttribute('aria-invalid', 'true');
    expect(screen.getByRole('radio', { name: 'Avançado' })).toBeChecked();
    boundary.failWith(); await user.click(screen.getByRole('button', { name: 'Tentar de novo' })); await screen.findByText('Nível salvo');
    expect(boundary.requests[0]?.key).toBe(boundary.requests[1]?.key);
    expect(boundary.requests[0]?.body).toEqual(boundary.requests[1]?.body);
  });

  it('service failure keeps the level and reuses the intent on retry', async () => {
    const user = userEvent.setup(); const boundary = renderLevel(); boundary.failWith({ code: 'LEARNING_UNAVAILABLE', status: 502 });
    await user.click(await screen.findByRole('radio', { name: 'Intermediário' }));
    await screen.findByText('Não foi possível salvar. Tente de novo.');
    expect(screen.getByRole('radio', { name: 'Intermediário' })).toBeChecked();
    boundary.failWith(); await user.click(screen.getByRole('button', { name: 'Tentar de novo' })); await screen.findByText('Nível salvo');
    expect(boundary.requests[0]?.key).toBe(boundary.requests[1]?.key);
  });

  it('strict schemas parse actual getCourse and listCourses OpenAPI 1.1.0 examples', () => {
    const document: unknown = parse(readFileSync('../../tasks/prd-nivel-prerequisito-curso/api-contract.yaml', 'utf8'));
    const contract = z.object({ info: z.object({ version: z.literal('1.1.0') }),
      components: z.object({ examples: z.object({ CourseWithLevel: z.object({ value: z.unknown() }) }) }),
      paths: z.record(z.string(), z.unknown()),
    }).parse(document);
    expect(courseSchema.parse(contract.components.examples.CourseWithLevel.value).level).toBe('advanced');
    const operation = z.object({ get: z.object({ responses: z.object({ '200': z.object({ content: z.object({ 'application/json': z.object({ examples: z.record(z.string(), z.object({ value: z.unknown() })) }) }) }) }) }) }).parse(contract.paths['/courses']);
    for (const example of Object.values(operation.get.responses['200'].content['application/json'].examples)) expect(coursePageSchema.parse(example.value).data).toHaveLength(2);
    expect(() => courseSchema.parse({ ...authoringCourseFixture, unknownField: true })).toThrow();
  });
});
