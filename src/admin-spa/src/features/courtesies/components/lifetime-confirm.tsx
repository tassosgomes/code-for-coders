import { Check } from 'lucide-react';
import { useEffect, useRef } from 'react';

type LifetimeConfirmProps = { confirmed: boolean; disabled: boolean; onChange: (confirmed: boolean) => void };
export const LifetimeConfirm = ({ confirmed, disabled, onChange }: LifetimeConfirmProps) => {
  const checkbox = useRef<HTMLInputElement>(null);
  useEffect(() => { checkbox.current?.focus(); }, []);
  return <label className="courtesy-lifetime-confirm">
    <span className="courtesy-lifetime-checkbox"><input ref={checkbox} type="checkbox" checked={confirmed} disabled={disabled} onChange={(event) => onChange(event.target.checked)} /><Check size={14} aria-hidden="true" /></span>
    <span>Entendo que esta cortesia vitalícia não há como desfazer pela tela.</span>
  </label>;
};
