import { cleanup, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { afterEach, describe, expect, it } from 'vitest';

import { AdminLayoutRoute } from '@/app/routes/admin-layout-route';
import { AuthoringCourseRoute } from '@/app/routes/authoring-course-route';
import { loadStaffSession } from '@/app/routes/staff-session-loader';
import { env } from '@/config/env';
import { courseSchema, type Course, type CourseSummary } from '@/features/course-authoring/types/course';
import { authoringCourseFixture } from '@/testing/authoring-course-handlers';
import { createCoursePrerequisiteHandlers } from '@/testing/authoring-prerequisite-handlers';
import { server } from '@/testing/server';
import { renderWithProviders } from '@/testing/test-utils';

const candidates: CourseSummary[] = Array.from({ length: 6 }, (_, index) => ({
  courseId: `0198dfac-674a-7000-8000-00000000002${index}`, title: index === 0 ? 'Fundamentos de C#' : `Fundamentos ${index + 1}`,
  status: 'published', currentVersion: 1, currentLevel: null, hasUnpublishedChanges: false,
  lastEditedAt: authoringCourseFixture.lastEditedAt, lastEditedBy: { name: 'Professor' },
}));
const base = (): Course => courseSchema.parse({ ...authoringCourseFixture, status: 'published', currentVersion: 1 });
const renderPrerequisite = (initial = base(), permissions = ['autoria.ler', 'autoria.editar']) => {
  const boundary = createCoursePrerequisiteHandlers(initial, [...candidates,
    { ...candidates[0]!, courseId: initial.courseId, title: 'Fundamentos próprio' },
    { ...candidates[0]!, courseId: '0198dfac-674a-7000-8000-000000000030', title: 'Fundamentos nunca publicado', status: 'draft', currentVersion: null },
  ]);
  server.use(...boundary.handlers, http.get(`${env.API_URL}/api/v1/staff-sessions/current`, () => HttpResponse.json({
    accountId: '0198dfac-674a-7000-8000-000000000002', name: 'Professor', roles: ['professor'], permissions, csrfToken: 'prerequisite-csrf',
  })));
  const router = createMemoryRouter([{ path: '/', loader: loadStaffSession, element: <AdminLayoutRoute title="Autoria" serviceName="admin-spa" />,
    children: [{ path: 'autoria/:courseId', element: <AuthoringCourseRoute /> }],
  }], { initialEntries: [`/autoria/${initial.courseId}`] });
  renderWithProviders(<RouterProvider router={router} />); return boundary;
};
const openSearch = async (user: ReturnType<typeof userEvent.setup>, title = 'fundaméntos') => {
  await user.click(await screen.findByRole('button', { name: '+ Adicionar curso' }));
  await user.type(screen.getByRole('searchbox', { name: 'Buscar por título' }), title);
  return screen.getByRole('dialog');
};

describe('authoring prerequisite', () => {
  afterEach(cleanup);

  it('searches published courses after two characters, excludes self and selections and saves one intent', async () => {
    const user = userEvent.setup(); const boundary = renderPrerequisite();
    await user.click(await screen.findByRole('button', { name: '+ Adicionar curso' }));
    const search = screen.getByRole('searchbox', { name: 'Buscar por título' });
    await user.type(search, 'f'); expect(boundary.searches).toHaveLength(0);
    await user.clear(search); await user.type(search, 'fundaméntos');
    const dialog = screen.getByRole('dialog');
    await user.click(await within(dialog).findByRole('button', { name: 'Adicionar Fundamentos de C#' }));
    expect(within(dialog).queryByText('Fundamentos próprio')).not.toBeInTheDocument();
    expect(within(dialog).queryByText('Fundamentos nunca publicado')).not.toBeInTheDocument();
    expect(within(dialog).queryByRole('button', { name: 'Adicionar Fundamentos de C#' })).not.toBeInTheDocument();
    await user.click(within(dialog).getByRole('button', { name: 'Adicionar Fundamentos 2' }));
    expect(boundary.writes).toHaveLength(0); await user.click(within(dialog).getByRole('button', { name: 'Concluir' }));
    await user.type(screen.getByRole('textbox', { name: 'O que a pessoa deveria saber antes (opcional)' }), 'Lógica e Git.');
    await user.click(screen.getByRole('button', { name: 'Salvar pré-requisito' })); await screen.findByText('Pré-requisito salvo');
    expect(boundary.writes).toHaveLength(1);
    expect(boundary.writes[0]?.body).toEqual({ prerequisiteText: 'Lógica e Git.', recommendedCourseIds: candidates.slice(0, 2).map((item) => item.courseId) });
    expect(boundary.writes[0]?.key).toBeTruthy(); expect(boundary.writes[0]?.csrf).toBe('prerequisite-csrf');
    expect(boundary.searches.some((params) => params.get('status') === 'published' && params.get('title') === 'fundaméntos')).toBe(true);
    expect(screen.getByText(/alterações não publicadas/)).toBeInTheDocument(); expect(screen.getByText(/Editado por Professor confirmado/)).toBeInTheDocument();
  });

  it('limits to five, reorders by keyboard with focus kept on the moved item, removes and undoes', async () => {
    const user = userEvent.setup(); const boundary = renderPrerequisite(); const dialog = await openSearch(user);
    for (const candidate of candidates.slice(0, 5)) await user.click(await within(dialog).findByRole('button', { name: `Adicionar ${candidate.title}` }));
    expect(within(dialog).getByRole('button', { name: 'Adicionar Fundamentos 6' })).toBeDisabled();
    await user.click(within(dialog).getByRole('button', { name: 'Concluir' }));
    expect(screen.getByRole('button', { name: '+ Adicionar curso' })).toBeDisabled();
    expect(screen.getByText('Você já escolheu 5 cursos. Remova um para adicionar outro.')).toBeInTheDocument();
    screen.getByRole('button', { name: 'Mover Fundamentos 2 para cima' }).focus(); await user.keyboard('{Enter}');
    const list = screen.getByRole('list', { name: 'Cursos recomendados (até 5)' });
    await waitFor(() => expect(within(list).getAllByRole('listitem')[0]).toHaveFocus());
    expect(within(list).getAllByRole('listitem')[0]).toHaveTextContent('1. Fundamentos 2');
    await user.click(screen.getByRole('button', { name: 'Remover Fundamentos 2' })); expect(screen.getByRole('button', { name: '+ Adicionar curso' })).toBeEnabled();
    await user.click(screen.getByRole('button', { name: 'Desfazer' })); expect(screen.getByText('Nenhum curso recomendado.')).toBeInTheDocument();
    expect(boundary.writes).toHaveLength(0);
  });

  it('reader sees text and ordered current titles without editing controls', async () => {
    renderPrerequisite({ ...base(), prerequisite: { text: 'Noções de Git', recommendedCourses: candidates.slice(0, 2).map(({ courseId, title }) => ({ courseId, title })) } }, ['autoria.ler']);
    await screen.findByText('Noções de Git');
    expect(screen.getByText('Fundamentos de C#')).toBeInTheDocument();
    expect(screen.queryByRole('textbox')).not.toBeInTheDocument(); expect(screen.queryByRole('button', { name: '+ Adicionar curso' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Mover|Remover|Salvar pré-requisito|Desfazer/ })).not.toBeInTheDocument();
  });

  it('displays indexed recommendation error at the field and preserves text and choices', async () => {
    const user = userEvent.setup(); const boundary = renderPrerequisite(); const dialog = await openSearch(user);
    await user.click(await within(dialog).findByRole('button', { name: 'Adicionar Fundamentos de C#' }));
    await user.click(within(dialog).getByRole('button', { name: 'Concluir' }));
    const text = screen.getByRole('textbox', { name: 'O que a pessoa deveria saber antes (opcional)' }); await user.type(text, 'Git');
    boundary.failWith({ code: 'RECOMMENDED_COURSE_INVALID', status: 422, errors: { 'recommendedCourseIds[0]': ['Invalid recommendation.'] } });
    await user.click(screen.getByRole('button', { name: 'Salvar pré-requisito' }));
    expect(await screen.findByText(/Não foi possível salvar: um curso recomendado/)).toHaveAttribute('role', 'alert');
    expect(screen.getByText('Este curso não está mais disponível para recomendação.')).toBeInTheDocument(); expect(text).toHaveValue('Git');
    const item = screen.getByRole('list', { name: 'Cursos recomendados (até 5)' }).querySelector('li'); await waitFor(() => expect(item).toHaveFocus());
  });

  it('service retry preserves the intent key and csrf and changed intent gets another key', async () => {
    const user = userEvent.setup(); const boundary = renderPrerequisite();
    const text = await screen.findByRole('textbox', { name: 'O que a pessoa deveria saber antes (opcional)' }); await user.type(text, 'Git');
    boundary.failWith({ code: 'LEARNING_UNAVAILABLE', status: 502 }); await user.click(screen.getByRole('button', { name: 'Salvar pré-requisito' }));
    await screen.findByText('Não foi possível salvar. Tente de novo.'); expect(text).toHaveValue('Git');
    boundary.failWith(); await user.click(screen.getByRole('button', { name: 'Tentar de novo' })); await screen.findByText('Pré-requisito salvo');
    expect(boundary.writes[0]?.key).toBe(boundary.writes[1]?.key); expect(boundary.writes[0]?.body).toEqual(boundary.writes[1]?.body);
    await user.type(screen.getByRole('textbox', { name: 'O que a pessoa deveria saber antes (opcional)' }), ' e C#');
    await user.click(screen.getByRole('button', { name: 'Salvar pré-requisito' })); await waitFor(() => expect(boundary.writes).toHaveLength(3));
    expect(boundary.writes[2]?.key).not.toBe(boundary.writes[0]?.key);
  });

  it('counts text, enforces the limit and clearing sends null and an empty list', async () => {
    const user = userEvent.setup(); const boundary = renderPrerequisite({ ...base(), prerequisite: { text: 'Git', recommendedCourses: [{ courseId: candidates[0]!.courseId, title: candidates[0]!.title }] } });
    const text = await screen.findByRole('textbox', { name: 'O que a pessoa deveria saber antes (opcional)' }); expect(screen.getByText('3/1000')).toBeInTheDocument();
    expect(text).toHaveAttribute('maxlength', '1000'); await user.clear(text);
    await user.click(screen.getByRole('button', { name: 'Remover Fundamentos de C#' }));
    await user.click(screen.getByRole('button', { name: 'Salvar pré-requisito' })); await screen.findByText('Pré-requisito salvo');
    expect(boundary.writes[0]?.body).toEqual({ prerequisiteText: null, recommendedCourseIds: [] }); expect(screen.getByText('0/1000')).toBeInTheDocument();
  });

  it('field invalid focuses the textarea, announces an error and keeps the input', async () => {
    const user = userEvent.setup(); const boundary = renderPrerequisite();
    const text = await screen.findByRole('textbox', { name: 'O que a pessoa deveria saber antes (opcional)' }); await user.type(text, 'Git');
    boundary.failWith({ code: 'FIELD_INVALID', status: 422 }); await user.click(screen.getByRole('button', { name: 'Salvar pré-requisito' }));
    expect(await screen.findByText('O pré-requisito deve ter até 1 000 caracteres.')).toHaveAttribute('role', 'alert');
    await waitFor(() => expect(text).toHaveFocus()); expect(text).toHaveAttribute('aria-invalid', 'true'); expect(text).toHaveValue('Git');
  });

  it('saving the level preserves unsaved prerequisite edits and the reordered list is sent in order', async () => {
    const user = userEvent.setup(); const boundary = renderPrerequisite({ ...base(), prerequisite: { text: null, recommendedCourses: candidates.slice(0, 2).map(({ courseId, title }) => ({ courseId, title })) } });
    const text = await screen.findByRole('textbox', { name: 'O que a pessoa deveria saber antes (opcional)' }); await user.type(text, 'Unsaved Git');
    await user.click(screen.getByRole('radio', { name: 'Avançado' })); await screen.findByText('Nível salvo'); expect(text).toHaveValue('Unsaved Git');
    await user.click(screen.getByRole('button', { name: 'Mover Fundamentos 2 para cima' }));
    await user.click(screen.getByRole('button', { name: 'Salvar pré-requisito' })); await screen.findByText('Pré-requisito salvo');
    expect(boundary.writes[1]?.body).toEqual({ prerequisiteText: 'Unsaved Git', recommendedCourseIds: candidates.slice(0, 2).reverse().map((item) => item.courseId) });
  });
});
