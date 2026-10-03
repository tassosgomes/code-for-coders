import { useParams } from 'react-router';

import { StudentLessonScreen } from '@/features/student-lessons/components/student-lesson-screen';

import { useStudentSession } from '@/features/student-session/api/student-session';

export const StudentLessonRoute = () => {
  const { lessonId = '' } = useParams();
  const session = useStudentSession();
  return <StudentLessonScreen key={lessonId} lessonId={lessonId} csrfToken={session.data?.csrfToken ?? ''} />;
};
