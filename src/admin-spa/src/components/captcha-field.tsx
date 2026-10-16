import { useImperativeHandle, useRef, type Ref } from 'react';
import { Turnstile, type TurnstileInstance } from '@marsidev/react-turnstile';
import { useTheme } from 'next-themes';

import { env } from '@/config/env';

export type CaptchaFieldHandle = {
  reset: () => void;
};

type CaptchaFieldProps = {
  /** Recebe o token quando o widget resolve e null quando ele expira, falha ou é reiniciado. */
  onTokenChange: (token: string | null) => void;
  ref?: Ref<CaptchaFieldHandle>;
};

export const CaptchaField = ({ onTokenChange, ref }: CaptchaFieldProps) => {
  const { resolvedTheme } = useTheme();
  const widgetRef = useRef<TurnstileInstance>(null);
  const theme = resolvedTheme === 'dark' ? 'dark' : 'light';

  const reset = () => {
    onTokenChange(null);
    widgetRef.current?.reset();
  };

  useImperativeHandle(ref, () => ({ reset }));

  return (
    <div aria-label="Verificação de segurança" className="captcha-field" role="group">
      <Turnstile
        // O tema só é lido ao renderizar o widget; a troca de tema remonta o iframe.
        key={theme}
        onError={() => onTokenChange(null)}
        onExpire={reset}
        onSuccess={onTokenChange}
        options={{ language: 'pt-BR', refreshExpired: 'manual', size: 'flexible', theme }}
        ref={widgetRef}
        siteKey={env.TURNSTILE_SITE_KEY}
      />
    </div>
  );
};
