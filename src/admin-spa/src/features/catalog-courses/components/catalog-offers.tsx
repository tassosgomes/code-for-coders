import { useState } from 'react';

import { CatalogOfferForm } from '@/features/catalog-courses/components/catalog-offer-form';
import { CatalogOfferActions } from '@/features/catalog-courses/components/catalog-offer-actions';
import type { CatalogCourseRecord } from '@/features/catalog-courses/api/get-catalog-course';
import type { CatalogOffer } from '@/features/catalog-courses/types/catalog-offer';
import { formatOfferPrice } from '@/features/catalog-courses/utils/offer-price';

type CatalogOffersProps = { course: CatalogCourseRecord };
const statusLabels = { draft: 'Rascunho', published: '✓ Publicada', unpublished: '– Despublicada' };
const order = { draft: 0, published: 1, unpublished: 2 };
export const CatalogOffers = ({ course }: CatalogOffersProps) => {
  const [editing, setEditing] = useState<{ offer?: CatalogOffer }>(); const [notice, setNotice] = useState('');
  const offers = [...course.offers].sort((a, b) => order[a.status] - order[b.status] || a.createdAt.localeCompare(b.createdAt) || a.offerId.localeCompare(b.offerId));
  return <section className="catalog-record-card"><div className="catalog-offers-heading"><h2>Ofertas</h2>
    <button type="button" className="primary-button" onClick={() => setEditing({})}>+ Nova oferta</button></div>
    {offers.length === 0 ? <p>Nenhuma oferta ainda. Crie a primeira para colocar o curso à venda.</p> : <ul className="catalog-offers-list">{offers.map((offer) => <li key={offer.offerId}>
      <div><h3>{offer.name}</h3><p>{formatOfferPrice(offer.priceCents)} · <span>{statusLabels[offer.status]}</span></p>
        <p>{offer.accessPeriod.type === 'months' ? `${offer.accessPeriod.months} meses` : 'Vitalícia'} · {offer.purchaseIntentCount ? `${offer.purchaseIntentCount} cliques em Comprar` : 'Nenhum clique em Comprar ainda'}</p></div>
      <CatalogOfferActions offer={offer} onEdit={() => setEditing({ offer })} onDeleted={() => setNotice('Rascunho excluído.')} onPublished={() => setNotice('Oferta publicada.')} />
    </li>)}</ul>}
    <p>{offers.reduce((total, offer) => total + offer.purchaseIntentCount, 0)} cliques em Comprar</p>
    {notice ? <p role="status">{notice}</p> : null}
    {editing ? <CatalogOfferForm courseId={course.courseId} offer={editing.offer} onClose={() => setEditing(undefined)} onSaved={() => { setEditing(undefined); setNotice(editing.offer && editing.offer.status !== 'draft' ? 'Oferta salva.' : 'Rascunho salvo.'); }} /> : null}
  </section>;
};
