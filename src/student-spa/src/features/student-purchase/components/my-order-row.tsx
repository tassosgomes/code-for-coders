import { Link } from 'react-router';

import { Button } from '@/components/ui/button';
import { paths } from '@/config/paths';
import { OrderStatusBadge } from '@/features/student-purchase/components/order-status-badge';
import type { StudentOrder } from '@/features/student-purchase/types/purchase';
import { formatPendingPaymentDeadline, purchaseDay, purchasePrice } from '@/features/student-purchase/utils/purchase-labels';

const paymentMethods: Record<string, string> = { card: 'Cartão de crédito', pix: 'PIX', boleto: 'Boleto' };
const paymentMethod = (order: StudentOrder) => order.paymentMethod ? paymentMethods[order.paymentMethod] ?? order.paymentMethod : 'Ainda não escolhido';
// Separador " · " das linhas que juntam dois dados no mobile.
const joined = "before:mx-1 before:content-['·']";

/** Pedido pendente em destaque (M1): borda de acento, prazo e Retomar pagamento; no mobile (M5) os dados empilham. */
export const PendingOrderCard = ({ order }: { order: StudentOrder }) => {
  const href = paths.studentOrder.getHref(order.orderId);
  const deadline = order.pendingPayment?.expiresAt ?? order.paymentPageExpiresAt;
  return (
    <article aria-label={`Pedido #${order.number}`}
      className="flex flex-col items-start gap-2 rounded-xl border-2 border-primary bg-card p-4 text-sm leading-5 md:grid md:grid-cols-[1fr_auto] md:items-center md:gap-x-4 md:gap-y-1 md:p-5">
      <p className="font-semibold text-foreground md:col-start-1 md:row-start-1">
        <Link className="hover:underline" to={href}>#{order.number}</Link>
        <span className={`md:hidden ${joined}`}>{purchaseDay(order.createdAt)}</span>
        <span className={`hidden md:inline ${joined}`}>{order.course.title}</span>
        <span className={`hidden md:inline ${joined}`}>{order.offer.name}</span>
      </p>
      <p className="font-medium text-foreground md:hidden">{order.course.title}</p>
      <p className="text-muted-foreground md:hidden">{order.offer.name}</p>
      <p className="text-muted-foreground md:col-start-1 md:row-start-2">
        {purchasePrice(order.priceCents)}
        <span className={`hidden md:inline ${joined}`}>{purchaseDay(order.createdAt)}</span>
        <span className={joined}>{paymentMethod(order)}</span>
      </p>
      <OrderStatusBadge status={order.status} className="md:col-start-2 md:row-start-1 md:justify-self-end" />
      {deadline ? <p className="text-muted-foreground md:col-start-1 md:row-start-3">Pague até {formatPendingPaymentDeadline(deadline)}</p> : null}
      <Button asChild className="w-full md:col-start-2 md:row-span-2 md:row-start-2 md:mt-2 md:w-auto md:self-start"><Link to={href}>Retomar pagamento</Link></Button>
    </article>
  );
};

type MyOrderRowProps = { order: StudentOrder; lessonId?: string };

/** Linha do histórico: card no mobile (M5) e linha da tabela no desktop (M1). */
export const MyOrderRow = ({ order, lessonId }: MyOrderRowProps) => {
  const href = paths.studentOrder.getHref(order.orderId);
  const paid = order.status === 'paid';
  const action = paid
    ? <Button asChild size="sm" variant="outline" className="w-full text-xs md:w-auto"><Link to={lessonId ? paths.studentLesson.getHref(lessonId) : href}>{lessonId ? 'Ir para o curso' : 'Liberando seu acesso'}</Link></Button>
    : order.status === 'awaiting-payment'
      ? <Button asChild size="sm" variant="outline" className="w-full text-xs md:w-auto"><Link to={href}>Retomar pagamento</Link></Button>
      : <Button asChild size="sm" variant="link" className="h-8 w-full px-0 text-xs md:w-auto"><Link to={href}>Ver pedido</Link></Button>;
  return (
    <article aria-label={`Pedido #${order.number}`}
      className="flex flex-wrap gap-y-2 rounded-xl border border-border bg-card p-4 text-sm leading-5 md:flex-nowrap md:items-center md:gap-4 md:rounded-none md:border-0 md:bg-transparent md:px-5 md:py-4">
      <Link className="font-semibold text-foreground hover:underline md:order-1 md:w-24 md:shrink-0 md:text-primary" to={href}>#{order.number}</Link>
      <time dateTime={order.createdAt} className={`font-semibold text-foreground md:order-4 md:w-26 md:shrink-0 md:font-normal md:before:content-none ${joined}`}>{purchaseDay(order.createdAt)}</time>
      <div className="flex basis-full flex-col gap-2 md:order-2 md:basis-auto md:min-w-0 md:flex-1 md:gap-0.5">
        <h2 className="font-sans text-sm font-medium leading-5 tracking-normal text-foreground">{order.course.title}</h2>
        <p className="text-muted-foreground md:text-xs md:leading-4">{order.offer.name}</p>
      </div>
      <p className="text-muted-foreground md:order-3 md:w-27.5 md:shrink-0 md:text-foreground">{purchasePrice(order.priceCents)}</p>
      <p className={`md:order-5 md:w-35 md:shrink-0 md:before:content-none ${joined} ${order.paymentMethod ? 'text-muted-foreground md:text-foreground' : 'text-muted-foreground'}`}>{paymentMethod(order)}</p>
      <div className="flex basis-full flex-col items-start gap-2 md:order-6 md:basis-auto md:w-47.5 md:shrink-0 md:gap-1.5">
        <OrderStatusBadge status={order.status} />
        {action}
      </div>
    </article>
  );
};
