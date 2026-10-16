import { useCallback, useState } from 'react';
import type { TurnstileInstance } from '@marsidev/react-turnstile';

export type CaptchaStatus = 'pending' | 'solved' | 'error';

/**
 * Holds the Turnstile token of one form. The token is single use, so call `reset()` after every
 * submission (success or failure) and whenever it expires.
 */
export const useCaptcha = () => {
  const [widget, setWidgetState] = useState<TurnstileInstance | null>(null);
  const [token, setToken] = useState<string | null>(null);
  const [status, setStatus] = useState<CaptchaStatus>('pending');

  const setWidget = useCallback((instance: TurnstileInstance | null | undefined) => {
    setWidgetState(instance ?? null);
  }, []);

  const onSuccess = useCallback((value: string) => {
    setToken(value);
    setStatus('solved');
  }, []);

  const onExpire = useCallback(() => {
    setToken(null);
    setStatus('pending');
  }, []);

  const onError = useCallback(() => {
    setToken(null);
    setStatus('error');
  }, []);

  const reset = useCallback(() => {
    setToken(null);
    setStatus('pending');
    widget?.reset();
  }, [widget]);

  return { setWidget, token, status, reset, onSuccess, onExpire, onError };
};

export type CaptchaController = ReturnType<typeof useCaptcha>;
