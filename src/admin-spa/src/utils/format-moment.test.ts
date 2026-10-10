import { describe, expect, it } from 'vitest';

import { formatMoment, isRelativeMoment } from '@/utils/format-moment';

const now = new Date(2026, 9, 10, 16, 44);
const iso = (...parts: [number, number, number, number, number]) => new Date(...parts).toISOString();

describe('formatMoment', () => {
  it('shows today as "hoje, HH:mm"', () => {
    expect(formatMoment(iso(2026, 9, 10, 10, 20), now)).toBe('hoje, 10:20');
    expect(isRelativeMoment(iso(2026, 9, 10, 10, 20), now)).toBe(true);
  });
  it('shows the previous calendar day as "ontem, HH:mm", even across a month boundary', () => {
    expect(formatMoment(iso(2026, 9, 9, 23, 5), now)).toBe('ontem, 23:05');
    expect(formatMoment(iso(2026, 8, 30, 8, 0), new Date(2026, 9, 1, 9, 0))).toBe('ontem, 08:00');
  });
  it('shows older dates as "dd/mm/aaaa · HH:mm"', () => {
    expect(formatMoment(iso(2026, 9, 5, 10, 0), now)).toBe('05/10/2026 · 10:00');
    expect(isRelativeMoment(iso(2026, 9, 5, 10, 0), now)).toBe(false);
  });
  it('keeps future dates absolute', () => {
    expect(formatMoment(iso(2026, 9, 11, 10, 0), now)).toBe('11/10/2026 · 10:00');
  });
});
