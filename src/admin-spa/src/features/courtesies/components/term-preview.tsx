import { formatTermDate } from '@/features/courtesies/utils/format-term-date';

type TermPreviewProps = { lifetime: boolean; endsOn?: string; busy: boolean; error: boolean; onRetry: () => void };
export const TermPreview = ({ lifetime, endsOn, busy, error, onRetry }: TermPreviewProps) =>
  <div className="courtesy-term-preview" role="status">{lifetime ? 'Acesso vitalício' : busy ? 'Calculando término…' : error ?
    <>Não foi possível calcular o término. <button type="button" onClick={onRetry}>Tentar novamente</button></> : endsOn ? `Acesso até ${formatTermDate(endsOn)}` : 'Informe de 1 a 60 meses inteiros.'}</div>;
