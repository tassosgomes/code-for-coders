import type { ReactNode } from 'react';

import { Dialog } from '@/components/ui/dialog';
import { CatalogAlert } from '@/features/catalog-courses/components/catalog-alert';
import { OfferOption } from '@/features/catalog-courses/components/offer-option';
import { useOfferPublication } from '@/features/catalog-courses/hooks/use-offer-publication';
import type { CatalogOffer } from '@/features/catalog-courses/types/catalog-offer';

type CatalogOfferPublicationProps = { offer: CatalogOffer; onPublished: () => void; renderTrigger: (confirm: () => void) => ReactNode };
export const CatalogOfferPublication = ({ offer, onPublished, renderTrigger }: CatalogOfferPublicationProps) => {
  const action = useOfferPublication(offer, onPublished);
  // Recusas (sem nível, já publicada) não melhoram com outra tentativa: o diálogo só fecha.
  const refused = Boolean(action.error && !action.error.retry);
  return <>{renderTrigger(action.confirm)}
    {action.open ? <Dialog className="catalog-dialog" title="Publicar oferta" description="Confira o que o visitante vai ver na página do curso." busy={action.busy} onClose={action.close}>
      {action.error ? <CatalogAlert {...action.error} onRetry={() => void action.publish()} /> : null}
      <OfferOption offer={offer} />
      <p className="catalog-dialog-text">Ao publicar, esta opção aparece na página pública do curso.</p>
      <div className="dialog-actions">
        <button autoFocus type="button" className="outline-button" disabled={action.busy} onClick={action.close}>{refused ? 'Fechar' : 'Cancelar'}</button>
        {refused ? null : <button type="button" className="primary-button" disabled={action.busy} onClick={() => void action.publish()}>
          {action.busy ? 'Publicando…' : 'Publicar'}
        </button>}
      </div>
    </Dialog> : null}
  </>;
};
