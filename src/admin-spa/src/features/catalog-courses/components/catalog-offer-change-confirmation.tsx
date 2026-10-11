import { CatalogAlert } from '@/features/catalog-courses/components/catalog-alert';
import type { CatalogOffer, CatalogProblem } from '@/features/catalog-courses/types/catalog-offer';
import { formatOfferPrice } from '@/features/catalog-courses/utils/offer-price';

type CatalogOfferChangeConfirmationProps = {
  offer: CatalogOffer; nextOffer: Pick<CatalogOffer, 'name' | 'priceCents' | 'accessPeriod'>;
  busy: boolean; error?: CatalogProblem; onBack: () => void; onConfirm: () => Promise<void>;
};
export const CatalogOfferChangeConfirmation = ({ offer, nextOffer, busy, error, onBack, onConfirm }: CatalogOfferChangeConfirmationProps) => <>
  <div className="offer-change-comparison">{[{ title: 'Antes', value: offer }, { title: 'Depois', value: nextOffer }].map(({ title, value }) =>
    <section key={title} aria-label={title} className={`offer-change-side ${title === 'Depois' ? 'is-after' : ''}`}><h3>{title}</h3>
      <strong>{formatOfferPrice(value.priceCents)}</strong><p>{value.accessPeriod.type === 'months' ? `${value.accessPeriod.months} meses` : 'Vitalícia'}</p>
    </section>)}</div>
  {error ? <CatalogAlert {...error} onRetry={() => void onConfirm()} /> : <CatalogAlert tone="info" title="Vale para compras futuras e não altera quem já comprou." detail="A mudança fica registrada na trilha com o seu nome." />}
  <div className="dialog-actions"><button autoFocus type="button" className="outline-button" disabled={busy} onClick={onBack}>Voltar e editar</button>
    <button type="button" className="primary-button" disabled={busy} onClick={() => void onConfirm()}>{busy ? 'Salvando…' : 'Confirmar'}</button></div>
</>;
