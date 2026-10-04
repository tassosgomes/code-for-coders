import { context, metrics, trace, SpanStatusCode } from '@opentelemetry/api';
import { getWebAutoInstrumentations } from '@opentelemetry/auto-instrumentations-web';
import { ZoneContextManager } from '@opentelemetry/context-zone';
import { OTLPMetricExporter } from '@opentelemetry/exporter-metrics-otlp-http';
import { OTLPTraceExporter } from '@opentelemetry/exporter-trace-otlp-http';
import { registerInstrumentations } from '@opentelemetry/instrumentation';
import { resourceFromAttributes } from '@opentelemetry/resources';
import { MeterProvider, PeriodicExportingMetricReader } from '@opentelemetry/sdk-metrics';
import { BatchSpanProcessor } from '@opentelemetry/sdk-trace-base';
import { WebTracerProvider } from '@opentelemetry/sdk-trace-web';
import {
  ATTR_SERVICE_NAME,
  ATTR_SERVICE_VERSION,
} from '@opentelemetry/semantic-conventions';

import { env } from '@/config/env';
import { createUrlRedactionSpanProcessor } from '@/lib/telemetry-url-redaction';

let initialized = false;

export const PLAYBACK_TIME_TO_START_INSTRUMENT = 'media.playback.time_to_start';

export const getPlaybackTimeToStartHistogram = () => {
  const meter = metrics.getMeter('student-spa');
  return meter.createHistogram(PLAYBACK_TIME_TO_START_INSTRUMENT, {
    description: 'Tempo até o vídeo começar (da abertura da aula ao primeiro quadro)',
    unit: 's',
  });
};

const recordUnhandledError = (error: unknown) => {
  const tracer = trace.getTracer('student-spa');
  const span = tracer.startSpan('frontend.unhandled-error', undefined, context.active());
  const normalizedError = error instanceof Error ? error : new Error(String(error));

  span.recordException(normalizedError);
  span.setStatus({ code: SpanStatusCode.ERROR });
  span.end();
};

export type PlaybackTimingHandle = {
  end: () => void;
  fail: (error?: unknown) => void;
  cancel: () => void;
};

export const startPlaybackTiming = (): PlaybackTimingHandle => {
  const tracer = trace.getTracer('student-spa');
  const span = tracer.startSpan('playback.time-to-start');
  const startTime = performance.now();
  let finished = false;

  return {
    end: () => {
      if (finished) return;
      finished = true;
      const durationSeconds = Math.max(0, (performance.now() - startTime) / 1000);
      getPlaybackTimeToStartHistogram().record(durationSeconds);
      span.end();
    },
    fail: (error?: unknown) => {
      if (finished) return;
      finished = true;
      if (error) {
        const normalized = error instanceof Error ? error : new Error(String(error));
        span.recordException(normalized);
        span.setStatus({ code: SpanStatusCode.ERROR });
      }
      span.end();
    },
    cancel: () => {
      if (finished) return;
      finished = true;
      span.setAttribute('playback.abandoned', true);
      span.end();
    },
  };
};

export const initTelemetry = () => {
  if (initialized || !env.OTEL_ENDPOINT) return;

  initialized = true;
  const resource = resourceFromAttributes({
    [ATTR_SERVICE_NAME]: 'student-spa',
    [ATTR_SERVICE_VERSION]: __APP_VERSION__,
    'deployment.environment.name': import.meta.env.MODE,
  });

  const provider = new WebTracerProvider({
    resource,
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

  const metricsUrl = env.OTEL_ENDPOINT.endsWith('/v1/traces')
    ? env.OTEL_ENDPOINT.replace(/\/v1\/traces$/, '/v1/metrics')
    : env.OTEL_ENDPOINT.replace(/\/+$/, '') + '/v1/metrics';

  const meterProvider = new MeterProvider({
    resource,
    readers: [
      new PeriodicExportingMetricReader({
        exporter: new OTLPMetricExporter({ url: metricsUrl }),
        exportIntervalMillis: 5000,
      }),
    ],
  });

  metrics.setGlobalMeterProvider(meterProvider);

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
