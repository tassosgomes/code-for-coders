import { http, HttpResponse } from 'msw';
import { z } from 'zod';

import { env } from '@/config/env';
import { catalogCourseRecordFixture } from '@/testing/catalog-course-record-handlers';

const wireInput = z.object({ name: z.string(), priceCents: z.number().int(), accessPeriod: z.discriminatedUnion('type', [
  z.object({ type: z.literal('months'), months: z.number().int() }).strict(), z.object({ type: z.literal('lifetime') }).strict(),
]) }).strict();
export const catalogOfferFixture = {
  offerId: '0198dfac-674a-7000-8000-000000000051', courseId: catalogCourseRecordFixture.courseId,
  name: 'Acesso por 12 meses', priceCents: 49700, accessPeriod: { type: 'months', months: 12 }, status: 'draft', purchaseIntentCount: 0,
  createdAt: '2026-09-30T10:00:00Z', updatedAt: '2026-09-30T10:00:00Z', publishedAt: null,
};
type TestOffer = Omit<typeof catalogOfferFixture, 'accessPeriod'> & { accessPeriod: z.infer<typeof wireInput>['accessPeriod'] };
export const createCatalogOfferHandlers = (initial: TestOffer[] = []) => {
  let offers = [...initial];
  const writes: { method: string; key: string | null; body: unknown }[] = [];
  return { writes, handlers: [
    http.get(`${env.API_URL}/api/v1/catalog/courses/:courseId`, () => HttpResponse.json({ ...catalogCourseRecordFixture, offers })),
    http.post(`${env.API_URL}/api/v1/catalog/courses/:courseId/offers`, async ({ request }) => {
      const body = wireInput.parse(await request.json()); writes.push({ method: 'POST', key: request.headers.get('Idempotency-Key'), body });
      const offer = { ...catalogOfferFixture, ...body, offerId: crypto.randomUUID() }; offers.push(offer);
      return HttpResponse.json(offer, { status: 201 });
    }),
    http.patch(`${env.API_URL}/api/v1/catalog/offers/:offerId`, async ({ request, params }) => {
      const body = wireInput.parse(await request.json()); writes.push({ method: 'PATCH', key: request.headers.get('Idempotency-Key'), body });
      offers = offers.map((offer) => offer.offerId === params.offerId ? { ...offer, ...body } : offer);
      return HttpResponse.json(offers.find((offer) => offer.offerId === params.offerId));
    }),
    http.delete(`${env.API_URL}/api/v1/catalog/offers/:offerId`, ({ request, params }) => {
      writes.push({ method: 'DELETE', key: request.headers.get('Idempotency-Key'), body: null });
      offers = offers.filter((offer) => offer.offerId !== params.offerId); return new HttpResponse(null, { status: 204 });
    }),
  ] };
};
