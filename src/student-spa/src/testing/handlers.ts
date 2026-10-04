import { http, HttpResponse } from 'msw';

import { env } from '@/config/env';
import { playbackData } from '@/testing/playback-data';
import { studentLessonData } from '@/testing/student-lesson-data';

export const handlers = [
  http.post(env.API_URL + "/api/v1/playback-sessions/:sessionId/renewals", () => HttpResponse.json(playbackData())), 
  http.post(env.API_URL + "/api/v1/playback-sessions/:sessionId/progress", () => HttpResponse.json({ recorded: true })), 
  http.post(env.API_URL + "/api/v1/lessons/:lessonId/playback-sessions", () => HttpResponse.json(playbackData(), { status: 201 })),
  http.get("*/api/v1/playback-sessions/:sessionId/playlist", () => HttpResponse.text("#EXTM3U")),
  http.get("*/api/v1/playback-sessions/:sessionId/variants/:quality", () => HttpResponse.text("#EXTM3U")),
  http.get("*/api/v1/playback-sessions/:sessionId/key", () => new HttpResponse(new Uint8Array(16))),
  http.get(`${env.API_URL}/api/v1/lessons/:lessonId`, () => HttpResponse.json(studentLessonData)),
  http.get(`${env.API_URL}/api/v1/student-sessions/current`, () =>
    HttpResponse.json({
      accountId: '00000000-0000-4000-8000-000000000001',
      name: 'Ana Souza',
      csrfToken: 'student-session-csrf',
    }),
  ),
  http.post(`${env.API_URL}/api/v1/student-sessions`, () =>
    HttpResponse.json({
      accountId: '00000000-0000-4000-8000-000000000001',
      name: 'Ana Souza',
      csrfToken: 'student-session-csrf',
    }),
  ),
  http.delete(`${env.API_URL}/api/v1/student-sessions/current`, () =>
    new HttpResponse(null, { status: 204 }),
  ),
  http.get(`${env.API_URL}/v1/student/workspace/status`, () =>
    HttpResponse.json({
      status: 'ready',
      message: 'The student workspace service is ready.',
    }),
  ),
];
