import type { ReactNode } from 'react';

import { Dialog } from '@/components/ui/dialog';
import { OfferOption } from '@/features/catalog-courses/components/offer-option';
import { useOfferPublication } from '@/features/catalog-courses/hooks/use-offer-publication';
import type { CatalogOffer } from '@/features/catalog-courses/types/catalog-offer';

type CatalogOfferPublicationProps = { offer: CatalogOffer; onPublished: () => void; renderTrigger: (confirm: () => void) => ReactNode };
export const CatalogOfferPublication = ({ offer, onPublished, renderTrigger }: CatalogOfferPublicationProps) => {
  const action = useOfferPublication(offer, onPublished);
  return <>{renderTrigger(action.confirm)}
    {action.open ? <Dialog title="Publicar oferta" description="Confira o que o visitante vai ver na página do curso." busy={action.busy} onClose={action.close}>
      <OfferOption offer={offer} />
      <p>Ao publicar, esta opção aparece na página pública do curso.</p>
      {action.error ? <p role="alert" className="inline-alert">{action.error}</p> : null}
      <div className="dialog-actions">
        <button autoFocus type="button" className="outline-button" disabled={action.busy} onClick={action.close}>Cancelar</button>
        <button type="button" className="primary-button" disabled={action.busy} onClick={() => void action.publish()}>
          {action.busy ? 'Publicando…' : action.error ? 'Tentar de novo' : 'Publicar'}
        </button>
      </div>
    </Dialog> : null}
  </>;
};
