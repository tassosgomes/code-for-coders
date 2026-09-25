import { queryOptions, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import axios from 'axios';
import { z } from 'zod';

import { apiClient } from '@/lib/api-client';

export const staffInvitationRoles = ['professor', 'suporte', 'financeiro', 'administrador'] as const;

export const createStaffInvitationSchema = z.object({
  email: z.string().trim().email('Informe um e-mail válido.').max(320),
  role: z.enum(staffInvitationRoles),
  reason: z.string().trim().min(1, 'Informe o motivo do convite.').max(1000, 'O motivo deve ter no máximo 1000 caracteres.'),
});

export type CreateStaffInvitationInput = z.infer<typeof createStaffInvitationSchema>;

export type PendingStaffInvitation = {
  invitationId: string;
  email: string;
  offeredRole: (typeof staffInvitationRoles)[number];
  invitedAt: string;
  expiresAt: string;
};

export type StaffInvitationPage = {
  data: PendingStaffInvitation[];
  pagination: { page: number; size: number; total: number; totalPages: number };
};

const pendingStaffInvitationSchema = z.object({
  invitationId: z.uuid(),
  email: z.string(),
  offeredRole: z.enum(staffInvitationRoles),
  invitedAt: z.string(),
  expiresAt: z.string(),
});

const staffInvitationPageSchema = z.object({
  data: z.array(pendingStaffInvitationSchema),
  pagination: z.object({
    page: z.number(),
    size: z.number(),
    total: z.number(),
    totalPages: z.number(),
  }),
});

const createdStaffInvitationSchema = pendingStaffInvitationSchema.extend({
  supersededInvitationId: z.uuid().nullable(),
});

export type CreatedStaffInvitation = PendingStaffInvitation & {
  supersededInvitationId: string | null;
};

export type CreateStaffInvitationCommand = {
  input: CreateStaffInvitationInput;
};

export const staffInvitationQueryKeys = {
  all: ['staff-invitations'] as const,
  pending: () => [...staffInvitationQueryKeys.all, 'pending'] as const,
};

export const getPendingStaffInvitations = async (): Promise<StaffInvitationPage> => {
  const response = await apiClient.get<StaffInvitationPage>('/api/v1/staff-invitations', {
    params: { page: 1, size: 100 },
  });
  return staffInvitationPageSchema.parse(response);
};

export const pendingStaffInvitationsQueryOptions = () => queryOptions({
  queryKey: staffInvitationQueryKeys.pending(),
  queryFn: getPendingStaffInvitations,
});

export const usePendingStaffInvitations = () => useQuery(pendingStaffInvitationsQueryOptions());

export const createStaffInvitation = async ({ input }: CreateStaffInvitationCommand): Promise<CreatedStaffInvitation> => {
  const request = createStaffInvitationSchema.parse(input);
  const response = await apiClient.post<CreatedStaffInvitation>('/api/v1/staff-invitations', request, {
    headers: { 'Idempotency-Key': crypto.randomUUID() },
  });
  return createdStaffInvitationSchema.parse(response);
};

export const useCreateStaffInvitation = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: createStaffInvitation,
    onSuccess: async () => queryClient.invalidateQueries({ queryKey: staffInvitationQueryKeys.all }),
  });
};

export const getStaffInvitationRequestError = (error: unknown): string => {
  if (axios.isAxiosError<{ code?: unknown }>(error)) {
    switch (error.response?.data?.code) {
      case 'EMAIL_BELONGS_TO_STAFF':
        return 'Este e-mail já pertence a uma conta interna.';
      case 'EMAIL_BELONGS_TO_STUDENT':
        return 'Este e-mail já pertence a uma conta de aluno.';
      case 'REASON_REQUIRED':
        return 'Informe o motivo do convite.';
      case 'PERMISSION_DENIED':
        return 'Você não tem permissão para gerenciar acessos.';
    }
  }

  return 'Não foi possível enviar o convite agora. Tente novamente.';
};
