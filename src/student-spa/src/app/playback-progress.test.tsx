import { setTimeout as wait } from 'node:timers/promises';

import { act, configure, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { MemoryRouter, Route, Routes } from 'react-router';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { StudentLessonRoute } from '@/app/routes/student-lesson-route';
import { env } from '@/config/env';
import { playbackData } from '@/testing/playback-data';
import { simulateVideo } from '@/testing/playback-simulator';
import { server } from '@/testing/server';
import { lessonId } from '@/testing/student-lesson-data';
import { renderWithProviders } from '@/testing/test-utils';

const hls = vi.hoisted(() => ({
  loadSource: vi.fn(),
  attachMedia: vi.fn(),
  destroy: vi.fn(),
  stopLoad: vi.fn(),
  config: undefined as { xhrSetup?: (xhr: XMLHttpRequest, url: string) => void; startPosition?: number } | undefined,
}));

vi.mock('hls.js', () => ({
  default: class {
    static isSupported = () => true;
    static Events = { ERROR: 'error' };
    constructor(config: typeof hls.config) {
      hls.config = config;
    }
    loadSource = hls.loadSource;
    attachMedia = hls.attachMedia;
    destroy = hls.destroy;
    stopLoad = hls.stopLoad;
    on = vi.fn();
  },
}));

const opening = env.API_URL + '/api/v1/lessons/:lessonId/playback-sessions';
const progressEndpoint = env.API_URL + '/api/v1/playback-sessions/:sessionId/progress';

configure({ asyncWrapper: (callback) => callback() });

const click = async (element: HTMLElement) => {
  await act(async () => {
    await user.click(element);
  });
};

let user: ReturnType<typeof userEvent.setup>;
let progressReports: Array<{ sequence: number; positionSeconds: number; reason: string }>;
let progressResponse: { recorded: boolean; status?: number };

const settle = async () => {
  await act(async () => {
    await wait(20);
    await vi.advanceTimersByTimeAsync(0);
  });
};

const advance = async (milliseconds: number) => {
  await act(async () => {
    await vi.advanceTimersByTimeAsync(milliseconds);
  });
  await settle();
  await settle();
};

const start = async () => {
  const view = renderWithProviders(
    <MemoryRouter initialEntries={['/aulas/' + lessonId]}>
      <Routes>
        <Route path="/aulas/:lessonId" element={<StudentLessonRoute />} />
      </Routes>
    </MemoryRouter>,
  );
  for (let i = 0; i < 30 && !hls.loadSource.mock.calls.length; i++) await settle();
  expect(hls.loadSource).toHaveBeenCalledOnce();
  const video = view.container.querySelector('video');
  if (!video) throw new Error('Expected lesson video');
  return { ...view, video };
};

const playing = () => screen.getByRole('button', { name: 'Pausar' });
const paused = () => screen.getByRole('button', { name: 'Reproduzir' });

beforeEach(() => {
  vi.useFakeTimers({ toFake: ['Date', 'setTimeout', 'clearTimeout', 'setInterval', 'clearInterval'] });
  vi.setSystemTime(new Date('2026-10-03T12:00:00Z'));
  hls.loadSource.mockClear();
  hls.attachMedia.mockClear();
  hls.destroy.mockClear();
  hls.stopLoad.mockClear();
  simulateVideo();
  user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
  progressReports = [];
  progressResponse = { recorded: true };

  server.use(
    http.post(opening, () => HttpResponse.json(playbackData(), { status: 201 })),
    http.post(progressEndpoint, async ({ request }) => {
      const body = (await request.json()) as { sequence: number; positionSeconds: number; reason: string };
      progressReports.push(body);
      return HttpResponse.json({ recorded: progressResponse.recorded }, { status: progressResponse.status ?? 200 });
    }),
  );
});

afterEach(() => {
  vi.useRealTimers();
  vi.restoreAllMocks();
});

describe('Playback progress tracking', () => {
  it('emits heartbeat at configured interval of 30 seconds with monotonic sequence', async () => {
    const { video } = await start();
    await click(paused());

    // Advance 30 seconds
    video.currentTime = 30;
    await advance(30_000);
    expect(progressReports).toHaveLength(1);
    expect(progressReports[0]).toEqual({ sequence: 1, positionSeconds: 30, reason: 'heartbeat' });

    // Advance another 30 seconds
    video.currentTime = 60;
    await advance(30_000);
    expect(progressReports).toHaveLength(2);
    expect(progressReports[1]).toEqual({ sequence: 2, positionSeconds: 60, reason: 'heartbeat' });
  });

  it('emits 20 heartbeats during 10 minutes of continuous playback', async () => {
    const { video } = await start();
    await click(paused());

    for (let i = 1; i <= 20; i++) {
      video.currentTime = i * 30;
      await advance(30_000);
    }

    expect(progressReports).toHaveLength(20);
    expect(progressReports.every((r) => r.reason === 'heartbeat')).toBe(true);
    for (let i = 0; i < 20; i++) {
      expect(progressReports[i]?.sequence).toBe(i + 1);
    }
  });

  it('emits paused event at 252s with exact positionSeconds', async () => {
    const { video } = await start();
    await click(paused());

    video.currentTime = 252;
    await click(playing());

    const pausedReport = progressReports.find((r) => r.reason === 'paused');
    expect(pausedReport).toBeDefined();
    expect(pausedReport?.positionSeconds).toBe(252);
    expect(pausedReport?.sequence).toBe(1);
  });

  it('emits ended event when video reaches the end', async () => {
    const { video } = await start();
    await click(paused());

    video.currentTime = 300;
    await act(async () => {
      video.dispatchEvent(new Event('ended'));
    });
    await settle();

    const endedReport = progressReports.find((r) => r.reason === 'ended');
    expect(endedReport).toBeDefined();
    expect(endedReport?.positionSeconds).toBe(300);
    expect(endedReport?.sequence).toBe(1);
  });

  it('emits left event using fetch with keepalive and csrf header on unmount', async () => {
    const fetchSpy = vi.spyOn(globalThis, 'fetch');
    const view = await start();
    await click(paused());

    view.video.currentTime = 120;
    view.unmount();
    await settle();

    expect(fetchSpy).toHaveBeenCalledWith(
      expect.stringContaining('/progress'),
      expect.objectContaining({
        method: 'POST',
        keepalive: true,
        headers: expect.objectContaining({
          'Content-Type': 'application/json',
          'X-CSRF-Token': 'student-session-csrf',
        }),
        body: JSON.stringify({ sequence: 1, positionSeconds: 120, reason: 'left' }),
      }),
    );
  });

  it('maintains monotonic sequence starting from 1 across heartbeats, pause, and exit', async () => {
    const fetchSpy = vi.spyOn(globalThis, 'fetch');
    const view = await start();
    await click(paused());

    // 1. Heartbeat at 30s
    view.video.currentTime = 30;
    await advance(30_000);
    expect(progressReports).toHaveLength(1);
    expect(progressReports[0]?.sequence).toBe(1);
    expect(progressReports[0]?.reason).toBe('heartbeat');

    // 2. Pause at 45s
    view.video.currentTime = 45;
    await click(playing());
    expect(progressReports).toHaveLength(2);
    expect(progressReports[1]?.sequence).toBe(2);
    expect(progressReports[1]?.reason).toBe('paused');

    // 3. Resume and heartbeat at 75s
    await click(paused());
    view.video.currentTime = 75;
    await advance(30_000);
    expect(progressReports).toHaveLength(3);
    expect(progressReports[2]?.sequence).toBe(3);
    expect(progressReports[2]?.reason).toBe('heartbeat');

    // 4. Exit
    view.unmount();
    await settle();

    expect(fetchSpy).toHaveBeenCalledWith(
      expect.stringContaining('/progress'),
      expect.objectContaining({
        body: JSON.stringify({ sequence: 4, positionSeconds: 75, reason: 'left' }),
      }),
    );
  });

  it('handles recorded: false response gracefully without interrupting playback', async () => {
    progressResponse = { recorded: false };
    const { video } = await start();
    await click(paused());

    video.currentTime = 30;
    await advance(30_000);

    expect(progressReports).toHaveLength(1);
    // Playback continues normally without error banner or pause
    expect(playing()).toBeEnabled();
    expect(screen.queryByText(/não foi possível/i)).not.toBeInTheDocument();
  });
});
