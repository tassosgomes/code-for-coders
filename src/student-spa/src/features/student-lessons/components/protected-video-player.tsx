import axios from 'axios';
import Hls from 'hls.js';
import { useEffect, useRef, useState } from 'react';

import { Button } from '@/components/ui/button';
import { openPlaybackSession } from '@/features/student-lessons/api/open-playback-session';
import { setupPlaybackRequest } from '@/features/student-lessons/utils/playback-request';
import { registerTelemetrySecret } from '@/lib/telemetry-url-redaction';

const zones = ['left-4 top-4', 'right-4 top-4', 'left-4 top-1/2', 'right-4 top-1/2'];
const supportMessage = 'Seu navegador não consegue mostrar este vídeo. Atualize o navegador ou tente em outro aparelho.';

type ProtectedVideoPlayerProps = { lessonId: string; csrfToken: string };

export const ProtectedVideoPlayer = ({ lessonId, csrfToken }: ProtectedVideoPlayerProps) => {
  const videoRef = useRef<HTMLVideoElement>(null);
  const containerRef = useRef<HTMLDivElement>(null);
  const [attempt, setAttempt] = useState(0);
  const [watermark, setWatermark] = useState('');
  const [zone, setZone] = useState(0);
  const [status, setStatus] = useState('Carregando vídeo…');
  const [playing, setPlaying] = useState(false);
  const [duration, setDuration] = useState(0);
  const [position, setPosition] = useState(0);
  const [volume, setVolume] = useState(1);

  useEffect(() => {
    const controller = new AbortController();
    let hls: Hls | undefined;
    let reposition: ReturnType<typeof setInterval> | undefined;
    let expiration: ReturnType<typeof setTimeout> | undefined;
    const start = async () => {
      if (!Hls.isSupported()) { setStatus(supportMessage); return; }
      try {
        const session = await openPlaybackSession(lessonId, csrfToken, controller.signal);
        if (controller.signal.aborted || !videoRef.current) return;
        registerTelemetrySecret(session.segmentAccess.query, Date.parse(session.segmentAccess.expiresAt));
        setWatermark(session.watermark.text);
        setStatus('');
        hls = new Hls({ startLevel: -1, xhrSetup: (xhr, url) => setupPlaybackRequest(xhr, url, session.segmentAccess.query) });
        hls.on(Hls.Events.ERROR, (_event, data) => {
          if (data.fatal) { videoRef.current?.pause(); setStatus('Não foi possível iniciar a aula.'); }
        });
        hls.attachMedia(videoRef.current);
        hls.loadSource(new URL('/api/v1/playback-sessions/' + session.sessionId + '/playlist', window.location.origin).href);
        reposition = setInterval(() => setZone((previous) => (previous + 1 + Math.floor(Math.random() * (zones.length - 1))) % zones.length),
          session.watermark.repositionSeconds * 1000);
        expiration = setTimeout(() => { videoRef.current?.pause(); hls?.destroy(); setStatus('A sessão de reprodução terminou.'); },
          Math.max(0, Date.parse(session.expiresAt) - Date.now()));
      } catch (error) {
        if (controller.signal.aborted) return;
        const code = axios.isAxiosError(error) ? error.response?.data?.code : undefined;
        setStatus(code === 'MEDIA_NOT_READY' || code === 'LESSON_NOT_AVAILABLE'
          ? 'Esta aula está indisponível no momento.' : code === 'ACCESS_DENIED'
            ? 'Você não tem acesso a esta aula.' : 'Não foi possível iniciar a aula.');
      }
    };
    void start();
    return () => { controller.abort(); hls?.destroy(); clearInterval(reposition); clearTimeout(expiration); };
  }, [lessonId, csrfToken, attempt]);

  const togglePlayback = () => {
    const video = videoRef.current;
    if (!video || status) return;
    if (video.paused) void video.play().catch(() => setStatus('Não foi possível iniciar a aula.'));
    else video.pause();
  };
  const seek = (seconds: number) => {
    const video = videoRef.current;
    if (video) { video.currentTime = Math.max(0, Math.min(duration, seconds)); setPosition(video.currentTime); }
  };
  const fullscreen = () => {
    if (document.fullscreenElement) void document.exitFullscreen();
    else void containerRef.current?.requestFullscreen?.();
  };

  return <div>
    <div ref={containerRef} className="protected-video-player relative overflow-hidden rounded-lg bg-black text-white focus-visible:outline focus-visible:outline-ring"
      tabIndex={0} aria-label="Player da aula" onKeyDown={(event) => {
        if (event.target !== event.currentTarget) return;
        if (event.key === ' ') { event.preventDefault(); togglePlayback(); }
        if (event.key === 'ArrowRight') { event.preventDefault(); seek(position + 10); }
        if (event.key === 'ArrowLeft') { event.preventDefault(); seek(position - 10); }
        if (event.key === 'f') fullscreen();
      }}>
      <video ref={videoRef} className="aspect-video w-full" playsInline disablePictureInPicture
        onPlay={() => setPlaying(true)} onPause={() => setPlaying(false)}
        onLoadedMetadata={() => setDuration(videoRef.current?.duration ?? 0)}
        onTimeUpdate={() => setPosition(videoRef.current?.currentTime ?? 0)} />
      {watermark ? <span aria-hidden="true" data-testid="video-watermark"
        className={'pointer-events-none absolute z-10 break-all rounded bg-black/60 px-2 py-1 text-xs text-white ' + zones[zone]}>{watermark}</span> : null}
      {status ? <div role="status" className="absolute inset-x-4 top-1/3 bg-black/80 p-4">{status}</div> : null}
      <div className="relative z-20 flex flex-wrap items-center gap-3 bg-black p-3">
        <Button aria-label={playing ? 'Pausar' : 'Reproduzir'} disabled={Boolean(status)} onClick={togglePlayback}>{playing ? 'Pausar' : 'Reproduzir'}</Button>
        <input aria-label="Posição do vídeo" type="range" min={0} max={Number.isFinite(duration) ? duration : 0}
          value={position} step={1} onChange={(event) => seek(Number(event.target.value))}
          className="min-w-24 flex-1 focus-visible:outline focus-visible:outline-ring" />
        <input aria-label="Volume" type="range" min={0} max={1} step={0.05} value={volume}
          onChange={(event) => { const value = Number(event.target.value); setVolume(value); if (videoRef.current) videoRef.current.volume = value; }}
          className="w-24 focus-visible:outline focus-visible:outline-ring" />
        <Button aria-label="Tela cheia" onClick={fullscreen}>Tela cheia</Button>
      </div>
    </div>
    <p className="mt-2 text-sm text-muted-foreground">Este conteúdo é de uso pessoal. O seu e-mail aparece sobre o vídeo durante a aula.</p>
    {status && status !== 'Carregando vídeo…' && status !== supportMessage
      ? <Button className="mt-2" onClick={() => { setStatus('Carregando vídeo…'); setWatermark(''); setAttempt((value) => value + 1); }}>Tentar de novo</Button> : null}
  </div>;
};
