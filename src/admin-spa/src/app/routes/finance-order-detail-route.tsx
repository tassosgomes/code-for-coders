import { useOutletContext, useParams } from 'react-router';

import { FinanceAreaScreen } from '@/features/finance-area/components/finance-area-screen';
import { FinanceOrderDetailScreen } from '@/features/finance-area/components/finance-order-detail-screen';

export const FinanceOrderDetailRoute = () => {
  const session = useOutletContext<{ permissions: readonly string[] }>();
  const { orderId = '' } = useParams();
  if (!session.permissions.includes('financeiro.ler')) return <FinanceAreaScreen />;
  return <FinanceOrderDetailScreen orderId={orderId} />;
};
