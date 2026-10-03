import { useState } from 'react';

import { useCourtesyCourses, type CourtesyCourseSearchInput } from '@/features/courtesies/api/list-courtesy-courses';

export const useCourtesyCourseStep = () => {
  const [input, setInput] = useState({ page: 1, title: '' });
  const query = useCourtesyCourses(input);
  const search = (values: CourtesyCourseSearchInput) => { setInput({ page: 1, title: values.title.trim() }); return Promise.resolve(true); };
  return { query, input, search, showAll: () => setInput({ page: 1, title: '' }),
    setPage: (page: number) => setInput((current) => ({ ...current, page })) };
};
