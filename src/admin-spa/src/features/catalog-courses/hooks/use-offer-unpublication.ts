import axios from 'axios';
import { useRef, useState } from 'react';

import { useUnpublishOffer } from '@/features/catalog-courses/api/unpublish-offer';
import type { CatalogOffer } from '@/features/catalog-courses/types/catalog-offer';

export const useOfferUnpublication = (offer: CatalogOffer, onUnpublished: () => void) => {
  const unpublication = useUnpublishOffer();
  const key = useRef<string | undefined>(undefined);
  const inFlight = useRef(false);
  const [open, setOpen] = useState(false);
  const [error, setError] = useState<string>();
  const confirm = () => { key.current = crypto.randomUUID(); setError(undefined); setOpen(true); };
  const close = () => { if (!inFlight.current) setOpen(false); };
  const unpublish = async () => {
    if (!key.current || inFlight.current) return;
    inFlight.current = true; setError(undefined);
    try {
      await unpublication.mutateAsync({ courseId: offer.courseId, offerId: offer.offerId, idempotencyKey: key.current });
      setOpen(false); onUnpublished();
    } catch (failure) {
      const code = axios.isAxiosError<{ code?: string }>(failure) ? failure.response?.data.code : undefined;
      setError(code === 'OFFER_STATE_CONFLICT' ? 'Esta oferta não está mais publicada. Atualize a ficha do curso.' : 'Não foi possível despublicar agora.');
    } finally { inFlight.current = false; }
  };
  return { confirm, close, unpublish, open, error, busy: unpublication.isPending };
};
