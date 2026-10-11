import { useEffect, useState } from 'react';

import { Toast } from '@/components/toast';
import { CatalogOfferForm } from '@/features/catalog-courses/components/catalog-offer-form';
import { CatalogOfferActions } from '@/features/catalog-courses/components/catalog-offer-actions';
import { OfferStatusBadge } from '@/features/catalog-courses/components/catalog-badges';
import type { CatalogCourseRecord } from '@/features/catalog-courses/api/get-catalog-course';
import type { CatalogOffer } from '@/features/catalog-courses/types/catalog-offer';
import { formatOfferPrice } from '@/features/catalog-courses/utils/offer-price';

type CatalogOffersProps = { course: CatalogCourseRecord };
const order = { draft: 0, published: 1, unpublished: 2 };
const noticeDurationMs = 5000;
const offerDetails = (offer: CatalogOffer) => `${offer.accessPeriod.type === 'months' ? `${offer.accessPeriod.months} meses` : 'Vitalícia'} · ${offer.purchaseIntentCount
  ? `${offer.purchaseIntentCount} ${offer.purchaseIntentCount === 1 ? 'clique' : 'cliques'} em Comprar` : 'Nenhum clique em Comprar ainda'}`;

export const CatalogOffers = ({ course }: CatalogOffersProps) => {
  const [editing, setEditing] = useState<{ offer?: CatalogOffer }>(); const [notice, setNotice] = useState('');
  useEffect(() => {
    if (!notice) return;
    const timer = window.setTimeout(() => setNotice(''), noticeDurationMs);
    return () => window.clearTimeout(timer);
  }, [notice]);
  const offers = [...course.offers].sort((a, b) => order[a.status] - order[b.status] || a.createdAt.localeCompare(b.createdAt) || a.offerId.localeCompare(b.offerId));
  const publishedCount = offers.filter((offer) => offer.status === 'published').length;
  return <section aria-labelledby="catalog-offers-title" className="catalog-offers">
    <div className="catalog-offers-header"><h2 id="catalog-offers-title">Ofertas</h2>
      <button type="button" className="primary-button" onClick={() => setEditing({})}>+ Nova oferta</button></div>
    {offers.length === 0 ? <p className="catalog-offers-empty">Nenhuma oferta ainda. Crie a primeira para colocar o curso à venda.</p>
      : <ul className="catalog-offer-list">{offers.map((offer) => <li className="catalog-offer-row" key={offer.offerId}>
        <div className="catalog-offer-info"><h3 id={`offer-${offer.offerId}`}>{offer.name}</h3><p>{offerDetails(offer)}</p></div>
        <p className="catalog-offer-price">{formatOfferPrice(offer.priceCents)}</p>
        <div className="catalog-offer-status"><OfferStatusBadge status={offer.status} /></div>
        <CatalogOfferActions offer={offer} lastPublished={offer.status === 'published' && publishedCount === 1} onEdit={() => setEditing({ offer })}
          onDeleted={() => setNotice('Rascunho excluído.')} onPublished={() => setNotice('Oferta publicada.')} onUnpublished={() => setNotice('Oferta despublicada.')} />
      </li>)}</ul>}
    {notice ? <Toast message={notice} /> : null}
    {editing ? <CatalogOfferForm courseId={course.courseId} offer={editing.offer} onClose={() => setEditing(undefined)} onSaved={() => { setEditing(undefined); setNotice(editing.offer && editing.offer.status !== 'draft' ? 'Oferta salva.' : 'Rascunho salvo.'); }} /> : null}
  </section>;
};
