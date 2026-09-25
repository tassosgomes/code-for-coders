import { useOutletContext } from 'react-router';

import { useFinanceArea } from '@/features/finance-area/api/get-finance-area';
import { FinanceAreaScreen } from '@/features/finance-area/components/finance-area-screen';

type StaffSessionContext = {
  permissions: readonly string[];
};

export const FinanceAreaRoute = () => {
  const session = useOutletContext<StaffSessionContext>();
  if (!session.permissions.includes('financeiro.ler')) {
    return <FinanceAreaScreen state="forbidden" />;
  }

  return <FinanceAreaContent />;
};

const FinanceAreaContent = () => {
  const { isError, isPending } = useFinanceArea();
  const state = isPending ? 'loading' : isError ? 'unavailable' : 'ready';
  return <FinanceAreaScreen state={state} />;
};
