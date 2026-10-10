const pad = (value: number) => String(value).padStart(2, '0');
const dayKey = (date: Date) => date.getFullYear() * 10000 + (date.getMonth() + 1) * 100 + date.getDate();
const time = (date: Date) => `${pad(date.getHours())}:${pad(date.getMinutes())}`;

// 'today' and 'yesterday' are relative; anything else (including future dates) is absolute.
const dayKind = (date: Date, now: Date) => {
  const yesterday = new Date(now.getFullYear(), now.getMonth(), now.getDate() - 1);
  return dayKey(date) === dayKey(now) ? 'today' : dayKey(date) === dayKey(yesterday) ? 'yesterday' : 'absolute';
};

export const isRelativeMoment = (value: string, now: Date = new Date()) => dayKind(new Date(value), now) !== 'absolute';

// "hoje, 10:20", "ontem, 10:20" or "05/10/2026 · 10:00", in the viewer's local time.
export const formatMoment = (value: string, now: Date = new Date()) => {
  const date = new Date(value);
  const kind = dayKind(date, now);
  if (kind === 'today') return `hoje, ${time(date)}`;
  if (kind === 'yesterday') return `ontem, ${time(date)}`;
  return `${pad(date.getDate())}/${pad(date.getMonth() + 1)}/${date.getFullYear()} · ${time(date)}`;
};
