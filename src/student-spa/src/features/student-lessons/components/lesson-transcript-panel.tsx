import { memo, useId } from 'react';
import { Play, Search } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import type { TranscriptSegment } from '@/features/student-lessons/api/get-lesson-transcription';
import { formatTranscriptTime,
  type TranscriptMatch } from '@/features/student-lessons/utils/transcript-search';
import { useLessonTranscript } from '@/features/student-lessons/hooks/use-lesson-transcript';

type TranscriptRowProps = {
  segment: TranscriptSegment; index: number; current: boolean; matches: TranscriptMatch[];
  selectedStart?: number; onSeek: (seconds: number) => void;
};
const TranscriptRow = memo(({ segment, index, current, matches, selectedStart, onSeek }: TranscriptRowProps) => {
  const pieces = [];
  let cursor = 0;
  for (const match of matches) {
    pieces.push(segment.text.slice(cursor, match.start));
    pieces.push(<mark key={match.start} className={match.start === selectedStart
      ? 'rounded bg-secondary font-bold underline outline outline-primary' : 'rounded bg-secondary underline'}>
      {segment.text.slice(match.start, match.end)}</mark>);
    cursor = match.end;
  }
  pieces.push(segment.text.slice(cursor));
  return <button type="button" data-segment={index} aria-current={current ? 'true' : undefined}
    onClick={() => onSeek(segment.startMs / 1000)}
    className="transcript-segment"
    style={{ contentVisibility: 'auto', containIntrinsicSize: 'auto 72px' }}>
    <span aria-hidden="true" className="transcript-segment-bar" />
    <span aria-hidden="true" className="transcript-segment-marker">{current ? <Play /> : null}</span>
    <span className="transcript-segment-time">{formatTranscriptTime(segment.startMs)}</span>
    <span className="transcript-segment-text">{pieces}{current ? <span className="sr-only"> trecho atual</span> : null}</span>
  </button>;
});

type LessonTranscriptPanelProps = { segments: TranscriptSegment[]; seconds: number; onSeek: (seconds: number) => void };
const emptyMatches: TranscriptMatch[] = [];

export const LessonTranscriptPanel = ({ segments, seconds, onSeek }: LessonTranscriptPanelProps) => {
  const { query, selection, following, listRef, needle, matches, bySegment, selected, current,
    move, updateQuery, clear, pauseFollowing, resumeFollowing } = useLessonTranscript(segments, seconds);

  const searchId = useId();

  return <div className="transcript-panel">
    <div className="transcript-search">
      <label htmlFor={searchId}>Buscar na aula</label>
      <div className="transcript-search-field">
        <Search aria-hidden="true" />
        <Input id={searchId} type="search" placeholder="Palavra ou trecho" value={query}
          onChange={(event) => updateQuery(event.target.value)} onKeyDown={(event) => {
            if (event.key === 'Enter') { event.preventDefault(); move(event.shiftKey ? -1 : 1); }
            if (event.key === 'Escape') clear();
          }} />
        {query ? <Button variant="ghost" aria-label="Limpar busca" onClick={clear}>×</Button> : null}
      </div>
    </div>
    {needle ? <div className="flex items-center justify-between gap-2">
      <p role="status" aria-live="polite">{matches.length ? `${selection + 1} de ${matches.length} resultados` : 'Nenhum trecho encontrado para essa busca.'}</p>
      <Button variant="outline" className="min-h-11 min-w-11" aria-label="Ocorrência anterior" disabled={!matches.length} onClick={() => move(-1)}>‹</Button>
      <Button variant="outline" className="min-h-11 min-w-11" aria-label="Próxima ocorrência" disabled={!matches.length} onClick={() => move(1)}>›</Button>
    </div> : null}
    <div ref={listRef} role="region" aria-label="Texto da transcrição" tabIndex={0}
      className="transcript-list"
      onWheel={pauseFollowing} onTouchMove={pauseFollowing}
      onPointerDown={pauseFollowing} onKeyDown={(event) => {
        if (['ArrowUp', 'ArrowDown', 'PageUp', 'PageDown', 'Home', 'End', ' '].includes(event.key)) pauseFollowing();
      }}>
      {segments.map((segment, index) => !needle || bySegment.has(index)
        ? <TranscriptRow key={index} segment={segment} index={index} current={current === index}
          matches={bySegment.get(index) ?? emptyMatches} selectedStart={selected?.segment === index ? selected.start : undefined} onSeek={onSeek} /> : null)}
    </div>
    {!following && !needle ? <Button className="absolute bottom-3 left-1/2 -translate-x-1/2" onClick={resumeFollowing}>Ir para o trecho atual</Button> : null}
    <span className="sr-only" aria-live="polite">{following && !needle ? 'Acompanhando o trecho atual' : ''}</span>
  </div>;
};
