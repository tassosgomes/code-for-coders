import { act, cleanup, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { delay, http, HttpResponse } from 'msw';
import { createMemoryRouter, Outlet, RouterProvider } from 'react-router';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { VideosAreaRoute } from '@/app/routes/videos-area-route';
import { env } from '@/config/env';
import type { VideoPage } from '@/features/videos/api/get-videos';
import { videoStatusFixtures, videoTranscriptionContentFixture, videoTranscriptionFixtures } from '@/testing/handlers';
import { server } from '@/testing/server';
import { renderWithProviders } from '@/testing/test-utils';

const readableVideo = { ...videoStatusFixtures.ready, transcription: videoTranscriptionFixtures.ready };
const renderRoute = (getVideo: () => VideoPage['data'][number] = () => readableVideo, permissions = ['midia.enviar']) => {
  server.use(http.get(`${env.API_URL}/api/v1/videos`, () => HttpResponse.json({
    data: [getVideo()], pagination: { page: 1, size: 20, total: 1, totalPages: 1 },
  })));
  const router = createMemoryRouter([{
    path: '/', element: <Outlet context={{ permissions, name: 'Marina', roles: ['professor'] }} />,
    children: [{ path: 'videos', element: <VideosAreaRoute /> }],
  }], { initialEntries: ['/videos'] });
  return { router, ...renderWithProviders(<RouterProvider router={router} />) };
};
const openReader = async (user: ReturnType<typeof userEvent.setup>) => {
  await user.click(await screen.findByRole('button', { name: 'Ler transcrição de Aula de exemplo' }));
  return screen.getByRole('dialog', { name: 'Transcrição' });
};
afterEach(() => { cleanup(); vi.useRealTimers(); });

describe('video-transcription-reader', () => {
  it('opens current text with timestamps as a read-only accessible region and renders HTML literally', async () => {
    const user = userEvent.setup();
    const literal = '<img src=x onerror=alert(1)> Private classroom marker';
    const urls: string[] = [];
    server.use(http.get(`${env.API_URL}/api/v1/videos/:videoId/transcription`, ({ request }) => {
      urls.push(request.url);
      return HttpResponse.json({ ...videoTranscriptionContentFixture, durationMs: 4_000_000, segments: [
        { startMs: 0, endMs: 1000, text: literal },
        { startMs: 61_000, endMs: 62_000, text: 'Second minute' },
        { startMs: 3_661_000, endMs: 3_662_000, text: 'Second hour' },
      ] });
    }));
    const { router } = renderRoute();
    const dialog = await openReader(user);
    const text = await within(dialog).findByRole('region', { name: 'Texto da transcrição' });
    expect(text).toHaveAttribute('tabindex', '0');
    expect(within(text).getByText(literal)).toBeInTheDocument();
    expect(text.querySelector('img')).toBeNull();
    expect(within(text).getByText('00:00')).toBeInTheDocument();
    expect(within(text).getByText('01:01')).toBeInTheDocument();
    expect(within(text).getByText('1:01:01')).toBeInTheDocument();
    expect(within(dialog).getByText(/Pronta em .*somente leitura/)).toBeInTheDocument();
    expect(within(dialog).queryByRole('searchbox')).not.toBeInTheDocument();
    expect(within(dialog).queryByRole('textbox')).not.toBeInTheDocument();
    expect(within(dialog).queryByRole('link')).not.toBeInTheDocument();
    expect(text.querySelector('button,[contenteditable]')).toBeNull();
    expect(router.state.location.pathname).toBe('/videos');
    expect(router.state.location.search).toBe('');
    expect(urls).toEqual([`${env.API_URL}/api/v1/videos/${readableVideo.videoId}/transcription`]);
    expect(localStorage.length).toBe(0); expect(sessionStorage.length).toBe(0);
  });

  it('shows loading placeholders and lets the professor close during loading', async () => {
    const user = userEvent.setup();
    server.use(http.get(`${env.API_URL}/api/v1/videos/:videoId/transcription`, async () => {
      await delay(200); return HttpResponse.json(videoTranscriptionContentFixture);
    }));
    renderRoute();
    const opener = await screen.findByRole('button', { name: 'Ler transcrição de Aula de exemplo' });
    const dialog = await openReader(user);
    expect(within(dialog).getByRole('status', { name: 'Carregando transcrição' })).toHaveAttribute('aria-busy', 'true');
    expect(dialog.querySelectorAll('.video-transcription-skeleton')).toHaveLength(8);
    expect(within(dialog).getByText('Carregando a transcrição…')).toBeVisible();
    await user.click(within(dialog).getAllByRole('button', { name: 'Fechar' })[0]!);
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(opener).toHaveFocus();
  });

  it('shows a neutral not-ready state with only close actions on TRANSCRIPTION_NOT_READY', async () => {
    const user = userEvent.setup();
    server.use(http.get(`${env.API_URL}/api/v1/videos/:videoId/transcription`, () => HttpResponse.json({
      code: 'TRANSCRIPTION_NOT_READY', title: 'Internal information',
    }, { status: 409 })));
    renderRoute(); const dialog = await openReader(user);
    expect(await within(dialog).findByRole('alert')).toHaveTextContent('Ainda não há transcrição pronta para este vídeo.');
    expect(within(dialog).queryByRole('button', { name: 'Tentar de novo' })).not.toBeInTheDocument();
    expect(within(dialog).queryByRole('region')).not.toBeInTheDocument();
    expect(within(dialog).queryByText('Internal information')).not.toBeInTheDocument();
    expect(within(dialog).getAllByRole('button').every((button) => button.textContent === 'Fechar' || button.getAttribute('aria-label') === 'Fechar')).toBe(true);
  });

  it('shows an error without internal details and retries the read', async () => {
    const user = userEvent.setup(); let reads = 0;
    server.use(http.get(`${env.API_URL}/api/v1/videos/:videoId/transcription`, () => {
      reads++;
      return reads === 1 ? HttpResponse.json({ code: 'MEDIA_UNAVAILABLE', title: 'Private error details' }, { status: 502 })
        : HttpResponse.json(videoTranscriptionContentFixture);
    }));
    renderRoute(); const dialog = await openReader(user);
    expect(await within(dialog).findByRole('alert')).toHaveTextContent('Não foi possível abrir a transcrição agora.');
    expect(within(dialog).queryByText('Private error details')).not.toBeInTheDocument();
    expect(within(within(dialog).getByRole('alert')).getByRole('button', { name: 'Tentar de novo' })).toBeInTheDocument();
    await user.click(within(dialog).getByRole('button', { name: 'Tentar de novo' }));
    expect(await within(dialog).findByText(videoTranscriptionContentFixture.segments[0]!.text)).toBeInTheDocument();
    expect(reads).toBe(2);
  });

  it('keeps the opened current text when a restart finishes and reads the new revision on reopening', async () => {
    vi.useFakeTimers({ toFake: ['setInterval', 'clearInterval'] });
    const user = userEvent.setup();
    let current: VideoPage['data'][number] = { ...readableVideo, transcription: videoTranscriptionFixtures.restarting };
    let newRevision = false; let reads = 0;
    server.use(http.get(`${env.API_URL}/api/v1/videos/:videoId/transcription`, () => {
      reads++;
      return HttpResponse.json(newRevision ? { ...videoTranscriptionContentFixture, revision: 'tr_00000002',
        segments: [{ startMs: 0, endMs: 1000, text: 'New current text' }],
      } : videoTranscriptionContentFixture);
    }));
    renderRoute(() => current);
    const dialog = await openReader(user);
    expect(await within(dialog).findByText(videoTranscriptionContentFixture.segments[0]!.text)).toBeInTheDocument();
    expect(within(dialog).getByRole('status')).toHaveTextContent('Há uma transcrição nova em andamento. Você está lendo a atual, que os alunos continuam vendo.');
    current = readableVideo; newRevision = true;
    await act(async () => { await vi.advanceTimersByTimeAsync(10_000); });
    await waitFor(() => expect(screen.getByText('Pronta')).toBeInTheDocument());
    expect(within(dialog).getByText(videoTranscriptionContentFixture.segments[0]!.text)).toBeInTheDocument();
    expect(within(dialog).queryByText('New current text')).not.toBeInTheDocument();
    expect(reads).toBe(1);
    await user.keyboard('{Escape}');
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    // Let the inactive query be discarded before opening a new snapshot.
    await act(async () => { await new Promise((resolve) => window.setTimeout(resolve, 0)); });
    const reopened = await openReader(user);
    expect(await within(reopened).findByText('New current text')).toBeInTheDocument();
    expect(within(reopened).queryByRole('status')).not.toBeInTheDocument();
    expect(reads).toBe(2);
  });

  it('allows reading a current transcription after a failed restart', async () => {
    const user = userEvent.setup();
    renderRoute(() => ({ ...readableVideo, transcription: videoTranscriptionFixtures.restartFailed }));
    const dialog = await openReader(user);
    expect(await within(dialog).findByText(videoTranscriptionContentFixture.segments[0]!.text)).toBeInTheDocument();
  });

  it('keeps keyboard focus inside the dialog, includes the text region, and restores the opener on Escape', async () => {
    const user = userEvent.setup(); renderRoute();
    const opener = await screen.findByRole('button', { name: 'Ler transcrição de Aula de exemplo' });
    const dialog = await openReader(user);
    const region = await within(dialog).findByRole('region', { name: 'Texto da transcrição' });
    const close = within(dialog).getAllByRole('button', { name: 'Fechar' });
    expect(close[0]).toHaveFocus();
    await user.tab(); expect(region).toHaveFocus();
    await user.tab(); expect(close[1]).toHaveFocus();
    await user.tab(); expect(close[0]).toHaveFocus();
    await user.tab({ shift: true }); expect(close[1]).toHaveFocus();
    await user.keyboard('{Escape}');
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument(); expect(opener).toHaveFocus();
  });

  it('does not offer reading when there is no current transcription', async () => {
    renderRoute(() => ({ ...readableVideo, transcription: videoTranscriptionFixtures['in-progress'] }));
    expect(await screen.findByText('Transcrevendo')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Ler transcrição/ })).not.toBeInTheDocument();
  });

  it('does not request or expose transcription to a professor without permission', async () => {
    let reads = 0;
    server.use(http.get(`${env.API_URL}/api/v1/videos/:videoId/transcription`, () => { reads++; return HttpResponse.json(videoTranscriptionContentFixture); }));
    renderRoute(() => readableVideo, []);
    expect(await screen.findByRole('heading', { name: 'Esta área não é do seu papel' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Ler transcrição/ })).not.toBeInTheDocument(); expect(reads).toBe(0);
  });
});
