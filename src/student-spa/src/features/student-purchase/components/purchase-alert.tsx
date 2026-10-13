import type { ReactNode } from 'react';
import { CircleAlert, Info } from 'lucide-react';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { cn } from 'cn';

const titleClassName = 'line-clamp-none font-semibold leading-5 tracking-normal';

type PurchaseAlertProps = { title?: string; children?: ReactNode; className?: string };

/** Alert · info do Figma da compra: ícone info, título semibold opcional e texto muted. */
export const InfoAlert = ({ title, children, className }: PurchaseAlertProps) => (
  <Alert className={cn('gap-y-1 p-4', className)}>
    <Info aria-hidden="true" className="text-info!" />
    {title ? <AlertTitle className={titleClassName}>{title}</AlertTitle> : null}
    {children ? <AlertDescription className="leading-5">{children}</AlertDescription> : null}
  </Alert>
);

/** Alert · destructive do Figma da compra: fundo vermelho suave, borda destrutiva e ações abaixo do texto. */
export const ErrorAlert = ({ title, children, className }: PurchaseAlertProps) => (
  <Alert variant="destructive" className={cn('gap-y-1 border-destructive bg-destructive/5 p-4', className)}>
    <CircleAlert aria-hidden="true" />
    {title ? <AlertTitle className={titleClassName}>{title}</AlertTitle> : null}
    {children ? <AlertDescription className="leading-5">{children}</AlertDescription> : null}
  </Alert>
);

/** Ações dentro de um alerta (Tentar de novo outline sm, links). */
export const AlertActions = ({ children }: { children: ReactNode }) => <div className="mt-1 flex flex-wrap items-center gap-3">{children}</div>;
