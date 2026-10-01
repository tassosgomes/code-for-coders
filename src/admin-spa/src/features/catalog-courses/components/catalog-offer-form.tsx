import { Dialog } from '@/components/ui/dialog';
import { ValidatedForm } from '@/components/ui/form/validated-form';
import { offerInputSchema } from '@/features/catalog-courses/api/offer-input';
import { useOfferWrite } from '@/features/catalog-courses/hooks/use-offer-write';
import type { CatalogOffer } from '@/features/catalog-courses/types/catalog-offer';
import { offerPriceInput } from '@/features/catalog-courses/utils/offer-price';

type CatalogOfferFormProps = { courseId: string; offer?: CatalogOffer; onClose: () => void; onSaved: () => void };
export const CatalogOfferForm = ({ courseId, offer, onClose, onSaved }: CatalogOfferFormProps) => {
  const editing = useOfferWrite(courseId, offer?.offerId);
  return <Dialog className="catalog-offer-form" title={offer ? 'Editar rascunho' : 'Nova oferta'} description="A oferta nasce como rascunho e só aparece ao público depois de publicada." busy={editing.busy} onClose={onClose}>
    <ValidatedForm schema={offerInputSchema} defaultValues={{ name: offer?.name ?? '', price: offer ? offerPriceInput(offer.priceCents) : '',
      periodType: offer?.accessPeriod.type ?? 'months', months: offer?.accessPeriod.type === 'months' ? String(offer.accessPeriod.months) : '12' }}
    onSubmit={async (input) => { const saved = await editing.save(input); if (saved) onSaved(); return saved; }}>
      {(form) => {
        const errors = { name: form.formState.errors.name?.message ?? editing.fieldErrors.name,
          price: form.formState.errors.price?.message ?? editing.fieldErrors.price, months: form.formState.errors.months?.message ?? editing.fieldErrors.months };
        return <>
          <label htmlFor="offer-name">Nome da opção</label><input id="offer-name" autoFocus disabled={editing.busy} aria-invalid={Boolean(errors.name)} aria-describedby="offer-name-error" {...form.register('name')} />
          <small>{form.watch('name').length}/60</small>{errors.name ? <p id="offer-name-error" role="alert" className="field-error">{errors.name}</p> : null}
          <label htmlFor="offer-price">Preço (R$)</label><input id="offer-price" inputMode="decimal" placeholder="497,00" disabled={editing.busy} aria-invalid={Boolean(errors.price)} aria-describedby="offer-price-error" {...form.register('price')} />
          {errors.price ? <p id="offer-price-error" role="alert" className="field-error">{errors.price}</p> : null}
          <fieldset disabled={editing.busy}><legend>Vigência do acesso</legend>
            <label><input type="radio" value="months" {...form.register('periodType')} /> Por período</label>
            <label><input type="radio" value="lifetime" {...form.register('periodType')} /> Vitalícia</label>
          </fieldset>
          {form.watch('periodType') === 'months' ? <><label htmlFor="offer-months">Meses</label><input id="offer-months" inputMode="numeric" disabled={editing.busy} aria-invalid={Boolean(errors.months)} aria-describedby="offer-months-error" {...form.register('months')} /></>
            : <p>Acesso vitalício, sem data de término.</p>}
          {errors.months ? <p id="offer-months-error" role="alert" className="field-error">{errors.months}</p> : null}
          {editing.error ? <p role="alert" className="inline-alert">{editing.error}</p> : null}
          <div className="dialog-actions"><button type="button" className="outline-button" disabled={editing.busy} onClick={onClose}>Cancelar</button>
            <button type="submit" className="primary-button" disabled={editing.busy}>{editing.busy ? 'Salvando…' : 'Salvar rascunho'}</button></div>
        </>;
      }}
    </ValidatedForm>
  </Dialog>;
};
