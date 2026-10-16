import { Turnstile } from '@marsidev/react-turnstile';
import { useTheme } from 'next-themes';

import { env } from '@/config/env';
import type { CaptchaController } from '@/hooks/use-captcha';

type CaptchaFieldProps = { captcha: CaptchaController };

export const CaptchaField = ({ captcha }: CaptchaFieldProps) => {
  const { onError, onExpire, onSuccess, setWidget, status } = captcha;
  const { resolvedTheme } = useTheme();
  const theme = resolvedTheme === 'dark' || resolvedTheme === 'light' ? resolvedTheme : 'auto';

  return (
    <div className="grid gap-2">
      <div
        aria-label="Verificação de segurança"
        className="flex min-h-[65px] justify-center"
        role="group"
      >
        <Turnstile
          ref={setWidget}
          options={{ language: 'pt-br', theme, refreshExpired: 'auto', size: 'flexible' }}
          siteKey={env.TURNSTILE_SITE_KEY}
          onError={onError}
          onExpire={onExpire}
          onSuccess={onSuccess}
        />
      </div>
      {status === 'error' ? (
        <p className="text-center text-sm text-destructive" role="alert">
          Não foi possível carregar a verificação de segurança. Recarregue a página e tente de novo.
        </p>
      ) : null}
    </div>
  );
};
