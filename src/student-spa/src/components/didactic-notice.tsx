import { TriangleAlert } from 'lucide-react';

import { env } from '@/config/env';

type DidacticNoticeProps = {
  variant?: 'banner' | 'inline';
};

export const DidacticNotice = ({ variant = 'inline' }: DidacticNoticeProps) => {
  if (!env.DIDACTIC_NOTICE_ENABLED) return null;

  return (
    <aside
      aria-label="Ambiente didático e de demonstração"
      className={`bg-warning-soft text-warning-soft-foreground ${variant === 'banner' ? 'border-t border-border' : 'mb-6 rounded-lg border border-border'}`}
      role="note"
    >
      <div className={`flex items-start gap-3 ${variant === 'banner' ? 'mx-auto w-full max-w-292 px-4 py-3 sm:px-6' : 'p-4'}`}>
        <TriangleAlert aria-hidden="true" className="mt-0.5 size-5 shrink-0" />
        <div className="min-w-0 text-sm leading-6">
          <p className="font-semibold">Ambiente didático e de demonstração</p>
          <p>Esta aplicação é utilizada para fins educacionais. Não informe dados pessoais, senhas ou informações de pagamento reais. Utilize apenas dados fictícios.</p>
        </div>
      </div>
    </aside>
  );
};
