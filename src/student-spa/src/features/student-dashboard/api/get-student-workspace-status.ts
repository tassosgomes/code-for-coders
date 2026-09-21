import { queryOptions, useQuery } from '@tanstack/react-query';
import { z } from 'zod';

import { apiClient } from '@/lib/api-client';

const StudentWorkspaceStatusSchema = z.object({
  status: z.enum(['ready', 'degraded']),
  message: z.string(),
});

export type StudentWorkspaceStatus = z.infer<typeof StudentWorkspaceStatusSchema>;

export const getStudentWorkspaceStatus = async (): Promise<StudentWorkspaceStatus> => {
  const response = await apiClient.get<StudentWorkspaceStatus>('/v1/student/workspace/status');

  return StudentWorkspaceStatusSchema.parse(response);
};

export const getStudentWorkspaceStatusQueryOptions = () =>
  queryOptions({
    queryKey: ['student-workspace-status'],
    queryFn: getStudentWorkspaceStatus,
    staleTime: 30_000,
  });

export const useStudentWorkspaceStatus = () =>
  useQuery(getStudentWorkspaceStatusQueryOptions());
