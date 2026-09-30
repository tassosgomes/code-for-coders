import { CircleCheck, Clock, TriangleAlert } from 'lucide-react';

import type { CourseSummary } from '@/features/course-authoring/types/course';

type CourseStatusBadgeProps = { course: Pick<CourseSummary, 'status' | 'currentVersion' | 'hasUnpublishedChanges'> };
export const CourseStatusBadge = ({ course }: CourseStatusBadgeProps) => {
  if (course.status === 'draft') return <span className="course-status course-status-draft"><Clock size={14} /><span className="role-badge">Rascunho</span></span>;
  return <span className={`course-status ${course.hasUnpublishedChanges ? 'course-status-warning' : 'course-status-published'}`}>
    {course.hasUnpublishedChanges ? <TriangleAlert size={14} /> : <CircleCheck size={14} />}
    <span className="role-badge">Publicado · v{course.currentVersion}{course.hasUnpublishedChanges ? ' · alterações não publicadas' : ''}</span>
  </span>;
};
