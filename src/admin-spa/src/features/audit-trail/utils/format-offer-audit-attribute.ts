const reais = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' });

export const formatOfferAuditAttribute = (key: string, value: string) => {
  if ((key === 'precoAnterior' || key === 'precoNovo') && /^\d+$/.test(value)) {
    const cents = Number(value);
    if (Number.isSafeInteger(cents)) return reais.format(cents / 100);
  }
  if (key === 'vigenciaAnterior' || key === 'vigenciaNova') {
    if (value === 'vitalicia') return 'Vitalícia';
    const months = /^(\d+)m$/.exec(value)?.[1];
    if (months) return `${Number(months)} ${Number(months) === 1 ? 'mês' : 'meses'}`;
  }
  return value;
};

const attributeOrder = ['curso', 'precoAnterior', 'precoNovo', 'vigenciaAnterior', 'vigenciaNova'];

export const getOfferAuditAttributeEntries = (attributes: Record<string, string>) =>
  Object.entries(attributes).sort(([left], [right]) => {
    const leftOrder = attributeOrder.indexOf(left);
    const rightOrder = attributeOrder.indexOf(right);
    return (leftOrder < 0 ? attributeOrder.length : leftOrder) - (rightOrder < 0 ? attributeOrder.length : rightOrder);
  });
