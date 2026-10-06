import axios from 'axios';
import { useState } from 'react';
import { Link, useLocation } from 'react-router';

import { paths } from '@/config/paths';
import { useFinanceOrder } from '@/features/finance-area/api/get-finance-order';
import { FinanceAreaScreen } from '@/features/finance-area/components/finance-area-screen';
import { FinanceOrderStatus } from '@/features/finance-area/components/finance-order-status';
import { financeMoney, financeMoment, financePaymentMethod } from '@/features/finance-area/utils/finance-order-format';

type FinanceOrderDetailScreenProps = { orderId: string };
export const FinanceOrderDetailScreen = ({ orderId }: FinanceOrderDetailScreenProps) => {
  const query = useFinanceOrder({ orderId });
  const location = useLocation();
  const state: unknown = location.state;
  const returnTo = state && typeof state === 'object' && 'returnTo' in state && typeof state.returnTo === 'string' && state.returnTo.startsWith('/financeiro') ? state.returnTo : paths.staffFinance.getHref();
  const [copied, setCopied] = useState(false);
  const [copyFailed, setCopyFailed] = useState(false);
  if (axios.isAxiosError(query.error) && query.error.response?.status === 403) return <FinanceAreaScreen />;
  const back = <Link className="outline-button" to={returnTo}>Voltar para Pedidos</Link>;
  if (query.isPending) return <main className="page-shell"><div role="status" className="finance-skeleton">Carregando pedido…{Array.from({ length: 4 }, (_, index) => <div key={index} />)}</div></main>;
  if (query.isError) return <main className="page-shell">{axios.isAxiosError(query.error) && query.error.response?.status === 404 ? <><h1>Pedido não encontrado</h1><p>Este pedido não existe ou não pertence a esta escola.</p></> : <div role="alert" className="inline-alert">Não foi possível carregar o pedido agora. <button type="button" className="outline-button" onClick={() => void query.refetch()}>Tentar de novo</button></div>}{back}</main>;
  const order = query.data;
  const moments = [['Criado em', order.createdAt], ['Página de pagamento válida até', order.paymentPageExpiresAt], ['Pago em', order.paidAt], ['Expirado em', order.expiredAt], ['Cancelado em', order.cancelledAt]];
  return <main className="page-shell finance-page"><nav aria-label="Caminho"><Link to={returnTo}>Financeiro / Pedidos</Link> / #{order.number}</nav><header className="finance-detail-heading"><h1>Pedido #{order.number}</h1><FinanceOrderStatus status={order.status} /></header>
    <div className="finance-detail-cards">
      <section className="catalog-record-card"><h2>Pedido</h2><dl><dt>Aluno</dt><dd>{order.student.name}<small>{order.student.email}</small></dd><dt>Curso</dt><dd>{order.courseTitle}</dd><dt>Opção</dt><dd>{order.offerName}</dd><dt>Valor</dt><dd>{financeMoney(order.priceCents)}</dd><dt>Vigência prometida</dt><dd>{order.accessPeriod.type === 'lifetime' ? 'Acesso vitalício' : `Acesso por ${order.accessPeriod.months} meses, contados a partir da liberação do acesso`}</dd></dl></section>
      <section className="catalog-record-card"><h2>Pagamento</h2><dl><dt>Meio</dt><dd>{order.paymentMethod ? financePaymentMethod(order.paymentMethod) : 'Ainda não escolhido'}</dd><dt>Valor recebido</dt><dd>{order.paidAmountCents ? financeMoney(order.paidAmountCents) : 'Ainda não confirmado'}</dd><dt>Referência do pagamento no gateway</dt><dd>{order.paymentReference ?? 'Ainda não disponível'}{order.paymentReference ? <><button type="button" className="outline-button" onClick={async () => { try { await navigator.clipboard.writeText(order.paymentReference!); setCopied(true); setCopyFailed(false); } catch { setCopyFailed(true); } }}>{copied ? 'Copiado' : 'Copiar'}</button><small>Para localizar o pagamento no painel do gateway.</small>{copyFailed ? <p role="alert">Não foi possível copiar. Selecione a referência.</p> : null}</> : null}</dd></dl></section>
      <section className="catalog-record-card"><h2>Momentos</h2><dl>{moments.filter(([, value]) => value).map(([label, value]) => <div key={label}><dt>{label}</dt><dd>{financeMoment(value!)}</dd></div>)}</dl></section>
      <section className="catalog-record-card"><h2>Acesso</h2><dl><dt>Concessão</dt><dd>{order.grantId ? <>✓ Existe<small>{order.grantId}</small></> : order.status === 'paid' ? '◷ Ainda não existe' : 'Sem concessão'}</dd>{order.accessGrantedAt ? <><dt>Acesso liberado em</dt><dd>{financeMoment(order.accessGrantedAt)}</dd></> : null}</dl>{order.status === 'paid' && !order.grantId ? <><p>O acesso é liberado sozinho; atualize em alguns instantes.</p><button type="button" className="outline-button" onClick={() => void query.refetch()}>Atualizar</button></> : null}</section>
    </div>{back}
  </main>;
};
