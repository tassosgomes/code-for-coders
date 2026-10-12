import { cleanup, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { createMemoryRouter, Outlet, RouterProvider } from 'react-router';
import { afterEach, describe, expect, it } from 'vitest';

import { VideosAreaRoute } from '@/app/routes/videos-area-route';
import { env } from '@/config/env';
import type { VideoPage } from '@/features/videos/api/get-videos';
import { videoStatusFixtures } from '@/testing/handlers';
import { server } from '@/testing/server';
import { renderWithProviders } from '@/testing/test-utils';

const renderRoute = (permissions = ['midia.enviar']) => {
  const router = createMemoryRouter([{ path: '/', element: <Outlet context={{ permissions, name: 'Marina', roles: ['professor'] }} />,
    children: [{ path: 'videos', element: <VideosAreaRoute /> }] }], { initialEntries: ['/videos'] });
  renderWithProviders(<RouterProvider router={router} />);
};
const setup = (initial: string[] = []) => {
  let current: VideoPage['data'][number] = { ...videoStatusFixtures.ready, title: 'Aula original', tags: initial };
  const writes: { tags: string[]; key: string | null }[] = [];
  server.use(
    http.get(`${env.API_URL}/api/v1/videos`, () => HttpResponse.json({ data: [current], pagination: { page: 1, size: 20, total: 1, totalPages: 1 } })),
    http.put(`${env.API_URL}/api/v1/videos/:id/tags`, async ({ request }) => {
      const body = await request.json() as { tags: string[] }; // MSW receives the validated client payload.
      writes.push({ tags: body.tags, key: request.headers.get('Idempotency-Key') });
      current = { ...current, tags: body.tags.map((tag) => tag.toLowerCase() === 'docker' ? 'Docker' : tag) };
      return HttpResponse.json(current);
    }),
  );
  return writes;
};
const openDialog = async () => {
  const user = userEvent.setup();
  await user.click(await screen.findByRole('button', { name: 'Editar tags de Aula original' }));
  return user;
};
afterEach(cleanup);

describe('video-tags-edit', () => {
  it('deduplicates free text and displays the canonical server spelling while preserving video fields', async () => {
    const writes = setup(); renderRoute(); const user = await openDialog();
    const input = screen.getByRole('combobox', { name: 'Tags' }); expect(input).toHaveFocus();
    expect(screen.getByRole('button', { name: 'Salvar' })).toBeDisabled();
    await user.type(input, 'docker{Enter}Dócker,DOCKER{Enter}');
    expect(screen.getAllByRole('button', { name: /Remover tag/ })).toHaveLength(1);
    await user.click(screen.getByRole('button', { name: 'Salvar' }));
    await screen.findByText('Tags atualizadas');
    expect(screen.getByText('Docker')).toBeInTheDocument(); expect(screen.getByText('Pronto')).toBeInTheDocument();
    expect(screen.getByText('Aula original')).toBeInTheDocument(); expect(screen.getByText('0:20')).toBeInTheDocument();
    expect(writes[0]?.tags).toEqual(['docker']); expect(writes[0]?.key).toBeTruthy();
  });
  it('uses the PUT response immediately without depending on a list refresh', async () => {
    setup();
    server.use(http.put(`${env.API_URL}/api/v1/videos/:id/tags`, () => HttpResponse.json({ ...videoStatusFixtures.ready, title: 'Aula original', tags: ['Docker'] })));
    renderRoute(); const user = await openDialog();
    await user.type(screen.getByRole('combobox', { name: 'Tags' }), 'docker{Enter}');
    await user.click(screen.getByRole('button', { name: 'Salvar' })); await screen.findByText('Tags atualizadas');
    expect(screen.getByText('Docker')).toBeInTheDocument();
  });
  it('allows removing every tag and persists the empty set', async () => {
    const writes = setup(['Docker']); renderRoute(); const user = await openDialog();
    await user.click(screen.getByRole('button', { name: 'Remover tag Docker' }));
    await user.click(screen.getByRole('button', { name: 'Salvar' })); await screen.findByText('Tags atualizadas');
    expect(writes[0]?.tags).toEqual([]); expect(screen.queryByText('Docker')).not.toBeInTheDocument();
  });
  it('cancels edits without a write and restores focus', async () => {
    const writes = setup(); renderRoute(); const user = await openDialog();
    await user.type(screen.getByRole('combobox', { name: 'Tags' }), 'docker{Enter}');
    await user.keyboard('{Escape}'); expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(writes).toHaveLength(0); expect(screen.getByRole('button', { name: 'Editar tags de Aula original' })).toHaveFocus();
  });
  it('rejects a tag over thirty characters before sending', async () => {
    const writes = setup(); renderRoute(); const user = await openDialog();
    await user.type(screen.getByRole('combobox', { name: 'Tags' }), `${'a'.repeat(31)}{Enter}`);
    expect(screen.getByText('Cada tag precisa ter de 1 a 30 caracteres.')).toBeInTheDocument(); expect(writes).toHaveLength(0);
  });
  it('rejects the eleventh distinct tag and keeps the selected set', async () => {
    const writes = setup(Array.from({ length: 10 }, (_, i) => `Tag ${i}`)); renderRoute(); const user = await openDialog();
    await user.type(screen.getByRole('combobox', { name: 'Tags' }), 'Eleventh{Enter}');
    expect(screen.getByText('Um vídeo pode ter no máximo 10 tags.')).toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: /Remover tag/ })).toHaveLength(10); expect(writes).toHaveLength(0);
  });
  it.each([['TAG_INVALID', 'Cada tag precisa ter de 1 a 30 caracteres.'], ['TAGS_LIMIT_EXCEEDED', 'Um vídeo pode ter no máximo 10 tags.']])('preserves values after %s', async (code, message) => {
    setup(); server.use(http.put(`${env.API_URL}/api/v1/videos/:id/tags`, () => HttpResponse.json({ code }, { status: 422 })));
    renderRoute(); const user = await openDialog(); await user.type(screen.getByRole('combobox', { name: 'Tags' }), 'Docker{Enter}');
    await user.click(screen.getByRole('button', { name: 'Salvar' })); expect(await screen.findByText(message)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Remover tag Docker' })).toBeInTheDocument();
  });
  it('retries the same intent with the same key and a changed body with a fresh key', async () => {
    setup(); const keys: string[] = []; let calls = 0;
    server.use(http.put(`${env.API_URL}/api/v1/videos/:id/tags`, ({ request }) => {
      keys.push(request.headers.get('Idempotency-Key') ?? ''); calls++;
      return calls < 3 ? HttpResponse.json({ code: 'MEDIA_UNAVAILABLE' }, { status: 502 }) : HttpResponse.json({ ...videoStatusFixtures.ready, tags: ['Docker', 'React'] });
    }));
    renderRoute(); const user = await openDialog(); await user.type(screen.getByRole('combobox', { name: 'Tags' }), 'Docker{Enter}');
    await user.click(screen.getByRole('button', { name: 'Salvar' })); await screen.findByText('Não foi possível salvar as tags. Tente de novo.');
    await user.click(screen.getByRole('button', { name: 'Tentar de novo' }));
    await waitFor(() => expect(keys).toHaveLength(2)); expect(keys[1]).toBe(keys[0]);
    await user.type(screen.getByRole('combobox', { name: 'Tags' }), 'React{Enter}');
    await user.click(screen.getByRole('button', { name: 'Salvar' })); await screen.findByText('Tags atualizadas');
    expect(keys[2]).not.toBe(keys[0]);
  });
  it('shows all ten tags without removing them from the list', async () => {
    setup(Array.from({ length: 10 }, (_, i) => `Tag ${i}`)); renderRoute(); await screen.findByText('Aula original');
    const tags = screen.getByLabelText('Tags de Aula original'); expect(within(tags).getAllByText(/Tag /)).toHaveLength(10);
  });
  it('blocks the videos area without permission', async () => {
    setup(); renderRoute([]); expect(await screen.findByRole('heading', { name: 'Esta área não é do seu papel' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Editar tags/ })).not.toBeInTheDocument();
  });
});
