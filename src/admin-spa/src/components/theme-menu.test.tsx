import { ThemeProvider } from 'next-themes';
import { cleanup, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { ThemeMenu } from '@/components/theme-menu';
import { renderWithProviders } from '@/testing/test-utils';

const stubSystemTheme = (prefersDark: boolean) => {
  vi.stubGlobal('matchMedia', (query: string) => ({
    matches: query.includes('light') ? !prefersDark : prefersDark,
    media: query,
    onchange: null,
    addListener: vi.fn(),
    removeListener: vi.fn(),
    addEventListener: vi.fn(),
    removeEventListener: vi.fn(),
    dispatchEvent: vi.fn(),
  }));
};

const renderThemeMenu = () => renderWithProviders(
  <ThemeProvider attribute="class" defaultTheme="system" enableSystem disableTransitionOnChange>
    <ThemeMenu />
  </ThemeProvider>,
);

const openMenu = async () => {
  await userEvent.click(screen.getByRole('button', { name: 'Escolher tema' }));
};

describe('ThemeMenu', () => {
  beforeEach(() => {
    stubSystemTheme(false);
  });

  afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
    window.localStorage.clear();
    document.documentElement.className = '';
  });

  it('oferece Claro, Escuro e Sistema, com Sistema marcado por padrão', async () => {
    renderThemeMenu();
    await openMenu();

    expect(screen.getByRole('menuitemradio', { name: 'Claro' })).toHaveAttribute('aria-checked', 'false');
    expect(screen.getByRole('menuitemradio', { name: 'Escuro' })).toHaveAttribute('aria-checked', 'false');
    expect(screen.getByRole('menuitemradio', { name: 'Sistema' })).toHaveAttribute('aria-checked', 'true');
  });

  it('aplica o escuro ao html e guarda a escolha no navegador', async () => {
    renderThemeMenu();
    await openMenu();
    await userEvent.click(screen.getByRole('menuitemradio', { name: 'Escuro' }));

    await waitFor(() => expect(document.documentElement).toHaveClass('dark'));
    expect(window.localStorage.getItem('theme')).toBe('dark');
    expect(screen.queryByRole('menu')).not.toBeInTheDocument();
  });

  it('com Sistema segue a preferência do navegador', async () => {
    stubSystemTheme(true);
    renderThemeMenu();

    await waitFor(() => expect(document.documentElement).toHaveClass('dark'));
  });

  it('com Claro força o claro mesmo quando o sistema está escuro', async () => {
    stubSystemTheme(true);
    renderThemeMenu();
    await openMenu();
    await userEvent.click(screen.getByRole('menuitemradio', { name: 'Claro' }));

    await waitFor(() => expect(document.documentElement).toHaveClass('light'));
    expect(document.documentElement).not.toHaveClass('dark');
  });

  it('fecha com Escape sem alterar o tema', async () => {
    renderThemeMenu();
    await openMenu();
    await userEvent.keyboard('{Escape}');

    expect(screen.queryByRole('menu')).not.toBeInTheDocument();
    expect(window.localStorage.getItem('theme')).toBeNull();
  });
});
