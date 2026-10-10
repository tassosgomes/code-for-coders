import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';

const readStyle = (name: string) => readFileSync(new URL(name, import.meta.url), 'utf8');
const withoutComments = (css: string) => css.replace(/\/\*[\s\S]*?\*\//g, '');
// Sombras usam os canais de --shadow-rgb e overlays usam --overlay: não são cores literais.
const withoutTokenColors = (css: string) => css.replace(/rgb\(var\(--shadow-rgb\)[^)]*\)/g, '');
const colorLiteral = /#[0-9a-fA-F]{3,8}\b|\brgba?\(|\bhsla?\(/;

const themeCss = withoutComments(readStyle('./theme.css'));
const [lightBlock = '', darkBlock = ''] = themeCss.split(':root.dark');
const tokenNames = (block: string) => new Set([...block.matchAll(/(--[\w-]+)\s*:/g)].map((match) => match[1] ?? ''));

describe('tokens de tema', () => {
  it.each(['./app.css', './authoring.css'])('%s não define cor literal fora de theme.css', (file) => {
    const offending = withoutTokenColors(withoutComments(readStyle(file)))
      .split('\n')
      .filter((line) => colorLiteral.test(line));

    expect(offending).toEqual([]);
  });

  it('toda variável de cor usada existe em theme.css', () => {
    const defined = tokenNames(themeCss);
    const used = new Set<string>();
    for (const file of ['./app.css', './authoring.css']) {
      for (const match of withoutComments(readStyle(file)).matchAll(/var\((--[\w-]+)/g)) {
        used.add(match[1] ?? '');
      }
    }

    expect([...used].filter((name) => !defined.has(name))).toEqual([]);
  });

  it('o tema escuro redefine todo token de interface definido no claro', () => {
    const light = [...tokenNames(lightBlock)].filter((name) => !name.startsWith('--code-'));
    const dark = tokenNames(darkBlock);

    expect(light.filter((name) => !dark.has(name))).toEqual([]);
    expect([...dark].filter((name) => !tokenNames(lightBlock).has(name))).toEqual([]);
  });
});
