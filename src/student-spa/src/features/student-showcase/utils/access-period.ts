// The platform always states the starting point of the period: it counts from the release, not from the purchase.
export const formatAccessPeriod = ({ type, months }: { type: string; months?: number }): string => {
  if (type === 'lifetime') {
    return 'Acesso vitalício';
  }

  if (type === 'months' && months !== undefined) {
    return months === 1
      ? 'Acesso por 1 mês, contado a partir da liberação'
      : `Acesso por ${months} meses, contados a partir da liberação`;
  }

  // A period kind the contract adds later is not guessed at.
  return '';
};
