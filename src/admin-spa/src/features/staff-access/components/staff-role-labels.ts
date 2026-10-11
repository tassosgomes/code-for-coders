import { staffInvitationRoles } from '@/features/staff-access/api/staff-invitations';

export type StaffRole = (typeof staffInvitationRoles)[number];

export const staffRoleLabels: Record<StaffRole, string> = {
  administrador: 'Administrador',
  professor: 'Professor',
  suporte: 'Suporte',
  financeiro: 'Financeiro',
};

export const staffRoleDescriptions: Record<StaffRole, string> = {
  professor: 'Autoria de cursos',
  suporte: 'Atendimento a alunos',
  financeiro: 'Vendas, pedidos e repasses',
  administrador: 'Gestão de acessos da equipe',
};

export const staffRoleOrder: StaffRole[] = ['professor', 'suporte', 'financeiro', 'administrador'];

export const sortStaffRoles = (roles: StaffRole[]) => staffRoleOrder.filter((role) => roles.includes(role));

export const firstName = (name: string) => name.trim().split(/\s+/)[0] ?? name;

export const formatRoleList = (roles: StaffRole[]) => {
  const labels = roles.map((role) => staffRoleLabels[role]);
  if (labels.length <= 1) return labels.join('');
  return `${labels.slice(0, -1).join(', ')} e ${labels[labels.length - 1]}`;
};

const dateFormat = new Intl.DateTimeFormat('pt-BR', { day: '2-digit', month: '2-digit', year: 'numeric' });
const timeFormat = new Intl.DateTimeFormat('pt-BR', { hour: '2-digit', minute: '2-digit', hour12: false });

export const formatStaffDate = (value: string) => dateFormat.format(new Date(value));
export const formatStaffTime = (value: string) => timeFormat.format(new Date(value));
export const formatStaffDateTime = (value: string) => `${formatStaffDate(value)} ${formatStaffTime(value)}`;
