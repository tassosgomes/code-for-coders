import { context, trace, SpanStatusCode } from '@opentelemetry/api';
import { getWebAutoInstrumentations } from '@opentelemetry/auto-instrumentations-web';
import { ZoneContextManager } from '@opentelemetry/context-zone';
import { OTLPTraceExporter } from '@opentelemetry/exporter-trace-otlp-http';
import { registerInstrumentations } from '@opentelemetry/instrumentation';
import { resourceFromAttributes } from '@opentelemetry/resources';
import { BatchSpanProcessor } from '@opentelemetry/sdk-trace-base';
import { WebTracerProvider } from '@opentelemetry/sdk-trace-web';
import {
  ATTR_SERVICE_NAME,
  ATTR_SERVICE_VERSION,
} from '@opentelemetry/semantic-conventions';

import { env } from '@/config/env';
import { createUrlRedactionSpanProcessor } from '@/lib/telemetry-url-redaction';

let initialized = false;

const recordUnhandledError = (error: unknown) => {
  const tracer = trace.getTracer('student-spa');
  const span = tracer.startSpan('frontend.unhandled-error', undefined, context.active());
  const normalizedError = error instanceof Error ? error : new Error(String(error));

  span.recordException(normalizedError);
  span.setStatus({ code: SpanStatusCode.ERROR });
  span.end();
};

export const initTelemetry = () => {
  if (initialized || !env.OTEL_ENDPOINT) return;

  initialized = true;
  const provider = new WebTracerProvider({
    resource: resourceFromAttributes({
      [ATTR_SERVICE_NAME]: 'student-spa',
      [ATTR_SERVICE_VERSION]: __APP_VERSION__,
      'deployment.environment.name': import.meta.env.MODE,
    }),
    spanProcessors: [
      createUrlRedactionSpanProcessor(),
      new BatchSpanProcessor(new OTLPTraceExporter({ url: env.OTEL_ENDPOINT }), {
        maxQueueSize: 100,
        maxExportBatchSize: 10,
        scheduledDelayMillis: 5000,
      }),
    ],
  });

  provider.register({ contextManager: new ZoneContextManager() });

  registerInstrumentations({
    instrumentations: [
      getWebAutoInstrumentations({
        '@opentelemetry/instrumentation-fetch': {
          // Enables W3C traceparent propagation to the configured API origin.
          propagateTraceHeaderCorsUrls: [new RegExp(env.API_URL)],
          clearTimingResources: true,
        },
        '@opentelemetry/instrumentation-xml-http-request': {
          propagateTraceHeaderCorsUrls: [new RegExp(env.API_URL)],
        },
        '@opentelemetry/instrumentation-user-interaction': {
          eventNames: ['click', 'submit'],
        },
      }),
    ],
  });

  window.addEventListener('error', (event) => recordUnhandledError(event.error));
  window.addEventListener('unhandledrejection', (event) => recordUnhandledError(event.reason));
};
