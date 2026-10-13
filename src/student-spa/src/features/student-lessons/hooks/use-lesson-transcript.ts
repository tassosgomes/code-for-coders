import { useEffect, useMemo, useRef, useState } from 'react';

import type { TranscriptSegment } from '@/features/student-lessons/api/get-lesson-transcription';
import { findCurrentSegment, normalizeTranscriptText, searchTranscript, type TranscriptMatch } from '@/features/student-lessons/utils/transcript-search';

export const useLessonTranscript = (segments: TranscriptSegment[], seconds: number) => {
  const [query, setQuery] = useState('');
  const [selection, setSelection] = useState(0);
  const [following, setFollowing] = useState(true);
  const listRef = useRef<HTMLDivElement>(null);
  const normalized = useMemo(() => segments.map((segment) => normalizeTranscriptText(segment.text)), [segments]);
  const needle = normalizeTranscriptText(query).text.trim();
  const matches = useMemo(() => searchTranscript(normalized, needle), [normalized, needle]);
  const bySegment = useMemo(() => {
    const groups = new Map<number, TranscriptMatch[]>();
    for (const match of matches) {
      const group = groups.get(match.segment) ?? []; group.push(match); groups.set(match.segment, group);
    }
    return groups;
  }, [matches]);
  const selected = matches[selection % (matches.length || 1)];
  const current = findCurrentSegment(segments, seconds);
  const scrollTo = (index: number) => listRef.current?.querySelector<HTMLElement>(`[data-segment="${index}"]`)
    ?.scrollIntoView?.({ block: 'nearest', behavior: 'auto' });
  useEffect(() => { if (needle && selected) scrollTo(selected.segment); }, [needle, selected]);
  useEffect(() => { if (!needle && following) scrollTo(current); }, [needle, following, current]);
  const move = (direction: number) => setSelection((previous) => (previous + direction + matches.length) % (matches.length || 1));
  const updateQuery = (value: string) => { setQuery(value); setSelection(0); if (!value.trim()) setFollowing(true); };
  const clear = () => { updateQuery(''); setFollowing(true); };
  const resumeFollowing = () => { setFollowing(true); scrollTo(current); };
  const pauseFollowing = () => setFollowing(false);

  return { query, selection, following, listRef, needle, matches, bySegment, selected, current,
    move, updateQuery, clear, pauseFollowing, resumeFollowing };
};
