import { Accordion } from 'radix-ui';
import { ChevronDown, Play } from 'lucide-react';
import { Link } from 'react-router';

import type { CourseProgress } from '@/features/student-lessons/api/get-course-progress';
import { paths } from '@/config/paths';
import type { StudentLessonScreenData } from '@/features/student-lessons/api/get-student-lesson';

export type StudentLessonNavProps = {
  course: StudentLessonScreenData['course'];
  currentLessonId?: string;
  progress?: CourseProgress;
  /** Dentro do painel com abas a lista não leva borda própria (evita card dentro de card). */
  framed?: boolean;
};

export const StudentLessonNav = ({ course, currentLessonId, progress, framed = true }: StudentLessonNavProps) => {
  return (
    <nav aria-label="Aulas do curso" className={`lesson-nav${framed ? ' lesson-nav-framed' : ''}`}>
      <h2 className="typo-overline text-primary">Aulas do curso</h2>
      {progress ? <div className="lesson-nav-progress">
        <p>{progress.percent}% · {progress.completedLessons} de {progress.totalLessons} aulas</p>
        <div role="progressbar" aria-label="Progresso do curso" aria-valuemin={0} aria-valuemax={100}
          aria-valuenow={progress.percent} aria-valuetext={`${progress.completedLessons} de ${progress.totalLessons} aulas concluídas`}
          className="lesson-nav-progress-track">
          <div className={progress.percent === 100 ? 'bg-success' : 'bg-primary'} style={{ width: `${progress.percent}%` }} />
        </div>
      </div> : null}
      <Accordion.Root type="multiple" defaultValue={course.modules.map((module) => module.moduleId)} asChild>
        <ul className="lesson-nav-modules">
          {course.modules.map((module) => (
            <Accordion.Item key={module.moduleId} value={module.moduleId} asChild>
              <li>
                <Accordion.Header asChild>
                  <h3>
                    <Accordion.Trigger className="lesson-nav-module">
                      <ChevronDown aria-hidden="true" />
                      <span>Módulo {module.position} · {module.title}</span>
                    </Accordion.Trigger>
                  </h3>
                </Accordion.Header>
                <Accordion.Content asChild>
                  <ul className="lesson-nav-lessons">
                    {module.lessons.map((item) => {
                      const isCurrent = item.lessonId === currentLessonId;
                      const completed = progress?.lessons.some((lesson) => lesson.lessonId === item.lessonId && lesson.completed);
                      return (
                        <li key={item.lessonId}>
                          <Link
                            to={paths.studentLesson.getHref(item.lessonId)}
                            state={{ course, knownLessonId: item.lessonId }}
                            aria-current={isCurrent ? 'page' : undefined}
                            className="lesson-nav-item"
                          >
                            <span aria-hidden="true" className="lesson-nav-marker">
                              {isCurrent ? <Play /> : completed ? '✓' : null}
                            </span>
                            <span aria-hidden="true" className="lesson-nav-number">{item.position}</span>
                            <span className="lesson-nav-text">
                              <span className="lesson-nav-title"><span className="sr-only">{item.position}. </span>{item.title}</span>
                              {completed ? <span className="lesson-nav-badge"><span aria-hidden="true">✓ </span>Concluída</span> : null}
                              {isCurrent ? <span className="lesson-nav-current">Aula atual</span> : null}
                            </span>
                          </Link>
                        </li>
                      );
                    })}
                  </ul>
                </Accordion.Content>
              </li>
            </Accordion.Item>
          ))}
        </ul>
      </Accordion.Root>
    </nav>
  );
};
