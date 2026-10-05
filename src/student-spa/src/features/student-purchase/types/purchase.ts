import * as z from 'zod';

export const accessPeriodSchema = z.union([z.object({ type: z.literal('lifetime') }), z.object({ type: z.literal('months'), months: z.number().int().min(1).max(60) })]);
const courseSchema = z.object({ courseId: z.uuid(), title: z.string() });
const offerSchema = z.object({ offerId: z.uuid(), name: z.string() });
const timestampSchema = z.iso.datetime({ offset: true });
export const purchaseSummarySchema = z.object({
  course: courseSchema,
  offer: offerSchema.extend({ priceCents: z.number().int().positive(), currency: z.literal('BRL'), accessPeriod: accessPeriodSchema }),
  pendingOrderId: z.uuid().nullable(), existingAccessChecked: z.boolean(),
  existingAccess: z.object({ origin: z.string(), validity: z.union([z.object({ type: z.literal('lifetime') }), z.object({ type: z.literal('until'), endsOn: z.iso.date() })]) }).nullable(),
});
export const studentOrderSchema = z.object({
  orderId: z.uuid(), number: z.string(), status: z.string(), course: courseSchema, offer: offerSchema,
  priceCents: z.number().int().positive(), currency: z.literal('BRL'), accessPeriod: accessPeriodSchema,
  paymentMethod: z.string().nullable(), pendingPayment: z.object({ method: z.string(), expiresAt: timestampSchema }).nullable(),
  paymentPageExpiresAt: timestampSchema.nullable(), accessGrantedAt: timestampSchema.nullable(), createdAt: timestampSchema,
  paidAt: timestampSchema.nullable(), expiredAt: timestampSchema.nullable(), cancelledAt: timestampSchema.nullable(),
});
export type PurchaseSummary = z.infer<typeof purchaseSummarySchema>;
export type StudentOrder = z.infer<typeof studentOrderSchema>;
