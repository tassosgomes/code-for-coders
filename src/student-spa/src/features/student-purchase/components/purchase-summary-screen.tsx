import { useRef } from 'react';
import axios from 'axios';
import { Info } from 'lucide-react';
import { Link, useNavigate } from 'react-router';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { paths } from '@/config/paths';
import { useCreateOrder } from '@/features/student-purchase/api/create-order';
import { usePurchaseSummary } from '@/features/student-purchase/api/get-purchase-summary';
import { purchaseDate, purchasePeriod, purchasePrice } from '@/features/student-purchase/utils/purchase-labels';
import { useDocumentTitle } from '@/hooks/use-document-title';

type PurchaseSummaryScreenProps = { offerId: string; csrfToken: string };
export const PurchaseSummaryScreen = ({ offerId, csrfToken }: PurchaseSummaryScreenProps) => {
  useDocumentTitle('Resumo da compra');
  const summary = usePurchaseSummary(offerId);
  const order = useCreateOrder();
  const navigate = useNavigate();
  const key = useRef<string | undefined>(undefined);
  const confirm = async () => {
    key.current ??= crypto.randomUUID();
    try {
      const created = await order.mutateAsync({ input: { offerId }, idempotencyKey: key.current, csrfToken });
      await navigate(paths.studentOrder.getHref(created.orderId));
    } catch { /* Mutation state renders the public error and retains the retry key. */ }
  };
  const unavailable = [summary.error, order.error].some((error) => axios.isAxiosError(error) && error.response?.status === 404);
  const data = summary.data;
  const access = data?.existingAccessChecked ? data.existingAccess : null;
  const origin = access?.origin === 'courtesy' ? 'cortesia' : access?.origin === 'purchase' ? 'compra' : 'acesso existente';
  return <div className="grid gap-6">
    <div><p className="typo-overline text-primary">Compra</p><h1 className="typo-h2">Resumo da compra</h1></div>
    {summary.isPending ? <Card className="max-w-[640px]" aria-label="Carregando resumo"><CardContent className="grid gap-4 p-6"><Skeleton className="h-8 w-2/3" /><Skeleton className="h-24 w-full" /><Skeleton className="h-10 w-full" /></CardContent></Card> : unavailable ?
      <Card className="max-w-[640px]"><CardHeader><CardTitle>Esta opção não está mais disponível</CardTitle></CardHeader><CardContent><p>Nenhum pedido foi criado e você não foi cobrado.</p><Button asChild className="mt-4"><Link to={paths.studentShowcase.getHref()}>Ver todos os cursos</Link></Button></CardContent></Card> : summary.isError ?
      <Alert variant="destructive"><AlertTitle>Não foi possível carregar o resumo agora.</AlertTitle><Button onClick={() => void summary.refetch()}>Tentar de novo</Button></Alert> : data ?
      <Card className="max-w-[640px]"><CardHeader><p className="text-sm text-muted-foreground">Curso</p><CardTitle>{data.course.title}</CardTitle></CardHeader><CardContent className="grid gap-6">
        {data.pendingOrderId ? <><Alert><Info aria-hidden="true" /><AlertTitle>Você já tem um pedido aguardando pagamento desta opção.</AlertTitle><AlertDescription>O pedido mantém o preço e a vigência de quando foi criado.</AlertDescription></Alert><Button asChild><Link to={paths.studentOrder.getHref(data.pendingOrderId)}>Ver pedido pendente</Link></Button></> : <>
          <dl className="grid gap-3 border-y border-border py-4"><div><dt className="text-sm text-muted-foreground">Opção de acesso</dt><dd>{data.offer.name}</dd></div><div><dt className="text-sm text-muted-foreground">Vigência</dt><dd>{purchasePeriod(data.offer.accessPeriod)}</dd></div><div><dt className="text-sm text-muted-foreground">Preço</dt><dd className="text-xl font-semibold">{purchasePrice(data.offer.priceCents)}</dd></div></dl>
          {access ? <Alert><Info aria-hidden="true" /><AlertTitle>{access.validity.type === 'lifetime' ? `Você já tem acesso vitalício a este curso (${origin}).` : `Você já tem acesso a este curso até ${purchaseDate(access.validity.endsOn)} (${origin}).`}</AlertTitle><AlertDescription>Você pode comprar mesmo assim: a nova compra não altera o acesso que você tem.</AlertDescription></Alert> : null}
          <Alert><Info aria-hidden="true" /><AlertDescription>Você será levado ao ambiente seguro de pagamento. Cartão, PIX ou boleto são escolhidos lá.</AlertDescription></Alert>
          {order.isError ? <Alert variant="destructive"><AlertTitle>Não foi possível confirmar a compra agora. Tente de novo.</AlertTitle></Alert> : null}
          <Button disabled={order.isPending} onClick={() => void confirm()}>{order.isPending ? 'Confirmando…' : 'Confirmar compra'}</Button>
        </>}
        <Button asChild variant="link"><Link to={paths.studentShowcaseCourse.getHref(data.course.courseId)}>Voltar ao curso</Link></Button>
      </CardContent></Card> : null}
  </div>;
};
