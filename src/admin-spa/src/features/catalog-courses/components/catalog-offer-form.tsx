import { CircleAlert } from 'lucide-react';

import { Dialog } from '@/components/ui/dialog';
import { ValidatedForm } from '@/components/ui/form/validated-form';
import { offerInputSchema } from '@/features/catalog-courses/api/offer-input';
import { CatalogAlert } from '@/features/catalog-courses/components/catalog-alert';
import { CatalogOfferChangeConfirmation } from '@/features/catalog-courses/components/catalog-offer-change-confirmation';
import { offerTermsChanged, useOfferChange } from '@/features/catalog-courses/hooks/use-offer-change';
import type { CatalogOffer } from '@/features/catalog-courses/types/catalog-offer';
import { offerPriceInput } from '@/features/catalog-courses/utils/offer-price';

const FieldError = ({ id, message }: { id: string; message?: string }) => message
  ? <p id={id} role="alert" className="catalog-field-error"><CircleAlert aria-hidden="true" size={14} />{message}</p> : null;

type CatalogOfferFormProps = { courseId: string; offer?: CatalogOffer; onClose: () => void; onSaved: () => void };
export const CatalogOfferForm = ({ courseId, offer, onClose, onSaved }: CatalogOfferFormProps) => {
  const editing = useOfferChange(courseId, offer, onSaved);
  const published = offer?.status === 'published';
  return <Dialog className={`catalog-dialog ${editing.pending ? 'is-wide' : ''}`} title={editing.pending ? 'Confirmar alteração' : published ? 'Editar oferta publicada' : offer?.status === 'unpublished' ? 'Editar oferta despublicada' : offer ? 'Editar rascunho' : 'Nova oferta'}
    description={editing.pending ? offer?.name ?? '' : offer ? 'Altere nome, preço ou vigência desta oferta.' : 'A oferta nasce como rascunho e só aparece ao público depois de publicada.'}
    busy={editing.busy} onClose={editing.pending ? editing.back : onClose}>
    {editing.pending && offer && editing.nextOffer ? <CatalogOfferChangeConfirmation offer={offer} nextOffer={editing.nextOffer} busy={editing.busy} error={editing.error}
      onBack={editing.back} onConfirm={editing.confirm} /> : null}
    <div hidden={Boolean(editing.pending)}>
    <ValidatedForm schema={offerInputSchema} defaultValues={{ name: offer?.name ?? '', price: offer ? offerPriceInput(offer.priceCents) : '',
      periodType: offer?.accessPeriod.type ?? 'months', months: offer?.accessPeriod.type === 'months' ? String(offer.accessPeriod.months) : '12' }}
    onSubmit={editing.submit}>
      {(form) => {
        const errors = { name: form.formState.errors.name?.message ?? editing.fieldErrors.name,
          price: form.formState.errors.price?.message ?? editing.fieldErrors.price, months: form.formState.errors.months?.message ?? editing.fieldErrors.months };
        return <>
          {editing.error && !editing.pending ? <CatalogAlert {...editing.error} retryType="submit" /> : null}
          {published ? <CatalogAlert tone="info" title="Esta oferta está à venda." detail="Mudar preço ou vigência vale só para compras futuras." /> : null}
          <div className="catalog-field">
            <div className="catalog-field-label"><label htmlFor="offer-name">Nome da opção</label><small>{form.watch('name').length}/60</small></div>
            <input id="offer-name" autoFocus disabled={editing.busy} aria-invalid={Boolean(errors.name)} aria-describedby="offer-name-error" {...form.register('name')} />
            <FieldError id="offer-name-error" message={errors.name} />
          </div>
          <div className="catalog-field">
            <div className="catalog-field-label"><label htmlFor="offer-price">Preço</label></div>
            <div className="catalog-price-row"><span aria-hidden="true">R$</span>
              <input id="offer-price" inputMode="decimal" placeholder="497,00" disabled={editing.busy} aria-invalid={Boolean(errors.price)} aria-describedby="offer-price-error" {...form.register('price')} /></div>
            <FieldError id="offer-price-error" message={errors.price} />
          </div>
          <fieldset className="catalog-period" disabled={editing.busy}><legend>Vigência do acesso</legend>
            <div className="catalog-period-options">
              <label className="catalog-period-option"><input type="radio" value="months" {...form.register('periodType')} />Por período</label>
              <label className="catalog-period-option"><input type="radio" value="lifetime" {...form.register('periodType')} />Vitalícia</label>
            </div>
          </fieldset>
          {form.watch('periodType') === 'months' ? <div className="catalog-field catalog-months-field">
            <div className="catalog-field-label"><label htmlFor="offer-months">Meses</label></div>
            <input id="offer-months" inputMode="numeric" disabled={editing.busy} aria-invalid={Boolean(errors.months)} aria-describedby="offer-months-hint offer-months-error" {...form.register('months')} />
            {errors.months ? <FieldError id="offer-months-error" message={errors.months} /> : <p id="offer-months-hint" className="catalog-field-hint">De 1 a 60 meses inteiros.</p>}
          </div> : <p className="catalog-period-note">Acesso vitalício, sem data de término.</p>}
          <div className="dialog-actions"><button type="button" className="outline-button" disabled={editing.busy} onClick={onClose}>Cancelar</button>
            <button type="submit" className="primary-button" disabled={editing.busy}>{editing.busy ? 'Salvando…' : published && offerTermsChanged(offer, form.watch()) ? 'Revisar alteração' : offer && offer.status !== 'draft' ? 'Salvar' : 'Salvar rascunho'}</button></div>
        </>;
      }}
    </ValidatedForm>
    </div>
  </Dialog>;
};
