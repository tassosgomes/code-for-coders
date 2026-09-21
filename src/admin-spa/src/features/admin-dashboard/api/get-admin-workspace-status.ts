import { queryOptions, useQuery } from '@tanstack/react-query';
import { z } from 'zod';

import { apiClient } from '@/lib/api-client';

const AdminWorkspaceStatusSchema = z.object({
  status: z.enum(['ready', 'degraded']),
  message: z.string(),
});

export type AdminWorkspaceStatus = z.infer<typeof AdminWorkspaceStatusSchema>;

export const getAdminWorkspaceStatus = async (): Promise<AdminWorkspaceStatus> => {
  const response = await apiClient.get<AdminWorkspaceStatus>('/v1/admin/workspace/status');

  return AdminWorkspaceStatusSchema.parse(response);
};

export const getAdminWorkspaceStatusQueryOptions = () =>
  queryOptions({
    queryKey: ['admin-workspace-status'],
    queryFn: getAdminWorkspaceStatus,
    staleTime: 30_000,
  });

export const useAdminWorkspaceStatus = () => useQuery(getAdminWorkspaceStatusQueryOptions());
