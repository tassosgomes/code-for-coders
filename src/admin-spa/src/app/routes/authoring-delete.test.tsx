import { cleanup, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { afterEach, describe, expect, it } from 'vitest';

import { AdminLayoutRoute } from '@/app/routes/admin-layout-route';
import { AuthoringCourseRoute } from '@/app/routes/authoring-course-route';
import { AuthoringCoursesRoute } from '@/app/routes/authoring-courses-route';
import { loadStaffSession } from '@/app/routes/staff-session-loader';
import { authoringCourseFixture } from '@/testing/authoring-course-handlers';
import { createDeletionBoundary } from '@/testing/authoring-delete-handlers';
import { server } from '@/testing/server';
import { renderWithProviders } from '@/testing/test-utils';

const editorPath = `/autoria/${authoringCourseFixture.courseId}`;
const renderDeletion = (boundary: ReturnType<typeof createDeletionBoundary>, path = editorPath) => {
  server.use(...boundary.handlers);
  const router = createMemoryRouter([{
    path: '/', loader: loadStaffSession, element: <AdminLayoutRoute title="Autoria" serviceName="admin-spa" />,
    children: [{ path: 'autoria', element: <AuthoringCoursesRoute /> }, { path: 'autoria/:courseId', element: <AuthoringCourseRoute /> }],
  }], { initialEntries: [path] });
  renderWithProviders(<RouterProvider router={router} />); return router;
};

describe('authoring deletion', () => {
  afterEach(cleanup);

  it('cancel preserves the draft and sends no deletion', async () => {
    const user = userEvent.setup(); const boundary = createDeletionBoundary(); renderDeletion(boundary);
    await user.click(await screen.findByRole('button', { name: 'Excluir curso' }));
    const dialog = screen.getByRole('alertdialog'); expect(dialog).toHaveTextContent('Esta ação não pode ser desfeita.');
    expect(within(dialog).getByRole('button', { name: 'Cancelar' })).toHaveFocus();
    await user.click(within(dialog).getByRole('button', { name: 'Cancelar' }));
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument(); expect(boundary.writes).toHaveLength(0);
    expect(screen.getByRole('heading', { name: authoringCourseFixture.title })).toBeInTheDocument();
  });

  it('confirmed editor deletion returns to the refreshed empty list and old link becomes not found', async () => {
    const user = userEvent.setup(); const boundary = createDeletionBoundary(); const router = renderDeletion(boundary);
    await user.click(await screen.findByRole('button', { name: 'Excluir curso' }));
    await user.click(within(screen.getByRole('alertdialog')).getByRole('button', { name: 'Excluir curso' }));
    await screen.findByRole('heading', { name: 'Nenhum curso ainda.' }); expect(screen.getByText('Curso excluído')).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/autoria'); expect(boundary.writes[0]?.key).toBeTruthy(); expect(boundary.writes[0]?.csrf).toBe('delete-csrf');
    await router.navigate(editorPath); await screen.findByRole('heading', { name: 'Curso não encontrado' });
  });

  it('list deletion removes the row only after confirmation', async () => {
    const user = userEvent.setup(); const boundary = createDeletionBoundary(); renderDeletion(boundary, '/autoria');
    await user.click(await screen.findByRole('button', { name: `Ações de ${authoringCourseFixture.title}` }));
    await user.click(await screen.findByRole('button', { name: 'Excluir curso' })); expect(screen.getByRole('link', { name: `Abrir ${authoringCourseFixture.title}` })).toBeInTheDocument();
    await user.click(within(screen.getByRole('alertdialog')).getByRole('button', { name: 'Excluir curso' }));
    await screen.findByRole('heading', { name: 'Nenhum curso ainda.' }); expect(boundary.writes).toHaveLength(1);
  });

  it('failure keeps confirmation and explicit retry uses the same intention', async () => {
    const user = userEvent.setup(); const boundary = createDeletionBoundary(); boundary.setMode('unavailable'); renderDeletion(boundary);
    await user.click(await screen.findByRole('button', { name: 'Excluir curso' }));
    await user.click(within(screen.getByRole('alertdialog')).getByRole('button', { name: 'Excluir curso' })); await screen.findByText('Não foi possível excluir o curso. Tente novamente.');
    expect(screen.getByRole('heading', { name: authoringCourseFixture.title })).toBeInTheDocument();
    boundary.setMode('success'); await user.click(within(screen.getByRole('alertdialog')).getByRole('button', { name: 'Excluir curso' }));
    await screen.findByText('Curso excluído'); expect(boundary.writes).toHaveLength(2); expect(boundary.writes[0]?.key).toBe(boundary.writes[1]?.key);
  });

  it('concurrent publication closes confirmation reloads the course and explains the refusal', async () => {
    const user = userEvent.setup(); const boundary = createDeletionBoundary(); boundary.setMode('published'); renderDeletion(boundary);
    await user.click(await screen.findByRole('button', { name: 'Excluir curso' })); await user.click(within(screen.getByRole('alertdialog')).getByRole('button', { name: 'Excluir curso' }));
    await screen.findByText('Este curso já foi publicado e não pode ser excluído.'); await screen.findByText('Publicado · v1');
    await waitFor(() => expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument()); expect(screen.queryByRole('button', { name: 'Excluir curso' })).not.toBeInTheDocument();
    expect(boundary.writes).toHaveLength(1);
  });

  it.each([editorPath, '/autoria'])('published course offers no deletion at %s', async (path) => {
    renderDeletion(createDeletionBoundary(true), path); await screen.findByText('Publicado · v1'); expect(screen.queryByRole('button', { name: 'Excluir curso' })).not.toBeInTheDocument();
  });

  it('reader has no deletion action for a draft', async () => {
    renderDeletion(createDeletionBoundary(false, false)); await screen.findByRole('heading', { name: authoringCourseFixture.title }); expect(screen.queryByRole('button', { name: 'Excluir curso' })).not.toBeInTheDocument();
  });
});
