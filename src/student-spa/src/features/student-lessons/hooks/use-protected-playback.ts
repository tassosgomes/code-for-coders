import axios from 'axios';
import Hls from 'hls.js';
import { useEffect, useRef, useState, type RefObject } from 'react';

import { env } from '@/config/env';
import { useOpenPlaybackSession, type PlaybackSession } from '@/features/student-lessons/api/open-playback-session';
import { useRenewPlaybackSession } from '@/features/student-lessons/api/renew-playback-session';
import { setupPlaybackRequest } from '@/features/student-lessons/utils/playback-request';
import { registerTelemetrySecret } from '@/lib/telemetry-url-redaction';

export const supportMessage = 'Seu navegador não consegue mostrar este vídeo. Atualize o navegador ou tente em outro aparelho.';
const unavailableMessage = 'Não foi possível verificar o acesso à aula. Tente de novo.';
const expiredMessage = 'A sessão de reprodução terminou.';
const retryMilliseconds = 5000;

type PlaybackOptions = { lessonId: string; csrfToken: string; videoRef: RefObject<HTMLVideoElement | null> };

const errorMessage = (error: unknown) => {
  const problem = axios.isAxiosError(error) ? error.response?.data : undefined;
  if (problem?.code === 'ACCESS_DENIED') {
    if (problem.reason !== 'grant-ended') return 'Você não tem acesso a esta aula.';
    const end = typeof problem.accessEndedAt === 'string' ? Date.parse(problem.accessEndedAt) : NaN;
    if (!Number.isFinite(end)) return 'Seu acesso a este curso terminou.';
    const date = new Intl.DateTimeFormat('pt-BR', { timeZone: env.SCHOOL_TIME_ZONE }).format(new Date(end - 1));
    return `Seu acesso a este curso terminou em ${date}.`;
  }
  if (problem?.code === 'ACCESS_DECISION_UNAVAILABLE') return unavailableMessage;
  if (problem?.code === 'PLAYBACK_SESSION_EXPIRED') return expiredMessage;
  if (problem?.code === 'MEDIA_NOT_READY' || problem?.code === 'LESSON_NOT_AVAILABLE') return 'Esta aula está indisponível no momento.';
  return 'Não foi possível iniciar a aula.';
};

export const useProtectedPlayback = ({ lessonId, csrfToken, videoRef }: PlaybackOptions) => {
  const { mutateAsync: open } = useOpenPlaybackSession();
  const { mutateAsync: renew } = useRenewPlaybackSession();
  const [attempt, setAttempt] = useState(0);
  const [watermark, setWatermark] = useState('');
  const [zone, setZone] = useState(0);
  const [status, setStatus] = useState('Carregando vídeo…');
  const [playing, setPlaying] = useState(false);
  const sessionRef = useRef<PlaybackSession | null>(null);
  const playingRef = useRef(false);
  const resumeRef = useRef(false);
  const positionRef = useRef(0);
  const playRef = useRef<() => void>(() => undefined);
  const pauseRef = useRef<() => void>(() => undefined);

  useEffect(() => {
    const controller = new AbortController();
    let renewalController: AbortController | undefined;
    let hls: Hls | undefined;
    let reposition: ReturnType<typeof setInterval> | undefined;
    let expiration: ReturnType<typeof setTimeout> | undefined;
    let renewal: ReturnType<typeof setTimeout> | undefined;
    let unavailable = false;
    sessionRef.current = null;
    playingRef.current = false;
    const stop = (message: string) => {
      clearTimeout(renewal);
      clearTimeout(expiration);
      renewalController?.abort();
      positionRef.current = videoRef.current?.currentTime ?? positionRef.current;
      videoRef.current?.pause();
      playingRef.current = false;
      setPlaying(false);
      hls?.stopLoad();
      setStatus(message);
    };
    const adopt = (session: PlaybackSession) => {
      sessionRef.current = session;
      registerTelemetrySecret(session.segmentAccess.query, Date.parse(session.segmentAccess.expiresAt));
      setWatermark(session.watermark.text);
      clearTimeout(expiration);
      expiration = setTimeout(() => {
        if (playingRef.current) stop(unavailable ? unavailableMessage : expiredMessage);
      }, Math.max(0, Date.parse(session.expiresAt) - Date.now()));
    };
    const schedule = (delay?: number) => {
      clearTimeout(renewal);
      const session = sessionRef.current;
      if (!session || !playingRef.current || controller.signal.aborted) return;
      renewal = setTimeout(() => { void refresh(); }, delay ?? Math.max(0, Date.parse(session.renewAfter) - Date.now()));
    };
    const refresh = async () => {
      const session = sessionRef.current;
      if (!session || !playingRef.current || controller.signal.aborted) return;
      if (Date.now() >= Date.parse(session.expiresAt)) { stop(unavailable ? unavailableMessage : expiredMessage); return; }
      const pending = new AbortController();
      renewalController = pending;
      try {
        const next = await renew({ sessionId: session.sessionId, csrfToken, signal: pending.signal });
        if (controller.signal.aborted || pending.signal.aborted) return;
        // Never accept a response arriving after the old session's deadline.
        if (Date.now() >= Date.parse(session.expiresAt)) { stop(unavailable ? unavailableMessage : expiredMessage); return; }
        unavailable = false;
        adopt(next);
        schedule();
      } catch (error) {
        if (controller.signal.aborted || pending.signal.aborted) return;
        if (axios.isAxiosError(error) && error.response?.status === 503) {
          unavailable = true;
          schedule(retryMilliseconds);
        } else stop(errorMessage(error));
      }
    };
    playRef.current = () => { playingRef.current = true; setPlaying(true); schedule(); };
    pauseRef.current = () => {
      playingRef.current = false;
      setPlaying(false);
      clearTimeout(renewal);
      renewalController?.abort();
    };
    const start = async () => {
      if (!Hls.isSupported()) { setStatus(supportMessage); return; }
      try {
        const session = await open({ lessonId, csrfToken, signal: controller.signal });
        if (controller.signal.aborted || !videoRef.current) return;
        adopt(session);
        setStatus('');
        hls = new Hls({ startLevel: -1, startPosition: positionRef.current,
          xhrSetup: (xhr, url) => setupPlaybackRequest(xhr, url, sessionRef.current?.segmentAccess.query ?? '') });
        hls.on(Hls.Events.ERROR, (_event, data) => { if (data.fatal) stop('Não foi possível iniciar a aula.'); });
        hls.attachMedia(videoRef.current);
        hls.loadSource(new URL('/api/v1/playback-sessions/' + session.sessionId + '/playlist', window.location.origin).href);
        reposition = setInterval(() => setZone((previous) => (previous + 1 + Math.floor(Math.random() * 3)) % 4),
          session.watermark.repositionSeconds * 1000);
      } catch (error) { if (!controller.signal.aborted) setStatus(errorMessage(error)); }
    };
    void start();
    return () => {
      controller.abort(); renewalController?.abort(); hls?.destroy();
      clearInterval(reposition); clearTimeout(expiration); clearTimeout(renewal);
    };
  }, [lessonId, csrfToken, attempt, open, renew, videoRef]);

  const retry = (resume = false) => {
    positionRef.current = videoRef.current?.currentTime ?? positionRef.current;
    resumeRef.current = resume;
    playingRef.current = false; setPlaying(false);
    setStatus('Carregando vídeo…'); setWatermark(''); setAttempt((value) => value + 1);
  };
  const togglePlayback = () => {
    const video = videoRef.current;
    if (!video || status) return;
    if (!video.paused) { video.pause(); return; }
    if (!sessionRef.current || Date.now() >= Date.parse(sessionRef.current.expiresAt)) { retry(true); return; }
    void video.play().catch(() => setStatus('Não foi possível iniciar a aula.'));
  };
  const loadedMetadata = () => {
    if (!resumeRef.current || !videoRef.current) return;
    videoRef.current.currentTime = positionRef.current;
    resumeRef.current = false;
    void videoRef.current.play().catch(() => setStatus('Não foi possível iniciar a aula.'));
  };
  return { status, watermark, zone, playing, togglePlayback, retry: () => retry(true), loadedMetadata,
    onPlay: () => playRef.current(), onPause: () => pauseRef.current() };
};
