import type { CatalogOffer } from '@/features/catalog-courses/types/catalog-offer';
import { formatOfferPrice } from '@/features/catalog-courses/utils/offer-price';

type OfferOptionProps = { offer: Pick<CatalogOffer, 'name' | 'priceCents' | 'accessPeriod'> };
export const OfferOption = ({ offer }: OfferOptionProps) => <article className="offer-option">
  <h3>{offer.name}</h3>
  <p>{offer.accessPeriod.type === 'months'
    ? `Acesso por ${offer.accessPeriod.months} meses, contados a partir da liberação`
    : 'Acesso vitalício, sem data de término'}</p>
  <strong>{formatOfferPrice(offer.priceCents)}</strong>
</article>;
