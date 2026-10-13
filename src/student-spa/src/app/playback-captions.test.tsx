import { act, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { MemoryRouter, Route, Routes } from 'react-router';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { StudentLessonRoute } from '@/app/routes/student-lesson-route';
import { env } from '@/config/env';
import { paths } from '@/config/paths';
import { playbackData } from '@/testing/playback-data';
import { simulateCaptions, simulateVideo } from '@/testing/playback-simulator';
import { server } from '@/testing/server';
import { lessonId, lessonTranscriptionData, secondLessonId, studentLessonData } from '@/testing/student-lesson-data';
import { renderWithProviders } from '@/testing/test-utils';

const hls = vi.hoisted(() => ({ loadSource: vi.fn(), destroy: vi.fn(), stopLoad: vi.fn() }));
vi.mock('hls.js', () => ({ default: class {
  static isSupported = () => true;
  static Events = { ERROR: 'error' };
  loadSource = hls.loadSource; destroy = hls.destroy; stopLoad = hls.stopLoad;
  attachMedia = vi.fn(); on = vi.fn();
} }));
const captionsEndpoint = env.API_URL + '/api/v1/playback-sessions/:sessionId/captions';
const opening = env.API_URL + '/api/v1/lessons/:lessonId/playback-sessions';
const renewal = env.API_URL + '/api/v1/playback-sessions/:sessionId/renewals';
const vtt = 'WEBVTT\n\n1\n00:00:00.000 --> 00:00:10.000\nCaption &amp; &lt;script&gt;literal&lt;/script&gt;\n\n2\n00:00:10.000 --> 00:01:00.000\nSecond caption 👩🏽‍💻\n\n';
let captions: ReturnType<typeof simulateCaptions>;
let media: ReturnType<typeof simulateVideo>;
let requests: string[];
const renderLesson = () => renderWithProviders(<MemoryRouter initialEntries={[paths.studentLesson.getHref(lessonId)]}>
  <Routes><Route path={paths.studentLesson.path} element={<StudentLessonRoute />} /></Routes>
</MemoryRouter>);
const start = async () => {
  const view = renderLesson(); await screen.findByRole('button', { name: 'Legenda' });
  const video = view.container.querySelector('video');
  if (!video) throw new Error('Expected the lesson video');
  return { ...view, video };
};
const turnOn = async () => {
  await userEvent.click(screen.getByRole('button', { name: 'Legenda' }));
  await screen.findByText('Caption & <script>literal</script>');
};

beforeEach(() => {
  localStorage.clear(); sessionStorage.clear();
  hls.loadSource.mockClear(); hls.destroy.mockClear(); hls.stopLoad.mockClear();
  media = simulateVideo(); captions = simulateCaptions(); requests = [];
  server.use(http.get(env.API_URL + '/api/v1/lessons/:lessonId/transcription', ({ params }) =>
    HttpResponse.json({ ...lessonTranscriptionData, lessonId: params.lessonId })),
  http.get(captionsEndpoint, ({ request }) => {
    requests.push(request.url); expect(request.headers.get('Accept')).toBe('text/vtt');
    return new HttpResponse(vtt, { headers: { 'Content-Type': 'text/vtt; charset=utf-8', 'Cache-Control': 'private, no-store' } });
  }));
});
afterEach(() => { captions.dispose(); vi.restoreAllMocks(); });

describe('Playback captions', () => {
  it('keeps the control absent without a ready transcription and still plays', async () => {
    localStorage.setItem('lesson-captions-enabled', 'true');
    server.use(http.get(env.API_URL + '/api/v1/lessons/:lessonId/transcription', () =>
      HttpResponse.json({ lessonId, status: 'unavailable', revision: null, language: null, durationMs: null, segments: [] })));
    renderLesson(); await screen.findByTestId('video-watermark');
    expect(screen.queryByRole('button', { name: 'Legenda' })).not.toBeInTheDocument(); expect(requests).toHaveLength(0);
    await userEvent.click(screen.getByRole('button', { name: 'Reproduzir' }));
    expect(screen.getByRole('button', { name: 'Pausar' })).toBeEnabled();
  });

  it('defaults off and loads an authenticated in-memory hidden track without restarting video', async () => {
    const { container } = await start();
    expect(screen.getByRole('button', { name: 'Legenda' })).toHaveAttribute('aria-pressed', 'false');
    expect(requests).toHaveLength(0); await turnOn();
    expect(screen.getByRole('button', { name: 'Legenda' })).toHaveAttribute('aria-pressed', 'true');
    expect(requests).toHaveLength(1); expect(requests[0]).not.toContain('?');
    expect([...captions.tracks.values()][0]?.mode).toBe('hidden');
    expect(container.querySelector('script')).toBeNull();
    expect(screen.getByText('Caption & <script>literal</script>').closest('[aria-hidden]')).toHaveAttribute('aria-hidden', 'true');
    expect(hls.loadSource).toHaveBeenCalledOnce(); expect(media.play).not.toHaveBeenCalled();
    expect(JSON.stringify({ ...localStorage, ...sessionStorage })).not.toContain('Caption');
    expect(JSON.stringify({ ...localStorage, ...sessionStorage })).not.toContain('WEBVTT');
  });

  it('keeps the paused cue and follows forward seek backward seek and playback speed', async () => {
    const { video } = await start(); await turnOn();
    await userEvent.click(screen.getByRole('button', { name: 'Reproduzir' }));
    await userEvent.click(screen.getByRole('button', { name: 'Pausar' }));
    expect(screen.getByText('Caption & <script>literal</script>')).toBeInTheDocument();
    act(() => captions.seek(video, 12)); expect(screen.getByText('Second caption 👩🏽‍💻')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Velocidade de reprodução' }));
    await userEvent.click(screen.getByRole('menuitemradio', { name: '1,5x' }));
    expect(video.playbackRate).toBe(1.5);
    act(() => { captions.seek(video, 3); video.dispatchEvent(new Event('ratechange')); });
    expect(screen.getByText('Caption & <script>literal</script>')).toBeInTheDocument();
    expect(screen.queryByText('Second caption 👩🏽‍💻')).not.toBeInTheDocument(); expect(requests).toHaveLength(1);
  });

  it('turns off and revokes the track while retaining only the device preference', async () => {
    const view = await start(); await turnOn();
    await userEvent.click(screen.getByRole('button', { name: 'Legenda' }));
    expect(screen.queryByText('Caption & <script>literal</script>')).not.toBeInTheDocument();
    expect(localStorage.getItem('lesson-captions-enabled')).toBe('false');
    expect(view.container.querySelector('track')).toBeNull(); expect(captions.revokeObjectURL).toHaveBeenCalledOnce();
    await turnOn(); view.unmount(); expect(captions.revokeObjectURL).toHaveBeenCalledTimes(2);
  });

  it('remembers the choice on the next lesson and releases the previous session track', async () => {
    server.use(http.get(env.API_URL + '/api/v1/lessons/:lessonId', ({ params }) => HttpResponse.json({ ...studentLessonData,
      lesson: { ...studentLessonData.lesson, lessonId: params.lessonId } })),
    http.post(opening, ({ params }) => HttpResponse.json({ ...playbackData(), sessionId: params.lessonId === secondLessonId
      ? '00000000-0000-7000-8000-000000000005' : '00000000-0000-7000-8000-000000000004' }, { status: 201 })));
    await start(); await turnOn();
    await userEvent.click(screen.getByRole('tab', { name: 'Aulas do curso' }));
    await userEvent.click(screen.getByRole('link', { name: '2. Persistência' }));
    await waitFor(() => expect(requests).toHaveLength(2));
    expect(screen.getByRole('button', { name: 'Legenda' })).toHaveAttribute('aria-pressed', 'true');
    expect(captions.revokeObjectURL).toHaveBeenCalledOnce();
    expect(requests[1]).toContain('000000000005/captions');
  });

  it('works when the device denies reading and writing preference storage', async () => {
    const get = Storage.prototype.getItem; const set = Storage.prototype.setItem;
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(function (this: Storage, key: string) {
      if (key === 'lesson-captions-enabled') throw new DOMException('Storage denied', 'SecurityError');
      return get.call(this, key);
    });
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(function (this: Storage, key: string, value: string) {
      if (key === 'lesson-captions-enabled') throw new DOMException('Storage denied', 'SecurityError');
      set.call(this, key, value);
    });
    await start(); await turnOn(); expect(screen.getByRole('button', { name: 'Legenda' })).toHaveAttribute('aria-pressed', 'true');
  });

  it('keeps the same visible cue and track across a successful renewal', async () => {
    let renewals = 0;
    server.use(http.post(opening, () => HttpResponse.json({ ...playbackData(), renewAfter: new Date(Date.now() + 150).toISOString() }, { status: 201 })),
      http.post(renewal, () => { renewals++; return HttpResponse.json(playbackData()); }));
    await start(); await turnOn();
    const visible = screen.getByText('Caption & <script>literal</script>');
    await userEvent.click(screen.getByRole('button', { name: 'Reproduzir' }));
    await waitFor(() => expect(renewals).toBe(1));
    expect(screen.getByText('Caption & <script>literal</script>')).toBe(visible);
    expect(captions.createObjectURL).toHaveBeenCalledOnce(); expect(captions.revokeObjectURL).not.toHaveBeenCalled();
    expect(requests).toHaveLength(1); expect(media.pause).not.toHaveBeenCalled();
  });

  it('clears captions with denied renewal and ignores subsequent cue events', async () => {
    server.use(http.post(opening, () => HttpResponse.json({ ...playbackData(), renewAfter: new Date(Date.now() + 150).toISOString() }, { status: 201 })),
      http.post(renewal, () => HttpResponse.json({ code: 'ACCESS_DENIED' }, { status: 403 })));
    const { video } = await start(); await turnOn();
    await userEvent.click(screen.getByRole('button', { name: 'Reproduzir' }));
    await screen.findByText('Você não tem acesso a esta aula.');
    act(() => { captions.seek(video, 12); for (const track of captions.tracks.values()) track.dispatchEvent(new Event('cuechange')); });
    expect(screen.queryByText('Caption & <script>literal</script>')).not.toBeInTheDocument();
    expect(screen.queryByText('Second caption 👩🏽‍💻')).not.toBeInTheDocument();
    expect(media.pause).toHaveBeenCalledOnce(); expect(hls.stopLoad).toHaveBeenCalledOnce();
    expect(captions.revokeObjectURL).toHaveBeenCalledOnce();
  });

  it('keeps captions during a temporary outage and clears them at session expiry', async () => {
    let calls = 0;
    server.use(http.post(opening, () => HttpResponse.json({ ...playbackData(), renewAfter: new Date(Date.now() + 150).toISOString(),
      expiresAt: new Date(Date.now() + 700).toISOString() }, { status: 201 })),
      http.post(renewal, () => { calls++; return HttpResponse.json({ code: 'ACCESS_DECISION_UNAVAILABLE' }, { status: 503 }); }));
    await start(); await turnOn(); await userEvent.click(screen.getByRole('button', { name: 'Reproduzir' }));
    await waitFor(() => expect(calls).toBe(1)); expect(screen.getByText('Caption & <script>literal</script>')).toBeInTheDocument();
    await screen.findByText('Não foi possível verificar o acesso à aula. Tente de novo.');
    expect(screen.queryByText('Caption & <script>literal</script>')).not.toBeInTheDocument(); expect(media.pause).toHaveBeenCalledOnce();
  });

  it.each([[404, 'CAPTIONS_NOT_AVAILABLE'], [410, 'PLAYBACK_SESSION_EXPIRED'], [503, 'UPSTREAM_UNAVAILABLE']])(
    'announces a neutral captions failure for %s without stopping video', async (status, code) => {
      server.use(http.get(captionsEndpoint, () => HttpResponse.json({ code }, { status: Number(status) })));
      await start(); await userEvent.click(screen.getByRole('button', { name: 'Legenda' }));
      await screen.findByText('A legenda não está disponível agora.');
      expect(screen.queryByRole('button', { name: 'Legenda' })).not.toBeInTheDocument();
      await userEvent.click(screen.getByRole('button', { name: 'Reproduzir' }));
      expect(screen.getByRole('button', { name: 'Pausar' })).toBeEnabled(); expect(hls.stopLoad).not.toHaveBeenCalled();
    });

  it('ignores captions arriving after the session has been stopped', async () => {
    let release: (() => void) | undefined;
    server.use(http.get(captionsEndpoint, async () => {
      await new Promise<void>((resolve) => { release = resolve; }); return new HttpResponse(vtt);
    }), http.post(opening, () => HttpResponse.json({ ...playbackData(), renewAfter: new Date(Date.now() + 150).toISOString() }, { status: 201 })),
    http.post(renewal, () => HttpResponse.json({ code: 'ACCESS_DENIED' }, { status: 403 })));
    await start(); await userEvent.click(screen.getByRole('button', { name: 'Legenda' }));
    await waitFor(() => expect(release).toBeDefined());
    await userEvent.click(screen.getByRole('button', { name: 'Reproduzir' })); await screen.findByText('Você não tem acesso a esta aula.');
    release?.(); await act(async () => { await new Promise((resolve) => setTimeout(resolve, 30)); });
    expect(captions.createObjectURL).not.toHaveBeenCalled(); expect(screen.queryByText('Caption & <script>literal</script>')).not.toBeInTheDocument();
  });

  it('requests fullscreen on the container containing captions and watermark', async () => {
    const request = vi.fn();
    Object.defineProperty(HTMLElement.prototype, 'requestFullscreen', { configurable: true, value: request });
    await start(); await turnOn(); await userEvent.click(screen.getByRole('button', { name: 'Tela cheia' }));
    expect(request).toHaveBeenCalledOnce();
    const player = screen.getByRole('toolbar', { name: 'Player da aula' });
    expect(player).toContainElement(screen.getByText('Caption & <script>literal</script>'));
    expect(player).toContainElement(screen.getByTestId('video-watermark'));
    Reflect.deleteProperty(HTMLElement.prototype, 'requestFullscreen');
  });
});
