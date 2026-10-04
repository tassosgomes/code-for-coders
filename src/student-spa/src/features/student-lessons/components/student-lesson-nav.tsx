import { Link } from 'react-router';

import { paths } from '@/config/paths';
import type { StudentLessonScreenData } from '@/features/student-lessons/api/get-student-lesson';

export type StudentLessonNavProps = {
  course: StudentLessonScreenData['course'];
  currentLessonId?: string;
};

export const StudentLessonNav = ({ course, currentLessonId }: StudentLessonNavProps) => {
  return (
    <nav aria-label="Aulas do curso" className="rounded-lg border p-4">
      <h2 className="typo-h4">Aulas do curso</h2>
      <p className="text-muted-foreground">{course.title}</p>
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
