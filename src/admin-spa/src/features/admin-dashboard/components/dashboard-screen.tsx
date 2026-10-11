import { ArrowRight, BookOpen, Clapperboard, ScrollText, Tags, Users, Wallet, type LucideIcon } from 'lucide-react';
import { Link } from 'react-router';

import type { StaffArea } from '@/types/staff-area';

type DashboardScreenProps = {
  areas?: readonly StaffArea[];
  name?: string;
  roles?: readonly string[];
};

const getAreaContent = (area: StaffArea): { icon: LucideIcon; description: string } => {
  switch (area.permission) {
    case 'acesso.gerir': return { icon: Users, description: 'Convide pessoas e ajuste os papéis da equipe.' };
    case 'autoria.ler': return { icon: BookOpen, description: 'Monte módulos e aulas, depois publique.' };
    case 'midia.enviar': return { icon: Clapperboard, description: 'Envie as gravações das aulas e acompanhe a preparação.' };
    case 'financeiro.ler': return { icon: Wallet, description: 'Vendas, pedidos e repasses da escola.' };
    case 'oferta.editar': return { icon: Tags, description: 'Publique ofertas e ajuste preços dos cursos.' };
    default:
      return area.role === 'administrador'
        ? { icon: ScrollText, description: 'Consulte os atos administrativos registrados para o seu tenant.' }
        : { icon: Users, description: 'Área reservada à equipe autorizada.' };
  }
};

export const DashboardScreen = ({ areas = [], name = '', roles = [] }: DashboardScreenProps) => {
  const firstName = name.split(' ')[0];
  const linkedAreas = areas.filter((area) => area.href).sort((first, second) => Number(second.permission === 'autoria.ler') - Number(first.permission === 'autoria.ler'));

  return <main className="page-shell dashboard-page authoring-page">
    <header className="dashboard-heading"><p className="eyebrow">Início</p>
    <h1>Olá, {firstName || 'equipe'}</h1>
    <p className="dashboard-roles"><span className="dashboard-roles-label">Seus papéis:</span>{roles.length ? roles.map((role) => <span className="role-badge" key={role}>{role}</span>) : <span className="role-badge role-badge-empty">Sem papel</span>}</p></header>
    {linkedAreas.length ? <div className="area-grid">{linkedAreas.map((area) => {
      const content = getAreaContent(area);
      const Icon = content.icon;
      return <article className="area-card" key={area.permission ?? area.role ?? area.label}>
        <span className="area-icon"><Icon aria-hidden="true" size={24} /></span>
        <h2>{area.label}</h2>
        <p>{content.description}</p>
        <Link className="outline-button" to={area.href!}>Abrir {area.label.toLocaleLowerCase('pt-BR')} <ArrowRight size={16} /></Link>
      </article>;
    })}</div> : <section className="empty-state"><h2>{roles.length ? 'As ferramentas do seu papel chegam em breve' : 'Você ainda não tem acesso a uma área'}</h2><p>{roles.length ? 'Sua conta está ativa. Volte em breve para usar as ferramentas da equipe.' : 'Peça a um administrador para conceder um papel à sua conta.'}</p></section>}
  </main>;
};
