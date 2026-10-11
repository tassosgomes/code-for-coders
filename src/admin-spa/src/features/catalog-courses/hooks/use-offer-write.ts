import axios from 'axios';
import { useRef, useState } from 'react';

import { useCreateOffer } from '@/features/catalog-courses/api/create-offer';
import { useUpdateOffer } from '@/features/catalog-courses/api/update-offer';
import { offerBody } from '@/features/catalog-courses/api/offer-input';
import type { OfferInput } from '@/features/catalog-courses/api/offer-input';
import type { CatalogProblem } from '@/features/catalog-courses/types/catalog-offer';

export const useOfferWrite = (courseId: string, offerId?: string) => {
  const creation = useCreateOffer(); const update = useUpdateOffer();
  const intent = useRef<{ body: string; key: string } | null>(null);
  const [error, setError] = useState<CatalogProblem>();
  const [fieldErrors, setFieldErrors] = useState<Partial<Record<keyof OfferInput, string>>>({});
  const save = async (input: OfferInput) => {
    const body = JSON.stringify(offerBody(input));
    const key = intent.current?.body === body ? intent.current.key : crypto.randomUUID();
    intent.current = { body, key }; setError(undefined); setFieldErrors({});
    try {
      await (offerId ? update : creation).mutateAsync({ courseId, offerId, input, idempotencyKey: key });
      intent.current = null; return true;
    } catch (failure) {
      const problem = axios.isAxiosError<{ code?: string; detail?: string }>(failure) ? failure.response?.data : undefined;
      const field = problem?.detail?.split(' ')[0];
      if (problem?.code === 'FIELD_INVALID' && field === 'name') setFieldErrors({ name: 'Informe um nome de 1 a 60 caracteres.' });
      else if (problem?.code === 'FIELD_INVALID' && field === 'priceCents') setFieldErrors({ price: 'Informe um valor entre R$ 0,01 e R$ 99.999,99.' });
      else if (problem?.code === 'FIELD_INVALID' && field === 'accessPeriod') setFieldErrors({ months: 'Informe de 1 a 60 meses inteiros.' });
      else setError(problem?.code === 'FIELD_INVALID' && field === 'offers' ? { title: 'O curso já tem 50 ofertas.', detail: 'Exclua um rascunho primeiro.' }
        : problem?.code === 'IDEMPOTENCY_KEY_REUSED' ? { title: 'Esta tentativa já foi usada para outra alteração.', detail: 'Edite os campos e salve novamente.' }
          : { title: 'Não foi possível salvar agora.', detail: offerId ? 'Nada foi alterado. Tente de novo.' : 'Seus dados foram mantidos. Tente de novo.', retry: true });
      return false;
    }
  };
  return { save, busy: creation.isPending || update.isPending, error, fieldErrors };
};
