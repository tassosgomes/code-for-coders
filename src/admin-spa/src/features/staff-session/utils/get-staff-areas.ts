import type { StaffArea } from '@/types/staff-area';

const staffAreas = [
  { label: 'Acessos', permission: 'acesso.gerir' },
  { label: 'Financeiro', permission: 'financeiro.ler' },
  { label: 'Autoria', permission: 'autoria.ler' },
  { label: 'Suporte', permission: 'suporte.atender' },
] satisfies readonly StaffArea[];

export const getStaffAreas = (permissions: readonly string[]) =>
  staffAreas.filter((area) => permissions.includes(area.permission));
