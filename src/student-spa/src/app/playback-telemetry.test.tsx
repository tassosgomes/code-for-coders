import { metrics, trace } from '@opentelemetry/api';
import {
  AggregationTemporality,
  InMemoryMetricExporter,
  MeterProvider,
  PeriodicExportingMetricReader,
} from '@opentelemetry/sdk-metrics';
import {
  InMemorySpanExporter,
  SimpleSpanProcessor,
} from '@opentelemetry/sdk-trace-base';
import { WebTracerProvider } from '@opentelemetry/sdk-trace-web';
import { fireEvent, screen, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { env } from '@/config/env';
import { ProtectedVideoPlayer } from '@/features/student-lessons/components/protected-video-player';
import { createUrlRedactionSpanProcessor } from '@/lib/telemetry-url-redaction';
import { PLAYBACK_TIME_TO_START_INSTRUMENT, startPlaybackTiming } from '@/lib/telemetry';
import { playbackData } from '@/testing/playback-data';
import { server } from '@/testing/server';
import { lessonId } from '@/testing/student-lesson-data';
import { renderWithProviders } from '@/testing/test-utils';

const hls = vi.hoisted(() => ({
  supported: true,
  loadSource: vi.fn(),
  attachMedia: vi.fn(),
  destroy: vi.fn(),
}));

vi.mock('hls.js', () => ({
  default: class {
    static isSupported = () => hls.supported;
    static Events = { ERROR: 'error' };
    attachMedia = hls.attachMedia;
    loadSource = hls.loadSource;
    destroy = hls.destroy;
    stopLoad = vi.fn();
    on = vi.fn();
  },
}));

const endpoint = env.API_URL + '/api/v1/lessons/:lessonId/playback-sessions';

describe('Playback telemetry', () => {
  let exporter: InMemorySpanExporter;
  let provider: WebTracerProvider;
  let metricExporter: InMemoryMetricExporter;
  let metricReader: PeriodicExportingMetricReader;
  let meterProvider: MeterProvider;

  beforeEach(() => {
    hls.supported = true;
    hls.loadSource.mockClear();
    hls.attachMedia.mockClear();
    hls.destroy.mockClear();
    vi.spyOn(HTMLMediaElement.prototype, 'play').mockResolvedValue();
    vi.spyOn(HTMLMediaElement.prototype, 'pause').mockImplementation(() => undefined);

    exporter = new InMemorySpanExporter();
    provider = new WebTracerProvider({
      spanProcessors: [
        createUrlRedactionSpanProcessor(),
        new SimpleSpanProcessor(exporter),
      ],
    });
    trace.setGlobalTracerProvider(provider);

    metricExporter = new InMemoryMetricExporter(AggregationTemporality.CUMULATIVE);
    metricReader = new PeriodicExportingMetricReader({ exporter: metricExporter });
    meterProvider = new MeterProvider({ readers: [metricReader] });
    metrics.setGlobalMeterProvider(meterProvider);
  });

  afterEach(async () => {
    await provider.shutdown();
    trace.disable();
    await meterProvider.shutdown();
    metrics.disable();
  });

  it('measures playback time-to-start and emits media.playback.time_to_start histogram when first frame arrives', async () => {
    server.use(
      http.post(endpoint, () => HttpResponse.json(playbackData(), { status: 201 })),
    );

    const { container } = renderWithProviders(
      <ProtectedVideoPlayer lessonId={lessonId} csrfToken="csrf-token" />,
    );

    await screen.findByTestId('video-watermark');
    await waitFor(() => expect(hls.loadSource).toHaveBeenCalled());

    const video = container.querySelector('video');
    expect(video).toBeInTheDocument();

    // Trigger loadeddata (first frame available)
    if (video) {
      fireEvent.loadedData(video);
    }

    await provider.forceFlush();
    const spans = exporter.getFinishedSpans();
    const timeSpan = spans.find((span) => span.name === 'playback.time-to-start');
    expect(timeSpan).toBeDefined();
    expect(timeSpan?.duration[0] ?? 0).toBeGreaterThanOrEqual(0);

    // Verify OpenTelemetry metric emission for the panel contract
    const { resourceMetrics } = await metricReader.collect();
    const metric = resourceMetrics.scopeMetrics
      .flatMap((sm) => sm.metrics)
      .find((m) => m.descriptor.name === PLAYBACK_TIME_TO_START_INSTRUMENT);

    expect(metric).toBeDefined();
    expect(metric?.descriptor.unit).toBe('s');
    expect(metric?.dataPoints.length).toBeGreaterThan(0);
    expect(metric?.dataPoints[0]?.attributes).toEqual({});
  });

  it('discards and cancels timing when playback is unmounted before first frame', async () => {
    server.use(
      http.post(endpoint, () => HttpResponse.json(playbackData(), { status: 201 })),
    );

    const { unmount } = renderWithProviders(
      <ProtectedVideoPlayer lessonId={lessonId} csrfToken="csrf-token" />,
    );

    await screen.findByTestId('video-watermark');
    await waitFor(() => expect(hls.loadSource).toHaveBeenCalled());

    // Unmount before loadeddata/playing
    unmount();

    await provider.forceFlush();
    const spans = exporter.getFinishedSpans();
    const abandonedSpan = spans.find((span) => span.name === 'playback.time-to-start');
    expect(abandonedSpan).toBeDefined();
    expect(abandonedSpan?.attributes['playback.abandoned']).toBe(true);

    // Abandoned playback must NOT record into the successful time_to_start histogram
    const { resourceMetrics } = await metricReader.collect();
    const metric = resourceMetrics.scopeMetrics
      .flatMap((sm) => sm.metrics)
      .find((m) => m.descriptor.name === PLAYBACK_TIME_TO_START_INSTRUMENT);

    expect(metric).toBeUndefined();
  });

  it('exported telemetry and metrics never contain the personal test email or student identifiers', async () => {
    const testEmail = 'aluno.especifico.teste@exemplo.com';
    server.use(
      http.post(endpoint, () =>
        HttpResponse.json(
          {
            ...playbackData(),
            watermark: { text: testEmail, repositionSeconds: 30 },
          },
          { status: 201 },
        ),
      ),
    );

    const { container } = renderWithProviders(
      <ProtectedVideoPlayer lessonId={lessonId} csrfToken="csrf-token" />,
    );

    await screen.findByTestId('video-watermark');
    const video = container.querySelector('video');
    if (video) {
      fireEvent.playing(video);
    }

    await provider.forceFlush();
    const exportedSpans = exporter.getFinishedSpans();

    // Stringify all exported span data
    const serializedSpans = JSON.stringify(
      exportedSpans.map(({ name, attributes, events, links }) => ({
        name,
        attributes,
        events,
        links,
      })),
    );

    // Negative check on traces: personal test email must never appear in exported telemetry
    expect(serializedSpans).not.toContain(testEmail);
    expect(serializedSpans).not.toMatch(/student@example\.com/);

    // Verify no span attributes carry forbidden personal keys
    for (const span of exportedSpans) {
      const keys = Object.keys(span.attributes);
      expect(keys).not.toContain('email');
      expect(keys).not.toContain('studentId');
      expect(keys).not.toContain('student_id');
      expect(keys).not.toContain('user');
      expect(keys).not.toContain('courseTitle');
      expect(keys).not.toContain('lessonTitle');
    }

    // Negative check on metrics: collect and verify no personal data in metrics
    const { resourceMetrics } = await metricReader.collect();
    const serializedMetrics = JSON.stringify(resourceMetrics);
    expect(serializedMetrics).not.toContain(testEmail);
    expect(serializedMetrics).not.toMatch(/student@example\.com/);
  });

  it('startPlaybackTiming records failures cleanly without leaking personal data', async () => {
    const handle = startPlaybackTiming();
    handle.fail(new Error('Falha simulada na reprodução'));

    await provider.forceFlush();
    const spans = exporter.getFinishedSpans();
    const failedSpan = spans.find((span) => span.name === 'playback.time-to-start');

    const serialized = JSON.stringify({
      name: failedSpan?.name,
      attributes: failedSpan?.attributes,
      events: failedSpan?.events,
      status: failedSpan?.status,
    });
    expect(serialized).not.toContain('student@example.com');

    // Failed playback must not record successful time_to_start metric
    const { resourceMetrics } = await metricReader.collect();
    const metric = resourceMetrics.scopeMetrics
      .flatMap((sm) => sm.metrics)
      .find((m) => m.descriptor.name === PLAYBACK_TIME_TO_START_INSTRUMENT);

    expect(metric).toBeUndefined();
  });
});
