import { Info, TriangleAlert } from 'lucide-react';

import { formatTermDate } from '@/features/courtesies/utils/format-term-date';

type TermPreviewProps = { lifetime: boolean; invalid?: boolean; endsOn?: string; busy: boolean; error: boolean; onRetry: () => void };
export const TermPreview = ({ lifetime, invalid = false, endsOn, busy, error, onRetry }: TermPreviewProps) => {
  if (invalid && !lifetime) return null;
  if (!lifetime && error) return <div className="courtesy-callout courtesy-callout-warning" role="alert">
    <TriangleAlert size={16} aria-hidden="true" />
    <p>Não foi possível mostrar a prévia agora. Tente de novo antes de continuar.</p>
    <button type="button" className="text-button" onClick={onRetry}>Tentar de novo</button>
  </div>;
  return <div className="courtesy-term-preview" role="status">
    <Info size={16} aria-hidden="true" />
    <div>{lifetime ? <p>Acesso vitalício, sem data de término.</p> : busy || !endsOn ? <p>Calculando término…</p> : <>
      <p>{`Termina em até ${formatTermDate(endsOn)} — vindo do servidor.`}</p>
      <p className="courtesy-hint">A prévia vale se a confirmação for hoje.</p></>}</div>
  </div>;
};
