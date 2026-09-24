import type { Attributes } from '@opentelemetry/api';
import type { ReadableSpan, SpanProcessor } from '@opentelemetry/sdk-trace-base';

// Confirmation and password reset links carry a one-time secret in the `token` query parameter.
const SENSITIVE_QUERY_PARAMETER = /(^|[?&#])(token)=[^&#\s]*/gi;

export const REDACTED_VALUE = 'REDACTED';

export const redactSensitiveUrl = (value: string) =>
  value.replace(SENSITIVE_QUERY_PARAMETER, `$1$2=${REDACTED_VALUE}`);

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
