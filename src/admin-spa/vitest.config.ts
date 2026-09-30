import path from 'node:path';
import { fileURLToPath } from 'node:url';

import react from '@vitejs/plugin-react-swc';
import { defineConfig } from 'vitest/config';

const sourceDirectory = path.dirname(fileURLToPath(import.meta.url));

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      '@': path.resolve(sourceDirectory, './src'),
    },
  },
  test: {
    environment: 'jsdom',
    setupFiles: './src/testing/setup.ts',
    // Cenários de autoria encadeiam dezenas de interações userEvent com MSW e levam ~2 s isolados
    // (~5 s sob carga de CPU: medido também no HEAD anterior à task 5.0, com workers em paralelo
    // e o runner compartilhado). O padrão de 5 s gerava timeouts intermitentes sem defeito de código.
    testTimeout: 15_000,
    coverage: {
      provider: 'v8',
      reporter: ['text', 'json-summary'],
      exclude: ['src/assets/**', 'src/testing/**', 'src/types/**'],
    },
  },
});
