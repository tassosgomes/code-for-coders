import { useState } from 'react';

import { CatalogOfferDelete } from '@/features/catalog-courses/components/catalog-offer-delete';
import type { CatalogOffer } from '@/features/catalog-courses/types/catalog-offer';

type CatalogOfferActionsProps = { offer: CatalogOffer; onEdit: () => void; onDeleted: () => void };
export const CatalogOfferActions = ({ offer, onEdit, onDeleted }: CatalogOfferActionsProps) => {
  const [open, setOpen] = useState(false);
  if (offer.status !== 'draft') return null;
  return <CatalogOfferDelete offer={offer} onDeleted={onDeleted} renderTrigger={(confirmDelete) => <div className="course-item-actions">
    <button type="button" className="course-action-trigger" aria-label={`Ações de ${offer.name}`} aria-expanded={open} onClick={() => setOpen(!open)}>⋯</button>
    {open ? <div className="course-action-menu"><button type="button" onClick={() => { setOpen(false); onEdit(); }}>Editar</button>
      <button type="button" onClick={() => { setOpen(false); confirmDelete(); }}>Excluir</button>
    </div> : null}
  </div>} />;
};
