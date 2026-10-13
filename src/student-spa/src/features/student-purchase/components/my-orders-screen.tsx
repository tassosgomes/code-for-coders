import { Receipt } from 'lucide-react';
import { Link } from 'react-router';

import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { paths } from '@/config/paths';
import type { useMyOrders } from '@/features/student-purchase/api/get-my-orders';
import { MyOrderRow, PendingOrderCard } from '@/features/student-purchase/components/my-order-row';
import { AlertActions, ErrorAlert } from '@/features/student-purchase/components/purchase-alert';

const cardClassName = 'flex w-full flex-col gap-4 rounded-xl border border-border bg-card p-6';
const columns = [['Pedido', 'w-24'], ['Curso · opção', 'min-w-0 flex-1'], ['Valor', 'w-27.5'], ['Data', 'w-26'], ['Meio', 'w-35'], ['Situação', 'w-47.5']] as const;

type PaginationProps = { page: number; totalPages: number; disabled: boolean; onPageChange: (page: number) => void };
const pageButtonClassName = 'h-8 min-w-8 px-3 text-xs';

/** Paginação compacta do Figma: ‹ 1 2 ›, página atual em outline. */
const MyOrdersPagination = ({ page, totalPages, disabled, onPageChange }: PaginationProps) => (
  <nav aria-label="Paginação dos pedidos" className="flex items-center justify-end gap-1">
    <Button size="sm" variant="ghost" className={pageButtonClassName} aria-label="Página anterior" disabled={disabled || page <= 1} onClick={() => onPageChange(page - 1)}>‹</Button>
    {Array.from({ length: totalPages }, (_, index) => index + 1).map((item) => (
      <Button key={item} size="sm" variant={item === page ? 'outline' : 'ghost'} className={pageButtonClassName} aria-label={`Página ${item}`}
        aria-current={item === page ? 'page' : undefined} disabled={disabled} onClick={() => onPageChange(item)}>{item}</Button>
    ))}
    <Button size="sm" variant="ghost" className={pageButtonClassName} aria-label="Próxima página" disabled={disabled || page >= totalPages} onClick={() => onPageChange(page + 1)}>›</Button>
    <p aria-live="polite" className="sr-only">Página {page} de {totalPages}</p>
  </nav>
);

type MyOrdersScreenProps = {
  orders: ReturnType<typeof useMyOrders>;
  lessons: Record<string, string>;
  onPageChange: (page: number) => void;
};

export const MyOrdersScreen = ({ orders, lessons, onPageChange }: MyOrdersScreenProps) => {
  const header = <header className="flex flex-col gap-1"><p className="typo-overline text-primary">Minha conta</p><h1 className="typo-h2 text-foreground">Meus pedidos</h1></header>;
  let body;
  if (orders.isPending) {
    body = <div aria-label="Carregando pedidos" aria-busy="true" className="flex flex-col gap-4 md:gap-6">
      <div className={cardClassName}>
        <Skeleton className="h-4 w-105 max-w-full rounded-lg bg-secondary" />
        <Skeleton className="h-4 w-75 max-w-full rounded-lg bg-secondary" />
        <Skeleton className="h-10 w-40 rounded-lg bg-secondary" />
      </div>
      <div className={cardClassName}>
        {[1, 2, 3].map((row) => <Skeleton key={row} className="h-4 w-225 max-w-full rounded-lg bg-secondary" />)}
      </div>
    </div>;
  } else if (!orders.data) {
    body = <ErrorAlert title="Não foi possível carregar seus pedidos agora.">
      <AlertActions><Button size="sm" variant="outline" className="text-xs text-foreground" onClick={() => void orders.refetch()}>Tentar de novo</Button></AlertActions>
    </ErrorAlert>;
  } else if (orders.data.pagination.total === 0) {
    body = <div className="flex flex-col items-center gap-4 rounded-xl border border-border bg-card px-8 py-10 text-center">
      <span className="flex size-12 items-center justify-center rounded-lg bg-secondary text-primary"><Receipt aria-hidden="true" className="size-6" /></span>
      <h2 className="typo-h4 text-foreground">Você ainda não tem compras</h2>
      <p className="text-sm text-muted-foreground">Quando você comprar um curso, o pedido aparece aqui, com a situação e o comprovante de pagamento por e-mail.</p>
      <Button asChild><Link to={paths.studentShowcase.getHref()}>Ver cursos</Link></Button>
    </div>;
  } else {
    const { data, pagination } = orders.data;
    // G13: na primeira página o pendente sai da lista e aparece em destaque.
    const pending = pagination.page === 1 ? data.filter((order) => order.status === 'awaiting-payment') : [];
    const remaining = data.filter((order) => !pending.includes(order));
    body = <>
      {pending.map((order) => <PendingOrderCard key={order.orderId} order={order} />)}
      {remaining.length > 0 ? <section aria-label="Histórico de pedidos"
        className="flex flex-col gap-4 md:gap-0 md:divide-y md:divide-border md:overflow-hidden md:rounded-xl md:border md:border-border md:bg-card">
        <div aria-hidden="true" className="hidden gap-4 bg-muted px-5 py-2.5 md:flex">
          {columns.map(([title, width]) => <span key={title} className={`typo-overline shrink-0 text-muted-foreground ${width}`}>{title}</span>)}
        </div>
        {remaining.map((order) => <MyOrderRow key={order.orderId} order={order} lessonId={lessons[order.course.courseId]} />)}
      </section> : null}
      {data.length === 0 ? <p className="text-sm text-muted-foreground">Nenhum pedido nesta página.</p> : null}
      {pagination.totalPages > 1 ? <MyOrdersPagination page={pagination.page} totalPages={pagination.totalPages} disabled={orders.isFetching} onPageChange={onPageChange} /> : null}
    </>;
  }
  return <div className="flex flex-col gap-4 md:gap-6">{header}{body}</div>;
};
