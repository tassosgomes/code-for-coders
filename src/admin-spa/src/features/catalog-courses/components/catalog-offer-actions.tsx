import { useState } from 'react';

import { CatalogOfferDelete } from '@/features/catalog-courses/components/catalog-offer-delete';
import { CatalogOfferPublication } from '@/features/catalog-courses/components/catalog-offer-publication';
import { CatalogOfferUnpublication } from '@/features/catalog-courses/components/catalog-offer-unpublication';
import type { CatalogOffer } from '@/features/catalog-courses/types/catalog-offer';

type CatalogOfferActionsProps = { offer: CatalogOffer; lastPublished: boolean; onEdit: () => void; onDeleted: () => void; onPublished: () => void; onUnpublished: () => void };
export const CatalogOfferActions = ({ offer, lastPublished, onEdit, onDeleted, onPublished, onUnpublished }: CatalogOfferActionsProps) => {
  const [open, setOpen] = useState(false);
  return <CatalogOfferPublication offer={offer} onPublished={onPublished} renderTrigger={(confirmPublish) =>
    <CatalogOfferUnpublication offer={offer} lastPublished={lastPublished} onUnpublished={onUnpublished} renderTrigger={(confirmUnpublish) =>
    <CatalogOfferDelete offer={offer} onDeleted={onDeleted} renderTrigger={(confirmDelete) => <div className="course-item-actions">
      <button type="button" className="course-action-trigger" aria-label={`Ações de ${offer.name}`} aria-expanded={open} onClick={() => setOpen(!open)}>⋯</button>
      {open ? <div className="course-action-menu">
        <button type="button" onClick={() => { setOpen(false); onEdit(); }}>Editar</button>
        {offer.status !== 'published' ? <button type="button" onClick={() => { setOpen(false); confirmPublish(); }}>{offer.status === 'unpublished' ? 'Publicar de novo' : 'Publicar'}</button> : null}
        {offer.status === 'published' ? <button type="button" onClick={() => { setOpen(false); confirmUnpublish(); }}>Despublicar</button> : null}
        {offer.status === 'draft' ? <button type="button" onClick={() => { setOpen(false); confirmDelete(); }}>Excluir</button> : null}
      </div> : null}
    </div>} />
  } />} />;
};
