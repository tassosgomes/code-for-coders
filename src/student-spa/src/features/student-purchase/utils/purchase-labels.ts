export const purchasePrice = (cents: number) => `R$ ${new Intl.NumberFormat('pt-BR').format(Math.floor(cents / 100))},${String(cents % 100).padStart(2, '0')}`;
export const purchasePeriod = (period: { type: string; months?: number }) => period.type === 'lifetime' ? 'Acesso vitalício' : `Acesso por ${period.months} ${period.months === 1 ? 'mês, contado' : 'meses, contados'} a partir da liberação`;
export const purchaseDate = (date: string) => date.split('-').reverse().join('/');
