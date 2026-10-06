export type OrderStudent = { studentId: string; name: string; email: string };
export type FinanceOrder = {
  orderId: string; number: string; student: OrderStudent; status: string; courseId: string;
  courseTitle: string; offerName: string; priceCents: number; currency: string;
  paymentMethod: string | null; createdAt: string; paidAt: string | null;
};
export type FinanceOrderPage = { data: FinanceOrder[]; pagination: { page: number; size: number; total: number; totalPages: number } };
export type FinanceOrderDetail = FinanceOrder & {
  offerId: string; accessPeriod: { type: string; months?: number }; paymentReference: string | null;
  paidAmountCents: number | null; grantId: string | null; accessGrantedAt: string | null;
  paymentPageExpiresAt: string | null; expiredAt: string | null; cancelledAt: string | null;
};
