import { purchaseCourseId, purchaseOrder } from '@/testing/student-purchase-data';

export const myOrdersLessonId = '0198dfac-674a-7000-8000-000000000044';
export const myOrders = [
  { ...purchaseOrder, number: '000124', paymentMethod: 'pix', pendingPayment: { method: 'pix', expiresAt: '2026-10-09T02:59:59Z' } },
  { ...purchaseOrder, orderId: '0198dfac-674a-7000-8000-000000000045', number: '000123', status: 'paid', paymentMethod: 'card', accessGrantedAt: '2026-10-05T14:20:00Z', paidAt: '2026-10-05T14:20:00Z', createdAt: '2026-10-04T14:19:30Z' },
  { ...purchaseOrder, orderId: '0198dfac-674a-7000-8000-000000000046', number: '000118', status: 'expired', paymentMethod: null, expiredAt: '2026-10-01T14:20:00Z', createdAt: '2026-09-30T14:19:30Z' },
] as const; // The three fixture positions are fixed: pending, paid, expired.
export const myOrdersPage = { data: myOrders, pagination: { page: 1, size: 10, total: 3, totalPages: 1 } };
export const myOrdersCourses = { progressAvailable: true, active: [{ courseId: purchaseCourseId, title: purchaseOrder.course.title, started: false, lastActivityAt: null, continueLessonId: myOrdersLessonId, progress: null }], ended: [] };
