import { financeOrderStatuses } from '@/features/finance-area/utils/finance-order-format';

type FinanceOrderStatusProps = { status: string };
export const FinanceOrderStatus = ({ status }: FinanceOrderStatusProps) => {
  const item = financeOrderStatuses[status];
  return <span className={`finance-status finance-status-${status}`}><span aria-hidden="true">{item?.icon}</span> {item?.label ?? status}</span>;
};
