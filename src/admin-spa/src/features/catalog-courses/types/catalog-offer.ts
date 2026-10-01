import { z } from 'zod';

export const accessPeriodSchema = z.discriminatedUnion('type', [
  z.object({ type: z.literal('months'), months: z.number().int().min(1).max(60) }).strict(),
  z.object({ type: z.literal('lifetime') }).strict(),
]);
export const catalogOfferSchema = z.object({
  offerId: z.uuid(), courseId: z.uuid(), name: z.string().min(1).max(60), priceCents: z.number().int().min(1).max(9999999),
  accessPeriod: accessPeriodSchema, status: z.enum(['draft', 'published', 'unpublished']), purchaseIntentCount: z.number().int().min(0),
  createdAt: z.iso.datetime({ offset: true }), updatedAt: z.iso.datetime({ offset: true }), publishedAt: z.iso.datetime({ offset: true }).nullable(),
});
export type CatalogOffer = z.infer<typeof catalogOfferSchema>;
