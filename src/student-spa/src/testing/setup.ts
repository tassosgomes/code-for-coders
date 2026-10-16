import '@testing-library/jest-dom/vitest';

import { cleanup } from '@testing-library/react';
import { afterAll, afterEach, beforeAll, vi } from 'vitest';

import { server } from '@/testing/server';
import { turnstileMock } from '@/testing/turnstile-mock';

vi.mock('@marsidev/react-turnstile', async () => {
  const { MockTurnstile } = await import('@/testing/turnstile-mock');
  return { Turnstile: MockTurnstile };
});

Object.defineProperty(window, 'matchMedia', {
  writable: true,
  value: vi.fn().mockImplementation((query: string) => ({
    matches: false,
    media: query,
    onchange: null,
    addListener: vi.fn(),
    removeListener: vi.fn(),
    addEventListener: vi.fn(),
    removeEventListener: vi.fn(),
    dispatchEvent: vi.fn(),
  })),
});

beforeAll(() => server.listen({ onUnhandledRequest: 'error' }));
afterEach(() => {
  turnstileMock.reset();
  cleanup();
  server.resetHandlers();
});
afterAll(() => server.close());
