import { http, HttpResponse } from 'msw';

import { env } from '@/config/env';
import { courseSchema, type Course } from '@/features/course-authoring/types/course';
import { structureCourseFixture } from '@/testing/authoring-structure-handlers';
import { videoTranscriptionFixtures } from '@/testing/handlers';

export const readyVideoFixtures = [
  { videoId: '0198dfac-674a-7000-8000-000000000051', title: 'Vídeo da colega', status: 'ready', tags: ['Docker', 'Módulo 2'], durationSeconds: 125, uploadedBy: { accountId: '0198dfac-674a-7000-8000-000000000031', name: 'Marina' } },
  { videoId: '0198dfac-674a-7000-8000-000000000052', title: 'Vídeo do professor', status: 'ready', tags: ['Docker'], durationSeconds: 90, uploadedBy: { accountId: '0198dfac-674a-7000-8000-000000000032', name: 'Rafael' } },
];
// Em preparação com as mesmas tags do primeiro vídeo pronto: o seletor nunca pode listá-lo (RN-C04).
const preparingVideoFixture = { videoId: '0198dfac-674a-7000-8000-000000000053', title: 'Vídeo em preparação', status: 'preparing', tags: ['Docker', 'Módulo 2'], durationSeconds: null, uploadedBy: { accountId: '0198dfac-674a-7000-8000-000000000033', name: 'Júlia' } };
const videoFixtures = [...readyVideoFixtures, preparingVideoFixture];
const hasTag = (video: { tags: string[] }, tag: string) => video.tags.some((item) => item.toLowerCase() === tag.toLowerCase());

export const createAuthoringVideoHandlers = () => {
  const course: Course = courseSchema.parse(structuredClone(structureCourseFixture));
  let unavailable = false; let listFails = false; let unknownStatus = false;
  const writes: { videoId: string | null; key: string | null }[] = [];
  const lists: URL[] = [];
  return {
    writes, lists, snapshot: () => course,
    setUnavailable: (value: boolean) => { unavailable = value; }, setListFails: (value: boolean) => { listFails = value; },
    setUnknownStatus: (value: boolean) => { unknownStatus = value; },
    handlers: [
      http.get(`${env.API_URL}/api/v1/courses/:courseId`, () => HttpResponse.json(course)),
      http.get(`${env.API_URL}/api/v1/videos`, ({ request }) => {
        const url = new URL(request.url); lists.push(url);
        if (listFails) return HttpResponse.json({ code: 'MEDIA_UNAVAILABLE' }, { status: 502 });
        const query = url.searchParams.get('q') ?? '';
        const statuses = url.searchParams.getAll('status');
        const tags = url.searchParams.getAll('tag');
        const tagMatch = url.searchParams.get('tagMatch') ?? 'all';
        const matches = videoFixtures
          .filter((video) => statuses.length === 0 || statuses.includes(video.status))
          .filter((video) => video.title.includes(query))
          .filter((video) => tags.length === 0 || (tagMatch === 'any'
            ? tags.some((tag) => hasTag(video, tag))
            : tags.every((tag) => hasTag(video, tag))));
        const data = matches.map((video) => ({ ...video, status: unknownStatus ? 'preparing' : video.status,
          transcription: { ...videoTranscriptionFixtures['not-requested'], canStart: !unknownStatus && video.status === 'ready' },
        }));
        return HttpResponse.json({ data, pagination: { page: 1, size: 20, total: data.length, totalPages: data.length ? 1 : 0 } });
      }),
      http.get(`${env.API_URL}/api/v1/video-tags`, ({ request }) => {
        const query = (new URL(request.url).searchParams.get('q') ?? '').toLowerCase();
        const catalog = Array.from(new Set(videoFixtures.flatMap((video) => video.tags))).sort((a, b) => a.localeCompare(b));
        return HttpResponse.json({ data: catalog.filter((tag) => tag.toLowerCase().includes(query)) });
      }),
      http.patch(`${env.API_URL}/api/v1/courses/:courseId/lessons/:lessonId`, async ({ request, params }) => {
        const body = await request.json() as { videoId: string | null }; // The typed client sends this validated patch to MSW's HTTP boundary.
        writes.push({ videoId: body.videoId, key: request.headers.get('Idempotency-Key') });
        if (unavailable) return HttpResponse.json({ code: 'VIDEO_NOT_AVAILABLE' }, { status: 422 });
        const lesson = course.modules.flatMap((module) => module.lessons).find((item) => item.lessonId === params.lessonId);
        if (!lesson) return HttpResponse.json({}, { status: 404 });
        const video = readyVideoFixtures.find((item) => item.videoId === body.videoId);
        lesson.video = video ? { videoId: video.videoId, title: video.title, durationSeconds: video.durationSeconds } : null;
        course.draftRevision++; return HttpResponse.json(course);
      }),
    ],
  };
};
