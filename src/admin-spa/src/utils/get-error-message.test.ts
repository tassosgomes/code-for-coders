import { describe, expect, it } from 'vitest';

import { getErrorMessage } from './get-error-message';

describe('getErrorMessage', () => {
  it('returns a non-empty Error message', () => {
    expect(getErrorMessage(new Error('request failed'), 'fallback')).toBe('request failed');
  });

  it('uses the fallback for empty or unknown errors', () => {
    expect(getErrorMessage(new Error('   '), 'fallback')).toBe('fallback');
    expect(getErrorMessage({ message: 'request failed' }, 'fallback')).toBe('fallback');
  });
});
