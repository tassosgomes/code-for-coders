import { useEffect, useImperativeHandle, type Ref } from 'react';
import { act } from '@testing-library/react';

export const turnstileTestToken = 'test-captcha-token';

type MockProps = {
  siteKey: string;
  options?: { theme?: string; language?: string; size?: string; refreshExpired?: string };
  onSuccess?: (token: string) => void;
  onExpire?: () => void;
  onError?: () => void;
  ref?: Ref<{ reset: () => void }>;
};

type TurnstileMockState = {
  props: Omit<MockProps, 'ref'> | null;
  resetCount: number;
  mountCount: number;
};

export const turnstileMock: TurnstileMockState = { props: null, resetCount: 0, mountCount: 0 };

export const resetTurnstileMock = () => {
  turnstileMock.props = null;
  turnstileMock.resetCount = 0;
  turnstileMock.mountCount = 0;
};

/** Simula o widget resolvendo o desafio. */
export const solveTurnstile = (token = turnstileTestToken) => {
  act(() => turnstileMock.props?.onSuccess?.(token));
};

/** Simula a expiração do token pelo widget. */
export const expireTurnstile = () => {
  act(() => turnstileMock.props?.onExpire?.());
};

/** Simula uma falha do widget. */
export const failTurnstile = () => {
  act(() => turnstileMock.props?.onError?.());
};

/**
 * Substitui o widget real nos testes: resolve sozinho ao montar (como o modo Managed sem desafio
 * visível) e, depois de um reset, só resolve de novo quando o teste chamar solveTurnstile().
 */
export const Turnstile = ({ ref, ...props }: MockProps) => {
  useImperativeHandle(ref, () => ({
    reset: () => {
      turnstileMock.resetCount += 1;
    },
  }));

  // Guarda as props mais recentes para os helpers dispararem os callbacks do componente.
  useEffect(() => {
    turnstileMock.props = props;
  });

  useEffect(() => {
    turnstileMock.mountCount += 1;
    props.onSuccess?.(turnstileTestToken);
    // Só ao montar: reproduz a primeira resolução do widget.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return <div data-testid="turnstile-widget" data-theme={props.options?.theme} />;
};
