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

// P1.e: cartões com a miniatura de 160 px e barras no corpo.
const ShowcaseSkeleton = () => (
  <div aria-label="Carregando cursos" className="grid items-start gap-8 sm:grid-cols-2 lg:grid-cols-3" role="status">
    {Array.from({ length: SKELETON_COUNT }, (_, index) => (
      <div className="overflow-hidden rounded-xl border bg-card" key={index}>
        <Skeleton aria-hidden="true" className="hidden h-40 w-full rounded-none sm:block" />
        <div className="flex flex-col gap-3 p-5">
          <Skeleton aria-hidden="true" className="h-5 w-20" />
          <Skeleton aria-hidden="true" className="h-5 w-3/5" />
          <Skeleton aria-hidden="true" className="h-4 w-4/5" />
          <Skeleton aria-hidden="true" className="h-4 w-2/5" />
        </div>
      </div>
    ))}
  </div>
);

const emptyCardClassName = 'flex flex-col items-center rounded-xl border bg-card p-8 text-center shadow-xs sm:p-12';

export const StudentShowcaseScreen = ({
  level,
  page,
  result,
  onLevelChange,
  onPageChange,
  onRetry,
}: StudentShowcaseScreenProps) => {
  const total = result.status === 'success' ? result.page.pagination.total : undefined;
  // P1.d: sem nenhum curso na vitrine, o filtro e a contagem não aparecem.
  const showcaseIsEmpty = result.status === 'success' && total === 0 && !level;

  return (
    <div className="flex flex-col gap-8">
      <header className="space-y-3">
        <p className="typo-overline text-primary">Cursos</p>
        <h1 className="typo-h2 sm:typo-h1">Escolha por onde começar</h1>
        <p className="max-w-prose text-lg text-muted-foreground">
          Aprenda programação com aulas em vídeo, do básico ao avançado.
        </p>
      </header>

      {showcaseIsEmpty ? null : (
        <div className="flex flex-col gap-3">
          <ShowcaseLevelFilter level={level} onLevelChange={onLevelChange} />
          <p aria-live="polite" className="min-h-5 text-sm text-muted-foreground" role="status">
            {total === undefined ? '' : formatCount(total)}
          </p>
        </div>
      )}

      {result.status === 'loading' ? <ShowcaseSkeleton /> : null}

      {result.status === 'error' ? (
        <Alert className="rounded-xl border-destructive p-4" variant="destructive">
          <AlertCircle aria-hidden="true" />
          <AlertTitle>Não foi possível carregar os cursos agora.</AlertTitle>
          <AlertDescription>
            <p>Tente de novo em instantes.</p>
            <Button className="h-8 px-0 text-xs text-primary" onClick={onRetry} variant="link">
              Tentar de novo
            </Button>
          </AlertDescription>
        </Alert>
      ) : null}

      {result.status === 'success' && result.page.data.length === 0 ? (
        level ? (
          <div className={`${emptyCardClassName} gap-4`}>
            <p className="typo-h4 text-foreground">Nenhum curso neste nível por enquanto.</p>
            <Button onClick={() => onLevelChange(undefined)} variant="outline">
              Ver todos os cursos
            </Button>
          </div>
        ) : (
          <div className={`${emptyCardClassName} gap-5`}>
            <CodeWindow className="w-full max-w-135 text-left" code={'// em breve, cursos por aqui'} filename="cursos.ts" />
            <p className="typo-h4 text-foreground">Ainda não há cursos disponíveis.</p>
            <p className="text-sm text-muted-foreground">Volte em breve.</p>
          </div>
        )
      ) : null}

      {result.status === 'success' && result.page.data.length > 0 ? (
        <>
          <ul className="grid items-start gap-6 sm:grid-cols-2 sm:gap-8 lg:grid-cols-3">
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
