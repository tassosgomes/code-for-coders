import { act, cleanup, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
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
const readyVideo = (transcription: VideoTranscription): Video => ({ ...videoStatusFixtures.ready, transcription });
const page = (data: Video[]): VideoPage => ({ data, pagination: { page: 1, size: 20, total: data.length, totalPages: data.length ? 1 : 0 } });
const renderRoute = () => {
  const router = createMemoryRouter([{
    path: '/', element: <Outlet context={{ permissions: ['midia.enviar'], name: 'Marina', roles: ['professor'] }} />,
    children: [{ path: 'videos', element: <VideosAreaRoute /> }],
  }], { initialEntries: ['/videos'] });
  return renderWithProviders(<RouterProvider router={router} />);
};
const listVideos = (getData: () => Video[]) => {
  server.use(http.get(`${env.API_URL}/api/v1/videos`, () => HttpResponse.json(page(getData()))));
};
const cell = () => screen.getByRole('group', { name: 'Transcrição de Aula de exemplo' });
const restartRoute = (getResponse: (request: Request) => Response | Promise<Response>) => {
  const requests: { headers: Headers; body: string }[] = [];
  server.use(http.post(`${env.API_URL}/api/v1/videos/:videoId/transcription-requests`, async ({ request }) => {
    requests.push({ headers: request.headers, body: await request.clone().text() });
    return getResponse(request);
  }));
  return requests;
};

beforeEach(() => { setCsrfToken('csrf-restart-test'); });
afterEach(() => { cleanup(); vi.useRealTimers(); clearCsrfToken(); });

describe('video-transcription-restart', () => {
  it.each([
    ['duration-limit', 'Vídeo mais longo que o limite de 2 horas. Refazer não resolve: envie um vídeo mais curto.', false],
    ['size-limit', 'Vídeo maior que o tamanho aceito para transcrição. Refazer não resolve: envie um vídeo menor.', false],
    ['service-unavailable', 'Serviço de transcrição indisponível no momento.', true],
    ['processing-failed', 'Não foi possível transcrever este vídeo.', true],
    ['timed-out', 'A transcrição demorou mais que o limite.', true],
  ])('shows the teacher text for %s and offers Refazer only when the server allows it', async (reason, message, offersRestart) => {
    listVideos(() => [readyVideo({ ...videoTranscriptionFixtures.failed, failureReason: reason, canStart: offersRestart })]);
    renderRoute();
    expect(await within(await screen.findByRole('group', { name: 'Transcrição de Aula de exemplo' })).findByText('Falhou')).toBeInTheDocument();
    expect(within(cell()).getByText(message)).toBeInTheDocument();
    expect(screen.queryByText(reason)).not.toBeInTheDocument();
    if (offersRestart) expect(within(cell()).getByRole('button', { name: 'Refazer Aula de exemplo' })).toBeInTheDocument();
    else expect(within(cell()).queryByRole('button')).not.toBeInTheDocument();
  });

  it('asks before replacing the text students read, cancels without a request and confirms with CSRF and an idempotency key', async () => {
    const user = userEvent.setup();
    listVideos(() => [readyVideo(videoTranscriptionFixtures.ready)]);
    const requests = restartRoute(() => HttpResponse.json(readyVideo(videoTranscriptionFixtures.restarting), { status: 202 }));
    renderRoute();
    await user.click(await screen.findByRole('button', { name: 'Refazer Aula de exemplo' }));
    const dialog = screen.getByRole('alertdialog', { name: 'Refazer a transcrição?' });
    expect(dialog).toHaveTextContent('Os alunos continuam vendo a transcrição atual até a nova ficar pronta.');
    await user.click(within(dialog).getByRole('button', { name: 'Cancelar' }));
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
    expect(requests).toHaveLength(0);
    await user.click(screen.getByRole('button', { name: 'Refazer Aula de exemplo' }));
    await user.click(within(screen.getByRole('alertdialog')).getByRole('button', { name: 'Refazer' }));
    expect(await within(cell()).findByText('Transcrevendo')).toBeInTheDocument();
    expect(within(cell()).getByText('A anterior continua valendo para os alunos.')).toBeInTheDocument();
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Refazer/ })).not.toBeInTheDocument();
    expect(requests).toHaveLength(1);
    expect(requests[0]?.headers.get('X-CSRF-Token')).toBe('csrf-restart-test');
    expect(requests[0]?.headers.get('Idempotency-Key')).toMatch(/^[\da-f-]{36}$/);
    expect(requests[0]?.body).toBe('');
    expect(cell().querySelector('.video-transcription-state')).toHaveFocus();
    expect(screen.getByText('Transcrição pedida')).toBeInTheDocument();
  });

  it('restarts a failed transcription that never had text without asking for confirmation', async () => {
    const user = userEvent.setup();
    listVideos(() => [readyVideo(videoTranscriptionFixtures.failed)]);
    const requests = restartRoute(() => HttpResponse.json(readyVideo(videoTranscriptionFixtures['in-progress']), { status: 202 }));
    renderRoute();
    await user.click(await screen.findByRole('button', { name: 'Refazer Aula de exemplo' }));
    expect(await within(cell()).findByText('Transcrevendo')).toBeInTheDocument();
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
    expect(within(cell()).getByText('Pode levar um tempo. Você não precisa ficar nesta tela.')).toBeInTheDocument();
    expect(within(cell()).queryByText('A anterior continua valendo para os alunos.')).not.toBeInTheDocument();
    expect(requests).toHaveLength(1);
  });

  it('shows a failed restart with its reason, tells that the previous text still stands, and keeps Refazer', async () => {
    listVideos(() => [readyVideo(videoTranscriptionFixtures.restartFailed)]);
    renderRoute();
    expect(await within(await screen.findByRole('group', { name: 'Transcrição de Aula de exemplo' })).findByText('Falhou')).toBeInTheDocument();
    expect(within(cell()).getByText('Serviço de transcrição indisponível no momento.')).toBeInTheDocument();
    expect(within(cell()).getByText('A anterior continua valendo para os alunos.')).toBeInTheDocument();
    expect(within(cell()).getByRole('button', { name: 'Refazer Aula de exemplo' })).toBeInTheDocument();
  });

  it('shows a limit reason with the previous text still valid and no Refazer', async () => {
    listVideos(() => [readyVideo({ ...videoTranscriptionFixtures.restartFailed, failureReason: 'duration-limit', canStart: false })]);
    renderRoute();
    expect(await within(await screen.findByRole('group', { name: 'Transcrição de Aula de exemplo' })).findByText(/^Vídeo mais longo que o limite de 2 horas/)).toBeInTheDocument();
    expect(within(cell()).getByText('A anterior continua valendo para os alunos.')).toBeInTheDocument();
    expect(within(cell()).queryByRole('button')).not.toBeInTheDocument();
  });

  it('closes the confirmation on a refusal, shows the message in the cell and keeps the earlier state', async () => {
    const user = userEvent.setup();
    listVideos(() => [readyVideo(videoTranscriptionFixtures.ready)]);
    restartRoute(() => HttpResponse.json({ code: 'TRANSCRIPTION_NOT_RETRYABLE', title: 'Internal details must stay private' }, { status: 409 }));
    renderRoute();
    await user.click(await screen.findByRole('button', { name: 'Refazer Aula de exemplo' }));
    await user.click(within(screen.getByRole('alertdialog')).getByRole('button', { name: 'Refazer' }));
    expect(await within(cell()).findByRole('alert')).toHaveTextContent('Refazer não resolve este caso. Envie um vídeo mais curto ou menor.');
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
    expect(within(cell()).getByText('Pronta')).toBeInTheDocument();
    expect(screen.queryByText('Internal details must stay private')).not.toBeInTheDocument();
    expect(screen.queryByText('Transcrição pedida')).not.toBeInTheDocument();
  });

  it('retries after a network failure with the same key and without asking again', async () => {
    const user = userEvent.setup(); let attempts = 0;
    listVideos(() => [readyVideo(videoTranscriptionFixtures.ready)]);
    const requests = restartRoute(() => {
      attempts++;
      return attempts === 1 ? HttpResponse.error() : HttpResponse.json(readyVideo(videoTranscriptionFixtures.restarting), { status: 202 });
    });
    renderRoute();
    await user.click(await screen.findByRole('button', { name: 'Refazer Aula de exemplo' }));
    await user.click(within(screen.getByRole('alertdialog')).getByRole('button', { name: 'Refazer' }));
    expect(await within(cell()).findByRole('alert')).toHaveTextContent('Não foi possível pedir a transcrição agora. Tente de novo.');
    await user.click(screen.getByRole('button', { name: 'Tentar de novo a transcrição de Aula de exemplo' }));
    expect(await within(cell()).findByText('Transcrevendo')).toBeInTheDocument();
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
    expect(requests).toHaveLength(2);
    expect(requests[1]?.headers.get('Idempotency-Key')).toBe(requests[0]?.headers.get('Idempotency-Key'));
  });

  it('announces once, with the teacher text, when a restart ends in failure', async () => {
    vi.useFakeTimers({ toFake: ['setInterval', 'clearInterval'] });
    let current = readyVideo(videoTranscriptionFixtures.restarting);
    listVideos(() => [current]);
    renderRoute();
    expect(await screen.findByText('Transcrevendo')).toBeInTheDocument();
    current = readyVideo(videoTranscriptionFixtures.restartFailed);
    await act(async () => { await vi.advanceTimersByTimeAsync(10_000); });
    expect(await within(cell()).findByText('Falhou')).toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveTextContent('Aula de exemplo: a transcrição falhou. Serviço de transcrição indisponível no momento.');
    expect(within(cell()).getByText('A anterior continua valendo para os alunos.')).toBeInTheDocument();
  });
});
