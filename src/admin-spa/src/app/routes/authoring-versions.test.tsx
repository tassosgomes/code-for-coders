import { cleanup, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { routes } from '@/app/app-routes';
import { paths } from '@/config/paths';
import { createVersionBoundary } from '@/testing/authoring-version-handlers';
import { publicationLessonId, publicationVideoId } from '@/testing/authoring-publication-handlers';
import { server } from '@/testing/server';
import { renderWithProviders } from '@/testing/test-utils';

const renderCourse = (boundary = createVersionBoundary(), version?: number) => {
  server.use(...boundary.handlers);
  const path = version ? paths.authoringVersion.getHref(boundary.snapshot().courseId, version) : paths.authoringCourse.getHref(boundary.snapshot().courseId);
  const router = createMemoryRouter(routes, { basename: '/admin', initialEntries: [`/admin${path}`] });
  const rendered = renderWithProviders(<RouterProvider router={router} />);
  return { boundary, router, ...rendered };
};
afterEach(() => { cleanup(); vi.restoreAllMocks(); });
describe('authoring versions', () => {
  it('keyboard arrows move focus and selection between draft and history without changing the course', async () => {
    const user = userEvent.setup(); const { boundary } = renderCourse();
    const draft = await screen.findByRole('tab', { name: 'Rascunho' });
    draft.focus(); await user.keyboard('{ArrowRight}');
    const history = screen.getByRole('tab', { name: 'Histórico' });
    expect(history).toHaveFocus(); expect(history).toHaveAttribute('aria-selected', 'true');
    expect(await screen.findByRole('tabpanel', { name: 'Histórico' })).toBeVisible();
    expect(screen.queryByRole('tabpanel', { name: 'Rascunho' })).not.toBeInTheDocument();
    await user.keyboard('{Home}'); expect(draft).toHaveFocus();
    expect(draft).toHaveAttribute('aria-selected', 'true'); expect(boundary.writes).toHaveLength(0);
  });

  it('copies the complete published video reference and announces success', async () => {
    const user = userEvent.setup(); const clipboard = vi.spyOn(navigator.clipboard, 'writeText');
    renderCourse(createVersionBoundary(), 1);
    await user.click(await screen.findByRole('button', { name: 'Copiar referência de Tipos originais' }));
    expect(clipboard).toHaveBeenCalledWith(publicationVideoId);
    expect(await screen.findByText('Referência copiada.')).toHaveAttribute('role', 'status');
  });

  it('makes the full reference available for manual copy if clipboard access fails', async () => {
    const user = userEvent.setup(); vi.spyOn(navigator.clipboard, 'writeText').mockRejectedValueOnce(new Error('Permission denied'));
    renderCourse(createVersionBoundary(), 1);
    await user.click(await screen.findByRole('button', { name: 'Copiar referência de Tipos originais' }));
    expect(await screen.findByText(`Não foi possível copiar. Referência do vídeo: ${publicationVideoId}`)).toHaveAttribute('role', 'status');
  });

  it('direct historical URL and a fresh router preserve the requested immutable version with no edit controls', async () => {
    const boundary = createVersionBoundary(); const first = renderCourse(boundary, 1);
    await screen.findByRole('heading', { name: 'Versão 1', level: 1 });
    expect(screen.getByText('Título publicado')).toBeVisible();
    expect(screen.getByText(`ID da aula: ${publicationLessonId}`)).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: '1. Tipos originais' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Editar|Publicar|Descartar|Trocar vídeo/ })).not.toBeInTheDocument();
    first.unmount(); first.router.dispose(); renderCourse(boundary, 1);
    await screen.findByRole('heading', { name: 'Versão 1', level: 1 }); expect(boundary.reads).toEqual([1, 1]);
  });

  it('republishes the displayed revision and history shows newest first, author, note and current marker', async () => {
    const user = userEvent.setup(); const { boundary } = renderCourse();
    await user.click(await screen.findByRole('button', { name: 'Publicar nova versão' }));
    expect(screen.getByRole('dialog')).toHaveTextContent('Revisão 4');
    await user.type(screen.getByLabelText('Nota da versão (opcional)'), 'Segunda versão');
    await user.click(screen.getByRole('button', { name: 'Publicar versão 2' }));
    await screen.findByText(/Versão 2 publicada por Rafael/);
    await user.click(screen.getByRole('tab', { name: 'Histórico' }));
    const history = await screen.findByRole('region', { name: 'Histórico de versões' });
    expect(within(history).getAllByRole('heading', { level: 3 }).map((item) => item.textContent)).toEqual(['Versão 2 · Vigente', 'Versão 1 · Anterior']);
    expect(within(history).getByText('Segunda versão')).toBeInTheDocument(); expect(within(history).getByText(/Marina/)).toBeInTheDocument();
    await user.click(within(history).getByRole('link', { name: 'Ver versão 1' }));
    await screen.findByRole('heading', { name: 'Versão 1', level: 1 }); expect(screen.getByText('Anterior')).toBeVisible();
    expect(boundary.writes[0]?.revision).toBe(4);
  });

  it('canceling discard makes no request and confirming restores current content and clears the change indicator', async () => {
    const user = userEvent.setup(); const { boundary } = renderCourse();
    await user.click(await screen.findByRole('button', { name: 'Descartar alterações' }));
    await user.click(screen.getByRole('button', { name: 'Cancelar' })); expect(boundary.writes).toHaveLength(0);
    await user.click(screen.getByRole('button', { name: 'Descartar alterações' }));
    await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Descartar alterações' }));
    await screen.findByRole('heading', { name: 'Título publicado' });
    expect(screen.getByText('Publicado · v1')).toBeInTheDocument(); expect(screen.queryByRole('button', { name: 'Descartar alterações' })).not.toBeInTheDocument();
    expect(boundary.writes).toHaveLength(1); expect(boundary.writes[0]?.revision).toBe(4);
  });

  it('stale discard reloads the colleague edit, never retries automatically and requires a new confirmation and key', async () => {
    const user = userEvent.setup(); const boundary = createVersionBoundary(); boundary.setMode('changed'); renderCourse(boundary);
    await user.click(await screen.findByRole('button', { name: 'Descartar alterações' }));
    await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Descartar alterações' }));
    await screen.findByText(/O rascunho mudou/); await screen.findByRole('heading', { name: 'Edição do colega' }); expect(boundary.writes).toHaveLength(1);
    await user.click(screen.getByRole('button', { name: 'Voltar ao rascunho' })); boundary.setMode('success');
    await user.click(screen.getByRole('button', { name: 'Descartar alterações' })); expect(screen.getByRole('dialog')).toHaveTextContent('Revisão 5');
    await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Descartar alterações' })); await screen.findByRole('heading', { name: 'Título publicado' });
    expect(boundary.writes[1]?.revision).toBe(5); expect(boundary.writes[1]?.key).not.toBe(boundary.writes[0]?.key);
  });

  it('stale republication reloads course and requires a new confirmation with the updated revision', async () => {
    const user = userEvent.setup(); const boundary = createVersionBoundary(); boundary.setMode('changed'); renderCourse(boundary);
    await user.click(await screen.findByRole('button', { name: 'Publicar nova versão' })); await user.click(screen.getByRole('button', { name: 'Publicar versão 2' }));
    await screen.findByText(/O rascunho mudou/); await screen.findByRole('heading', { name: 'Edição do colega' }); expect(boundary.writes).toHaveLength(1);
    await user.click(screen.getByRole('button', { name: 'Recarregar rascunho' })); boundary.setMode('success');
    await user.click(screen.getByRole('button', { name: 'Publicar nova versão' })); expect(screen.getByRole('dialog')).toHaveTextContent('Revisão 5');
    await user.click(screen.getByRole('button', { name: 'Publicar versão 2' })); await screen.findByText(/Versão 2 publicada por Rafael/);
    expect(boundary.writes[1]?.revision).toBe(5); expect(boundary.writes[1]?.key).not.toBe(boundary.writes[0]?.key);
  });

  it('transport failure preserves the discard intent key on an explicit retry', async () => {
    const user = userEvent.setup(); const boundary = createVersionBoundary(); boundary.setMode('unavailable'); renderCourse(boundary);
    await user.click(await screen.findByRole('button', { name: 'Descartar alterações' }));
    await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Descartar alterações' })); await screen.findByText(/Não foi possível descartar/);
    boundary.setMode('success'); await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Descartar alterações' })); await screen.findByRole('heading', { name: 'Título publicado' });
    expect(boundary.writes[0]?.key).toBe(boundary.writes[1]?.key);
  });

  it('reader sees history and historical navigation without mutation actions', async () => {
    const user = userEvent.setup(); renderCourse(createVersionBoundary(false)); await screen.findByRole('heading', { name: 'Rascunho alterado' });
    expect(screen.queryByRole('button', { name: /Publicar|Descartar|Editar/ })).not.toBeInTheDocument();
    await user.click(screen.getByRole('tab', { name: 'Histórico' })); await user.click(await screen.findByRole('link', { name: 'Ver versão 1' }));
    await screen.findByRole('heading', { name: 'Versão 1', level: 1 });
  });

  it('never-published course has empty history and no discard control', async () => {
    const user = userEvent.setup(); renderCourse(createVersionBoundary(true, false)); await screen.findByRole('heading', { name: 'Rascunho alterado' });
    expect(screen.queryByRole('button', { name: 'Descartar alterações' })).not.toBeInTheDocument(); await user.click(screen.getByRole('tab', { name: 'Histórico' }));
    await screen.findByText('Este curso ainda não tem versões publicadas.');
  });

  it('missing historical version has an explicit not-found state', async () => {
    renderCourse(createVersionBoundary(), 99); await screen.findByRole('heading', { name: 'Versão não encontrada' }); expect(screen.getByRole('link', { name: 'Voltar ao curso' })).toBeInTheDocument();
  });
});
