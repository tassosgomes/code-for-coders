import { Link } from 'react-router';

import type { CourseProgress } from '@/features/student-lessons/api/get-course-progress';
import { Badge } from '@/components/ui/badge';
import { paths } from '@/config/paths';
import type { StudentLessonScreenData } from '@/features/student-lessons/api/get-student-lesson';

export type StudentLessonNavProps = {
  course: StudentLessonScreenData['course'];
  currentLessonId?: string;
  progress?: CourseProgress;
};

export const StudentLessonNav = ({ course, currentLessonId, progress }: StudentLessonNavProps) => {
  return (
    <nav aria-label="Aulas do curso" className="rounded-lg border p-4">
      <h2 className="typo-h4">Aulas do curso</h2>
      <p className="text-muted-foreground">{course.title}</p>
      {progress ? <div className="mt-3">
        <p className="text-sm">{progress.percent}% · {progress.completedLessons} de {progress.totalLessons} aulas concluídas</p>
        <div role="progressbar" aria-label="Progresso do curso" aria-valuemin={0} aria-valuemax={100}
          aria-valuenow={progress.percent} aria-valuetext={`${progress.completedLessons} de ${progress.totalLessons} aulas concluídas`}
          className="mt-2 h-2 overflow-hidden rounded bg-muted">
          <div className={`h-full ${progress.percent === 100 ? 'bg-success' : 'bg-primary'}`} style={{ width: `${progress.percent}%` }} />
        </div>
      </div> : null}
      <ul className="mt-4 space-y-4">
        {course.modules.map((module) => (
          <li key={module.moduleId}>
            <h3 className="font-semibold">
              {module.position}. {module.title}
            </h3>
            <ul className="mt-2 space-y-1">
              {module.lessons.map((item) => {
                const isCurrent = item.lessonId === currentLessonId;
                return (
                  <li key={item.lessonId}>
                    <Link
                      to={paths.studentLesson.getHref(item.lessonId)}
                      state={{ course, knownLessonId: item.lessonId }}
                      aria-current={isCurrent ? 'page' : undefined}
                      className={`block rounded-md p-2 focus-visible:outline focus-visible:outline-ring ${
                        isCurrent ? 'bg-secondary' : 'hover:bg-muted'
                      }`}
                    >
                      {item.position}. {item.title}
                      {progress?.lessons.some((lesson) => lesson.lessonId === item.lessonId && lesson.completed)
                        ? <Badge variant="secondary" className="ml-2 bg-success-soft text-success-soft-foreground"><span aria-hidden="true">✓ </span>Concluída</Badge> : null}
                      {isCurrent ? <span className="ml-2 text-sm">▶ Aula atual</span> : null}
                    </Link>
                  </li>
                );
              })}
            </ul>
          </li>
        ))}
      </ul>
    </nav>
  );
};
