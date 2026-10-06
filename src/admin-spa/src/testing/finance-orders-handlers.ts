import { http, HttpResponse } from 'msw';

import { env } from '@/config/env';

export const financeCourseId = '9a7e5c31-2b4d-4f68-8e01-6d3c5a7b9f24';
export const financeStudentId = '9d8c7b6a-5f4e-4d3c-8b2a-1f0e9d8c7b6a';
export const financeOrderId = '1b2c3d4e-5f60-4718-9a2b-3c4d5e6f7a81';
export const financeOrderFixture = {
  orderId: financeOrderId, number: '000123', student: { studentId: financeStudentId, name: 'Ana Souza', email: 'ana.souza@example.com' },
  status: 'paid', courseId: financeCourseId, courseTitle: 'React na prática', offerName: 'Acesso por 12 meses', priceCents: 49700, currency: 'BRL',
  paymentMethod: 'card', createdAt: '2026-10-05T14:19:00Z', paidAt: '2026-10-05T14:21:00Z',
};
export const financeDetailFixture = { ...financeOrderFixture, offerId: '7c8d9e0f-1a2b-4c3d-8e4f-5a6b7c8d9e0f', accessPeriod: { type: 'months', months: 12 }, paymentReference: 'pi_3Q2w3E4r5T6y7U8i0', paidAmountCents: 39700, grantId: 'd69bd911-9631-4f59-8242-c43629806177', accessGrantedAt: '2026-10-05T14:21:00Z', paymentPageExpiresAt: null, expiredAt: null, cancelledAt: null };
export const financePage = (page = 1) => ({ data: [financeOrderFixture], pagination: { page, size: 20, total: 21, totalPages: 2 } });
export const financeOrdersHandlers = [
  http.get(`${env.API_URL}/api/v1/catalog/courses`, () => HttpResponse.json({ data: [{ courseId: financeCourseId, title: 'React na prática', level: 'beginner', inShowcase: true, offerCounts: { draft: 0, published: 1, unpublished: 0 } }], pagination: { page: 1, size: 50, total: 1, totalPages: 1 } })),
  http.get(`${env.API_URL}/api/v1/finance/orders`, ({ request }) => HttpResponse.json(financePage(Number(new URL(request.url).searchParams.get('_page') ?? 1)))),
  http.get(`${env.API_URL}/api/v1/finance/orders/:orderId`, () => HttpResponse.json(financeDetailFixture)),
  http.post(`${env.API_URL}/api/v1/student-account-lookups`, () => HttpResponse.json({ ...financeOrderFixture.student, emailConfirmed: true, status: 'active' })),
];
