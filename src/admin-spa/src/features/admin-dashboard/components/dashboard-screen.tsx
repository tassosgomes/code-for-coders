import { ArrowRight, Users, Wallet } from 'lucide-react';
import { Link } from 'react-router';

import type { StaffArea } from '@/types/staff-area';

type DashboardScreenProps = {
  areas?: readonly StaffArea[];
  name?: string;
  roles?: readonly string[];
};

export const DashboardScreen = ({ areas = [], name = '', roles = [] }: DashboardScreenProps) => {
  const firstName = name.split(' ')[0];
  const linkedAreas = areas.filter((area) => area.href);

  return <main className="page-shell dashboard-page">
    <p className="eyebrow">Início</p>
    <h1>Olá, {firstName || 'equipe'}</h1>
    <p className="page-subtitle">Seus papéis: {roles.length ? roles.map((role) => <span className="role-badge" key={role}>{role}</span>) : <span className="role-badge role-badge-empty">Sem papel</span>}</p>
    {linkedAreas.length ? <div className="area-grid">{linkedAreas.map((area) => <article className="area-card" key={area.permission}>
      <span className="area-icon">{area.permission === 'financeiro.ler' ? <Wallet size={22} /> : <Users size={22} />}</span>
      <h2>{area.label}</h2>
      <p>{area.permission === 'acesso.gerir' ? 'Convide pessoas e ajuste os papéis da equipe.' : 'Área financeira reservada à equipe autorizada.'}</p>
      <Link className="outline-button" to={area.href!}>Abrir {area.label.toLowerCase()} <ArrowRight size={16} /></Link>
    </article>)}</div> : <section className="empty-state"><h2>{roles.length ? 'As ferramentas do seu papel chegam em breve' : 'Você ainda não tem acesso a uma área'}</h2><p>{roles.length ? 'Sua conta está ativa. Volte em breve para usar as ferramentas da equipe.' : 'Peça a um administrador para conceder um papel à sua conta.'}</p></section>}
  </main>;
};
