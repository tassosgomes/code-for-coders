import { AlertCircle } from 'lucide-react';

import { CodeWindow } from '@/components/blocks/code-window';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import type { ShowcaseCoursePage } from '@/features/student-showcase/api/get-showcase-courses';
import { ShowcaseCourseCard } from '@/features/student-showcase/components/showcase-course-card';
import { ShowcaseLevelFilter } from '@/features/student-showcase/components/showcase-level-filter';
import { ShowcasePagination } from '@/features/student-showcase/components/showcase-pagination';
import type { ShowcaseLevelParam } from '@/features/student-showcase/utils/showcase-level';

type StudentShowcaseScreenProps = {
  level: ShowcaseLevelParam | undefined;
  page: number;
  result: { status: 'loading' } | { status: 'error' } | { status: 'success'; page: ShowcaseCoursePage };
  onLevelChange: (level: ShowcaseLevelParam | undefined) => void;
  onPageChange: (page: number) => void;
  onRetry: () => void;
};

const SKELETON_COUNT = 6;

const formatCount = (total: number) => (total === 1 ? '1 curso' : `${total} cursos`);

const ShowcaseSkeleton = () => (
  <div aria-label="Carregando cursos" className="grid gap-6 sm:grid-cols-2 lg:grid-cols-3" role="status">
    {Array.from({ length: SKELETON_COUNT }, (_, index) => (
      <div className="flex flex-col gap-3 rounded-xl border p-5" key={index}>
        <Skeleton aria-hidden="true" className="hidden h-24 w-full sm:block" />
        <Skeleton aria-hidden="true" className="h-5 w-24" />
        <Skeleton aria-hidden="true" className="h-6 w-4/5" />
        <Skeleton aria-hidden="true" className="h-4 w-full" />
        <Skeleton aria-hidden="true" className="h-4 w-3/5" />
      </div>
    ))}
  </div>
);

export const StudentShowcaseScreen = ({
  level,
  page,
  result,
  onLevelChange,
  onPageChange,
  onRetry,
}: StudentShowcaseScreenProps) => {
  const total = result.status === 'success' ? result.page.pagination.total : undefined;

  return (
    <div className="flex flex-col gap-8">
      <header className="space-y-2">
        <p className="typo-overline text-primary">Cursos</p>
        <h1 className="typo-h2">Escolha por onde começar</h1>
        <p className="max-w-prose text-muted-foreground">
          Aprenda programação com aulas em vídeo, do básico ao avançado.
        </p>
      </header>

      <div className="flex flex-col gap-3">
        <ShowcaseLevelFilter level={level} onLevelChange={onLevelChange} />
        <p aria-live="polite" className="text-sm text-muted-foreground" role="status">
          {total === undefined ? '' : formatCount(total)}
        </p>
      </div>

      {result.status === 'loading' ? <ShowcaseSkeleton /> : null}

      {result.status === 'error' ? (
        <Alert variant="destructive">
          <AlertCircle aria-hidden="true" />
          <AlertTitle>Não foi possível carregar os cursos agora.</AlertTitle>
          <AlertDescription>
            <Button onClick={onRetry} size="sm" variant="outline">
              Tentar de novo
            </Button>
          </AlertDescription>
        </Alert>
      ) : null}

      {result.status === 'success' && result.page.data.length === 0 ? (
        level ? (
          <div className="flex flex-col items-start gap-4">
            <p className="text-lg font-medium text-foreground">Nenhum curso neste nível por enquanto.</p>
            <Button onClick={() => onLevelChange(undefined)} variant="outline">
              Ver todos os cursos
            </Button>
          </div>
        ) : (
          <div className="flex max-w-xl flex-col gap-5">
            <CodeWindow code={'// em breve, cursos por aqui'} filename="cursos.ts" />
            <div className="space-y-1">
              <p className="text-lg font-medium text-foreground">Ainda não há cursos disponíveis.</p>
              <p className="text-muted-foreground">Volte em breve.</p>
            </div>
          </div>
        )
      ) : null}

      {result.status === 'success' && result.page.data.length > 0 ? (
        <>
          <ul className="grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
            {result.page.data.map((course) => (
              <li className="flex" key={course.courseId}>
                <div className="w-full">
                  <ShowcaseCourseCard course={course} />
                </div>
              </li>
            ))}
          </ul>
          <ShowcasePagination onPageChange={onPageChange} page={page} totalPages={result.page.pagination.totalPages} />
        </>
      ) : null}
    </div>
  );
};
