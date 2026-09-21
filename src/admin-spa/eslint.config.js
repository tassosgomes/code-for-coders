import fs from 'node:fs';

import js from '@eslint/js';
import checkFile from 'eslint-plugin-check-file';
import { importX } from 'eslint-plugin-import-x';
import reactHooks from 'eslint-plugin-react-hooks';
import globals from 'globals';
import tseslint from 'typescript-eslint';

const featureZones = fs
  .readdirSync('./src/features', { withFileTypes: true })
  .filter((entry) => entry.isDirectory())
  .map((entry) => ({
    target: `./src/features/${entry.name}`,
    from: './src/features',
    except: [`./${entry.name}`],
    message: 'Features do not import one another. Compose them in src/app.',
  }));

export default tseslint.config(
  { ignores: ['dist', 'coverage', 'playwright-report'] },
  js.configs.recommended,
  ...tseslint.configs.recommended,
  reactHooks.configs.flat['recommended-latest'],
  {
    files: ['**/*.{ts,tsx}'],
    languageOptions: {
      ecmaVersion: 'latest',
      sourceType: 'module',
      globals: globals.browser,
    },
    plugins: { 'import-x': importX, 'check-file': checkFile },
    settings: {
      'import-x/resolver': { typescript: true },
    },
    rules: {
      'import-x/no-restricted-paths': [
        'error',
        {
          zones: [
            ...featureZones,
            {
              target: './src/features',
              from: './src/app',
              message: 'Features do not know the composition layer.',
            },
            {
              target: [
                './src/components',
                './src/hooks',
                './src/lib',
                './src/stores',
                './src/types',
                './src/utils',
              ],
              from: ['./src/features', './src/app'],
              message: 'Shared code cannot depend on features or app.',
            },
          ],
        },
      ],
      'import-x/no-cycle': 'error',
      'check-file/filename-naming-convention': [
        'error',
        { '**/*.{ts,tsx}': 'KEBAB_CASE' },
        { ignoreMiddleExtensions: true },
      ],
      'check-file/folder-naming-convention': [
        'error',
        { 'src/**/!(__tests__)': 'KEBAB_CASE' },
      ],
      '@typescript-eslint/no-explicit-any': 'error',
      '@typescript-eslint/consistent-type-definitions': ['error', 'type'],
      'no-restricted-imports': [
        'error',
        {
          patterns: [
            {
              group: ['@/features/*/index', '@/features/*/index.ts'],
              message: 'Import feature files directly; do not add barrels.',
            },
          ],
        },
      ],
    },
  },
);
