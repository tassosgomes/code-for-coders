import { http, HttpResponse } from 'msw';

import { env } from '@/config/env';

export const courtesyCourseFixture = { courseId: '0198dfac-674a-7000-8000-000000000088', title: 'Fundamentos de C#' };
export const courtesyCourseHandlers = [http.get(`${env.API_URL}/api/v1/courtesy-courses`, () => HttpResponse.json({ data: [courtesyCourseFixture], pagination: { page: 1, size: 10, total: 1, totalPages: 1 } }))];
