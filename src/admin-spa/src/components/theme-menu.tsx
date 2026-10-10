import { useEffect, useRef, useState } from 'react';
import { Laptop, Moon, Sun } from 'lucide-react';
import { useTheme } from 'next-themes';

const themeOptions = [
  { value: 'light', label: 'Claro', icon: Sun },
  { value: 'dark', label: 'Escuro', icon: Moon },
  { value: 'system', label: 'Sistema', icon: Laptop },
] as const;

export const ThemeMenu = () => {
  const { resolvedTheme, setTheme, theme } = useTheme();
  const [open, setOpen] = useState(false);
  const wrapRef = useRef<HTMLDivElement>(null);
  const TriggerIcon = resolvedTheme === 'dark' ? Moon : Sun;

  useEffect(() => {
    if (!open) return;

    const closeOnOutsideClick = (event: MouseEvent) => {
      if (!wrapRef.current?.contains(event.target as Node)) setOpen(false);
    };
    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === 'Escape') setOpen(false);
    };

    document.addEventListener('mousedown', closeOnOutsideClick);
    document.addEventListener('keydown', closeOnEscape);
    return () => {
      document.removeEventListener('mousedown', closeOnOutsideClick);
      document.removeEventListener('keydown', closeOnEscape);
    };
  }, [open]);

  return (
    <div className="theme-menu-wrap" ref={wrapRef}>
      <button
        aria-expanded={open}
        aria-haspopup="menu"
        aria-label="Escolher tema"
        className="theme-trigger"
        onClick={() => setOpen(!open)}
        type="button"
      >
        <TriggerIcon aria-hidden="true" size={18} />
      </button>
      {open ? (
        <div aria-label="Tema" className="theme-menu" role="menu">
          <span className="theme-menu-label">Tema</span>
          {themeOptions.map((option) => {
            const Icon = option.icon;
            return (
              <button
                aria-checked={theme === option.value}
                key={option.value}
                onClick={() => {
                  setTheme(option.value);
                  setOpen(false);
                }}
                role="menuitemradio"
                type="button"
              >
                <Icon aria-hidden="true" size={16} />
                {option.label}
              </button>
            );
          })}
        </div>
      ) : null}
    </div>
  );
};
