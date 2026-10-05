import { setTimeout as wait } from 'node:timers/promises';

import { act, configure, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { MemoryRouter, Route, Routes } from 'react-router';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { StudentLessonRoute } from '@/app/routes/student-lesson-route';
import { env } from '@/config/env';
import { courseProgressData } from '@/testing/course-progress-data';
import { playbackData } from '@/testing/playback-data';
import { simulateVideo } from '@/testing/playback-simulator';
import { server } from '@/testing/server';
import { lessonId, secondLessonId, studentLessonData } from '@/testing/student-lesson-data';
import { renderWithProviders } from '@/testing/test-utils';

const hls = vi.hoisted(() => ({ loadSource: vi.fn(), destroy: vi.fn(), startPosition: 0 }));
vi.mock('hls.js', () => ({ default: class {
  static isSupported = () => true;
  static Events = { ERROR: 'error' };
  constructor(config: { startPosition: number }) { hls.startPosition = config.startPosition; }
  loadSource = hls.loadSource;
  destroy = hls.destroy;
  attachMedia = vi.fn();
  stopLoad = vi.fn();
  on = vi.fn();
} }));

const endpoint = `${env.API_URL}/api/v1/courses/:courseId/progress`;
const recordedEndpoint = `${env.API_URL}/api/v1/playback-sessions/:sessionId/progress`;
const savedProgress = (resume = 252, completed = true) => ({
  ...courseProgressData, completedLessons: completed ? 1 : 0, percent: completed ? 50 : 0,
  lessons: [{ lessonId, completed, lastPositionSeconds: 252, resumeAtSeconds: resume }],
});
let user: ReturnType<typeof userEvent.setup>;
let reads: number;
configure({ asyncWrapper: (callback) => callback() });
const settle = async () => { await act(async () => { await wait(20); await vi.advanceTimersByTimeAsync(0); }); };
const advance = async (milliseconds: number) => {
  await act(async () => { await vi.advanceTimersByTimeAsync(milliseconds); });
  await settle(); await settle();
};
const click = async (element: HTMLElement) => { await act(async () => { await user.click(element); }); await settle(); };
const renderLesson = (known = true) => renderWithProviders(<MemoryRouter initialEntries={[{
  pathname: `/aulas/${lessonId}`, state: known ? { course: studentLessonData.course } : undefined,
}]}><Routes><Route path="/aulas/:lessonId" element={<StudentLessonRoute />} /><Route path="/" element={<h1>Início</h1>} /></Routes></MemoryRouter>);
const start = async (known = true) => {
  const view = renderLesson(known);
  for (let i = 0; i < 40 && !hls.loadSource.mock.calls.length; i++) await settle();
  expect(hls.loadSource).toHaveBeenCalledOnce();
  const video = view.container.querySelector('video');
  if (!video) throw new Error('Expected lesson video');
  Object.defineProperty(video, 'duration', { configurable: true, value: 600 });
  await act(async () => { video.dispatchEvent(new Event('loadedmetadata')); });
  return { ...view, video };
};

beforeEach(() => {
  vi.useFakeTimers({ toFake: ['Date', 'setTimeout', 'clearTimeout', 'setInterval', 'clearInterval'] });
  vi.setSystemTime(new Date('2026-10-03T12:00:00Z'));
  user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
  hls.loadSource.mockClear(); hls.destroy.mockClear(); hls.startPosition = -1;
  localStorage.clear(); sessionStorage.clear(); reads = 0; simulateVideo();
  server.use(
    http.get(endpoint, () => { reads++; return HttpResponse.json(savedProgress()); }),
    http.post(`${env.API_URL}/api/v1/lessons/:lessonId/playback-sessions`, () => HttpResponse.json(playbackData(), { status: 201 })),
  );
});
afterEach(() => { vi.useRealTimers(); vi.restoreAllMocks(); });

describe('Lesson resume and progress', () => {
  it('resumes at the server position with an accessible notice and course count', async () => {
    const { video } = await start();
    expect(hls.startPosition).toBe(252); expect(video.currentTime).toBe(252);
    expect(screen.getByText('Retomando de 4:12').closest('[role="status"]')).toHaveAttribute('aria-live', 'polite');
    expect(screen.getByRole('progressbar', { name: 'Progresso do curso' })).toHaveAttribute('aria-valuenow', '50');
    expect(screen.getByRole('progressbar')).toHaveAttribute('aria-valuetext', '1 de 2 aulas concluídas');
    expect(screen.getByRole('link', { name: /Injeção de dependência.*Concluída/ })).toHaveAttribute('aria-current', 'page');
    expect(screen.getByRole('link', { name: '2. Persistência' })).toBeInTheDocument();
    expect(JSON.stringify({ local: { ...localStorage }, session: { ...sessionStorage } })).not.toMatch(/resumeAtSeconds|completedLessons|lastPositionSeconds/);
  });
  it('starts at zero without a notice when the server applies the restart rule', async () => {
    server.use(http.get(endpoint, () => HttpResponse.json(savedProgress(0))));
    const { video } = await start(); expect(video.currentTime).toBe(0); expect(hls.startPosition).toBe(0);
    expect(screen.queryByText(/Retomando de/)).not.toBeInTheDocument();
  });
  it('starts at zero without a notice for a lesson without a record', async () => {
    server.use(http.get(endpoint, () => HttpResponse.json(courseProgressData)));
    const { video } = await start(); expect(video.currentTime).toBe(0);
    expect(screen.queryByRole('button', { name: 'Começar do início' })).not.toBeInTheDocument();
  });
  it('allows video playback from zero when progress fails and removes progress marks', async () => {
    server.use(http.get(endpoint, () => HttpResponse.json({ code: 'UPSTREAM_UNAVAILABLE' }, { status: 502 })));
    const { video } = await start(); expect(video.currentTime).toBe(0);
    expect(screen.queryByRole('progressbar')).not.toBeInTheDocument(); expect(screen.queryByText('Concluída')).not.toBeInTheDocument();
    await click(screen.getByRole('button', { name: 'Reproduzir' })); expect(screen.getByRole('button', { name: 'Pausar' })).toBeInTheDocument();
  });
  it('waits at most two seconds and never seeks when the response arrives late', async () => {
    let release: (() => void) | undefined;
    const pending = new Promise<void>((resolve) => { release = resolve; });
    server.use(http.get(endpoint, async () => { await pending; return HttpResponse.json(savedProgress()); }));
    const view = renderLesson(); for (let i = 0; i < 6; i++) await settle();
    expect(hls.loadSource).not.toHaveBeenCalled(); await advance(1999); expect(hls.loadSource).not.toHaveBeenCalled();
    await advance(1); for (let i = 0; i < 10 && !hls.loadSource.mock.calls.length; i++) await settle();
    expect(hls.startPosition).toBe(0);
    const video = view.container.querySelector('video')!; video.currentTime = 30;
    release?.(); for (let i = 0; i < 8; i++) await settle();
    expect(screen.getByText('Concluída')).toBeInTheDocument(); expect(video.currentTime).toBe(30);
    expect(hls.loadSource).toHaveBeenCalledOnce(); expect(screen.queryByText(/Retomando de/)).not.toBeInTheDocument();
  });
  it('starts over at zero through the keyboard accessible action', async () => {
    const { video } = await start(); const button = screen.getByRole('button', { name: 'Começar do início' });
    await act(async () => { button.focus(); await user.keyboard('{Enter}'); });
    expect(video.currentTime).toBe(0); expect(screen.getByRole('slider', { name: 'Posição do vídeo' })).toHaveValue('0');
    expect(screen.queryByText(/Retomando de/)).not.toBeInTheDocument();
  });
  it('keeps the resume notice while its action has focus and dismisses it after focus leaves', async () => {
    await start(); const button = screen.getByRole('button', { name: 'Começar do início' });
    act(() => button.focus()); await advance(9000); expect(button).toHaveFocus(); expect(screen.getByText(/Retomando de/)).toBeInTheDocument();
    act(() => screen.getByRole('button', { name: 'Reproduzir' }).focus());
    expect(screen.queryByText(/Retomando de/)).not.toBeInTheDocument();
  });
  it('automatically dismisses the resume notice when it has no focus', async () => {
    await start(); await advance(8000); expect(screen.queryByText(/Retomando de/)).not.toBeInTheDocument();
  });
  it('refreshes after pause and marks the current lesson without reopening playback', async () => {
    server.use(http.get(endpoint, () => { reads++; return HttpResponse.json(savedProgress(252, reads > 1)); }));
    await start(); expect(screen.queryByText('Concluída')).not.toBeInTheDocument();
    await click(screen.getByRole('button', { name: 'Reproduzir' }));
    await click(screen.getByRole('button', { name: 'Pausar' })); for (let i = 0; i < 4; i++) await settle();
    expect(reads).toBe(2); expect(screen.getByText('Concluída')).toBeInTheDocument(); expect(hls.loadSource).toHaveBeenCalledOnce();
  });
  it('refreshes after the video ends', async () => {
    const { video } = await start();
    await act(async () => { video.dispatchEvent(new Event('ended')); }); for (let i = 0; i < 6; i++) await settle();
    expect(reads).toBe(2); expect(hls.loadSource).toHaveBeenCalledOnce();
  });
  it('refreshes every sixty seconds of playback and stops polling when paused', async () => {
    let recordedPosition = 0;
    const readPositions: number[] = [];
    server.use(
      http.post(recordedEndpoint, async ({ request }) => {
        const payload: unknown = await request.json();
        if (payload && typeof payload === 'object' && 'positionSeconds' in payload && typeof payload.positionSeconds === 'number') {
          recordedPosition = payload.positionSeconds;
        }
        return HttpResponse.json({ recorded: true });
      }),
      http.get(endpoint, () => { reads++; readPositions.push(recordedPosition); return HttpResponse.json(savedProgress(252, recordedPosition >= 540)); }),
    );
    const { video } = await start(); expect(screen.queryByText('Concluída')).not.toBeInTheDocument();
    await advance(60_000); expect(reads).toBe(1);
    await click(screen.getByRole('button', { name: 'Reproduzir' })); video.currentTime = 550;
    await advance(59_999); expect(reads).toBe(1); await advance(1); expect(reads).toBe(2);
    expect(recordedPosition).toBe(550);
    expect(readPositions).toEqual([0, 550]);
    // Flush the query notification queued by the interval callback at this clock boundary.
    await advance(1);
    expect(screen.getByText('Concluída')).toBeInTheDocument(); expect(video.currentTime).toBe(550);
    expect(hls.loadSource).toHaveBeenCalledOnce();
    await click(screen.getByRole('button', { name: 'Pausar' })); for (let i = 0; i < 4; i++) await settle();
    const afterPause = reads; await advance(60_000); expect(reads).toBe(afterPause);
  });
  it('requests progress while the lesson response is still pending when the course is known', async () => {
    let release: (() => void) | undefined;
    const pending = new Promise<void>((resolve) => { release = resolve; });
    server.use(http.get(`${env.API_URL}/api/v1/lessons/:lessonId`, async () => { await pending; return HttpResponse.json(studentLessonData); }));
    renderLesson(); for (let i = 0; i < 6; i++) await settle();
    expect(reads).toBe(1); expect(hls.loadSource).not.toHaveBeenCalled();
    release?.(); for (let i = 0; i < 15 && !hls.loadSource.mock.calls.length; i++) await settle();
    expect(hls.startPosition).toBe(252);
  });
  it('avoids showing marks or resuming from a different published version', async () => {
    server.use(http.get(endpoint, () => HttpResponse.json({ ...savedProgress(), versionNumber: 3 })));
    const { video } = await start(); expect(video.currentTime).toBe(0);
    expect(screen.queryByText('Concluída')).not.toBeInTheDocument(); expect(screen.queryByRole('progressbar')).not.toBeInTheDocument();
  });
  it('can discover the course on a direct visit and provides a return to home', async () => {
    const { video } = await start(false); expect(video.currentTime).toBe(252);
    await click(screen.getByRole('link', { name: 'Voltar para o Início' })); expect(screen.getByRole('heading', { name: 'Início' })).toBeInTheDocument();
  });
  it('starts a different lesson from its own position after navigation', async () => {
    server.use(http.get(`${env.API_URL}/api/v1/lessons/:lessonId`, ({ params }) => HttpResponse.json(params.lessonId === secondLessonId
      ? { ...studentLessonData, lesson: { ...studentLessonData.lesson, lessonId: secondLessonId, title: 'Persistência', position: 2 } } : studentLessonData)));
    await start(); await click(screen.getByRole('link', { name: '2. Persistência' }));
    for (let i = 0; i < 15 && hls.loadSource.mock.calls.length < 2; i++) await settle();
    expect(hls.loadSource).toHaveBeenCalledTimes(2); expect(hls.startPosition).toBe(0); expect(screen.queryByText(/Retomando de/)).not.toBeInTheDocument();
  });
  it('still refreshes progress when recording a paused advance fails', async () => {
    server.use(http.post(recordedEndpoint, () => new HttpResponse(null, { status: 503 })));
    await start(); await click(screen.getByRole('button', { name: 'Reproduzir' })); await click(screen.getByRole('button', { name: 'Pausar' }));
    for (let i = 0; i < 6; i++) await settle(); expect(reads).toBe(2);
  });
});
