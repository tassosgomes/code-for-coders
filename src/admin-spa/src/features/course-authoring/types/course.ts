import { z } from 'zod';

const actorSchema = z.object({ name: z.string() }).strict();
export const courseLevelSchema = z.enum(['beginner', 'intermediate', 'advanced']).nullable();
export const recommendedCourseSchema = z.object({ courseId: z.string().uuid(), title: z.string() }).strict();
export type RecommendedCourse = z.infer<typeof recommendedCourseSchema>;
export const courseSummarySchema = z.object({
  courseId: z.string().uuid(), title: z.string(), status: z.enum(['draft', 'published']),
  currentVersion: z.number().int().positive().nullable(), hasUnpublishedChanges: z.boolean(),
  currentLevel: courseLevelSchema,
  lastEditedAt: z.string().datetime({ offset: true }), lastEditedBy: actorSchema,
}).strict();
export const lessonSchema = z.object({
  lessonId: z.string().uuid(), title: z.string(), description: z.string().nullable().optional(), position: z.number().int().positive(),
  video: z.object({ videoId: z.string().uuid(), title: z.string().optional(), durationSeconds: z.number().int().positive().optional() }).strict().nullable(),
}).strict();
export const moduleSchema = z.object({
  moduleId: z.string().uuid(), title: z.string(), position: z.number().int().positive(), lessons: z.array(lessonSchema).max(200),
}).strict();
export type CourseModule = z.infer<typeof moduleSchema>;
export type CourseLesson = z.infer<typeof lessonSchema>;
export const courseSchema = courseSummarySchema.extend({
  description: z.string().nullable().optional(), draftRevision: z.number().int().positive(),
  level: courseLevelSchema,
  prerequisite: z.object({ text: z.string().nullable(), recommendedCourses: z.array(recommendedCourseSchema).max(5) }).strict(),
  createdAt: z.string().datetime({ offset: true }), createdBy: actorSchema, modules: z.array(moduleSchema).max(100),
}).strict();
export const coursePageSchema = z.object({
  data: z.array(courseSummarySchema),
  pagination: z.object({ page: z.number().int().positive(), size: z.number().int().positive(), total: z.number().int().nonnegative(), totalPages: z.number().int().nonnegative() }).strict(),
}).strict();
export type CourseSummary = z.infer<typeof courseSummarySchema>;
export type Course = z.infer<typeof courseSchema>;
export type CoursePage = z.infer<typeof coursePageSchema>;
