import { env } from '@/config/env';

export const purchasePrice = (cents: number) => `R$ ${new Intl.NumberFormat('pt-BR').format(Math.floor(cents / 100))},${String(cents % 100).padStart(2, '0')}`;
export const purchasePeriod = (period: { type: string; months?: number }) => period.type === 'lifetime' ? 'Acesso vitalício' : `Acesso por ${period.months} ${period.months === 1 ? 'mês, contado' : 'meses, contados'} a partir da liberação`;
export const purchaseDate = (date: string) => date.split('-').reverse().join('/');
export const formatPendingPaymentDeadline = (isoString: string) => {
  const date = new Date(isoString);
  const dateFormatter = new Intl.DateTimeFormat('pt-BR', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    timeZone: env.SCHOOL_TIME_ZONE,
  });
  const timeFormatter = new Intl.DateTimeFormat('pt-BR', {
    hour: '2-digit',
    minute: '2-digit',
    timeZone: env.SCHOOL_TIME_ZONE,
  });
  return `${dateFormatter.format(date)} às ${timeFormatter.format(date)}`;
};
