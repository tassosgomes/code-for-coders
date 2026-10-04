import { fireEvent, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { env } from '@/config/env';
import { ProtectedVideoPlayer } from '@/features/student-lessons/components/protected-video-player';
import { setupPlaybackRequest } from '@/features/student-lessons/utils/playback-request';
import { playbackData } from '@/testing/playback-data';
import { server } from '@/testing/server';
import { lessonId } from '@/testing/student-lesson-data';
import { renderWithProviders } from '@/testing/test-utils';

const hls = vi.hoisted(() => ({ supported: true, loadSource: vi.fn(), attachMedia: vi.fn(), destroy: vi.fn() }));
vi.mock('hls.js', () => ({
  default: class {
    static isSupported = () => hls.supported;
    static Events = { ERROR: 'error' };
    attachMedia = hls.attachMedia;
    loadSource = hls.loadSource;
    destroy = hls.destroy;
    stopLoad = vi.fn();
    on = vi.fn();
  },
}));
const endpoint = env.API_URL + '/api/v1/lessons/:lessonId/playback-sessions';
const renderPlayer = () => renderWithProviders(<ProtectedVideoPlayer lessonId={lessonId} csrfToken="csrf-proof" />);
const started = async () => { await screen.findByTestId('video-watermark'); await waitFor(() => expect(hls.loadSource).toHaveBeenCalled()); };

beforeEach(() => {
  hls.supported = true; hls.loadSource.mockClear(); hls.attachMedia.mockClear(); hls.destroy.mockClear();
  vi.spyOn(HTMLMediaElement.prototype, 'play').mockResolvedValue();
  vi.spyOn(HTMLMediaElement.prototype, 'pause').mockImplementation(() => undefined);
});

describe('Playback opening', () => {
  it('opens with CSRF and keeps the personal email only over the video', async () => {
    let proof: string | null = null;
    server.use(http.post(endpoint, ({ request }) => { proof = request.headers.get('X-CSRF-Token'); return HttpResponse.json(playbackData(), { status: 201 }); }));
    renderPlayer(); await started();
    expect(proof).toBe('csrf-proof');
    expect(hls.loadSource).toHaveBeenCalledWith(expect.stringContaining('/api/v1/playback-sessions/'));
    expect(screen.getByTestId('video-watermark')).toHaveAttribute('aria-hidden', 'true');
    expect(screen.getByTestId('video-watermark')).toHaveClass('pointer-events-none');
    expect(JSON.stringify({ local: { ...localStorage }, session: { ...sessionStorage } })).not.toContain('student@example.com');
    expect(screen.getByText(/Este conteúdo é de uso pessoal/)).toBeInTheDocument();
  });

  it.each([
    [409, 'MEDIA_NOT_READY', 'Esta aula está indisponível no momento.'],
    [422, 'WATERMARK_UNAVAILABLE', 'Não foi possível iniciar a aula.'],
    [403, 'ACCESS_DENIED', 'Você não tem acesso a esta aula.'],
    [503, 'ACCESS_DECISION_UNAVAILABLE', 'Não foi possível verificar o acesso à aula. Tente de novo.'],
    [404, 'LESSON_NOT_AVAILABLE', 'Esta aula está indisponível no momento.'],
  ])('never starts HLS when opening fails with %s', async (status, code, message) => {
    server.use(http.post(endpoint, () => HttpResponse.json({ code }, { status: Number(status) })));
    renderPlayer(); expect(await screen.findByText(message)).toBeInTheDocument();
    expect(hls.loadSource).not.toHaveBeenCalled(); expect(screen.queryByTestId('video-watermark')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Reproduzir' })).toBeDisabled();
  });

  it('does not start playback without a valid watermark in the response', async () => {
    server.use(http.post(endpoint, () => HttpResponse.json({ ...playbackData(), watermark: { text: '', repositionSeconds: 30 } }, { status: 201 })));
    renderPlayer(); await screen.findByText('Não foi possível iniciar a aula.');
    expect(hls.loadSource).not.toHaveBeenCalled();
  });

  it('shows unsupported browser state without opening a session', async () => {
    hls.supported = false; renderPlayer();
    await screen.findByText(/Seu navegador não consegue mostrar este vídeo/);
    expect(hls.loadSource).not.toHaveBeenCalled();
  });

  it('moves the watermark to another safe zone while paused', async () => {
    renderPlayer(); await started();
    const watermark = screen.getByTestId('video-watermark'); const before = watermark.className;
    await waitFor(() => expect(watermark.className).not.toBe(before), { timeout: 1800 });
    expect(watermark).toBeVisible();
  });

  it('offers accessible play pause seek volume and keyboard controls', async () => {
    const { container } = renderPlayer(); await started();
    const video = container.querySelector('video')!;
    Object.defineProperty(video, 'duration', { configurable: true, value: 60 }); fireEvent.loadedMetadata(video);
    await userEvent.click(screen.getByRole('button', { name: 'Reproduzir' })); expect(video.play).toHaveBeenCalled();
    fireEvent.play(video); expect(screen.getByRole('button', { name: 'Pausar' })).toBeInTheDocument();
    fireEvent.change(screen.getByRole('slider', { name: 'Posição do vídeo' }), { target: { value: '20' } }); expect(video.currentTime).toBe(20);
    fireEvent.keyDown(screen.getByLabelText('Player da aula'), { key: 'ArrowRight' }); expect(video.currentTime).toBe(30);
    fireEvent.change(screen.getByRole('slider', { name: 'Volume' }), { target: { value: '0.5' } }); expect(video.volume).toBe(0.5);
  });

  it('requests fullscreen on the container containing the watermark', async () => {
    renderPlayer(); await started();
    const container = screen.getByLabelText('Player da aula'); const request = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(container, 'requestFullscreen', { value: request, configurable: true });
    await userEvent.click(screen.getByRole('button', { name: 'Tela cheia' }));
    expect(request).toHaveBeenCalledOnce(); expect(container).toContainElement(screen.getByTestId('video-watermark'));
  });

  it.each(['playlist', 'variants/480p', 'key'])('keeps segment credentials away from BFF %s', (resource) => {
    const xhr = { open: vi.fn(), withCredentials: false } as unknown as XMLHttpRequest;
    setupPlaybackRequest(xhr, window.location.origin + '/api/v1/playback-sessions/id/' + resource, 'opaque-secret=value');
    expect(xhr.open).not.toHaveBeenCalled(); expect(xhr.withCredentials).toBe(true);
  });

  it('adds the opaque query only to the requested segment', () => {
    const xhr = { open: vi.fn(), withCredentials: true } as unknown as XMLHttpRequest;
    setupPlaybackRequest(xhr, 'https://edge.test/tenant/video/hls/480p/segment_00001.ts', 'opaque-secret=value');
    expect(xhr.open).toHaveBeenCalledWith('GET', 'https://edge.test/tenant/video/hls/480p/segment_00001.ts?opaque-secret=value', true);
    expect(xhr.withCredentials).toBe(false);
  });
});
