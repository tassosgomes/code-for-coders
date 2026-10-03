import { useRef, useState } from 'react';

import { Button } from '@/components/ui/button';
import { supportMessage, useProtectedPlayback } from '@/features/student-lessons/hooks/use-protected-playback';

const zones = ['left-4 top-4', 'right-4 top-4', 'left-4 top-1/2', 'right-4 top-1/2'];

type ProtectedVideoPlayerProps = { lessonId: string; csrfToken: string };

export const ProtectedVideoPlayer = ({ lessonId, csrfToken }: ProtectedVideoPlayerProps) => {
  const videoRef = useRef<HTMLVideoElement>(null);
  const containerRef = useRef<HTMLDivElement>(null);
  const { status, watermark, zone, playing, togglePlayback, retry, loadedMetadata, onPlay, onPause } =
    useProtectedPlayback({ lessonId, csrfToken, videoRef });
  const [duration, setDuration] = useState(0);
  const [position, setPosition] = useState(0);
  const [volume, setVolume] = useState(1);

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
        onPlay={onPlay} onPause={onPause} onEnded={onPause}
        onLoadedMetadata={() => { setDuration(videoRef.current?.duration ?? 0); loadedMetadata(); setPosition(videoRef.current?.currentTime ?? 0); }}
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
      ? <Button className="mt-2" onClick={retry}>Tentar de novo</Button> : null}
  </div>;
};
