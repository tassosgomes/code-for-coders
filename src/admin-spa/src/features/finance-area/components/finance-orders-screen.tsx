import axios from 'axios';
import { CircleAlert, Receipt, Search } from 'lucide-react';
import { Link, useLocation, useNavigate } from 'react-router';

import { paths } from '@/config/paths';
import { useFinanceOrders } from '@/features/finance-area/api/list-finance-orders';
import { FinanceAreaScreen } from '@/features/finance-area/components/finance-area-screen';
import { FinanceOrderFilters } from '@/features/finance-area/components/finance-order-filters';
import { FinanceOrderStatus } from '@/features/finance-area/components/finance-order-status';
import { useFinanceFilters, emptyFinanceFilters } from '@/features/finance-area/hooks/use-finance-filters';
import type { FinanceStudentLookup, FinanceCourseOptions } from '@/features/finance-area/types/finance-filter-support';
import { financeMoney, financeDate, financePaymentMethod } from '@/features/finance-area/utils/finance-order-format';

const columns = [['Nº', 'number'], ['Data', 'date'], ['Aluno', 'student'], ['Curso · opção', 'course'], ['Valor', 'value'], ['Meio', 'method'], ['Situação', 'status']] as const;

type FinanceOrdersScreenProps = { lookup: FinanceStudentLookup; courses: FinanceCourseOptions };
export const FinanceOrdersScreen = ({ lookup, courses }: FinanceOrdersScreenProps) => {
  const filter = useFinanceFilters();
  const query = useFinanceOrders({ ...filter.filters, page: filter.page });
  const location = useLocation();
  const navigate = useNavigate();
  if (axios.isAxiosError(query.error) && query.error.response?.status === 403) return <FinanceAreaScreen />;
  const returnTo = paths.staffFinance.getHref(location.search);
  const clear = () => { lookup.reset(); filter.apply(emptyFinanceFilters); };
  let body;
  if (query.isPending) {
    body = <div role="status" aria-label="Carregando pedidos" className="finance-card finance-skeleton">{Array.from({ length: 5 }, (_, index) => <span key={index} />)}</div>;
  } else if (query.isError) {
    body = <div role="alert" className="finance-alert"><CircleAlert aria-hidden="true" size={16} /><div><strong>Não foi possível carregar os pedidos agora.</strong>
      <button type="button" className="outline-button finance-alert-action" onClick={() => void query.refetch()}>Tentar de novo</button></div></div>;
  } else if (query.data.data.length === 0) {
    body = <section className="finance-card finance-empty">
      <span className="finance-empty-tile">{filter.count ? <Search aria-hidden="true" size={24} /> : <Receipt aria-hidden="true" size={24} />}</span>
      {filter.count ? <><h2>Nenhum pedido com estes filtros.</h2><p>Ajuste a situação, o curso, o período ou o e-mail do aluno.</p>
        <button type="button" className="primary-button" onClick={clear}>Limpar filtros</button></>
        : <><h2>Ainda não há pedidos</h2><p>Quando um aluno comprar um curso, o pedido aparece aqui.</p></>}
    </section>;
  } else {
    const { data, pagination } = query.data;
    body = <>
      <div className="finance-orders-table"><table><caption className="visually-hidden">Pedidos da escola</caption>
        <thead><tr>{columns.map(([title, column]) => <th key={title} scope="col" className={`finance-col-${column}`}>{title}</th>)}</tr></thead>
        <tbody>{data.map((order) => <tr key={order.orderId} onClick={() => void navigate(paths.staffFinanceOrder.getHref(order.orderId), { state: { returnTo } })}>
          <td className="finance-col-number"><Link aria-label={`Pedido #${order.number}`} to={paths.staffFinanceOrder.getHref(order.orderId)} state={{ returnTo }} onClick={(event) => event.stopPropagation()}>#{order.number}</Link></td>
          <td className="finance-col-date">{financeDate(order.createdAt)}</td>
          <td className="finance-col-student"><strong>{order.student.name}</strong><small>{order.student.email}</small></td>
          <td className="finance-col-course"><strong>{order.courseTitle}</strong><small>{order.offerName}</small></td>
          <td className="finance-col-value">{financeMoney(order.priceCents)}</td>
          <td className="finance-col-method">{financePaymentMethod(order.paymentMethod)}</td>
          <td className="finance-col-status"><FinanceOrderStatus status={order.status} /></td>
        </tr>)}</tbody></table></div>
      <nav className="finance-pagination" aria-label="Páginas dos pedidos">
        <span>{pagination.size} por página</span>
        <button type="button" aria-label="Página anterior" disabled={filter.page <= 1} onClick={() => filter.apply(filter.filters, filter.page - 1)}>‹</button>
        {Array.from({ length: Math.max(pagination.totalPages, 1) }, (_, index) => index + 1).map((item) => (
          <button key={item} type="button" aria-label={`Página ${item}`} aria-current={item === filter.page ? 'page' : undefined} onClick={() => filter.apply(filter.filters, item)}>{item}</button>
        ))}
        <button type="button" aria-label="Próxima página" disabled={filter.page >= pagination.totalPages} onClick={() => filter.apply(filter.filters, filter.page + 1)}>›</button>
        <span aria-live="polite" className="visually-hidden">Página {filter.page} de {Math.max(pagination.totalPages, 1)}</span>
      </nav>
    </>;
  }
  return <main className="page-shell finance-page">
    <header className="finance-page-header"><p className="eyebrow">Financeiro</p><h1>Pedidos</h1><p className="page-subtitle">Compras feitas pelos alunos, com a situação do pagamento.</p></header>
    <FinanceOrderFilters filters={filter.filters} count={filter.count} apply={filter.apply} lookup={lookup} courses={courses} />
    {query.data && query.data.pagination.total > 0 ? <p role="status" className="finance-count">{query.data.pagination.total} {query.data.pagination.total === 1 ? 'pedido' : 'pedidos'}</p> : null}
    {body}
  </main>;
};
