import { z } from 'zod';

const actorSchema = z.object({ name: z.string() }).strict();
export const courseSummarySchema = z.object({
  courseId: z.string().uuid(), title: z.string(), status: z.enum(['draft', 'published']),
  currentVersion: z.number().int().positive().nullable(), hasUnpublishedChanges: z.boolean(),
  lastEditedAt: z.string().datetime({ offset: true }), lastEditedBy: actorSchema,
}).strict();
export const courseSchema = courseSummarySchema.extend({
  description: z.string().nullable().optional(), draftRevision: z.number().int().positive(),
  createdAt: z.string().datetime({ offset: true }), createdBy: actorSchema, modules: z.array(z.unknown()),
}).strict();
export const coursePageSchema = z.object({
  data: z.array(courseSummarySchema),
  pagination: z.object({ page: z.number().int().positive(), size: z.number().int().positive(), total: z.number().int().nonnegative(), totalPages: z.number().int().nonnegative() }).strict(),
}).strict();
export type CourseSummary = z.infer<typeof courseSummarySchema>;
export type Course = z.infer<typeof courseSchema>;
export type CoursePage = z.infer<typeof coursePageSchema>;
