import { Link, Outlet } from 'react-router';

import { paths } from '@/config/paths';
import { useDocumentTitle } from '@/hooks/use-document-title';
import { useShellStore } from '@/stores/use-shell-store';
import type { StaffArea } from '@/types/staff-area';

type AppShellProps = {
  serviceName: string;
  title: string;
  areas: readonly StaffArea[];
  outletContext: unknown;
  isLoggingOut: boolean;
  logoutError: string | null;
  onLogout: () => void;
};

export const AppShell = ({
  serviceName,
  title,
  areas,
  outletContext,
  isLoggingOut,
  logoutError,
  onLogout,
}: AppShellProps) => {
  useDocumentTitle(title);
  const menuOpen = useShellStore((state) => state.menuOpen);
  const toggleMenu = useShellStore((state) => state.toggleMenu);

  return (
    <div className="app-shell">
      <header className="app-header">
        <Link className="brand" to={paths.home.getHref()}>
          {serviceName}
        </Link>
        <button
          aria-expanded={menuOpen}
          aria-label="Toggle navigation"
          className="menu-button"
          type="button"
          onClick={toggleMenu}
        >
          {menuOpen ? 'Close menu' : 'Open menu'}
        </button>
        <button disabled={isLoggingOut} type="button" onClick={onLogout}>
          {isLoggingOut ? 'Saindo…' : 'Sair'}
        </button>
      </header>
      {logoutError ? <p role="alert">{logoutError}</p> : null}
      {menuOpen ? (
        <nav aria-label={`${serviceName} navigation`} className="app-nav">
          <Link to={paths.home.getHref()}>Início</Link>
          {areas.map((area) => area.href
            ? <Link key={area.permission} to={area.href}>{area.label}</Link>
            : <span key={area.permission}>{area.label}</span>)}
        </nav>
      ) : null}
      <Outlet context={outletContext} />
    </div>
  );
};
