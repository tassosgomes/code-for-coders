import { lessonId } from '@/testing/student-lesson-data';

export const playbackData = () => ({
  sessionId: '00000000-0000-7000-8000-000000000004', lessonId,
  expiresAt: new Date(Date.now() + 300_000).toISOString(), renewAfter: new Date(Date.now() + 210_000).toISOString(),
  watermark: { text: 'student@example.com', repositionSeconds: 1 },
  progress: { intervalSeconds: 30, minGapSeconds: 10 },
  segmentAccess: { query: 'st=segment-test-secret&e=1999999999', expiresAt: new Date(Date.now() + 300_000).toISOString() },
});
