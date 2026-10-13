import { act, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { MemoryRouter, Route, Routes } from 'react-router';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { StudentLessonRoute } from '@/app/routes/student-lesson-route';
import { env } from '@/config/env';
import { paths } from '@/config/paths';
import { playbackData } from '@/testing/playback-data';
import { simulateVideo } from '@/testing/playback-simulator';
import { server } from '@/testing/server';
import { lessonId, lessonTranscriptionData, secondLessonId, studentLessonData } from '@/testing/student-lesson-data';
import { renderWithProviders } from '@/testing/test-utils';

const hls = vi.hoisted(() => ({ loadSource: vi.fn(), destroy: vi.fn() }));
vi.mock('hls.js', () => ({ default: class {
  static isSupported = () => true;
  static Events = { ERROR: 'error' };
  loadSource = hls.loadSource; destroy = hls.destroy;
  attachMedia = vi.fn(); stopLoad = vi.fn(); on = vi.fn();
} }));
const endpoint = env.API_URL + '/api/v1/lessons/:lessonId/transcription';
const opening = env.API_URL + '/api/v1/lessons/:lessonId/playback-sessions';
const renderLesson = () => renderWithProviders(<MemoryRouter initialEntries={[paths.studentLesson.getHref(lessonId)]}>
  <Routes><Route path={paths.studentLesson.path} element={<StudentLessonRoute />} /></Routes>
</MemoryRouter>);
let scroll: ReturnType<typeof vi.fn>;

beforeEach(() => {
  localStorage.clear(); sessionStorage.clear(); hls.loadSource.mockClear(); hls.destroy.mockClear();
  simulateVideo();
  scroll = vi.fn(); Object.defineProperty(HTMLElement.prototype, 'scrollIntoView', { configurable: true, value: scroll });
  server.use(http.get(endpoint, () => HttpResponse.json(lessonTranscriptionData)));
});

describe('Lesson transcription', () => {
  it('fetches once per opening in parallel with the lesson, never persists text and discards it on navigation', async () => {
    const requests: string[] = [];
    let releaseLesson: (() => void) | undefined;
    server.use(http.get(endpoint, ({ request, params }) => {
      requests.push(request.url);
      return HttpResponse.json({ ...lessonTranscriptionData, lessonId: params.lessonId,
        segments: params.lessonId === secondLessonId ? [{ startMs: 0, endMs: 60_000, text: 'Second lesson text' }] : lessonTranscriptionData.segments });
    }), http.get(env.API_URL + '/api/v1/lessons/:lessonId', async ({ params }) => {
      if (params.lessonId === lessonId) await new Promise<void>((resolve) => { releaseLesson = resolve; });
      return HttpResponse.json({ ...studentLessonData, lesson: { ...studentLessonData.lesson,
        lessonId: params.lessonId, title: params.lessonId === secondLessonId ? 'Persistência' : studentLessonData.lesson.title } });
    }));
    renderLesson(); await waitFor(() => expect(requests).toHaveLength(1));
    expect(screen.getByRole('status', { name: 'Carregando aula' })).toBeInTheDocument();
    releaseLesson?.(); await screen.findByRole('searchbox', { name: 'Buscar na aula' });
    await userEvent.type(screen.getByRole('searchbox'), 'introducao');
    act(() => { window.dispatchEvent(new Event('focus')); window.dispatchEvent(new Event('online')); });
    expect(requests).toHaveLength(1); expect(requests.every((url) => !url.includes('?'))).toBe(true);
    expect(JSON.stringify({ ...localStorage, ...sessionStorage })).not.toContain('Docker');
    await userEvent.click(screen.getByRole('tab', { name: 'Aulas do curso' }));
    await userEvent.click(screen.getByRole('link', { name: '2. Persistência' }));
    await screen.findByText('Second lesson text');
    expect(screen.queryByText(/Introdução ao Docker/u)).not.toBeInTheDocument();
    expect(requests).toHaveLength(2);
    await userEvent.click(screen.getByRole('tab', { name: 'Aulas do curso' }));
    // The second visit must request again rather than serve a stored transcription.
    server.use(http.get(env.API_URL + '/api/v1/lessons/:lessonId', () => HttpResponse.json(studentLessonData)));
    await userEvent.click(screen.getByRole('link', { name: '1. Injeção de dependência' }));
    await screen.findByRole('searchbox'); expect(requests).toHaveLength(3);
  });

  it('matches accents case and punctuation, highlights original text and keeps search off the server', async () => {
    renderLesson(); const search = await screen.findByRole('searchbox');
    await userEvent.type(search, 'INTRODUCAO');
    expect(screen.getByText('Introdução', { selector: 'mark' })).toBeInTheDocument();
    expect(screen.getByRole('region', { name: 'Texto da transcrição' }).querySelectorAll('button')).toHaveLength(1);
    expect(screen.getByText('1 de 1 resultados')).toBeInTheDocument();
    await userEvent.clear(search); await userEvent.type(search, 'docker,');
    expect(screen.getByText('1 de 3 resultados')).toBeInTheDocument();
    expect(screen.getAllByText(/docker/i, { selector: 'mark' })).toHaveLength(3);
    expect(HTMLMediaElement.prototype.play).not.toHaveBeenCalled();
  });

  it('cycles all occurrences with buttons and Enter without moving focus or video, and Escape clears', async () => {
    renderLesson(); const search = await screen.findByRole('searchbox'); await userEvent.type(search, 'docker');
    await userEvent.keyboard('{Enter}'); expect(screen.getByText('2 de 3 resultados')).toBeInTheDocument(); expect(search).toHaveFocus();
    await userEvent.keyboard('{Shift>}{Enter}{/Shift}'); expect(screen.getByText('1 de 3 resultados')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Ocorrência anterior' })); expect(screen.getByText('3 de 3 resultados')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Próxima ocorrência' })); expect(screen.getByText('1 de 3 resultados')).toBeInTheDocument();
    await userEvent.click(search); await userEvent.keyboard('{Escape}'); expect(search).toHaveValue('');
    expect(within(screen.getByRole('region')).getAllByRole('button')).toHaveLength(3);
    expect(HTMLMediaElement.prototype.play).not.toHaveBeenCalled();
  });

  it('treats spaces as an empty search and shows a neutral no-match state', async () => {
    renderLesson(); const search = await screen.findByRole('searchbox'); await userEvent.type(search, '   ');
    expect(within(screen.getByRole('region')).getAllByRole('button')).toHaveLength(3);
    expect(screen.queryByText(/resultados/u)).not.toBeInTheDocument();
    await userEvent.type(search, 'xyz'); expect(screen.getByText('Nenhum trecho encontrado para essa busca.')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Limpar busca' })); expect(search).toHaveValue('');
  });

  it('renders service markup literally as React text', async () => {
    const view = renderLesson(); await screen.findByRole('searchbox');
    expect(screen.getByText(/<script>literal<\/script>/u)).toBeInTheDocument();
    expect(view.container.querySelector('script')).toBeNull();
    await userEvent.type(screen.getByRole('searchbox'), 'literal');
    expect(screen.getByText('literal', { selector: 'mark' })).toBeInTheDocument(); expect(view.container.querySelector('script')).toBeNull();
  });

  it('renders all 3000 segments and searches them locally without additional requests', async () => {
    let requests = 0;
    server.use(http.get(endpoint, () => { requests++; return HttpResponse.json({ ...lessonTranscriptionData, durationMs: 3_000_000,
      segments: Array.from({ length: 3000 }, (_, index) => ({ startMs: index * 1000, endMs: (index + 1) * 1000,
        text: index === 2999 ? 'Introdução ao último trecho' : `Trecho de aula ${index}` })),
    }); }));
    renderLesson(); const search = await screen.findByRole('searchbox');
    expect(screen.getByRole('region').querySelectorAll('button')).toHaveLength(3000);
    await userEvent.type(search, 'introducao');
    expect(screen.getByRole('region').querySelectorAll('button')).toHaveLength(1);
    expect(screen.getByText('Introdução', { selector: 'mark' })).toBeInTheDocument(); expect(requests).toBe(1);
  });

  it('highlights the current segment, pauses following on manual scrolling and resumes explicitly', async () => {
    const view = renderLesson(); await screen.findByRole('searchbox');
    const video = view.container.querySelector('video')!;
    act(() => { video.currentTime = 13; video.dispatchEvent(new Event('timeupdate')); });
    const current = screen.getByRole('button', { name: /12:00|00:12 Agora/u });
    expect(current).toHaveAttribute('aria-current', 'true');
    const region = screen.getByRole('region');
    await userEvent.click(region); await userEvent.keyboard('{PageUp}');
    expect(screen.getByRole('button', { name: 'Ir para o trecho atual' })).toBeInTheDocument();
    scroll.mockClear(); act(() => { video.currentTime = 31; video.dispatchEvent(new Event('timeupdate')); });
    expect(scroll).not.toHaveBeenCalled();
    await userEvent.click(screen.getByRole('button', { name: 'Ir para o trecho atual' }));
    expect(scroll).toHaveBeenCalled(); expect(screen.queryByRole('button', { name: 'Ir para o trecho atual' })).not.toBeInTheDocument();
    await userEvent.type(screen.getByRole('searchbox'), 'docker'); scroll.mockClear();
    act(() => { video.currentTime = 15; video.dispatchEvent(new Event('timeupdate')); });
    expect(scroll).not.toHaveBeenCalled();
  });

  it('clicks a segment in an active session to seek and play without reopening the lesson', async () => {
    let openings = 0; server.use(http.post(opening, () => { openings++; return HttpResponse.json(playbackData(), { status: 201 }); }));
    const view = renderLesson(); await screen.findByRole('searchbox'); await screen.findByTestId('video-watermark');
    await userEvent.click(screen.getByRole('button', { name: /00:12 Agora/u }));
    expect(view.container.querySelector('video')?.currentTime).toBe(12);
    expect(screen.getByRole('button', { name: 'Pausar' })).toBeEnabled(); expect(openings).toBe(1);
  });

  it('keeps the chosen time while the normal session opening is still pending', async () => {
    let release: (() => void) | undefined; let openings = 0;
    server.use(http.post(opening, async () => { openings++; await new Promise<void>((resolve) => { release = resolve; }); return HttpResponse.json(playbackData(), { status: 201 }); }));
    const view = renderLesson(); await screen.findByRole('searchbox'); await waitFor(() => expect(release).toBeDefined());
    await userEvent.click(screen.getByRole('button', { name: /00:12 Agora/u }));
    release?.(); await screen.findByTestId('video-watermark');
    const video = view.container.querySelector('video')!; act(() => video.dispatchEvent(new Event('loadedmetadata')));
    expect(video.currentTime).toBe(12); expect(screen.getByRole('button', { name: 'Pausar' })).toBeEnabled(); expect(openings).toBe(1);
  });

  it('shows neutral unavailability while keeping the player usable', async () => {
    server.use(http.get(endpoint, () => HttpResponse.json({ lessonId, status: 'unavailable', revision: null, language: null, durationMs: null, segments: [] })));
    renderLesson(); await screen.findByTestId('video-watermark');
    expect(screen.getByRole('tab', { name: 'Aulas do curso' })).toHaveAttribute('aria-selected', 'true');
    await userEvent.click(screen.getByRole('tab', { name: 'Transcrição' }));
    expect(screen.getByText('A transcrição desta aula ainda não está disponível. O vídeo toca normalmente.')).toBeInTheDocument();
    expect(screen.queryByRole('searchbox')).not.toBeInTheDocument(); expect(screen.getByRole('button', { name: 'Reproduzir' })).toBeEnabled();
  });

  it.each([[403, 'ACCESS_DENIED', 'Você não tem acesso a este curso.'], [503, 'ACCESS_DECISION_UNAVAILABLE', 'Não foi possível confirmar seu acesso agora.']])(
    'keeps transcription error %s distinct from unavailability', async (status, code, message) => {
      server.use(http.get(endpoint, () => HttpResponse.json({ code }, { status: Number(status) })));
      renderLesson(); await screen.findByTestId('video-watermark'); await userEvent.click(screen.getByRole('tab', { name: 'Transcrição' }));
      expect(await screen.findByText(String(message))).toBeInTheDocument(); expect(screen.queryByRole('searchbox')).not.toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Reproduzir' })).toBeEnabled();
    });

  it('supports an explicit retry after 503 without automatic requests', async () => {
    let requests = 0; server.use(http.get(endpoint, () => { requests++; return requests === 1
      ? HttpResponse.json({ code: 'ACCESS_DECISION_UNAVAILABLE' }, { status: 503 }) : HttpResponse.json(lessonTranscriptionData); }));
    renderLesson(); await screen.findByTestId('video-watermark'); await userEvent.click(screen.getByRole('tab', { name: 'Transcrição' }));
    await screen.findByText('Não foi possível confirmar seu acesso agora.'); expect(requests).toBe(1);
    await userEvent.click(screen.getByRole('button', { name: 'Tentar de novo' })); await screen.findByRole('searchbox'); expect(requests).toBe(2);
  });

  it('removes all transcript text and its tab when access ends at renewal', async () => {
    let requests = 0;
    server.use(http.get(endpoint, () => { requests++; return HttpResponse.json(lessonTranscriptionData); }),
      http.post(opening, () => HttpResponse.json({ ...playbackData(), renewAfter: new Date(Date.now() + 20).toISOString() }, { status: 201 })),
      http.post(env.API_URL + '/api/v1/playback-sessions/:sessionId/renewals', () => HttpResponse.json({ code: 'ACCESS_DENIED' }, { status: 403 })));
    renderLesson(); await screen.findByRole('searchbox'); await screen.findByTestId('video-watermark');
    await userEvent.click(screen.getByRole('button', { name: 'Reproduzir' }));
    await screen.findByText('Você não tem acesso a esta aula.');
    await waitFor(() => expect(screen.queryByRole('tab', { name: 'Transcrição' })).not.toBeInTheDocument());
    expect(screen.queryByText(/Introdução ao Docker/u)).not.toBeInTheDocument();
    expect(screen.getByRole('navigation', { name: 'Aulas do curso' })).toBeInTheDocument(); expect(requests).toBe(1);
  });
});
