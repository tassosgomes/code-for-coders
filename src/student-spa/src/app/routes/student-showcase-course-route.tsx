import { useParams } from 'react-router';

import { isShowcaseCourseNotFound, useShowcaseCourse } from '@/features/student-showcase/api/get-showcase-course';
import { StudentCoursePage } from '@/features/student-showcase/components/student-course-page';
import { useDocumentTitle } from '@/hooks/use-document-title';

export const StudentShowcaseCourseRoute = () => {
  const { courseId = '' } = useParams();
  const course = useShowcaseCourse(courseId);

  useDocumentTitle(course.data ? `${course.data.title} | Code4Coders` : 'Curso | Code4Coders');

  return (
    <StudentCoursePage
      onRetry={() => void course.refetch()}
      result={
        course.isError
          ? isShowcaseCourseNotFound(course.error)
            ? { status: 'not-found' }
            : { status: 'error' }
          : course.data
            ? { status: 'success', course: course.data }
            : { status: 'loading' }
      }
    />
  );
};
