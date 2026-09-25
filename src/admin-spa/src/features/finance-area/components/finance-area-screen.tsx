type FinanceAreaScreenProps = {
  state: 'forbidden' | 'loading' | 'unavailable' | 'ready';
};

export const FinanceAreaScreen = ({ state }: FinanceAreaScreenProps) => {
  const message = {
    forbidden: 'Você não tem permissão para esta área.',
    loading: 'Carregando a área financeira…',
    unavailable: 'Não foi possível carregar a área financeira.',
    ready: 'Área financeira reservada.',
  }[state];

  return (
    <main className="page-shell">
      <p className="eyebrow">Backoffice</p>
      <h1>Financeiro</h1>
      <p role={state === 'forbidden' || state === 'unavailable' ? 'alert' : 'status'}>{message}</p>
    </main>
  );
};
