import { useEffect } from 'react';
import { useSearchParams } from 'react-router';

import { useShowcaseCourses } from '@/features/student-showcase/api/get-showcase-courses';
import { StudentShowcaseScreen } from '@/features/student-showcase/components/student-showcase-screen';
import { parseLevelParam, type ShowcaseLevelParam } from '@/features/student-showcase/utils/showcase-level';
import { useDocumentTitle } from '@/hooks/use-document-title';

const LEVEL_PARAM = 'nivel';
const PAGE_PARAM = 'pagina';

const parsePage = (value: string | null) => {
  const page = Number(value);

  return Number.isInteger(page) && page >= 1 ? page : 1;
};

export const StudentShowcaseRoute = () => {
  const [searchParams, setSearchParams] = useSearchParams();
  // An unknown level in the address behaves as "Todos" (P1.g), without an error on screen.
  const level = parseLevelParam(searchParams.get(LEVEL_PARAM));
  const page = parsePage(searchParams.get(PAGE_PARAM));
  const courses = useShowcaseCourses({ level, page });

  useDocumentTitle('Cursos | Code4Coders');

  const pageCount = courses.data?.pagination.totalPages ?? 0;
  const pastTheEnd = courses.data !== undefined && courses.data.data.length === 0 && page > 1 && pageCount >= 1;

  useEffect(() => {
    if (pastTheEnd) {
      setSearchParams((current) => {
        const next = new URLSearchParams(current);
        next.delete(PAGE_PARAM);
        return next;
      }, { replace: true });
    }
  }, [pastTheEnd, setSearchParams]);

  const changeLevel = (nextLevel: ShowcaseLevelParam | undefined) => {
    setSearchParams((current) => {
      const next = new URLSearchParams(current);
      next.delete(PAGE_PARAM);
      if (nextLevel) {
        next.set(LEVEL_PARAM, nextLevel);
      } else {
        next.delete(LEVEL_PARAM);
      }
      return next;
    });
  };

  const changePage = (nextPage: number) => {
    setSearchParams((current) => {
      const next = new URLSearchParams(current);
      if (nextPage <= 1) {
        next.delete(PAGE_PARAM);
      } else {
        next.set(PAGE_PARAM, String(nextPage));
      }
      return next;
    });
    window.scrollTo?.({ top: 0 });
  };

  return (
    <StudentShowcaseScreen
      level={level}
      onLevelChange={changeLevel}
      onPageChange={changePage}
      onRetry={() => void courses.refetch()}
      page={Math.min(page, Math.max(pageCount, 1))}
      result={
        courses.isError
          ? { status: 'error' }
          : courses.data && !pastTheEnd
            ? { status: 'success', page: courses.data }
            : { status: 'loading' }
      }
    />
  );
};

