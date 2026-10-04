import { useEffect, useRef, useState } from 'react';

import { Button } from '@/components/ui/button';

const noticeMilliseconds = 8000;
type ResumeNoticeProps = { seconds: number; onRestart: () => void };

export const ResumeNotice = ({ seconds, onRestart }: ResumeNoticeProps) => {
  const regionRef = useRef<HTMLDivElement>(null);
  const [dismissed, setDismissed] = useState(false);
  const [expired, setExpired] = useState(false);
  useEffect(() => {
    const timer = setTimeout(() => {
      setExpired(true);
      if (!regionRef.current?.contains(document.activeElement)) setDismissed(true);
    }, noticeMilliseconds);
    return () => clearTimeout(timer);
  }, []);
  if (dismissed) return null;
  const time = `${Math.floor(seconds / 60)}:${String(seconds % 60).padStart(2, '0')}`;
  return <div ref={regionRef} role="status" aria-live="polite" className="mt-3 flex flex-wrap items-center gap-3 rounded-lg bg-secondary p-3"
    onBlur={(event) => { if (expired && !event.currentTarget.contains(event.relatedTarget)) setDismissed(true); }}>
    <span>Retomando de {time}</span>
    <Button variant="outline" onClick={() => { onRestart(); setDismissed(true); }}>Começar do início</Button>
  </div>;
};
