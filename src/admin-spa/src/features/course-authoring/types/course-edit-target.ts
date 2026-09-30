import type { CourseLesson, CourseModule } from '@/features/course-authoring/types/course';

export type CourseEditTarget =
  | { kind: 'course' }
  | { kind: 'create-module' }
  | { kind: 'module'; module: CourseModule }
  | { kind: 'create-lesson'; module: CourseModule }
  | { kind: 'lesson'; lesson: CourseLesson };
export type CourseRemoveTarget = { kind: 'module'; module: CourseModule } | { kind: 'lesson'; lesson: CourseLesson };
