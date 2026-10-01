import { Dialog } from 'radix-ui';
import { X } from 'lucide-react';
import { useRef } from 'react';

import { Button } from '@/components/ui/button';
import { useRegisterPurchaseIntent } from '@/features/student-showcase/api/register-purchase-intent';

type PurchaseIntentButtonProps = { offerId: string; offerName: string };

export const PurchaseIntentButton = ({ offerId, offerName }: PurchaseIntentButtonProps) => {
  const intent = useRegisterPurchaseIntent();
  const acknowledge = useRef<HTMLButtonElement>(null);
  return (
    <Dialog.Root>
      <Dialog.Trigger asChild>
        <Button aria-label={`Comprar ${offerName}`} onClick={() => intent.mutate({ offerId, idempotencyKey: crypto.randomUUID() })} type="button">
          Comprar
        </Button>
      </Dialog.Trigger>
      <Dialog.Portal>
        <Dialog.Overlay className="fixed inset-0 z-40 bg-black/50" />
        <Dialog.Content
          className="fixed inset-x-0 bottom-0 z-50 rounded-t-xl border border-border bg-background p-6 shadow-xl sm:inset-x-auto sm:bottom-auto sm:left-1/2 sm:top-1/2 sm:w-full sm:max-w-[440px] sm:-translate-x-1/2 sm:-translate-y-1/2 sm:rounded-xl"
          onOpenAutoFocus={(event) => { event.preventDefault(); acknowledge.current?.focus(); }}
        >
          <Dialog.Title className="pr-6 font-heading text-xl font-semibold">A compra estará disponível em breve</Dialog.Title>
          <Dialog.Description className="mt-3 text-muted-foreground">
            Ainda estamos preparando o pagamento deste curso. Volte em breve para concluir a sua compra.
            {' '}Não pedimos nenhum dado seu agora.
          </Dialog.Description>
          <Dialog.Close asChild><Button aria-label="Fechar" className="absolute right-2 top-2" size="icon" variant="ghost"><X aria-hidden="true" /></Button></Dialog.Close>
          <div className="mt-6 flex justify-end"><Dialog.Close asChild><Button ref={acknowledge}>Entendi</Button></Dialog.Close></div>
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  );
};
