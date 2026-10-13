import { Check, Clock, TimerOff, X, type LucideIcon } from 'lucide-react';

import { financeOrderStatuses } from '@/features/finance-area/utils/finance-order-format';

const icons: Record<string, LucideIcon> = { 'awaiting-payment': Clock, paid: Check, expired: TimerOff, cancelled: X };

/** OrderStatusBadge: situação com ícone e texto, nunca só cor (G10). */
type FinanceOrderStatusProps = { status: string };
export const FinanceOrderStatus = ({ status }: FinanceOrderStatusProps) => {
  const Icon = icons[status] ?? Clock;
  return <span className={`finance-status finance-status-${status}`}><Icon aria-hidden="true" size={12} />{financeOrderStatuses[status]?.label ?? status}</span>;
};
