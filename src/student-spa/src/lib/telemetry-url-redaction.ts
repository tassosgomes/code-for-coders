import type { Attributes } from '@opentelemetry/api';
import type { ReadableSpan, SpanProcessor } from '@opentelemetry/sdk-trace-base';

// Confirmation and password reset links carry a one-time secret in the `token` query parameter.
const SENSITIVE_QUERY_PARAMETER = /(^|[?&#])(token|st|Policy|Signature|Key-Pair-Id)=[^&#\s]*/gi;
const secrets = new Set<string>();

export const registerTelemetrySecret = (query: string, expiresAt: number) => {
  if (!Number.isFinite(expiresAt)) throw new Error('Playback credential expiry is invalid.');
  for (const value of [query, ...new URLSearchParams(query.replace(/^\?/, '')).values()]) {
    if (value.length > 4) { secrets.add(value); secrets.add(encodeURIComponent(value)); }
  }
};

export const REDACTED_VALUE = 'REDACTED';

export const redactSensitiveUrl = (value: string) => {
  let safe = value;
  for (const secret of secrets) {
    safe = safe.replaceAll(secret, REDACTED_VALUE);
  }
  return safe.replace(SENSITIVE_QUERY_PARAMETER, (_match, prefix: string, name: string) => prefix + name + '=' + REDACTED_VALUE)
    .replace(/[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}/gi, REDACTED_VALUE);
};

const redactAttributes = (attributes: Attributes | undefined) => {
  if (!attributes) return;

  for (const [key, value] of Object.entries(attributes)) {
    if (typeof value === 'string') {
      attributes[key] = redactSensitiveUrl(value);
    } else if (Array.isArray(value)) {
      attributes[key] = value.map((item) =>
        typeof item === 'string' ? redactSensitiveUrl(item) : item,
      ) as typeof value;
    }
  }
};

/**
 * Removes link secrets from every span attribute before any exporter sees the span.
 * Auto-instrumentations (document-load, user-interaction, fetch) record `url.full`/`http.url`
 * from `location.href`, which can still hold the token before the screen strips it.
 * Register it before the exporting processor.
 */
export const createUrlRedactionSpanProcessor = (): SpanProcessor => ({
  onStart: () => undefined,
  onEnd: (span: ReadableSpan) => {
    redactAttributes(span.attributes);
    span.events.forEach((event) => redactAttributes(event.attributes));
    span.links.forEach((link) => redactAttributes(link.attributes));
  },
  forceFlush: () => Promise.resolve(),
  shutdown: () => Promise.resolve(),
});
