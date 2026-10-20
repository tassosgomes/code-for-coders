import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const originalRuntimeEnv = window.RUNTIME_ENV;

beforeEach(() => {
  vi.resetModules();
  window.RUNTIME_ENV = {
    API_URL: 'https://example.com/students',
    TURNSTILE_SITE_KEY: 'test-site-key',
  };
});

afterEach(() => {
  window.RUNTIME_ENV = originalRuntimeEnv;
  vi.resetModules();
});

describe('didactic notice runtime configuration', () => {
  it.each([
    [undefined, true],
    ['', true],
    ['${DIDACTIC_NOTICE_ENABLED}', true],
    ['true', true],
    ['false', false],
  ])('interprets runtime value %s as %s', async (value, expected) => {
    window.RUNTIME_ENV = { ...window.RUNTIME_ENV, DIDACTIC_NOTICE_ENABLED: value };

    const { env } = await import('@/config/env');

    expect(env.DIDACTIC_NOTICE_ENABLED).toBe(expected);
  });

  it.each(['0', '1', 'FALSE', 'invalid'])('rejects invalid runtime value %s', async (value) => {
    window.RUNTIME_ENV = { ...window.RUNTIME_ENV, DIDACTIC_NOTICE_ENABLED: value };

    await expect(import('@/config/env')).rejects.toThrow('DIDACTIC_NOTICE_ENABLED');
  });
});
