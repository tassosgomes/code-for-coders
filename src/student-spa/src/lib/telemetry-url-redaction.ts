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

const isEmailLocal = (code: number) => (code >= 48 && code <= 57) || (code >= 65 && code <= 90) || (code >= 97 && code <= 122)
  || code === 46 || code === 95 || code === 37 || code === 43 || code === 45;
const isDomainLabel = (code: number) => (code >= 48 && code <= 57) || (code >= 65 && code <= 90) || (code >= 97 && code <= 122) || code === 45;
const isLetter = (code: number) => (code >= 65 && code <= 90) || (code >= 97 && code <= 122);

const redactEmails = (value: string) => {
  let result = '';
  let index = 0;
  while (index < value.length) {
    const at = value.indexOf('@', index);
    if (at === -1) { result += value.slice(index); break; }
    if (at === index) { result += value[index]; index += 1; continue; }
    let start = at;
    while (start > index && isEmailLocal(value.charCodeAt(start - 1))) start -= 1;
    if (start === at) { result += value.slice(index, at + 1); index = at + 1; continue; }
    let cursor = at + 1;
    let lastDot = -1;
    let labelLength = 0;
    while (cursor < value.length) {
      const code = value.charCodeAt(cursor);
      if (isDomainLabel(code)) { labelLength += 1; cursor += 1; continue; }
      if (code === 46 && labelLength > 0) { lastDot = cursor; labelLength = 0; cursor += 1; continue; }
      break;
    }
    const tldStart = lastDot + 1;
    let tldLetters = lastDot > at && cursor - tldStart >= 2;
    for (let tld = tldStart; tldLetters && tld < cursor; tld += 1) tldLetters = isLetter(value.charCodeAt(tld));
    if (!tldLetters) { result += value.slice(index, at + 1); index = at + 1; continue; }
    result += value.slice(index, start) + REDACTED_VALUE;
    index = cursor;
  }
  return result;
};

export const redactSensitiveUrl = (value: string) => {
  let safe = value;
  for (const secret of secrets) {
    safe = safe.replaceAll(secret, REDACTED_VALUE);
  }
  return redactEmails(safe.replace(SENSITIVE_QUERY_PARAMETER, (_match, prefix: string, name: string) => prefix + name + '=' + REDACTED_VALUE));
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
