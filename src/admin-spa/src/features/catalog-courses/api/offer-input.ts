import { z } from 'zod';

import { parseOfferPrice } from '@/features/catalog-courses/utils/offer-price';

export const offerInputSchema = z.object({
  name: z.string().min(1, 'Informe o nome da opção.').max(60, 'Use até 60 caracteres.'),
  price: z.string().refine((value) => !/[,.]\d{3,}$/.test(value), 'Use até duas casas decimais.')
    .refine((value) => parseOfferPrice(value) !== undefined, 'Informe um valor entre R$ 0,01 e R$ 99.999,99.'),
  periodType: z.enum(['months', 'lifetime']), months: z.string(),
}).superRefine((input, context) => {
  if (input.periodType === 'months' && (!/^\d{1,2}$/.test(input.months) || Number(input.months) < 1 || Number(input.months) > 60))
    context.addIssue({ code: 'custom', path: ['months'], message: 'Informe de 1 a 60 meses inteiros.' });
});
export type OfferInput = z.infer<typeof offerInputSchema>;
export type OfferVariables = { courseId: string; offerId?: string; input: OfferInput; idempotencyKey: string };
export const offerBody = (input: OfferInput) => {
  const value = offerInputSchema.parse(input);
  return { name: value.name, priceCents: parseOfferPrice(value.price)!, accessPeriod: value.periodType === 'lifetime'
    ? { type: 'lifetime' as const } : { type: 'months' as const, months: Number(value.months) } };
};
