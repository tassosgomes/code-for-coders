import axios from 'axios';
import { useRef, useState } from 'react';

import { usePublishOffer } from '@/features/catalog-courses/api/publish-offer';
import type { CatalogOffer } from '@/features/catalog-courses/types/catalog-offer';

export const useOfferPublication = (offer: CatalogOffer, onPublished: () => void) => {
  const publication = usePublishOffer();
  const key = useRef<string | undefined>(undefined);
  const inFlight = useRef(false);
  const [open, setOpen] = useState(false);
  const [error, setError] = useState<string>();
  const confirm = () => { key.current = crypto.randomUUID(); setError(undefined); setOpen(true); };
  const close = () => { if (!inFlight.current) setOpen(false); };
  const publish = async () => {
    if (!key.current || inFlight.current) return;
    inFlight.current = true; setError(undefined);
    try {
      await publication.mutateAsync({ courseId: offer.courseId, offerId: offer.offerId, idempotencyKey: key.current });
      setOpen(false); onPublished();
    } catch (failure) {
      const code = axios.isAxiosError<{ code?: string }>(failure) ? failure.response?.data.code : undefined;
      setError(code === 'COURSE_LEVEL_REQUIRED'
        ? 'O curso não declara nível. O professor precisa declarar o nível e publicar o curso.'
        : code === 'OFFER_STATE_CONFLICT' ? 'Esta oferta já está publicada. Atualize a ficha do curso.'
          : 'Não foi possível publicar agora.');
    } finally { inFlight.current = false; }
  };
  return { confirm, close, publish, open, error, busy: publication.isPending };
};
