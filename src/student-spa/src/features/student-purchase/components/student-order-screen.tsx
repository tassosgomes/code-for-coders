import axios from 'axios';
import { Clock } from 'lucide-react';

import { Alert, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { useOrder } from '@/features/student-purchase/api/get-order';
import { purchasePeriod, purchasePrice } from '@/features/student-purchase/utils/purchase-labels';
import { useDocumentTitle } from '@/hooks/use-document-title';

type StudentOrderScreenProps = { orderId: string };
export const StudentOrderScreen = ({ orderId }: StudentOrderScreenProps) => {
  useDocumentTitle('Pedido');
  const order = useOrder(orderId);
  if (order.isPending) return <Skeleton aria-label="Carregando pedido" className="h-64 max-w-[640px]" />;
  if (order.isError) return <Alert variant="destructive"><AlertTitle>{axios.isAxiosError(order.error) && order.error.response?.status === 404 ? 'Pedido não encontrado.' : 'Não foi possível carregar o pedido agora.'}</AlertTitle><Button onClick={() => void order.refetch()}>Tentar de novo</Button></Alert>;
  const data = order.data;
  return <div className="grid gap-6"><h1 className="typo-h2">Pedido nº {data.number}</h1><Card className="max-w-[640px]"><CardHeader><CardTitle>{data.course.title}</CardTitle></CardHeader><CardContent className="grid gap-4"><p>{data.offer.name}</p><p>{purchasePeriod(data.accessPeriod)}</p><p className="text-xl font-semibold">{purchasePrice(data.priceCents)}</p><Alert><Clock aria-hidden="true" /><AlertTitle>{data.status === 'awaiting-payment' ? 'Aguardando pagamento' : 'Situação do pedido indisponível'}</AlertTitle></Alert></CardContent></Card></div>;
};
