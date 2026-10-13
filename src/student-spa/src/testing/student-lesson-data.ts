export const lessonId = '00000000-0000-7000-8000-000000000010';
export const secondLessonId = '00000000-0000-7000-8000-000000000011';
export const lessonTranscriptionData = {
  lessonId, status: 'ready', revision: 'tr_12345678', language: 'pt-BR', durationMs: 60_000,
  segments: [
    { startMs: 0, endMs: 12_000, text: 'Introdução ao Docker, Docker e injeção de dependência.' },
    { startMs: 12_000, endMs: 30_000, text: 'Agora vamos criar o contêiner.' },
    { startMs: 30_000, endMs: 60_000, text: 'O docker compose encerra a aula. <script>literal</script>' },
  ],
};
export const studentLessonData = {
  lesson: { lessonId, moduleId: '00000000-0000-7000-8000-000000000020', title: 'Injeção de dependência', position: 1 },
  course: { courseId: '00000000-0000-7000-8000-000000000030', title: 'APIs com .NET', versionNumber: 2, modules: [
    { moduleId: '00000000-0000-7000-8000-000000000020', title: 'Fundamentos', position: 1, lessons: [
      { lessonId, title: 'Injeção de dependência', position: 1 },
      { lessonId: secondLessonId, title: 'Persistência', position: 2 },
    ] },
  ] },
};
