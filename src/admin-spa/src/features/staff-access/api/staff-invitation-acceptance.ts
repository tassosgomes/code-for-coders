import { queryOptions, useMutation, useQuery } from '@tanstack/react-query';
import axios from 'axios';
import { z } from 'zod';

import { staffInvitationRoles } from '@/features/staff-access/api/staff-invitations';
import { apiClient } from '@/lib/api-client';

const invitationTokenSchema = z.string()
  .min(1, 'Este link de convite não é válido.')
  .max(512, 'Este link de convite não é válido.');

const invitationNameSchema = z.string()
  .max(200, 'O nome deve ter no máximo 200 caracteres.')
  .trim()
  .min(1, 'Informe seu nome.');

const invitationPasswordSchema = z.string()
  .min(8, 'Use pelo menos 8 caracteres.')
  .refine((password) => /\p{Lu}/u.test(password), 'Inclua uma letra maiúscula.')
  .refine((password) => /\p{Ll}/u.test(password), 'Inclua uma letra minúscula.')
  .refine((password) => /\p{Nd}/u.test(password), 'Inclua um número.')
  .refine((password) => /[^\p{L}\p{N}\s]/u.test(password), 'Inclua um símbolo ou sinal de pontuação.');

export const acceptStaffInvitationSchema = z.object({
  token: invitationTokenSchema,
  name: invitationNameSchema,
  password: invitationPasswordSchema,
});

export const staffInvitationAcceptanceFormSchema = acceptStaffInvitationSchema.omit({ token: true });

export type StaffInvitationAcceptanceFormInput = z.infer<typeof staffInvitationAcceptanceFormSchema>;

export type AcceptStaffInvitationCommand = {
  token: string;
  input: StaffInvitationAcceptanceFormInput;
};

export type StaffInvitationPreview = {
  offeredRole: (typeof staffInvitationRoles)[number];
  expiresAt: string;
};

const staffInvitationPreviewSchema = z.object({
  offeredRole: z.enum(staffInvitationRoles),
  expiresAt: z.string(),
});

const staffInvitationSessionSchema = z.object({
  accountId: z.uuid(),
  name: z.string(),
  roles: z.array(z.string()),
  permissions: z.array(z.string()),
  csrfToken: z.string(),
});

export const staffInvitationAcceptanceQueryKeys = {
  all: ['staff-invitation-acceptance'] as const,
  preview: (token: string) => [...staffInvitationAcceptanceQueryKeys.all, 'preview', token] as const,
};

export const lookupStaffInvitation = async (token: string): Promise<StaffInvitationPreview> => {
  const requestToken = invitationTokenSchema.parse(token);
  const response = await apiClient.post<StaffInvitationPreview>(
    '/api/v1/staff-invitation-lookups',
    { token: requestToken },
  );
  return staffInvitationPreviewSchema.parse(response);
};

export const staffInvitationPreviewQueryOptions = (token: string) => queryOptions({
  queryKey: staffInvitationAcceptanceQueryKeys.preview(token),
  queryFn: () => lookupStaffInvitation(token),
});

export const useStaffInvitationPreview = (token: string, enabled: boolean) =>
  useQuery({ ...staffInvitationPreviewQueryOptions(token), enabled });

export const acceptStaffInvitation = async ({ token, input }: AcceptStaffInvitationCommand) => {
  const request = acceptStaffInvitationSchema.parse({ token, ...input });
  const response = await apiClient.post<unknown>(
    '/api/v1/staff-invitation-acceptances',
    request,
    { headers: { 'Idempotency-Key': crypto.randomUUID() } },
  );
  return staffInvitationSessionSchema.parse(response);
};

export const useAcceptStaffInvitation = () => useMutation({ mutationFn: acceptStaffInvitation });

export const getStaffInvitationAcceptanceError = (error: unknown): string => {
  if (axios.isAxiosError<{ code?: unknown }>(error)) {
    switch (error.response?.data?.code) {
      case 'INVITATION_INVALID':
        return 'Este convite não vale mais. Peça um novo ao administrador.';
      case 'INVITATION_EMAIL_UNAVAILABLE':
        return 'Não foi possível ativar este convite. Procure o administrador.';
      case 'PASSWORD_POLICY_VIOLATION':
        return 'A senha não atende à política. Use 8 caracteres ou mais, com letra maiúscula, minúscula, número e símbolo.';
      case 'IDEMPOTENCY_KEY_REUSED':
        return 'Não foi possível concluir o aceite. Recarregue o convite e tente novamente.';
    }
  }

  return 'Não foi possível validar ou aceitar este convite. Peça um novo ao administrador.';
};
