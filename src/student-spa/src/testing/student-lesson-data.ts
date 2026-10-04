export const lessonId = '00000000-0000-7000-8000-000000000010';
export const secondLessonId = '00000000-0000-7000-8000-000000000011';
export const studentLessonData = {
  lesson: { lessonId, moduleId: '00000000-0000-7000-8000-000000000020', title: 'Injeção de dependência', position: 1 },
  course: { courseId: '00000000-0000-7000-8000-000000000030', title: 'APIs com .NET', versionNumber: 2, modules: [
    { moduleId: '00000000-0000-7000-8000-000000000020', title: 'Fundamentos', position: 1, lessons: [
      { lessonId, title: 'Injeção de dependência', position: 1 },
      { lessonId: secondLessonId, title: 'Persistência', position: 2 },
    ] },
  ] },
};
