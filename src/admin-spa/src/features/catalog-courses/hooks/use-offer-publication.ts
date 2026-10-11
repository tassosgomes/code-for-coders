import axios from 'axios';
import { useRef, useState } from 'react';

import { usePublishOffer } from '@/features/catalog-courses/api/publish-offer';
import type { CatalogOffer, CatalogProblem } from '@/features/catalog-courses/types/catalog-offer';

export const useOfferPublication = (offer: CatalogOffer, onPublished: () => void) => {
  const publication = usePublishOffer();
  const key = useRef<string | undefined>(undefined);
  const inFlight = useRef(false);
  const [open, setOpen] = useState(false);
  const [error, setError] = useState<CatalogProblem>();
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
        ? { title: 'O curso não declara nível.', detail: 'O professor precisa declarar o nível e publicar o curso. A oferta continua em rascunho.' }
        : code === 'OFFER_STATE_CONFLICT' ? { title: 'Esta oferta já está publicada.', detail: 'Atualize a ficha do curso.' }
          : { title: 'Não foi possível publicar agora.', detail: 'Tente de novo: a operação é segura de repetir.', retry: true });
    } finally { inFlight.current = false; }
  };
  return { confirm, close, publish, open, error, busy: publication.isPending };
};
