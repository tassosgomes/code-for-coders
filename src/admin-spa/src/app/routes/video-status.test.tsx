import { act, screen } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { createMemoryRouter, Outlet, RouterProvider } from 'react-router';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { VideosAreaRoute } from '@/app/routes/videos-area-route';
import { env } from '@/config/env';
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

const videoPage = (video: typeof videoStatusFixtures[keyof typeof videoStatusFixtures]) => ({
  data: [video],
  pagination: { page: 1, size: 10, total: 1, totalPages: 1 },
});

afterEach(() => {
  vi.useRealTimers();
});

describe('video preparation status', () => {
  it('updates the row to ready with its duration and stops polling', async () => {
    vi.useFakeTimers({ toFake: ['setInterval', 'clearInterval'] });
    let requestCount = 0;
    const states = [videoStatusFixtures.received, videoStatusFixtures.preparing, videoStatusFixtures.ready];
    server.use(http.get(`${env.API_URL}/api/v1/videos`, () => {
      const video = states[Math.min(requestCount, states.length - 1)]!;
      requestCount += 1;
      return HttpResponse.json(videoPage(video));
    }));

    renderVideosRoute();

    expect(await screen.findByText('Recebido')).toBeInTheDocument();
    expect(screen.getByText('Atualizando automaticamente enquanto há vídeo em andamento')).toBeInTheDocument();
    const announcement = screen.getByRole('status');
    expect(announcement).toHaveAttribute('aria-live', 'polite');
    expect(announcement).toBeEmptyDOMElement();
    await act(async () => { await vi.advanceTimersByTimeAsync(10_000); });
    expect(screen.getByText('Em preparação')).toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveTextContent('Aula de exemplo: Em preparação');
    await act(async () => { await vi.advanceTimersByTimeAsync(10_000); });
    expect(screen.getByText('Pronto')).toBeInTheDocument();
    expect(screen.getByText('0:20')).toBeInTheDocument();
    expect(screen.getAllByRole('status')).toHaveLength(1);
    expect(screen.getByRole('status')).toHaveTextContent('Aula de exemplo: Pronto');
    expect(screen.queryByText('Atualizando automaticamente')).not.toBeInTheDocument();
    await act(async () => { await vi.advanceTimersByTimeAsync(30_000); });

    expect(requestCount).toBe(3);
  });

  it('does not refetch terminal videos', async () => {
    vi.useFakeTimers({ toFake: ['setInterval', 'clearInterval'] });
    let requestCount = 0;
    server.use(http.get(`${env.API_URL}/api/v1/videos`, () => {
      requestCount += 1;
      return HttpResponse.json(videoPage(videoStatusFixtures.ready));
    }));

    renderVideosRoute();

    expect(await screen.findByText('Pronto')).toBeInTheDocument();
    expect(screen.getByText('0:20')).toBeInTheDocument();
    await act(async () => { await vi.advanceTimersByTimeAsync(30_000); });

    expect(requestCount).toBe(1);
  });

  it('does not refetch while the tab is hidden', async () => {
    vi.useFakeTimers({ toFake: ['setInterval', 'clearInterval'] });
    const visibility = Object.getOwnPropertyDescriptor(document, 'visibilityState');
    Object.defineProperty(document, 'visibilityState', { configurable: true, value: 'hidden' });
    let requestCount = 0;
    server.use(http.get(`${env.API_URL}/api/v1/videos`, () => {
      requestCount += 1;
      return HttpResponse.json(videoPage(videoStatusFixtures.received));
    }));

    try {
      renderVideosRoute();
      expect(await screen.findByText('Recebido')).toBeInTheDocument();
      await act(async () => { await vi.advanceTimersByTimeAsync(20_000); });
      expect(requestCount).toBe(1);
    } finally {
      if (visibility) Object.defineProperty(document, 'visibilityState', visibility);
      else Reflect.deleteProperty(document, 'visibilityState');
    }
  });
});
