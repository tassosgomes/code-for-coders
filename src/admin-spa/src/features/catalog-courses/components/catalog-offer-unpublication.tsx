import type { ReactNode } from 'react';

import { Dialog } from '@/components/ui/dialog';
import { CatalogAlert } from '@/features/catalog-courses/components/catalog-alert';
import { useOfferUnpublication } from '@/features/catalog-courses/hooks/use-offer-unpublication';
import type { CatalogOffer } from '@/features/catalog-courses/types/catalog-offer';

type CatalogOfferUnpublicationProps = { offer: CatalogOffer; lastPublished: boolean; onUnpublished: () => void; renderTrigger: (confirm: () => void) => ReactNode };
export const CatalogOfferUnpublication = ({ offer, lastPublished, onUnpublished, renderTrigger }: CatalogOfferUnpublicationProps) => {
  const action = useOfferUnpublication(offer, onUnpublished);
  return <>{renderTrigger(action.confirm)}
    {action.open ? <Dialog className="catalog-dialog" role="alertdialog" title={`Despublicar “${offer.name}”?`} description="A opção sai da página pública e não aceita compra nova. Quem já comprou não é afetado. Você pode publicá-la de novo." busy={action.busy} onClose={action.close}>
      {action.error ? <CatalogAlert {...action.error} onRetry={() => void action.unpublish()} />
        : lastPublished ? <CatalogAlert tone="info" title="O curso também sai da vitrine." detail="Esta é a única oferta publicada do curso." /> : null}
      <div className="dialog-actions">
        <button autoFocus type="button" className="outline-button" disabled={action.busy} onClick={action.close}>Cancelar</button>
        <button type="button" className="primary-button" disabled={action.busy} onClick={() => void action.unpublish()}>
          {action.busy ? 'Despublicando…' : 'Despublicar'}
        </button>
      </div>
    </Dialog> : null}
  </>;
};
