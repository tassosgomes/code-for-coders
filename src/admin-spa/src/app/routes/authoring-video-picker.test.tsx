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
import { createAuthoringVideoHandlers, readyVideoFixtures } from '@/testing/authoring-video-handlers';
import { server } from '@/testing/server';
import { renderWithProviders } from '@/testing/test-utils';

const renderPicker = (boundary = createAuthoringVideoHandlers()) => {
  server.use(...boundary.handlers, http.get(`${env.API_URL}/api/v1/staff-sessions/current`, () => HttpResponse.json({
    accountId: '0198dfac-674a-7000-8000-000000000002', name: 'Professor', roles: ['professor'], permissions: ['autoria.ler', 'autoria.editar', 'midia.enviar'], csrfToken: 'video-csrf',
  })));
  const router = createMemoryRouter([{ path: '/', loader: loadStaffSession, element: <AdminLayoutRoute title="Autoria" serviceName="admin-spa" />,
    children: [{ path: 'autoria/:courseId', element: <AuthoringCourseRoute /> }],
  }], { initialEntries: [`/autoria/${authoringCourseFixture.courseId}`] });
  renderWithProviders(<RouterProvider router={router} />); return boundary;
};
const open = async (user: ReturnType<typeof userEvent.setup>) => {
  await user.click(within(await screen.findByRole('article', { name: 'Tipos' })).getByRole('button', { name: /Escolher vídeo|Trocar vídeo/ }));
};
describe('authoring video picker', () => {
  afterEach(cleanup);

  it('selects a school colleague ready video, replaces it and unlinks without losing lesson identity', async () => {
    const user = userEvent.setup(); const boundary = renderPicker(); const id = boundary.snapshot().modules[0]?.lessons[0]?.lessonId;
    await open(user); await user.click(await screen.findByRole('radio', { name: /Vídeo da colega/ }));
    expect(screen.getByRole('dialog')).toHaveTextContent('2:05 · Marina');
    expect(screen.getAllByText('Pronto')).toHaveLength(2);
    await user.click(screen.getByRole('button', { name: 'Vincular vídeo' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(screen.getByRole('article', { name: 'Tipos' })).toHaveTextContent('Vídeo da colega');
    await open(user); expect(await screen.findByRole('radio', { name: /Vídeo da colega/ })).toBeChecked();
    await user.click(screen.getByRole('radio', { name: /Vídeo do professor/ })); await user.click(screen.getByRole('button', { name: 'Vincular vídeo' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    await open(user); await user.click(screen.getByRole('button', { name: 'Desvincular vídeo' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(screen.getByRole('article', { name: 'Tipos' })).toHaveAttribute('id', `aula-${id}`);
    expect(screen.getByRole('article', { name: 'Tipos' })).toHaveTextContent('Sem vídeo');
    expect(boundary.writes.map((write) => write.videoId)).toEqual([readyVideoFixtures[0]?.videoId, readyVideoFixtures[1]?.videoId, null]);
    expect(boundary.lists.every((url) => url.searchParams.getAll('status').join(',') === 'ready')).toBe(true);
  });

  it('projection delay preserves the sheet, selected video, lesson and idempotency key until retry succeeds', async () => {
    const user = userEvent.setup(); const boundary = renderPicker(); boundary.setUnavailable(true);
    await open(user); await user.click(await screen.findByRole('radio', { name: /Vídeo da colega/ })); await user.click(screen.getByRole('button', { name: 'Vincular vídeo' }));
    expect(await screen.findByText(/Aguarde alguns instantes/)).toBeInTheDocument();
    expect(screen.getByRole('article', { name: 'Tipos' })).toHaveTextContent('Sem vídeo');
    expect(screen.getByRole('radio', { name: /Vídeo da colega/ })).toBeChecked();
    boundary.setUnavailable(false); await user.click(screen.getByRole('button', { name: 'Atualizar vídeos' }));
    await waitFor(() => expect(screen.getByRole('button', { name: 'Vincular vídeo' })).toBeEnabled());
    await user.click(screen.getByRole('button', { name: 'Vincular vídeo' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(boundary.writes[0]?.key).toBe(boundary.writes[1]?.key);
  });

  it('Media outage shows retry and leaves the lesson unchanged', async () => {
    const user = userEvent.setup(); const boundary = renderPicker(); boundary.setListFails(true);
    await open(user); expect(await screen.findByText('Não foi possível carregar os vídeos.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Vincular vídeo' })).toBeDisabled(); expect(boundary.writes).toHaveLength(0);
    boundary.setListFails(false); await user.click(screen.getByRole('button', { name: 'Tentar de novo' }));
    expect(await screen.findByRole('radio', { name: /Vídeo da colega/ })).toBeInTheDocument();
  });

  it('unknown upstream status cannot appear as a selectable ready video', async () => {
    const user = userEvent.setup(); const boundary = renderPicker(); boundary.setUnknownStatus(true); await open(user);
    expect(await screen.findByText('Não foi possível carregar os vídeos.')).toBeInTheDocument();
    expect(within(screen.getByRole('dialog')).queryByRole('radio')).not.toBeInTheDocument(); expect(boundary.writes).toHaveLength(0);
  });

  it('search without a match offers clear search and cancellation never writes', async () => {
    const user = userEvent.setup(); const boundary = renderPicker(); await open(user);
    await screen.findByRole('radio', { name: /Vídeo da colega/ }); await user.type(screen.getByLabelText('Buscar título'), 'Inexistente');
    expect(await screen.findByText('Nenhum vídeo pronto com esse título.')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Limpar busca' })); await screen.findByRole('radio', { name: /Vídeo da colega/ });
    await user.click(screen.getByRole('button', { name: 'Cancelar' })); expect(boundary.writes).toHaveLength(0);
    expect(within(screen.getByRole('article', { name: 'Tipos' })).getByRole('button', { name: 'Escolher vídeo' })).toHaveFocus();
  });
});
