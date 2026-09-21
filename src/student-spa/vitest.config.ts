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
    coverage: {
      provider: 'v8',
      reporter: ['text', 'json-summary'],
      exclude: ['src/assets/**', 'src/testing/**', 'src/types/**'],
    },
  },
});
