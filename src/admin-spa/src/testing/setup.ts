import '@testing-library/jest-dom/vitest';

import { configure } from '@testing-library/react';
import { afterAll, afterEach, beforeAll, vi } from 'vitest';

import { server } from '@/testing/server';
import { resetTurnstileMock } from '@/testing/turnstile-mock';

// O widget real carrega o script da Cloudflare; os testes usam um mock que resolve com token fixo.
vi.mock('@marsidev/react-turnstile', () => import('@/testing/turnstile-mock'));

// O padrão de 1 s de findBy*/waitFor estoura quando os workers do vitest competem por CPU com o
// restante do runner (medido: load ~13 em 12 núcleos, também no HEAD anterior à task 5.0). Sem defeito
// de código por trás: os mesmos cenários passam em <1 s isolados. O timeout de cada teste fica
// em vitest.config.ts.
configure({ asyncUtilTimeout: 5_000 });

// next-themes consulta matchMedia ao montar o ThemeProvider; o jsdom não o implementa.
if (typeof window !== 'undefined' && !window.matchMedia) {
  window.matchMedia = (query: string) => ({
    matches: false,
    media: query,
    onchange: null,
    addListener: () => undefined,
    removeListener: () => undefined,
    addEventListener: () => undefined,
    removeEventListener: () => undefined,
    dispatchEvent: () => false,
  });
}

beforeAll(() => server.listen({ onUnhandledRequest: 'error' }));
afterEach(() => {
  server.resetHandlers();
  resetTurnstileMock();
});
afterAll(() => server.close());
