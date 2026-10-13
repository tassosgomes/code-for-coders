import { describe, expect, it } from 'vitest';

import { findCurrentSegment, normalizeTranscriptText, searchTranscript } from './transcript-search';

describe('Lesson transcription search', () => {
  it('maps normalized matches back to original accented and punctuated text', () => {
    const original = 'Introdução! À ação — introdução.';
    const matches = searchTranscript([normalizeTranscriptText(original)], 'INTRODUCAO');
    expect(matches.map((match) => original.slice(match.start, match.end))).toEqual(['Introdução', 'introdução']);
    expect(searchTranscript([normalizeTranscriptText('docker-compose')], 'dockercompose')).toEqual([{ segment: 0, start: 0, end: 14 }]);
    expect(searchTranscript([normalizeTranscriptText(original)], '   ')).toEqual([]);
    const decomposed = 'Cafe\u0301'; const match = searchTranscript([normalizeTranscriptText(decomposed)], 'cafe')[0];
    expect(decomposed.slice(match?.start, match?.end)).toBe(decomposed);
  });

  it('prepares and searches 3000 segments within an interactive time budget', () => {
    const start = performance.now();
    const texts = Array.from({ length: 3000 }, (_, i) => normalizeTranscriptText(`Introdução ao Docker, trecho ${i}.`));
    const prepared = performance.now(); const matches = searchTranscript(texts, 'introducao'); const end = performance.now();
    expect(matches).toHaveLength(3000);
    expect(prepared - start).toBeLessThan(250);
    expect(end - prepared).toBeLessThan(100);
  });

  it('selects only the segment covering the current time, including gaps and exact boundaries', () => {
    const segments = [{ startMs: 0, endMs: 1000, text: 'first' }, { startMs: 2000, endMs: 3000, text: 'second' }];
    expect(findCurrentSegment(segments, 0)).toBe(0); expect(findCurrentSegment(segments, 1)).toBe(-1);
    expect(findCurrentSegment(segments, 2)).toBe(1); expect(findCurrentSegment(segments, 3)).toBe(-1);
  });
});
