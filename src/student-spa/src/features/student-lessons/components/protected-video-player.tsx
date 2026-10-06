import { useRef, useState } from 'react';

import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { ResumeNotice } from '@/features/student-lessons/components/resume-notice';
import { supportMessage, useProtectedPlayback } from '@/features/student-lessons/hooks/use-protected-playback';

const zones = ['left-4 top-4', 'right-4 top-4', 'left-4 top-1/2', 'right-4 top-1/2'];
const speeds = [0.5, 1, 1.25, 1.5, 2];

type ProtectedVideoPlayerProps = {
  lessonId: string; csrfToken: string; initialPosition?: number; waitForProgress?: boolean; onProgressRefresh?: () => void;
};

export const ProtectedVideoPlayer = ({ lessonId, csrfToken, initialPosition, waitForProgress = false, onProgressRefresh }: ProtectedVideoPlayerProps) => {
  const videoRef = useRef<HTMLVideoElement>(null);
  const containerRef = useRef<HTMLDivElement>(null);
  const { restart, status, watermark, zone, playing, togglePlayback, retry, loadedMetadata, onPlay, onPause, onEnded } =
    useProtectedPlayback({ lessonId, csrfToken, videoRef, initialPosition, enabled: !waitForProgress || initialPosition !== undefined, onProgressRefresh });
  const [duration, setDuration] = useState(0);
  const [position, setPosition] = useState(0);
  const [volume, setVolume] = useState(1);
  const [speed, setSpeed] = useState(1);

  const seek = (seconds: number) => {
    const video = videoRef.current;
    if (video) { video.currentTime = Math.max(0, Math.min(duration, seconds)); setPosition(video.currentTime); }
  };
  const handleSpeedChange = (rate: number) => {
    setSpeed(rate);
    if (videoRef.current) videoRef.current.playbackRate = rate;
  };
  const fullscreen = () => {
    if (document.fullscreenElement) void document.exitFullscreen();
    else void containerRef.current?.requestFullscreen?.();
  };

  return <div>
    <div ref={containerRef} role="toolbar" className="protected-video-player relative overflow-hidden rounded-lg bg-black text-white focus-visible:outline focus-visible:outline-ring"
      tabIndex={0} aria-label="Player da aula" onKeyDown={(event) => {
        if (event.target !== event.currentTarget) return;
        if (event.key === ' ') { event.preventDefault(); togglePlayback(); }
        if (event.key === 'ArrowRight') { event.preventDefault(); seek(position + 10); }
        if (event.key === 'ArrowLeft') { event.preventDefault(); seek(position - 10); }
        if (event.key === 'f') fullscreen();
      }}>
      <video ref={videoRef} className="aspect-video w-full" playsInline disablePictureInPicture
        onPlay={onPlay} onPause={onPause} onEnded={onEnded}
        onLoadedMetadata={() => {
          setDuration(videoRef.current?.duration ?? 0);
          loadedMetadata();
          setPosition(videoRef.current?.currentTime ?? 0);
          if (videoRef.current) videoRef.current.playbackRate = speed;
        }}
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
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button
              variant="outline"
              aria-label="Velocidade de reprodução"
              disabled={Boolean(status)}
              className="border-white/20 bg-transparent text-white hover:bg-white/20 hover:text-white focus-visible:outline focus-visible:outline-ring"
            >
              {speed.toString().replace('.', ',')}x
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" className="w-24">
            <DropdownMenuRadioGroup
              value={speed.toString()}
              onValueChange={(val) => handleSpeedChange(Number(val))}
            >
              {speeds.map((rate) => (
                <DropdownMenuRadioItem key={rate} value={rate.toString()}>
                  {rate.toString().replace('.', ',')}x
                </DropdownMenuRadioItem>
              ))}
            </DropdownMenuRadioGroup>
          </DropdownMenuContent>
        </DropdownMenu>
        <Button aria-label="Tela cheia" onClick={fullscreen}>Tela cheia</Button>
      </div>
    </div>
    {initialPosition !== undefined && initialPosition > 0 ? <ResumeNotice seconds={initialPosition}
      onRestart={() => { restart(); setPosition(0); }} /> : null}
    <p className="mt-2 text-sm text-muted-foreground">Este conteúdo é de uso pessoal. O seu e-mail aparece sobre o vídeo durante a aula.</p>
    {status && status !== 'Carregando vídeo…' && status !== supportMessage
      ? <Button className="mt-2" onClick={retry}>Tentar de novo</Button> : null}
  </div>;
};
