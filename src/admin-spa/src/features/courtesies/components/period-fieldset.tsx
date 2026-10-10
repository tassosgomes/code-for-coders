import type { UseFormReturn } from 'react-hook-form';

import type { CourtesyGrantInput } from '@/features/courtesies/api/grant-courtesy';

type PeriodFieldsetProps = { form: UseFormReturn<CourtesyGrantInput>; lifetime: boolean; invalid: boolean; onMonths: () => void; onLifetime: () => void };
export const PeriodFieldset = ({ form, lifetime, invalid, onMonths, onLifetime }: PeriodFieldsetProps) => {
  const error = form.formState.errors.accessPeriod;
  return <>
    <fieldset className="courtesy-period-fieldset"><legend>Vigência do acesso</legend>
      <div className="courtesy-period-options">
        <label className={`courtesy-period-card ${lifetime ? '' : 'is-selected'}`}>
          <input type="radio" name="period" checked={!lifetime} aria-labelledby="period-months-title" aria-describedby="period-months-hint" onChange={onMonths} />
          <span><span id="period-months-title" className="courtesy-period-title">Por período</span><span id="period-months-hint" className="courtesy-hint">De 1 a 60 meses</span></span>
        </label>
        <label className={`courtesy-period-card ${lifetime ? 'is-selected' : ''}`}>
          <input type="radio" name="period" checked={lifetime} aria-labelledby="period-lifetime-title" aria-describedby="period-lifetime-hint" onChange={onLifetime} />
          <span><span id="period-lifetime-title" className="courtesy-period-title">Vitalícia</span><span id="period-lifetime-hint" className="courtesy-hint">Sem data de término</span></span>
        </label>
      </div>
    </fieldset>
    {lifetime ? null : <div className="courtesy-months-field">
      <label htmlFor="courtesy-months">Meses</label>
      <div className="courtesy-months-row">
        <input id="courtesy-months" type="number" min="1" max="60" aria-invalid={invalid || Boolean(error)} {...form.register('accessPeriod.months', { valueAsNumber: true })} />
        <span className="courtesy-hint">de 1 a 60 meses inteiros</span>
      </div>
      {error ? <p role="alert" className="field-error">Informe de 1 a 60 meses inteiros.</p> : null}
    </div>}
  </>;
};
