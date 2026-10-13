import axios from 'axios';
import { ArrowLeft, Check, CircleAlert, Search } from 'lucide-react';
import { useState, type ReactNode } from 'react';
import { Link, useLocation } from 'react-router';

import { paths } from '@/config/paths';
import { useFinanceOrder } from '@/features/finance-area/api/get-finance-order';
import { FinanceAreaScreen } from '@/features/finance-area/components/finance-area-screen';
import { FinanceOrderStatus } from '@/features/finance-area/components/finance-order-status';
import { financeAccessPeriod, financeMoney, financeMoment, financePaymentMethod } from '@/features/finance-area/utils/finance-order-format';

const Row = ({ label, children }: { label: string; children: ReactNode }) => <div className="finance-detail-row"><dt>{label}</dt><dd>{children}</dd></div>;
const Card = ({ title, children, note }: { title: string; children: ReactNode; note?: ReactNode }) => <section className="finance-card finance-detail-card" aria-label={title}><h2>{title}</h2><dl>{children}</dl>{note}</section>;

type FinanceOrderDetailScreenProps = { orderId: string };
export const FinanceOrderDetailScreen = ({ orderId }: FinanceOrderDetailScreenProps) => {
  const query = useFinanceOrder({ orderId });
  const location = useLocation();
  const state: unknown = location.state;
  const returnTo = state && typeof state === 'object' && 'returnTo' in state && typeof state.returnTo === 'string' && state.returnTo.startsWith('/financeiro') ? state.returnTo : paths.staffFinance.getHref();
  const [copied, setCopied] = useState(false);
  const [copyFailed, setCopyFailed] = useState(false);
  if (axios.isAxiosError(query.error) && query.error.response?.status === 403) return <FinanceAreaScreen />;
  const back = <Link className="finance-back-link" to={returnTo}><ArrowLeft aria-hidden="true" size={16} />Voltar para Pedidos</Link>;
  if (query.isPending) {
    return <main className="page-shell finance-page"><div role="status" aria-label="Carregando pedido" className="finance-detail">
      <span className="finance-skeleton-bar finance-skeleton-title" />
      {[1, 2, 3, 4].map((card) => <div key={card} className="finance-card finance-skeleton"><span /><span /><span /></div>)}
    </div></main>;
  }
  if (query.isError) {
    return <main className="page-shell finance-page">{axios.isAxiosError(query.error) && query.error.response?.status === 404
      ? <section className="finance-card finance-empty finance-not-found"><span className="finance-empty-tile"><Search aria-hidden="true" size={24} /></span>
        <h1>Pedido não encontrado</h1><p>Este pedido não existe ou não pertence a esta escola.</p>
        <Link className="primary-button" to={returnTo}>Voltar para Pedidos</Link></section>
      : <div className="finance-detail"><div role="alert" className="finance-alert"><CircleAlert aria-hidden="true" size={16} /><div><strong>Não foi possível carregar o pedido agora.</strong>
        <button type="button" className="outline-button finance-alert-action" onClick={() => void query.refetch()}>Tentar de novo</button></div></div>{back}</div>}
    </main>;
  }
  const order = query.data;
  // F3.b: cada momento só aparece quando existe; Expirado e Cancelado vêm antes do Pago (RN-V08).
  const moments: [string, string][] = [['Criado em', financeMoment(order.createdAt)]];
  if (order.paymentPageExpiresAt) moments.push(['Página de pagamento', `válida até ${financeMoment(order.paymentPageExpiresAt)}`]);
  if (order.expiredAt) moments.push(['Expirado em', financeMoment(order.expiredAt)]);
  if (order.cancelledAt) moments.push(['Cancelado em', financeMoment(order.cancelledAt)]);
  if (order.paidAt) moments.push(['Pago em', financeMoment(order.paidAt)]);
  const grant = order.grantId
    ? <span className="finance-badge finance-badge-success"><Check aria-hidden="true" size={12} />Existe</span>
    : order.status === 'paid'
      ? <span className="finance-badge finance-badge-warning">Ainda não existe</span>
      : <span className="finance-badge finance-badge-neutral">— Sem concessão{order.status === 'awaiting-payment' ? ': pedido não pago' : ''}</span>;
  return <main className="page-shell finance-page"><div className="finance-detail">
    <nav aria-label="Caminho" className="finance-breadcrumb"><Link to={paths.staffFinance.getHref()}>Financeiro</Link><span aria-hidden="true">/</span><Link to={returnTo}>Pedidos</Link><span aria-hidden="true">/</span><span aria-current="page">#{order.number}</span></nav>
    <header className="finance-detail-heading"><h1>Pedido #{order.number}</h1><FinanceOrderStatus status={order.status} /></header>
    <Card title="Pedido">
      <Row label="Aluno">{order.student.name} · {order.student.email}</Row>
      <Row label="Curso">{order.courseTitle}</Row>
      <Row label="Opção">{order.offerName}</Row>
      <Row label="Valor">{financeMoney(order.priceCents)}</Row>
      <Row label="Vigência prometida">{financeAccessPeriod(order.accessPeriod)}</Row>
    </Card>
    <Card title="Pagamento">
      <Row label="Meio">{order.paymentMethod ? financePaymentMethod(order.paymentMethod) : 'Ainda não escolhido'}</Row>
      {order.paidAmountCents ? <Row label="Valor recebido">{financeMoney(order.paidAmountCents)}</Row> : null}
      {order.paymentReference ? <Row label="Referência do pagamento no gateway">
        <span className="finance-reference"><code>{order.paymentReference}</code>
          <button type="button" className="outline-button finance-copy-button" onClick={async () => { try { await navigator.clipboard.writeText(order.paymentReference!); setCopied(true); setCopyFailed(false); } catch { setCopyFailed(true); } }}>{copied ? 'Copiado' : 'Copiar'}</button></span>
        <small>Para localizar o pagamento no painel do gateway.</small>
        {copyFailed ? <span role="alert" className="field-error">Não foi possível copiar. Selecione a referência.</span> : null}
      </Row> : null}
    </Card>
    <Card title="Momentos">{moments.map(([label, value]) => <Row key={label} label={label}>{value}</Row>)}</Card>
    <Card title="Acesso" note={order.status === 'paid' && !order.grantId ? <p className="finance-detail-note">O acesso é liberado sozinho; atualize em alguns instantes.</p> : null}>
      <Row label="Concessão">{grant}</Row>
      {order.accessGrantedAt ? <Row label="Acesso liberado em">{financeMoment(order.accessGrantedAt)}</Row> : null}
    </Card>
    {back}
  </div></main>;
};
