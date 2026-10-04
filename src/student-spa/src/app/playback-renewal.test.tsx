import { setTimeout as wait } from 'node:timers/promises';

import { act, configure, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { MemoryRouter, Route, Routes } from 'react-router';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { env } from '@/config/env';
import { StudentLessonRoute } from '@/app/routes/student-lesson-route';
import { playbackData } from '@/testing/playback-data';
import { simulateVideo } from '@/testing/playback-simulator';
import { server } from '@/testing/server';
import { lessonId } from '@/testing/student-lesson-data';
import { renderWithProviders } from '@/testing/test-utils';

const hls = vi.hoisted(() => ({ loadSource: vi.fn(), attachMedia: vi.fn(), destroy: vi.fn(), stopLoad: vi.fn(),
  config: undefined as { xhrSetup?: (xhr: XMLHttpRequest, url: string) => void; startPosition?: number } | undefined }));
vi.mock('hls.js', () => ({ default: class {
  static isSupported = () => true;
  static Events = { ERROR: 'error' };
  constructor(config: typeof hls.config) { hls.config = config; }
  loadSource = hls.loadSource; attachMedia = hls.attachMedia; destroy = hls.destroy; stopLoad = hls.stopLoad; on = vi.fn();
} }));
const opening = env.API_URL + '/api/v1/lessons/:lessonId/playback-sessions';
const renewal = env.API_URL + '/api/v1/playback-sessions/:sessionId/renewals';
// The default async wrapper waits on a real-time timeout, incompatible with the simulated clock.
configure({ asyncWrapper: (callback) => callback() });
const click = async (element: HTMLElement) => { await act(async () => { await user.click(element); }); };
let media: ReturnType<typeof simulateVideo>;
let user: ReturnType<typeof userEvent.setup>;
let openingCount: number;
let renewed: number[];
let current: ReturnType<typeof playbackData>;

const settle = async () => {
  await act(async () => { await wait(20); await vi.advanceTimersByTimeAsync(0); });
};
const advance = async (milliseconds: number) => {
  await act(async () => { await vi.advanceTimersByTimeAsync(milliseconds); });
  await settle(); await settle();
};
const start = async () => {
  const view = renderWithProviders(<MemoryRouter initialEntries={["/aulas/" + lessonId]}><Routes><Route path="/aulas/:lessonId" element={<StudentLessonRoute />} /></Routes></MemoryRouter>);
  for (let i = 0; i < 30 && !hls.loadSource.mock.calls.length; i++) await settle();
  expect(hls.loadSource).toHaveBeenCalledOnce();
  const video = view.container.querySelector('video');
  if (!video) throw new Error('Expected lesson video');
  return { ...view, video };
};
const playing = () => screen.getByRole('button', { name: 'Pausar' });

beforeEach(() => {
  vi.useFakeTimers({ toFake: ['Date', 'setTimeout', 'clearTimeout', 'setInterval', 'clearInterval'] });
  vi.setSystemTime(new Date('2026-10-03T12:00:00Z'));
  hls.loadSource.mockClear(); hls.attachMedia.mockClear(); hls.destroy.mockClear(); hls.stopLoad.mockClear();
  media = simulateVideo(); user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
  openingCount = 0; renewed = []; current = playbackData();
  server.use(http.post(opening, () => { openingCount++; current = { ...playbackData(), sessionId: openingCount === 1
    ? '00000000-0000-7000-8000-000000000004' : '00000000-0000-7000-8000-000000000005' }; return HttpResponse.json(current, { status: 201 }); }),
  http.post(renewal, ({ request, params }) => {
    expect(request.headers.get('X-CSRF-Token')).toBe('student-session-csrf'); expect(params.sessionId).toBe(current.sessionId);
    renewed.push(Date.now()); current = { ...playbackData(), sessionId: current.sessionId,
      segmentAccess: { query: 'sa=new-' + renewed.length, expiresAt: new Date(Date.now() + 300_000).toISOString() } };
    return HttpResponse.json(current);
  }));
});
afterEach(() => { vi.useRealTimers(); vi.restoreAllMocks(); });

describe('Playback renewal', () => {
  it('renews while playing and switches the segment credential without reloading', async () => {
    await start(); await click(screen.getByRole('button', { name: 'Reproduzir' }));
    await advance(209_000); expect(renewed).toHaveLength(0);
    await advance(1000); expect(renewed).toHaveLength(1); expect(playing()).toBeEnabled();
    // XHR is the browser boundary; assertion inspects the segment request, not player internals.
    const open = vi.fn();
    const xhr = { open, withCredentials: false } as unknown as XMLHttpRequest;
    hls.config?.xhrSetup?.(xhr, 'https://edge.test/tenant/video/hls/480p/segment_00001.ts');
    expect(open).toHaveBeenCalledWith('GET', expect.stringContaining('?sa=new-1'), true);
    expect(hls.loadSource).toHaveBeenCalledOnce(); expect(media.pause).not.toHaveBeenCalled();
  });

  it('plays for twenty minutes with one playlist and no pauses', async () => {
    await start(); await click(screen.getByRole('button', { name: 'Reproduzir' }));
    for (let i = 0; i < 20; i++) await advance(60_000);
    expect(renewed.length).toBeGreaterThanOrEqual(5); expect(playing()).toBeEnabled();
    expect(hls.loadSource).toHaveBeenCalledOnce(); expect(hls.attachMedia).toHaveBeenCalledOnce();
    expect(hls.destroy).not.toHaveBeenCalled(); expect(media.pause).not.toHaveBeenCalled();
  });

  it('never renews while paused and opens a new session at the saved position after expiry', async () => {
    const { video } = await start(); await click(screen.getByRole('button', { name: 'Reproduzir' }));
    video.currentTime = 42; await click(playing()); await advance(301_000);
    expect(renewed).toHaveLength(0); expect(openingCount).toBe(1);
    await click(screen.getByRole('button', { name: 'Reproduzir' })); await settle(); await settle();
    expect(openingCount).toBe(2); expect(hls.loadSource).toHaveBeenLastCalledWith(expect.stringContaining('000000000005/playlist'));
    act(() => video.dispatchEvent(new Event('loadedmetadata')));
    expect(video.currentTime).toBe(42); expect(playing()).toBeEnabled();
  });

  it('resumes a valid paused session and immediately renews when renewAfter has passed', async () => {
    await start(); await click(screen.getByRole('button', { name: 'Reproduzir' }));
    await click(playing()); await advance(220_000); expect(renewed).toHaveLength(0);
    await click(screen.getByRole('button', { name: 'Reproduzir' })); await advance(0);
    expect(renewed).toHaveLength(1); expect(openingCount).toBe(1);
  });

  it('retries every five seconds during a short outage and recovers without interrupting', async () => {
    let calls = 0; const times: number[] = [];
    server.use(http.post(renewal, () => { calls++; times.push(Date.now());
      return calls < 13 ? HttpResponse.json({ code: 'ACCESS_DECISION_UNAVAILABLE' }, { status: 503 }) : HttpResponse.json(playbackData()); }));
    await start(); await click(screen.getByRole('button', { name: 'Reproduzir' })); await advance(210_000);
    for (let i = 0; i < 12; i++) await advance(5000);
    expect(calls).toBe(13); expect(times.slice(1).every((time, i) => time - (times[i] ?? NaN) === 5000)).toBe(true);
    await advance(40_000); expect(playing()).toBeEnabled(); expect(media.pause).not.toHaveBeenCalled();
    expect(hls.loadSource).toHaveBeenCalledOnce();
  });

  it('stops at expiry after a long outage and offers retry without renewing an expired session', async () => {
    let calls = 0;
    server.use(http.post(renewal, () => { calls++; return HttpResponse.json({ code: 'ACCESS_DECISION_UNAVAILABLE' }, { status: 503 }); }));
    await start(); await click(screen.getByRole('button', { name: 'Reproduzir' })); await advance(210_000);
    for (let i = 0; i < 18; i++) await advance(5000);
    expect(screen.getByText('Não foi possível verificar o acesso à aula. Tente de novo.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Tentar de novo' })).toBeEnabled(); expect(media.pause).toHaveBeenCalledOnce();
    const count = calls; await advance(10_000); expect(calls).toBe(count);
  });

  it.each([
    ['no-grant', undefined, 'Você não tem acesso a esta aula.'],
    ['grant-ended', '2026-10-03T03:00:00Z', 'Seu acesso a este curso terminou em 02/10/2026.'],
  ])('stops immediately when access becomes %s', async (reason, accessEndedAt, message) => {
    server.use(http.post(renewal, () => HttpResponse.json({ code: 'ACCESS_DENIED', reason, accessEndedAt }, { status: 403 })));
    await start(); await click(screen.getByRole('button', { name: 'Reproduzir' })); await advance(210_000);
    expect(screen.getByText(message)).toBeInTheDocument(); expect(media.pause).toHaveBeenCalledOnce();
    expect(screen.getByRole('button', { name: 'Reproduzir' })).toBeDisabled(); expect(hls.stopLoad).toHaveBeenCalledOnce();
  });

  it('offers a fresh session after a 410 and preserves the video position', async () => {
    server.use(http.post(renewal, () => HttpResponse.json({ code: 'PLAYBACK_SESSION_EXPIRED' }, { status: 410 })));
    const { video } = await start(); await click(screen.getByRole('button', { name: 'Reproduzir' })); video.currentTime = 65;
    await advance(210_000); expect(screen.getByText('A sessão de reprodução terminou.')).toBeInTheDocument();
    await click(screen.getByRole('button', { name: 'Tentar de novo' })); await settle(); await settle();
    act(() => video.dispatchEvent(new Event('loadedmetadata')));
    expect(openingCount).toBe(2); expect(video.currentTime).toBe(65); expect(playing()).toBeEnabled();
  });

  it('ignores a pending renewal response after pause', async () => {
    let release: (() => void) | undefined;
    server.use(http.post(renewal, async () => {
      await new Promise<void>((resolve) => { release = resolve; }); return HttpResponse.json(playbackData()); }));
    await start(); await click(screen.getByRole('button', { name: 'Reproduzir' })); await advance(210_000);
    expect(release).toBeDefined(); await click(playing());
    release?.(); await settle(); await advance(100_000);
    expect(screen.queryByText('A sessão de reprodução terminou.')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Reproduzir' })).toBeEnabled(); expect(openingCount).toBe(1);
  });
});
