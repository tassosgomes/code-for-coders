import { LoaderCircle, X } from 'lucide-react';

import {
  AlertDialog,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import { Button } from '@/components/ui/button';
import { Sheet, SheetContent, SheetDescription, SheetTitle } from '@/components/ui/sheet';
import { ErrorAlert } from '@/features/student-purchase/components/purchase-alert';
import { useIsMobile } from '@/hooks/use-mobile';

type CancelOrderConfirmProps = {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  number: string;
  pending: boolean;
  failed: boolean;
  onConfirm: () => void;
};

/** O8: AlertDialog no desktop e Sheet inferior no mobile; o foco abre em Manter pedido. */
export const CancelOrderConfirm = ({ open, onOpenChange, number, pending, failed, onConfirm }: CancelOrderConfirmProps) => {
  const isMobile = useIsMobile();
  const title = 'Desistir deste pedido?';
  const description = `O pedido #${number} será cancelado e o pagamento pendente, também. Se você quiser comprar de novo, a compra seguirá as condições da opção naquele momento, que podem ser diferentes das deste pedido.`;
  const error = failed ? <ErrorAlert>Nada mudou. Tente de novo.</ErrorAlert> : null;
  const confirm = <Button variant="destructive" className="w-full sm:w-auto" disabled={pending} onClick={onConfirm}>
    {pending ? <><LoaderCircle aria-hidden="true" className="animate-spin" />Cancelando…</> : 'Desistir do pedido'}
  </Button>;

  if (isMobile) {
    return (
      <Sheet open={open} onOpenChange={onOpenChange}>
        <SheetContent side="bottom" showCloseButton={false} className="gap-4 rounded-t-xl border-0 bg-card p-5">
          <span aria-hidden="true" className="mx-auto h-1 w-10 rounded-sm bg-border" />
          <SheetTitle className="typo-h4 text-foreground">{title}</SheetTitle>
          {error}
          <SheetDescription className="text-sm text-muted-foreground">{description}</SheetDescription>
          <div className="flex flex-col gap-2">
            <Button variant="outline" className="w-full" onClick={() => onOpenChange(false)}>Manter pedido</Button>
            {confirm}
          </div>
        </SheetContent>
      </Sheet>
    );
  }

  return (
    <AlertDialog open={open} onOpenChange={onOpenChange}>
      <AlertDialogContent className="gap-4 rounded-xl bg-card p-6 sm:max-w-110">
        <div className="flex items-center gap-4">
          <AlertDialogTitle className="typo-h4 min-w-0 flex-1 text-xl text-foreground">{title}</AlertDialogTitle>
          <button type="button" aria-label="Fechar" onClick={() => onOpenChange(false)}
            className="rounded-sm text-muted-foreground opacity-70 transition-opacity hover:opacity-100 focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-hidden">
            <X aria-hidden="true" className="size-4" />
          </button>
        </div>
        {error}
        <AlertDialogDescription className="text-sm text-muted-foreground">{description}</AlertDialogDescription>
        <AlertDialogFooter className="gap-3">
          <AlertDialogCancel>Manter pedido</AlertDialogCancel>
          {confirm}
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
};
