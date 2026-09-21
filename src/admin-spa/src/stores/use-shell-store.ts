import { create } from 'zustand';

type ShellState = {
  menuOpen: boolean;
  toggleMenu: () => void;
};

export const useShellStore = create<ShellState>((set) => ({
  menuOpen: false,
  toggleMenu: () => set((state) => ({ menuOpen: !state.menuOpen })),
}));
