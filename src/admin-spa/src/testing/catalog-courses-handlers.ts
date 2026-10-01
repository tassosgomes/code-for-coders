import { http, HttpResponse } from 'msw';

import { env } from '@/config/env';

export const catalogCourseFixture = { courseId: '0198dfac-674a-7000-8000-000000000011', title: 'Fundamentos de C#', level: null, inShowcase: false,
  offerCounts: { draft: 0, published: 0, unpublished: 0 } };
export const catalogCoursesHandlers = [http.get(`${env.API_URL}/api/v1/catalog/courses`, ({ request }) => {
  const page = Number(new URL(request.url).searchParams.get('_page') ?? 1);
  return HttpResponse.json({ data: [catalogCourseFixture], pagination: { page, size: 10, total: 11, totalPages: 2 } });
})];
