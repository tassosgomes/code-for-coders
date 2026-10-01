const wholeFormatter = new Intl.NumberFormat('pt-BR');

// Integer arithmetic only: prices travel as cents and never touch floating point.
export const formatPriceCents = (cents: number): string => {
  const reais = Math.floor(cents / 100);
  const remainder = String(cents % 100).padStart(2, '0');

  return `R$ ${wholeFormatter.format(reais)},${remainder}`;
};
