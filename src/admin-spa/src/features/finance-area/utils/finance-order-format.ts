export const financeOrderStatuses: Record<string, { label: string; icon: string }> = {
  'awaiting-payment': { label: 'Aguardando pagamento', icon: '◷' }, paid: { label: 'Pago', icon: '✓' },
  expired: { label: 'Expirado', icon: '◴' }, cancelled: { label: 'Cancelado', icon: '×' },
};
export const financePaymentMethod = (method: string | null) => method === 'card' ? 'Cartão de crédito' : method === 'pix' ? 'PIX' : method === 'boleto' ? 'Boleto' : '—';
export const financeMoney = (cents: number) => new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(cents / 100);
export const financeDate = (value: string) => new Intl.DateTimeFormat('pt-BR').format(new Date(value));
export const financeMoment = (value: string) => new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(value));
