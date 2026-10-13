import type { TranscriptSegment } from '@/features/student-lessons/api/get-lesson-transcription';

export type NormalizedText = { text: string; starts: number[]; ends: number[] };
export type TranscriptMatch = { segment: number; start: number; end: number };

export const normalizeTranscriptText = (original: string): NormalizedText => {
  let text = '';
  const starts: number[] = [];
  const ends: number[] = [];
  let offset = 0;
  for (const character of original) {
    const normalized = character.normalize('NFD').replace(/\p{M}|\p{P}/gu, '').toLowerCase();
    if (!normalized && /\p{M}/u.test(character) && ends.length) ends[ends.length - 1] = offset + character.length;
    for (const value of normalized) {
      if (/\s/u.test(value)) {
        if (text.endsWith(' ')) { ends[ends.length - 1] = offset + character.length; continue; }
        text += ' ';
      } else text += value;
      for (let i = 0; i < value.length; i++) { starts.push(offset); ends.push(offset + character.length); }
    }
    offset += character.length;
  }
  return { text, starts, ends };
};

export const searchTranscript = (texts: NormalizedText[], query: string): TranscriptMatch[] => {
  const needle = normalizeTranscriptText(query).text.trim();
  if (!needle) return [];
  const matches: TranscriptMatch[] = [];
  texts.forEach((normalized, segment) => {
    let from = 0;
    let at: number;
    while ((at = normalized.text.indexOf(needle, from)) !== -1) {
      const start = normalized.starts[at]; const end = normalized.ends[at + needle.length - 1];
      if (start !== undefined && end !== undefined) matches.push({ segment, start, end });
      from = at + needle.length;
    }
  });
  return matches;
};

export const findCurrentSegment = (segments: TranscriptSegment[], seconds: number) => {
  const milliseconds = seconds * 1000;
  let low = 0; let high = segments.length - 1; let candidate = -1;
  while (low <= high) {
    const middle = Math.floor((low + high) / 2);
    const segment = segments[middle];
    if (segment && segment.startMs <= milliseconds) { candidate = middle; low = middle + 1; }
    else high = middle - 1;
  }
  const segment = segments[candidate];
  return segment && milliseconds < segment.endMs ? candidate : -1;
};

export const formatTranscriptTime = (milliseconds: number) => {
  const seconds = Math.floor(milliseconds / 1000);
  const minutes = Math.floor(seconds / 60);
  return minutes < 60 ? `${minutes.toString().padStart(2, '0')}:${(seconds % 60).toString().padStart(2, '0')}`
    : `${Math.floor(minutes / 60)}:${(minutes % 60).toString().padStart(2, '0')}:${(seconds % 60).toString().padStart(2, '0')}`;
};
