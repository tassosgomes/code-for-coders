import { useState } from 'react';

import { offerBody, type OfferInput } from '@/features/catalog-courses/api/offer-input';
import { useOfferWrite } from '@/features/catalog-courses/hooks/use-offer-write';
import type { CatalogOffer } from '@/features/catalog-courses/types/catalog-offer';
import { parseOfferPrice } from '@/features/catalog-courses/utils/offer-price';

export const offerTermsChanged = (offer: CatalogOffer | undefined, input: OfferInput) => Boolean(offer
  && (offer.priceCents !== parseOfferPrice(input.price) || offer.accessPeriod.type !== input.periodType
    || input.periodType === 'months' && offer.accessPeriod.type === 'months' && offer.accessPeriod.months !== Number(input.months)));

export const useOfferChange = (courseId: string, offer: CatalogOffer | undefined, onSaved: () => void) => {
  const editing = useOfferWrite(courseId, offer?.offerId);
  const [pending, setPending] = useState<OfferInput>();
  const save = async (input: OfferInput) => {
    const saved = await editing.save(input);
    if (saved) onSaved();
    return saved;
  };
  const submit = async (input: OfferInput) => {
    if (offer?.status === 'published' && offerTermsChanged(offer, input)) {
      setPending(input); return false;
    }
    return save(input);
  };
  return { ...editing, submit, pending, nextOffer: pending ? offerBody(pending) : undefined,
    back: () => setPending(undefined), confirm: async () => { if (pending) await save(pending); } };
};
