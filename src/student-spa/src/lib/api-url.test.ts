import { describe, expect, it } from 'vitest';

import { joinApiUrl } from '@/lib/api-url';

describe('joinApiUrl', () => {
  it('keeps the path prefix of the API base', () => {
    expect(joinApiUrl('https://dev-code4coders.tasso.dev.br/students', '/api/v1/playback-sessions/s1/playlist'))
      .toBe('https://dev-code4coders.tasso.dev.br/students/api/v1/playback-sessions/s1/playlist');
  });

  it('works with a base that has no prefix', () => {
    expect(joinApiUrl('https://c4c-student.example.com', '/api/v1/playback-sessions/s1/progress'))
      .toBe('https://c4c-student.example.com/api/v1/playback-sessions/s1/progress');
  });

  it('ignores trailing slashes on the base', () => {
    expect(joinApiUrl('https://host/students///', '/api/v1/x')).toBe('https://host/students/api/v1/x');
  });
});
