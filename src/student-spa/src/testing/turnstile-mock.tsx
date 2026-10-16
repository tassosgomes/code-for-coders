import { useEffect, useImperativeHandle, type Ref } from 'react';

type Callbacks = {
  onSuccess?: (token: string) => void;
  onError?: () => void;
  onExpire?: () => void;
};

type MockTurnstileProps = Callbacks & {
  ref?: Ref<{ reset: () => void }>;
  siteKey: string;
};

type TurnstileMock = {
  token: string;
  autoSolve: boolean;
  resetCount: number;
  siteKey: string;
  callbacks: Callbacks;
  solve: (token?: string) => void;
  expire: () => void;
  fail: () => void;
  reset: () => void;
};

/** Controls the Turnstile widget mock used by every test (see setup.ts). */
export const turnstileMock: TurnstileMock = {
  token: 'test-captcha-token',
  autoSolve: true,
  resetCount: 0,
  siteKey: '',
  callbacks: {},
  solve: (token) => turnstileMock.callbacks.onSuccess?.(token ?? turnstileMock.token),
  expire: () => turnstileMock.callbacks.onExpire?.(),
  fail: () => turnstileMock.callbacks.onError?.(),
  reset: () => {
    turnstileMock.token = 'test-captcha-token';
    turnstileMock.autoSolve = true;
    turnstileMock.resetCount = 0;
    turnstileMock.siteKey = '';
    turnstileMock.callbacks = {};
  },
};

export const MockTurnstile = ({ ref, siteKey, onSuccess, onError, onExpire }: MockTurnstileProps) => {
  useEffect(() => {
    turnstileMock.callbacks = { onSuccess, onError, onExpire };
    turnstileMock.siteKey = siteKey;
  }, [siteKey, onSuccess, onError, onExpire]);

  useImperativeHandle(ref, () => ({
    reset: () => {
      turnstileMock.resetCount += 1;
      if (turnstileMock.autoSolve) {
        queueMicrotask(() => turnstileMock.callbacks.onSuccess?.(turnstileMock.token));
      }
    },
  }), []);

  useEffect(() => {
    // Mimics the widget solving the challenge once on mount.
    if (turnstileMock.autoSolve) {
      onSuccess?.(turnstileMock.token);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return <div data-testid="turnstile-widget" />;
};
