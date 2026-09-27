import { act, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { createMemoryRouter, Outlet, RouterProvider } from 'react-router';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { VideosAreaRoute } from '@/app/routes/videos-area-route';
import { env } from '@/config/env';
import type { VideoPage } from '@/features/videos/api/get-videos';
import { videoStatusFixtures } from '@/testing/handlers';
import { renderWithProviders } from '@/testing/test-utils';
import { server } from '@/testing/server';

const renderVideosRoute = () => {
  const router = createMemoryRouter([
    {
      path: '/',
      element: <Outlet context={{ permissions: ['midia.enviar'], name: 'Marina Alves', roles: ['professor'] }} />,
      children: [{ path: 'videos', element: <VideosAreaRoute /> }],
    },
  ], { initialEntries: ['/videos'] });

  renderWithProviders(<RouterProvider router={router} />);
};

const videoPage = (videos: VideoPage['data']) => HttpResponse.json({
  data: videos,
  pagination: { page: 1, size: 10, total: videos.length, totalPages: videos.length ? 1 : 0 },
});

afterEach(() => {
  vi.useRealTimers();
});

describe('video preparation failure', () => {
  it('shows a clear reason for every failure and opens a new upload', async () => {
    const user = userEvent.setup();
    server.use(http.get(`${env.API_URL}/api/v1/videos`, () => videoPage([
      { ...videoStatusFixtures.failed, title: 'Aula ilegível', failureReason: 'unreadable-file' },
      { ...videoStatusFixtures.failed, videoId: 'e4955d81-73e4-4064-a1ee-8af816412fb9', title: 'Aula incompatível', failureReason: 'unsupported-format' },
      { ...videoStatusFixtures.failed, videoId: 'f5a66e92-84f5-4175-b2ff-9b09275230ca', title: 'Aula longa', failureReason: 'duration-exceeded' },
      { ...videoStatusFixtures.failed, videoId: 'a6b77fa3-9506-4286-83a0-ac1a386341db', title: 'Aula temporária', failureReason: 'preparation-failed' },
    ])));

    renderVideosRoute();

    expect(await screen.findByText('Arquivo de vídeo ilegível.')).toBeInTheDocument();
    expect(screen.getByText('Formato de vídeo não suportado.')).toBeInTheDocument();
    expect(screen.getByText('Duração acima de 3 horas.')).toBeInTheDocument();
    expect(screen.getByText('Não foi possível preparar este vídeo — envie novamente.')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Enviar Aula ilegível de novo' }));
    expect(screen.getByRole('dialog', { name: 'Enviar vídeo' })).toBeInTheDocument();
  });

  it('uses a safe fallback for an unknown reason and stops polling', async () => {
    vi.useFakeTimers({ toFake: ['setInterval', 'clearInterval'] });
    let requestCount = 0;
    server.use(http.get(`${env.API_URL}/api/v1/videos`, () => {
      requestCount += 1;
      return videoPage([{ ...videoStatusFixtures.failed, failureReason: 'future-internal-code' }]);
    }));

    renderVideosRoute();

    expect(await screen.findByText('Não foi possível preparar este vídeo — envie novamente.')).toBeInTheDocument();
    expect(screen.queryByText('Atualizando automaticamente')).not.toBeInTheDocument();
    await act(async () => { await vi.advanceTimersByTimeAsync(30_000); });

    expect(requestCount).toBe(1);
  });
});
