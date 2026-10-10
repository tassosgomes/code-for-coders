import { cleanup, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { afterEach, describe, expect, it } from 'vitest';

import { AdminLayoutRoute } from '@/app/routes/admin-layout-route';
import { AuthoringCourseRoute } from '@/app/routes/authoring-course-route';
import { loadStaffSession } from '@/app/routes/staff-session-loader';
import { createPublicationBoundary, publicationLessonId } from '@/testing/authoring-publication-handlers';
import { server } from '@/testing/server';
import { renderWithProviders } from '@/testing/test-utils';

const renderEditor = (boundary = createPublicationBoundary()) => {
  server.use(...boundary.handlers);
  const router = createMemoryRouter([{ path: '/', loader: loadStaffSession, element: <AdminLayoutRoute title="Autoria" serviceName="admin-spa" />, children: [{ path: 'autoria/:courseId', element: <AuthoringCourseRoute /> }] }], { initialEntries: [`/autoria/${boundary.snapshot().courseId}`] });
  renderWithProviders(<RouterProvider router={router} />); return boundary;
};
afterEach(cleanup);
describe('authoring publish', () => {
  it('shows incomplete curriculum and focuses the missing lesson without publishing', async () => {
    const user = userEvent.setup(); const boundary = renderEditor(createPublicationBoundary(false));
    await user.click(await screen.findByRole('button', { name: 'Recolher Fundamentos' }));
    await user.click(screen.getByRole('button', { name: 'Publicar curso' }));
    expect(await screen.findByRole('dialog')).toHaveTextContent('Antes de publicar');
    await user.click(screen.getByRole('button', { name: 'Tipos — escolha um vídeo.' }));
    await waitFor(() => expect(screen.getByRole('article', { name: 'Tipos' })).toHaveFocus());
    expect(screen.getByRole('article', { name: 'Tipos' })).toHaveAttribute('id', `aula-${publicationLessonId}`);
    expect(boundary.writes).toHaveLength(0);
  });

  it('confirms the displayed revision and optional note and displays publication author and time', async () => {
    const user = userEvent.setup(); const boundary = renderEditor();
    await user.click(await screen.findByRole('button', { name: 'Publicar curso' }));
    expect(screen.getByRole('dialog')).toHaveTextContent('Confira o que será publicado. Esta será a versão 1.'); expect(screen.getByRole('dialog')).not.toHaveTextContent('Revisão');
    await user.type(screen.getByLabelText('Nota de versão (opcional)'), 'Primeira versão');
    await user.click(screen.getByRole('button', { name: 'Publicar versão 1' }));
    expect(await screen.findByText(/Versão 1 publicada por Professor/)).toBeInTheDocument();
    expect(boundary.writes[0]?.body).toEqual({ draftRevision: 1, versionNote: 'Primeira versão' });
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('a failed request retains the note and reuses the intent key on retry', async () => {
    const user = userEvent.setup(); const boundary = renderEditor(); boundary.setMode('unavailable');
    await user.click(await screen.findByRole('button', { name: 'Publicar curso' })); await user.type(screen.getByLabelText('Nota de versão (opcional)'), 'Mantida');
    await user.click(screen.getByRole('button', { name: 'Publicar versão 1' }));
    expect(await screen.findByText(/Sua nota foi mantida/)).toBeInTheDocument();
    expect(screen.getByLabelText('Nota de versão (opcional)')).toHaveValue('Mantida'); boundary.setMode('success');
    await user.click(screen.getByRole('button', { name: 'Publicar versão 1' })); await screen.findByText(/Versão 1 publicada por/);
    expect(boundary.writes[0]?.key).toBe(boundary.writes[1]?.key);
  });

  it('a changed draft requires reload and a new confirmation with the new revision', async () => {
    const user = userEvent.setup(); const boundary = renderEditor(); boundary.setMode('changed');
    await user.click(await screen.findByRole('button', { name: 'Publicar curso' })); await user.click(screen.getByRole('button', { name: 'Publicar versão 1' }));
    expect(await screen.findByText(/O rascunho mudou/)).toBeInTheDocument(); expect(boundary.writes).toHaveLength(1);
    boundary.setMode('success'); await user.click(screen.getByRole('button', { name: 'Recarregar rascunho' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    await user.click(screen.getByRole('button', { name: 'Publicar curso' })); expect(screen.getByRole('dialog')).toHaveTextContent('Publicar curso');
    await user.click(screen.getByRole('button', { name: 'Publicar versão 1' })); await screen.findByText(/Versão 1 publicada por/);
    expect(boundary.writes[1]?.body).toEqual({ draftRevision: 2 }); expect(boundary.writes[1]?.key).not.toBe(boundary.writes[0]?.key);
  });

  it('server pendencies lead back to the matching lesson', async () => {
    const user = userEvent.setup(); const boundary = renderEditor(); boundary.setMode('incomplete');
    await user.click(await screen.findByRole('button', { name: 'Publicar curso' })); await user.click(screen.getByRole('button', { name: 'Publicar versão 1' }));
    await user.click(await screen.findByRole('button', { name: 'Tipos — escolha um vídeo.' }));
    await waitFor(() => expect(screen.getByRole('article', { name: 'Tipos' })).toHaveFocus());
  });

  it('limits the optional note to 1000 characters before publishing', async () => {
    const user = userEvent.setup(); const boundary = renderEditor();
    await user.click(await screen.findByRole('button', { name: 'Publicar curso' }));
    const note = screen.getByLabelText('Nota de versão (opcional)');
    await user.click(note); await user.paste('x'.repeat(1001));
    expect(note).toHaveValue('x'.repeat(1000));
    await user.click(screen.getByRole('button', { name: 'Publicar versão 1' })); await screen.findByText(/Versão 1 publicada por/);
    expect(boundary.writes[0]?.body).toEqual({ draftRevision: 1, versionNote: 'x'.repeat(1000) });
  });

  it('reader sees the draft without publication actions', async () => {
    renderEditor(createPublicationBoundary(true, false)); await screen.findByRole('heading', { name: '.NET do zero à API' });
    expect(screen.queryByRole('button', { name: 'Publicar curso' })).not.toBeInTheDocument();
  });
});
