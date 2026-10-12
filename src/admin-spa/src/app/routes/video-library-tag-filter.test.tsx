import { act, cleanup, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { createMemoryRouter, Outlet, RouterProvider } from 'react-router';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { VideosAreaRoute } from '@/app/routes/videos-area-route';
import { env } from '@/config/env';
import type { VideoPage } from '@/features/videos/api/get-videos';
import { normalizeVideoTag } from '@/features/videos/utils/video-tags';
import { videoStatusFixtures } from '@/testing/handlers';
import { server } from '@/testing/server';
import { renderWithProviders } from '@/testing/test-utils';

const videos = [
  { ...videoStatusFixtures.ready, title: 'Aula A', tags: ['Docker', 'Módulo 2'] },
  { ...videoStatusFixtures.failed, title: 'Aula B', tags: ['Docker'] },
  { ...videoStatusFixtures.preparing, title: 'Aula C', tags: ['Módulo 2'] },
];
let requests: URLSearchParams[] = [];

const renderRoute = (entry = '/videos') => {
  const router = createMemoryRouter([{
    path: '/', element: <Outlet context={{ permissions: ['midia.enviar'], name: 'Marina', roles: ['professor'] }} />,
    children: [{ path: 'videos', element: <VideosAreaRoute /> }],
  }], { initialEntries: [entry] });
  const view = renderWithProviders(<RouterProvider router={router} />);
  return { router, ...view };
};
const paramsOf = (router: ReturnType<typeof createMemoryRouter>) => new URLSearchParams(router.state.location.search);
const responsePage = (data: VideoPage['data'], params: URLSearchParams) => ({
  data, pagination: { page: Number(params.get('_page') ?? 1), size: 20, total: data.length, totalPages: data.length ? 1 : 0 },
});

beforeEach(() => {
  requests = [];
  server.use(
    http.get(`${env.API_URL}/api/v1/video-tags`, ({ request }) => {
      const query = normalizeVideoTag(new URL(request.url).searchParams.get('q') ?? '');
      return HttpResponse.json({ data: ['Docker', 'Módulo 2'].filter((tag) => normalizeVideoTag(tag).includes(query)) });
    }),
    http.get(`${env.API_URL}/api/v1/videos`, ({ request }) => {
      const params = new URL(request.url).searchParams;
      requests.push(params);
      const tags = params.getAll('tag').map(normalizeVideoTag);
      const data = videos.filter((video) => {
        const names = video.tags.map(normalizeVideoTag);
        const match = tags.length === 0 || (params.get('tagMatch') === 'any'
          ? tags.some((tag) => names.includes(tag)) : tags.every((tag) => names.includes(tag)));
        return match && (!params.has('status') || params.getAll('status').includes(video.status))
          && (!params.has('q') || video.title.includes(params.get('q') ?? ''));
      });
      return HttpResponse.json(responsePage(data, params));
    }),
  );
});
afterEach(() => { cleanup(); vi.useRealTimers(); });

describe('video library tag filter', () => {
  it('restores repeated tags and OU from the URL and from a reload', async () => {
    const entry = '/videos?tag=docker&tag=m%C3%B3dulo%202&tagMatch=any';
    const view = renderRoute(entry);
    expect(await screen.findByText('Aula A')).toBeInTheDocument();
    expect(screen.getByText('Aula B')).toBeInTheDocument();
    expect(screen.getByText('Aula C')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'OU' })).toHaveAttribute('aria-pressed', 'true');
    expect(requests[0]?.getAll('tag')).toEqual(['docker', 'módulo 2']);
    view.unmount();
    renderRoute(view.router.state.location.pathname + view.router.state.location.search);
    expect(await screen.findByText('Aula B')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'OU' })).toHaveAttribute('aria-pressed', 'true');
  });

  it('selects existing suggestions, defaults to E and changes to OU', async () => {
    const user = userEvent.setup();
    const { router } = renderRoute();
    await screen.findByText('Aula A');
    expect(screen.queryByRole('group', { name: 'Combinar tags' })).not.toBeInTheDocument();
    const input = screen.getByRole('combobox', { name: 'Filtrar por tag' });
    await user.type(input, 'dock');
    await user.click(await screen.findByRole('option', { name: 'Docker' }));
    expect(await screen.findByText('Aula B')).toBeInTheDocument();
    expect(screen.queryByRole('group', { name: 'Combinar tags' })).not.toBeInTheDocument();
    await user.type(input, 'modulo');
    await screen.findByRole('option', { name: 'Módulo 2' });
    await user.keyboard('{ArrowDown}{Enter}');
    expect(await screen.findByRole('button', { name: 'E' })).toHaveAttribute('aria-pressed', 'true');
    await waitFor(() => expect(screen.queryByText('Aula B')).not.toBeInTheDocument());
    expect(paramsOf(router).getAll('tag')).toEqual(['Docker', 'Módulo 2']);
    expect(paramsOf(router).get('tagMatch')).toBe('all');
    await user.click(screen.getByRole('button', { name: 'OU' }));
    expect(await screen.findByText('Aula B')).toBeInTheDocument();
    expect(await screen.findByText('Aula C')).toBeInTheDocument();
    expect(paramsOf(router).get('tagMatch')).toBe('any');
    await user.click(screen.getByRole('button', { name: 'Remover tag Módulo 2' }));
    expect(screen.queryByRole('group', { name: 'Combinar tags' })).not.toBeInTheDocument();
    await waitFor(() => expect(screen.queryByText('Aula C')).not.toBeInTheDocument());
  });

  it('resets the page when changing tags or E/OU and preserves status and title', async () => {
    const user = userEvent.setup();
    const { router } = renderRoute('/videos?status=failed&q=Aula&tag=Docker&tag=M%C3%B3dulo%202&tagMatch=all&page=3');
    await screen.findByText('Nenhum vídeo atende aos filtros.');
    await user.click(screen.getByRole('button', { name: 'OU' }));
    expect(await screen.findByText('Aula B')).toBeInTheDocument();
    expect(paramsOf(router).has('page')).toBe(false);
    expect(requests.at(-1)?.get('_page')).toBe('1');
    expect(paramsOf(router).get('q')).toBe('Aula');
    expect(paramsOf(router).get('status')).toBe('failed');
    await act(async () => { await router.navigate('/videos?status=failed&q=Aula&tag=Docker&tag=M%C3%B3dulo%202&tagMatch=any&page=2'); });
    await user.click(screen.getByRole('button', { name: 'Remover tag Módulo 2' }));
    expect(paramsOf(router).has('page')).toBe(false);
    expect(paramsOf(router).getAll('tag')).toEqual(['Docker']);
    expect(paramsOf(router).get('q')).toBe('Aula');
    expect(paramsOf(router).get('status')).toBe('failed');
  });

  it('preserves tags and mode when changing status, title and page', async () => {
    const user = userEvent.setup();
    server.use(http.get(`${env.API_URL}/api/v1/videos`, ({ request }) => {
      const params = new URL(request.url).searchParams; requests.push(params);
      return HttpResponse.json({ data: [videos[1]], pagination: { page: Number(params.get('_page')), size: 20, total: 40, totalPages: 2 } });
    }));
    const { router } = renderRoute('/videos?tag=Docker&tag=M%C3%B3dulo%202&tagMatch=any');
    await screen.findByText('Aula B');
    await user.click(screen.getByRole('button', { name: 'Falharam' }));
    await user.type(screen.getByRole('searchbox', { name: 'Buscar título' }), 'Aula');
    await waitFor(() => expect(requests.at(-1)?.get('q')).toBe('Aula'));
    await user.click(await screen.findByRole('button', { name: 'Página 2' }));
    await waitFor(() => expect(requests.at(-1)?.get('_page')).toBe('2'));
    expect(paramsOf(router).getAll('tag')).toEqual(['Docker', 'Módulo 2']);
    expect(paramsOf(router).get('tagMatch')).toBe('any');
    expect(requests.at(-1)?.getAll('status')).toEqual(['failed']);
    expect(requests.at(-1)?.get('q')).toBe('Aula');
  });

  it('shows a filtered empty state for unknown URL tags and clears all filters together', async () => {
    const user = userEvent.setup();
    const { router } = renderRoute('/videos?tag=unknown&status=failed&q=missing&page=2&tagMatch=any');
    expect(await screen.findByText('Nenhum vídeo atende aos filtros.')).toBeInTheDocument();
    expect(screen.queryByText('Nenhum vídeo ainda')).not.toBeInTheDocument();
    expect(requests[0]?.getAll('tag')).toEqual(['unknown']);
    await user.click(screen.getByRole('button', { name: 'Limpar filtros' }));
    expect(await screen.findByText('Aula A')).toBeInTheDocument();
    expect(router.state.location.search).toBe('');
    expect(screen.getByRole('searchbox', { name: 'Buscar título' })).toHaveValue('');
    await waitFor(() => expect(requests.at(-1)?.has('tag')).toBe(false));
    expect(requests.at(-1)?.has('q')).toBe(false);
    expect(requests.at(-1)?.has('status')).toBe(false);
  });

  it('guides the teacher when Enter cannot select an existing tag', async () => {
    const user = userEvent.setup();
    const { router } = renderRoute(); await screen.findByText('Aula A');
    await user.type(screen.getByRole('combobox', { name: 'Filtrar por tag' }), 'unknown');
    await user.keyboard('{Enter}');
    expect(await screen.findByText('Escolha uma tag existente nas sugestões.')).toBeInTheDocument();
    expect(paramsOf(router).getAll('tag')).toEqual([]);
  });

  it('removes a video from the filtered list after its last matching tag is edited away', async () => {
    const user = userEvent.setup();
    let current = { ...videos[1]! };
    server.use(
      http.get(`${env.API_URL}/api/v1/videos`, ({ request }) => {
        const params = new URL(request.url).searchParams;
        return HttpResponse.json(responsePage(current.tags.includes('Docker') ? [current] : [], params));
      }),
      http.put(`${env.API_URL}/api/v1/videos/:videoId/tags`, () => {
        current = { ...current, tags: [] };
        return HttpResponse.json(current);
      }),
    );
    renderRoute('/videos?tag=Docker');
    await screen.findByText('Aula B');
    await user.click(screen.getByRole('button', { name: 'Editar tags de Aula B' }));
    const dialog = screen.getByRole('dialog', { name: 'Editar tags' });
    await user.click(within(dialog).getByRole('button', { name: 'Remover tag Docker' }));
    await user.click(within(dialog).getByRole('button', { name: 'Salvar' }));
    expect(await screen.findByText('Nenhum vídeo atende aos filtros.')).toBeInTheDocument();
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(screen.queryByText('Aula B')).not.toBeInTheDocument();
  });

  it('keeps the same tag, mode, title and status filters on automatic refresh', async () => {
    vi.useFakeTimers({ toFake: ['setInterval', 'clearInterval'] });
    server.use(http.get(`${env.API_URL}/api/v1/videos`, ({ request }) => {
      const params = new URL(request.url).searchParams; requests.push(params);
      return HttpResponse.json(responsePage([{ ...videos[2]!, status: 'preparing' }], params));
    }));
    renderRoute('/videos?status=progress&q=Aula&tag=Docker&tag=M%C3%B3dulo%202&tagMatch=any');
    await screen.findByText('Aula C');
    const initial = requests[0]?.toString();
    await act(async () => { await vi.advanceTimersByTimeAsync(10_000); });
    await waitFor(() => expect(requests.length).toBeGreaterThan(1));
    expect(requests.at(-1)?.toString()).toBe(initial);
    expect(screen.getByRole('button', { name: 'OU' })).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByRole('button', { name: 'Remover tag Docker' })).toBeInTheDocument();
  });
});
