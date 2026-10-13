import type { RefObject } from 'react';
import { createPortal } from 'react-dom';

import { Button } from '@/components/ui/button';
import { usePlayerCaptions } from '@/features/student-lessons/hooks/use-player-captions';

type PlayerCaptionsProps = { videoRef: RefObject<HTMLVideoElement | null>; sessionId: string; captionContainer: HTMLDivElement | null };
export const PlayerCaptions = ({ videoRef, sessionId, captionContainer }: PlayerCaptionsProps) => {
  const captions = usePlayerCaptions({ videoRef, sessionId, available: true });
  return <>
    {captions.failed ? <span role="status" className="text-sm">A legenda não está disponível agora.</span>
      : <Button aria-label="Legenda" aria-pressed={captions.enabled}
        title={captions.enabled ? 'Desligar a legenda' : 'Ligar a legenda'} onClick={captions.toggle}
        className={captions.enabled ? 'bg-primary underline' : ''}>CC</Button>}
    {captions.text && captionContainer ? createPortal(<div aria-hidden="true" className="player-caption-layer pointer-events-none">
      <span>{captions.text}</span>
    </div>, captionContainer) : null}
  </>;
};
