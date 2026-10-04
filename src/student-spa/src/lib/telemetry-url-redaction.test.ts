import {
  InMemorySpanExporter,
  SimpleSpanProcessor,
} from '@opentelemetry/sdk-trace-base';
import { WebTracerProvider } from '@opentelemetry/sdk-trace-web';
import { describe, expect, it } from 'vitest';

import {
  createUrlRedactionSpanProcessor,
  redactSensitiveUrl,
  registerTelemetrySecret,
} from './telemetry-url-redaction';

const resetLink = 'http://localhost:8082/student/redefinir-senha?token=reset-secret&step=1#top';
const confirmationLink = 'http://localhost:8082/student/confirm-account?token=confirm-secret';

describe('telemetry URL redaction', () => {
  it('redacts the token query parameter and keeps the rest of the URL', () => {
    expect(redactSensitiveUrl(resetLink)).toBe(
      'http://localhost:8082/student/redefinir-senha?token=REDACTED&step=1#top',
    );
    expect(redactSensitiveUrl(confirmationLink)).toBe(
      'http://localhost:8082/student/confirm-account?token=REDACTED',
    );
    expect(redactSensitiveUrl('?step=1&Token=abc')).toBe('?step=1&Token=REDACTED');
    expect(redactSensitiveUrl('token=abc')).toBe('token=REDACTED');
    expect(redactSensitiveUrl('http://localhost:8082/student/?csrftoken=kept')).toBe(
      'http://localhost:8082/student/?csrftoken=kept',
    );
  });

  it('removes link secrets from span, event and link attributes before export', async () => {
    const exporter = new InMemorySpanExporter();
    const provider = new WebTracerProvider({
      spanProcessors: [createUrlRedactionSpanProcessor(), new SimpleSpanProcessor(exporter)],
    });
    const tracer = provider.getTracer('telemetry-url-redaction-test');

    const parent = tracer.startSpan('documentFetch', { attributes: { 'http.url': resetLink } });
    const span = tracer.startSpan('documentLoad', {
      attributes: { 'url.query': 'token=reset-secret' },
      links: [{ context: parent.spanContext(), attributes: { 'url.full': confirmationLink } }],
    });
    // document-load sets url.full after the span starts.
    span.setAttribute('url.full', resetLink);
    span.setAttribute('http.status_code', 200);
    span.setAttribute('resource.urls', [confirmationLink, 'plain']);
    span.addEvent('navigation', { 'url.full': confirmationLink });
    span.end();
    parent.end();
    await provider.forceFlush();

    const exported = exporter.getFinishedSpans();
    expect(exported).toHaveLength(2);
    expect(JSON.stringify(exported.map(({ attributes, events, links }) => ({ attributes, events, links }))))
      .not.toMatch(/reset-secret|confirm-secret/);

    const documentLoad = exported.find(({ name }) => name === 'documentLoad');
    expect(documentLoad?.attributes).toMatchObject({
      'url.full': 'http://localhost:8082/student/redefinir-senha?token=REDACTED&step=1#top',
      'url.query': 'token=REDACTED',
      'http.status_code': 200,
      'resource.urls': ['http://localhost:8082/student/confirm-account?token=REDACTED', 'plain'],
    });
    expect(documentLoad?.events[0]?.attributes?.['url.full']).toBe(
      'http://localhost:8082/student/confirm-account?token=REDACTED',
    );

    await provider.shutdown();
  });
});

describe('Playback secret redaction', () => {
  it.each([
    'opaque=signature-secret&expires=1999999999',
    'https://edge.test/segment.ts?opaque=signature-secret&expires=1999999999',
    'signature-secret',
    'opaque%3Dsignature-secret%26expires%3D1999999999',
  ])('removes registered provider-independent secrets from %s', (value) => {
    registerTelemetrySecret('opaque=signature-secret&expires=1999999999', Date.now() + 300_000);
    expect(redactSensitiveUrl(value)).not.toContain('signature-secret');
  });
  it('removes student email from telemetry attributes', () => {
    expect(redactSensitiveUrl('student@example.com')).toBe('REDACTED');
  });
  it('removes opaque credentials from exported span events links and arrays', async () => {
    const query = 'opaque=export-signature-secret&expires=1999999999';
    registerTelemetrySecret(query, Date.now() + 300_000);
    const exporter = new InMemorySpanExporter();
    const provider = new WebTracerProvider({ spanProcessors: [createUrlRedactionSpanProcessor(), new SimpleSpanProcessor(exporter)] });
    const parent = provider.getTracer('playback').startSpan('parent');
    const span = provider.getTracer('playback').startSpan('segment', {
      attributes: { query, values: ['export-signature-secret', encodeURIComponent(query)] },
      links: [{ context: parent.spanContext(), attributes: { query } }],
    });
    span.addEvent('request', { query }); span.end(); parent.end(); await provider.forceFlush();
    expect(JSON.stringify(exporter.getFinishedSpans().map(({ attributes, events, links }) => ({ attributes, events, links })))).not.toContain('export-signature-secret');
    await provider.shutdown();
  });
});
