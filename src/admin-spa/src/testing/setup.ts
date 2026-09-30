import '@testing-library/jest-dom/vitest';

import { configure } from '@testing-library/react';
import { afterAll, afterEach, beforeAll } from 'vitest';

import { server } from '@/testing/server';

// O padrão de 1 s de findBy*/waitFor estoura quando os workers do vitest competem por CPU com o
// restante do runner (medido: load ~13 em 12 núcleos, também no HEAD anterior à task 5.0). Sem defeito
// de código por trás: os mesmos cenários passam em <1 s isolados. O timeout de cada teste fica
// em vitest.config.ts.
configure({ asyncUtilTimeout: 5_000 });

beforeAll(() => server.listen({ onUnhandledRequest: 'error' }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());
