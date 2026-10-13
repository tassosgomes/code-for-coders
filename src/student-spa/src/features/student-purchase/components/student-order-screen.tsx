import axios from 'axios';
import { ArrowLeft, Check, Clock, LoaderCircle, Search, TimerOff, X, type LucideIcon } from 'lucide-react';
import { useEffect, useRef, useState, type ReactNode } from 'react';
import { Link } from 'react-router';
import { toast } from 'sonner';

import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { paths } from '@/config/paths';
import { useCancelOrder } from '@/features/student-purchase/api/cancel-order';
import { useOrder } from '@/features/student-purchase/api/get-order';
import { useStartOrderPayment } from '@/features/student-purchase/api/start-order-payment';
import { CancelOrderConfirm } from '@/features/student-purchase/components/cancel-order-confirm';
import { OrderStatusBadge } from '@/features/student-purchase/components/order-status-badge';
import { AlertActions, ErrorAlert, InfoAlert } from '@/features/student-purchase/components/purchase-alert';
import { formatPendingPaymentDeadline, purchaseDay, purchasePeriod, purchasePrice } from '@/features/student-purchase/utils/purchase-labels';
import { useDocumentTitle } from '@/hooks/use-document-title';
import { cn } from 'cn';

const cardClassName = 'flex w-full flex-col gap-4 rounded-xl border border-border bg-card p-5 sm:p-6';
const panelTextClassName = 'text-base text-muted-foreground';
const actionsClassName = 'flex flex-col gap-2 sm:flex-row sm:flex-wrap sm:gap-3';
const actionClassName = 'w-full sm:w-auto';
const paymentMethods: Record<string, string> = { card: 'Cartão de crédito', pix: 'PIX', boleto: 'Boleto' };
const paidElsewhereNotice = 'Se você pagou este pedido e o pagamento ainda não foi compensado, o acesso é liberado sozinho quando ele chegar.';

type Tone = 'progress' | 'waiting' | 'success' | 'expired' | 'cancelled';
const tones: Record<Tone, { icon: LucideIcon; className: string }> = {
  progress: { icon: LoaderCircle, className: 'bg-accent text-primary [&>svg]:motion-safe:animate-spin' },
  waiting: { icon: Clock, className: 'bg-warning-soft text-warning-soft-foreground' },
  success: { icon: Check, className: 'bg-success-soft text-success-soft-foreground' },
  expired: { icon: TimerOff, className: 'bg-destructive/10 text-destructive' },
  cancelled: { icon: X, className: 'bg-secondary text-primary' },
};

/** OrderStatePanel: o título anuncia a troca de estado (aria-live), o resto acompanha. */
const OrderStatePanel = ({ tone, title, children }: { tone: Tone; title: string; children?: ReactNode }) => {
  const { icon: Icon, className } = tones[tone];
  return (
    <section aria-label="Situação do pedido" className={cardClassName}>
      <div role="status" aria-live="polite" aria-atomic="true" className="flex items-center gap-3">
        <span className={cn('flex size-10 shrink-0 items-center justify-center rounded-lg', className)}><Icon aria-hidden="true" className="size-5" /></span>
        <h2 className="typo-h4 min-w-0 flex-1 text-foreground sm:typo-h3">{title}</h2>
      </div>
      {children}
    </section>
  );
};

const SummaryRow = ({ label, children }: { label: string; children: ReactNode }) => (
  <div className="flex gap-4 text-sm leading-5">
    <dt className="w-24 shrink-0 text-muted-foreground sm:w-30">{label}</dt>
    <dd className="min-w-0 flex-1 font-medium text-foreground">{children}</dd>
  </div>
);

const OrderLoading = () => (
  <div aria-label="Carregando pedido" aria-busy="true" className="flex w-full max-w-190 flex-col gap-6">
    <div className="flex flex-col gap-2">
      <Skeleton className="h-3.5 w-30 rounded-lg bg-secondary" />
      <Skeleton className="h-8 w-90 max-w-full rounded-lg bg-secondary" />
      <Skeleton className="h-4 w-75 max-w-full rounded-lg bg-secondary" />
    </div>
    <div className={cardClassName}>
      <Skeleton className="h-7 w-70 max-w-full rounded-lg bg-secondary" />
      {[1, 2].map((line) => <Skeleton key={line} className="h-4 w-full rounded-lg bg-secondary" />)}
    </div>
    <div className={cardClassName}>
      <Skeleton className="h-5 w-50 max-w-full rounded-lg bg-secondary" />
      {[1, 2, 3].map((line) => <Skeleton key={line} className="h-4 w-full rounded-lg bg-secondary" />)}
    </div>
  </div>
);

type StudentOrderScreenProps = {
  orderId: string;
  order: ReturnType<typeof useOrder>;
  result: string | null;
  delayed: boolean;
  lessonId?: string;
  csrfToken: string;
};

export const StudentOrderScreen = ({ orderId, order, result, delayed, lessonId, csrfToken }: StudentOrderScreenProps) => {
  useDocumentTitle('Pedido');
  const payment = useStartOrderPayment();
  const cancel = useCancelOrder();
  const [cancelDialogOpen, setCancelDialogOpen] = useState(false);
  const courseLink = useRef<HTMLAnchorElement>(null);

  const confirmed = order.data?.status === 'paid' && !!order.data.accessGrantedAt && !!lessonId;
  useEffect(() => {
    if (confirmed && result === 'concluido') courseLink.current?.focus();
  }, [confirmed, result]);

  if (!order.data && order.isPending) return <OrderLoading />;
  if (!order.data) {
    if (axios.isAxiosError(order.error) && order.error.response?.status === 404) {
      return (
        <div className="flex w-full max-w-160 flex-col items-center gap-4 rounded-xl border border-border bg-card px-8 py-10 text-center">
          <span className="flex size-12 items-center justify-center rounded-lg bg-secondary text-primary"><Search aria-hidden="true" className="size-6" /></span>
          <h1 className="typo-h4 text-foreground">Pedido não encontrado</h1>
          <p className="text-sm text-muted-foreground">Este pedido não existe ou não é seu. Confira o endereço ou veja a lista dos seus pedidos.</p>
          <Button asChild><Link to={paths.studentMyOrders.getHref()}>Ver meus pedidos</Link></Button>
        </div>
      );
    }
    return (
      <ErrorAlert className="max-w-190" title="Não foi possível carregar o pedido agora.">
        <AlertActions><Button size="sm" variant="outline" className="text-xs text-foreground" onClick={() => void order.refetch()}>Tentar de novo</Button></AlertActions>
      </ErrorAlert>
    );
  }

  const data = order.data;
  const isPaymentExpired = axios.isAxiosError(payment.error) &&
    (payment.error.response?.data as { code?: string } | undefined)?.code === 'ORDER_NOT_PAYABLE';
  const isCancelled = data.status === 'cancelled';
  const isExpired = !isCancelled && (data.status === 'expired' || isPaymentExpired);
  const awaiting = !isCancelled && !isExpired && data.status === 'awaiting-payment';
  const following = result === 'concluido' && !confirmed;
  const pendingPayment = awaiting ? data.pendingPayment : null;
  // Depois de obter o endereço a página sai para o gateway; o botão fica desabilitado até lá.
  const opening = payment.isPending || payment.isSuccess;
  const startPayment = () => payment.mutate({ orderId, csrfToken });

  const openCancelDialog = (open: boolean) => {
    if (open) cancel.reset();
    setCancelDialogOpen(open);
  };
  const confirmCancel = () => cancel.mutate({ orderId, csrfToken }, {
    onSuccess: () => { setCancelDialogOpen(false); toast.success('Pedido cancelado.'); },
    onError: (error) => {
      // Pedido que já não está aguardando: fecha e mostra a situação atual.
      if (axios.isAxiosError(error) && error.response?.status === 422) { setCancelDialogOpen(false); void order.refetch(); }
    },
  });
  const cancelFailed = cancel.isError && !(axios.isAxiosError(cancel.error) && cancel.error.response?.status === 422);

  const delayNotice = <InfoAlert>
    <p>A confirmação pode demorar. Seu acesso será liberado sozinho assim que o pagamento for confirmado e você receberá o comprovante por e-mail. Você pode sair desta página: acompanhe em <Link className="underline underline-offset-4" to={paths.studentMyOrders.getHref()}>Meus pedidos</Link>.</p>
  </InfoAlert>;
  const refreshNotice = order.isRefetchError ? <InfoAlert>Não conseguimos atualizar agora. Tentaremos de novo.</InfoAlert> : null;
  const paymentError = (description: string) => payment.isError && !isPaymentExpired
    ? <ErrorAlert title="Não foi possível abrir o pagamento agora.">
      <p>{description}</p>
      <AlertActions><Button size="sm" variant="outline" className="text-xs text-foreground" disabled={opening || !csrfToken} onClick={startPayment}>Tentar de novo</Button></AlertActions>
    </ErrorAlert>
    : null;
  const paymentActions = (primary: string, openingLabel: string, cancelLabel: string) => (
    <div className={actionsClassName}>
      <Button className={actionClassName} disabled={opening || !csrfToken} onClick={startPayment}>
        {opening ? <><LoaderCircle aria-hidden="true" className="animate-spin" />{openingLabel}</> : primary}
      </Button>
      <Button className={actionClassName} variant="outline" disabled={cancel.isPending || !csrfToken} onClick={() => openCancelDialog(true)}>{cancelLabel}</Button>
    </div>
  );
  const buyAgain = <div className={actionsClassName}>
    <Button asChild className={actionClassName}><Link to={paths.studentPurchase.getHref(data.offer.offerId, data.course.courseId)}>Comprar de novo</Link></Button>
  </div>;

  let panel: ReactNode;
  if (confirmed && lessonId) {
    panel = <OrderStatePanel tone="success" title="Compra confirmada">
      <p className={panelTextClassName}>Seu acesso a {data.course.title} está liberado. {purchasePeriod(data.accessPeriod)}.</p>
      {result === 'concluido' ? <p className="text-sm text-muted-foreground">Você receberá o comprovante por e-mail.</p> : null}
      <div className={actionsClassName}>
        <Button asChild className={actionClassName}><Link ref={courseLink} to={paths.studentLesson.getHref(lessonId)}>Ir para o curso</Link></Button>
      </div>
    </OrderStatePanel>;
  } else if (isCancelled) {
    panel = <OrderStatePanel tone="cancelled" title="Pedido cancelado">
      <p className={panelTextClassName}>Você desistiu deste pedido{data.cancelledAt ? ` em ${purchaseDay(data.cancelledAt)}` : ''}. Nenhum acesso foi liberado. Se quiser comprar de novo, a nova compra segue as condições vigentes da opção.</p>
      <InfoAlert>{paidElsewhereNotice}</InfoAlert>
      {buyAgain}
    </OrderStatePanel>;
  } else if (isExpired) {
    panel = <OrderStatePanel tone="expired" title="Pedido expirado">
      <p className={panelTextClassName}>O prazo para pagar este pedido venceu{data.expiredAt ? ` em ${purchaseDay(data.expiredAt)}` : ''} e o acesso não foi liberado. Se você ainda quiser o curso, faça uma nova compra.</p>
      <InfoAlert>{paidElsewhereNotice}</InfoAlert>
      {buyAgain}
    </OrderStatePanel>;
  } else if (data.status === 'paid' || following) {
    const releasing = data.status === 'paid';
    panel = <OrderStatePanel tone="progress" title={releasing ? 'Liberando seu acesso' : 'Confirmando pagamento'}>
      {delayed ? delayNotice : <p className={panelTextClassName}>{releasing
        ? 'Seu pagamento foi confirmado. Estamos liberando o curso: isso leva alguns segundos. Esta página atualiza sozinha.'
        : 'Recebemos a sua volta do pagamento e estamos aguardando a confirmação. Esta página atualiza sozinha: você não precisa recarregar.'}</p>}
      {refreshNotice}
    </OrderStatePanel>;
  } else if (pendingPayment) {
    const pix = pendingPayment.method === 'pix';
    panel = <OrderStatePanel tone="waiting" title={pix ? 'Aguardando pagamento do PIX' : 'Aguardando pagamento do boleto'}>
      <p className={panelTextClassName}>{pix
        ? `Pague o PIX até ${formatPendingPaymentDeadline(pendingPayment.expiresAt)}. O acesso será liberado sozinho assim que o pagamento for confirmado. O curso ainda não está liberado.`
        : `Pague o boleto até ${formatPendingPaymentDeadline(pendingPayment.expiresAt)}. O acesso a ${data.course.title} será liberado sozinho quando o pagamento for compensado, o que pode levar até 3 dias úteis. O curso ainda não está liberado.`}</p>
      {paymentError('Seu pedido continua aguardando pagamento. Tente de novo em instantes.')}
      {paymentActions(pix ? 'Ver código PIX' : 'Ver boleto', pix ? 'Ver código PIX' : 'Ver boleto', 'Desistir e pagar de outra forma')}
    </OrderStatePanel>;
  } else {
    const resumed = !!data.paymentPageExpiresAt || result === 'saiu';
    panel = <OrderStatePanel tone="waiting" title="Aguardando pagamento">
      <p className={panelTextClassName}>{data.paymentPageExpiresAt
        ? `Você saiu do pagamento antes de escolher como pagar. Seu pedido continua aguardando e vale até ${formatPendingPaymentDeadline(data.paymentPageExpiresAt)}.`
        : 'Seu pedido continua aguardando pagamento. Cartão, PIX ou boleto são escolhidos no ambiente seguro de pagamento.'}</p>
      {paymentError('Seu pedido continua aguardando.')}
      {paymentActions(resumed ? 'Continuar pagamento' : 'Ir para o pagamento', 'Abrindo o pagamento…', 'Desistir')}
    </OrderStatePanel>;
  }

  return (
    <div className="flex w-full max-w-190 flex-col gap-6">
      <header className="flex flex-col gap-1">
        <p className="typo-overline text-primary">Pedido #{data.number}</p>
        <h1 className="typo-h3 text-foreground sm:typo-h2">{data.course.title}</h1>
        <p className="text-base text-muted-foreground">{data.offer.name} · {purchasePrice(data.priceCents)} · criado em {purchaseDay(data.createdAt)}</p>
      </header>

      {panel}

      <section aria-labelledby="order-summary-title" className={cardClassName}>
        <h2 id="order-summary-title" className="typo-h5 text-foreground">Resumo do pedido</h2>
        <dl className="flex flex-col gap-4">
          <SummaryRow label="Curso">{data.course.title}</SummaryRow>
          <SummaryRow label="Opção">{data.offer.name}</SummaryRow>
          <SummaryRow label="Vigência">{purchasePeriod(data.accessPeriod)}</SummaryRow>
          <SummaryRow label="Valor">{purchasePrice(data.priceCents)}</SummaryRow>
          <SummaryRow label="Meio">{data.paymentMethod ? paymentMethods[data.paymentMethod] ?? data.paymentMethod : 'Ainda não escolhido'}</SummaryRow>
          <SummaryRow label="Situação"><OrderStatusBadge status={isPaymentExpired && data.status === 'awaiting-payment' ? 'expired' : data.status} /></SummaryRow>
        </dl>
      </section>

      <Button asChild variant="ghost" className="self-start">
        <Link to={paths.studentMyOrders.getHref()}><ArrowLeft aria-hidden="true" />Meus pedidos</Link>
      </Button>

      <CancelOrderConfirm open={cancelDialogOpen} onOpenChange={openCancelDialog} number={data.number}
        pending={cancel.isPending} failed={cancelFailed} onConfirm={confirmCancel} />
    </div>
  );
};
