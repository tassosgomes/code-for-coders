import axios from 'axios';
import { AlertCircle, CheckCircle, Clock, XCircle } from 'lucide-react';
import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router';

import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import { Alert, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { paths } from '@/config/paths';
import { useCancelOrder } from '@/features/student-purchase/api/cancel-order';
import { useOrder } from '@/features/student-purchase/api/get-order';
import { useStartOrderPayment } from '@/features/student-purchase/api/start-order-payment';
import { formatPendingPaymentDeadline, purchasePeriod, purchasePrice } from '@/features/student-purchase/utils/purchase-labels';
import { useDocumentTitle } from '@/hooks/use-document-title';

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

  if (!order.data && order.isPending) return <Skeleton aria-label="Carregando pedido" className="h-64 max-w-[640px]" />;
  if (!order.data) {
    return (
      <Alert variant="destructive">
        <AlertTitle>
          {axios.isAxiosError(order.error) && order.error.response?.status === 404
            ? 'Pedido não encontrado.'
            : 'Não foi possível carregar o pedido agora.'}
        </AlertTitle>
        <Button onClick={() => void order.refetch()}>Tentar de novo</Button>
      </Alert>
    );
  }

  const data = order.data;
  const isPaymentExpired = axios.isAxiosError(payment.error) &&
    (payment.error.response?.data as { code?: string } | undefined)?.code === 'ORDER_NOT_PAYABLE';
  const isCancelled = data.status === 'cancelled';
  const isExpired = data.status === 'expired' || isPaymentExpired;
  const following = result === 'concluido' && !confirmed;
  const pendingPayment = data.status === 'awaiting-payment' ? data.pendingPayment : null;

  const status = confirmed
    ? 'Compra confirmada'
    : isCancelled
      ? 'Pedido cancelado'
      : isExpired
        ? 'Pedido expirado'
        : data.status === 'paid'
          ? 'Liberando seu acesso'
          : following
            ? 'Confirmando pagamento'
            : pendingPayment?.method === 'pix'
              ? 'Aguardando pagamento do PIX'
              : pendingPayment?.method === 'boleto'
                ? 'Aguardando pagamento do boleto'
                : 'Aguardando pagamento';

  const paymentMethodLabel = data.paymentMethod === 'card'
    ? 'Cartão de crédito'
    : data.paymentMethod === 'pix'
      ? 'PIX'
      : data.paymentMethod === 'boleto'
        ? 'Boleto'
        : data.paymentMethod;

  const actionButtonText = payment.isError
    ? 'Tentar de novo'
    : pendingPayment?.method === 'pix'
      ? 'Ver código PIX'
      : pendingPayment?.method === 'boleto'
        ? 'Ver boleto'
        : result === 'saiu'
          ? 'Continuar pagamento'
          : 'Ir para o pagamento';

  const cancelLabel = pendingPayment
    ? 'Desistir e pagar de outra forma'
    : 'Desistir do pedido';

  const paymentActions = (
    <>
      {payment.isError && <p role="alert">Não foi possível abrir o pagamento agora. Seu pedido continua aguardando.</p>}
      {cancel.isError && <p role="alert">Não foi possível cancelar o pedido agora.</p>}
      <div className="flex flex-wrap gap-3">
        <Button disabled={payment.isPending || !csrfToken} onClick={() => payment.mutate({ orderId, csrfToken })}>
          {actionButtonText}
        </Button>
        <Button
          variant="outline"
          disabled={cancel.isPending || !csrfToken}
          onClick={() => setCancelDialogOpen(true)}
        >
          {cancelLabel}
        </Button>
      </div>
    </>
  );

  return (
    <div className="grid max-w-[640px] gap-6">
      <p className="text-sm text-muted-foreground">PEDIDO #{data.number}</p>
      <h1 className="typo-h2">Pedido nº {data.number}</h1>
      <h2 className="typo-h3">{data.course.title}</h2>
      <div role="status" aria-live="polite" aria-atomic="true">
        <Alert>
          {confirmed ? (
            <CheckCircle aria-hidden="true" />
          ) : isCancelled ? (
            <XCircle aria-hidden="true" />
          ) : isExpired ? (
            <AlertCircle aria-hidden="true" />
          ) : (
            <Clock aria-hidden="true" />
          )}
          <AlertTitle>{status}</AlertTitle>
        </Alert>
      </div>

      {following && delayed && (
        <p role="status">
          A confirmação está demorando um pouco. Você pode voltar a este pedido depois; continuamos acompanhando por aqui.
        </p>
      )}
      {confirmed && result === 'concluido' && <p>Você receberá o comprovante por e-mail.</p>}
      {confirmed && lessonId && (
        <Button asChild>
          <Link ref={courseLink} to={paths.studentLesson.getHref(lessonId)}>
            Ir para o curso
          </Link>
        </Button>
      )}

      {isCancelled && (
        <div className="grid gap-3">
          <p>Este pedido foi cancelado. Você pode realizar uma nova compra pelas condições vigentes da oferta.</p>
          <Button asChild>
            <Link to={paths.studentPurchase.getHref(data.offer.offerId, data.course.courseId)}>
              Comprar de novo
            </Link>
          </Button>
        </div>
      )}

      {isExpired && !isCancelled && (
        <div className="grid gap-3">
          <p>O prazo de pagamento deste pedido venceu. Você pode comprar de novo pelas condições vigentes da oferta.</p>
          <Button asChild>
            <Link to={paths.studentPurchase.getHref(data.offer.offerId, data.course.courseId)}>
              Comprar de novo
            </Link>
          </Button>
        </div>
      )}

      {!isCancelled && !isExpired && pendingPayment && !following && (
        <div className="grid gap-3">
          {pendingPayment.method === 'pix' && (
            <p>
              Pague o PIX até {formatPendingPaymentDeadline(pendingPayment.expiresAt)}. O acesso será liberado sozinho assim que o pagamento for confirmado. O curso ainda não está liberado.
            </p>
          )}
          {pendingPayment.method === 'boleto' && (
            <p>
              Pague o boleto até {formatPendingPaymentDeadline(pendingPayment.expiresAt)}. O acesso a {data.course.title} será liberado sozinho quando o pagamento for compensado, o que pode levar até 3 dias úteis. O curso ainda não está liberado.
            </p>
          )}
          {paymentActions}
        </div>
      )}

      {!isCancelled && !isExpired && data.status === 'awaiting-payment' && !pendingPayment && !following && (
        <div className="grid gap-3">
          {paymentActions}
        </div>
      )}

      <AlertDialog open={cancelDialogOpen} onOpenChange={setCancelDialogOpen}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Desistir do pedido?</AlertDialogTitle>
            <AlertDialogDescription>
              Seu pedido atual será cancelado e você poderá escolher outro meio de pagamento ou realizar uma nova compra na oferta.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Voltar</AlertDialogCancel>
            <AlertDialogAction
              onClick={() => {
                cancel.mutate({ orderId, csrfToken });
              }}
            >
              Sim, desistir
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      <Card>
        <CardHeader>
          <CardTitle>Resumo do pedido</CardTitle>
        </CardHeader>
        <CardContent className="grid gap-4">
          <p>{data.course.title}</p>
          <p>{data.offer.name}</p>
          <p>{purchasePeriod(data.accessPeriod)}</p>
          <p className="text-xl font-semibold">{purchasePrice(data.priceCents)}</p>
          {data.paymentMethod && <p>Meio de pagamento: {paymentMethodLabel}</p>}
          <p>
            Situação:{' '}
            {data.status === 'paid'
              ? 'Pago'
              : data.status === 'cancelled'
                ? 'Cancelado'
                : data.status === 'expired'
                  ? 'Expirado'
                  : 'Aguardando pagamento'}
          </p>
        </CardContent>
      </Card>
      <Link className="text-sm underline underline-offset-4" to={paths.studentMyOrders.getHref()}>← Meus pedidos</Link>
    </div>
  );
};
