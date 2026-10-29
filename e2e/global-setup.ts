import { access } from 'node:fs/promises';

import { env } from './support/env';
import { seedScenario } from './support/seed';

/**
 * Monta o cenário (admin, curso com vídeo pronto, oferta) uma vez por execução. Com E2E_REUSE_SEED=1 reaproveita
 * o cenário da execução anterior (iteração local contra um ambiente compartilhado, sem acumular cursos).
 */
export default async function globalSetup() {
  if (process.env.E2E_REUSE_SEED === '1') {
    try {
      await access(env.stateFile);
      console.log('[e2e-seed] reaproveitando o cenário existente');
      return;
    } catch {
      // Sem cenário anterior: monta um novo.
    }
  }
  await seedScenario();
}
