import { useState } from 'react';

import { CatalogOfferDelete } from '@/features/catalog-courses/components/catalog-offer-delete';
import { CatalogOfferPublication } from '@/features/catalog-courses/components/catalog-offer-publication';
import type { CatalogOffer } from '@/features/catalog-courses/types/catalog-offer';

type CatalogOfferActionsProps = { offer: CatalogOffer; onEdit: () => void; onDeleted: () => void; onPublished: () => void };
export const CatalogOfferActions = ({ offer, onEdit, onDeleted, onPublished }: CatalogOfferActionsProps) => {
  const [open, setOpen] = useState(false);
  if (offer.status === 'published') return null;
  return <CatalogOfferPublication offer={offer} onPublished={onPublished} renderTrigger={(confirmPublish) =>
    <CatalogOfferDelete offer={offer} onDeleted={onDeleted} renderTrigger={(confirmDelete) => <div className="course-item-actions">
      <button type="button" className="course-action-trigger" aria-label={`Ações de ${offer.name}`} aria-expanded={open} onClick={() => setOpen(!open)}>⋯</button>
      {open ? <div className="course-action-menu">
        {offer.status === 'draft' ? <button type="button" onClick={() => { setOpen(false); onEdit(); }}>Editar</button> : null}
        <button type="button" onClick={() => { setOpen(false); confirmPublish(); }}>{offer.status === 'unpublished' ? 'Publicar de novo' : 'Publicar'}</button>
        {offer.status === 'draft' ? <button type="button" onClick={() => { setOpen(false); confirmDelete(); }}>Excluir</button> : null}
      </div> : null}
    </div>} />
  } />;
};
