import axios from 'axios';
import { useState, type ReactNode } from 'react';

import { Dialog } from '@/components/ui/dialog';
import { useDeleteOffer } from '@/features/catalog-courses/api/delete-offer';
import { CatalogAlert } from '@/features/catalog-courses/components/catalog-alert';
import type { CatalogOffer, CatalogProblem } from '@/features/catalog-courses/types/catalog-offer';

type CatalogOfferDeleteProps = { offer: CatalogOffer; onDeleted: () => void; renderTrigger: (confirm: () => void) => ReactNode };
export const CatalogOfferDelete = ({ offer, onDeleted, renderTrigger }: CatalogOfferDeleteProps) => {
  const deletion = useDeleteOffer(); const [key, setKey] = useState<string>(); const [error, setError] = useState<CatalogProblem>();
  const submit = async () => {
    if (!key) return; setError(undefined);
    try { await deletion.mutateAsync({ courseId: offer.courseId, offerId: offer.offerId, idempotencyKey: key }); setKey(undefined); onDeleted(); }
    catch (failure) { setError(axios.isAxiosError<{ code?: string }>(failure) && failure.response?.data.code === 'OFFER_STATE_CONFLICT'
      ? { title: 'Esta oferta já foi publicada.', detail: 'Ela não pode mais ser excluída. Atualize a ficha do curso.' }
      : { title: 'Não foi possível excluir agora.', detail: 'O rascunho continua na lista. Tente de novo.', retry: true }); }
  };
  if (offer.status !== 'draft') return <>{renderTrigger(() => undefined)}</>;
  return <>{renderTrigger(() => { setKey(crypto.randomUUID()); setError(undefined); })}
    {key ? <Dialog className="catalog-dialog" role="alertdialog" title={`Excluir o rascunho “${offer.name}”?`} description="O rascunho será apagado e não poderá ser recuperado." busy={deletion.isPending} onClose={() => setKey(undefined)}>
      {error ? <CatalogAlert {...error} onRetry={() => void submit()} /> : null}
      <div className="dialog-actions"><button autoFocus type="button" className="outline-button" disabled={deletion.isPending} onClick={() => setKey(undefined)}>Cancelar</button>
        <button type="button" className="primary-button course-delete-confirm" disabled={deletion.isPending} onClick={() => void submit()}>{deletion.isPending ? 'Excluindo…' : 'Excluir'}</button></div>
    </Dialog> : null}</>;
};
