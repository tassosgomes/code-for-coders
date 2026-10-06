import { queryOptions, useQuery } from '@tanstack/react-query';
import axios from 'axios';
import { useState } from 'react';
import { useOutletContext } from 'react-router';

import { getCatalogCourses } from '@/features/catalog-courses/api/get-catalog-courses';
import { useStudentAccountLookup } from '@/features/courtesies/api/lookup-student-account';
import { FinanceAreaScreen } from '@/features/finance-area/components/finance-area-screen';
import { FinanceOrdersScreen } from '@/features/finance-area/components/finance-orders-screen';

const financeCourseOptions = queryOptions({
  queryKey: ['finance-course-options'],
  queryFn: async () => {
    const first = await getCatalogCourses({ page: 1, size: 50 });
    const data = [...first.data];
    for (let page = 2; page <= first.pagination.totalPages; page += 1) data.push(...(await getCatalogCourses({ page, size: 50 })).data);
    return data;
  },
});
export const FinanceAreaRoute = () => {
  const session = useOutletContext<{ permissions: readonly string[] }>();
  if (!session.permissions.includes('financeiro.ler')) return <FinanceAreaScreen />;
  return <FinanceAreaContent />;
};
const FinanceAreaContent = () => {
  const lookup = useStudentAccountLookup();
  const courses = useQuery(financeCourseOptions);
  const [search, setSearch] = useState('');
  const code = axios.isAxiosError<{ code?: string }>(lookup.error) ? lookup.error.response?.data.code : undefined;
  const filtered = courses.data?.filter((course) => course.title.toLocaleLowerCase('pt-BR').includes(search.toLocaleLowerCase('pt-BR'))) ?? [];
  return <FinanceOrdersScreen lookup={{
    account: lookup.data, busy: lookup.isPending,
    error: lookup.isError ? code === 'STUDENT_ACCOUNT_NOT_FOUND' ? 'Nenhuma conta de aluno com este e-mail.' : 'Não foi possível localizar agora. Tente de novo.' : null,
    locate: async (email) => { try { await lookup.mutateAsync({ email }); return true; } catch { return false; } }, reset: lookup.reset,
  }} courses={{ data: filtered, search, setSearch, busy: courses.isPending, unavailable: courses.isError, page: 1, totalPages: 1, changePage: () => undefined }} />;
};
