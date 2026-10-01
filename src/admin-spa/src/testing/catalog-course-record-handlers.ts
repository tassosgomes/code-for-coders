import { http, HttpResponse } from 'msw';

import { env } from '@/config/env';

export const catalogCourseRecordFixture = {
  courseId: '0198dfac-674a-7000-8000-000000000041', title: 'Fundamentos de C#', level: null,
  prerequisite: { text: 'Conhecimentos básicos de programação.', recommendedCourses: [{ courseId: '0198dfac-674a-7000-8000-000000000042', title: 'Lógica atualizada' }] },
  tagline: null, inShowcase: false, offers: [],
};

export const catalogCourseRecordHandlers = [
  http.get(`${env.API_URL}/api/v1/catalog/courses/:courseId`, () => HttpResponse.json(catalogCourseRecordFixture)),
  http.patch(`${env.API_URL}/api/v1/catalog/courses/:courseId`, () => HttpResponse.json(catalogCourseRecordFixture)),
];
