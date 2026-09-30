import type { StaffArea } from '@/types/staff-area';
import { paths } from '@/config/paths';

const staffAreas = [
  { label: 'Vídeos', permission: 'midia.enviar', href: paths.videos.getHref() },
  { label: 'Acessos', permission: 'acesso.gerir', href: paths.staffAccess.getHref() },
  { label: 'Auditoria', role: 'administrador', href: paths.auditTrail.getHref() },
  { label: 'Catálogo', permission: 'oferta.editar', href: paths.catalog.getHref() },
  { label: 'Financeiro', permission: 'financeiro.ler', href: paths.staffFinance.getHref() },
  { label: 'Autoria', permission: 'autoria.ler', href: paths.authoring.getHref() },
  { label: 'Suporte', permission: 'suporte.atender' },
] satisfies readonly StaffArea[];

export const getStaffAreas = (permissions: readonly string[], roles: readonly string[] = []) =>
  staffAreas.filter((area) => area.role
    ? roles.includes(area.role)
    : area.permission !== undefined && permissions.includes(area.permission));
