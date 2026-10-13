import { useEffect, useState, type RefObject } from 'react';

import { usePlaybackCaptions } from '@/features/student-lessons/api/get-playback-captions';

const preferenceKey = 'lesson-captions-enabled';
const readPreference = () => {
  try { return window.localStorage.getItem(preferenceKey) === 'true'; }
  catch { return false; }
};

type PlayerCaptionsOptions = {
  videoRef: RefObject<HTMLVideoElement | null>; sessionId: string; available: boolean;
};

export const usePlayerCaptions = ({ videoRef, sessionId, available }: PlayerCaptionsOptions) => {
  const [enabled, setEnabled] = useState(readPreference);
  const [text, setText] = useState('');
  const [trackFailed, setTrackFailed] = useState(false);
  const query = usePlaybackCaptions(sessionId, Boolean(sessionId) && available && enabled);
  const failed = query.isError || trackFailed;

  useEffect(() => {
    if (!sessionId || !available || !enabled || !query.data || failed) return;
    const video = videoRef.current;
    if (!video) return;
    let disposed = false;
    const element = document.createElement('track');
    element.kind = 'captions'; element.label = 'Legenda'; element.srclang = 'pt-BR';
    const url = URL.createObjectURL(new Blob([query.data], { type: 'text/vtt' }));
    element.src = url;
    const track = element.track;
    const update = () => {
      if (disposed) return;
      const cues = track.activeCues;
      const current: string[] = [];
      for (let index = 0; cues && index < cues.length; index++) {
        const cue = cues[index];
        if (cue && 'getCueAsHTML' in cue && typeof cue.getCueAsHTML === 'function') {
          current.push(cue.getCueAsHTML().textContent ?? '');
        }
      }
      setText(current.join('\n'));
    };
    const loaded = () => { track.mode = 'hidden'; update(); };
    const error = () => { if (!disposed) setTrackFailed(true); };
    track.addEventListener('cuechange', update);
    element.addEventListener('load', loaded); element.addEventListener('error', error);
    video.append(element); track.mode = 'hidden';
    return () => {
      disposed = true;
      track.removeEventListener('cuechange', update);
      element.removeEventListener('load', loaded); element.removeEventListener('error', error);
      track.mode = 'disabled'; element.remove(); URL.revokeObjectURL(url);
    };
  }, [sessionId, available, enabled, query.data, failed, videoRef]);

  const toggle = () => {
    const next = !enabled;
    setText('');
    setEnabled(next);
    try { window.localStorage.setItem(preferenceKey, String(next)); }
    catch { /* The device may deny storage; captions still work for this visit. */ }
  };
  return { enabled, toggle, failed, text: available && enabled && !failed ? text : '' };
};
