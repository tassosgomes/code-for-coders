import { act, cleanup, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { delay, http, HttpResponse } from 'msw';
import { createMemoryRouter, Outlet, RouterProvider } from 'react-router';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { VideosAreaRoute } from '@/app/routes/videos-area-route';
import { env } from '@/config/env';
import type { VideoPage, VideoTranscription } from '@/features/videos/api/get-videos';
import { clearCsrfToken, setCsrfToken } from '@/lib/api-client';
import { videoStatusFixtures, videoTranscriptionFixtures } from '@/testing/handlers';
import { server } from '@/testing/server';
import { renderWithProviders } from '@/testing/test-utils';

type Video = VideoPage['data'][number];
const readyVideo = (transcription: VideoTranscription = videoTranscriptionFixtures['not-requested']): Video => ({
  ...videoStatusFixtures.ready, transcription,
});
const page = (data: Video[]): VideoPage => ({ data, pagination: { page: 1, size: 20, total: data.length, totalPages: data.length ? 1 : 0 } });
const renderRoute = (entry = '/videos', permissions = ['midia.enviar']) => {
  const router = createMemoryRouter([{
    path: '/', element: <Outlet context={{ permissions, name: 'Marina', roles: ['professor'] }} />,
    children: [{ path: 'videos', element: <VideosAreaRoute /> }],
  }], { initialEntries: [entry] });
  return { router, ...renderWithProviders(<RouterProvider router={router} />) };
};
const listVideos = (getData: () => Video[], requests: URLSearchParams[] = []) => {
  server.use(http.get(`${env.API_URL}/api/v1/videos`, ({ request }) => {
    requests.push(new URL(request.url).searchParams);
    return HttpResponse.json(page(getData()));
  }));
};
const transcriptionCell = (title = 'Aula de exemplo') => screen.getByRole('group', { name: `Transcrição de ${title}` });
const advancePoll = async (milliseconds = 10_000) => {
  await act(async () => { await vi.advanceTimersByTimeAsync(milliseconds); });
};

beforeEach(() => { setCsrfToken('csrf-transcription-test'); });
afterEach(() => { cleanup(); vi.useRealTimers(); clearCsrfToken(); });

describe('video-transcription-status', () => {
  it('shows server transcription states beside preparation and tags', async () => {
    listVideos(() => [
      { ...readyVideo(), title: 'Sem pedido', tags: ['Docker'] },
      { ...readyVideo(videoTranscriptionFixtures['in-progress']), videoId: '0198dfac-674a-7000-8000-000000000052', title: 'Em transcrição' },
      { ...readyVideo(videoTranscriptionFixtures.ready), videoId: '0198dfac-674a-7000-8000-000000000053', title: 'Concluída' },
    ]);
    renderRoute();
    expect(await screen.findByText('Sem pedido')).toBeInTheDocument();
    expect(within(transcriptionCell('Sem pedido')).getByText('Sem transcrição')).toBeInTheDocument();
    expect(within(transcriptionCell('Em transcrição')).getByText('Transcrevendo')).toBeInTheDocument();
    expect(within(transcriptionCell('Concluída')).getByText('Pronta')).toBeInTheDocument();
    expect(within(transcriptionCell('Concluída')).getByText(/^Pronta em /)).toBeInTheDocument();
    expect(screen.getAllByText('Pronto')).toHaveLength(3);
    expect(screen.getByLabelText('Tags de Sem pedido')).toHaveTextContent('Docker');
    expect(screen.queryByRole('button', { name: /Ler/ })).not.toBeInTheDocument();
  });

  it('requests without a body, sends CSRF and idempotency, and uses the accepted server row without reloading', async () => {
    const user = userEvent.setup();
    let headers: Headers | undefined;
    let body: string | undefined;
    const lists: URLSearchParams[] = [];
    listVideos(() => [readyVideo()], lists);
    server.use(http.post(`${env.API_URL}/api/v1/videos/:videoId/transcription-requests`, async ({ request }) => {
      headers = request.headers; body = await request.text();
      return HttpResponse.json({ ...readyVideo(videoTranscriptionFixtures['in-progress']), tags: ['Servidor'] }, { status: 202 });
    }));
    const { router } = renderRoute();
    await user.click(await screen.findByRole('button', { name: 'Transcrever Aula de exemplo' }));
    expect(await screen.findByText('Transcrevendo')).toBeInTheDocument();
    expect(headers?.get('Idempotency-Key')).toMatch(/^[\da-f-]{36}$/);
    expect(headers?.get('X-CSRF-Token')).toBe('csrf-transcription-test');
    expect(body).toBe('');
    expect(screen.getByLabelText('Tags de Aula de exemplo')).toHaveTextContent('Servidor');
    expect(screen.queryByRole('button', { name: /^Transcrever / })).not.toBeInTheDocument();
    expect(transcriptionCell().querySelector('.video-transcription-state')).toHaveFocus();
    expect(screen.getByRole('status')).toHaveTextContent('Aula de exemplo: transcrição transcrevendo');
    expect(router.state.location.pathname).toBe('/videos');
    expect(lists).toHaveLength(1);
  });

  it('keeps the previous server state and disables repeat clicks until the request is accepted', async () => {
    const user = userEvent.setup(); let writes = 0;
    listVideos(() => [readyVideo()]);
    server.use(http.post(`${env.API_URL}/api/v1/videos/:videoId/transcription-requests`, async () => {
      writes++; await delay(250);
      return HttpResponse.json(readyVideo(videoTranscriptionFixtures['in-progress']), { status: 202 });
    }));
    renderRoute();
    const action = await screen.findByRole('button', { name: 'Transcrever Aula de exemplo' });
    await user.dblClick(action);
    expect(action).toBeDisabled();
    expect(within(transcriptionCell()).getByText('Sem transcrição')).toBeInTheDocument();
    expect(await screen.findByText('Transcrevendo')).toBeInTheDocument();
    expect(writes).toBe(1);
  });

  it.each(['received', 'preparing', 'failed'] as const)('offers no transcription action for a %s video even when canStart is true', async (status) => {
    listVideos(() => [{ ...videoStatusFixtures[status], transcription: videoTranscriptionFixtures['not-requested'] }]);
    renderRoute();
    expect(await screen.findByText('Aula de exemplo')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /^Transcrever / })).not.toBeInTheDocument();
    if (status === 'failed') expect(within(transcriptionCell()).getByText('Sem transcrição: o vídeo não ficou pronto')).toBeInTheDocument();
    else expect(within(transcriptionCell()).getByText('Começa sozinha quando o vídeo ficar pronto.')).toBeInTheDocument();
  });

  it('offers no action when the server disables transcription', async () => {
    listVideos(() => [readyVideo({ ...videoTranscriptionFixtures['not-requested'], canStart: false })]);
    renderRoute();
    expect(await screen.findByText('Sem transcrição')).toBeInTheDocument();
    expect(screen.getByText('Transcrição indisponível neste ambiente.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /^Transcrever / })).not.toBeInTheDocument();
  });

  it('shows a failed transcription with a generic reason for a value the screen does not know, hiding the code', async () => {
    listVideos(() => [readyVideo({ ...videoTranscriptionFixtures.failed, failureReason: 'future-service-reason' })]);
    renderRoute();
    expect(await screen.findByText('Aula de exemplo')).toBeInTheDocument();
    expect(within(transcriptionCell()).getByText('Falhou')).toBeInTheDocument();
    expect(within(transcriptionCell()).getByText('Não foi possível transcrever este vídeo.')).toBeInTheDocument();
    expect(screen.queryByText('future-service-reason')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Refazer Aula de exemplo' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Transcrever|Ler/ })).not.toBeInTheDocument();
  });

  it('polls after an accepted request until ready, announces completion, and stops', async () => {
    vi.useFakeTimers({ toFake: ['setInterval', 'clearInterval'] });
    const user = userEvent.setup(); let current = readyVideo(); const lists: URLSearchParams[] = [];
    listVideos(() => [current], lists);
    server.use(http.post(`${env.API_URL}/api/v1/videos/:videoId/transcription-requests`, () => {
      current = readyVideo(videoTranscriptionFixtures['in-progress']);
      return HttpResponse.json(current, { status: 202 });
    }));
    renderRoute();
    await user.click(await screen.findByRole('button', { name: 'Transcrever Aula de exemplo' }));
    expect(await screen.findByText('Transcrevendo')).toBeInTheDocument();
    current = readyVideo(videoTranscriptionFixtures.ready);
    await advancePoll();
    expect(await screen.findByText('Pronta')).toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveTextContent('Aula de exemplo: transcrição pronta');
    expect(screen.queryByText(/Atualizando automaticamente/)).not.toBeInTheDocument();
    await advancePoll(30_000);
    expect(lists).toHaveLength(2);
  });

  it('keeps polling while another row still has a transcription in progress', async () => {
    vi.useFakeTimers({ toFake: ['setInterval', 'clearInterval'] });
    const lists: URLSearchParams[] = [];
    let first = readyVideo(videoTranscriptionFixtures['in-progress']);
    let second = { ...first, videoId: '0198dfac-674a-7000-8000-000000000052', title: 'Outra aula' };
    listVideos(() => [first, second], lists); renderRoute();
    expect(await screen.findAllByText('Transcrevendo')).toHaveLength(2);
    first = readyVideo(videoTranscriptionFixtures.ready);
    await advancePoll();
    expect(await screen.findByText('Pronta')).toBeInTheDocument();
    expect(screen.getByText('Transcrevendo')).toBeInTheDocument();
    second = { ...second, transcription: videoTranscriptionFixtures.ready };
    await advancePoll();
    expect(await screen.findAllByText('Pronta')).toHaveLength(2);
    await advancePoll(30_000);
    expect(lists).toHaveLength(3);
  });

  it('preserves preparation filter, title search, chosen tags and matching mode during automatic updates', async () => {
    vi.useFakeTimers({ toFake: ['setInterval', 'clearInterval'] });
    const lists: URLSearchParams[] = [];
    let current = { ...readyVideo(videoTranscriptionFixtures['in-progress']), tags: ['Docker', 'Módulo 2'] };
    listVideos(() => [current], lists);
    const entry = '/videos?status=ready&q=Aula&tag=Docker&tag=M%C3%B3dulo+2&tagMatch=any';
    const { router } = renderRoute(entry);
    expect(await screen.findByText('Transcrevendo')).toBeInTheDocument();
    current = { ...current, transcription: videoTranscriptionFixtures.ready };
    await advancePoll();
    expect(await screen.findByText('Pronta')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Prontos' })).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByRole('searchbox', { name: 'Buscar título' })).toHaveValue('Aula');
    expect(screen.getByRole('button', { name: 'Remover tag Docker' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Remover tag Módulo 2' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'OU' })).toHaveAttribute('aria-pressed', 'true');
    expect(router.state.location.search).toBe(entry.slice('/videos'.length));
    expect(lists).toHaveLength(2);
    for (const params of lists) {
      expect(params.getAll('status')).toEqual(['ready']); expect(params.get('q')).toBe('Aula');
      expect(params.getAll('tag')).toEqual(['Docker', 'Módulo 2']); expect(params.get('tagMatch')).toBe('any');
    }
  });

  it.each([
    [409, 'VIDEO_NOT_READY', 'Este vídeo ainda não está pronto para ser transcrito.'],
    [409, 'TRANSCRIPTION_NOT_RETRYABLE', 'Refazer não resolve este caso. Envie um vídeo mais curto ou menor.'],
    [403, 'PERMISSION_DENIED', 'Você não tem permissão para pedir a transcrição.'],
    [403, 'CSRF_INVALID', 'Sua sessão mudou. Recarregue a página e tente de novo.'],
  ])('shows the message for %s %s inline and preserves the previous state', async (status, code, message) => {
    const user = userEvent.setup(); const lists: URLSearchParams[] = [];
    listVideos(() => [readyVideo()], lists);
    server.use(http.post(`${env.API_URL}/api/v1/videos/:videoId/transcription-requests`, () => HttpResponse.json({ code, title: 'Internal details must stay private' }, { status: Number(status) })));
    renderRoute();
    await user.click(await screen.findByRole('button', { name: 'Transcrever Aula de exemplo' }));
    expect(await within(transcriptionCell()).findByRole('alert')).toHaveTextContent(String(message));
    expect(screen.getByText('Sem transcrição')).toBeInTheDocument();
    expect(screen.queryByText('Internal details must stay private')).not.toBeInTheDocument();
    expect(screen.queryByText('Transcrevendo')).not.toBeInTheDocument();
    expect(lists).toHaveLength(2);
  });

  it('reuses the idempotency key after a network failure and accepts the actual server state on retry', async () => {
    const user = userEvent.setup(); const keys: (string | null)[] = [];
    listVideos(() => [readyVideo()]);
    server.use(http.post(`${env.API_URL}/api/v1/videos/:videoId/transcription-requests`, ({ request }) => {
      keys.push(request.headers.get('Idempotency-Key'));
      return keys.length === 1 ? HttpResponse.error() : HttpResponse.json(readyVideo(videoTranscriptionFixtures['in-progress']), { status: 202 });
    }));
    renderRoute();
    await user.click(await screen.findByRole('button', { name: 'Transcrever Aula de exemplo' }));
    expect(await within(transcriptionCell()).findByRole('alert')).toHaveTextContent('Não foi possível pedir a transcrição agora. Tente de novo.');
    expect(screen.getByText('Sem transcrição')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Tentar de novo a transcrição de Aula de exemplo' }));
    expect(await screen.findByText('Transcrevendo')).toBeInTheDocument();
    expect(keys).toHaveLength(2); expect(keys[0]).toBeTruthy(); expect(keys[1]).toBe(keys[0]);
    expect(within(transcriptionCell()).queryByRole('alert')).not.toBeInTheDocument();
  });

  it('retains the row and inline request error when the recovery list also fails', async () => {
    const user = userEvent.setup(); let recoveryFails = false;
    server.use(
      http.get(`${env.API_URL}/api/v1/videos`, () => recoveryFails
        ? HttpResponse.error() : HttpResponse.json(page([readyVideo()]))),
      http.post(`${env.API_URL}/api/v1/videos/:videoId/transcription-requests`, () => {
        recoveryFails = true; return HttpResponse.error();
      }),
    );
    renderRoute();
    await user.click(await screen.findByRole('button', { name: 'Transcrever Aula de exemplo' }));
    expect(await within(transcriptionCell()).findByRole('alert')).toHaveTextContent('Não foi possível pedir a transcrição agora.');
    expect(screen.getByText('Aula de exemplo')).toBeInTheDocument();
    expect(screen.getByText('Sem transcrição')).toBeInTheDocument();
  });

  it('does not poll a ready transcription', async () => {
    vi.useFakeTimers({ toFake: ['setInterval', 'clearInterval'] }); const lists: URLSearchParams[] = [];
    listVideos(() => [readyVideo(videoTranscriptionFixtures.ready)], lists); renderRoute();
    expect(await screen.findByText('Pronta')).toBeInTheDocument();
    await advancePoll(30_000); expect(lists).toHaveLength(1);
  });

  it('does not poll an in-progress transcription while the tab is hidden', async () => {
    vi.useFakeTimers({ toFake: ['setInterval', 'clearInterval'] });
    const visibility = Object.getOwnPropertyDescriptor(document, 'visibilityState');
    Object.defineProperty(document, 'visibilityState', { configurable: true, value: 'hidden' });
    try {
      const lists: URLSearchParams[] = []; listVideos(() => [readyVideo(videoTranscriptionFixtures['in-progress'])], lists);
      renderRoute(); expect(await screen.findByText('Transcrevendo')).toBeInTheDocument();
      await advancePoll(30_000); expect(lists).toHaveLength(1);
    } finally {
      if (visibility) Object.defineProperty(document, 'visibilityState', visibility);
      else Reflect.deleteProperty(document, 'visibilityState');
    }
  });

  it('keeps the area forbidden without midia.enviar and makes no list request', async () => {
    const lists: URLSearchParams[] = []; listVideos(() => [readyVideo()], lists);
    renderRoute('/videos', []);
    expect(await screen.findByRole('heading', { name: 'Esta área não é do seu papel' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /^Transcrever / })).not.toBeInTheDocument();
    expect(lists).toHaveLength(0);
  });

  it('rejects unknown fields in the strict transcription schema', async () => {
    server.use(http.get(`${env.API_URL}/api/v1/videos`, () => HttpResponse.json({ ...page([]), data: [
      { ...readyVideo(), transcription: { ...videoTranscriptionFixtures['not-requested'], internalState: 'private' } },
    ] })));
    renderRoute();
    await waitFor(() => expect(screen.getByRole('alert')).toHaveTextContent('Não conseguimos carregar os vídeos agora.'));
    expect(screen.queryByRole('button', { name: /^Transcrever / })).not.toBeInTheDocument();
  });
});
