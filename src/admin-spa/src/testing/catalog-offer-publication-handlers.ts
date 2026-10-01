import { http, HttpResponse } from 'msw';

import { env } from '@/config/env';
import { catalogCourseRecordFixture } from '@/testing/catalog-course-record-handlers';
import { catalogOfferFixture } from '@/testing/catalog-offer-handlers';

export const createOfferPublicationHandlers = (status = 'draft', lifetime = false) => {
  let offer: Omit<typeof catalogOfferFixture, 'accessPeriod' | 'publishedAt'> & { accessPeriod: { type: string; months?: number }; publishedAt: string | null } = { ...catalogOfferFixture, status, publishedAt: null,
    accessPeriod: lifetime ? { type: 'lifetime' } : { type: 'months', months: 12 } };
  const keys: (string | null)[] = [];
  return { keys, handlers: [
    http.get(`${env.API_URL}/api/v1/catalog/courses/:courseId`, () => HttpResponse.json({ ...catalogCourseRecordFixture, offers: [offer] })),
    http.post(`${env.API_URL}/api/v1/catalog/offers/:offerId/publish`, ({ request }) => {
      keys.push(request.headers.get('Idempotency-Key'));
      offer = { ...offer, status: 'published', publishedAt: '2026-09-30T12:00:00Z' };
      return HttpResponse.json(offer);
    }),
    http.get(`${env.API_URL}/api/v1/staff-sessions/current`, () => HttpResponse.json({
      accountId: '0198dfac-674a-7000-8000-000000000002', name: 'Financeiro', roles: ['financeiro'], permissions: ['oferta.editar'], csrfToken: 'publication-csrf',
    })),
  ] };
};
