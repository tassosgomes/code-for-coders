import { useRef, useState, type ReactNode } from 'react';
import axios from 'axios';
import { ArrowLeft, CircleAlert, Info, LoaderCircle } from 'lucide-react';
import { Link, useNavigate } from 'react-router';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { paths } from '@/config/paths';
import { useCreateOrder } from '@/features/student-purchase/api/create-order';
import { usePurchaseSummary } from '@/features/student-purchase/api/get-purchase-summary';
import { useStartOrderPayment } from '@/features/student-purchase/api/start-order-payment';
import { purchaseDate, purchasePeriod, purchasePrice } from '@/features/student-purchase/utils/purchase-labels';
import { useDocumentTitle } from '@/hooks/use-document-title';

const cardClassName = 'flex w-full max-w-160 flex-col gap-4 rounded-xl border border-border bg-card p-5 sm:p-6';
const infoAlertClassName = 'gap-y-1 p-4';
const destructiveAlertClassName = 'gap-y-1 border-destructive bg-destructive/5 p-4';
const alertTitleClassName = 'line-clamp-none font-semibold leading-5 tracking-normal';

const InfoAlert = ({ title, children }: { title?: string; children?: ReactNode }) => (
  <Alert className={infoAlertClassName}>
    <Info aria-hidden="true" className="text-info!" />
    {title ? <AlertTitle className={alertTitleClassName}>{title}</AlertTitle> : null}
    {children ? <AlertDescription className="leading-5">{children}</AlertDescription> : null}
  </Alert>
);

const SummaryRow = ({ label, children, price = false }: { label: string; children: ReactNode; price?: boolean }) => (
  <div className="flex gap-4 text-sm leading-5">
    <dt className="w-21 shrink-0 text-muted-foreground sm:w-35">{label}</dt>
    <dd className={price ? 'typo-h5 min-w-0 flex-1 text-foreground' : 'min-w-0 flex-1 font-medium text-foreground'}>{children}</dd>
  </div>
);

type PurchaseSummaryScreenProps = { offerId: string; csrfToken: string };
export const PurchaseSummaryScreen = ({ offerId, csrfToken }: PurchaseSummaryScreenProps) => {
  useDocumentTitle('Resumo da compra');
  const summary = usePurchaseSummary(offerId);
  const order = useCreateOrder();
  const payment = useStartOrderPayment();
  const navigate = useNavigate();
  const key = useRef<string | undefined>(undefined);
  const [orderId, setOrderId] = useState<string>();
  // G6: o clique cria o pedido (uma chave por tentativa de compra) e abre o pagamento; depois de criado, só reabre o pagamento do mesmo pedido.
  const goToPayment = async () => {
    try {
      let current = orderId;
      if (!current) {
        key.current ??= crypto.randomUUID();
        current = (await order.mutateAsync({ input: { offerId }, idempotencyKey: key.current, csrfToken })).orderId;
        setOrderId(current);
      }
      await payment.mutateAsync({ orderId: current, csrfToken });
    } catch { /* Mutation state renders the public error and retains the order and the retry key. */ }
  };
  // Depois de abrir o pagamento a página sai para o gateway; o botão fica desabilitado até lá.
  const opening = order.isPending || payment.isPending || payment.isSuccess;
  const unavailable = [summary.error, order.error].some((error) => axios.isAxiosError(error) && error.response?.status === 404);
  const data = summary.data;
  const access = data?.existingAccessChecked ? data.existingAccess : null;
  const origin = access?.origin === 'courtesy' ? 'cortesia' : access?.origin === 'purchase' ? 'compra' : 'acesso existente';
  const backToCourse = data ? <Button asChild variant="ghost" className="w-full sm:w-auto">
    <Link to={paths.studentShowcaseCourse.getHref(data.course.courseId)}><ArrowLeft aria-hidden="true" />Voltar ao curso</Link>
  </Button> : null;

  let body: ReactNode = null;
  if (summary.isPending) {
    body = <div aria-label="Carregando resumo" className={cardClassName}>
      <Skeleton className="h-4 w-50 max-w-full rounded-lg bg-secondary" />
      <Skeleton className="h-7 w-80 max-w-full rounded-lg bg-secondary" />
      {[1, 2, 3].map((line) => <Skeleton key={line} className="h-4 w-full rounded-lg bg-secondary" />)}
      <Skeleton className="h-10 w-40 rounded-lg bg-secondary" />
    </div>;
  } else if (unavailable) {
    body = <div className="flex w-full max-w-160 flex-col items-center gap-4">
      <div className="flex w-full flex-col items-center gap-4 rounded-xl border border-border bg-card px-8 py-10 text-center">
        <span className="flex size-12 items-center justify-center rounded-lg bg-secondary text-primary"><CircleAlert aria-hidden="true" className="size-6" /></span>
        <h2 className="typo-h4 text-foreground">Esta opção não está mais disponível</h2>
        <p className="text-sm text-muted-foreground">A escola retirou esta opção de compra. Nenhum pedido foi criado e você não foi cobrado.</p>
      </div>
      <div className="flex flex-wrap justify-center gap-3">
        <Button asChild><Link to={paths.studentShowcase.getHref()}>Ver todos os cursos</Link></Button>
        <Button variant="ghost" onClick={() => void navigate(-1)}>Voltar</Button>
      </div>
    </div>;
  } else if (summary.isError) {
    body = <Alert variant="destructive" className={`max-w-160 ${destructiveAlertClassName}`}>
      <CircleAlert aria-hidden="true" />
      <AlertTitle className={alertTitleClassName}>Não foi possível carregar o resumo agora.</AlertTitle>
      <AlertDescription><Button size="sm" variant="outline" className="mt-1 text-xs text-foreground" onClick={() => void summary.refetch()}>Tentar de novo</Button></AlertDescription>
    </Alert>;
  } else if (data?.pendingOrderId) {
    body = <section aria-label="Resumo da compra" className={cardClassName}>
      <h2 className="typo-h4 text-foreground sm:typo-h3">{data.course.title}</h2>
      <InfoAlert title="Você já tem um pedido aguardando pagamento desta opção.">O pedido mantém o preço e a vigência de quando foi criado.</InfoAlert>
      <div className="flex flex-col-reverse gap-4 sm:flex-row sm:items-center sm:justify-between">
        {backToCourse}
        <Button asChild className="w-full sm:w-auto"><Link to={paths.studentOrder.getHref(data.pendingOrderId)}>Ver pedido pendente</Link></Button>
      </div>
    </section>;
  } else if (data) {
    body = <section aria-label="Resumo da compra" className={cardClassName}>
      <p className="typo-overline text-muted-foreground">Curso</p>
      <h2 className="typo-h4 text-foreground sm:typo-h3">{data.course.title}</h2>
      <dl className="flex flex-col gap-4 border-y border-border py-4">
        <SummaryRow label="Opção de acesso">{data.offer.name}</SummaryRow>
        <SummaryRow label="Vigência">{purchasePeriod(data.offer.accessPeriod)}</SummaryRow>
        <SummaryRow label="Preço" price>{purchasePrice(data.offer.priceCents)}</SummaryRow>
      </dl>
      {access ? <InfoAlert title={access.validity.type === 'lifetime' ? `Você já tem acesso vitalício a este curso (${origin}).` : `Você já tem acesso a este curso até ${purchaseDate(access.validity.endsOn)} (${origin}).`}>
        Você pode comprar mesmo assim: a nova compra não altera o acesso que você tem.
      </InfoAlert> : null}
      {order.isError ? <Alert variant="destructive" className={destructiveAlertClassName}>
        <CircleAlert aria-hidden="true" />
        <AlertTitle className={alertTitleClassName}>Não foi possível confirmar a compra agora. Tente de novo.</AlertTitle>
      </Alert> : null}
      {payment.isError && orderId ? <Alert variant="destructive" className={destructiveAlertClassName}>
        <CircleAlert aria-hidden="true" />
        <AlertTitle className={alertTitleClassName}>Não foi possível abrir o pagamento agora.</AlertTitle>
        <AlertDescription className="leading-5">
          <p>Seu pedido foi criado e continua aguardando pagamento. Você pode tentar de novo ou voltar mais tarde em Meus pedidos.</p>
          <div className="mt-1 flex flex-wrap items-center gap-3">
            <Button size="sm" variant="outline" className="text-xs text-foreground" disabled={opening} onClick={() => void goToPayment()}>Tentar de novo</Button>
            <Button asChild size="sm" variant="link" className="px-0 text-xs"><Link to={paths.studentOrder.getHref(orderId)}>Ver meu pedido</Link></Button>
          </div>
        </AlertDescription>
      </Alert> : null}
      <InfoAlert>Você será levado ao ambiente seguro de pagamento. Cartão, PIX ou boleto são escolhidos lá.</InfoAlert>
      <div className="flex flex-col-reverse gap-4 sm:flex-row sm:items-center sm:justify-between">
        {backToCourse}
        <Button className="w-full sm:w-auto" disabled={opening} onClick={() => void goToPayment()}>
          {opening ? <><LoaderCircle aria-hidden="true" className="animate-spin" />Abrindo o pagamento…</> : 'Ir para o pagamento'}
        </Button>
      </div>
    </section>;
  }

  return <div className="flex flex-col gap-5 sm:gap-6">
    <header className="flex flex-col gap-1"><p className="typo-overline text-primary">Compra</p><h1 className="typo-h2">Resumo da compra</h1></header>
    {body}
  </div>;
};
