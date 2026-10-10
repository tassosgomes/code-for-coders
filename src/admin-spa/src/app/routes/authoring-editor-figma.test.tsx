import { cleanup, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { afterEach, describe, expect, it } from 'vitest';

import { AdminLayoutRoute } from '@/app/routes/admin-layout-route';
import { AuthoringCourseRoute } from '@/app/routes/authoring-course-route';
import { loadStaffSession } from '@/app/routes/staff-session-loader';
import { env } from '@/config/env';
import { authoringCourseFixture } from '@/testing/authoring-course-handlers';
import { server } from '@/testing/server';
import { renderWithProviders } from '@/testing/test-utils';

const id = (n: number) => `0198dfac-674a-7000-8000-0000000001${String(n).padStart(2, '0')}`;
const video = (n: number) => ({ videoId: id(n), title: `Vídeo ${n}` });
const lesson = (n: number, withVideo = true) => ({ lessonId: id(n), title: `Aula ${n}`, position: n, video: withVideo ? video(n) : null });
const lessonIn = (position: number, n: number, withVideo = true) => ({ ...lesson(n, withVideo), position });
const render = (course: Record<string, unknown>) => {
  server.use(http.get(`${env.API_URL}/api/v1/courses/:courseId`, () => HttpResponse.json({ ...authoringCourseFixture, ...course })),
    http.get(`${env.API_URL}/api/v1/staff-sessions/current`, () => HttpResponse.json({ accountId: id(90), name: 'Professor', roles: ['professor'], permissions: ['autoria.ler', 'autoria.editar', 'midia.enviar'], csrfToken: 'figma-csrf' })));
  const router = createMemoryRouter([{ path: '/', loader: loadStaffSession, element: <AdminLayoutRoute title="Autoria" serviceName="admin-spa" />,
    children: [{ path: 'autoria/:courseId', element: <AuthoringCourseRoute /> }] }], { initialEntries: [`/autoria/${authoringCourseFixture.courseId}`] });
  renderWithProviders(<RouterProvider router={router} />);
};
const published = { status: 'published', currentVersion: 1, level: 'beginner', currentLevel: 'beginner', draftRevision: 3 };
const oneModule = [{ moduleId: id(1), title: 'Fundamentos', position: 1, lessons: [lessonIn(1, 11)] }];
afterEach(cleanup);

describe('authoring editor against Figma', () => {
  it('uses singular and plural counts, numbers without a dot and a single move item', async () => {
    const user = userEvent.setup(); render({ modules: oneModule });
    expect(await screen.findByRole('heading', { name: '1 Fundamentos' })).toBeInTheDocument();
    expect(screen.getByRole('region', { name: 'Fundamentos' })).toHaveTextContent('1 aula');
    expect(screen.getByRole('region', { name: 'Fundamentos' })).not.toHaveTextContent('1 aulas');
    expect(screen.getByRole('heading', { name: '1 Aula 11' })).toBeInTheDocument();
    await user.click(screen.getByLabelText('Ações de Aula 11'));
    const moves = screen.getAllByText(/Mover para outro módulo/);
    expect(moves).toHaveLength(1); expect(moves[0]?.closest('button')).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Remover aula…' })).toHaveClass('course-action-danger');
    expect(screen.getByText('Primeiro item: não pode subir.')).toBeInTheDocument();
  });

  it('opens one move submenu listing the other modules with cancel', async () => {
    const user = userEvent.setup();
    render({ modules: [...oneModule, { moduleId: id(2), title: 'Coleções', position: 2, lessons: [] }] });
    await user.click(await screen.findByLabelText('Ações de Aula 11'));
    expect(screen.queryByRole('button', { name: 'Mover para Coleções' })).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /Mover para outro módulo/ }));
    expect(screen.getByRole('button', { name: 'Mover para Coleções' })).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Cancelar' }));
    expect(screen.queryByRole('button', { name: 'Mover para Coleções' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Mover para outro módulo/ })).toBeInTheDocument();
  });

  it('shows the pendency strip that leads to the first pending item', async () => {
    const user = userEvent.setup();
    render({ modules: [{ moduleId: id(1), title: 'Fundamentos', position: 1, lessons: [lessonIn(1, 11, false), lessonIn(2, 12, false)] }, { moduleId: id(2), title: 'Coleções', position: 2, lessons: [] }] });
    expect(await screen.findByText('3 pendências')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Ver pendências' }));
    await waitFor(() => expect(screen.getByRole('article', { name: 'Aula 11' })).toHaveFocus());
  });

  it('warns that changes stay in the draft when a published course has unpublished changes', async () => {
    render({ ...published, hasUnpublishedChanges: true, modules: oneModule });
    expect(await screen.findByText('As alterações ficam no rascunho até você publicar uma nova versão.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Publicar nova versão' })).toBeInTheDocument();
  });

  it('keeps the publish button and tells the draft equals the current version when nothing changed', async () => {
    const user = userEvent.setup(); render({ ...published, hasUnpublishedChanges: false, modules: oneModule });
    expect(await screen.findByText('O rascunho está igual à versão vigente.')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Publicar nova versão' }));
    const dialog = screen.getByRole('dialog');
    expect(within(dialog).getByRole('heading', { name: 'Publicar nova versão' })).toBeInTheDocument();
    expect(dialog).toHaveTextContent('A versão 2 ficará vigente para todos; a versão 1 continuará no histórico.');
  });

  it('describes the first publication with video lesson counts, level, prerequisite and note label', async () => {
    const user = userEvent.setup();
    render({ draftRevision: 6, prerequisite: { text: 'Lógica', recommendedCourses: [{ courseId: id(70), title: 'Base' }, { courseId: id(71), title: 'Mais' }] }, modules: [{ moduleId: id(1), title: 'Fundamentos', position: 1, lessons: [lessonIn(1, 11), lessonIn(2, 12)] }] });
    await user.click(await screen.findByRole('button', { name: 'Publicar curso' }));
    const dialog = screen.getByRole('dialog');
    expect(within(dialog).getByRole('heading', { name: 'Publicar curso' })).toBeInTheDocument();
    expect(dialog).toHaveTextContent('Confira o que será publicado. Esta será a versão 1.');
    expect(dialog).toHaveTextContent('1 módulo · 2 aulas com vídeo');
    expect(dialog).not.toHaveTextContent('Revisão');
    expect(dialog).toHaveTextContent('Pré-requisito: texto + 2 cursos recomendados');
    expect(within(dialog).getByRole('list', { name: 'Currículo a publicar' })).toHaveTextContent('1 Fundamentos1 Aula 11');
    expect(within(dialog).getByLabelText('Nota de versão (opcional)')).toBeInTheDocument();
    expect(within(dialog).getByRole('button', { name: 'Publicar versão 1' })).toBeInTheDocument();
  });

  it('empty curriculum offers a primary add module button and a text link to delete the course', async () => {
    render({ modules: [] });
    const add = await screen.findByRole('button', { name: '+ Adicionar módulo' });
    expect(add).toHaveClass('primary-button'); expect(screen.getAllByRole('button', { name: '+ Adicionar módulo' })).toHaveLength(1);
    expect(screen.getByRole('button', { name: 'Excluir curso' })).toHaveClass('text-button');
  });
});
