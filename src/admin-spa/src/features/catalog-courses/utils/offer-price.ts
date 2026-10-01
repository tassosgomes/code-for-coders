export const parseOfferPrice = (value: string): number | undefined => {
  if (!/^\d{1,5}(?:[,.]\d{1,2})?$/.test(value)) return undefined;
  const [whole = '', fraction = ''] = value.split(/[,.]/);
  const cents = BigInt(whole) * 100n + BigInt(fraction.padEnd(2, '0'));
  return cents >= 1n && cents <= 9999999n ? Number(cents) : undefined;
};
export const offerPriceInput = (cents: number) => `${Math.trunc(cents / 100)},${String(cents % 100).padStart(2, '0')}`;
export const formatOfferPrice = (cents: number) => `R$ ${new Intl.NumberFormat('pt-BR').format(BigInt(cents) / 100n)},${String(cents % 100).padStart(2, '0')}`;
