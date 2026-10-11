import { useState } from 'react';
import { cleanup, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { delay, http, HttpResponse } from 'msw';
import { createMemoryRouter, Outlet, RouterProvider } from 'react-router';
import { afterEach, describe, expect, it } from 'vitest';

import { VideosAreaRoute } from '@/app/routes/videos-area-route';
import { env } from '@/config/env';
import { VideoTagField } from '@/features/videos/components/video-tag-field';
import { normalizeVideoTag } from '@/features/videos/utils/video-tags';
import { videoStatusFixtures } from '@/testing/handlers';
import { server } from '@/testing/server';
import { renderWithProviders } from '@/testing/test-utils';

const catalog = ['Docker', 'Docker Compose', 'Introdução', 'Moby Docker'];
const setup = (initial: string[] = []) => {
  let video = { ...videoStatusFixtures.ready, title: 'Aula original', tags: initial };
  const queries: (string | null)[] = [];
  const writes: string[][] = [];
  server.use(
    http.get(`${env.API_URL}/api/v1/videos`, () => HttpResponse.json({ data: [video], pagination: { page: 1, size: 20, total: 1, totalPages: 1 } })),
    http.get(`${env.API_URL}/api/v1/video-tags`, ({ request }) => {
      const query = new URL(request.url).searchParams.get('q'); queries.push(query);
      const normalized = normalizeVideoTag(query ?? '');
      return HttpResponse.json({ data: catalog.filter((tag) => normalizeVideoTag(tag).includes(normalized)) });
    }),
    http.put(`${env.API_URL}/api/v1/videos/:id/tags`, async ({ request }) => {
      const body = await request.json() as { tags: string[] }; // MSW receives the validated client payload.
      writes.push(body.tags); video = { ...video, tags: body.tags }; return HttpResponse.json(video);
    }),
  );
  return { queries, writes };
};
const openEditor = async () => {
  const router = createMemoryRouter([{ path: '/', element: <Outlet context={{ permissions: ['midia.enviar'], name: 'Marina', roles: ['professor'] }} />,
    children: [{ path: 'videos', element: <VideosAreaRoute /> }] }], { initialEntries: ['/videos'] });
  renderWithProviders(<RouterProvider router={router} />);
  const user = userEvent.setup();
  await user.click(await screen.findByRole('button', { name: 'Editar tags de Aula original' }));
  return { user, input: screen.getByRole('combobox', { name: 'Tags' }) };
};
const FilterField = () => {
  const [tags, setTags] = useState<string[]>([]);
  return <VideoTagField tags={tags} onChange={setTags} onDraftChange={() => {}} disabled={false} mode="suggestion-only" />;
};
afterEach(cleanup);

describe('video-tag-suggestions', () => {
  it('queries after a short pause and selects the canonical accent spelling with the keyboard', async () => {
    const { queries, writes } = setup(); const { user, input } = await openEditor();
    await user.type(input, 'introducao');
    expect(await screen.findByRole('option', { name: 'Introdução' })).toBeInTheDocument();
    expect(queries).toContain('introducao');
    expect(input).toHaveAttribute('aria-expanded', 'true');
    expect(within(screen.getByRole('dialog')).getByRole('status')).toHaveTextContent('1 sugestões disponíveis');
    await user.keyboard('{ArrowDown}');
    const option = screen.getByRole('option', { name: 'Introdução' });
    expect(option).toHaveAttribute('aria-selected', 'true'); expect(input).toHaveAttribute('aria-activedescendant', option.id);
    expect(within(screen.getByRole('dialog')).getByRole('status')).toHaveTextContent('Introdução');
    await user.keyboard('{Enter}'); expect(screen.getByRole('button', { name: 'Remover tag Introdução' })).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Salvar' })); await screen.findByText('Tags atualizadas');
    expect(writes).toEqual([['Introdução']]);
  });

  it('navigates prefix suggestions with both arrows and confirms with a comma', async () => {
    setup(); const { user, input } = await openEditor(); await user.type(input, 'dock');
    await screen.findByRole('option', { name: 'Docker Compose' });
    expect(within(screen.getByRole('listbox')).getAllByRole('option').map((option) => option.textContent)).toEqual(['Docker', 'Docker Compose', 'Moby Docker']);
    await user.keyboard('{ArrowDown}{ArrowDown}{ArrowUp}{ArrowDown},');
    expect(screen.getByRole('button', { name: 'Remover tag Docker Compose' })).toBeInTheDocument();
    expect(input).toHaveValue(''); expect(input).toHaveFocus();
  });

  it('Escape dismisses suggestions without closing the dialog or confirming the draft', async () => {
    const { writes } = setup(); const { user, input } = await openEditor(); await user.type(input, 'dock');
    await screen.findByRole('option', { name: 'Docker' }); await user.keyboard('{Escape}');
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument(); expect(screen.getByRole('dialog')).toBeInTheDocument();
    expect(input).toHaveValue('dock'); expect(screen.queryByRole('button', { name: /Remover tag/ })).not.toBeInTheDocument(); expect(writes).toEqual([]);
    await user.keyboard('{Escape}'); expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Editar tags de Aula original' })).toHaveFocus();
  });

  it('accepts a new free text tag and Backspace removes the last selected chip only with an empty draft', async () => {
    setup(); const { user, input } = await openEditor(); await user.type(input, 'Nova tag{Enter}');
    expect(screen.getByRole('button', { name: 'Remover tag Nova tag' })).toBeInTheDocument();
    await user.type(input, 'x{Backspace}'); expect(screen.getByRole('button', { name: 'Remover tag Nova tag' })).toBeInTheDocument();
    await user.keyboard('{Backspace}'); expect(screen.queryByRole('button', { name: /Remover tag/ })).not.toBeInTheDocument();
  });

  it('deduplicates the selected suggestion and announces the existing chip', async () => {
    setup(['Docker']); const { user, input } = await openEditor(); await user.type(input, 'docker');
    await screen.findByRole('option', { name: 'Docker' }); await user.keyboard('{Enter}');
    expect(screen.getAllByRole('button', { name: /Remover tag/ })).toHaveLength(1);
    expect(within(screen.getByRole('dialog')).getByRole('status')).toHaveTextContent('Docker já incluída');
  });

  it('selects a suggestion by click while retaining input focus', async () => {
    setup(); const { user, input } = await openEditor(); await user.type(input, 'introducao');
    await user.click(await screen.findByRole('option', { name: 'Introdução' }));
    expect(screen.getByRole('button', { name: 'Remover tag Introdução' })).toBeInTheDocument(); expect(input).toHaveFocus();
  });

  it('suggestion-only mode rejects new text and permits selecting an existing tag', async () => {
    setup(); renderWithProviders(<FilterField />); const user = userEvent.setup(); const input = screen.getByRole('combobox', { name: 'Tags' });
    await user.type(input, 'New tag{Enter}'); expect(screen.queryByRole('button', { name: /Remover tag/ })).not.toBeInTheDocument();
    await user.clear(input); await user.type(input, 'introducao'); await screen.findByRole('option', { name: 'Introdução' });
    await user.keyboard('{ArrowUp}{Enter}'); expect(screen.getByRole('button', { name: 'Remover tag Introdução' })).toBeInTheDocument();
  });

  it('hides old suggestions while a new query is pending', async () => {
    setup(); const { user, input } = await openEditor(); await user.type(input, 'dock'); await screen.findByRole('option', { name: 'Docker' });
    server.use(http.get(`${env.API_URL}/api/v1/video-tags`, async ({ request }) => {
      const query = new URL(request.url).searchParams.get('q'); await delay(100);
      return HttpResponse.json({ data: query === 'introducao' ? ['Introdução'] : [] });
    }));
    await user.clear(input); await user.type(input, 'introducao');
    expect(screen.queryByRole('option', { name: 'Docker' })).not.toBeInTheDocument();
    await screen.findByRole('option', { name: 'Introdução' });
    await waitFor(() => expect(screen.queryByRole('option', { name: 'Docker' })).not.toBeInTheDocument());
  });
});
