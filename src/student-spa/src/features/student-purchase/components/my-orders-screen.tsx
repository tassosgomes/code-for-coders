import { AlertCircle, Receipt } from 'lucide-react';
import { Link } from 'react-router';

import { Alert, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { paths } from '@/config/paths';
import type { useMyOrders } from '@/features/student-purchase/api/get-my-orders';
import { MyOrderRow } from '@/features/student-purchase/components/my-order-row';

type MyOrdersScreenProps = {
  orders: ReturnType<typeof useMyOrders>;
  lessons: Record<string, string>;
  onPageChange: (page: number) => void;
};

export const MyOrdersScreen = ({ orders, lessons, onPageChange }: MyOrdersScreenProps) => {
  if (orders.isPending) return <div aria-label="Carregando pedidos" className="grid gap-4"><Skeleton className="h-8 w-56" /><Skeleton className="h-40" />{[1, 2, 3].map((row) => <Skeleton key={row} className="h-20" />)}</div>;
  if (!orders.data) return <Alert variant="destructive"><AlertCircle aria-hidden="true" /><AlertTitle>Não foi possível carregar seus pedidos agora.</AlertTitle><Button onClick={() => void orders.refetch()}>Tentar de novo</Button></Alert>;
  const { data, pagination } = orders.data;
  const pending = pagination.page === 1 ? data.filter((order) => order.status === 'awaiting-payment') : [];
  const remaining = data.filter((order) => !pending.includes(order));
  return (
    <div className="grid gap-6">
      <p className="typo-overline text-primary">Minha conta</p>
      <h2 className="typo-h2">Meus pedidos</h2>
      {pagination.total === 0 ? <div className="grid justify-items-start gap-4 rounded-xl border border-border bg-card p-8"><Receipt aria-hidden="true" className="size-8 text-muted-foreground" /><h3 className="typo-h3">Você ainda não tem compras</h3><p>Quando você comprar um curso, o pedido aparece aqui, com a situação e o comprovante de pagamento por e-mail.</p><Button asChild><Link to={paths.studentShowcase.getHref()}>Ver cursos</Link></Button></div> : <>
        {pending.map((order) => <MyOrderRow key={order.orderId} order={order} highlighted />)}
        {remaining.length > 0 && <section aria-label="Histórico de pedidos" className="grid gap-3">
          <div aria-hidden="true" className="hidden grid-cols-[6rem_2fr_1fr_1fr_1fr_1.5fr] gap-4 px-5 text-xs font-medium text-muted-foreground md:grid"><span>Pedido</span><span>Curso · opção</span><span>Valor</span><span>Data</span><span>Meio</span><span>Situação</span></div>
          {remaining.map((order) => <MyOrderRow key={order.orderId} order={order} lessonId={lessons[order.course.courseId]} />)}
        </section>}
        {data.length === 0 && <p>Nenhum pedido nesta página.</p>}
        {pagination.totalPages > 1 && <nav aria-label="Paginação dos pedidos" className="flex items-center justify-end gap-4"><Button variant="outline" disabled={pagination.page <= 1 || orders.isFetching} onClick={() => onPageChange(pagination.page - 1)}>Anterior</Button><p aria-live="polite">Página {pagination.page} de {pagination.totalPages}</p><Button variant="outline" disabled={pagination.page >= pagination.totalPages || orders.isFetching} onClick={() => onPageChange(pagination.page + 1)}>Próxima</Button></nav>}
      </>}
    </div>
  );
};
