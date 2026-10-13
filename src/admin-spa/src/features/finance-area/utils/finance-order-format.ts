// G17: datas e momentos no fuso da escola (o mesmo padrão do SPA do aluno).
const schoolTimeZone = 'America/Sao_Paulo';

export const financeOrderStatuses: Record<string, { label: string }> = {
  'awaiting-payment': { label: 'Aguardando pagamento' }, paid: { label: 'Pago' },
  expired: { label: 'Expirado' }, cancelled: { label: 'Cancelado' },
};
export const financePaymentMethod = (method: string | null) => method === 'card' ? 'Cartão de crédito' : method === 'pix' ? 'PIX' : method === 'boleto' ? 'Boleto' : '—';
export const financeMoney = (cents: number) => new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(cents / 100);
export const financeDate = (value: string) => new Intl.DateTimeFormat('pt-BR', { day: '2-digit', month: '2-digit', year: 'numeric', timeZone: schoolTimeZone }).format(new Date(value));
const financeTime = (value: string) => new Intl.DateTimeFormat('pt-BR', { hour: '2-digit', minute: '2-digit', timeZone: schoolTimeZone }).format(new Date(value));
/** Momento "DD/MM/AAAA às HH:MM". */
export const financeMoment = (value: string) => `${financeDate(value)} às ${financeTime(value)}`;
export const financeAccessPeriod = (period: { type: string; months?: number }) => period.type === 'lifetime'
  ? 'Acesso vitalício'
  : `Acesso por ${period.months} ${period.months === 1 ? 'mês, contado' : 'meses, contados'} a partir da liberação do acesso`;
