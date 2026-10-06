import axios from 'axios';
import { Link, useLocation, useNavigate } from 'react-router';

import { paths } from '@/config/paths';
import { useFinanceOrders } from '@/features/finance-area/api/list-finance-orders';
import { FinanceAreaScreen } from '@/features/finance-area/components/finance-area-screen';
import { FinanceOrderFilters } from '@/features/finance-area/components/finance-order-filters';
import { FinanceOrderStatus } from '@/features/finance-area/components/finance-order-status';
import { useFinanceFilters, emptyFinanceFilters } from '@/features/finance-area/hooks/use-finance-filters';
import type { FinanceStudentLookup, FinanceCourseOptions } from '@/features/finance-area/types/finance-filter-support';
import { financeMoney, financeDate, financePaymentMethod } from '@/features/finance-area/utils/finance-order-format';

type FinanceOrdersScreenProps = { lookup: FinanceStudentLookup; courses: FinanceCourseOptions };
export const FinanceOrdersScreen = ({ lookup, courses }: FinanceOrdersScreenProps) => {
  const filter = useFinanceFilters();
  const query = useFinanceOrders({ ...filter.filters, page: filter.page });
  const location = useLocation();
  const navigate = useNavigate();
  if (axios.isAxiosError(query.error) && query.error.response?.status === 403) return <FinanceAreaScreen />;
  const returnTo = paths.staffFinance.getHref(location.search);
  return <main className="page-shell finance-page"><header><p className="eyebrow">Financeiro</p><h1>Pedidos</h1><p>Compras feitas pelos alunos, com a situação do pagamento.</p></header>
    <FinanceOrderFilters filters={filter.filters} count={filter.count} apply={filter.apply} lookup={lookup} courses={courses} />
    {query.isPending ? <div role="status" className="finance-skeleton">Carregando pedidos…{Array.from({ length: 5 }, (_, index) => <div key={index} />)}</div> : query.isError ? <div role="alert" className="inline-alert">Não foi possível carregar os pedidos agora. <button type="button" className="outline-button" onClick={() => void query.refetch()}>Tentar de novo</button></div> : <>
      <p role="status">{query.data.pagination.total} {query.data.pagination.total === 1 ? 'pedido' : 'pedidos'}</p>
      {query.data.data.length === 0 ? <section className="empty-state">{filter.count ? <><p>Nenhum pedido com estes filtros.</p><button type="button" className="outline-button" onClick={() => { lookup.reset(); filter.apply(emptyFinanceFilters); }}>Limpar filtros</button></> : <p>Ainda não há pedidos. Quando um aluno comprar um curso, o pedido aparece aqui.</p>}</section> : <div className="finance-orders-table"><table><caption className="visually-hidden">Pedidos da escola</caption><thead><tr>{['Nº', 'Data', 'Aluno', 'Curso · opção', 'Valor', 'Meio', 'Situação'].map((title) => <th key={title} scope="col">{title}</th>)}</tr></thead><tbody>{query.data.data.map((order) => <tr key={order.orderId} onClick={() => void navigate(paths.staffFinanceOrder.getHref(order.orderId), { state: { returnTo } })}>
        <td><Link aria-label={`Pedido #${order.number}`} to={paths.staffFinanceOrder.getHref(order.orderId)} state={{ returnTo }} onClick={(event) => event.stopPropagation()}>#{order.number}</Link></td><td>{financeDate(order.createdAt)}</td><td><strong>{order.student.name}</strong><small>{order.student.email}</small></td><td><strong>{order.courseTitle}</strong><small>{order.offerName}</small></td><td>{financeMoney(order.priceCents)}</td><td>{financePaymentMethod(order.paymentMethod)}</td><td><FinanceOrderStatus status={order.status} /></td>
      </tr>)}</tbody></table></div>}
      {query.data.pagination.totalPages > 1 ? <nav className="finance-pagination" aria-label="Páginas dos pedidos"><button type="button" className="outline-button" disabled={filter.page === 1} onClick={() => filter.apply(filter.filters, filter.page - 1)}>Anterior</button><span>Página {filter.page} de {query.data.pagination.totalPages}</span><button type="button" className="outline-button" disabled={filter.page >= query.data.pagination.totalPages} onClick={() => filter.apply(filter.filters, filter.page + 1)}>Próxima</button></nav> : null}
    </>}
  </main>;
};
