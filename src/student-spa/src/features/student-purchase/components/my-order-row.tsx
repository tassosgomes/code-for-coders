import { AlertCircle, CheckCircle, Clock, XCircle, type LucideIcon } from 'lucide-react';
import { Link } from 'react-router';

import { Button } from '@/components/ui/button';
import { paths } from '@/config/paths';
import type { StudentOrder } from '@/features/student-purchase/types/purchase';
import { formatPendingPaymentDeadline, purchasePeriod, purchasePrice } from '@/features/student-purchase/utils/purchase-labels';
import { env } from '@/config/env';

type MyOrderRowProps = { order: StudentOrder; highlighted?: boolean; lessonId?: string };
const statuses: Record<string, { label: string; icon: LucideIcon }> = {
  paid: { label: 'Pago', icon: CheckCircle },
  expired: { label: 'Expirado', icon: AlertCircle },
  cancelled: { label: 'Cancelado', icon: XCircle },
};
const paymentMethods: Record<string, string> = { card: 'Cartão de crédito', pix: 'PIX', boleto: 'Boleto' };
const orderDate = (iso: string) => new Intl.DateTimeFormat('pt-BR', { timeZone: env.SCHOOL_TIME_ZONE }).format(new Date(iso));

export const MyOrderRow = ({ order, highlighted = false, lessonId }: MyOrderRowProps) => {
  const status = statuses[order.status] ?? { label: 'Aguardando pagamento', icon: Clock };
  const StatusIcon = status.icon;
  const href = paths.studentOrder.getHref(order.orderId);
  return (
    <article aria-label={`Pedido #${order.number}`} className={`grid grid-cols-2 gap-4 rounded-xl border p-5 md:grid-cols-[6rem_2fr_1fr_1fr_1fr_1.5fr] ${highlighted ? 'border-primary bg-primary/5' : 'border-border bg-card'}`}>
      <Link className="col-span-2 font-medium underline underline-offset-4 md:col-span-1" to={href}>#{order.number}</Link>
      <div className="col-span-2 md:col-span-1"><h2 className="font-heading font-semibold">{order.course.title}</h2><p className="text-sm text-muted-foreground">{order.offer.name}</p><p className="text-xs text-muted-foreground">{purchasePeriod(order.accessPeriod)}</p></div>
      <p className="font-medium">{purchasePrice(order.priceCents)}</p>
      <time dateTime={order.createdAt}>{orderDate(order.createdAt)}</time>
      <p className="col-span-2 text-sm md:col-span-1">{order.paymentMethod ? paymentMethods[order.paymentMethod] ?? order.paymentMethod : <><span aria-hidden="true">— </span>Ainda não escolhido</>}</p>
      <div className="col-span-2 grid justify-items-start gap-3 md:col-span-1">
        <p className="flex items-center gap-2 text-sm"><StatusIcon aria-hidden="true" className="size-4" />{status.label}</p>
        {highlighted && (order.pendingPayment?.expiresAt ?? order.paymentPageExpiresAt) && <p className="text-sm">Pague até {formatPendingPaymentDeadline(order.pendingPayment?.expiresAt ?? order.paymentPageExpiresAt ?? '')}</p>}
        <Button asChild size="sm" variant={highlighted || order.status === 'paid' ? 'default' : 'outline'}>
          <Link to={order.status === 'paid' && lessonId ? paths.studentLesson.getHref(lessonId) : href}>
            {order.status === 'awaiting-payment' ? 'Retomar pagamento' : order.status === 'paid' ? lessonId ? 'Ir para o curso' : 'Liberando seu acesso' : 'Ver pedido'}
          </Link>
        </Button>
      </div>
    </article>
  );
};
