import { queryOptions, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import axios from 'axios';
import { z } from 'zod';

import { apiClient } from '@/lib/api-client';
import { staffInvitationRoles } from '@/features/staff-access/api/staff-invitations';

export const staffRoleActionSchema = z.object({
  role: z.enum(staffInvitationRoles),
  reason: z.string()
    .min(1, 'Informe o motivo da alteração.')
    .max(1000, 'O motivo deve ter no máximo 1000 caracteres.')
    .refine((reason) => reason.trim().length > 0, 'Informe o motivo da alteração.'),
});

export type StaffRoleActionInput = z.infer<typeof staffRoleActionSchema>;
export type StaffRoleActionKind = 'grant' | 'revoke';

export type StaffMember = {
  accountId: string;
  name: string;
  email: string;
  roles: (typeof staffInvitationRoles)[number][];
  isSelf: boolean;
};

export type StaffMemberPage = {
  data: StaffMember[];
  pagination: { page: number; size: number; total: number; totalPages: number };
};

export type StaffRoleActionResult = {
  member: StaffMember;
  changed: boolean;
  sessionsEnded: boolean;
};

export type StaffRoleActionCommand = {
  accountId: string;
  input: StaffRoleActionInput;
  idempotencyKey: string;
};

const staffMemberSchema = z.object({
  accountId: z.uuid(),
  name: z.string(),
  email: z.email().max(254),
  roles: z.array(z.enum(staffInvitationRoles)).max(4)
    .refine((roles) => new Set(roles).size === roles.length),
  isSelf: z.boolean(),
});

const staffMemberPageSchema = z.object({
  data: z.array(staffMemberSchema),
  pagination: z.object({
    page: z.number().int().min(1),
    size: z.number().int().min(1).max(100),
    total: z.number().int().min(0),
    totalPages: z.number().int().min(0),
  }),
});

const staffRoleActionResultSchema = z.object({
  member: staffMemberSchema,
  changed: z.boolean(),
  sessionsEnded: z.boolean(),
});

export const staffMemberQueryKeys = {
  all: ['staff-members'] as const,
  list: () => [...staffMemberQueryKeys.all, 'list'] as const,
};

export const getStaffMembers = async (): Promise<StaffMemberPage> => {
  const response = await apiClient.get<StaffMemberPage>('/api/v1/staff-members', {
    params: { page: 1, size: 100 },
  });
  return staffMemberPageSchema.parse(response);
};

export const staffMembersQueryOptions = () => queryOptions({
  queryKey: staffMemberQueryKeys.list(),
  queryFn: getStaffMembers,
});

export const useStaffMembers = () => useQuery(staffMembersQueryOptions());

const submitStaffRoleAction = async (
  command: StaffRoleActionCommand,
  action: StaffRoleActionKind,
): Promise<StaffRoleActionResult> => {
  const request = staffRoleActionSchema.parse(command.input);
  const endpoint = action === 'grant' ? 'role-grants' : 'role-revocations';
  const response = await apiClient.post<StaffRoleActionResult>(
    `/api/v1/staff-members/${command.accountId}/${endpoint}`,
    request,
    { headers: { 'Idempotency-Key': command.idempotencyKey } },
  );
  return staffRoleActionResultSchema.parse(response);
};

export const grantStaffRole = (command: StaffRoleActionCommand) => submitStaffRoleAction(command, 'grant');

export const revokeStaffRole = (command: StaffRoleActionCommand) => submitStaffRoleAction(command, 'revoke');

export const useGrantStaffRole = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: grantStaffRole,
    onSuccess: async () => queryClient.invalidateQueries({ queryKey: staffMemberQueryKeys.all }),
  });
};

export const useRevokeStaffRole = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: revokeStaffRole,
    onSuccess: async () => queryClient.invalidateQueries({ queryKey: staffMemberQueryKeys.all }),
  });
};

export const getStaffRoleActionRequestError = (error: unknown): string => {
  if (axios.isAxiosError<{ code?: unknown }>(error)) {
    switch (error.response?.data?.code) {
      case 'REASON_REQUIRED':
        return 'Informe o motivo da alteração.';
      case 'SELF_ROLE_CHANGE_FORBIDDEN':
        return 'Você não pode alterar os próprios papéis.';
      case 'STAFF_MEMBER_NOT_FOUND':
        return 'A conta interna não foi encontrada.';
      case 'IDEMPOTENCY_KEY_REUSED':
        return 'A chave desta ação já foi usada com outros dados. Atualize a página e tente novamente.';
      case 'PERMISSION_DENIED':
        return 'Você não tem permissão para gerenciar acessos.';
    }
  }

  return 'Não foi possível alterar os papéis agora. Tente novamente.';
};
