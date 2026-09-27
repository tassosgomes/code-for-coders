import { cleanup, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { createMemoryRouter, Outlet, RouterProvider } from 'react-router';
import { afterEach, describe, expect, it } from 'vitest';

import { VideosAreaRoute } from '@/app/routes/videos-area-route';
import { env } from '@/config/env';
import { videoStatusFixtures } from '@/testing/handlers';
import { server } from '@/testing/server';
import { renderWithProviders } from '@/testing/test-utils';

const videos = [
  { ...videoStatusFixtures.ready, title: 'Injeção de dependência' },
  { ...videoStatusFixtures.failed, title: 'Aula falhada' },
];

const renderRoute = () => {
  const router = createMemoryRouter([{
    path: '/',
    element: <Outlet context={{ permissions: ['midia.enviar'], name: 'Marina Alves', roles: ['professor'] }} />,
    children: [{ path: 'videos', element: <VideosAreaRoute /> }],
  }], { initialEntries: ['/videos'] });
  renderWithProviders(<RouterProvider router={router} />);
};

const page = (data: typeof videos) => ({
  data,
  pagination: { page: 1, size: 10, total: data.length, totalPages: data.length ? 1 : 0 },
});

afterEach(cleanup);

describe('video library filters', () => {
  it('filters failed videos through the status query', async () => {
    const user = userEvent.setup();
    let requestedStatus: string[] = [];
    server.use(http.get(`${env.API_URL}/api/v1/videos`, ({ request }) => {
      requestedStatus = new URL(request.url).searchParams.getAll('status');
      return HttpResponse.json(page(requestedStatus.includes('failed') ? [videos[1]!] : videos));
    }));
    renderRoute();
    expect(await screen.findByText('Injeção de dependência')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Falharam' }));
    await waitFor(() => expect(requestedStatus).toEqual(['failed']));
    expect(await screen.findByText('Aula falhada')).toBeInTheDocument();
    expect(screen.queryByText('Injeção de dependência')).not.toBeInTheDocument();
  });

  it('searches by title and offers to clear an empty result', async () => {
    const user = userEvent.setup();
    const searchedTitles: string[] = [];
    server.use(http.get(`${env.API_URL}/api/v1/videos`, ({ request }) => {
      const query = new URL(request.url).searchParams.get('q');
      if (query) searchedTitles.push(query);
      return HttpResponse.json(page(query === 'injecao' ? [videos[0]!] : query ? [] : videos));
    }));
    renderRoute();
    expect(await screen.findByText('Aula falhada')).toBeInTheDocument();
    await user.type(screen.getByRole('searchbox', { name: 'Buscar título' }), 'injecao');
    expect(await screen.findByText('Injeção de dependência')).toBeInTheDocument();
    await waitFor(() => expect(screen.queryByText('Aula falhada')).not.toBeInTheDocument());
    expect(searchedTitles).toEqual(['injecao']);
    await user.clear(screen.getByRole('searchbox', { name: 'Buscar título' }));
    await user.type(screen.getByRole('searchbox', { name: 'Buscar título' }), 'ausente');
    expect(await screen.findByText('Nenhum vídeo encontrado com esses filtros.')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Limpar filtros' }));
    expect(await screen.findByText('Aula falhada')).toBeInTheDocument();
  });

  it('renames a colleague video and keeps its status on the row', async () => {
    const user = userEvent.setup();
    let current = videos[0]!;
    server.use(
      http.get(`${env.API_URL}/api/v1/videos`, () => HttpResponse.json(page([current]))),
      http.patch(`${env.API_URL}/api/v1/videos/:videoId`, async ({ request }) => {
        const body = await request.json() as { title: string };
        current = { ...current, title: body.title };
        return HttpResponse.json(current);
      }),
    );
    renderRoute();
    expect(await screen.findByText('Injeção de dependência')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Editar título de Injeção de dependência' }));
    const title = screen.getByRole('textbox', { name: 'Título' });
    await user.clear(title);
    expect(screen.getByText('Dê um título para reconhecer o vídeo.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Salvar' })).toBeDisabled();
    await user.type(title, 'Aula corrigida');
    await user.click(screen.getByRole('button', { name: 'Salvar' }));
    expect(await screen.findByText('Aula corrigida')).toBeInTheDocument();
    expect(screen.getByText('Pronto · 0:20')).toBeInTheDocument();
    expect(screen.getByText('Título atualizado')).toBeInTheDocument();
  });

  it('shows server title errors in the field and reuses the retry key', async () => {
    const user = userEvent.setup();
    const keys: string[] = [];
    let attempts = 0;
    server.use(
      http.get(`${env.API_URL}/api/v1/videos`, () => HttpResponse.json(page([videos[0]!]))),
      http.patch(`${env.API_URL}/api/v1/videos/:videoId`, ({ request }) => {
        keys.push(request.headers.get('Idempotency-Key') ?? '');
        attempts += 1;
        return attempts === 1
          ? HttpResponse.json({ code: 'TITLE_REQUIRED' }, { status: 422 })
          : HttpResponse.json({ ...videos[0], title: 'Aula corrigida' });
      }),
    );
    renderRoute();
    await screen.findByText('Injeção de dependência');
    await user.click(screen.getByRole('button', { name: 'Editar título de Injeção de dependência' }));
    const title = screen.getByRole('textbox', { name: 'Título' });
    await user.clear(title);
    await user.type(title, 'Aula corrigida');
    await user.click(screen.getByRole('button', { name: 'Salvar' }));
    expect(await screen.findByText('Dê um título para reconhecer o vídeo.')).toHaveClass('field-error');
    await user.click(screen.getByRole('button', { name: 'Salvar' }));
    await screen.findByText('Título atualizado');
    expect(keys).toHaveLength(2);
    expect(keys[0]).toBeTruthy();
    expect(keys[1]).toBe(keys[0]);
  });
});
