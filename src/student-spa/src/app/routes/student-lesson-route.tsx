import { useParams } from 'react-router';

import { StudentLessonScreen } from '@/features/student-lessons/components/student-lesson-screen';

export const StudentLessonRoute = () => {
  const { lessonId = '' } = useParams();
  return <StudentLessonScreen key={lessonId} lessonId={lessonId} />;
};
