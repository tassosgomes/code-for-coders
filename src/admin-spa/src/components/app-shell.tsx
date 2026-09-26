import { useState } from 'react';
import { CodeXml, ChevronDown, House, Menu, Users, Wallet, X } from 'lucide-react';
import { Link, NavLink, Outlet } from 'react-router';

import { paths } from '@/config/paths';
import { useDocumentTitle } from '@/hooks/use-document-title';
import { useShellStore } from '@/stores/use-shell-store';
import type { StaffArea } from '@/types/staff-area';

type AppShellProps = {
  serviceName: string;
  title: string;
  areas: readonly StaffArea[];
  name: string;
  roles: readonly string[];
  outletContext: unknown;
  isLoggingOut: boolean;
  logoutError: string | null;
  onLogout: () => void;
};

export const AppShell = ({ serviceName, title, areas, name, roles, outletContext, isLoggingOut, logoutError, onLogout }: AppShellProps) => {
  useDocumentTitle(title);
  const menuOpen = useShellStore((state) => state.menuOpen);
  const toggleMenu = useShellStore((state) => state.toggleMenu);
  const [accountOpen, setAccountOpen] = useState(false);
  const initials = name.split(/\s+/).slice(0, 2).map((part) => part[0]).join('').toUpperCase();

  return (
    <div className="app-shell">
      <aside className={`app-sidebar ${menuOpen ? 'is-open' : ''}`}>
        <Link aria-label={`${serviceName} — início`} className="backoffice-brand" to={paths.home.getHref()}>
          <span className="brand-mark"><CodeXml size={18} /></span><span>Code4Coders</span><span className="brand-badge">Backoffice</span>
        </Link>
        <p className="sidebar-heading">Operação</p>
        <nav aria-label={`${serviceName} navigation`} className="app-nav">
          <NavLink end to={paths.home.getHref()}><House size={18} />Início</NavLink>
          {areas.map((area) => area.href ? (
            <NavLink key={area.permission} to={area.href}>
              {area.permission === 'financeiro.ler' ? <Wallet size={18} /> : <Users size={18} />}{area.label}
            </NavLink>
          ) : null)}
        </nav>
      </aside>
      <div className="app-main">
        <header className="app-header">
          <button aria-expanded={menuOpen} aria-label="Toggle navigation" className="menu-button" onClick={toggleMenu} type="button">
            {menuOpen ? <X size={20} /> : <Menu size={20} />}
          </button>
          <div className="account-menu-wrap">
            <button aria-expanded={accountOpen} aria-haspopup="menu" className="account-trigger" onClick={() => setAccountOpen(!accountOpen)} type="button">
              <span className="account-avatar">{initials}</span><span>{name}</span><ChevronDown size={16} />
            </button>
            {accountOpen ? <div aria-label="Conta" className="account-menu" role="menu">
              <strong>{name}</strong><div className="role-badges">{roles.map((role) => <span className="role-badge" key={role}>{role}</span>)}</div>
              <button disabled={isLoggingOut} onClick={onLogout} role="menuitem" type="button">{isLoggingOut ? 'Saindo…' : 'Sair'}</button>
            </div> : null}
          </div>
        </header>
        {logoutError ? <p className="inline-alert" role="alert">{logoutError}</p> : null}
        <Outlet context={outletContext} />
      </div>
    </div>
  );
};
