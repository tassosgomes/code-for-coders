import * as z from 'zod';

const EnvSchema = z.object({
  API_URL: z.url(),
  OTEL_ENDPOINT: z.url().optional(),
  TURNSTILE_SITE_KEY: z.string().min(1),
});

declare global {
  // Declaration merging is required to extend the DOM Window interface.
  // eslint-disable-next-line @typescript-eslint/consistent-type-definitions
  interface Window {
    RUNTIME_ENV?: Record<string, string | undefined>;
  }
}

const devFallbacks = import.meta.env.DEV ? {
      API_URL: 'http://localhost:8080',
      // Sitekey de teste oficial da Cloudflare: sempre aprova (ADR-0023).
      TURNSTILE_SITE_KEY: '1x00000000000000000000AA',
    } : {};

const createEnv = () => {
  const raw = Object.fromEntries(
    Object.entries(window.RUNTIME_ENV ?? {}).filter(
      ([, value]) => value && !value.startsWith('${'),
    ),
  );

  const parsed = EnvSchema.safeParse({ ...devFallbacks, ...raw });

  if (!parsed.success) {
    throw new Error(`Invalid runtime configuration:\n${z.prettifyError(parsed.error)}`);
  }

  return parsed.data;
};

export const env = createEnv();
