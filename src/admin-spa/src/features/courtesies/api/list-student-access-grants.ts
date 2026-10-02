import { queryOptions, useQuery } from '@tanstack/react-query';

import type { CourtesyGrant } from '@/features/courtesies/types/courtesy-grant';
import { apiClient } from '@/lib/api-client';

export type StudentAccessGrantPage = { data: CourtesyGrant[]; pagination: { page: number; size: number; total: number; totalPages: number } };
export const listStudentAccessGrants = (studentId: string, page = 1): Promise<StudentAccessGrantPage> =>
  apiClient.get(`/api/v1/students/${studentId}/access-grants`, { params: { _page: page, _size: 50 } });

export const studentAccessGrantsQueryOptions = (studentId: string) => queryOptions({
  queryKey: ['student-access-grants', studentId],
  queryFn: async () => {
    const first = await listStudentAccessGrants(studentId);
    const grants = [...first.data];
    // The warning must consider every grant, including access beyond the first page.
    for (let page = 2; page <= first.pagination.totalPages; page++) grants.push(...(await listStudentAccessGrants(studentId, page)).data);
    return grants;
  },
  staleTime: 0,
});
export const useStudentAccessGrants = (studentId: string, enabled = true) => useQuery({ ...studentAccessGrantsQueryOptions(studentId), enabled: enabled && Boolean(studentId) });
