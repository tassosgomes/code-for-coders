import { Check, Clock, TimerOff, X, type LucideIcon } from 'lucide-react';

import { cn } from 'cn';

const states: Record<string, { label: string; icon: LucideIcon; className: string }> = {
  'awaiting-payment': { label: 'Aguardando pagamento', icon: Clock, className: 'bg-warning-soft text-warning-soft-foreground' },
  paid: { label: 'Pago', icon: Check, className: 'bg-success-soft text-success-soft-foreground' },
  expired: { label: 'Expirado', icon: TimerOff, className: 'bg-destructive text-destructive-foreground' },
  cancelled: { label: 'Cancelado', icon: X, className: 'bg-secondary text-secondary-foreground' },
};

/** Situação do pedido com ícone e texto, nunca só cor (G10). */
export const OrderStatusBadge = ({ status, className }: { status: string; className?: string }) => {
  const state = states[status] ?? states['awaiting-payment']!;
  const Icon = state.icon;
  return (
    <span className={cn('inline-flex w-fit shrink-0 items-center gap-1 rounded-full px-3 py-0.5 text-xs font-medium whitespace-nowrap', state.className, className)}>
      <Icon aria-hidden="true" className="size-3" />{state.label}
    </span>
  );
};
