import type { AccessPeriod } from '@/features/courtesies/types/courtesy-grant';
import { formatTermDate } from '@/features/courtesies/utils/format-term-date';

const monthsLabel = (months: number) => `${months} ${months === 1 ? 'mês' : 'meses'}`;
/** "6 meses · até 15/09/2027" ou "Acesso vitalício". */
export const formatAccessPeriod = (period: AccessPeriod, endsOn?: string | null) =>
  period.type === 'lifetime' ? 'Acesso vitalício' : `${monthsLabel(period.months)}${endsOn ? ` · até ${formatTermDate(endsOn)}` : ''}`;
/** Frase da cortesia concedida: "6 meses · até 15/09/2027" ou "Vitalícia · Acesso vitalício". */
export const formatGrantedPeriod = (period: AccessPeriod | undefined, endsOn: string | null) =>
  period?.type === 'months' ? formatAccessPeriod(period, endsOn) : `Vitalícia · ${endsOn ? `até ${formatTermDate(endsOn)}` : 'Acesso vitalício'}`;
