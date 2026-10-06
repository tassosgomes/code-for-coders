import { useSearchParams } from 'react-router';

import { financeOrderFiltersSchema, type FinanceOrderFilters } from '@/features/finance-area/api/list-finance-orders';

export const emptyFinanceFilters: FinanceOrderFilters = { status: '', courseId: '', studentId: '', createdFrom: '', createdTo: '' };
export const useFinanceFilters = () => {
  const [params, setParams] = useSearchParams();
  const parsed = financeOrderFiltersSchema.safeParse(Object.fromEntries(Object.keys(emptyFinanceFilters).map((key) => [key, params.get(key) ?? ''])));
  const filters = parsed.success ? parsed.data : emptyFinanceFilters;
  const rawPage = Number(params.get('_page') ?? 1);
  const page = Number.isSafeInteger(rawPage) && rawPage > 0 && rawPage <= 100000 ? rawPage : 1;
  const apply = (next: FinanceOrderFilters, nextPage = 1) => {
    const values = Object.fromEntries(Object.entries(next).filter(([, value]) => value));
    setParams({ ...values, ...(nextPage > 1 ? { _page: String(nextPage) } : {}) });
  };
  return { filters, page, apply, count: Object.values(filters).filter(Boolean).length };
};
