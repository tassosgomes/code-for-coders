import { http, HttpResponse } from 'msw';

import { env } from '@/config/env';
import { catalogCourseRecordFixture } from '@/testing/catalog-course-record-handlers';
import { catalogOfferFixture } from '@/testing/catalog-offer-handlers';

const published = (offerId: string, name: string) => ({ ...catalogOfferFixture, offerId, name, status: 'published', publishedAt: '2026-09-30T11:00:00Z' });
export const unpublishFirstOfferId = '0198dfac-674a-7000-8000-000000000061';
export const unpublishSecondOfferId = '0198dfac-674a-7000-8000-000000000062';

export const createOfferUnpublishHandlers = (offerCount: 1 | 2 = 1) => {
  let offers = [published(unpublishFirstOfferId, 'Acesso por 12 meses'), ...(offerCount === 2 ? [published(unpublishSecondOfferId, 'Acesso vitalício')] : [])];
  const keys: (string | null)[] = [];
  return { keys, handlers: [
    http.get(`${env.API_URL}/api/v1/catalog/courses/:courseId`, () => HttpResponse.json({
      ...catalogCourseRecordFixture, level: 'beginner', offers, inShowcase: offers.some((offer) => offer.status === 'published'),
    })),
    http.post(`${env.API_URL}/api/v1/catalog/offers/:offerId/unpublish`, ({ request, params }) => {
      keys.push(request.headers.get('Idempotency-Key'));
      offers = offers.map((offer) => offer.offerId === params.offerId ? { ...offer, status: 'unpublished' } : offer);
      return HttpResponse.json(offers.find((offer) => offer.offerId === params.offerId));
    }),
    http.get(`${env.API_URL}/api/v1/staff-sessions/current`, () => HttpResponse.json({
      accountId: '0198dfac-674a-7000-8000-000000000002', name: 'Financeiro', roles: ['financeiro'], permissions: ['oferta.editar'], csrfToken: 'unpublish-csrf',
    })),
  ] };
};
