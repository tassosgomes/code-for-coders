import axios from 'axios';

const formatWait = (seconds: number) => {
  if (seconds < 60) {
    return `${seconds} ${seconds === 1 ? 'segundo' : 'segundos'}`;
  }

  const minutes = Math.ceil(seconds / 60);
  return `${minutes} ${minutes === 1 ? 'minuto' : 'minutos'}`;
};

/**
 * Message for the errors added by the anonymous-endpoint protection (ADR-0023), or undefined when
 * the error is something else and the caller should use its own message.
 */
export const getCaptchaErrorMessage = (error: unknown): string | undefined => {
  if (!axios.isAxiosError(error) || !error.response) {
    return undefined;
  }

  const data: unknown = error.response.data;
  const code = typeof data === 'object' && data !== null && 'code' in data && typeof data.code === 'string'
    ? data.code
    : undefined;

  if (code === 'CAPTCHA_REQUIRED') {
    return 'Confirme que você não é um robô para continuar.';
  }

  if (code === 'CAPTCHA_INVALID') {
    return 'Não conseguimos confirmar que você não é um robô. Tente de novo.';
  }

  if (code === 'CAPTCHA_UNAVAILABLE') {
    return 'A verificação de segurança está indisponível no momento. Tente de novo em instantes.';
  }

  if (code === 'RATE_LIMITED' || error.response.status === 429) {
    const retryAfter = Number(error.response.headers['retry-after']);
    return Number.isFinite(retryAfter) && retryAfter > 0
      ? `Muitas tentativas. Aguarde ${formatWait(Math.ceil(retryAfter))} e tente de novo.`
      : 'Muitas tentativas. Aguarde um pouco e tente de novo.';
  }

  return undefined;
};
