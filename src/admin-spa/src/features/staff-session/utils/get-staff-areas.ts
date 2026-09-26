import type { StaffArea } from '@/types/staff-area';
import { paths } from '@/config/paths';

const staffAreas = [
  { label: 'Vídeos', permission: 'midia.enviar', href: paths.videos.getHref() },
  { label: 'Acessos', permission: 'acesso.gerir', href: paths.staffAccess.getHref() },
  { label: 'Financeiro', permission: 'financeiro.ler', href: paths.staffFinance.getHref() },
  { label: 'Autoria', permission: 'autoria.ler' },
  { label: 'Suporte', permission: 'suporte.atender' },
] satisfies readonly StaffArea[];

export const getStaffAreas = (permissions: readonly string[]) =>
  staffAreas.filter((area) => permissions.includes(area.permission));
