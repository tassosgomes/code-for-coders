import { create } from 'zustand';

type ShellState = {
  menuOpen: boolean;
  theme: 'light' | 'dark' | null;
  toggleTheme: () => void;
  toggleMenu: () => void;
};

export const useShellStore = create<ShellState>((set) => ({
  menuOpen: false,
  theme: null,
  toggleTheme: () => set((state) => ({ theme: (state.theme === 'dark' || (state.theme === null && window.matchMedia?.('(prefers-color-scheme: dark)').matches)) ? 'light' : 'dark' })),
  toggleMenu: () => set((state) => ({ menuOpen: !state.menuOpen })),
}));
