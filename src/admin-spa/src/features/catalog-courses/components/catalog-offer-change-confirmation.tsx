import type { CatalogOffer } from '@/features/catalog-courses/types/catalog-offer';
import { formatOfferPrice } from '@/features/catalog-courses/utils/offer-price';

type CatalogOfferChangeConfirmationProps = {
  offer: CatalogOffer; nextOffer: Pick<CatalogOffer, 'name' | 'priceCents' | 'accessPeriod'>;
  busy: boolean; error?: string; onBack: () => void; onConfirm: () => Promise<void>;
};
export const CatalogOfferChangeConfirmation = ({ offer, nextOffer, busy, error, onBack, onConfirm }: CatalogOfferChangeConfirmationProps) => <>
  <div className="offer-change-comparison">{[{ title: 'Antes', value: offer }, { title: 'Depois', value: nextOffer }].map(({ title, value }) =>
    <section key={title} aria-label={title} className="offer-option"><h3>{title}</h3>
      <strong>{formatOfferPrice(value.priceCents)}</strong><p>{value.accessPeriod.type === 'months' ? `${value.accessPeriod.months} meses` : 'Vitalícia'}</p>
    </section>)}</div>
  <p className="inline-alert">Vale para compras futuras e não altera quem já comprou.</p>
  <p>A mudança fica registrada na trilha com o seu nome.</p>
  {error ? <p role="alert" className="inline-alert">{error}</p> : null}
  <div className="dialog-actions"><button autoFocus type="button" className="outline-button" disabled={busy} onClick={onBack}>Voltar e editar</button>
    <button type="button" className="primary-button" disabled={busy} onClick={() => void onConfirm()}>{busy ? 'Salvando…' : error ? 'Tentar de novo' : 'Confirmar'}</button></div>
</>;
