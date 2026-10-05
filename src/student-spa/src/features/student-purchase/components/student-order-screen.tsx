import axios from 'axios';
import { CheckCircle, Clock } from 'lucide-react';
import { useEffect, useRef } from 'react';
import { Link } from 'react-router';

import { Alert, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { paths } from '@/config/paths';
import { useOrder } from '@/features/student-purchase/api/get-order';
import { useStartOrderPayment } from '@/features/student-purchase/api/start-order-payment';
import { purchasePeriod, purchasePrice } from '@/features/student-purchase/utils/purchase-labels';
import { useDocumentTitle } from '@/hooks/use-document-title';

type StudentOrderScreenProps = { orderId: string; order: ReturnType<typeof useOrder>; result: string | null; delayed: boolean; lessonId?: string; csrfToken: string };
export const StudentOrderScreen = ({ orderId, order, result, delayed, lessonId, csrfToken }: StudentOrderScreenProps) => {
  useDocumentTitle('Pedido');
  const payment = useStartOrderPayment();
  const courseLink = useRef<HTMLAnchorElement>(null);
  const confirmed = order.data?.status === 'paid' && !!order.data.accessGrantedAt && !!lessonId;
  useEffect(() => { if (confirmed && result === 'concluido') courseLink.current?.focus(); }, [confirmed, result]);
  if (!order.data && order.isPending) return <Skeleton aria-label="Carregando pedido" className="h-64 max-w-[640px]" />;
  if (!order.data) return <Alert variant="destructive"><AlertTitle>{axios.isAxiosError(order.error) && order.error.response?.status === 404 ? 'Pedido não encontrado.' : 'Não foi possível carregar o pedido agora.'}</AlertTitle><Button onClick={() => void order.refetch()}>Tentar de novo</Button></Alert>;
  const data = order.data;
  const following = result === 'concluido' && !confirmed;
  const status = confirmed ? 'Compra confirmada' : data.status === 'paid' ? 'Liberando seu acesso' : following ? 'Confirmando pagamento' : 'Aguardando pagamento';
  return <div className="grid max-w-[640px] gap-6">
    <p className="text-sm text-muted-foreground">PEDIDO #{data.number}</p>
    <h1 className="typo-h2">Pedido nº {data.number}</h1>
    <h2 className="typo-h3">{data.course.title}</h2>
    <div role="status" aria-live="polite" aria-atomic="true"><Alert>{confirmed ? <CheckCircle aria-hidden="true" /> : <Clock aria-hidden="true" />}<AlertTitle>{status}</AlertTitle></Alert></div>
    {following && delayed && <p role="status">A confirmação está demorando um pouco. Você pode voltar a este pedido depois; continuamos acompanhando por aqui.</p>}
    {confirmed && result === 'concluido' && <p>Você receberá o comprovante por e-mail.</p>}
    {confirmed && lessonId && <Button asChild><Link ref={courseLink} to={paths.studentLesson.getHref(lessonId)}>Ir para o curso</Link></Button>}
    {data.status === 'awaiting-payment' && !following && <div className="grid gap-3">
      {payment.isError && <p role="alert">Não foi possível abrir o pagamento agora. Seu pedido continua aguardando.</p>}
      <Button disabled={payment.isPending || !csrfToken} onClick={() => payment.mutate({ orderId, csrfToken })}>
        {payment.isError ? 'Tentar de novo' : result === 'saiu' ? 'Continuar pagamento' : 'Ir para o pagamento'}
      </Button>
    </div>}
    <Card><CardHeader><CardTitle>Resumo do pedido</CardTitle></CardHeader><CardContent className="grid gap-4">
      <p>{data.course.title}</p><p>{data.offer.name}</p><p>{purchasePeriod(data.accessPeriod)}</p><p className="text-xl font-semibold">{purchasePrice(data.priceCents)}</p>
      {data.paymentMethod && <p>Meio de pagamento: {data.paymentMethod === 'card' ? 'Cartão de crédito' : data.paymentMethod}</p>}
      <p>Situação: {data.status === 'paid' ? 'Pago' : 'Aguardando pagamento'}</p>
    </CardContent></Card>
  </div>;
};
