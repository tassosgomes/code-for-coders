import { Link } from 'react-router';

import { CodeWindow } from '@/components/blocks/code-window';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Progress } from '@/components/ui/progress';
import { Skeleton } from '@/components/ui/skeleton';
import { paths } from '@/config/paths';
import type { MyCourses } from '@/features/student-dashboard/api/get-my-courses';

type MyCoursesListProps = {
  courses?: MyCourses;
  isLoading: boolean;
};

export const MyCoursesList = ({ courses, isLoading }: MyCoursesListProps) => (
  <section aria-label="Seus cursos" className="min-w-0 space-y-4">
    {(isLoading || Boolean(courses?.active.length)) && <h3 className="typo-h3">Seus cursos</h3>}
    {isLoading ? (
      <div aria-label="Carregando seus cursos" role="status" className="space-y-4">
        {[0, 1].map((item) => (
          <Card key={item}>
            <CardContent className="space-y-4">
              <Skeleton aria-hidden="true" className="h-6 w-3/5" />
              <Skeleton aria-hidden="true" className="h-4 w-2/5" />
              <Skeleton aria-hidden="true" className="h-2 w-full" />
              <Skeleton aria-hidden="true" className="h-10 w-28" />
            </CardContent>
          </Card>
        ))}
      </div>
    ) : courses?.active.length === 0 && courses.ended.length === 0 ? (
      <Card>
        <CardContent className="space-y-5">
          <CodeWindow code="cursos.length === 0" filename="meus-cursos.ts" />
          <div className="space-y-2">
            <h4 className="typo-h4">Você ainda não tem cursos</h4>
            <p className="text-sm text-muted-foreground">Quando você se matricular em um curso, ele aparece aqui.</p>
          </div>
          <Button asChild><Link to={paths.studentShowcase.getHref()}>Explorar cursos</Link></Button>
        </CardContent>
      </Card>
    ) : courses?.active.map((course) => (
      <Card key={course.courseId}>
        <CardContent className="space-y-4">
          <h4 className="typo-h4">{course.title}</h4>
          {course.progress && (
            <>
              <p className="text-sm text-muted-foreground">
                {course.progress.percent}% · {course.progress.completedLessons} de {course.progress.totalLessons} aulas
              </p>
              <Progress
                aria-label={`Progresso de ${course.title}`}
                aria-valuetext={`${course.progress.completedLessons} de ${course.progress.totalLessons} aulas concluídas`}
                value={course.progress.percent}
              />
            </>
          )}
          <Button asChild>
            <Link to={paths.studentLesson.getHref(course.continueLessonId)}>{course.started ? 'Continuar' : 'Começar'}</Link>
          </Button>
        </CardContent>
      </Card>
    ))}
  </section>
);
