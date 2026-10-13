import { useEffect, useRef, useState, type CSSProperties } from 'react';
import { ChevronDown, Info, Maximize, Minimize, Pause, Play, Volume2, VolumeX } from 'lucide-react';

import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { Button } from '@/components/ui/button';
import { PlayerCaptions } from '@/features/student-lessons/components/player-captions';
import { ResumeNotice } from '@/features/student-lessons/components/resume-notice';
import { supportMessage, useProtectedPlayback } from '@/features/student-lessons/hooks/use-protected-playback';

const speeds = [0.5, 1, 1.25, 1.5, 2];
// Um clique toca ou pausa; o segundo clique dentro desta janela vira clique duplo (tela cheia) e cancela o primeiro.
const doubleClickMilliseconds = 250;

const formatClock = (value: number) => {
  const total = Number.isFinite(value) ? Math.max(0, Math.floor(value)) : 0;
  const hours = Math.floor(total / 3600);
  const minutes = Math.floor(total % 3600 / 60);
  const seconds = String(total % 60).padStart(2, '0');
  return hours ? `${hours}:${String(minutes).padStart(2, '0')}:${seconds}` : `${String(minutes).padStart(2, '0')}:${seconds}`;
};

type ProtectedVideoPlayerProps = {
  lessonId: string; csrfToken: string; initialPosition?: number; waitForProgress?: boolean; onProgressRefresh?: () => void;
  captionsAvailable?: boolean;
  seekRequest?: { seconds: number; sequence: number };
  onTimeChange?: (seconds: number) => void;
  onAccessBlocked?: (blocked: boolean) => void;
};

export const ProtectedVideoPlayer = ({ lessonId, csrfToken, initialPosition, waitForProgress = false, onProgressRefresh,
  seekRequest, onTimeChange, onAccessBlocked, captionsAvailable = false }: ProtectedVideoPlayerProps) => {
  const videoRef = useRef<HTMLVideoElement>(null);
  const [captionContainer, setCaptionContainer] = useState<HTMLDivElement | null>(null);
  const containerRef = useRef<HTMLDivElement>(null);
  const clickTimer = useRef<ReturnType<typeof setTimeout>>(undefined);
  const { restart, status, watermark, zone, playing, accessBlocked, captionSessionId, seekAndPlay, togglePlayback, retry, loadedMetadata, onPlay, onPause, onEnded } =
    useProtectedPlayback({ lessonId, csrfToken, videoRef, initialPosition, enabled: !waitForProgress || initialPosition !== undefined, onProgressRefresh });
  const [duration, setDuration] = useState(0);
  const [position, setPosition] = useState(0);
  const [volume, setVolume] = useState(1);
  const [muted, setMuted] = useState(false);
  const [speed, setSpeed] = useState(1);
  const [fullscreen, setFullscreen] = useState(false);
  useEffect(() => { if (seekRequest) seekAndPlay(seekRequest.seconds); }, [seekRequest, seekAndPlay]);
  useEffect(() => { onTimeChange?.(position); }, [position, onTimeChange]);
  useEffect(() => { onAccessBlocked?.(accessBlocked); }, [accessBlocked, onAccessBlocked]);
  useEffect(() => {
    const update = () => setFullscreen(document.fullscreenElement === containerRef.current);
    document.addEventListener('fullscreenchange', update);
    return () => document.removeEventListener('fullscreenchange', update);
  }, []);
  useEffect(() => () => clearTimeout(clickTimer.current), []);

  const seek = (seconds: number) => {
    const video = videoRef.current;
    if (video) { video.currentTime = Math.max(0, Math.min(duration, seconds)); setPosition(video.currentTime); }
  };
  const handleSpeedChange = (rate: number) => {
    setSpeed(rate);
    if (videoRef.current) videoRef.current.playbackRate = rate;
  };
  const changeVolume = (value: number) => {
    setVolume(value); setMuted(value === 0);
    if (videoRef.current) { videoRef.current.volume = value; videoRef.current.muted = value === 0; }
  };
  const toggleMute = () => {
    const next = !muted;
    setMuted(next);
    if (videoRef.current) videoRef.current.muted = next;
  };
  const toggleFullscreen = () => {
    if (document.fullscreenElement) void document.exitFullscreen();
    else void containerRef.current?.requestFullscreen?.();
  };
  const handlePictureClick = () => {
    clearTimeout(clickTimer.current);
    clickTimer.current = setTimeout(togglePlayback, doubleClickMilliseconds);
  };
  const handlePictureDoubleClick = () => {
    clearTimeout(clickTimer.current);
    toggleFullscreen();
  };
  const progress = duration > 0 ? Math.min(100, position / duration * 100) : 0;
  const silent = muted || volume === 0;

  return <div className="protected-video-player-block">
    <div ref={containerRef} role="toolbar" className="protected-video-player"
      tabIndex={0} aria-label="Player da aula" onKeyDown={(event) => {
        if (event.target !== event.currentTarget) return;
        if (event.key === ' ') { event.preventDefault(); togglePlayback(); }
        if (event.key === 'ArrowRight') { event.preventDefault(); seek(position + 10); }
        if (event.key === 'ArrowLeft') { event.preventDefault(); seek(position - 10); }
        if (event.key === 'f') toggleFullscreen();
      }}>
      <div ref={setCaptionContainer} className="player-media">
        <div className="player-picture" data-testid="player-picture" onClick={handlePictureClick} onDoubleClick={handlePictureDoubleClick}>
          <video ref={videoRef} playsInline disablePictureInPicture
            onPlay={onPlay} onPause={onPause} onEnded={onEnded}
            onLoadedMetadata={() => {
              setDuration(videoRef.current?.duration ?? 0);
              loadedMetadata();
              setPosition(videoRef.current?.currentTime ?? 0);
              if (videoRef.current) videoRef.current.playbackRate = speed;
            }}
            onTimeUpdate={() => setPosition(videoRef.current?.currentTime ?? 0)} />
          {watermark ? <span aria-hidden="true" data-testid="video-watermark" data-zone={zone}
            className={`player-watermark player-watermark-zone-${zone} pointer-events-none`}>{watermark}</span> : null}
          {status ? <div role="status" className="player-status">{status}</div> : null}
        </div>
      </div>
      <div className="player-controls">
        <div className="player-controls-row">
          <Button variant="ghost" size="icon" className="player-control" aria-label={playing ? 'Pausar' : 'Reproduzir'}
            disabled={Boolean(status)} onClick={togglePlayback}>
            {playing ? <Pause aria-hidden="true" /> : <Play aria-hidden="true" />}
          </Button>
          <input aria-label="Posição do vídeo" type="range" min={0} max={Number.isFinite(duration) ? duration : 0}
            value={position} step={1} onChange={(event) => seek(Number(event.target.value))}
            aria-valuetext={`${formatClock(position)} de ${formatClock(duration)}`}
            className="player-range player-timeline" style={{ '--player-range-fill': `${progress}%` } as CSSProperties} />
          <span className="player-time">{formatClock(position)} / {formatClock(duration)}</span>
        </div>
        <div className="player-controls-row">
          <div className="player-volume">
            <Button variant="ghost" size="icon" className="player-control" aria-label={silent ? 'Ativar o som' : 'Silenciar'}
              onClick={toggleMute}>
              {silent ? <VolumeX aria-hidden="true" /> : <Volume2 aria-hidden="true" />}
            </Button>
            <input aria-label="Volume" type="range" min={0} max={1} step={0.05} value={muted ? 0 : volume}
              onChange={(event) => changeVolume(Number(event.target.value))}
              className="player-range player-volume-range" style={{ '--player-range-fill': `${(muted ? 0 : volume) * 100}%` } as CSSProperties} />
          </div>
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button variant="ghost" className="player-control player-speed" aria-label="Velocidade de reprodução" disabled={Boolean(status)}>
                {speed.toString().replace('.', ',')}x
                <ChevronDown aria-hidden="true" />
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
          {captionsAvailable && captionSessionId ? <PlayerCaptions key={captionSessionId} videoRef={videoRef} sessionId={captionSessionId} captionContainer={captionContainer} /> : null}
          <span aria-hidden="true" className="player-controls-spacer" />
          {fullscreen
            ? <Button variant="ghost" className="player-control player-exit-fullscreen" aria-label="Sair da tela cheia" onClick={toggleFullscreen}>
              <Minimize aria-hidden="true" />Sair
            </Button>
            : <Button variant="ghost" size="icon" className="player-control" aria-label="Tela cheia" onClick={toggleFullscreen}>
              <Maximize aria-hidden="true" />
            </Button>}
        </div>
      </div>
    </div>
    {initialPosition !== undefined && initialPosition > 0 ? <ResumeNotice seconds={initialPosition}
      onRestart={() => { restart(); setPosition(0); }} /> : null}
    <p className="personal-use-notice"><Info aria-hidden="true" />Este conteúdo é de uso pessoal. O seu e-mail aparece sobre o vídeo durante a aula.</p>
    {status && status !== 'Carregando vídeo…' && status !== supportMessage
      ? <Button className="mt-2" onClick={retry}>Tentar de novo</Button> : null}
  </div>;
};
