import axios from 'axios';

export const CAPTCHA_HEADER = 'X-Captcha-Token';

export const captchaHeaders = (token: string): Record<string, string> => ({ [CAPTCHA_HEADER]: token });

const formatWait = (seconds: number) => {
  if (seconds < 60) return `${seconds} ${seconds === 1 ? 'segundo' : 'segundos'}`;
  const minutes = Math.ceil(seconds / 60);
  return `${minutes} ${minutes === 1 ? 'minuto' : 'minutos'}`;
};

const readRetryAfterSeconds = (headers: unknown): number | null => {
  if (!headers || typeof headers !== 'object') return null;
  const getter = (headers as { get?: (name: string) => unknown }).get;
  const raw = typeof getter === 'function'
    ? getter.call(headers, 'retry-after')
    : (headers as Record<string, unknown>)['retry-after'];
  const seconds = Number(raw);
  return Number.isFinite(seconds) && seconds > 0 ? Math.ceil(seconds) : null;
};

/**
 * Mensagem pt-BR para os erros do captcha e do limite por IP (ADR-0023).
 * Devolve null quando o erro não é um desses, para o chamador aplicar o tratamento próprio.
 */
export const getCaptchaErrorMessage = (error: unknown): string | null => {
  if (!axios.isAxiosError(error) || !error.response) return null;
  const { status, data, headers } = error.response;
  const code = (data as { code?: unknown } | undefined)?.code;

  if (code === 'CAPTCHA_REQUIRED') return 'Confirme que você não é um robô para continuar.';
  if (code === 'CAPTCHA_INVALID') return 'Não conseguimos confirmar que você não é um robô. Tente de novo.';
  if (code === 'CAPTCHA_UNAVAILABLE') {
    return 'A verificação de segurança está indisponível no momento. Tente novamente em instantes.';
  }
  if (code === 'RATE_LIMITED' || status === 429) {
    const seconds = readRetryAfterSeconds(headers);
    return seconds
      ? `Muitas tentativas seguidas. Tente de novo em ${formatWait(seconds)}.`
      : 'Muitas tentativas seguidas. Aguarde um pouco e tente de novo.';
  }
  return null;
};
