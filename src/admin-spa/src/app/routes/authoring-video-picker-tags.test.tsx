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
const addTag = async (user: ReturnType<typeof userEvent.setup>, tag: string) => {
  const field = screen.getByRole('combobox', { name: 'Filtrar por tag' });
  await user.click(field); await user.type(field, tag);
  await user.click(await screen.findByRole('option', { name: tag }));
};
const lastListTags = (boundary: ReturnType<typeof createAuthoringVideoHandlers>) => {
  const last = boundary.lists[boundary.lists.length - 1];
  return { tags: last?.searchParams.getAll('tag'), tagMatch: last?.searchParams.get('tagMatch') };
};

describe('authoring video picker with tags', () => {
  afterEach(cleanup);

  it('filters by tag and shows the tags of each ready video', async () => {
    const user = userEvent.setup(); const boundary = renderPicker(); await open(user);
    await screen.findByRole('radio', { name: /Vídeo da colega/ });
    await addTag(user, 'Docker');
    await waitFor(() => expect(lastListTags(boundary).tags).toEqual(['Docker']));
    expect(await screen.findByRole('radio', { name: /Vídeo do professor/ })).toBeInTheDocument();
    const colega = screen.getByRole('radio', { name: /Vídeo da colega/ }).closest('label');
    expect(colega).toHaveTextContent('Docker');
    expect(colega).toHaveTextContent('Módulo 2');
    expect(screen.queryByRole('group', { name: 'Combinar tags' })).not.toBeInTheDocument();
  });

  it('lists only ready videos, even when a video being prepared has the same tags', async () => {
    const user = userEvent.setup(); const boundary = renderPicker(); await open(user);
    await screen.findByRole('radio', { name: /Vídeo da colega/ });
    await addTag(user, 'Docker'); await addTag(user, 'Módulo 2');
    await waitFor(() => expect(lastListTags(boundary).tags).toEqual(['Docker', 'Módulo 2']));
    expect(await screen.findByRole('radio', { name: /Vídeo da colega/ })).toBeInTheDocument();
    expect(screen.queryByRole('radio', { name: /Vídeo em preparação/ })).not.toBeInTheDocument();
    expect(boundary.lists.every((url) => url.searchParams.getAll('status').join(',') === 'ready')).toBe(true);
  });

  it('switches between E (default, all tags) and OU (any tag) only with two or more tags', async () => {
    const user = userEvent.setup(); const boundary = renderPicker(); await open(user);
    await screen.findByRole('radio', { name: /Vídeo da colega/ });
    await addTag(user, 'Docker');
    expect(screen.queryByRole('group', { name: 'Combinar tags' })).not.toBeInTheDocument();
    await addTag(user, 'Módulo 2');
    const group = screen.getByRole('group', { name: 'Combinar tags' });
    expect(within(group).getByRole('button', { name: 'E' })).toHaveAttribute('aria-pressed', 'true');
    await waitFor(() => expect(screen.queryByRole('radio', { name: /Vídeo do professor/ })).not.toBeInTheDocument());
    expect(screen.getByRole('radio', { name: /Vídeo da colega/ })).toBeInTheDocument();
    await user.click(within(group).getByRole('button', { name: 'OU' }));
    await waitFor(() => expect(lastListTags(boundary).tagMatch).toBe('any'));
    expect(await screen.findByRole('radio', { name: /Vídeo do professor/ })).toBeInTheDocument();
    await user.click(within(group).getByRole('button', { name: 'E' }));
    await waitFor(() => expect(lastListTags(boundary).tagMatch).toBe('all'));
    await waitFor(() => expect(screen.queryByRole('radio', { name: /Vídeo do professor/ })).not.toBeInTheDocument());
  });

  it('empty filter result explains it and "Limpar filtros" clears title and tags', async () => {
    const user = userEvent.setup(); renderPicker(); await open(user);
    await screen.findByRole('radio', { name: /Vídeo da colega/ });
    await addTag(user, 'Docker'); await user.type(screen.getByLabelText('Buscar título'), 'Inexistente');
    expect(await screen.findByText('Nenhum vídeo pronto atende aos filtros.')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Limpar filtros' }));
    expect(await screen.findByRole('radio', { name: /Vídeo da colega/ })).toBeInTheDocument();
    expect(screen.getByLabelText('Buscar título')).toHaveValue('');
    expect(screen.queryByRole('button', { name: 'Remover tag Docker' })).not.toBeInTheDocument();
  });

  it('linking a video found by the filter keeps the current flow and writes only the video', async () => {
    const user = userEvent.setup(); const boundary = renderPicker(); await open(user);
    await screen.findByRole('radio', { name: /Vídeo da colega/ });
    await addTag(user, 'Docker');
    await user.click(await screen.findByRole('radio', { name: /Vídeo do professor/ }));
    await user.click(screen.getByRole('button', { name: 'Vincular vídeo' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(screen.getByRole('article', { name: 'Tipos' })).toHaveTextContent('Vídeo do professor');
    expect(boundary.writes.map((write) => write.videoId)).toEqual([readyVideoFixtures[1]?.videoId]);
  });
});
