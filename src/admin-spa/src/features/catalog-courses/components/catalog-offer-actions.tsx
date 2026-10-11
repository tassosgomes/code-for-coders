import { MoreHorizontal } from 'lucide-react';
import { useState } from 'react';

import { CatalogOfferDelete } from '@/features/catalog-courses/components/catalog-offer-delete';
import { CatalogOfferPublication } from '@/features/catalog-courses/components/catalog-offer-publication';
import { CatalogOfferUnpublication } from '@/features/catalog-courses/components/catalog-offer-unpublication';
import type { CatalogOffer } from '@/features/catalog-courses/types/catalog-offer';

type CatalogOfferActionsProps = { offer: CatalogOffer; lastPublished: boolean; onEdit: () => void; onDeleted: () => void; onPublished: () => void; onUnpublished: () => void };
export const CatalogOfferActions = ({ offer, lastPublished, onEdit, onDeleted, onPublished, onUnpublished }: CatalogOfferActionsProps) => {
  const [open, setOpen] = useState(false);
  const nameId = `offer-${offer.offerId}`;
  return <CatalogOfferPublication offer={offer} onPublished={onPublished} renderTrigger={(confirmPublish) =>
    <CatalogOfferUnpublication offer={offer} lastPublished={lastPublished} onUnpublished={onUnpublished} renderTrigger={(confirmUnpublish) =>
    <CatalogOfferDelete offer={offer} onDeleted={onDeleted} renderTrigger={(confirmDelete) => <>
      <div className="catalog-offer-action">
        {offer.status === 'published'
          ? <button type="button" className="outline-button" aria-describedby={nameId} onClick={confirmUnpublish}>Despublicar</button>
          : <button type="button" className={offer.status === 'draft' ? 'primary-button' : 'outline-button'} aria-describedby={nameId} onClick={confirmPublish}>
            {offer.status === 'draft' ? 'Publicar' : 'Publicar de novo'}</button>}
      </div>
      <div className="course-item-actions">
        <button type="button" className="course-action-trigger" aria-label={`Ações de ${offer.name}`} aria-expanded={open} onClick={() => setOpen(!open)}><MoreHorizontal aria-hidden="true" size={16} /></button>
        {open ? <div className="course-action-menu">
          <button type="button" onClick={() => { setOpen(false); onEdit(); }}>Editar</button>
          {offer.status === 'draft' ? <button type="button" onClick={() => { setOpen(false); confirmDelete(); }}>Excluir</button> : null}
        </div> : null}
      </div>
    </>} />
  } />} />;
};
